using UnityEngine;
using UnityEngine.InputSystem;

// Generator listrik: diaktifkan player (E / tombol B gamepad) untuk menyalakan
// kembali Directional Light yang sengaja dimatikan sebagai efek mati lampu.
public class GeneratorListrik : MonoBehaviour
{
    [Header("Interaksi")]
    public float interactionRange = 3f;
    public string playerTag = "Player";

    [Header("Lampu")]
    [Tooltip("Kosongkan untuk otomatis mencari Directional Light di scene.")]
    public Light targetLight;
    [Tooltip("Jika true, semua generator di scene harus aktif dulu sebelum lampu menyala.")]
    public bool requireAllGenerators = true;

    [Header("Audio (opsional)")]
    public AudioClip activationClip;
    [Range(0f, 1f)] public float activationVolume = 0.8f;

    [Header("Prompt")]
    public bool showPrompt = true;
    public string promptText = "Tekan [E] untuk menyalakan generator";

    public bool IsActivated { get; private set; }

    // Dipakai ElectricTrapZone untuk mendeteksi momen generator dinyalakan.
    public static event System.Action<GeneratorListrik> OnGeneratorActivated;

    private Transform player;
    private bool playerInRange;

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
            player = playerObject.transform;

        if (targetLight == null)
            targetLight = FindDirectionalLight();
    }

    void Update()
    {
        if (IsActivated || player == null)
        {
            playerInRange = false;
            return;
        }

        playerInRange = Vector3.Distance(player.position, transform.position) <= interactionRange;

        if (playerInRange && InteractPressedThisFrame())
            Activate();
    }

    static bool InteractPressedThisFrame()
    {
        bool key = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool pad = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        return key || pad;
    }

    public void Activate()
    {
        if (IsActivated)
            return;

        IsActivated = true;
        OnGeneratorActivated?.Invoke(this);

        if (activationClip != null)
            AudioSource.PlayClipAtPoint(activationClip, transform.position, Mathf.Clamp01(activationVolume));

        if (!requireAllGenerators || AllGeneratorsActivated())
            TurnOnLight();
    }

    bool AllGeneratorsActivated()
    {
        GeneratorListrik[] generators = FindObjectsByType<GeneratorListrik>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (GeneratorListrik generator in generators)
        {
            if (generator != null && !generator.IsActivated)
                return false;
        }
        return true;
    }

    void TurnOnLight()
    {
        if (targetLight == null)
            targetLight = FindDirectionalLight();

        if (targetLight == null)
        {
            Debug.LogWarning("GeneratorListrik: Directional Light tidak ditemukan di scene.", this);
            return;
        }

        targetLight.gameObject.SetActive(true);
        targetLight.enabled = true;
        Debug.Log("GeneratorListrik: Listrik menyala, lampu dihidupkan.", this);
    }

    Light FindDirectionalLight()
    {
        // Include inactive: lampunya memang sengaja dimatikan.
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Light light in lights)
        {
            if (light != null && light.type == LightType.Directional)
                return light;
        }
        return null;
    }

    void OnGUI()
    {
        if (!showPrompt || !playerInRange || IsActivated)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect((Screen.width - 500f) * 0.5f, Screen.height - 120f, 500f, 50f), promptText, style);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
