using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// Sekuens ending: video pilihan (tanpa skip) -> foto + 2 tombol pilihan ->
// video bad/happy ending -> kembali ke menu utama.
// Pasang di satu GameObject kosong di Scene Ending; asset otomatis terisi.
public class EndingSequence : MonoBehaviour
{
    [Header("Video")]
    [Tooltip("Video pembuka pilihan ending (tidak bisa di-skip).")]
    public VideoClip choiceIntroClip;
    public VideoClip badEndingClip;
    [Tooltip("Boleh kosong dulu; jika kosong langsung ke menu utama.")]
    public VideoClip happyEndingClip;

    [Header("Pilihan")]
    [Tooltip("Foto yang ditampilkan saat memilih.")]
    public Texture2D choicePhoto;
    public string ignoreButtonLabel = "Pura Pura Tidak Tahu";
    public string suspectButtonLabel = "Curigai Satpam";

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private RawImage videoImage;
    private GameObject choicePanel;
    private System.Action onVideoFinished;
    private PlayerRuntimeHUD cachedHUD;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (choiceIntroClip == null)
            choiceIntroClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Scenes/Scene Ending/Scene Pilihan Ending.mp4");
        if (badEndingClip == null)
            badEndingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Scenes/Scene Ending/BAD ENDING.mp4");
        if (choicePhoto == null)
            choicePhoto = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Scenes/Scene Ending/Screenshot 2026-08-14 222802.png");
    }
#endif

    void Awake()
    {
        BacksoundPlayer.PausedForIntro = true;
        BacksoundPlayer.EnsureInstance()?.StopBacksound();
    }

    void Start()
    {
        BacksoundPlayer.EnsureInstance()?.StopBacksound();
        cachedHUD = FindFirstObjectByType<PlayerRuntimeHUD>();
        cachedHUD?.SetVisible(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        BuildUI();
        EnsureEventSystem();

        // Tahap 1: video pilihan, tanpa skip.
        PlayVideo(choiceIntroClip, ShowChoices);
    }

    // ---------------------------------------------------
    // Video
    // ---------------------------------------------------

    void PlayVideo(VideoClip clip, System.Action onFinished)
    {
        onVideoFinished = onFinished;

        if (clip == null)
        {
            onFinished?.Invoke();
            return;
        }

        if (videoPlayer == null)
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.loopPointReached += _ => FinishCurrentVideo();
            videoPlayer.prepareCompleted += vp => vp.Play();
        }

        choicePanel.SetActive(false);
        videoImage.gameObject.SetActive(true);
        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.Prepare();
    }

    void FinishCurrentVideo()
    {
        System.Action callback = onVideoFinished;
        onVideoFinished = null;
        videoImage.gameObject.SetActive(false);
        callback?.Invoke();
    }

    // ---------------------------------------------------
    // Pilihan
    // ---------------------------------------------------

    void ShowChoices()
    {
        choicePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnChooseIgnore()
    {
        // Happy ending; clip boleh menyusul — jika kosong langsung ke menu.
        PlayVideo(happyEndingClip, GoToMainMenu);
    }

    void OnChooseSuspect()
    {
        PlayVideo(badEndingClip, GoToMainMenu);
    }

    void GoToMainMenu()
    {
        Time.timeScale = 1f;
        BacksoundPlayer.PausedForIntro = false;
        cachedHUD?.SetVisible(true);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ---------------------------------------------------
    // UI builder
    // ---------------------------------------------------

    void BuildUI()
    {
        renderTexture = new RenderTexture(1920, 1080, 0);

        GameObject canvasObject = new GameObject("EndingCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        // Latar hitam permanen selama sekuens ending.
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(canvasObject.transform, false);
        bg.AddComponent<Image>().color = Color.black;
        Stretch(bg.GetComponent<RectTransform>());

        // Layar video.
        GameObject videoObject = new GameObject("VideoImage");
        videoObject.transform.SetParent(canvasObject.transform, false);
        videoImage = videoObject.AddComponent<RawImage>();
        videoImage.texture = renderTexture;
        Stretch(videoObject.GetComponent<RectTransform>());

        // Panel pilihan: foto + 2 tombol.
        choicePanel = new GameObject("ChoicePanel");
        choicePanel.transform.SetParent(canvasObject.transform, false);
        Stretch(choicePanel.AddComponent<RectTransform>());

        GameObject photo = new GameObject("Photo");
        photo.transform.SetParent(choicePanel.transform, false);
        RawImage photoImage = photo.AddComponent<RawImage>();
        photoImage.texture = choicePhoto;
        RectTransform photoRect = photo.GetComponent<RectTransform>();
        photoRect.anchorMin = new Vector2(0.5f, 0.5f);
        photoRect.anchorMax = new Vector2(0.5f, 0.5f);
        photoRect.pivot = new Vector2(0.5f, 0.5f);
        photoRect.sizeDelta = new Vector2(1200f, 600f);
        photoRect.anchoredPosition = new Vector2(0f, 120f);

        CreateChoiceButton(choicePanel.transform, ignoreButtonLabel, new Vector2(-320f, -280f), OnChooseIgnore);
        CreateChoiceButton(choicePanel.transform, suspectButtonLabel, new Vector2(320f, -280f), OnChooseSuspect);

        choicePanel.SetActive(false);
        videoImage.gameObject.SetActive(false);
    }

    void CreateChoiceButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject("Button_" + label);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.1f, 0.1f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.45f, 0.15f, 0.1f, 1f);
        colors.pressedColor = new Color(0.6f, 0.2f, 0.12f, 1f);
        button.colors = colors;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(480f, 90f);
        rect.anchoredPosition = position;

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        Stretch(textObject.GetComponent<RectTransform>());
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<StandaloneInputModule>();
#endif
    }

    void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
