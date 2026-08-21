using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Pasang di GameObject dengan Collider (set Is Trigger = true).
/// Player yang masuk zona ini akan di-teleport ke destinationPoint dalam scene yang sama.
/// </summary>
public class BossRoomTrigger : MonoBehaviour
{
    [Header("Titik Tujuan Teleport")]
    public Transform destinationPoint;

    [Header("Filter")]
    public string playerTag = "Player";

    [Header("UI Prompt (opsional)")]
    public GameObject promptUI;          // Teks/panel "Tekan E untuk masuk" dll.
    public bool requireKeyPress = false; // True = tunggu input, False = langsung teleport
    public Key activationKey = Key.E;

    [Header("Efek")]
    public AudioClip transitionSFX;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    [Header("Kode Akses")]
    public bool requireAccessCode = false;
    [Tooltip("Kode yang harus dimasukkan player. Sesuaikan dengan secretCode di item.")]
    public string correctCode = "";

    [Header("Boss")]
    [Tooltip("Assign BossNPCController di scene. Akan diaktifkan setelah player masuk.")]
    public BossNPCController bossToActivate;

    [Header("Alur Selesai Game")]
    [Tooltip("Titik keluar ruangan boss. Jika kosong, pakai destinationPoint.")]
    public Transform exitPoint;
    [Tooltip("Nama item kunci yang membuka kembali destinasi. Kosong = item apa pun.")]
    public string requiredKeyItemName = "Kunci";
    [Tooltip("Jika true, periksa secretCode item juga agar lebih spesifik.")]
    public bool requireSecretCodeMatch = false;
    public string requiredSecretCode = "";

    [Header("Alarm Kode Salah")]
    public AudioClip wrongCodeAlarmClip;
    [Range(0f, 1f)] public float wrongCodeAlarmVolume = 0.9f;
    [Tooltip("Berapa lama NPC terpancing menuju posisi player setelah alarm.")]
    public float alarmLureDuration = 12f;
    [Tooltip("Jarak trigger di sekitar destinationPoint untuk menyelesaikan game.")]
    public float destinationReachDistance = 2f;
    [Tooltip("Scene tujuan lantai berikutnya. Jika diisi, ini diprioritaskan saat game selesai.")]
    public string nextFloorSceneName = "";
    [Tooltip("Scene tujuan saat game selesai.")]
    public string mainMenuSceneName = "MainMenu";
    [Tooltip("Jika true, kunci tetap bisa mengaktifkan portal return walau player belum tercatat teleport masuk (berguna saat test/start langsung di ruang boss).")]
    public bool allowReturnWithoutEntry = true;
    [Tooltip("Sembunyikan objek visual destinasi setelah teleport lalu munculkan lagi saat kunci didapat.")]
    public GameObject[] destinationVisualObjects;

    [Header("Debug")]
    public bool showGizmos = true;

    [Header("Lingkaran Penanda")]
    public bool showCircleMarker = true;
    public Color circleColor = new Color(0f, 1f, 0.5f, 0.9f);          // warna di titik trigger
    public Color destinationCircleColor = new Color(1f, 0.5f, 0f, 0.9f); // warna di titik tujuan
    [Range(0.5f, 10f)] public float circleRadius = 2f;
    [Range(8, 64)]     public int circleSegments = 48;
    [Range(0f, 0.5f)]  public float lineWidth = 0.08f;
    public float circleYOffset = 0.05f;

    private bool playerInZone = false;
    private AudioSource audioSource;
    private float teleportCooldown = 0f;
    private bool showAutoPrompt = false;
    private bool hasTeleportedToBossRoom = false;
    private bool destinationReturnReady = false;
    private bool hasGameCompleted = false;
    private GameObject sourceMarkerObject;
    private GameObject destinationMarkerObject;

    void OnEnable()
    {
        CollectibleItem.OnItemCollected += HandleItemCollected;
    }

    void OnDisable()
    {
        CollectibleItem.OnItemCollected -= HandleItemCollected;
    }

    void Start()
    {
        EnsureColliderIsTrigger();
        ReEnableColliderIfDisabled();
        EnsureAudioSource();

        if (promptUI != null)
            promptUI.SetActive(false);

        if (showCircleMarker)
        {
            sourceMarkerObject = CreateCircleMarker(transform, circleColor);
            if (destinationPoint != null)
                destinationMarkerObject = CreateCircleMarker(destinationPoint, destinationCircleColor);
        }

        SetDestinationVisibility(true);
    }

    void Update()
    {
        if (teleportCooldown > 0f) teleportCooldown -= Time.deltaTime;

        CheckReturnCompletion();

        if (!playerInZone) return;

        // requireAccessCode selalu butuh key press agar tidak langsung pause game
        bool needKey = requireKeyPress || requireAccessCode;
        bool keyPressed = Keyboard.current != null && Keyboard.current[activationKey].wasPressedThisFrame;
        bool padPressed = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        if (needKey && (keyPressed || padPressed))
            ActivateTeleport();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag) || teleportCooldown > 0f) return;

        playerInZone = true;

        if (promptUI != null)
            promptUI.SetActive(true);
        else
            showAutoPrompt = true;

        if (!requireKeyPress && !requireAccessCode)
            ActivateTeleport();
    }

    // fallback jika OnTriggerEnter terlewat physics engine
    void OnTriggerStay(Collider other)
    {
        if (requireKeyPress || requireAccessCode || teleportCooldown > 0f) return;
        if (!other.CompareTag(playerTag)) return;
        ActivateTeleport();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInZone = false;

        if (promptUI != null)
            promptUI.SetActive(false);
        else
            showAutoPrompt = false;
    }

    void OnGUI()
    {
        if (!showAutoPrompt || promptUI != null) return;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        string msg = $"Tekan [{activationKey}] untuk masuk";
        float w = 400f, h = 50f;
        GUI.Label(new Rect((Screen.width - w) * 0.5f, Screen.height - 120f, w, h), msg, style);
    }

    void ActivateTeleport()
    {
        playerInZone = false;
        teleportCooldown = 1f;
        if (promptUI != null) promptUI.SetActive(false);

        if (requireAccessCode)
        {
            TeleportCodeUI.Instance.Show(correctCode, DoTeleport, OnCodeCancelled, OnWrongCodeEntered);
            return;
        }

        DoTeleport();
    }

    // Kode salah: bunyikan alarm dan pancing semua NPC ke posisi player.
    void OnWrongCodeEntered()
    {
        TryAutoAssignAlarmClip();

        if (wrongCodeAlarmClip != null)
            AudioSource.PlayClipAtPoint(wrongCodeAlarmClip, transform.position, Mathf.Clamp01(wrongCodeAlarmVolume));

        GameObject player = GameObject.FindWithTag(playerTag);
        Vector3 lureTarget = player != null ? player.transform.position : transform.position;

        NPCController[] npcs = FindObjectsByType<NPCController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (NPCController npc in npcs)
        {
            if (npc != null)
                npc.InvestigateNoise(lureTarget, Mathf.Max(1f, alarmLureDuration));
        }

        Debug.Log($"BossRoomTrigger: Kode salah! Alarm berbunyi, {npcs.Length} NPC menuju posisi player.");
    }

    void TryAutoAssignAlarmClip()
    {
#if UNITY_EDITOR
        if (wrongCodeAlarmClip == null)
            wrongCodeAlarmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Scenes/Lantai 6/Sound/alaram.mp3");
#endif
    }

    void OnCodeCancelled()
    {
        // Beri jeda singkat sebelum trigger bisa aktif lagi
        teleportCooldown = 1f;
    }

    void TeleportPlayer() => DoTeleport(); // backward compat

    void DoTeleport()
    {
        teleportCooldown = 2f;
        playerInZone = false;

        if (promptUI != null)
            promptUI.SetActive(false);
        showAutoPrompt = false;

        if (destinationPoint == null)
        {
            Debug.LogWarning($"BossRoomTrigger [{name}]: destinationPoint belum di-assign di Inspector!");
            return;
        }

        // Cari player berdasarkan tag lalu pindahkan posisinya
        GameObject player = GameObject.FindWithTag(playerTag);
        if (player == null)
        {
            Debug.LogWarning($"BossRoomTrigger [{name}]: GameObject dengan tag '{playerTag}' tidak ditemukan.");
            return;
        }

        StartCoroutine(TeleportCoroutine(player));
    }

    System.Collections.IEnumerator TeleportCoroutine(GameObject player)
    {
        CharacterController cc   = player.GetComponent<CharacterController>();
        PlayerMovement      pm   = player.GetComponent<PlayerMovement>();
        Animator            anim = player.GetComponentInChildren<Animator>();

        // Matikan root motion sementara agar animasi tidak menggeser posisi saat warp.
        bool hadRootMotion = anim != null && anim.applyRootMotion;
        if (hadRootMotion) anim.applyRootMotion = false;

        // Simpan semua property CC agar tidak berubah setelah disable/enable.
        float savedMinMoveDist = 0f, savedStepOffset = 0f, savedHeight = 0f, savedRadius = 0f, savedSkinWidth = 0f;
        Vector3 savedCenter = Vector3.zero;
        if (cc != null)
        {
            savedMinMoveDist = cc.minMoveDistance;
            savedStepOffset  = cc.stepOffset;
            savedHeight      = cc.height;
            savedRadius      = cc.radius;
            savedSkinWidth   = cc.skinWidth;
            savedCenter      = cc.center;
            cc.enabled = false;
        }

        // Gunakan posisi destinationPoint langsung — user sudah mengatur letaknya di Editor.
        // Scale player diperhitungkan: capsule bottom = transform.y karena center.y = height/2.
        Vector3 destPos = destinationPoint.position;
        player.transform.position = destPos;
        player.transform.rotation = destinationPoint.rotation;

        // Tunggu 1 frame agar physics engine memproses posisi baru.
        yield return null;

        if (cc != null)
        {
            cc.enabled = true;
            cc.minMoveDistance = savedMinMoveDist;
            cc.stepOffset      = savedStepOffset;
            cc.height          = savedHeight;
            cc.radius          = savedRadius;
            cc.skinWidth       = savedSkinWidth;
            cc.center          = savedCenter;
        }
        if (pm != null) pm.ResetVelocity();

        // Pulihkan root motion setelah warp selesai.
        if (hadRootMotion) anim.applyRootMotion = true;

        if (bossToActivate != null)
            bossToActivate.Activate();

        hasTeleportedToBossRoom = true;
        MarkBossRoomProgressOnAllTriggers();
        destinationReturnReady = false;
        SetDestinationVisibility(false);

        PlaySFX();
    }

    void CheckReturnCompletion()
    {
        if (!CanUseReturnFlow() || !destinationReturnReady || hasGameCompleted)
            return;

        // Pakai exitPoint jika ada, fallback ke destinationPoint
        Transform checkPoint = exitPoint != null ? exitPoint : destinationPoint;
        if (checkPoint == null) return;

        GameObject player = GameObject.FindWithTag(playerTag);
        if (player == null)
            return;

        // Jarak horizontal saja agar beda ketinggian titik keluar tidak menggagalkan cek.
        Vector3 playerFlat = new Vector3(player.transform.position.x, 0f, player.transform.position.z);
        Vector3 pointFlat = new Vector3(checkPoint.position.x, 0f, checkPoint.position.z);
        float distance = Vector3.Distance(playerFlat, pointFlat);
        if (distance > Mathf.Max(0.25f, destinationReachDistance))
            return;

        hasGameCompleted = true;
        CompleteGame();
    }

    void CompleteGame()
    {
        string targetScene = string.IsNullOrWhiteSpace(nextFloorSceneName)
            ? mainMenuSceneName
            : nextFloorSceneName;

        if (string.IsNullOrWhiteSpace(targetScene))
        {
            Debug.LogWarning("BossRoomTrigger: scene tujuan kosong. Isi nextFloorSceneName atau mainMenuSceneName.");
            return;
        }

        SceneManager.LoadScene(targetScene);
    }

    void HandleItemCollected(CollectibleItem item)
    {
        if (!CanUseReturnFlow() || destinationReturnReady || item == null)
            return;

        if (!IsRequiredKeyItem(item))
            return;

        destinationReturnReady = true;
        SetDestinationVisibility(true);
        Debug.Log("BossRoomTrigger: Kunci didapat. Destinasi kembali muncul.");
    }

    bool CanUseReturnFlow()
    {
        return hasTeleportedToBossRoom || allowReturnWithoutEntry;
    }

    void MarkBossRoomProgressOnAllTriggers()
    {
        BossRoomTrigger[] allTriggers = FindObjectsByType<BossRoomTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BossRoomTrigger trigger in allTriggers)
        {
            if (trigger != null)
                trigger.hasTeleportedToBossRoom = true;
        }
    }

    bool IsRequiredKeyItem(CollectibleItem item)
    {
        bool nameMatched = string.IsNullOrWhiteSpace(requiredKeyItemName)
            || string.Equals(item.itemName?.Trim(), requiredKeyItemName.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (!nameMatched)
            return false;

        if (!requireSecretCodeMatch)
            return true;

        return string.Equals(item.secretCode?.Trim(), requiredSecretCode?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    void SetDestinationVisibility(bool isVisible)
    {
        if (destinationMarkerObject != null)
            destinationMarkerObject.SetActive(isVisible);

        if (destinationVisualObjects == null)
            return;

        foreach (GameObject visualObject in destinationVisualObjects)
        {
            if (visualObject != null)
                visualObject.SetActive(isVisible);
        }
    }

    void PlaySFX()
    {
        if (transitionSFX == null || audioSource == null) return;
        audioSource.PlayOneShot(transitionSFX, sfxVolume);
    }

    void EnsureColliderIsTrigger()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            CapsuleCollider cap = gameObject.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.radius = circleRadius;
            cap.height = 2f;
            cap.center = Vector3.zero;
        }
        else if (!col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    void ReEnableColliderIfDisabled()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.enabled)
            col.enabled = true;
    }

    void EnsureAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    GameObject CreateCircleMarker(Transform parent, Color color)
    {
        GameObject obj = new GameObject("TeleportCircleMarker");
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = circleSegments;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = color;
        lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        lr.material = new Material(shader);
        lr.material.color = color;

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * circleRadius,
                circleYOffset,
                Mathf.Sin(angle) * circleRadius));
        }

        return obj;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }

#if UNITY_EDITOR
        Gizmos.matrix = Matrix4x4.identity;
        if (destinationPoint != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawLine(transform.position, destinationPoint.position);
            Gizmos.DrawWireSphere(destinationPoint.position, 0.4f);
            UnityEditor.Handles.Label(
                destinationPoint.position + Vector3.up * 1.2f,
                "Titik Tujuan Teleport"
            );
        }
#endif
    }
}
