using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Portal keluar ruangan kelistrikan. Tersembunyi sampai semua GeneratorListrik
// di scene aktif, lalu muncul dan bisa dipakai player untuk teleport keluar.
public class GeneratorExitPortal : MonoBehaviour
{
    [Header("Tujuan Teleport")]
    [Tooltip("Dipakai jika Exit Scene Name kosong: teleport dalam scene.")]
    public Transform destinationPoint;

    [Header("Pindah Scene")]
    [Tooltip("Jika diisi, keluar portal langsung memuat scene ini (mis. Scene Ending).")]
    public string exitSceneName = "Scene Ending";
    [Tooltip("Nonaktifkan semua boss saat player berhasil keluar.")]
    public bool deactivateBossesOnExit = true;
    [Tooltip("Jika true, portal baru muncul setelah satpam/boss tumbang oleh jebakan listrik.")]
    public bool requireBossDefeated = false;

    [Header("Filter")]
    public string playerTag = "Player";

    [Header("Interaksi")]
    [Tooltip("Jika true, player harus menekan E / tombol B saat di dalam portal.")]
    public bool requireKeyPress = true;
    public string promptText = "Tekan [E] untuk keluar";

    [Header("Visual")]
    [Tooltip("Object visual portal yang disembunyikan sampai generator menyala.")]
    public GameObject[] portalVisuals;
    public bool showCircleMarker = true;
    public Color circleColor = new Color(0.2f, 1f, 0.6f, 1f);
    public float circleRadius = 2f;

    [Header("Audio (opsional)")]
    public AudioClip appearClip;
    public AudioClip teleportClip;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    public bool IsUnlocked { get; private set; }

    private bool playerInZone;
    private float teleportCooldown;
    private GameObject circleMarker;

    void Start()
    {
        EnsureTriggerCollider();

        if (showCircleMarker)
        {
            circleMarker = CreateCircleMarker();
            circleMarker.SetActive(false);
        }

        SetPortalVisible(false);
    }

    void Update()
    {
        if (teleportCooldown > 0f)
            teleportCooldown -= Time.deltaTime;

        if (!IsUnlocked)
        {
            if (AllGeneratorsActivated() && (!requireBossDefeated || ElectricTrapZone.AnyBossDefeated))
                UnlockPortal();
            return;
        }

        if (!playerInZone || !requireKeyPress)
            return;

        if (InteractPressedThisFrame())
            TeleportPlayer();
    }

    static bool InteractPressedThisFrame()
    {
        bool key = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool pad = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        return key || pad;
    }

    bool AllGeneratorsActivated()
    {
        GeneratorListrik[] generators = FindObjectsByType<GeneratorListrik>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (generators.Length == 0)
            return false;

        foreach (GeneratorListrik generator in generators)
        {
            if (generator != null && !generator.IsActivated)
                return false;
        }
        return true;
    }

    void UnlockPortal()
    {
        IsUnlocked = true;
        SetPortalVisible(true);

        if (appearClip != null)
            AudioSource.PlayClipAtPoint(appearClip, transform.position, Mathf.Clamp01(sfxVolume));

        Debug.Log("GeneratorExitPortal: Semua generator menyala, portal keluar muncul.", this);
    }

    void SetPortalVisible(bool isVisible)
    {
        if (portalVisuals != null)
        {
            foreach (GameObject visual in portalVisuals)
                if (visual != null)
                    visual.SetActive(isVisible);
        }

        if (circleMarker != null)
            circleMarker.SetActive(isVisible);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsUnlocked || teleportCooldown > 0f || !other.CompareTag(playerTag))
            return;

        playerInZone = true;

        if (!requireKeyPress)
            TeleportPlayer();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
            playerInZone = false;
    }

    void TeleportPlayer()
    {
        if (deactivateBossesOnExit)
            DeactivateAllBosses();

        // Mode pindah scene: langsung muat scene tujuan.
        if (!string.IsNullOrWhiteSpace(exitSceneName))
        {
            teleportCooldown = 2f;
            playerInZone = false;

            if (teleportClip != null)
                AudioSource.PlayClipAtPoint(teleportClip, transform.position, Mathf.Clamp01(sfxVolume));

            SceneManager.LoadScene(exitSceneName);
            return;
        }

        if (destinationPoint == null)
        {
            Debug.LogWarning("GeneratorExitPortal: destinationPoint belum di-assign.", this);
            return;
        }

        GameObject player = GameObject.FindWithTag(playerTag);
        if (player == null)
            return;

        teleportCooldown = 2f;
        playerInZone = false;

        // Nonaktifkan CharacterController dulu agar teleport bersih.
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = destinationPoint.position;

        if (cc != null) cc.enabled = true;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ResetVelocity();

        if (teleportClip != null)
            AudioSource.PlayClipAtPoint(teleportClip, destinationPoint.position, Mathf.Clamp01(sfxVolume));
    }

    void DeactivateAllBosses()
    {
        BossNPCController[] bosses = FindObjectsByType<BossNPCController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (BossNPCController boss in bosses)
        {
            if (boss != null)
                boss.Deactivate();
        }
    }

    void EnsureTriggerCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            CapsuleCollider cap = gameObject.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.radius = circleRadius;
            cap.height = 3f;
            cap.center = Vector3.up * 1f;
        }
        else if (!col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    GameObject CreateCircleMarker()
    {
        GameObject marker = new GameObject("PortalCircleMarker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        LineRenderer line = marker.AddComponent<LineRenderer>();
        const int segments = 48;
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = segments;
        line.widthMultiplier = 0.08f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = circleColor;
        line.endColor = circleColor;

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * circleRadius, 0f, Mathf.Sin(angle) * circleRadius));
        }

        return marker;
    }

    void OnGUI()
    {
        if (!IsUnlocked || !playerInZone || !requireKeyPress)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect((Screen.width - 400f) * 0.5f, Screen.height - 120f, 400f, 50f), promptText, style);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = circleColor;
        Gizmos.DrawWireSphere(transform.position, circleRadius);

        if (destinationPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, destinationPoint.position);
            Gizmos.DrawWireSphere(destinationPoint.position, 0.5f);
        }
    }
}
