using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Putar video cutscene lalu otomatis pindah ke scene gameplay.
/// Pasang script ini di GameObject di scene Cut Scene 1.
/// Tekan SPACE atau klik untuk skip.
/// </summary>
public class CutsceneVideoPlayer : MonoBehaviour
{
    [Header("Video")]
    [Tooltip("Drag file .mp4 ke sini")]
    public VideoClip cutsceneClip;

    [Header("Scene Tujuan")]
    [Tooltip("Nama scene yang diload setelah video selesai")]
    public string nextSceneName = "Scene Lantai 6";

    [Header("UI")]
    [Tooltip("RawImage tempat video ditampilkan (opsional, jika tidak ada pakai full screen)")]
    public RawImage displayImage;

    [Tooltip("Teks 'Tekan SPACE untuk skip' (opsional)")]
    public GameObject skipHintUI;

    [Header("Pengaturan")]
    [Tooltip("Izinkan skip dengan tekan SPACE atau klik")]
    public bool allowSkip = true;

    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;
    private bool isFinished = false;

    void Start()
    {
        SetupVideoPlayer();
    }

    void SetupVideoPlayer()
    {
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;

        // Buat render texture untuk video
        renderTexture = new RenderTexture(1920, 1080, 0);
        videoPlayer.targetTexture = renderTexture;

        if (displayImage != null)
            displayImage.texture = renderTexture;

        if (cutsceneClip != null)
        {
            videoPlayer.clip = cutsceneClip;
        }
        else
        {
            Debug.LogWarning("CutsceneVideoPlayer: cutsceneClip belum diisi! Langsung pindah ke scene gameplay.");
            LoadNextScene();
            return;
        }

        // Callback saat video selesai
        videoPlayer.loopPointReached += OnVideoFinished;

        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += (vp) => vp.Play();

        if (skipHintUI != null)
            skipHintUI.SetActive(allowSkip);
    }

    void Update()
    {
        if (isFinished) return;

        if (allowSkip)
        {
            bool skipPressed = false;

#if ENABLE_INPUT_SYSTEM
            skipPressed = UnityEngine.InputSystem.Keyboard.current != null &&
                          UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame;
            skipPressed |= UnityEngine.InputSystem.Mouse.current != null &&
                           UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#else
            skipPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
            if (skipPressed)
                LoadNextScene();
        }
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        LoadNextScene();
    }

    void LoadNextScene()
    {
        if (isFinished) return;
        isFinished = true;

        if (videoPlayer != null)
            videoPlayer.Stop();

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("CutsceneVideoPlayer: nextSceneName kosong!");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
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
