using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// PENTING: file ini harus berada di dalam folder bernama "Editor"
/// (mis. Assets/Editor/LemburMautMenuBuilder.cs) agar Unity mengenalinya
/// sebagai editor-only script.
///
/// Cara pakai:
/// 1. Pastikan TMP Essentials sudah diimport (Window > TextMeshPro > Import TMP Essential Resources).
/// 2. (Opsional) Import font Creepster / Bebas Neue / IBM Plex Mono sebagai TMP Font Asset
///    lewat Window > TextMeshPro > Font Asset Creator, beri nama mengandung
///    "Creepster", "BebasNeue", dan "IBMPlexMono" agar otomatis terdeteksi oleh tool ini.
/// 3. Jalankan menu: Tools > Lembur Maut > Build Main Menu Canvas.
/// 4. Sebuah Canvas baru bernama "LemburMautMainMenu" akan muncul di scene,
///    lengkap dengan background, lampu flicker, judul, 2 tombol, dan HUD jam/status.
///
/// CATATAN DESAIN: seluruh isi Content memakai posisi tetap (anchoredPosition manual)
/// alih-alih VerticalLayoutGroup/HorizontalLayoutGroup, supaya hasilnya selalu
/// konsisten dan tidak bergantung pada kombinasi childControlWidth/Height yang rawan salah.
/// </summary>
public static class LemburMautMenuBuilder
{
    // ---- Palet warna ----
    private static readonly Color ColBgVoid   = HexToColor("0A0A0B");
    private static readonly Color ColBgTop    = HexToColor("121417");
    private static readonly Color ColBgBottom = HexToColor("08090A");
    private static readonly Color ColFluor    = HexToColor("CDD968");
    private static readonly Color ColFluorDim = HexToColor("8B9346");
    private static readonly Color ColBlood    = HexToColor("B3241F");
    private static readonly Color ColPaper    = HexToColor("D9D4C4");
    private static readonly Color ColPaperDim = HexToColor("7D7A6D");
    private static readonly Color ColLine     = HexToColor("2A2B26");

    [MenuItem("Tools/Lembur Maut/Build Main Menu Canvas")]
    public static void BuildMainMenu()
    {
        ReplaceOldGeneratedObject("LemburMautMainMenu");
        ReplaceOldGeneratedObject("LemburMautMenuManager");

        // ---------- Canvas + Scaler ----------
        GameObject canvasGO = new GameObject("LemburMautMainMenu",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); // 16:9
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();

        // ---------- Background ----------
        RectTransform bg = CreateFullRect("Background", canvasGO.transform);
        Image bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = ColBgVoid;

        // ---------- Background depth layers ----------
        RectTransform grad = CreateFullRect("BgGradient", canvasGO.transform);
        Image gradImg = grad.gameObject.AddComponent<Image>();
        Sprite gradSprite = CreateAndSaveTexture("bg_gradient", 16, 256, GradientPixel);
        if (gradSprite != null)
        {
            gradImg.sprite = gradSprite;
            gradImg.type = Image.Type.Simple;
            gradImg.color = Color.white;
        }
        else
        {
            gradImg.enabled = false;
            Debug.LogWarning("Background gradient gagal dibuat. Layer gradient dinonaktifkan untuk mencegah layar putih.");
        }
        gradImg.raycastTarget = false;

        RectTransform horizonBand = CreateUIObject("HorizonBand", canvasGO.transform);
        horizonBand.anchorMin = new Vector2(0f, 0.44f);
        horizonBand.anchorMax = new Vector2(1f, 0.62f);
        horizonBand.offsetMin = Vector2.zero;
        horizonBand.offsetMax = Vector2.zero;
        Image horizonImg = horizonBand.gameObject.AddComponent<Image>();
        horizonImg.color = new Color(0f, 0f, 0f, 0.28f);
        horizonImg.raycastTarget = false;

        RectTransform bloodFog = CreateUIObject("BloodFog", canvasGO.transform);
        bloodFog.anchorMin = new Vector2(0f, 0.34f);
        bloodFog.anchorMax = new Vector2(1f, 0.52f);
        bloodFog.offsetMin = Vector2.zero;
        bloodFog.offsetMax = Vector2.zero;
        Image bloodFogImg = bloodFog.gameObject.AddComponent<Image>();
        bloodFogImg.color = new Color(0.22f, 0.03f, 0.03f, 0.14f);
        bloodFogImg.raycastTarget = false;

        RectTransform leftWall = CreateUIObject("LeftWall", canvasGO.transform);
        leftWall.anchorMin = new Vector2(0f, 0.32f);
        leftWall.anchorMax = new Vector2(0f, 0.64f);
        leftWall.pivot = new Vector2(0f, 0.5f);
        leftWall.sizeDelta = new Vector2(220f, 0f);
        leftWall.anchoredPosition = new Vector2(40f, 0f);
        Image leftWallImg = leftWall.gameObject.AddComponent<Image>();
        leftWallImg.color = new Color(0.16f, 0.17f, 0.16f, 0.18f);
        leftWallImg.raycastTarget = false;

        RectTransform rightWall = CreateUIObject("RightWall", canvasGO.transform);
        rightWall.anchorMin = new Vector2(1f, 0.32f);
        rightWall.anchorMax = new Vector2(1f, 0.64f);
        rightWall.pivot = new Vector2(1f, 0.5f);
        rightWall.sizeDelta = new Vector2(220f, 0f);
        rightWall.anchoredPosition = new Vector2(-40f, 0f);
        Image rightWallImg = rightWall.gameObject.AddComponent<Image>();
        rightWallImg.color = new Color(0.16f, 0.17f, 0.16f, 0.18f);
        rightWallImg.raycastTarget = false;

        RectTransform lampGlow = CreateUIObject("LampGlow", canvasGO.transform);
        lampGlow.anchorMin = new Vector2(0.5f, 1f);
        lampGlow.anchorMax = new Vector2(0.5f, 1f);
        lampGlow.pivot = new Vector2(0.5f, 1f);
        lampGlow.sizeDelta = new Vector2(980f, 300f);
        lampGlow.anchoredPosition = new Vector2(0f, -4f);
        Image lampGlowImg = lampGlow.gameObject.AddComponent<Image>();
        Sprite glowSprite = CreateAndSaveTexture("lamp_glow", 256, 256, SoftGlowPixel);
        if (glowSprite != null)
        {
            lampGlowImg.sprite = glowSprite;
            lampGlowImg.type = Image.Type.Simple;
            lampGlowImg.color = new Color(0.78f, 0.86f, 0.35f, 0.22f);
        }
        else
        {
            lampGlowImg.enabled = false;
            Debug.LogWarning("Lamp glow sprite gagal dibuat. Layer glow dinonaktifkan untuk mencegah layar putih.");
        }
        lampGlowImg.raycastTarget = false;

        RectTransform grain = CreateFullRect("FilmGrain", canvasGO.transform);
        Image grainImg = grain.gameObject.AddComponent<Image>();
        Sprite grainSprite = CreateAndSaveTexture("film_grain", 64, 64, GrainPixel, filterMode: FilterMode.Point);
        if (grainSprite != null)
        {
            grainImg.sprite = grainSprite;
            grainImg.type = Image.Type.Tiled;
            grainImg.pixelsPerUnitMultiplier = 5f;
            grainImg.color = new Color(1f, 1f, 1f, 0.06f);
        }
        else
        {
            grainImg.enabled = false;
            Debug.LogWarning("Film grain sprite gagal dibuat. Layer grain dinonaktifkan untuk mencegah layar putih.");
        }
        grainImg.raycastTarget = false;

        // ---------- Lampu neon (flicker) ----------
        RectTransform light = CreateUIObject("LightBar", canvasGO.transform);
        light.anchorMin = new Vector2(0.5f, 1f);
        light.anchorMax = new Vector2(0.5f, 1f);
        light.pivot = new Vector2(0.5f, 1f);
        light.sizeDelta = new Vector2(420, 14);
        light.anchoredPosition = Vector2.zero;
        Image lightImg = light.gameObject.AddComponent<Image>();
        lightImg.color = ColFluor;
        light.gameObject.AddComponent<CanvasGroup>();
        light.gameObject.AddComponent<FlickerLight>();

        // ---------- Vignette (gradasi gelap di tepi) ----------
        RectTransform vignette = CreateFullRect("Vignette", canvasGO.transform);
        Image vImg = vignette.gameObject.AddComponent<Image>();
        Sprite vignetteSprite = CreateAndSaveTexture("vignette", 256, 256, VignettePixel);
        if (vignetteSprite != null)
        {
            vImg.sprite = vignetteSprite;
            vImg.type = Image.Type.Simple;
            vImg.color = Color.white;
        }
        else
        {
            vImg.enabled = false;
            Debug.LogWarning("Vignette sprite gagal dibuat. Layer vignette dinonaktifkan agar UI tidak menjadi putih.");
        }
        vImg.raycastTarget = false;

        // ---------- Scanline overlay ----------
        RectTransform scan = CreateFullRect("Scanlines", canvasGO.transform);
        Image scanImg = scan.gameObject.AddComponent<Image>();
        Sprite scanlineSprite = CreateAndSaveTexture("scanline", 4, 4, ScanlinePixel, filterMode: FilterMode.Point);
        if (scanlineSprite != null)
        {
            scanImg.sprite = scanlineSprite;
            scanImg.type = Image.Type.Tiled;
            scanImg.pixelsPerUnitMultiplier = 4f; // tile besar & wajar, bukan jutaan tile kecil
            scanImg.color = Color.white;
        }
        else
        {
            scanImg.enabled = false;
            Debug.LogWarning("Scanline sprite gagal dibuat. Layer scanline dinonaktifkan agar UI tidak menjadi putih.");
        }
        scanImg.raycastTarget = false;
        CanvasGroup scanCg = scan.gameObject.AddComponent<CanvasGroup>();
        scanCg.alpha = 0.3f;
        scan.gameObject.AddComponent<ScanlineDrift>();

        // ---------- Clock (pojok kanan atas) ----------
        RectTransform clockLabel = CreateUIObject("ClockLabel", canvasGO.transform);
        clockLabel.anchorMin = clockLabel.anchorMax = new Vector2(1f, 1f);
        clockLabel.pivot = new Vector2(1f, 1f);
        clockLabel.anchoredPosition = new Vector2(-40, -32);
        clockLabel.sizeDelta = new Vector2(220, 60);
        TextMeshProUGUI clockEyebrow = AddTMP(clockLabel.gameObject, "SHIFT LOG", 14, ColFluorDim, TextAlignmentOptions.TopRight);
        TryAssignFont(clockEyebrow, "IBMPlexMono");
        clockEyebrow.characterSpacing = 3;

        RectTransform clockTimeRT = CreateUIObject("ClockTime", clockLabel);
        clockTimeRT.anchorMin = new Vector2(0f, 0f);
        clockTimeRT.anchorMax = new Vector2(1f, 0.6f);
        clockTimeRT.offsetMin = Vector2.zero;
        clockTimeRT.offsetMax = Vector2.zero;
        TextMeshProUGUI clockTime = AddTMP(clockTimeRT.gameObject, "23:47", 26, ColPaperDim, TextAlignmentOptions.TopRight);
        TryAssignFont(clockTime, "IBMPlexMono");

        // ---------- Content (tengah, posisi manual/fixed, TANPA LayoutGroup) ----------
        RectTransform content = CreateUIObject("Content", canvasGO.transform);
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = new Vector2(0f, 212f); // titik atas dari tumpukan elemen

        float cursorY = 0f; // jarak turun dari titik atas Content

        // Stamp
        RectTransform stamp = AddStackedItem(content, "Stamp", 360, 32, ref cursorY, 22f);
        Image stampBorder = stamp.gameObject.AddComponent<Image>();
        stampBorder.color = new Color(0, 0, 0, 0);
        Outline stampOutline = stamp.gameObject.AddComponent<Outline>();
        stampOutline.effectColor = ColFluorDim;
        stampOutline.effectDistance = new Vector2(1, -1);

        // teks harus di child terpisah: 1 GameObject cuma boleh punya 1 komponen Graphic
        RectTransform stampTextRT = CreateFullRect("StampText", stamp);
        TextMeshProUGUI stampText = AddTMP(stampTextRT.gameObject, "KARYAWAN TIDAK BOLEH PULANG", 13, ColFluorDim, TextAlignmentOptions.Center);
        TryAssignFont(stampText, "IBMPlexMono");
        stampText.characterSpacing = 4;

        // Title
        RectTransform title = AddStackedItem(content, "Title", 680, 170, ref cursorY, 4f);
        TextMeshProUGUI titleText = AddTMP(title.gameObject, "LEMBUR MAUT", 92, ColPaper, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;
        titleText.characterSpacing = 4;
        Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(ColBlood.r, ColBlood.g, ColBlood.b, 0.6f);
        titleShadow.effectDistance = new Vector2(3, -3);
        TryAssignFont(titleText, "Creepster");
        MainMenuGlitch titleGlitch = title.gameObject.AddComponent<MainMenuGlitch>();
        titleGlitch.enabled = true;

        // Subtitle
        RectTransform subtitle = AddStackedItem(content, "Subtitle", 680, 30, ref cursorY, 34f);
        TextMeshProUGUI subtitleText = AddTMP(subtitle.gameObject, "ABSEN MASUK. TIDAK ADA ABSEN PULANG.", 14, ColBlood, TextAlignmentOptions.Center);
        subtitleText.characterSpacing = 6;
        TryAssignFont(subtitleText, "IBMPlexMono");
        subtitle.gameObject.AddComponent<MainMenuGlitch>();

        // ---- Menu: 2 tombol + garis pembatas, semua item stack langsung di Content ----
        const float menuWidth = 360f;

        AddDividerStacked(content, "DividerTop", menuWidth, ref cursorY);

        GameObject mainMenuManager = new GameObject("LemburMautMenuManager");
        MainMenuButtons buttonActions = mainMenuManager.AddComponent<MainMenuButtons>();
        buttonActions.SetGameplaySceneName(FindGameplaySceneName());
        mainMenuManager.transform.SetParent(canvasGO.transform, false);

        Button mulaiBtn = CreateMenuButtonStacked(content, "ButtonMulai", "MULAI", "[ENTER]", true, menuWidth, ref cursorY);
        AddDividerStacked(content, "DividerMid", menuWidth, ref cursorY);
        Button keluarBtn = CreateMenuButtonStacked(content, "ButtonKeluar", "KELUAR", "EXIT", false, menuWidth, ref cursorY);
        AddDividerStacked(content, "DividerBottom", menuWidth, ref cursorY);

        UnityEditor.Events.UnityEventTools.AddPersistentListener(mulaiBtn.onClick, buttonActions.Mulai);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(keluarBtn.onClick, buttonActions.Keluar);
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(mulaiBtn.gameObject);
        }

        cursorY += 24f; // jarak sebelum footer

        // Footer (dot + status), posisi manual di dalam rect footer
        RectTransform footer = AddStackedItem(content, "Footer", menuWidth, 24, ref cursorY, 0f);

        RectTransform dot = CreateUIObject("Dot", footer);
        dot.anchorMin = new Vector2(0f, 0.5f);
        dot.anchorMax = new Vector2(0f, 0.5f);
        dot.pivot = new Vector2(0f, 0.5f);
        dot.sizeDelta = new Vector2(8, 8);
        dot.anchoredPosition = new Vector2(0f, 0f);
        Image dotImg = dot.gameObject.AddComponent<Image>();
        dotImg.color = ColBlood;
        Sprite dotSprite = CreateAndSaveTexture("dot_circle", 32, 32, CirclePixel);
        if (dotSprite != null)
        {
            dotImg.sprite = dotSprite;
        }

        RectTransform statusRT = CreateUIObject("StatusText", footer);
        statusRT.anchorMin = new Vector2(0f, 0f);
        statusRT.anchorMax = new Vector2(1f, 1f);
        statusRT.pivot = new Vector2(0f, 0.5f);
        statusRT.offsetMin = new Vector2(20, 0);
        statusRT.offsetMax = new Vector2(0, 0);
        TextMeshProUGUI statusText = AddTMP(statusRT.gameObject, "1 KARYAWAN MASIH DI DALAM GEDUNG", 10, ColLine, TextAlignmentOptions.Left);
        TryAssignFont(statusText, "IBMPlexMono");
        statusText.characterSpacing = 2;

        // ---------- HUD script (jam + status log) ----------
        MainMenuHUD hud = canvasGO.AddComponent<MainMenuHUD>();
        hud.clockText = clockTime;
        hud.statusText = statusText;

        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Lembur Maut Main Menu");
        Selection.activeGameObject = canvasGO;

        Debug.Log("Lembur Maut main menu berhasil dibuat. Cek object 'LemburMautMainMenu' di Hierarchy.");
    }

    // =================== HELPERS ===================

    private static void ReplaceOldGeneratedObject(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }
    }

    private static string FindGameplaySceneName()
    {
        const string preferredScene = "Scene Lantai 6";
        if (Application.CanStreamedLevelBeLoaded(preferredScene))
        {
            return preferredScene;
        }

        const string fallbackAlias = "Lantai 6";
        if (Application.CanStreamedLevelBeLoaded(fallbackAlias))
        {
            return fallbackAlias;
        }

        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled)
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(buildScene.path);
            if (!string.Equals(name, activeSceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return preferredScene;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            ConfigureInputModule(es);
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            return;
        }

        ConfigureInputModule(existing.gameObject);
    }

    private static void ConfigureInputModule(GameObject eventSystemGO)
    {
#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule oldModule = eventSystemGO.GetComponent<StandaloneInputModule>();
        if (oldModule != null)
        {
            Object.DestroyImmediate(oldModule);
        }

        if (eventSystemGO.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystemGO.AddComponent<InputSystemUIInputModule>();
        }
#else
        if (eventSystemGO.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystemGO.AddComponent<StandaloneInputModule>();
        }
#endif
    }

    private static RectTransform CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static RectTransform CreateFullRect(string name, Transform parent)
    {
        RectTransform rt = CreateUIObject(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>
    /// Membuat RectTransform baru di bawah "cursorY" saat ini (dari titik atas parent),
    /// lalu menggeser cursorY ke bawah sejumlah height + gapAfter untuk item berikutnya.
    /// Menggantikan VerticalLayoutGroup dengan posisi manual yang deterministik.
    /// </summary>
    private static RectTransform AddStackedItem(RectTransform parent, string name, float width, float height, ref float cursorY, float gapAfter)
    {
        RectTransform rt = CreateUIObject(name, parent);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, -cursorY);
        cursorY += height + gapAfter;
        return rt;
    }

    private static TextMeshProUGUI AddTMP(GameObject go, string text, float size, Color color, TextAlignmentOptions align)
    {
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Cari TMP Font Asset di project berdasarkan nama parsial, lalu assign jika ketemu.</summary>
    private static void TryAssignFont(TextMeshProUGUI tmp, string nameContains)
    {
        string[] guids = AssetDatabase.FindAssets($"t:TMP_FontAsset {nameContains}");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null)
            {
                tmp.font = font;
                return;
            }
        }

        // Fallback for fonts created under Assets/Fonts.
        if (nameContains == "Creepster")
        {
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Creepster-Regular SDF.asset");
            if (fallback != null)
                tmp.font = fallback;
        }
        else if (nameContains == "BebasNeue")
        {
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/BebasNeue-Regular SDF.asset");
            if (fallback != null)
                tmp.font = fallback;
        }
        else if (nameContains == "IBMPlexMono")
        {
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/IBMPlexMono-Regular SDF.asset");
            if (fallback != null)
                tmp.font = fallback;
        }
    }

    private static void AddDividerStacked(RectTransform parent, string name, float width, ref float cursorY)
    {
        RectTransform d = AddStackedItem(parent, name, width, 1f, ref cursorY, 0f);
        Image img = d.gameObject.AddComponent<Image>();
        img.color = ColLine;
    }

    private static Button CreateMenuButtonStacked(RectTransform parent, string name, string label, string code, bool primary, float width, ref float cursorY)
    {
        RectTransform row = AddStackedItem(parent, name, width, 52f, ref cursorY, 0f);

        Image rowImg = row.gameObject.AddComponent<Image>();
        rowImg.color = new Color(0, 0, 0, 0.001f); // nyaris transparan, hanya supaya Button bisa menerima klik
        Button btn = row.gameObject.AddComponent<Button>();
        btn.targetGraphic = rowImg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = cb;

        // Label (rata kiri), stretch penuh minus ruang untuk Code di kanan
        RectTransform labelRT = CreateUIObject("Label", row);
        labelRT.anchorMin = new Vector2(0f, 0f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.pivot = new Vector2(0f, 0.5f);
        labelRT.offsetMin = new Vector2(10, 0);
        labelRT.offsetMax = new Vector2(-90, 0);
        Color labelColor = primary ? ColPaper : ColPaperDim;
        TextMeshProUGUI labelText = AddTMP(labelRT.gameObject, label, 19, labelColor, TextAlignmentOptions.Left);
        labelText.characterSpacing = 3;
        labelText.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
        TryAssignFont(labelText, "BebasNeue");

        // Code (rata kanan), lebar tetap 80 menempel ke tepi kanan
        RectTransform codeRT = CreateUIObject("Code", row);
        codeRT.anchorMin = new Vector2(1f, 0f);
        codeRT.anchorMax = new Vector2(1f, 1f);
        codeRT.pivot = new Vector2(1f, 0.5f);
        codeRT.sizeDelta = new Vector2(80, 0);
        codeRT.anchoredPosition = new Vector2(-6, 0);
        Color codeColor = primary ? ColFluorDim : ColLine;
        TextMeshProUGUI codeText = AddTMP(codeRT.gameObject, code, 11, codeColor, TextAlignmentOptions.Right);
        TryAssignFont(codeText, "IBMPlexMono");

        return btn;
    }

    private static Color HexToColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    // ---- Generator tekstur prosedural (disimpan sebagai asset PNG) ----

    private static Sprite CreateAndSaveTexture(string name, int width, int height,
        System.Func<int, int, int, int, Color> pixelFunc, FilterMode filterMode = FilterMode.Bilinear)
    {
        const string dir = "Assets/LemburMaut/Generated";
        if (!AssetDatabase.IsValidFolder("Assets/LemburMaut"))
            AssetDatabase.CreateFolder("Assets", "LemburMaut");
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/LemburMaut", "Generated");

        string path = $"{dir}/{name}.png";

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, pixelFunc(x, y, width, height));
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = filterMode;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Color VignettePixel(int x, int y, int w, int h)
    {
        float dx = (x - w / 2f) / (w / 2f);
        float dy = (y - h / 2f) / (h / 2f);
        float dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
        float alpha = Mathf.SmoothStep(0f, 0.9f, Mathf.InverseLerp(0.35f, 1f, dist));
        return new Color(0f, 0f, 0f, alpha);
    }

    private static Color GradientPixel(int x, int y, int w, int h)
    {
        float t = y / (float)(h - 1);
        Color c = Color.Lerp(ColBgBottom, ColBgTop, t);
        return new Color(c.r, c.g, c.b, 1f);
    }

    private static Color SoftGlowPixel(int x, int y, int w, int h)
    {
        float dx = (x - (w * 0.5f)) / (w * 0.5f);
        float dy = (y - (h * 0.5f)) / (h * 0.5f);
        float dist = Mathf.Sqrt((dx * dx) + (dy * dy));
        float alpha = Mathf.Clamp01(1f - dist);
        alpha = alpha * alpha;
        return new Color(1f, 1f, 1f, alpha);
    }

    private static Color GrainPixel(int x, int y, int w, int h)
    {
        int hash = (x * 92837111) ^ (y * 689287499);
        hash ^= (hash >> 13);
        float n = (hash & 1023) / 1023f;
        float lum = 0.32f + (n * 0.45f);
        return new Color(lum, lum, lum, 1f);
    }

    private static Color ScanlinePixel(int x, int y, int w, int h)
    {
        // baris genap gelap, baris ganjil transparan -> pola garis horizontal
        return (y % 2 == 0) ? new Color(0, 0, 0, 0.5f) : new Color(0, 0, 0, 0f);
    }

    private static Color CirclePixel(int x, int y, int w, int h)
    {
        float dx = (x - w / 2f) / (w / 2f);
        float dy = (y - h / 2f) / (h / 2f);
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        return dist <= 1f ? Color.white : new Color(1, 1, 1, 0);
    }
}