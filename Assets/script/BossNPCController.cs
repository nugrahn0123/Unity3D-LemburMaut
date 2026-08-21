using UnityEngine;
using System;
using UnityEngine.AI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class BossNPCController : MonoBehaviour, IStunnable
{
    public enum BossState { Idle, Chase, Attack }
    private enum DashState { Ready, Windup, Dashing, Attacking, Cooldown }

    [Header("Aktivasi")]
    [Tooltip("Centang jika boss langsung aktif. Biarkan false jika diaktifkan lewat BossRoomTrigger.")]
    public bool activateOnStart = false;
    public bool isActivated = false;

    [Header("State")]
    public BossState currentState = BossState.Idle;

    [Header("Target")]
    public Transform player;
    public bool autoFindPlayer = true;
    public string playerTag = "Player";

    [Header("Spawn")]
    public Transform spawnPoint;
    public bool snapToSpawnOnStart = false;
    public bool autoFindSpawnPointByName = false;
    public string spawnPointName = "Titik Diam Bos";
    public bool useSpawnRotation = true;
    public float spawnSearchRadius = 2f;
    public float spawnSearchStep = 0.5f;

    [Header("Movement")]
    public bool useNavMeshAgentMovement = false;
    public float moveSpeed = 5.5f;
    public float rotationSpeed = 9f;
    public float stopDistance = 1.6f;

    [Header("Collision")]
    public LayerMask obstacleMask = Physics.DefaultRaycastLayers;
    public float obstacleCheckRadius = 0.45f;
    public float obstacleCheckHeight = 1.8f;
    public float obstacleCheckPadding = 0.08f;
    public float minBlockingHitDistance = 0.03f;
    public float steerAngleStep = 25f;
    public int steerProbeCount = 2;
    public float stuckEscapeDistance = 0.5f;

    [Header("Detection")]
    public float detectionRange = 30f;
    public float detectionAngle = 120f;
    public float loseTargetDistanceMultiplier = 1.5f;
    public bool unlimitedVisionDistanceAfterActivation = true;

    [Header("Attack")]
    public float attackRange = 2.2f;
    public int attackDamage = 1;
    public float attackHitInterval = 1f;
    public float attackAnimationDuration = 0.85f;
    public float postAttackWalkDuration = 1.2f;

    [Header("Dash")]
    public bool enableDash = true;
    public float dashTriggerRange = 12f;
    public float dashMinRange = 3f;
    public float dashSpeed = 10f;
    public float dashDuration = 0.35f;
    public float dashCooldown = 2f;
    public float dashWindupDuration = 0.45f;
    public int dashDamage = 2;
    public float dashHitRadius = 1.8f;

    [Header("Stun")]
    public bool canBeStunned = true;
    public string stunParameter = "IsStunned";
    public bool freezeAnimatorDuringStun = true;
    public float postStunNoChaseDuration = 0.5f;
    public bool forceIdleAnimationOnStun = true;

    [Header("Animator")]
    public string walkParameter = "IsWalking";
    public string runParameter = "IsRunning";
    public string attackParameter = "IsAttacking";
    public string idleStateName = "Idle";
    public string walkStateName = "Running";
    public string runningStateName = "Running";
    public string attackStateName = "Standing 1H Magic Attack 03";
    public float animationCrossFade = 0.08f;
    [Range(0f, 1f)] public float idleStateNormalizedTime = 0f;

    [Header("Audio - NPC Marah")]
    public AudioClip alertClip;
    [Range(0f, 1f)] public float alertVolume = 0.6f;
    public float alertCooldown = 2f;
    [Range(0f, 1f)] public float alertSpatialBlend = 1f;
    public float alertMinDistance = 2f;
    public float alertMaxDistance = 40f;
    public bool forceAlert2D = true;

    private Animator animator;
    private NavMeshAgent agent;
    private CharacterController playerController;
    private PlayerHealth playerHealth;
    private readonly HashSet<string> animatorBoolParams = new HashSet<string>();
    private float cachedAnimatorSpeed = 1f;
    private bool animatorFrozenByStun = false;
    private float stunTimer = 0f;
    private float postStunTimer = 0f;
    private float nextPlayerSearchTime = 0f;
    private float lastAttackHitTime = -999f;
    private float lastAlertTime = -999f;
    private bool wasInAttackStateLastFrame = false;
    private AudioSource alertAudioSource;
    private BossState previousState;
    private DashState dashState = DashState.Ready;
    private float dashStateTimer = 0f;
    private float dashCooldownTimer = 0f;
    private float attackRecoveryTimer = 0f;
    private Vector3 dashDirection = Vector3.zero;
    private bool attackHitAppliedThisCycle = false;
    private string currentAnimationState = string.Empty;
    private float introTimer = 0f;
    private bool hasUnlimitedVisionDistance = false;
    private float lockedY;
    private bool hasLockedY;

    public bool IsStunned => stunTimer > 0f;
    public bool IsAggressive => currentState == BossState.Chase || currentState == BossState.Attack;
    public bool IsBusy => dashState == DashState.Windup || dashState == DashState.Dashing || dashState == DashState.Attacking;

    void Awake()
    {
        if (!Application.isPlaying)
            ResolveSpawnPoint();
    }

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        // Root motion animasi bisa menggeser/menenggelamkan boss; script yang mengatur gerak.
        if (animator != null)
            animator.applyRootMotion = false;
        CacheAnimatorBoolParameters();
        agent = GetComponent<NavMeshAgent>();
        snapToSpawnOnStart = false;
        autoFindSpawnPointByName = false;
        spawnPoint = null;
        NormalizeAnimatorSettings();
        ConfigureAgent();
        TryAssignPlayer();
        playerHealth = ResolvePlayerHealth(player);
        TryAutoAssignAlertClip();
        EnsureAlertAudioSource();
        currentState = BossState.Idle;
        previousState = currentState;
        isActivated = activateOnStart;
        hasUnlimitedVisionDistance = activateOnStart && unlimitedVisionDistanceAfterActivation;
        LockCurrentHeight();
        SyncAnimatorByState();
    }

    // Y dikunci karena boss hanya bergerak horizontal; mencegah drift dari root motion.
    public void LockCurrentHeight()
    {
        lockedY = transform.position.y;
        hasLockedY = true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ResolveSpawnPoint();

        if (!snapToSpawnOnStart || spawnPoint == null)
            return;

        transform.position = spawnPoint.position;

        if (useSpawnRotation)
            transform.rotation = spawnPoint.rotation;
    }
#endif

    void Update()
    {
        if (!Application.isPlaying)
            return;

        KeepBossUpright();

        if (!isActivated)
            return;

        if (postStunTimer > 0f)
            postStunTimer -= Time.deltaTime;

        if (attackRecoveryTimer > 0f)
            attackRecoveryTimer -= Time.deltaTime;

        if (IsStunned)
        {
            stunTimer -= Time.deltaTime;
            HandleStunned();
            return;
        }

        RestoreAnimatorAfterStun();
        UpdateDashCooldown();

        if (introTimer > 0f)
        {
            introTimer -= Time.deltaTime;
            currentState = BossState.Idle;
            if (player != null) MoveTowardsPlayer();
            SyncAnimatorByState();
            if (introTimer <= 0f)
                LaunchOpeningAttack();
            return;
        }

        if (player == null)
        {
            if (autoFindPlayer && Time.time >= nextPlayerSearchTime)
            {
                TryAssignPlayer();
                nextPlayerSearchTime = Time.time + 1f;
            }

            currentState = BossState.Idle;
            SyncAnimatorByState();
            return;
        }

        if (UpdateDashState())
        {
            SyncAnimatorByState();
            return;
        }

        float flatDistanceToPlayer = HorizontalDistance(transform.position, player.position);
        float effectiveAttackRange = attackRange + GetPlayerRadius();
        HandleAlertTransition(true);

        if (postStunTimer > 0f)
        {
            currentState = BossState.Idle;
            SyncAnimatorByState();
            return;
        }

        switch (currentState)
        {
            case BossState.Idle:
                MoveTowardsPlayer();
                if (IsPlayerInSight())
                    currentState = BossState.Chase;
                break;

            case BossState.Chase:
                if (TryStartDash(flatDistanceToPlayer))
                {
                    currentState = BossState.Attack;
                    SyncAnimatorByState();
                    return;
                }
                // Tetap kejar player saat dash belum siap.
                MoveTowardsPlayer();
                break;

            case BossState.Attack:
                if (dashState != DashState.Attacking)
                {
                    if (flatDistanceToPlayer <= effectiveAttackRange && attackRecoveryTimer <= 0f)
                    {
                        BeginAttackSequence();
                        SyncAnimatorByState();
                        return;
                    }
                }
                break;
        }

        if (currentState != previousState)
        {
            if (currentState == BossState.Chase || currentState == BossState.Attack)
                TryPlayAlertSound();

            previousState = currentState;
        }

        if (currentState != BossState.Attack)
            wasInAttackStateLastFrame = false;

        SyncAnimatorByState();
    }

    void MoveTowardsPlayer()
    {
        if (player == null)
            return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= stopDistance * stopDistance)
            return;

        Vector3 direction = toPlayer.normalized;
        FaceDirection(direction);

        float moveDistance = moveSpeed * Time.deltaTime;
        if (TryMove(direction, moveDistance))
            return;

        for (int i = 1; i <= Mathf.Max(1, steerProbeCount); i++)
        {
            float angle = steerAngleStep * i;

            Vector3 leftDirection = Quaternion.Euler(0f, -angle, 0f) * direction;
            if (TryMove(leftDirection.normalized, moveDistance))
                return;

            Vector3 rightDirection = Quaternion.Euler(0f, angle, 0f) * direction;
            if (TryMove(rightDirection.normalized, moveDistance))
                return;
        }

        // Jika jalur ke depan buntu, coba langkah escape aman tanpa menembus obstacle.
        TryEscapeFromObstacle(direction, Mathf.Max(0.2f, stuckEscapeDistance));
    }

    bool TryEscapeFromObstacle(Vector3 blockedForward, float escapeDistance)
    {
        if (blockedForward.sqrMagnitude <= 0.0001f)
            return false;

        float moveDistance = Mathf.Max(0.05f, escapeDistance);
        Vector3 forward = blockedForward.normalized;
        Vector3 back = -forward;
        Vector3 left = Quaternion.Euler(0f, -90f, 0f) * forward;
        Vector3 right = Quaternion.Euler(0f, 90f, 0f) * forward;
        Vector3 backLeft = (back + left).normalized;
        Vector3 backRight = (back + right).normalized;

        Vector3[] candidates = { back, left, right, backLeft, backRight };
        foreach (Vector3 candidate in candidates)
        {
            if (TryMove(candidate, moveDistance))
                return true;
        }

        return false;
    }

    bool TryMove(Vector3 direction, float moveDistance)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        float stepDistance = Mathf.Max(0.02f, moveDistance);
        for (int i = 0; i < 4; i++)
        {
            if (!IsObstacleBlocking(direction, stepDistance))
            {
                FaceDirection(direction);
                transform.position += direction * stepDistance;
                return true;
            }

            stepDistance *= 0.5f;
            if (stepDistance < 0.02f)
                break;
        }

        return false;
    }

    bool TryMoveWithAgent()
    {
        if (!useNavMeshAgentMovement)
            return false;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stopDistance;
        agent.isStopped = false;
        if (!agent.SetDestination(player.position))
            return false;

        if (!agent.pathPending && !agent.hasPath)
            return false;

        Vector3 desiredVelocity = agent.desiredVelocity;
        desiredVelocity.y = 0f;
        if (desiredVelocity.sqrMagnitude > 0.0001f)
        {
            FaceDirection(desiredVelocity.normalized);

            return true;
        }

        if (agent.pathPending)
            return true;

        return false;
    }

    void UpdateDashCooldown()
    {
        if (dashState != DashState.Cooldown)
            return;

        dashCooldownTimer += Time.deltaTime;
        if (dashCooldownTimer >= Mathf.Max(0.05f, dashCooldown))
            dashState = DashState.Ready;
    }

    bool UpdateDashState()
    {
        if (!enableDash)
            return false;

        switch (dashState)
        {
            case DashState.Windup:
                currentState = BossState.Idle;
                dashStateTimer -= Time.deltaTime;
                FacePlayer();
                if (dashStateTimer <= 0f)
                    StartDash();
                return true;

            case DashState.Dashing:
                currentState = BossState.Chase;
                dashStateTimer -= Time.deltaTime;
                PerformDash();
                if (dashStateTimer <= 0f)
                    BeginAttackSequence();
                return true;

            case DashState.Attacking:
                currentState = BossState.Attack;
                dashStateTimer -= Time.deltaTime;
                FacePlayer();

                if (!attackHitAppliedThisCycle)
                {
                    TryDamagePlayer();
                    attackHitAppliedThisCycle = true;
                }

                if (dashStateTimer <= 0f)
                    EndAttackSequence();
                return true;
        }

        return false;
    }

    bool TryStartDash(float flatDistanceToPlayer)
    {
        if (!enableDash || player == null || dashState != DashState.Ready)
            return false;

        if (attackRecoveryTimer > 0f)
            return false;

        if (flatDistanceToPlayer <= attackRange + GetPlayerRadius())
            return false;

        if (flatDistanceToPlayer < dashMinRange)
            return false;

        dashState = DashState.Windup;
        dashStateTimer = Mathf.Max(0.01f, dashWindupDuration);
        attackHitAppliedThisCycle = false;
        return true;
    }

    void StartDash()
    {
        if (player == null)
        {
            EndDash();
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude <= 0.0001f)
        {
            EndDash();
            return;
        }

        dashDirection = toPlayer.normalized;
        dashState = DashState.Dashing;
        dashStateTimer = Mathf.Max(0.01f, dashDuration);
        TryPlayDashSound();
    }

    void PerformDash()
    {
        if (dashDirection.sqrMagnitude <= 0.0001f)
        {
            EndDash();
            return;
        }

        FaceDirection(dashDirection);
        float moveDistance = dashSpeed * Time.deltaTime;
        if (IsObstacleBlocking(dashDirection, moveDistance))
        {
            // Menabrak obstacle saat dash: keluar dari dash dan coba geser aman agar tidak nyangkut.
            TryEscapeFromObstacle(dashDirection, Mathf.Max(stuckEscapeDistance, moveDistance));
            EndDash();
            return;
        }

        transform.position += dashDirection * moveDistance;

        float distanceToPlayer = HorizontalDistance(transform.position, player.position);
        if (distanceToPlayer <= Mathf.Max(attackRange + GetPlayerRadius(), dashHitRadius))
            BeginAttackSequence();
    }

    void BeginAttackSequence()
    {
        dashState = DashState.Attacking;
        dashStateTimer = Mathf.Max(0.05f, attackAnimationDuration);
        StopAgentMovement();
        attackHitAppliedThisCycle = false;
        dashDirection = Vector3.zero;
        currentState = BossState.Attack;
    }

    void EndDash()
    {
        dashState = DashState.Cooldown;
        dashCooldownTimer = 0f;
        StopAgentMovement();
        dashStateTimer = 0f;
        dashDirection = Vector3.zero;
        attackHitAppliedThisCycle = false;
        currentState = player != null ? BossState.Chase : BossState.Idle;
    }

    void EndAttackSequence()
    {
        dashState = DashState.Cooldown;
        dashCooldownTimer = 0f;
        StopAgentMovement();
        dashStateTimer = 0f;
        attackRecoveryTimer = 0f;
        dashDirection = Vector3.zero;
        attackHitAppliedThisCycle = false;
        // Kembali ke idle; cooldown dash diatur oleh dash state.
        introTimer = 0f;
        currentState = BossState.Idle;
    }

    bool IsObstacleBlocking(Vector3 direction, float moveDistance)
    {
        float castDistance = Mathf.Max(0.05f, moveDistance + obstacleCheckPadding);
        float radius = Mathf.Max(0.05f, obstacleCheckRadius);
        float height = Mathf.Max(radius * 2f + 0.05f, obstacleCheckHeight);

        Vector3 center = transform.position;
        Vector3 bottom = center + Vector3.up * radius;
        Vector3 top = center + Vector3.up * (height - radius);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            radius,
            direction,
            castDistance,
            obstacleMask,
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

            if (hit.distance <= Mathf.Max(0.001f, minBlockingHitDistance))
                continue;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (player != null && (hitTransform == player || hitTransform.IsChildOf(player)))
                continue;

            if (hitTransform.GetComponentInParent<BossNPCController>() != null)
                continue;

            // Abaikan hit dengan permukaan lantai datar yang sering terbaca sebagai halangan saat cast horizontal.
            if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f)
                continue;

            return true;
        }

        return false;
    }

    void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            FaceDirection(direction.normalized);
    }

    void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        KeepBossUpright();
    }

    public void Activate()
    {
        isActivated = true;
        introTimer = 5f;
        dashState = DashState.Ready;
        dashCooldownTimer = 0f;
        hasUnlimitedVisionDistance = unlimitedVisionDistanceAfterActivation;
        TryAssignPlayer();
        playerHealth = ResolvePlayerHealth(player);
        currentState = BossState.Idle;
        SyncAnimatorByState();
    }

    // Matikan kembali boss (mis. saat player berhasil kabur dari ruangannya).
    public void Deactivate()
    {
        isActivated = false;
        introTimer = 0f;
        dashState = DashState.Ready;
        dashStateTimer = 0f;
        dashDirection = Vector3.zero;
        attackHitAppliedThisCycle = false;
        StopAgentMovement();
        currentState = BossState.Idle;

        if (animator != null)
        {
            if (HasAnimatorBool(walkParameter)) animator.SetBool(walkParameter, false);
            if (HasAnimatorBool(runParameter)) animator.SetBool(runParameter, false);
            if (HasAnimatorBool(attackParameter)) animator.SetBool(attackParameter, false);
        }
    }

    void LaunchOpeningAttack()
    {
        if (player == null) { currentState = BossState.Idle; return; }
        currentState = BossState.Chase;
        float dist = HorizontalDistance(transform.position, player.position);
        if (!TryStartDash(dist))
            BeginAttackSequence();
    }

    void KeepBossUpright()
    {
        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

        if (hasLockedY && Application.isPlaying)
        {
            Vector3 pos = transform.position;
            if (!Mathf.Approximately(pos.y, lockedY))
            {
                pos.y = lockedY;
                transform.position = pos;
            }
        }
    }

    bool IsPlayerInSight()
    {
        if (player == null)
            return false;

        Vector3 origin = transform.position + Vector3.up;
        Vector3 target = player.position + Vector3.up;
        Vector3 toPlayer = target - origin;
        float distance = toPlayer.magnitude;

        if (!hasUnlimitedVisionDistance && distance > detectionRange)
            return false;

        Vector3 flatDirection = player.position - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
            return true;

        float angle = Vector3.Angle(transform.forward, flatDirection.normalized);
        if (angle > detectionAngle * 0.5f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            toPlayer.normalized,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return true;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform == null)
                continue;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (hitTransform == player || hitTransform.IsChildOf(player))
                return true;

            return false;
        }

        return true;
    }

    bool TryDamagePlayer()
    {
        if (player == null)
            return false;

        if (!wasInAttackStateLastFrame)
            wasInAttackStateLastFrame = true;

        if (Time.time - lastAttackHitTime < Mathf.Max(0.05f, attackHitInterval))
            return false;

        if (playerHealth == null)
            playerHealth = ResolvePlayerHealth(player);

        if (playerHealth == null || playerHealth.IsDead)
            return false;

        float distance = HorizontalDistance(transform.position, player.position);
        if (distance > attackRange + GetPlayerRadius() + 0.1f)
            return false;

        bool hitApplied = playerHealth.TryTakeHit(Mathf.Max(1, attackDamage), GetInstanceID());
        if (hitApplied)
            lastAttackHitTime = Time.time;

        return hitApplied;
    }

    float GetPlayerRadius()
    {
        if (player == null)
            return 0f;

        if (playerController == null)
            playerController = player.GetComponent<CharacterController>();

        return playerController != null ? playerController.radius : 0f;
    }

    void HandleStunned()
    {
        currentState = BossState.Idle;
        dashState = DashState.Cooldown;
        StopAgentMovement();
        dashCooldownTimer = 0f;
        dashStateTimer = 0f;
        introTimer = 0f;
        dashDirection = Vector3.zero;
        attackHitAppliedThisCycle = false;
        currentAnimationState = string.Empty;

        if (animator != null)
        {
            if (HasAnimatorBool(walkParameter)) animator.SetBool(walkParameter, false);
            if (HasAnimatorBool(runParameter)) animator.SetBool(runParameter, false);
            if (HasAnimatorBool(attackParameter)) animator.SetBool(attackParameter, false);
            if (HasAnimatorBool(stunParameter)) animator.SetBool(stunParameter, true);

            if (forceIdleAnimationOnStun && HasAnimatorState(idleStateName))
                animator.Play(idleStateName, 0, Mathf.Clamp01(idleStateNormalizedTime));

            if (freezeAnimatorDuringStun && !animatorFrozenByStun)
            {
                cachedAnimatorSpeed = animator.speed;
                animator.speed = 0f;
                animatorFrozenByStun = true;
            }
        }
    }

    void ConfigureAgent()
    {
        if (agent == null)
            return;

        agent.updateRotation = false;
        agent.speed = moveSpeed;
        agent.stoppingDistance = stopDistance;

        if (!useNavMeshAgentMovement)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            return;
        }

        if (agent.isOnNavMesh)
            agent.Warp(transform.position);
    }

    void StopAgentMovement()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    void RestoreAnimatorAfterStun()
    {
        if (animator == null)
            return;

        if (animatorFrozenByStun)
        {
            animator.speed = cachedAnimatorSpeed;
            animatorFrozenByStun = false;
        }

        if (HasAnimatorBool(stunParameter))
            animator.SetBool(stunParameter, false);

        currentAnimationState = string.Empty;
    }

    public void ApplyStun(float duration)
    {
        if (!canBeStunned)
            return;

        stunTimer = Mathf.Max(stunTimer, duration);
        postStunTimer = Mathf.Max(postStunTimer, postStunNoChaseDuration);
        currentState = BossState.Idle;
    }

    void SyncAnimatorByState()
    {
        if (animator == null)
            return;

        bool isWalking = (currentState == BossState.Idle || currentState == BossState.Chase)
            && dashState != DashState.Windup
            && dashState != DashState.Dashing
            && dashState != DashState.Attacking;
        // Dash animation should only play while the boss is actually moving in dash state.
        bool isRunning = dashState == DashState.Dashing;
        bool isAttacking = dashState == DashState.Attacking;

        if (HasAnimatorBool(walkParameter))
            animator.SetBool(walkParameter, isWalking);

        if (HasAnimatorBool(runParameter))
            animator.SetBool(runParameter, isRunning);

        if (HasAnimatorBool(attackParameter))
            animator.SetBool(attackParameter, isAttacking);

        if (HasAnimatorBool(stunParameter))
            animator.SetBool(stunParameter, false);
    }

    void PlayAnimationState(string stateName, float speed)
    {
        PlayAnimationState(stateName, speed, 0f, false);
    }

    void PlayAnimationState(string stateName, float speed, float normalizedTime)
    {
        PlayAnimationState(stateName, speed, normalizedTime, true);
    }

    void PlayAnimationState(string stateName, float speed, float normalizedTime, bool forceTime)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        if (currentAnimationState != stateName)
        {
            if (forceTime)
                animator.Play(stateName, 0, Mathf.Clamp01(normalizedTime));
            else
                animator.CrossFadeInFixedTime(stateName, Mathf.Max(0f, animationCrossFade));

            currentAnimationState = stateName;
        }

        animator.speed = Mathf.Max(0f, speed);

        if (forceTime && speed == 0f)
            animator.Update(0f);
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

    bool HasAnimatorState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return false;

        return animator.HasState(0, Animator.StringToHash(stateName));
    }

    void NormalizeAnimatorSettings()
    {
        if (string.IsNullOrWhiteSpace(runParameter))
            runParameter = "IsRunning";

        if (string.IsNullOrWhiteSpace(attackParameter))
            attackParameter = "IsAttacking";

        if (string.Equals(idleStateName, "Running", StringComparison.OrdinalIgnoreCase)
            && string.Equals(walkStateName, "Idle", StringComparison.OrdinalIgnoreCase))
        {
            idleStateName = "Idle";
            walkStateName = "Running";
        }

        if (string.IsNullOrWhiteSpace(idleStateName))
            idleStateName = "Idle";

        if (string.IsNullOrWhiteSpace(walkStateName))
            walkStateName = "Running";

        if (string.IsNullOrWhiteSpace(runningStateName))
            runningStateName = walkStateName;
    }

    void TryAssignPlayer()
    {
        if (player != null)
            return;

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

    void ResolveSpawnPoint()
    {
        if (spawnPoint != null || !autoFindSpawnPointByName || string.IsNullOrEmpty(spawnPointName))
            return;

        GameObject spawnObject = GameObject.Find(spawnPointName);
        if (spawnObject != null)
            spawnPoint = spawnObject.transform;
    }

    void SnapToSpawnIfNeeded()
    {
        if (!snapToSpawnOnStart || spawnPoint == null)
            return;

        transform.position = FindBestSpawnPosition(spawnPoint.position);
        LockCurrentHeight();

        if (useSpawnRotation)
            transform.rotation = spawnPoint.rotation;
    }

    Vector3 FindBestSpawnPosition(Vector3 desiredPosition)
    {
        Vector3 navPosition = GetNearestNavMeshPosition(desiredPosition);
        if (IsSpawnPositionClear(navPosition))
            return navPosition;

        float maxRadius = Mathf.Max(0f, spawnSearchRadius);
        float step = Mathf.Max(0.25f, spawnSearchStep);

        for (float radius = step; radius <= maxRadius + 0.001f; radius += step)
        {
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Vector3 candidate = GetNearestNavMeshPosition(desiredPosition + offset);
                if (IsSpawnPositionClear(candidate))
                    return candidate;
            }
        }

        return navPosition;
    }

    Vector3 GetNearestNavMeshPosition(Vector3 position)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, Mathf.Max(0.5f, spawnSearchRadius + 0.5f), NavMesh.AllAreas))
            return hit.position;

        return position;
    }

    bool IsSpawnPositionClear(Vector3 position)
    {
        float radius = Mathf.Max(0.2f, obstacleCheckRadius);
        float halfHeight = Mathf.Max(radius + 0.1f, obstacleCheckHeight * 0.5f);
        Vector3 center = position + Vector3.up * halfHeight;

        Collider[] overlaps = Physics.OverlapCapsule(
            center - Vector3.up * (halfHeight - radius),
            center + Vector3.up * (halfHeight - radius),
            radius,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        if (overlaps == null || overlaps.Length == 0)
            return true;

        foreach (Collider col in overlaps)
        {
            if (col == null)
                continue;

            Transform hitTransform = col.transform;
            if (hitTransform == null)
                continue;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (player != null && (hitTransform == player || hitTransform.IsChildOf(player)))
                continue;

            return false;
        }

        return true;
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

    void HandleAlertTransition(bool isDetectedNow)
    {
        if (isDetectedNow && currentState == BossState.Idle)
            TryPlayAlertSound();
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
        alertAudioSource.PlayOneShot(alertClip, Mathf.Clamp01(alertVolume));
        Debug.Log(name + ": Boss mode aktif.");
        lastAlertTime = Time.time;
    }

    void TryPlayDashSound()
    {
        if (alertAudioSource == null)
            EnsureAlertAudioSource();

        if (alertAudioSource != null && alertClip != null)
            alertAudioSource.PlayOneShot(alertClip, Mathf.Clamp01(alertVolume));
    }

    void TryAutoAssignAlertClip()
    {
        if (alertClip != null)
            return;

#if UNITY_EDITOR
        alertClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Scenes/Lantai 6/Sound/NPC Marah.mp3");
#endif
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}