using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NPCController : MonoBehaviour, IStunnable
{
    public enum NPCState { Patrol, Chase, Attack }
    public NPCState currentState = NPCState.Patrol;

    [Header("Stun")]
    public bool canBeStunned = true;
    public string stunParameter = "IsStunned";
    public bool freezeAnimatorDuringStun = true;
    public float postStunNoChaseDuration = 0.5f;
    public bool requireSightReacquireAfterStun = true;

    [Header("Target")]
    public Transform player;
    public bool autoFindPlayer = true;
    public string playerTag = "Player";

    [Header("Patrol")]
    public Transform[] waypoints;
    public float patrolSpeed = 4f;
    public float waypointStopDistance = 1f;

    [Header("Chase")]
    public float chaseSpeed = 40f;
    public float detectionRange = 30f;
    public float detectionAngle = 60f;

    [Header("Obstacle Blocking")]
    public bool blockByPhysicalObstacles = false;
    public LayerMask obstacleBlockMask = Physics.DefaultRaycastLayers;
    public float obstacleCheckRadius = 0.28f;
    public float obstacleCheckDistance = 0.9f;
    public float obstacleCheckHeight = 0.9f;
    public bool useAgentRadiusForObstacleCheck = true;
    public bool preventObstaclePenetration = false;
    public float penetrationCheckRadius = 0.34f;
    public float penetrationCheckHeight = 1.75f;

    [Header("Attack")]
    public float attackRange = 20f;
    public string attackParameter = "IsAttacking";
    public float attackDistanceBuffer = 0.75f;
    public int attackDamage = 1;
    public float attackHitInterval = 1.0f;
    public float attackAnimationDuration = 0.9f;
    public bool enableCrowdedAttackAssist = true;
    public float crowdedAttackExtraDistance = 0.75f;
    public float crowdedPlayerCheckRadius = 2.0f;

    [Header("Animasi")]
    public string walkParameter = "IsWalking";
    public string runParameter = "IsRunning";

    [Header("Investigasi Alarm")]
    public float investigateSpeedMultiplier = 1.6f;
    public float investigateStopDistance = 2f;

    [Header("Debug")]
    public bool showGizmos = true;

    [Header("Vision Light")]
    public bool autoCreateVisionLight = true;
    public bool alwaysShowVisionMarker = true;
    public bool lightOnDuringPatrol = false;
    public bool lightOnDuringChase = true;
    public bool lightOnDuringAttack = true;
    public float patrolLightIntensity = 3f;
    public float chaseLightIntensity = 5f;
    public float attackLightIntensity = 6f;

    [Header("Audio - NPC Marah")]
    public AudioClip alertClip;
    [Range(0f, 1f)] public float alertVolume = 0.6f;
    public float alertCooldown = 2f;
    [Range(0f, 1f)] public float alertSpatialBlend = 1f;
    public float alertMinDistance = 2f;
    public float alertMaxDistance = 40f;
    public bool forceAlert2D = true;

    private NavMeshAgent agent;
    private int currentWaypointIndex = 0;
    private Animator animator;
    private Light visionLight;
    private float nextPlayerSearchTime = 0f;
    private float stunTimer = 0f;
    private bool wasStunnedLastFrame = false;
    private HashSet<string> animatorBoolParams = new HashSet<string>();
    private float cachedAnimatorSpeed = 1f;
    private bool animatorFrozenByStun = false;
    private float postStunTimer = 0f;
    private bool needsSightReacquire = false;
    private bool wasPlayerDetectedLastFrame = false;
    private float lastAlertTime = -999f;
    private AudioSource alertAudioSource;
    private NPCState previousState;
    private CharacterController playerController;
    private PlayerHealth playerHealth;
    private Collider[] nearbyNpcBuffer = new Collider[16];
    private float lastAttackHitTime = -999f;
    private float attackStateTimer = 0f;
    private bool attackDamageTriggeredThisCycle = false;
    private Vector3 investigatePosition;
    private float investigateTimer = 0f;

    public bool IsStunned => stunTimer > 0f;

    // Dipanggil saat alarm berbunyi; NPC bergerak menyelidiki posisi sumber suara.
    public void InvestigateNoise(Vector3 position, float duration = 12f)
    {
        investigatePosition = position;
        investigateTimer = Mathf.Max(investigateTimer, duration);
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        // Root motion animasi bertabrakan dengan NavMeshAgent; gerak diatur agent.
        if (animator != null)
            animator.applyRootMotion = false;
        CacheAnimatorBoolParameters();
        TryAssignPlayer();

        // Jika tidak ada NavMesh di area spawn, nonaktifkan agent agar NPC tidak warp
        if (agent != null)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, 15f, NavMesh.AllAreas))
            {
                agent.enabled = false;
                Debug.LogWarning($"NPCController [{name}]: Tidak ada NavMesh di area ini, NavMeshAgent dinonaktifkan.");
            }
            else
            {
                agent.Warp(hit.position);
            }
        }
        playerHealth = ResolvePlayerHealth(player);

        // Cari spotlight di children NPC
        visionLight = GetComponentInChildren<Light>();

        if (visionLight == null && autoCreateVisionLight)
        {
            GameObject lightObject = new GameObject("Vision Light");
            lightObject.transform.SetParent(transform);
            lightObject.transform.localPosition = new Vector3(0f, 1.8f, 0.2f);
            lightObject.transform.localRotation = Quaternion.identity;
            visionLight = lightObject.AddComponent<Light>();
        }

        if (visionLight != null)
        {
            visionLight.enabled = true;
            visionLight.type = LightType.Spot;
            visionLight.color = new Color(1f, 0.9f, 0f);
            visionLight.intensity = patrolLightIntensity;
            visionLight.range = detectionRange;
            visionLight.spotAngle = detectionAngle * 2f;
        }

        if (waypoints.Length > 0)
            GoToNextWaypoint();

        TryAutoAssignAlertClip();
        EnsureAlertAudioSource();
        previousState = currentState;
    }

    void Update()
    {
        if (postStunTimer > 0f)
            postStunTimer -= Time.deltaTime;

        if (IsStunned)
        {
            stunTimer -= Time.deltaTime;
            HandleStunned();
            wasStunnedLastFrame = true;
            return;
        }

        if (wasStunnedLastFrame)
        {
            wasStunnedLastFrame = false;
            if (animator != null && HasAnimatorBool(stunParameter))
                animator.SetBool(stunParameter, false);

            if (animator != null && animatorFrozenByStun)
            {
                animator.speed = cachedAnimatorSpeed;
                animatorFrozenByStun = false;
            }

            SyncAnimatorByState();
        }

        if (player == null)
        {
            if (autoFindPlayer && Time.time >= nextPlayerSearchTime)
            {
                TryAssignPlayer();
                nextPlayerSearchTime = Time.time + 1f;
            }

            // Tetap patroli walau target player belum ditemukan.
            currentState = NPCState.Patrol;
            HandlePatrol();
            SyncAnimatorByState();
            UpdateVisionLightByState();
            return;
        }

        if (needsSightReacquire)
        {
            bool hasSightAgain = IsPlayerInSight();
            if (!hasSightAgain)
            {
                currentState = NPCState.Patrol;
                HandlePatrol();

                if (animator != null)
                {
                    if (HasAnimatorBool(walkParameter)) animator.SetBool(walkParameter, true);
                    if (HasAnimatorBool(runParameter)) animator.SetBool(runParameter, false);
                    if (HasAnimatorBool(attackParameter)) animator.SetBool(attackParameter, false);
                }

                UpdateVisionLightByState();

                return;
            }

            needsSightReacquire = false;
        }

        if (postStunTimer > 0f)
        {
            currentState = NPCState.Patrol;
            HandlePatrol();

            if (animator != null)
            {
                if (HasAnimatorBool(walkParameter)) animator.SetBool(walkParameter, true);
                if (HasAnimatorBool(runParameter)) animator.SetBool(runParameter, false);
                if (HasAnimatorBool(attackParameter)) animator.SetBool(attackParameter, false);
            }

            UpdateVisionLightByState();

            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);
        Vector3 npcFlatPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlatPos = new Vector3(player.position.x, 0f, player.position.z);
        float flatDistToPlayer = Vector3.Distance(npcFlatPos, playerFlatPos);
        float effectiveAttackDistance = attackRange;
        if (agent != null)
            effectiveAttackDistance = Mathf.Max(attackRange, agent.stoppingDistance + attackDistanceBuffer);

        float playerRadius = 0f;
        if (playerController == null && player != null)
            playerController = player.GetComponent<CharacterController>();
        if (playerController != null)
            playerRadius = playerController.radius;

        effectiveAttackDistance += playerRadius;

        // Give a small extra margin when agents crowd each other around the player.
        float crowdAwareAttackDistance = effectiveAttackDistance + (agent != null ? agent.radius : 0f);
        if (enableCrowdedAttackAssist && IsCrowdedNearPlayer())
            crowdAwareAttackDistance += crowdedAttackExtraDistance;
        bool playerDetected = IsPlayerInSight();
        HandleAlertTransition(playerDetected);

        switch (currentState)
        {
            case NPCState.Patrol:
                HandlePatrol();
                // Prioritas tertinggi: kalau sudah sangat dekat, langsung attack
                if (flatDistToPlayer <= crowdAwareAttackDistance)
                {
                    currentState = NPCState.Attack;
                    attackStateTimer = 0f;
                    attackDamageTriggeredThisCycle = false;
                    if (animator != null && HasAnimatorBool(attackParameter))
                        animator.SetBool(attackParameter, true);
                    if (agent != null && agent.isOnNavMesh) agent.ResetPath();
                }
                else if (playerDetected)
                {
                    currentState = NPCState.Chase;
                }
                break;

            case NPCState.Chase:
                HandleChase();
                // Prioritas 1: kalau sudah dekat, attack! (prioritas LEBIH TINGGI dari deteksi)
                if (flatDistToPlayer <= crowdAwareAttackDistance)
                {
                    currentState = NPCState.Attack;
                    attackStateTimer = 0f;
                    attackDamageTriggeredThisCycle = false;
                    if (animator != null && HasAnimatorBool(attackParameter))
                        animator.SetBool(attackParameter, true);
                    if (agent != null && agent.isOnNavMesh) agent.ResetPath();
                }
                // Prioritas 2: kalau player hilang atau terlalu jauh
                else if (!playerDetected || distToPlayer > detectionRange * 1.5f)
                {
                    currentState = NPCState.Patrol;
                    GoToNextWaypoint();
                }
                break;

            case NPCState.Attack:
                HandleAttack();

                if (flatDistToPlayer > crowdAwareAttackDistance * 1.2f)
                {
                    currentState = NPCState.Chase;
                    attackStateTimer = 0f;
                    attackDamageTriggeredThisCycle = false;
                    if (animator != null && HasAnimatorBool(attackParameter))
                        animator.SetBool(attackParameter, false);
                    if (agent != null && agent.isOnNavMesh)
                        agent.speed = chaseSpeed;
                }
                else if (!playerDetected && flatDistToPlayer > detectionRange * 1.2f)
                {
                    currentState = NPCState.Patrol;
                    attackStateTimer = 0f;
                    attackDamageTriggeredThisCycle = false;
                    if (animator != null && HasAnimatorBool(attackParameter))
                        animator.SetBool(attackParameter, false);
                    GoToNextWaypoint();
                }
                break;
        }

        if (currentState != previousState)
        {
            if (currentState == NPCState.Chase || currentState == NPCState.Attack)
                TryPlayAlertSound();

            previousState = currentState;
        }

        // Update animasi
        SyncAnimatorByState();

        UpdateVisionLightByState();

        if (currentState != NPCState.Attack)
        {
            if (animator != null && HasAnimatorBool(attackParameter))
                animator.SetBool(attackParameter, false);
        }

        // Warp recovery dimatikan agar NPC tidak snap balik ke posisi spawn.
    }

    bool IsPlayerInSight()
    {
        Vector3 directionToPlayer = player.position - transform.position;
        float distance = directionToPlayer.magnitude;

        // Cek jarak
        if (distance > detectionRange) return false;

        // Cek apakah player ada di depan NPC (dalam sudut pandang)
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > detectionAngle) return false;

        // Cek apakah ada halangan antara NPC dan player
        RaycastHit[] hits = Physics.RaycastAll(
            transform.position + Vector3.up,
            directionToPlayer.normalized,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.transform;

                if (hitTransform == null)
                    continue;

                // Ignore own colliders and other NPC colliders so crowds do not block LOS.
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;

                NPCController hitNpc = hitTransform.GetComponentInParent<NPCController>();
                if (hitNpc != null)
                    continue;

                if (hitTransform == player || hitTransform.IsChildOf(player))
                    return true;

                return false; // ada tembok/objek lain menghalangi
            }
        }

        return true;
    }

    void HandlePatrol()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (agent.isStopped)
            agent.isStopped = false;

        // Prioritaskan penyelidikan alarm di atas rute waypoint.
        if (investigateTimer > 0f)
        {
            investigateTimer -= Time.deltaTime;
            agent.speed = patrolSpeed * Mathf.Max(1f, investigateSpeedMultiplier);
            agent.SetDestination(investigatePosition);

            Vector3 selfFlat = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 targetFlat = new Vector3(investigatePosition.x, 0f, investigatePosition.z);
            if (Vector3.Distance(selfFlat, targetFlat) <= Mathf.Max(0.5f, investigateStopDistance))
                investigateTimer = 0f;

            return;
        }

        if (waypoints.Length == 0) return;

        agent.speed = patrolSpeed;

        if (!agent.pathPending && agent.remainingDistance < waypointStopDistance)
        {
            GoToNextWaypoint();
        }
    }

    void GoToNextWaypoint()
    {
        if (waypoints.Length == 0) return;
        if (agent == null || !agent.isOnNavMesh) return;
        agent.SetDestination(waypoints[currentWaypointIndex].position);
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    void HandleChase()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (agent.isStopped)
            agent.isStopped = false;

        agent.speed = chaseSpeed;
        agent.acceleration = 999f;
        agent.angularSpeed = 999f;
        agent.SetDestination(player.position);
    }

    bool IsForwardPathBlockedByObstacle()
    {
        if (player == null)
            return false;

        Vector3 toTarget = player.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return false;

        Vector3 dir = toTarget.normalized;
        float checkDistance = Mathf.Max(0.1f, obstacleCheckDistance);
        float checkRadius = Mathf.Max(0.05f, obstacleCheckRadius);
        if (useAgentRadiusForObstacleCheck && agent != null)
            checkRadius = Mathf.Max(checkRadius, agent.radius * 0.9f);

        float halfHeight = Mathf.Max(0.6f, obstacleCheckHeight);
        Vector3 bottom = transform.position + Vector3.up * 0.1f;
        Vector3 top = transform.position + Vector3.up * (halfHeight * 2f);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            checkRadius,
            dir,
            checkDistance,
            obstacleBlockMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform == null)
                continue;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (player != null && (hitTransform == player || hitTransform.IsChildOf(player)))
                continue;

            if (hitTransform.GetComponentInParent<NPCController>() != null)
                continue;

            return true;
        }

        return false;
    }

    void HandleAttack()
    {
        if (player == null)
            return;

        if (currentState != NPCState.Attack)
            return;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        attackStateTimer += Time.deltaTime;

        bool attackClipStarted = false;
        if (animator != null)
        {
            bool attackStateFlag = HasAnimatorBool(attackParameter) && animator.GetBool(attackParameter);
            attackClipStarted = attackStateFlag;

            if (!attackClipStarted)
            {
                AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
                if (clipInfos != null)
                {
                    for (int i = 0; i < clipInfos.Length; i++)
                    {
                        AnimationClip clip = clipInfos[i].clip;
                        if (clip != null && clip.name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            attackClipStarted = true;
                            break;
                        }
                    }
                }
            }
        }

        float damageTriggerTime = Mathf.Max(0.2f, attackAnimationDuration * 0.65f);
        float damageWindowEnd = damageTriggerTime + Mathf.Max(0.15f, attackHitInterval);

        // Hanya damage setelah animasi serangan benar-benar aktif, bukan saat state attack baru masuk.
        if (attackClipStarted && attackStateTimer >= damageTriggerTime && !attackDamageTriggeredThisCycle)
        {
            TryDamagePlayer();
            attackDamageTriggeredThisCycle = true;
        }

        // Hanya satu damage per siklus serangan.
        if (attackStateTimer >= damageWindowEnd)
        {
            attackStateTimer = 0f;
            attackDamageTriggeredThisCycle = false;
        }

    }

    bool TryDamagePlayer()
    {
        if (player == null)
            return false;

        if (currentState != NPCState.Attack)
            return false;

        if (Time.time - lastAttackHitTime < Mathf.Max(0.05f, attackHitInterval))
            return false;

        if (playerHealth == null)
            playerHealth = ResolvePlayerHealth(player);

        if (playerHealth == null || playerHealth.IsDead)
            return false;

        // Paksa damage saat animasi serang aktif, tanpa bergantung pada collider player.
        // Ini membuat attack tetap terasa realistis meski hitbox karakter tidak bersentuhan.
        bool hitApplied = playerHealth.TryTakeHit(Mathf.Max(1, attackDamage), GetInstanceID());
        if (hitApplied)
            lastAttackHitTime = Time.time;

        return hitApplied;
    }

    PlayerHealth ResolvePlayerHealth(Transform target)
    {
        if (target == null)
            return FindFirstObjectByType<PlayerHealth>();

        PlayerHealth found = target.GetComponent<PlayerHealth>();
        if (found != null)
            return found;

        found = target.GetComponentInParent<PlayerHealth>();
        if (found != null)
            return found;

        if (target.root != null)
        {
            PlayerHealth rootHealth = target.root.GetComponent<PlayerHealth>();
            if (rootHealth != null)
                return rootHealth;
        }

        return FindFirstObjectByType<PlayerHealth>();
    }

    bool IsCrowdedNearPlayer()
    {
        if (player == null)
            return false;

        int hitCount = Physics.OverlapSphereNonAlloc(
            player.position,
            Mathf.Max(0.1f, crowdedPlayerCheckRadius),
            nearbyNpcBuffer,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        int nearbyNpcCount = 0;
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = nearbyNpcBuffer[i];
            if (col == null)
                continue;

            NPCController npc = col.GetComponentInParent<NPCController>();
            if (npc == null)
                continue;

            if (npc == this)
                continue;

            nearbyNpcCount++;
            if (nearbyNpcCount >= 1)
                return true;
        }

        return false;
    }

    void HandleStunned()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (animator != null)
        {
            if (HasAnimatorBool(walkParameter)) animator.SetBool(walkParameter, false);
            if (HasAnimatorBool(runParameter)) animator.SetBool(runParameter, false);
            if (HasAnimatorBool(attackParameter)) animator.SetBool(attackParameter, false);
            if (HasAnimatorBool(stunParameter))
                animator.SetBool(stunParameter, true);

            if (freezeAnimatorDuringStun && !animatorFrozenByStun)
            {
                cachedAnimatorSpeed = animator.speed;
                animator.speed = 0f;
                animatorFrozenByStun = true;
            }
        }

        if (visionLight != null)
        {
            visionLight.color = Color.cyan;
            visionLight.intensity = attackLightIntensity;
        }
    }

    public void ApplyStun(float duration)
    {
        if (!canBeStunned)
            return;

        stunTimer = Mathf.Max(stunTimer, duration);
        postStunTimer = Mathf.Max(postStunTimer, postStunNoChaseDuration);
        currentState = NPCState.Patrol;

        if (requireSightReacquireAfterStun)
            needsSightReacquire = true;
    }

    void UpdateVisionLightByState()
    {
        if (visionLight == null)
            return;

        visionLight.enabled = true;

        if (currentState == NPCState.Attack)
        {
            visionLight.color = new Color(1f, 0.5f, 0f); // orange
            visionLight.intensity = attackLightIntensity;
        }
        else if (currentState == NPCState.Chase)
        {
            visionLight.color = Color.red;
            visionLight.intensity = chaseLightIntensity;
        }
        else
        {
            visionLight.color = new Color(1f, 0.9f, 0f); // kuning
            visionLight.intensity = patrolLightIntensity;
        }

        visionLight.range = detectionRange;
        visionLight.spotAngle = detectionAngle * 2f;
    }

    void SyncAnimatorByState()
    {
        if (animator == null)
            return;

        bool isPatrolling = currentState == NPCState.Patrol;
        bool isChasing = currentState == NPCState.Chase;
        bool isAttacking = currentState == NPCState.Attack;

        if (!string.IsNullOrEmpty(walkParameter))
        {
            if (HasAnimatorBool(walkParameter))
                animator.SetBool(walkParameter, isPatrolling);
        }

        if (!string.IsNullOrEmpty(runParameter))
        {
            if (HasAnimatorBool(runParameter))
                animator.SetBool(runParameter, isChasing);
        }

        if (!string.IsNullOrEmpty(attackParameter))
        {
            if (HasAnimatorBool(attackParameter))
                animator.SetBool(attackParameter, isAttacking);
        }

        if (HasAnimatorBool(stunParameter))
            animator.SetBool(stunParameter, false);
    }

    void CacheAnimatorBoolParameters()
    {
        animatorBoolParams.Clear();

        if (animator == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool)
                animatorBoolParams.Add(parameter.name);
        }
    }

    bool HasAnimatorBool(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        return animatorBoolParams.Contains(parameterName);
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Visualisasi range deteksi
        Gizmos.color = currentState == NPCState.Chase ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Visualisasi sudut pandang
        Vector3 leftDir = Quaternion.Euler(0, -detectionAngle, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, detectionAngle, 0) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + leftDir * detectionRange);
        Gizmos.DrawLine(transform.position, transform.position + rightDir * detectionRange);
    }

    void TryAssignPlayer()
    {
        if (player != null) return;

        GameObject playerObject = null;

        if (autoFindPlayer && !string.IsNullOrEmpty(playerTag))
            playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject == null)
            playerObject = GameObject.Find("Player");

        if (playerObject == null)
        {
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null)
                playerObject = playerMovement.gameObject;
        }

        if (playerObject != null)
            player = playerObject.transform;
    }

    void HandleAlertTransition(bool isDetectedNow)
    {
        if (isDetectedNow && !wasPlayerDetectedLastFrame)
        {
            TryPlayAlertSound();
            TryRegisterPlayerSpotted();
        }

        wasPlayerDetectedLastFrame = isDetectedNow;
    }

    void TryRegisterPlayerSpotted()
    {
        if (playerHealth == null)
            playerHealth = ResolvePlayerHealth(player);

        if (playerHealth != null && !playerHealth.IsDead)
            playerHealth.AddSpottedCount(1);
    }

    void EnsureAlertAudioSource()
    {
        if (alertAudioSource == null)
        {
            alertAudioSource = GetComponent<AudioSource>();
            if (alertAudioSource == null)
                alertAudioSource = gameObject.AddComponent<AudioSource>();
        }

        alertAudioSource.playOnAwake = false;
        alertAudioSource.loop = false;
        alertAudioSource.spatialBlend = forceAlert2D ? 0f : Mathf.Clamp01(alertSpatialBlend);
        alertAudioSource.volume = Mathf.Clamp01(alertVolume);
        alertAudioSource.minDistance = Mathf.Max(0.1f, alertMinDistance);
        alertAudioSource.maxDistance = Mathf.Max(alertAudioSource.minDistance + 0.1f, alertMaxDistance);
        alertAudioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    void TryPlayAlertSound()
    {
        if (alertClip == null)
            return;

        if (Time.time - lastAlertTime < Mathf.Max(0.05f, alertCooldown))
            return;

        EnsureAlertAudioSource();
        alertAudioSource.spatialBlend = forceAlert2D ? 0f : Mathf.Clamp01(alertSpatialBlend);
        alertAudioSource.volume = Mathf.Clamp01(alertVolume);
        alertAudioSource.minDistance = Mathf.Max(0.1f, alertMinDistance);
        alertAudioSource.maxDistance = Mathf.Max(alertAudioSource.minDistance + 0.1f, alertMaxDistance);
        alertAudioSource.PlayOneShot(alertClip, Mathf.Clamp01(alertVolume));
        Debug.Log($"{name}: NPC Marah diputar.");
        lastAlertTime = Time.time;
    }

    void TryAutoAssignAlertClip()
    {
        if (alertClip != null)
            return;

#if UNITY_EDITOR
        alertClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Scenes/Lantai 6/Sound/NPC Marah.mp3");
#endif
    }
}
