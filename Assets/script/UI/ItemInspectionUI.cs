using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI pop-up item ala Outlast/Resident Evil. Dibuat otomatis saat runtime.
// Untuk font kustom: salin BebasNeue-Regular.ttf & IBMPlexMono-Regular.ttf ke Assets/Resources/
[DefaultExecutionOrder(1001)]
public class ItemInspectionUI : MonoBehaviour
{
    private static ItemInspectionUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (instance != null)
            return;
        instance = new GameObject("ItemInspectionUI").AddComponent<ItemInspectionUI>();
        DontDestroyOnLoad(instance.gameObject);
    }

    public static ItemInspectionUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ItemInspectionUI>();
                if (instance == null)
                    instance = new GameObject("ItemInspectionUI").AddComponent<ItemInspectionUI>();
            }
            return instance;
        }
    }

    [Header("Pengaturan")]
    public bool pauseGameWhilePopupOpen = true;
    public float popupAnimationDuration = 0.3f;

    // Panel landscape: gambar di kiri, teks di kanan.
    private const float PanelW = 960f;
    private const float PanelH = 560f;
    private const float ImageAreaW = 300f;
    private const float TitleH = 72f;
    private const float ButtonH = 62f;

    private Font fontTitle;
    private Font fontBody;
    private GameObject promptRoot;
    private Text promptText;
    private GameObject popupRoot;
    private RectTransform popupPanelRect;
    private Image popupItemImage;
    private Text popupTitleText;
    private Text popupDescriptionText;
    private Text popupCodeText;
    private CollectibleItem currentPromptItem;
    private bool isPopupOpen;
    private float animTimer;
    private float previousTimeScale = 1f;

    private Transform codesListContent;
    private int codesListCount = 0;
    private const float CodeRowH = 38f;
    private System.Collections.Generic.List<(string itemName, string code)> collectedCodes = new System.Collections.Generic.List<(string, string)>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        LoadFonts();
        BuildUI();
        EnsureEventSystem();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearCollectedCodes();
    }

    // Kode PIN yang terkumpul hanya berlaku untuk lantai tempat ia ditemukan.
    public void ClearCollectedCodes()
    {
        collectedCodes.Clear();
        codesListCount = 0;

        if (codesListContent == null)
            return;

        for (int i = codesListContent.childCount - 1; i >= 0; i--)
            Destroy(codesListContent.GetChild(i).gameObject);

        RectTransform panelRect = codesListContent.parent.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, 0f);
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    // -------------------------------------------------------
    // Font loading — Resources → system font → builtin
    // -------------------------------------------------------

    void LoadFonts()
    {
        fontTitle = Resources.Load<Font>("BebasNeue-Regular");
        if (fontTitle == null)
            fontTitle = Font.CreateDynamicFontFromOSFont("Georgia", 1);
        if (fontTitle == null)
            fontTitle = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        fontBody = Resources.Load<Font>("IBMPlexMono-Regular");
        if (fontBody == null)
            fontBody = Font.CreateDynamicFontFromOSFont("Courier New", 1);
        if (fontBody == null)
            fontBody = fontTitle;
    }

    // -------------------------------------------------------
    // Update / input
    // -------------------------------------------------------

    void Update()
    {
        if (isPopupOpen)
        {
            AnimatePopup();
            Keyboard kb = Keyboard.current;
            bool closeKey = kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
            bool closePad = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (closeKey || closePad)
                ClosePopup();
            return;
        }

        if (currentPromptItem != null)
        {
            Keyboard kb = Keyboard.current;
            bool collectKey = kb != null && kb.eKey.wasPressedThisFrame;
            bool collectPad = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (collectKey || collectPad)
                currentPromptItem.Collect();
        }
    }

    // -------------------------------------------------------
    // API yang dipanggil CollectibleItem
    // -------------------------------------------------------

    public void ShowPrompt(CollectibleItem item)
    {
        currentPromptItem = item;
        promptText.text = "[E]  Ambil  " + item.itemName.ToUpper();
        promptRoot.SetActive(!isPopupOpen);
    }

    public void HidePrompt(CollectibleItem item)
    {
        if (currentPromptItem != item)
            return;
        currentPromptItem = null;
        promptRoot.SetActive(false);
    }

    public void ShowItemPopup(CollectibleItem item)
    {
        currentPromptItem = null;
        promptRoot.SetActive(false);

        popupTitleText.text = item.itemName.ToUpper();
        popupDescriptionText.text = item.itemDescription;

        bool hasSprite = item.itemSprite != null;
        popupItemImage.gameObject.SetActive(hasSprite);
        if (hasSprite)
            popupItemImage.sprite = item.itemSprite;

        bool hasCode = !string.IsNullOrEmpty(item.secretCode);
        popupCodeText.gameObject.SetActive(hasCode);
        if (hasCode)
        {
            popupCodeText.text = "» KODE SANDI :  " + item.secretCode + "  «";
            AddCollectedCode(item.itemName, item.secretCode);
        }

        isPopupOpen = true;
        animTimer = 0f;
        popupPanelRect.localScale = new Vector3(0.85f, 0.85f, 1f);
        popupRoot.SetActive(true);

        if (pauseGameWhilePopupOpen)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ClosePopup()
    {
        if (!isPopupOpen)
            return;
        isPopupOpen = false;
        popupRoot.SetActive(false);

        if (pauseGameWhilePopupOpen)
            Time.timeScale = previousTimeScale;
    }

    void AnimatePopup()
    {
        animTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(animTimer / Mathf.Max(0.01f, popupAnimationDuration));
        // Ease-out cubic.
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        float s = Mathf.Lerp(0.85f, 1f, eased);
        popupPanelRect.localScale = new Vector3(s, s, 1f);
    }

    // -------------------------------------------------------
    // Pembuatan UI
    // -------------------------------------------------------

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    void BuildUI()
    {
        GameObject canvasObj = new GameObject("ItemInspectionCanvas");
        canvasObj.transform.SetParent(transform, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObj.AddComponent<GraphicRaycaster>();

        BuildPrompt(canvasObj.transform);
        BuildPopup(canvasObj.transform);
        BuildCollectedCodesPanel(canvasObj.transform);
    }

    // ---------- Prompt [E] ----------

    void BuildPrompt(Transform parent)
    {
        promptRoot = new GameObject("InteractPrompt");
        promptRoot.transform.SetParent(parent, false);

        RectTransform rect = promptRoot.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 110f);
        rect.sizeDelta = new Vector2(520f, 54f);

        Image bg = promptRoot.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.02f, 0.02f, 0.88f);

        // Aksen merah di atas prompt.
        GameObject border = new GameObject("Border");
        border.transform.SetParent(promptRoot.transform, false);
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0f, 1f);
        borderRect.anchorMax = new Vector2(1f, 1f);
        borderRect.pivot = new Vector2(0.5f, 1f);
        borderRect.sizeDelta = new Vector2(0f, 3f);
        borderRect.anchoredPosition = Vector2.zero;
        border.AddComponent<Image>().color = new Color(0.85f, 0.1f, 0.1f, 1f);

        promptText = CreateText(promptRoot.transform, "PromptText", "[E]  AMBIL", 26,
            TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.85f), fontTitle);
        StretchToParent(promptText.rectTransform, 12f, 0f);

        promptRoot.SetActive(false);
    }

    // ---------- Pop-up Inspeksi (landscape: gambar kiri + teks kanan) ----------

    void BuildPopup(Transform parent)
    {
        popupRoot = new GameObject("ItemPopup");
        popupRoot.transform.SetParent(parent, false);
        RectTransform rootRect = popupRoot.AddComponent<RectTransform>();
        StretchToParent(rootRect);
        popupRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(popupRoot.transform, false);
        popupPanelRect = panel.AddComponent<RectTransform>();
        popupPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupPanelRect.pivot = new Vector2(0.5f, 0.5f);
        popupPanelRect.sizeDelta = new Vector2(PanelW, PanelH);
        panel.AddComponent<Image>().color = new Color(0.07f, 0.06f, 0.05f, 0.97f);

        AddHorizontalAccent(panel.transform, true,  new Color(0.75f, 0.3f,  0.1f, 1f), 4f);
        AddHorizontalAccent(panel.transform, false, new Color(0.4f,  0.18f, 0.05f, 1f), 2f);

        BuildTitleBar(panel.transform);
        AddHorizontalAccentAt(panel.transform, TitleH, new Color(0.5f, 0.22f, 0.07f, 0.8f), 1f);

        float contentBottom = ButtonH;
        float contentH = PanelH - TitleH - contentBottom;

        BuildImageArea(panel.transform, contentH, contentBottom);
        AddVerticalDivider(panel.transform, ImageAreaW, contentBottom);
        BuildTextArea(panel.transform, contentH, contentBottom);
        BuildCloseButton(panel.transform);

        popupRoot.SetActive(false);
    }

    void BuildTitleBar(Transform panel)
    {
        popupTitleText = CreateText(panel, "Title", "NAMA ITEM", 46,
            TextAnchor.MiddleCenter, new Color(0.96f, 0.84f, 0.46f), fontTitle);
        RectTransform r = popupTitleText.rectTransform;
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-40f, TitleH);
        r.anchoredPosition = Vector2.zero;
    }

    void BuildImageArea(Transform panel, float contentH, float contentBottom)
    {
        GameObject holder = new GameObject("ImageHolder");
        holder.transform.SetParent(panel, false);
        RectTransform holderRect = holder.AddComponent<RectTransform>();
        holderRect.anchorMin = new Vector2(0f, 0f);
        holderRect.anchorMax = new Vector2(0f, 1f);
        holderRect.pivot = new Vector2(0f, 0.5f);
        holderRect.offsetMin = new Vector2(0f, contentBottom);
        holderRect.offsetMax = new Vector2(ImageAreaW, -TitleH);

        float imgSize = Mathf.Min(ImageAreaW - 40f, contentH - 40f);
        GameObject imgObj = new GameObject("ItemImage");
        imgObj.transform.SetParent(holder.transform, false);
        RectTransform imgRect = imgObj.AddComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0.5f, 0.5f);
        imgRect.anchorMax = new Vector2(0.5f, 0.5f);
        imgRect.pivot = new Vector2(0.5f, 0.5f);
        imgRect.sizeDelta = new Vector2(imgSize, imgSize);
        imgRect.anchoredPosition = Vector2.zero;

        popupItemImage = imgObj.AddComponent<Image>();
        popupItemImage.preserveAspect = true;

        Outline outline = imgObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.5f, 0.25f, 0.05f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    void BuildTextArea(Transform panel, float contentH, float contentBottom)
    {
        float x = ImageAreaW + 16f;
        float w = PanelW - ImageAreaW - 32f;

        // Deskripsi mengisi area atas-kanan.
        popupDescriptionText = CreateText(panel, "Description", "", 22,
            TextAnchor.UpperLeft, new Color(0.88f, 0.86f, 0.8f), fontBody);
        RectTransform descRect = popupDescriptionText.rectTransform;
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(0f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descRect.offsetMin = new Vector2(x, contentBottom + 54f);
        descRect.offsetMax = new Vector2(x + w, -TitleH - 12f);
        popupDescriptionText.lineSpacing = 1.3f;

        // Kode sandi di bawah deskripsi, tepat di atas tombol.
        popupCodeText = CreateText(panel, "SecretCode", "", 34,
            TextAnchor.MiddleLeft, new Color(0.95f, 0.25f, 0.2f), fontTitle);
        RectTransform codeRect = popupCodeText.rectTransform;
        codeRect.anchorMin = new Vector2(0f, 0f);
        codeRect.anchorMax = new Vector2(0f, 0f);
        codeRect.pivot = new Vector2(0f, 0f);
        codeRect.sizeDelta = new Vector2(w, 50f);
        codeRect.anchoredPosition = new Vector2(x, contentBottom + 4f);
        popupCodeText.gameObject.AddComponent<Shadow>().effectColor = new Color(1f, 0f, 0f, 0.3f);
    }

    void BuildCloseButton(Transform panel)
    {
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(panel, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.sizeDelta = new Vector2(280f, ButtonH - 12f);
        btnRect.anchoredPosition = new Vector2(0f, 8f);

        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.1f, 0.06f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.35f, 0.18f, 0.05f, 1f);
        cb.pressedColor    = new Color(0.5f,  0.25f, 0.05f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(ClosePopup);

        // Aksen garis atas tombol.
        GameObject btnBorder = new GameObject("Border");
        btnBorder.transform.SetParent(btnObj.transform, false);
        RectTransform bbr = btnBorder.AddComponent<RectTransform>();
        bbr.anchorMin = new Vector2(0f, 1f);
        bbr.anchorMax = new Vector2(1f, 1f);
        bbr.pivot = new Vector2(0.5f, 1f);
        bbr.sizeDelta = new Vector2(0f, 2f);
        bbr.anchoredPosition = Vector2.zero;
        btnBorder.AddComponent<Image>().color = new Color(0.75f, 0.3f, 0.1f, 1f);

        Text btnText = CreateText(btnObj.transform, "Label", "TUTUP  [E]", 28,
            TextAnchor.MiddleCenter, new Color(0.95f, 0.84f, 0.46f), fontTitle);
        StretchToParent(btnText.rectTransform);
    }

    // ---------- Panel kode terkumpul (pojok kanan atas) ----------

    void BuildCollectedCodesPanel(Transform parent)
    {
        // Panel luar — lebar tetap, tinggi berkembang ke bawah.
        GameObject panel = new GameObject("CollectedCodesPanel");
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot    = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(340f, 0f);   // tinggi 0 = tersembunyi sampai ada isi

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.04f, 0.03f, 0.88f);

        // Garis kiri oranye sebagai aksen.
        GameObject accent = new GameObject("Accent");
        accent.transform.SetParent(panel.transform, false);
        RectTransform ar = accent.AddComponent<RectTransform>();
        ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(0f, 1f);
        ar.pivot = new Vector2(0f, 0.5f);
        ar.sizeDelta = new Vector2(3f, 0f);
        ar.offsetMin = Vector2.zero; ar.offsetMax = new Vector2(3f, 0f);
        accent.AddComponent<Image>().color = new Color(0.75f, 0.3f, 0.1f, 1f);

        // Header "PETUNJUK KODE".
        Text header = CreateText(panel.transform, "Header", "PETUNJUK KODE", 20,
            TextAnchor.MiddleLeft, new Color(0.75f, 0.3f, 0.1f, 1f), fontTitle);
        RectTransform hr = header.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(-18f, 36f);
        hr.anchoredPosition = new Vector2(9f, 0f);

        // Garis pemisah di bawah header.
        GameObject divider = new GameObject("Divider");
        divider.transform.SetParent(panel.transform, false);
        RectTransform dr = divider.AddComponent<RectTransform>();
        dr.anchorMin = new Vector2(0f, 1f);
        dr.anchorMax = new Vector2(1f, 1f);
        dr.pivot = new Vector2(0.5f, 1f);
        dr.sizeDelta = new Vector2(-12f, 1f);
        dr.anchoredPosition = new Vector2(0f, -36f);
        divider.AddComponent<Image>().color = new Color(0.5f, 0.22f, 0.07f, 0.7f);

        // Container untuk baris kode (tumbuh ke bawah).
        GameObject content = new GameObject("Content");
        content.transform.SetParent(panel.transform, false);
        RectTransform cr = content.AddComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(0f, 0f);
        cr.anchoredPosition = new Vector2(0f, -38f);

        codesListContent = content.transform;
    }

    // Ambil kode akses portal bos di scene aktif (jika ada).
    string FindBossPortalCode()
    {
        BossRoomTrigger[] triggers = FindObjectsByType<BossRoomTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BossRoomTrigger trigger in triggers)
        {
            if (trigger != null && trigger.requireAccessCode && !string.IsNullOrEmpty(trigger.correctCode))
                return trigger.correctCode.Trim();
        }
        return null;
    }

    // Tambahkan satu baris kode. Dipanggil saat item berkode diambil.
    void AddCollectedCode(string itemName, string code)
    {
        if (codesListContent == null)
            return;

        // Cegah duplikat.
        foreach (var entry in collectedCodes)
            if (entry.itemName == itemName) return;

        collectedCodes.Add((itemName, code));

        // Urutkan mengikuti posisi digit di kode portal bos; fallback ascending numerik.
        // Tiap posisi kode hanya boleh dipakai satu item (penting saat ada digit kembar, mis. "010").
        string portalCode = FindBossPortalCode();
        var remaining = new System.Collections.Generic.List<(string itemName, string code)>(collectedCodes);
        var ordered = new System.Collections.Generic.List<(string itemName, string code)>();

        if (!string.IsNullOrEmpty(portalCode))
        {
            int pos = 0;
            while (pos < portalCode.Length && remaining.Count > 0)
            {
                int match = remaining.FindIndex(e =>
                    !string.IsNullOrEmpty(e.code) &&
                    pos + e.code.Length <= portalCode.Length &&
                    string.CompareOrdinal(portalCode, pos, e.code, 0, e.code.Length) == 0);
                if (match >= 0)
                {
                    ordered.Add(remaining[match]);
                    pos += remaining[match].code.Length;
                    remaining.RemoveAt(match);
                }
                else
                {
                    pos++;
                }
            }
        }

        // Sisa yang tidak cocok dengan kode portal: fallback ascending numerik.
        remaining.Sort((a, b) =>
        {
            bool aNum = int.TryParse(a.code, out int aVal);
            bool bNum = int.TryParse(b.code, out int bVal);
            if (aNum && bNum) return aVal.CompareTo(bVal);
            return string.Compare(a.code, b.code, System.StringComparison.Ordinal);
        });
        ordered.AddRange(remaining);
        collectedCodes = ordered;

        // Hapus semua baris lama lalu bangun ulang sesuai urutan.
        for (int i = codesListContent.childCount - 1; i >= 0; i--)
            Destroy(codesListContent.GetChild(i).gameObject);

        codesListCount = 0;
        foreach (var entry in collectedCodes)
        {
            float yOffset = -(codesListCount * CodeRowH);

            GameObject row = new GameObject("Row_" + entry.itemName);
            row.transform.SetParent(codesListContent, false);
            RectTransform rr = row.AddComponent<RectTransform>();
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = new Vector2(0f, CodeRowH);
            rr.anchoredPosition = new Vector2(0f, yOffset);

            Text nameText = CreateText(row.transform, "Name",
                entry.itemName, 19, TextAnchor.MiddleLeft, new Color(0.85f, 0.83f, 0.78f), fontBody);
            RectTransform nr = nameText.rectTransform;
            nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one;
            nr.offsetMin = new Vector2(14f, 0f);
            nr.offsetMax = new Vector2(-90f, 0f);

            Text codeText = CreateText(row.transform, "Code",
                entry.code, 26, TextAnchor.MiddleRight, new Color(0.96f, 0.84f, 0.46f), fontTitle);
            RectTransform cdr = codeText.rectTransform;
            cdr.anchorMin = new Vector2(1f, 0f); cdr.anchorMax = Vector2.one;
            cdr.offsetMin = new Vector2(-80f, 0f);
            cdr.offsetMax = new Vector2(-10f, 0f);

            codesListCount++;
        }

        RectTransform panelRect = codesListContent.parent.GetComponent<RectTransform>();
        float totalH = 38f + codesListCount * CodeRowH + 6f;
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, totalH);
    }

    // -------------------------------------------------------
    // Helper
    // -------------------------------------------------------

    void AddHorizontalAccent(Transform panel, bool top, Color color, float height)
    {
        GameObject obj = new GameObject(top ? "AccentTop" : "AccentBottom");
        obj.transform.SetParent(panel, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        float ay = top ? 1f : 0f;
        r.anchorMin = new Vector2(0f, ay);
        r.anchorMax = new Vector2(1f, ay);
        r.pivot = new Vector2(0.5f, ay);
        r.sizeDelta = new Vector2(0f, height);
        r.anchoredPosition = Vector2.zero;
        obj.AddComponent<Image>().color = color;
    }

    void AddHorizontalAccentAt(Transform panel, float fromTop, Color color, float height)
    {
        GameObject obj = new GameObject("Divider");
        obj.transform.SetParent(panel, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(0f, height);
        r.anchoredPosition = new Vector2(0f, -fromTop);
        obj.AddComponent<Image>().color = color;
    }

    void AddVerticalDivider(Transform panel, float fromLeft, float contentBottom)
    {
        GameObject obj = new GameObject("VDivider");
        obj.transform.SetParent(panel, false);
        RectTransform r = obj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = new Vector2(fromLeft,     contentBottom);
        r.offsetMax = new Vector2(fromLeft + 1f, -TitleH);
        obj.AddComponent<Image>().color = new Color(0.5f, 0.22f, 0.07f, 0.6f);
    }

    Text CreateText(Transform parent, string name, string content, int fontSize,
        TextAnchor alignment, Color color, Font font)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        Text text = obj.AddComponent<Text>();
        text.font = font != null ? font : fontBody;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        return text;
    }

    void StretchToParent(RectTransform rect, float padH = 0f, float padV = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padH, padV);
        rect.offsetMax = new Vector2(-padH, -padV);
    }
}
