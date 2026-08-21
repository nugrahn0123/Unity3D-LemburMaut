using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class GameOverStats
{
    public static float SurvivalTime;
    public static int FloorReached;
    public static int SpottedCount;
}

public class GameOverController : MonoBehaviour
{
    [Header("Auto Build UI")]
    [SerializeField] private bool autoBuildIfMissing = true;

    [Header("Scene Names (must match Build Settings)")]
    [SerializeField] private string gameplaySceneName = "Scene Lantai 6";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Typography")]
    [SerializeField] private TMP_FontAsset displayFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private TMP_FontAsset monoFont;

    [Header("UI References")]
    [SerializeField] private Image fluorescentTube;
    [SerializeField] private Image vignetteOverlay;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI survivalTimeText;
    [SerializeField] private TextMeshProUGUI floorReachedText;
    [SerializeField] private TextMeshProUGUI spottedCountText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("FX")]
    [SerializeField] private float vignettePulseSpeed = 0.22f;
    [SerializeField] private float introDuration = 0.45f;
    [SerializeField] private float glitchMinDelay = 2.6f;
    [SerializeField] private float glitchMaxDelay = 4.6f;

    private EventSystem activeEventSystem;
    private CanvasGroup cardGroup;
    private CanvasGroup statsGroup;
    private CanvasGroup buttonGroup;
    private RectTransform titleRect;
    private RectTransform scanlineRect;
    private Image scanlineImage;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureFallbackCamera();
        activeEventSystem = EnsureEventSystemExists();
        EnsureUiReady();

        BindButtons();
        DisplayStats();

        if (activeEventSystem != null && restartButton != null)
            activeEventSystem.SetSelectedGameObject(restartButton.gameObject);

        StartCoroutine(PlayIntro());
        if (vignetteOverlay != null) StartCoroutine(PulseVignette());
        if (fluorescentTube != null) StartCoroutine(FlickerTube());
        if (titleRect != null) StartCoroutine(GlitchTitle());
        if (scanlineRect != null && scanlineImage != null) StartCoroutine(ScanlineSweep());
    }

    private void BindButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartGame);
            restartButton.interactable = false;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(GoToMainMenu);
            mainMenuButton.interactable = false;
        }
    }

    [ContextMenu("Rebuild Game Over Canvas")]
    public void RebuildCanvas()
    {
        GameObject existing = GameObject.Find("GameOverCanvas");
        if (existing != null)
            DestroyImmediate(existing);

        EnsureUiReady(forceBuild: true);
    }

    private void EnsureUiReady(bool forceBuild = false)
    {
        bool missing = titleText == null || survivalTimeText == null || floorReachedText == null ||
                       spottedCountText == null || restartButton == null || mainMenuButton == null ||
                       vignetteOverlay == null;

        if (!forceBuild && (!autoBuildIfMissing || !missing))
            return;

        BuildRuntimeCanvas();
    }

    private void BuildRuntimeCanvas()
    {
        TryAutoAssignRecommendedFonts();
        EnsureEventSystemExists();

        GameObject canvasGo = new GameObject("GameOverCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        Stretch(root);

        Image bg = CreateImage("Background", root, new Color(0.03f, 0.01f, 0.015f, 1f));
        Stretch(bg.rectTransform);

        Image edgeRed = CreateImage("EdgeRed", root, new Color(0.30f, 0.01f, 0.02f, 0.26f));
        Stretch(edgeRed.rectTransform);

        vignetteOverlay = CreateImage("Vignette", root, new Color(0f, 0f, 0f, 0.72f));
        Stretch(vignetteOverlay.rectTransform);

        GameObject content = CreateUiObject("Content", root);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(980f, 760f);
        contentRect.anchoredPosition = Vector2.zero;

        Image lampGlow = CreateImage("LampGlow", contentRect, new Color(0.72f, 1f, 0.47f, 0.14f));
        lampGlow.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        lampGlow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        lampGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        lampGlow.rectTransform.sizeDelta = new Vector2(900f, 28f);
        lampGlow.rectTransform.anchoredPosition = new Vector2(0f, -46f);

        fluorescentTube = CreateImage("FluorescentTube", contentRect, new Color(0.72f, 1f, 0.47f, 0.95f));
        fluorescentTube.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        fluorescentTube.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        fluorescentTube.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        fluorescentTube.rectTransform.sizeDelta = new Vector2(860f, 10f);
        fluorescentTube.rectTransform.anchoredPosition = new Vector2(0f, -46f);

        Image card = CreateImage("Card", contentRect, new Color(0.06f, 0.06f, 0.07f, 0.92f));
        card.rectTransform.anchorMin = new Vector2(0.16f, 0.17f);
        card.rectTransform.anchorMax = new Vector2(0.84f, 0.80f);
        card.rectTransform.offsetMin = Vector2.zero;
        card.rectTransform.offsetMax = Vector2.zero;
        RectTransform cardRect = card.rectTransform;
        Outline border = card.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.18f, 0.18f, 0.18f, 0.72f);
        border.effectDistance = new Vector2(1f, -1f);
        cardGroup = AddCanvasGroup(card.gameObject);

        TextMeshProUGUI titleShadow = CreateText("TitleShadow", cardRect, "GAME OVER", 96f,
            new Color(0.20f, 0.03f, 0.03f, 0.92f), FontStyles.UpperCase | FontStyles.Bold, displayFont);
        titleShadow.rectTransform.anchoredPosition = new Vector2(4f, 122f);
        titleShadow.characterSpacing = 6f;
        titleShadow.rectTransform.sizeDelta = new Vector2(620f, 90f);

        titleText = CreateText("Title", cardRect, "GAME OVER", 96f,
            new Color(0.92f, 0.17f, 0.21f, 1f), FontStyles.UpperCase | FontStyles.Bold, displayFont);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 124f);
        titleText.characterSpacing = 6f;
        titleText.rectTransform.sizeDelta = new Vector2(620f, 90f);
        titleRect = titleText.rectTransform;

        TextMeshProUGUI line = CreateText("Message", cardRect,
            "Lemburmu berakhir... <color=#A8DB6D>selamanya.</color>",
            36f, new Color(0.80f, 0.82f, 0.78f, 0.9f), FontStyles.Normal, bodyFont);
        line.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        line.rectTransform.sizeDelta = new Vector2(600f, 78f);

        Image divider = CreateImage("Divider", cardRect, new Color(0.7f, 0.09f, 0.1f, 0.75f));
        divider.rectTransform.sizeDelta = new Vector2(112f, 2f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -54f);

        GameObject statsRow = CreateUiObject("StatsRow", cardRect);
        RectTransform statsRect = statsRow.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0.5f, 0.5f);
        statsRect.anchorMax = new Vector2(0.5f, 0.5f);
        statsRect.pivot = new Vector2(0.5f, 0.5f);
        statsRect.sizeDelta = new Vector2(620f, 130f);
        statsRect.anchoredPosition = new Vector2(0f, -128f);
        statsGroup = AddCanvasGroup(statsRow);

        CreateStatColumn(statsRect, -200f, "WAKTU BERTAHAN", out survivalTimeText, "00:00", monoFont, monoFont);
        CreateStatColumn(statsRect, 0f, "LANTAI TERCAPAI", out floorReachedText, "0", monoFont, monoFont);
        CreateStatColumn(statsRect, 200f, "TERLIHAT", out spottedCountText, "0X", monoFont, monoFont);

        restartButton = CreateButton(contentRect, "RestartButton", "MULAI LAGI",
            new Vector2(0f, -292f), new Vector2(500f, 76f),
            new Color(0.60f, 0.07f, 0.08f, 1f), new Color(0.76f, 0.13f, 0.14f, 1f),
            false, 38f, displayFont);

        mainMenuButton = CreateButton(contentRect, "MainMenuButton", "KEMBALI KE MENU UTAMA",
            new Vector2(0f, -388f), new Vector2(500f, 76f),
            new Color(0.08f, 0.09f, 0.10f, 0.95f), new Color(0.18f, 0.18f, 0.20f, 0.98f),
            true, 30f, displayFont);

        buttonGroup = AddCanvasGroup(CreateButtonsGroup(contentRect, restartButton, mainMenuButton));

        scanlineImage = CreateImage("Scanline", contentRect, new Color(0.82f, 1f, 0.86f, 0.02f));
        scanlineRect = scanlineImage.rectTransform;
        scanlineRect.anchorMin = new Vector2(0.5f, 0.5f);
        scanlineRect.anchorMax = new Vector2(0.5f, 0.5f);
        scanlineRect.pivot = new Vector2(0.5f, 0.5f);
        scanlineRect.sizeDelta = new Vector2(620f, 2f);
        scanlineRect.anchoredPosition = new Vector2(0f, 120f);
    }

    private static GameObject CreateButtonsGroup(RectTransform content, Button restart, Button menu)
    {
        GameObject group = CreateUiObject("ButtonsRoot", content);
        RectTransform rect = group.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        restart.transform.SetParent(rect, false);
        menu.transform.SetParent(rect, false);
        return group;
    }

    private static EventSystem EnsureEventSystemExists()
    {
        EventSystem[] all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem current = all.Length > 0 ? all[0] : null;

        if (current == null)
            current = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i] != current)
            {
                all[i].enabled = false;
                all[i].gameObject.SetActive(false);
            }
        }

        current.enabled = true;
        current.gameObject.SetActive(true);

#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule legacy = current.GetComponent<StandaloneInputModule>();
        if (legacy != null) DestroyImmediate(legacy);
        if (current.GetComponent<InputSystemUIInputModule>() == null)
            current.gameObject.AddComponent<InputSystemUIInputModule>();
#else
        if (current.GetComponent<StandaloneInputModule>() == null)
            current.gameObject.AddComponent<StandaloneInputModule>();
#endif
        return current;
    }

    private static void EnsureFallbackCamera()
    {
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cams.Length; i++)
        {
            if (cams[i] != null && cams[i].isActiveAndEnabled && cams[i].targetDisplay == 0)
                return;
        }

        GameObject cameraGo = new GameObject("GameOverCamera", typeof(Camera));
        Camera cam = cameraGo.GetComponent<Camera>();
        cam.targetDisplay = 0;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.02f, 1f);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 60f;
    }

    private static GameObject CreateUiObject(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return go;
    }

    private static CanvasGroup AddCanvasGroup(GameObject go)
    {
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject go = CreateUiObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, RectTransform parent, string value, float size, Color color, FontStyles style, TMP_FontAsset font = null)
    {
        GameObject go = CreateUiObject(name, parent);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        if (font != null) t.font = font;
        else if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.rectTransform.sizeDelta = new Vector2(920f, 84f);
        return t;
    }

    private static void CreateStatColumn(RectTransform parent, float x, string label, out TextMeshProUGUI valueText, string value, TMP_FontAsset labelFont, TMP_FontAsset valueFont)
    {
        GameObject col = CreateUiObject(label + "_Column", parent);
        RectTransform rect = col.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200f, 124f);
        rect.anchoredPosition = new Vector2(x, 0f);

        TextMeshProUGUI labelText = CreateText(label + "_Label", rect, label, 18f,
            new Color(0.6f, 0.62f, 0.6f, 0.9f), FontStyles.UpperCase, labelFont);
        labelText.rectTransform.anchoredPosition = new Vector2(0f, 20f);
        labelText.characterSpacing = 1.5f;
        labelText.rectTransform.sizeDelta = new Vector2(190f, 40f);

        valueText = CreateText(label + "_Value", rect, value, 50f,
            new Color(0.88f, 0.88f, 0.84f, 1f), FontStyles.Bold, valueFont);
        valueText.rectTransform.anchoredPosition = new Vector2(0f, -30f);
        valueText.rectTransform.sizeDelta = new Vector2(190f, 58f);
    }

    private static Button CreateButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size,
        Color normal, Color highlighted, bool outlined, float labelSize, TMP_FontAsset font)
    {
        GameObject go = CreateUiObject(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = normal;

        if (outlined)
        {
            Outline o = go.AddComponent<Outline>();
            o.effectColor = new Color(0.24f, 0.24f, 0.24f, 0.85f);
            o.effectDistance = new Vector2(1f, -1f);
        }

        Button b = go.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = normal;
        cb.highlightedColor = highlighted;
        cb.pressedColor = highlighted * 0.9f;
        cb.selectedColor = highlighted;
        cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.35f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        TextMeshProUGUI txt = CreateText(name + "_Label", rect, label, labelSize,
            new Color(0.95f, 0.92f, 0.86f, 1f), FontStyles.UpperCase | FontStyles.Bold, font);
        txt.characterSpacing = 2f;
        Stretch(txt.rectTransform);
        return b;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void DisplayStats()
    {
        if (survivalTimeText != null)
        {
            int minutes = Mathf.FloorToInt(GameOverStats.SurvivalTime / 60f);
            int seconds = Mathf.FloorToInt(GameOverStats.SurvivalTime % 60f);
            survivalTimeText.text = $"{minutes:00}:{seconds:00}";
        }

        if (floorReachedText != null)
            floorReachedText.text = GameOverStats.FloorReached.ToString();

        if (spottedCountText != null)
            spottedCountText.text = $"{GameOverStats.SpottedCount}x";
    }

    private IEnumerator PlayIntro()
    {
        if (cardGroup != null) cardGroup.alpha = 0f;
        if (statsGroup != null) statsGroup.alpha = 0f;
        if (buttonGroup != null) buttonGroup.alpha = 0f;

        if (cardGroup != null) yield return FadeGroup(cardGroup, 1f, introDuration);
        if (statsGroup != null) yield return FadeGroup(statsGroup, 1f, introDuration * 0.9f);
        if (buttonGroup != null) yield return FadeGroup(buttonGroup, 1f, introDuration * 0.9f);

        if (restartButton != null) restartButton.interactable = true;
        if (mainMenuButton != null) mainMenuButton.interactable = true;
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = target;
    }

    private IEnumerator PulseVignette()
    {
        Color baseColor = vignetteOverlay.color;
        while (true)
        {
            float pulse = 0.92f + Mathf.Sin(Time.unscaledTime * vignettePulseSpeed) * 0.05f;
            vignetteOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * pulse);
            yield return null;
        }
    }

    private IEnumerator FlickerTube()
    {
        Color baseColor = fluorescentTube.color;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(0.08f, 2.2f));
            fluorescentTube.color = new Color(baseColor.r, baseColor.g, baseColor.b, Random.Range(0.42f, 0.95f));
            yield return new WaitForSeconds(Random.Range(0.02f, 0.07f));
            fluorescentTube.color = baseColor;
        }
    }

    private IEnumerator GlitchTitle()
    {
        Vector3 origin = titleRect.localPosition;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(glitchMinDelay, glitchMaxDelay));
            titleRect.localPosition = origin + new Vector3(Random.Range(-2f, 2f), Random.Range(-1f, 1f), 0f);
            yield return new WaitForSeconds(0.03f);
            titleRect.localPosition = origin;
        }
    }

    private IEnumerator ScanlineSweep()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(1.3f, 2.4f));

            float startY = 120f;
            float endY = -240f;
            float duration = Random.Range(0.45f, 0.75f);
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                scanlineRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, k));
                float alpha = Mathf.Lerp(0.015f, 0.06f, 1f - Mathf.Abs((k * 2f) - 1f));
                scanlineImage.color = new Color(0.82f, 1f, 0.86f, alpha);
                yield return null;
            }

            scanlineImage.color = new Color(0.82f, 1f, 0.86f, 0.015f);
        }
    }

    private void TryAutoAssignRecommendedFonts()
    {
        if (displayFont != null && bodyFont != null && monoFont != null)
            return;

#if UNITY_EDITOR
        if (displayFont == null)
            displayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BebasNeue-Regular SDF.asset");
        if (bodyFont == null)
            bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BebasNeue-Regular SDF.asset");
        if (monoFont == null)
            monoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/IBMPlexMono-Regular SDF.asset");
#endif
    }

    public void RestartGame()
    {
        PlayerDeathTracker.Reset();
        Time.timeScale = 1f;

        string firstLevel = "Scene Lantai 1";
        if (!string.IsNullOrWhiteSpace(firstLevel) && Application.CanStreamedLevelBeLoaded(firstLevel))
        {
            SceneManager.LoadScene(firstLevel);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError($"GameOverController: Scene gameplay '{gameplaySceneName}' tidak ditemukan di Build Profiles.");
            return;
        }
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError($"GameOverController: Scene main menu '{mainMenuSceneName}' tidak ditemukan di Build Profiles.");
            return;
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }
}