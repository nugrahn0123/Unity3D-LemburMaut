using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public class IntroVideoPlayer : MonoBehaviour
{
    [Header("Video")]
    public VideoClip videoClip;

    [Header("UI")]
    public Canvas videoCanvas;
    public RawImage displayImage;
    public GameObject skipHintUI;

    [Header("Player")]
    public GameObject player;

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private bool finished = false;
    private PlayerRuntimeHUD cachedHUD;

    void Awake()
    {
        // Harus di Awake agar jalan sebelum sceneLoaded event BacksoundPlayer
        BacksoundPlayer.PausedForIntro = true;
        BacksoundPlayer.EnsureInstance()?.StopBacksound();
    }

    void Start()
    {
        // Stop lagi di Start untuk jaga-jaga jika sceneLoaded jalan setelah Awake
        BacksoundPlayer.EnsureInstance()?.StopBacksound();
        // Simpan referensi sebelum disembunyikan agar bisa ditemukan kembali saat inactive
        cachedHUD = FindFirstObjectByType<PlayerRuntimeHUD>();
        cachedHUD?.SetVisible(false);

        // Bekukan dunia game selama video berlangsung
        Time.timeScale = 0f;

        if (videoClip == null)
        {
            EndVideo();
            return;
        }

        renderTexture = new RenderTexture(1920, 1080, 0);
        if (displayImage != null)
            displayImage.texture = renderTexture;

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.clip = videoClip;
        // Video pakai unscaled time agar tetap jalan saat timeScale = 0
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.loopPointReached += _ => EndVideo();
        videoPlayer.prepareCompleted += vp => vp.Play();
        videoPlayer.Prepare();

        if (videoCanvas != null) videoCanvas.gameObject.SetActive(true);
        if (skipHintUI != null) skipHintUI.SetActive(true);
    }

    void Update()
    {
        if (finished) return;

        // Gunakan unscaledDeltaTime agar input terbaca saat timeScale = 0
        bool skip = false;
#if ENABLE_INPUT_SYSTEM
        skip = UnityEngine.InputSystem.Keyboard.current?.spaceKey.wasPressedThisFrame == true
            || UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame == true;
#else
        skip = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
        if (skip) EndVideo();
    }

    void EndVideo()
    {
        if (finished) return;
        finished = true;

        if (videoPlayer != null) videoPlayer.Stop();
        if (videoCanvas != null) videoCanvas.gameObject.SetActive(false);

        // Pulihkan dunia game
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
