using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// UI input kode akses — menggunakan New Input System.
public class TeleportCodeUI : MonoBehaviour
{
    private static TeleportCodeUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (instance != null) return;
        instance = new GameObject("TeleportCodeUI").AddComponent<TeleportCodeUI>();
        DontDestroyOnLoad(instance.gameObject);
    }

    public static TeleportCodeUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TeleportCodeUI>();
                if (instance == null)
                    instance = new GameObject("TeleportCodeUI").AddComponent<TeleportCodeUI>();
            }
            return instance;
        }
    }

    private GameObject panelRoot;
    private Text displayText;   // teks yang menampilkan input ketikan
    private Text errorText;
    private Text attemptsText;

    private string typedCode = "";
    private string correctCode;
    private System.Action onSuccess;
    private System.Action onCancel;
    private System.Action onWrongAttempt;
    private int attempts;
    private const int MaxAttempts = 3;
    private float lockTimer = 0f;
    private float cursorBlink = 0f;
    private bool isOpen = false;

    private Font fontTitle;
    private Font fontBody;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        LoadFonts();
        BuildUI();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    public bool IsOpen => isOpen;

    public void Show(string code, System.Action success, System.Action cancel, System.Action wrongAttempt = null)
    {
        correctCode = code.Trim().ToUpper();
        onSuccess  = success;
        onCancel   = cancel;
        onWrongAttempt = wrongAttempt;
        attempts   = 0;
        lockTimer  = 0f;
        typedCode  = "";
        UpdateDisplayText();
        errorText.gameObject.SetActive(false);
        attemptsText.text = $"Sisa percobaan: {MaxAttempts}";
        panelRoot.SetActive(true);
        isOpen = true;

        if (Keyboard.current != null)
            Keyboard.current.onTextInput += OnTextInput;

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void Cancel()
    {
        if (!isOpen) return;
        Close();
        onCancel?.Invoke();
    }

    // -------------------------------------------------------
    // Update — tangkap ketikan via New Input System
    // -------------------------------------------------------

    void Update()
    {
        if (!isOpen) return;

        // Blink kursor
        cursorBlink += Time.unscaledDeltaTime * 2f;
        UpdateDisplayText();

        if (lockTimer > 0f)
        {
            lockTimer -= Time.unscaledDeltaTime;
            if (lockTimer <= 0f)
            {
                typedCode = "";
                UpdateDisplayText();
                errorText.gameObject.SetActive(false);
                attemptsText.text = $"Sisa percobaan: {MaxAttempts}";
                attempts = 0;
            }
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame) { Cancel(); return; }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { TrySubmit(); return; }
        if (kb.backspaceKey.wasPressedThisFrame && typedCode.Length > 0)
        {
            typedCode = typedCode.Substring(0, typedCode.Length - 1);
            UpdateDisplayText();
        }
    }

    void OnTextInput(char c)
    {
        if (!isOpen || lockTimer > 0f) return;
        if (c == '\b' || c == '\n' || c == '\r') return;
        if (typedCode.Length < 20)
            typedCode += c;
        UpdateDisplayText();
    }

    void UpdateDisplayText()
    {
        bool showCursor = Mathf.Sin(cursorBlink * Mathf.PI) > 0f;
        displayText.text = typedCode.ToUpper() + (showCursor ? "|" : " ");
    }

    // -------------------------------------------------------
    // Submit logic
    // -------------------------------------------------------

    void TrySubmit()
    {
        string entered = typedCode.Trim().ToUpper();
        if (string.IsNullOrEmpty(entered)) return;

        if (entered == correctCode)
        {
            Close();
            onSuccess?.Invoke();
        }
        else
        {
            attempts++;
            int sisa = MaxAttempts - attempts;
            onWrongAttempt?.Invoke();

            if (sisa <= 0)
            {
                errorText.text = "AKSES DIKUNCI 5 DETIK. Kumpulkan semua dokumen.";
                errorText.gameObject.SetActive(true);
                attemptsText.text = "Sisa percobaan: 0";
                lockTimer = 5f;
            }
            else
            {
                errorText.text = "KODE SALAH. Periksa dokumen yang sudah dikumpulkan.";
                errorText.gameObject.SetActive(true);
                attemptsText.text = $"Sisa percobaan: {sisa}";
                typedCode = "";
                UpdateDisplayText();
            }
        }
    }

    void Close()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnTextInput;
        panelRoot.SetActive(false);
        isOpen = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // -------------------------------------------------------
    // UI Builder
    // -------------------------------------------------------

    void LoadFonts()
    {
        fontTitle = Resources.Load<Font>("BebasNeue-Regular")
                    ?? Font.CreateDynamicFontFromOSFont("Georgia", 1)
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fontBody  = Resources.Load<Font>("IBMPlexMono-Regular")
                    ?? Font.CreateDynamicFontFromOSFont("Courier New", 1)
                    ?? fontTitle;
    }

    void BuildUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("TeleportCodeCanvas");
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Overlay gelap
        panelRoot = new GameObject("CodePanelRoot");
        panelRoot.transform.SetParent(canvasObj.transform, false);
        RectTransform rootRect = panelRoot.AddComponent<RectTransform>();
        Stretch(rootRect);
        panelRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

        // Panel tengah
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(panelRoot.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin  = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax  = new Vector2(0.5f, 0.5f);
        panelRect.pivot      = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta  = new Vector2(700f, 440f);
        panel.AddComponent<Image>().color = new Color(0.07f, 0.05f, 0.04f, 0.98f);
        AddEdgeLine(panel.transform, true,  new Color(0.75f, 0.3f, 0.1f, 1f), 4f);
        AddEdgeLine(panel.transform, false, new Color(0.4f, 0.18f, 0.05f, 1f), 2f);

        float y = -20f;

        // Judul
        Text title = MakeText(panel.transform, "KODE AKSES DIPERLUKAN", 42,
            TextAnchor.MiddleCenter, new Color(0.96f, 0.84f, 0.46f), fontTitle);
        Place(title.rectTransform, y, 640f, 52f); y -= 58f;

        AddFixedLine(panel.transform, y, new Color(0.5f, 0.22f, 0.07f, 0.7f), 1f); y -= 18f;

        // Keterangan
        Text hint = MakeText(panel.transform,
            "Masukkan kode yang ditemukan di dokumen yang telah dikumpulkan.", 22,
            TextAnchor.MiddleCenter, new Color(0.75f, 0.72f, 0.66f), fontBody);
        hint.lineSpacing = 1.3f;
        Place(hint.rectTransform, y, 620f, 50f); y -= 62f;

        // Kotak tampilan kode ketikan
        GameObject box = new GameObject("CodeBox");
        box.transform.SetParent(panel.transform, false);
        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin      = new Vector2(0.5f, 1f);
        boxRect.anchorMax      = new Vector2(0.5f, 1f);
        boxRect.pivot          = new Vector2(0.5f, 1f);
        boxRect.sizeDelta      = new Vector2(500f, 70f);
        boxRect.anchoredPosition = new Vector2(0f, y);
        box.AddComponent<Image>().color = new Color(0.12f, 0.09f, 0.07f, 1f);
        AddFixedLine(box.transform, -70f, new Color(0.75f, 0.3f, 0.1f, 1f), 2f, 500f);

        // Teks yang menampilkan ketikan
        displayText = MakeText(box.transform, "|", 36,
            TextAnchor.MiddleCenter, new Color(0.96f, 0.84f, 0.46f), fontTitle);
        Stretch(displayText.rectTransform);
        y -= 84f;

        // Petunjuk tombol
        Text keyHint = MakeText(panel.transform, "[ENTER] Konfirmasi     [ESC] Batal     [BACKSPACE] Hapus", 18,
            TextAnchor.MiddleCenter, new Color(0.55f, 0.52f, 0.48f), fontBody);
        Place(keyHint.rectTransform, y, 640f, 26f); y -= 38f;

        // Error
        errorText = MakeText(panel.transform, "", 24,
            TextAnchor.MiddleCenter, new Color(0.95f, 0.2f, 0.2f, 1f), fontBody);
        Place(errorText.rectTransform, y, 620f, 34f);
        errorText.gameObject.SetActive(false); y -= 40f;

        // Sisa percobaan
        attemptsText = MakeText(panel.transform, $"Sisa percobaan: {MaxAttempts}", 20,
            TextAnchor.MiddleCenter, new Color(0.6f, 0.58f, 0.54f), fontBody);
        Place(attemptsText.rectTransform, y, 620f, 28f);

        panelRoot.SetActive(false);
    }

    // -------------------------------------------------------
    // Layout helpers
    // -------------------------------------------------------

    Text MakeText(Transform parent, string content, int size, TextAnchor anchor, Color color, Font font)
    {
        GameObject obj = new GameObject("Txt");
        obj.transform.SetParent(parent, false);
        Text t = obj.AddComponent<Text>();
        t.text           = content;
        t.font           = font ?? fontBody;
        t.fontSize       = size;
        t.alignment      = anchor;
        t.color          = color;
        t.supportRichText = false;
        return t;
    }

    void Place(RectTransform r, float yFromTop, float width, float height)
    {
        r.anchorMin        = new Vector2(0.5f, 1f);
        r.anchorMax        = new Vector2(0.5f, 1f);
        r.pivot            = new Vector2(0.5f, 1f);
        r.sizeDelta        = new Vector2(width, height);
        r.anchoredPosition = new Vector2(0f, yFromTop);
    }

    void Stretch(RectTransform r)
    {
        r.anchorMin  = Vector2.zero;
        r.anchorMax  = Vector2.one;
        r.offsetMin  = Vector2.zero;
        r.offsetMax  = Vector2.zero;
    }

    void AddEdgeLine(Transform parent, bool top, Color color, float h)
    {
        GameObject obj = new GameObject(top ? "LineTop" : "LineBot");
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, top ? 1f : 0f);
        r.anchorMax = new Vector2(1f, top ? 1f : 0f);
        r.pivot     = new Vector2(0.5f, top ? 1f : 0f);
        r.sizeDelta = new Vector2(0f, h);
        r.anchoredPosition = Vector2.zero;
        obj.AddComponent<Image>().color = color;
    }

    void AddFixedLine(Transform parent, float yFromTop, Color color, float h, float width = 680f)
    {
        GameObject obj = new GameObject("Line");
        obj.transform.SetParent(parent, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 1f);
        r.anchorMax        = new Vector2(0.5f, 1f);
        r.pivot            = new Vector2(0.5f, 1f);
        r.sizeDelta        = new Vector2(width, h);
        r.anchoredPosition = new Vector2(0f, yFromTop);
        obj.AddComponent<Image>().color = color;
    }
}
