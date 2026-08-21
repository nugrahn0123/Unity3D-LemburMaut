using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerFlashlightStun : MonoBehaviour
{
    [Header("Flashlight")]
    public Light phoneFlashlight;
    public Key holdFlashlightKey = Key.F;
    public bool alignFlashlightVisualToCamera = true;
    public float visualAlignSpeed = 16f;

    [Header("Durasi Senter")]
    public float maxFlashlightEnergy = 10f;
    public float flashlightDrainPerSecond = 1f;
    public float flashlightRecoverPerSecond = 1f;
    public float minEnergyToTurnOn = 0.15f;

    [Header("Animation")]
    public Animator animator;
    public string flashlightAimParameter = "IsFlashlightAiming";

    [Header("Stun Attack")]
    public float stunDuration = 2f;
    public float hitInterval = 0.12f;
    public float pointBlankAutoHitDistance = 1.6f;
    public bool useNpcLayerMask = true;
    public LayerMask npcLayerMask = ~0;
    public bool useCameraForwardForDetection = true;
    public float cameraDetectionOffset = 0.1f;

    [Header("Blocking")]
    public LayerMask occlusionMask = Physics.DefaultRaycastLayers;
    public bool ignoreOcclusion = false;

    [Header("Debug")]
    public bool debugLogHits = false;

    [Header("Audio - Senter")]
    public AudioClip flashlightToggleClip;
    [Range(0f, 1f)] public float flashlightToggleVolume = 0.45f;
    public float flashlightToggleCooldown = 0.08f;
    public bool forceFlashlightToggle2D = true;

    [Header("Audio - Baterai Habis")]
    public AudioClip flashlightBatteryDepletedClip;
    [Range(0f, 1f)] public float flashlightBatteryDepletedVolume = 0.85f;

    private float nextHitTime;
    public bool IsFlashlightActive { get; private set; }
    private HashSet<string> animatorBoolParams = new HashSet<string>();
    private Quaternion initialFlashlightLocalRotation;
    private bool hasInitialFlashlightRotation;
    private AudioSource flashlightAudioSource;
    private float lastFlashlightToggleTime = -999f;
    private float currentFlashlightEnergy;
    private bool mustReleaseFlashlightKeyAfterDepleted;
    private bool flashlightBatteryDepleted;
    private bool wasFlashlightKeyPressedLastFrame;

    public float CurrentFlashlightEnergy => currentFlashlightEnergy;
    public float MaxFlashlightEnergy => Mathf.Max(0.01f, maxFlashlightEnergy);
    public float FlashlightEnergyNormalized => Mathf.Clamp01(CurrentFlashlightEnergy / MaxFlashlightEnergy);

    void Start()
    {
        if (phoneFlashlight == null)
            phoneFlashlight = GetComponentInChildren<Light>();

        if (phoneFlashlight != null)
        {
            initialFlashlightLocalRotation = phoneFlashlight.transform.localRotation;
            hasInitialFlashlightRotation = true;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        TryAutoAssignFlashlightToggleClip();
        EnsureFlashlightAudioSource();

        CacheAnimatorBoolParameters();

        currentFlashlightEnergy = Mathf.Max(0.01f, maxFlashlightEnergy);

        SetFlashlightState(false);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetFlashlightEnergy();
    }

    public void ResetFlashlightEnergy()
    {
        currentFlashlightEnergy = MaxFlashlightEnergy;
        flashlightBatteryDepleted = false;
        mustReleaseFlashlightKeyAfterDepleted = false;
        SetFlashlightState(false, false);
    }

    void Update()
    {
        HandleFlashlightHold();
        UpdateFlashlightEnergy();

        if (phoneFlashlight == null || !phoneFlashlight.enabled)
            return;

        if (Time.time < nextHitTime)
            return;

        nextHitTime = Time.time + hitInterval;
        TryStunTargetsInLightCone();
    }

    void HandleFlashlightHold()
    {
        bool isHolding = false;

        if (Keyboard.current != null)
        {
            var keyControl = Keyboard.current[holdFlashlightKey];
            isHolding = keyControl != null && keyControl.isPressed;
        }

        if (Gamepad.current != null && Gamepad.current.rightShoulder.isPressed)
            isHolding = true;

        if (Keyboard.current == null && Gamepad.current == null)
        {
            SetFlashlightState(false);
            wasFlashlightKeyPressedLastFrame = false;
            return;
        }

        bool justPressed = isHolding && !wasFlashlightKeyPressedLastFrame;
        wasFlashlightKeyPressedLastFrame = isHolding;

        if (flashlightBatteryDepleted)
        {
            if (justPressed)
                TryPlayBatteryDepletedSound();

            SetFlashlightState(false);
            return;
        }

        if (!isHolding)
            mustReleaseFlashlightKeyAfterDepleted = false;

        if (mustReleaseFlashlightKeyAfterDepleted && isHolding)
        {
            SetFlashlightState(false);
            return;
        }

        bool hasEnergy = currentFlashlightEnergy > 0.001f;
        bool canTurnOn = currentFlashlightEnergy >= Mathf.Max(0f, minEnergyToTurnOn);
        bool shouldBeActive = false;

        if (isHolding)
            shouldBeActive = IsFlashlightActive ? hasEnergy : canTurnOn;

        SetFlashlightState(shouldBeActive);
    }

    void UpdateFlashlightEnergy()
    {
        if (IsFlashlightActive)
            currentFlashlightEnergy -= Mathf.Max(0f, flashlightDrainPerSecond) * Time.deltaTime;

        currentFlashlightEnergy = Mathf.Clamp(currentFlashlightEnergy, 0f, Mathf.Max(0.01f, maxFlashlightEnergy));

        if (currentFlashlightEnergy <= 0.001f)
        {
            bool justDepletedNow = !flashlightBatteryDepleted;
            flashlightBatteryDepleted = true;
            mustReleaseFlashlightKeyAfterDepleted = true;
            SetFlashlightState(false, false);

            if (justDepletedNow)
                TryPlayBatteryDepletedSound();
        }
    }

    void SetFlashlightState(bool isActive, bool playToggleSound = true)
    {
        bool stateChanged = IsFlashlightActive != isActive;
        IsFlashlightActive = isActive;

        if (phoneFlashlight != null)
            phoneFlashlight.enabled = isActive;

        if (animator != null && !string.IsNullOrEmpty(flashlightAimParameter))
        {
            if (HasAnimatorBool(flashlightAimParameter))
                animator.SetBool(flashlightAimParameter, isActive);
        }

        if (!isActive)
            ResetFlashlightVisualRotation();

        if (stateChanged && playToggleSound)
            TryPlayFlashlightToggleSound();
    }

    void TryPlayBatteryDepletedSound()
    {
        if (flashlightBatteryDepletedClip == null)
            return;

        EnsureFlashlightAudioSource();
        flashlightAudioSource.spatialBlend = forceFlashlightToggle2D ? 0f : 1f;
        flashlightAudioSource.volume = Mathf.Clamp01(flashlightBatteryDepletedVolume);
        flashlightAudioSource.loop = false;

        if (flashlightAudioSource.clip != flashlightBatteryDepletedClip)
            flashlightAudioSource.clip = flashlightBatteryDepletedClip;

        // Hindari overlap: jika dipicu lagi, reset dari awal.
        if (flashlightAudioSource.isPlaying)
            flashlightAudioSource.Stop();

        flashlightAudioSource.time = 0f;
        flashlightAudioSource.Play();
    }

    void EnsureFlashlightAudioSource()
    {
        if (flashlightAudioSource == null)
        {
            Transform child = transform.Find("PlayerFlashlightAudio");
            if (child == null)
            {
                GameObject childGo = new GameObject("PlayerFlashlightAudio");
                childGo.transform.SetParent(transform, false);
                child = childGo.transform;
            }

            flashlightAudioSource = child.GetComponent<AudioSource>();
            if (flashlightAudioSource == null)
                flashlightAudioSource = child.gameObject.AddComponent<AudioSource>();
        }

        flashlightAudioSource.playOnAwake = false;
        flashlightAudioSource.loop = false;
        flashlightAudioSource.spatialBlend = forceFlashlightToggle2D ? 0f : 1f;
        flashlightAudioSource.volume = Mathf.Clamp01(flashlightToggleVolume);
        flashlightAudioSource.minDistance = 1f;
        flashlightAudioSource.maxDistance = 16f;
        flashlightAudioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    void TryPlayFlashlightToggleSound()
    {
        if (flashlightToggleClip == null)
            return;

        if (Time.time - lastFlashlightToggleTime < Mathf.Max(0.01f, flashlightToggleCooldown))
            return;

        EnsureFlashlightAudioSource();
        flashlightAudioSource.spatialBlend = forceFlashlightToggle2D ? 0f : 1f;
        flashlightAudioSource.volume = Mathf.Clamp01(flashlightToggleVolume);
        flashlightAudioSource.PlayOneShot(flashlightToggleClip, Mathf.Clamp01(flashlightToggleVolume));
        lastFlashlightToggleTime = Time.time;
    }

    void TryAutoAssignFlashlightToggleClip()
    {
        if (flashlightToggleClip != null)
            return;

#if UNITY_EDITOR
        flashlightToggleClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Scenes/Lantai 6/Sound/Senter Menyala.mp3");
#endif
    }

    void LateUpdate()
    {
        if (!IsFlashlightActive)
            return;

        if (!alignFlashlightVisualToCamera || phoneFlashlight == null || Camera.main == null)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
        phoneFlashlight.transform.rotation = Quaternion.Slerp(
            phoneFlashlight.transform.rotation,
            targetRotation,
            visualAlignSpeed * Time.deltaTime
        );
    }

    void ResetFlashlightVisualRotation()
    {
        if (!hasInitialFlashlightRotation || phoneFlashlight == null)
            return;

        phoneFlashlight.transform.localRotation = initialFlashlightLocalRotation;
    }

    void TryStunTargetsInLightCone()
    {
        Vector3 origin = phoneFlashlight.transform.position;
        Vector3 forward = phoneFlashlight.transform.forward;

        if (useCameraForwardForDetection && Camera.main != null)
        {
            origin = Camera.main.transform.position + Camera.main.transform.forward * cameraDetectionOffset;
            forward = Camera.main.transform.forward;
        }

        float range = Mathf.Max(0.1f, phoneFlashlight.range);
        Transform selfRoot = transform.root;
        float allowedAngle = phoneFlashlight.type == LightType.Spot ? phoneFlashlight.spotAngle * 0.5f : 45f;
        HashSet<int> stunnedRoots = new HashSet<int>();

        NPCController[] npcControllers = FindObjectsByType<NPCController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (NPCController npcController in npcControllers)
        {
            TryApplyStun(
                npcController != null ? npcController.transform : null,
                npcController,
                origin,
                forward,
                range,
                allowedAngle,
                selfRoot,
                stunnedRoots,
                "npc-controller",
                false
            );
        }

        BossNPCController[] bossControllers = FindObjectsByType<BossNPCController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (BossNPCController bossController in bossControllers)
        {
            TryApplyStun(
                bossController != null ? bossController.transform : null,
                bossController,
                origin,
                forward,
                range,
                allowedAngle,
                selfRoot,
                stunnedRoots,
                "boss-controller",
                true
            );
        }

        EnemyAI[] enemyAis = FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (EnemyAI enemyAi in enemyAis)
        {
            TryApplyStun(
                enemyAi != null ? enemyAi.transform : null,
                enemyAi,
                origin,
                forward,
                range,
                allowedAngle,
                selfRoot,
                stunnedRoots,
                "enemy-ai",
                false
            );
        }

        TryStunNavMeshAgentsInCone(origin, forward, range, allowedAngle, selfRoot, stunnedRoots);
    }

    void TryApplyStun(
        Transform targetTransform,
        IStunnable stunnable,
        Vector3 origin,
        Vector3 forward,
        float range,
        float allowedAngle,
        Transform selfRoot,
        HashSet<int> stunnedRoots,
        string sourceTag,
        bool bypassNpcLayerMask
    )
    {
        if (targetTransform == null || stunnable == null)
            return;

        Transform root = targetTransform.root;
        int rootId = root.GetInstanceID();

        if (stunnedRoots.Contains(rootId))
            return;

        if (useNpcLayerMask && !bypassNpcLayerMask && !IsLayerIncluded(npcLayerMask, root.gameObject.layer) && !IsLayerIncluded(npcLayerMask, targetTransform.gameObject.layer))
            return;

        Vector3 targetPoint = GetBestTargetPoint(root);
        if (!IsTargetInConeAndVisible(origin, forward, targetPoint, range, allowedAngle, selfRoot, root))
            return;

        stunnable.ApplyStun(stunDuration);
        stunnedRoots.Add(rootId);

        if (debugLogHits)
            Debug.Log("Flashlight stun hit (" + sourceTag + "): " + root.name, this);
    }

    Vector3 GetBestTargetPoint(Transform root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (Collider col in colliders)
            {
                if (col == null || !col.enabled)
                    continue;

                sum += col.bounds.center;
                count++;
            }

            if (count > 0)
                return sum / count;
        }

        return root.position + Vector3.up;
    }

    void TryStunNavMeshAgentsInCone(
        Vector3 origin,
        Vector3 forward,
        float range,
        float allowedAngle,
        Transform selfRoot,
        HashSet<int> stunnedRoots
    )
    {
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (NavMeshAgent navMeshAgent in agents)
        {
            if (navMeshAgent == null || !navMeshAgent.isActiveAndEnabled)
                continue;

            Transform root = navMeshAgent.transform.root;
            int rootId = root.GetInstanceID();
            if (stunnedRoots.Contains(rootId))
                continue;

            if (useNpcLayerMask && !IsLayerIncluded(npcLayerMask, root.gameObject.layer) && !IsLayerIncluded(npcLayerMask, navMeshAgent.gameObject.layer))
                continue;

            IStunnable stunnable = navMeshAgent.GetComponent<IStunnable>();
            if (stunnable == null)
            {
                NavMeshAgentStunAdapter adapter = navMeshAgent.GetComponent<NavMeshAgentStunAdapter>();
                if (adapter == null)
                    adapter = navMeshAgent.gameObject.AddComponent<NavMeshAgentStunAdapter>();

                stunnable = adapter;
            }

            Vector3 targetPoint = GetBestTargetPoint(root);
            if (!IsTargetInConeAndVisible(origin, forward, targetPoint, range, allowedAngle, selfRoot, root))
                continue;

            stunnable.ApplyStun(stunDuration);
            stunnedRoots.Add(rootId);

            if (debugLogHits)
                Debug.Log("Flashlight stun hit (agent fallback): " + root.name, this);
        }
    }

    bool IsTargetInConeAndVisible(
        Vector3 origin,
        Vector3 forward,
        Vector3 targetPoint,
        float range,
        float allowedAngle,
        Transform selfRoot,
        Transform targetRoot
    )
    {
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f || distance > range)
            return false;

        Vector3 dir = toTarget / distance;
        if (distance <= pointBlankAutoHitDistance)
            return true;

        float angle = Vector3.Angle(forward, dir);
        if (angle > allowedAngle)
            return false;

        if (ignoreOcclusion)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, distance, occlusionMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
            return true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitRoot = hit.collider.transform.root;

            if (hitRoot == selfRoot)
                continue;

            if (hitRoot == targetRoot)
                return true;

            return false;
        }

        return true;
    }

    bool IsLayerIncluded(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
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
}

[DisallowMultipleComponent]
public class NavMeshAgentStunAdapter : MonoBehaviour, IStunnable
{
    public bool freezeRotationWhileStunned = false;
    public bool freezeAnimatorWhileStunned = true;

    private NavMeshAgent agent;
    private Animator animator;
    private float stunTimer;
    private Quaternion cachedRotation;
    private float cachedAnimatorSpeed = 1f;
    private bool animatorFrozenByStun = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (stunTimer <= 0f)
            return;

        stunTimer -= Time.deltaTime;
        ApplyFreeze();

        if (stunTimer <= 0f)
            ReleaseFreeze();
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0f)
            return;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        stunTimer = Mathf.Max(stunTimer, duration);
        cachedRotation = transform.rotation;
        ApplyFreeze();
    }

    void ApplyFreeze()
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (freezeAnimatorWhileStunned && animator != null && !animatorFrozenByStun)
        {
            cachedAnimatorSpeed = animator.speed;
            animator.speed = 0f;
            animatorFrozenByStun = true;
        }

        if (freezeRotationWhileStunned)
            transform.rotation = cachedRotation;
    }

    void ReleaseFreeze()
    {
        if (agent != null)
            agent.isStopped = false;

        if (animator != null && animatorFrozenByStun)
        {
            animator.speed = cachedAnimatorSpeed;
            animatorFrozenByStun = false;
        }
    }
}
