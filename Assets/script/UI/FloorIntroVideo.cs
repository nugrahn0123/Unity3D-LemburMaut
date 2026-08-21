using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// Cutscene pembuka lantai: otomatis memutar video saat scene dimuat,
// menyembunyikan HUD dan membekukan dunia sampai video selesai / di-skip.
// Pasang di satu GameObject kosong per scene lantai; clip otomatis terisi
// sesuai nama scene (Lantai N -> "Cut Scene Lantai N.mp4").
public class FloorIntroVideo : MonoBehaviour
{
    [Header("Video")]
    [Tooltip("Kosongkan untuk otomatis memakai 'Cut Scene Lantai N' sesuai nama scene.")]
    public VideoClip videoClip;

    [Header("Pengaturan")]
    public bool allowSkip = true;
    public string skipHintText = "Tekan [SPACE] untuk skip";

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private Canvas videoCanvas;
    private bool finished = false;
    private PlayerRuntimeHUD cachedHUD;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (videoClip != null || Application.isPlaying)
            return;

        var match = System.Text.RegularExpressions.Regex.Match(gameObject.scene.name ?? "", @"Lantai\s*(\d+)");
        if (!match.Success)
            return;

        string path = $"Assets/Scenes/Cut Scenes 1/Cut Scene Lantai {match.Groups[1].Value}.mp4";
        videoClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(path);
    }
#endif

    void Awake()
    {
        // Harus di Awake agar jalan sebelum sceneLoaded event BacksoundPlayer.
        BacksoundPlayer.PausedForIntro = true;
        BacksoundPlayer.EnsureInstance()?.StopBacksound();
    }

    void Start()
    {
        BacksoundPlayer.EnsureInstance()?.StopBacksound();

        cachedHUD = FindFirstObjectByType<PlayerRuntimeHUD>();
        cachedHUD?.SetVisible(false);

        Time.timeScale = 0f;

        if (videoClip == null)
        {
            Debug.LogWarning($"FloorIntroVideo: videoClip kosong di scene {SceneManager.GetActiveScene().name}.", this);
            EndVideo();
            return;
        }

        BuildVideoUI();

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.clip = videoClip;
        // Unscaled time agar video tetap berjalan saat timeScale = 0.
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.loopPointReached += _ => EndVideo();
        videoPlayer.prepareCompleted += vp => vp.Play();
        videoPlayer.Prepare();
    }

    void BuildVideoUI()
    {
        renderTexture = new RenderTexture(1920, 1080, 0);

        GameObject canvasObject = new GameObject("FloorIntroCanvas");
        canvasObject.transform.SetParent(transform, false);
        videoCanvas = canvasObject.AddComponent<Canvas>();
        videoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        videoCanvas.sortingOrder = 5000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Latar hitam penuh di belakang video.
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(canvasObject.transform, false);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = Color.black;
        StretchToParent(bg.GetComponent<RectTransform>());

        GameObject videoObject = new GameObject("VideoImage");
        videoObject.transform.SetParent(canvasObject.transform, false);
        RawImage rawImage = videoObject.AddComponent<RawImage>();
        rawImage.texture = renderTexture;
        StretchToParent(videoObject.GetComponent<RectTransform>());

        if (allowSkip)
        {
            GameObject hint = new GameObject("SkipHint");
            hint.transform.SetParent(canvasObject.transform, false);
            Text hintText = hint.AddComponent<Text>();
            hintText.text = skipHintText;
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 26;
            hintText.alignment = TextAnchor.LowerRight;
            hintText.color = new Color(1f, 1f, 1f, 0.75f);
            RectTransform hintRect = hint.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.sizeDelta = new Vector2(500f, 60f);
            hintRect.anchoredPosition = new Vector2(-40f, 30f);
        }
    }

    static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void Update()
    {
        if (finished || !allowSkip)
            return;

        bool skip = false;
#if ENABLE_INPUT_SYSTEM
        skip = UnityEngine.InputSystem.Keyboard.current?.spaceKey.wasPressedThisFrame == true
            || UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame == true
            || UnityEngine.InputSystem.Gamepad.current?.startButton.wasPressedThisFrame == true
            || UnityEngine.InputSystem.Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
#else
        skip = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
        if (skip)
            EndVideo();
    }

    void EndVideo()
    {
        if (finished)
            return;
        finished = true;

        if (videoPlayer != null) videoPlayer.Stop();
        if (videoCanvas != null) videoCanvas.gameObject.SetActive(false);

        Time.timeScale = 1f;

        BacksoundPlayer.PausedForIntro = false;
        BacksoundPlayer.EnsureInstance()?.PlayBacksound();
        cachedHUD?.SetVisible(true);

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
