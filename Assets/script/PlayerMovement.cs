using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Pengaturan Pergerakan")]
    public float speed = 5f;
    public float runSpeed = 8f;
    public float gravity = -9.81f;
    public float rotationSpeed = 10f;

    [Header("Animasi")]
    public Animator animator;
    public string walkAnimationParameter = "IsWalking";
    public string runAnimationParameter = "IsRunning";

    [Header("Stamina Lari")]
    public float maxStamina = 3f;
    public float staminaDrainPerSecond = 1f;
    public float staminaRecoverPerSecond = 0.6f;
    public float staminaRegenDelay = 2f;
    public float minStaminaToStartRun = 0.2f;

    [Header("Audio - Ngos Ngosan")]
    public AudioClip lowStaminaBreathClip;
    [Range(0f, 1f)] public float lowStaminaBreathVolume = 0.7f;
    public bool lowStaminaBreath2D = true;
    [Range(0f, 1f)] public float lowStaminaThresholdNormalized = 0.5f;

    private CharacterController controller;
    private Vector3 velocity;
    private PlayerFlashlightStun flashlightController;
    private float currentStamina;
    private bool isRunningNow;
    private float staminaRegenDelayTimer;
    private AudioSource lowStaminaBreathSource;

    [Header("Safety")]
    public bool disableExtraRootColliders = true;

    [Header("Character Controller Tuning")]
    public bool autoTuneCharacterController = true;
    public float tunedRadius = 0.18f;
    public float tunedHeight = 1.75f;
    public float tunedStepOffset = 0.2f;
    public float tunedSkinWidth = 0.015f;
    public bool forceCenteredCapsule = true;

    [Header("Visual Alignment")]
    public bool autoAlignVisualToController = true;
    public Transform visualRoot;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => Mathf.Max(0.01f, maxStamina);
    public float StaminaNormalized => Mathf.Clamp01(CurrentStamina / MaxStamina);
    public bool IsRunningNow => isRunningNow;

    private bool AnimatorHasBoolParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
                return true;
        }

        return false;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        flashlightController = GetComponent<PlayerFlashlightStun>();
        currentStamina = Mathf.Max(0.01f, maxStamina);
        EnsureLowStaminaBreathSource();
        EnsurePlayerHealth();

        if (autoTuneCharacterController)
            TuneCharacterController();

        if (disableExtraRootColliders)
            DisableExtraRootColliders();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (autoAlignVisualToController)
            AlignVisualToController();

        EnsurePlayerRuntimeHud();
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
        ResetStamina();
    }

    public void ResetStamina()
    {
        currentStamina = MaxStamina;
        staminaRegenDelayTimer = 0f;
        isRunningNow = false;

        if (lowStaminaBreathSource != null && lowStaminaBreathSource.isPlaying)
            lowStaminaBreathSource.Stop();
    }

    void EnsurePlayerHealth()
    {
        if (GetComponent<PlayerHealth>() == null)
            gameObject.AddComponent<PlayerHealth>();
    }

    void EnsurePlayerRuntimeHud()
    {
        if (FindFirstObjectByType<PlayerRuntimeHUD>() != null)
            return;

        GameObject hudObject = new GameObject("PlayerRuntimeHUD");
        hudObject.AddComponent<PlayerRuntimeHUD>();
    }

    private void TuneCharacterController()
    {
        if (controller == null)
            return;

        controller.height = Mathf.Clamp(tunedHeight, 1.2f, 2.2f);
        controller.radius = Mathf.Clamp(tunedRadius, 0.1f, controller.height * 0.5f - 0.01f);
        controller.skinWidth = Mathf.Clamp(tunedSkinWidth, 0.005f, 0.08f);

        if (forceCenteredCapsule)
        {
            Vector3 center = controller.center;
            center.x = 0f;
            center.z = 0f;
            center.y = controller.height * 0.5f;
            controller.center = center;
        }

        controller.stepOffset = Mathf.Clamp(tunedStepOffset, 0.01f, 0.5f);

        // Pastikan step offset valid terhadap tinggi kapsul.
        float maxStepOffset = Mathf.Max(0.01f, controller.height - (controller.radius * 2f));
        if (controller.stepOffset > maxStepOffset)
            controller.stepOffset = maxStepOffset;
    }

    private void AlignVisualToController()
    {
        if (controller == null)
            return;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        Transform targetVisualRoot = visualRoot != null ? visualRoot : (animator != null ? animator.transform : null);
        if (targetVisualRoot == null)
            return;

        Renderer visualRenderer = targetVisualRoot.GetComponentInChildren<Renderer>();
        if (visualRenderer == null)
            return;

        Vector3 modelCenterLocal = transform.InverseTransformPoint(visualRenderer.bounds.center);
        Vector3 controllerCenter = controller.center;
        Vector3 deltaLocal = new Vector3(modelCenterLocal.x - controllerCenter.x, 0f, modelCenterLocal.z - controllerCenter.z);

        if (deltaLocal.sqrMagnitude < 0.000001f)
            return;

        targetVisualRoot.localPosition -= deltaLocal;
    }

    private void DisableExtraRootColliders()
    {
        Collider[] rootColliders = GetComponents<Collider>();
        for (int i = 0; i < rootColliders.Length; i++)
        {
            Collider col = rootColliders[i];
            if (col == null || !col.enabled || col.isTrigger)
                continue;

            // CharacterController adalah collider utama untuk movement, jangan dinonaktifkan.
            if (col is CharacterController)
                continue;

            col.enabled = false;
            Debug.LogWarning("PlayerMovement: Menonaktifkan collider tambahan di root Player agar tidak menghalangi CharacterController.", this);
        }
    }

    // Dipanggil setelah teleport agar gravitasi terakumulasi tidak langsung mendorong player ke lantai.
    public void ResetVelocity()
    {
        velocity = Vector3.zero;
    }

    void Update()
    {
        if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            return;

        bool isFlashlightActive = flashlightController != null && flashlightController.IsFlashlightActive;

        // Input pergerakan
        Vector2 inputVec = Vector2.zero;

        if (!isFlashlightActive && Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) inputVec.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) inputVec.y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) inputVec.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) inputVec.x += 1f;
        }

        if (!isFlashlightActive && Gamepad.current != null)
        {
            inputVec += Gamepad.current.leftStick.ReadValue();
        }

        inputVec = Vector2.ClampMagnitude(inputVec, 1f);

        // Animasi
        bool isMoving = inputVec.sqrMagnitude > 0.01f;
        bool runKeyHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        bool runPadHeld = Gamepad.current != null && Gamepad.current.rightTrigger.ReadValue() > 0.3f;
        bool isRunPressed = !isFlashlightActive && (runKeyHeld || runPadHeld);
        bool canRunNow = currentStamina > (isRunningNow ? 0.001f : Mathf.Max(0f, minStaminaToStartRun));
        bool isRunning = isMoving && isRunPressed && canRunNow;
        bool isWalking = isMoving;
        UpdateStamina(isRunning);
        isRunningNow = isRunning;

        if (animator != null)
        {
            string walkParam = walkAnimationParameter;
            string runParam = runAnimationParameter;

            // Fallback otomatis jika nama parameter di Inspector tidak sinkron.
            if (!AnimatorHasBoolParameter(walkParam) && AnimatorHasBoolParameter("IsWalking"))
                walkParam = "IsWalking";

            if (!AnimatorHasBoolParameter(runParam) && AnimatorHasBoolParameter("IsRunning"))
                runParam = "IsRunning";

            bool sameParam = !string.IsNullOrEmpty(walkParam) && string.Equals(walkParam, runParam, StringComparison.Ordinal);

            if (!string.IsNullOrEmpty(walkParam) && AnimatorHasBoolParameter(walkParam))
                animator.SetBool(walkParam, isWalking);

            if (!sameParam && !string.IsNullOrEmpty(runParam) && AnimatorHasBoolParameter(runParam))
                animator.SetBool(runParam, isRunning);
        }

        // Pergerakan relatif ke arah camera (W = arah camera lihat, D = camera kanan)
        Vector3 move = Vector3.zero;
        if (Camera.main != null)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
            move = (camForward * inputVec.y + camRight * inputVec.x);
        }
        else
        {
            move = new Vector3(inputVec.x, 0f, inputVec.y);
        }

        if (move.sqrMagnitude > 0.01f)
        {
            move = move.normalized;
            float currentSpeed = isRunning ? runSpeed : speed;
            controller.Move(move * currentSpeed * Time.deltaTime);

            // Rotasi player menghadap ke arah gerakan
            Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // Gravitasi
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, -20f);
        controller.Move(velocity * Time.deltaTime);

        UpdateLowStaminaBreathAudio();
    }

    void EnsureLowStaminaBreathSource()
    {
        if (lowStaminaBreathSource == null)
        {
            Transform child = transform.Find("LowStaminaBreathAudio");
            if (child == null)
            {
                GameObject childGo = new GameObject("LowStaminaBreathAudio");
                childGo.transform.SetParent(transform, false);
                child = childGo.transform;
            }

            lowStaminaBreathSource = child.GetComponent<AudioSource>();
            if (lowStaminaBreathSource == null)
                lowStaminaBreathSource = child.gameObject.AddComponent<AudioSource>();
        }

        lowStaminaBreathSource.playOnAwake = false;
        lowStaminaBreathSource.loop = true;
        lowStaminaBreathSource.spatialBlend = lowStaminaBreath2D ? 0f : 1f;
        lowStaminaBreathSource.volume = Mathf.Clamp01(lowStaminaBreathVolume);
        lowStaminaBreathSource.minDistance = 1f;
        lowStaminaBreathSource.maxDistance = 16f;
        lowStaminaBreathSource.rolloffMode = AudioRolloffMode.Linear;
        lowStaminaBreathSource.clip = lowStaminaBreathClip;
    }

    void UpdateLowStaminaBreathAudio()
    {
        if (lowStaminaBreathSource == null)
            return;

        lowStaminaBreathSource.spatialBlend = lowStaminaBreath2D ? 0f : 1f;
        lowStaminaBreathSource.volume = Mathf.Clamp01(lowStaminaBreathVolume);
        if (lowStaminaBreathSource.clip != lowStaminaBreathClip)
            lowStaminaBreathSource.clip = lowStaminaBreathClip;

        if (lowStaminaBreathClip == null)
        {
            if (lowStaminaBreathSource.isPlaying)
                lowStaminaBreathSource.Stop();
            return;
        }

        float threshold = Mathf.Clamp01(lowStaminaThresholdNormalized);
        bool shouldPlay = StaminaNormalized < threshold;
        bool shouldStop = StaminaNormalized > threshold;

        if (shouldPlay && !lowStaminaBreathSource.isPlaying)
            lowStaminaBreathSource.Play();
        else if (shouldStop && lowStaminaBreathSource.isPlaying)
            lowStaminaBreathSource.Stop();
    }

    void UpdateStamina(bool isRunning)
    {
        if (isRunning)
        {
            currentStamina -= Mathf.Max(0f, staminaDrainPerSecond) * Time.deltaTime;
            staminaRegenDelayTimer = Mathf.Max(0f, staminaRegenDelay);
        }
        else
        {
            if (staminaRegenDelayTimer > 0f)
                staminaRegenDelayTimer -= Time.deltaTime;
            else
                currentStamina += Mathf.Max(0f, staminaRecoverPerSecond) * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, Mathf.Max(0.01f, maxStamina));
    }
}