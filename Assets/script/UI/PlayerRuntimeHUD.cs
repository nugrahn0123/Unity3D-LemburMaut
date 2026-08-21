using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public class PlayerRuntimeHUD : MonoBehaviour
{
    public PlayerMovement playerMovement;
    public PlayerFlashlightStun playerFlashlight;
    public PlayerHealth playerHealth;

    private Image staminaFill;
    private RectTransform staminaFillRect;
    private Text staminaText;
    private Image flashlightFill;
    private RectTransform flashlightFillRect;
    private Text flashlightText;
    private Font runtimeFont;
    private Image[] lifeIndicators;

    void Awake()
    {
        runtimeFont = LoadRuntimeFont();
        EnsureHudExists();
        ResolveTargets();
    }

    Font LoadRuntimeFont()
    {
        Font loadedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (loadedFont != null)
            return loadedFont;

        // Fallback untuk versi Unity lama.
        loadedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return loadedFont;
    }

    void Update()
    {
        if (playerMovement == null || playerFlashlight == null || playerHealth == null)
            ResolveTargets();

        UpdateBars();
        UpdateLives();
    }

    void ResolveTargets()
    {
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (playerFlashlight == null)
            playerFlashlight = FindFirstObjectByType<PlayerFlashlightStun>();

        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    void EnsureHudExists()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        if (GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        Transform panel = transform.Find("ResourcePanel");
        if (panel == null)
            panel = BuildPanel(transform);

        Transform staminaBar = panel.Find("StaminaBar");
        if (staminaBar == null)
            staminaBar = BuildBar(panel, "StaminaBar", new Color(0.16f, 0.72f, 0.31f, 1f), "STAMINA");

        Transform flashlightBar = panel.Find("FlashlightBar");
        if (flashlightBar == null)
            flashlightBar = BuildBar(panel, "FlashlightBar", new Color(1f, 0.84f, 0.1f, 1f), "SENTER");

        Transform lifeRow = panel.Find("LifeRow");
        if (lifeRow == null)
            lifeRow = BuildLifeRow(panel);

        staminaFill = staminaBar.Find("Fill").GetComponent<Image>();
        staminaFillRect = staminaFill.GetComponent<RectTransform>();
        staminaText = staminaBar.Find("Label").GetComponent<Text>();

        flashlightFill = flashlightBar.Find("Fill").GetComponent<Image>();
        flashlightFillRect = flashlightFill.GetComponent<RectTransform>();
        flashlightText = flashlightBar.Find("Label").GetComponent<Text>();

        ConfigureHorizontalRightToLeftFill(staminaFill, new Color(0.16f, 0.72f, 0.31f, 1f));
        ConfigureHorizontalRightToLeftFill(flashlightFill, new Color(1f, 0.84f, 0.1f, 1f));

        lifeIndicators = new Image[3];
        for (int i = 0; i < lifeIndicators.Length; i++)
        {
            Transform item = lifeRow.Find("Life" + i);
            if (item != null)
                lifeIndicators[i] = item.GetComponent<Image>();
        }
    }

    void ConfigureHorizontalRightToLeftFill(Image fillImage, Color fillColor)
    {
        if (fillImage == null)
            return;

        fillImage.color = fillColor;
        fillImage.type = Image.Type.Simple;

        RectTransform fillRect = fillImage.GetComponent<RectTransform>();
        if (fillRect != null)
        {
            // Pivot kanan agar scale X mengecil dari kanan ke kiri.
            fillRect.pivot = new Vector2(1f, 0.5f);
            fillRect.localScale = Vector3.one;
        }
    }

    Transform BuildPanel(Transform parent)
    {
        GameObject panelObj = new GameObject("ResourcePanel");
        panelObj.transform.SetParent(parent, false);

        RectTransform rect = panelObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(26f, -26f);
        rect.sizeDelta = new Vector2(520f, 244f);

        VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        Image bg = panelObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.48f);

        return panelObj.transform;
    }

    Transform BuildBar(Transform parent, string name, Color fillColor, string labelPrefix)
    {
        GameObject barObj = new GameObject(name);
        barObj.transform.SetParent(parent, false);

        RectTransform barRect = barObj.AddComponent<RectTransform>();
        barRect.sizeDelta = new Vector2(0f, 66f);

        Image bg = barObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barObj.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        Image fill = fillObj.AddComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Simple;

        // Pivot kanan supaya saat diskalakan di sumbu X, bar berkurang dari kanan ke kiri.
        fillRect.pivot = new Vector2(1f, 0.5f);

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(barObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObj.AddComponent<Text>();
        label.font = runtimeFont;
        label.fontSize = 27;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = labelPrefix + " 100%";

        Outline outline = labelObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        return barObj.transform;
    }

    Transform BuildLifeRow(Transform parent)
    {
        GameObject rowObj = new GameObject("LifeRow");
        rowObj.transform.SetParent(parent, false);

        RectTransform rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 46f);

        HorizontalLayoutGroup layout = rowObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(4, 4, 0, 0);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;

        for (int i = 0; i < 3; i++)
        {
            GameObject lifeObj = new GameObject("Life" + i);
            lifeObj.transform.SetParent(rowObj.transform, false);

            RectTransform lifeRect = lifeObj.AddComponent<RectTransform>();
            lifeRect.sizeDelta = new Vector2(42f, 42f);

            Image lifeImg = lifeObj.AddComponent<Image>();
            lifeImg.color = new Color(0.95f, 0.2f, 0.2f, 1f);
        }

        return rowObj.transform;
    }

    void UpdateBars()
    {
        if (staminaFill != null)
        {
            float stamina01 = playerMovement != null ? playerMovement.StaminaNormalized : 1f;
            if (staminaFillRect != null)
                staminaFillRect.localScale = new Vector3(Mathf.Clamp01(stamina01), 1f, 1f);

            if (staminaText != null)
                staminaText.text = "STAMINA " + Mathf.RoundToInt(stamina01 * 100f) + "%";
        }

        if (flashlightFill != null)
        {
            float flashlight01 = playerFlashlight != null ? playerFlashlight.FlashlightEnergyNormalized : 1f;
            if (flashlightFillRect != null)
                flashlightFillRect.localScale = new Vector3(Mathf.Clamp01(flashlight01), 1f, 1f);

            if (flashlightText != null)
                flashlightText.text = "SENTER " + Mathf.RoundToInt(flashlight01 * 100f) + "%";
        }
    }

    void UpdateLives()
    {
        if (lifeIndicators == null || lifeIndicators.Length == 0)
            return;

        int lives = playerHealth != null ? playerHealth.CurrentLives : 3;
        for (int i = 0; i < lifeIndicators.Length; i++)
        {
            Image img = lifeIndicators[i];
            if (img == null)
                continue;

            bool alive = i < lives;
            img.color = alive
                ? new Color(0.95f, 0.2f, 0.2f, 1f)
                : new Color(0.28f, 0.28f, 0.28f, 0.9f);
        }
    }
}
