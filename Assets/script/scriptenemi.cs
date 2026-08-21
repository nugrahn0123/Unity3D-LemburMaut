using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; 
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour, IStunnable
{
    [Header("Target & Titik Patroli")]
    public Transform playerTarget;
    public Transform[] patrolPoints;
    public Transform patrolParent; // parent yang berisi waypoint sebagai child

    
    [Header("Pengaturan AI")]
    public float detectionRange = 15f; // Jarak deteksi musuh
    public float patrolSpeed = 2f;     // Kecepatan saat patroli
    public float chaseSpeed = 5f;      // Kecepatan saat mengejar pemain
    public float forwardAngle = 90f;   // Sudut bidang depan musuh jika ingin deteksi berdasarkan arah
    public bool chaseOnlyWhenInFront = false; // Jika true, musuh hanya mengejar saat player ada di depan

    [Header("Obstacle Blocking")]
    public bool blockByPhysicalObstacles = false;
    public LayerMask obstacleBlockMask = Physics.DefaultRaycastLayers;
    public float obstacleCheckRadius = 0.28f;
    public float obstacleCheckDistance = 0.9f;
    public float obstacleCheckHeight = 0.9f;
    public bool useAgentRadiusForObstacleCheck = true;

    [Header("Stabilitas Musuh")]
    public bool keepUpright = true; // Menjaga musuh tetap tegak (tidak rebah)
    public float uprightXRotation = -90f; // Sesuaikan orientasi model (contoh: -90)
    public float uprightZRotation = 0f;
    public bool lockYPosition = true; // Kunci tinggi Y agar tidak melayang/ambles

    [Header("Stun")]
    public bool canBeStunned = true;
    public float minimumStunDuration = 0.1f;
    public float postStunNoChaseDuration = 0.5f;
    public bool requireFrontSightReacquireAfterStun = true;

    [Header("Audio - NPC Marah")]
    public AudioClip alertClip;
    [Range(0f, 1f)] public float alertVolume = 0.6f;
    public float alertCooldown = 2f;
    [Range(0f, 1f)] public float alertSpatialBlend = 1f;
    public float alertMinDistance = 2f;
    public float alertMaxDistance = 40f;
    public bool forceAlert2D = true;

    [Header("Vision Light")]
    public bool autoCreateVisionLight = true;
    public bool alwaysShowVisionMarker = true;
    public bool lightOnDuringPatrol = true;
    public bool lightOnDuringChase = true;
    public float patrolLightIntensity = 2.8f;
    public float chaseLightIntensity = 5f;
    public Color patrolLightColor = new Color(1f, 0.9f, 0f);
    public Color chaseLightColor = Color.red;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private float lockedYPosition;
    private int currentPatrolIndex;
    private bool isChasing;
    private bool isPlayerDetected;
    private float stunTimer;
    private float postStunTimer;
    private bool needsSightReacquire;
    private float lastAlertTime = -999f;
    private AudioSource alertAudioSource;
    private Light visionLight;
    private PlayerHealth playerHealth;

    void Start()
    {
        lockedYPosition = transform.position.y;

        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // Pastikan agent diaktifkan dan memakai kecepatan patrol default
        agent.enabled = true;
        if (agent.isOnNavMesh) agent.isStopped = false;
        agent.autoBraking = false;
        agent.speed = patrolSpeed;

        if (keepUpright && rb != null)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // Mencari objek player secara otomatis jika kolom kosong
        if (playerTarget == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                playerObj = GameObject.Find("Player");
            }

            if (playerObj != null) playerTarget = playerObj.transform;
        }

        playerHealth = ResolvePlayerHealth(playerTarget);

        // Mulai patroli ke titik pertama
        currentPatrolIndex = 0;

        if ((patrolPoints == null || patrolPoints.Length == 0) && patrolParent != null)
        {
            List<Transform> children = new List<Transform>();
            foreach (Transform t in patrolParent)
                children.Add(t);
            patrolPoints = children.ToArray();
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogWarning("EnemyAI: Tidak ada `patrolPoints` yang di-set. Musuh tidak dapat patroli.");
            return;
        }

        // Pastikan agent berada di NavMesh; jika tidak, coba sample posisi terdekat
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                Debug.Log("EnemyAI: Agent tidak berada pada NavMesh — memindahkan ke posisi terdekat pada NavMesh.");
            }
            else
            {
                Debug.LogError("EnemyAI: Agent tidak berada pada NavMesh dan tidak ditemukan titik NavMesh dekatnya.");
            }
        }

        TryAutoAssignAlertClip();
        EnsureAlertAudioSource();

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
            visionLight.type = LightType.Spot;
            visionLight.range = detectionRange;
            visionLight.spotAngle = Mathf.Clamp(forwardAngle, 20f, 170f);
        }

        UpdateVisionLight(isChasing);
        SetPatrolDestination();
    }

    void LateUpdate()
    {
        Vector3 position = transform.position;

        if (lockYPosition)
        {
            position.y = lockedYPosition;
        }

        transform.position = position;

        if (!keepUpright) return;

        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(uprightXRotation, euler.y, uprightZRotation);
    }

    void Update()
    {
        if (postStunTimer > 0f)
            postStunTimer -= Time.deltaTime;

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            StopAgentDuringStun();
            UpdateVisionLight(false);
            return;
        }

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        if (needsSightReacquire)
        {
            bool canReacquire = EvaluatePlayerDetection(true);
            if (!canReacquire)
            {
                isPlayerDetected = false;
                Patrol();
                UpdateVisionLight(false);
                return;
            }

            needsSightReacquire = false;
        }

        if (postStunTimer > 0f)
        {
            isPlayerDetected = false;
            Patrol();
            UpdateVisionLight(false);
            return;
        }

        if (playerTarget == null)
        {
            Patrol();
            UpdateVisionLight(false);
            return;
        }

        bool canDetectPlayer = EvaluatePlayerDetection(chaseOnlyWhenInFront);

        if (canDetectPlayer)
        {
            if (!isPlayerDetected)
            {
                TryPlayAlertSound();
                TryRegisterPlayerSpotted();
            }

            isPlayerDetected = true;
            ChasePlayer();
            UpdateVisionLight(true);
        }
        else if (isPlayerDetected)
        {
            isPlayerDetected = false;
            Patrol();
            UpdateVisionLight(false);
        }
        else
        {
            Patrol();
            UpdateVisionLight(false);
        }
    }

    void UpdateVisionLight(bool chasing)
    {
        if (visionLight == null)
            return;

        visionLight.enabled = true;
        visionLight.range = detectionRange;
        visionLight.spotAngle = Mathf.Clamp(forwardAngle, 20f, 170f);

        if (chasing)
        {
            visionLight.color = chaseLightColor;
            visionLight.intensity = chaseLightIntensity;
        }
        else
        {
            visionLight.color = patrolLightColor;
            visionLight.intensity = patrolLightIntensity;
        }
    }

    void TryRegisterPlayerSpotted()
    {
        if (playerHealth == null)
            playerHealth = ResolvePlayerHealth(playerTarget);

        if (playerHealth != null && !playerHealth.IsDead)
            playerHealth.AddSpottedCount(1);
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
            found = target.root.GetComponent<PlayerHealth>();
            if (found != null)
                return found;
        }

        return FindFirstObjectByType<PlayerHealth>();
    }

    void Patrol()
    {
        if (isChasing)
        {
            isChasing = false;
            agent.speed = patrolSpeed;
        }

        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.1f))
        {
            GotoNextPatrolPoint();
        }
    }

    void GotoNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        SetPatrolDestination();
    }

    void SetPatrolDestination()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Transform patrolPoint = patrolPoints[currentPatrolIndex];
        if (patrolPoint == null)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            patrolPoint = patrolPoints[currentPatrolIndex];
        }

        Vector3 targetPosition = patrolPoint != null ? patrolPoint.position : transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
        {
            targetPosition = hit.position;
        }

        agent.SetDestination(targetPosition);
    }

    void ChasePlayer()
    {
        if (agent.isStopped)
            agent.isStopped = false;

        isChasing = true;
        agent.speed = chaseSpeed;
        agent.SetDestination(playerTarget.position);
    }

    bool IsForwardPathBlockedByObstacle()
    {
        if (playerTarget == null)
            return false;

        Vector3 toTarget = playerTarget.position - transform.position;
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

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform == null)
                continue;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (playerTarget != null && (hitTransform == playerTarget || hitTransform.IsChildOf(playerTarget)))
                continue;

            if (hitTransform.GetComponentInParent<EnemyAI>() != null)
                continue;

            if (hitTransform.GetComponentInParent<NPCController>() != null)
                continue;

            return true;
        }

        return false;
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

    void StopAgentDuringStun()
    {
        if (agent == null)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    public void ApplyStun(float duration)
    {
        if (!canBeStunned)
            return;

        float finalDuration = Mathf.Max(minimumStunDuration, duration);
        stunTimer = Mathf.Max(stunTimer, finalDuration);
        postStunTimer = Mathf.Max(postStunTimer, postStunNoChaseDuration);
        isPlayerDetected = false;
        isChasing = false;

        if (requireFrontSightReacquireAfterStun)
            needsSightReacquire = true;

        StopAgentDuringStun();
    }

    bool EvaluatePlayerDetection(bool mustBeInFront)
    {
        if (playerTarget == null)
            return false;

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;

        float distanceToPlayer = toPlayer.magnitude;
        if (distanceToPlayer > detectionRange)
            return false;

        bool isInFront = true;
        if (mustBeInFront)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            float dot = Vector3.Dot(forward, toPlayer.normalized);
            isInFront = dot > Mathf.Cos(forwardAngle * 0.5f * Mathf.Deg2Rad);
        }

        if (!isInFront)
            return false;

        RaycastHit hit;
        Vector3 startPos = transform.position + Vector3.up * 0.5f;
        if (!Physics.Raycast(startPos, toPlayer.normalized, out hit, detectionRange))
            return true;

        return hit.transform == playerTarget || hit.collider.transform == playerTarget;
    }

    // Menggambar lingkaran merah di editor Unity untuk melihat jarak deteksi
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}