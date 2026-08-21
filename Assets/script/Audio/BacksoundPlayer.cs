using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(AudioSource))]
public class BacksoundPlayer : MonoBehaviour
{
    private const string DefaultBacksoundPath = "Assets/Scenes/Lantai 6/Sound/Backsound.mp3";
    private const string PreferredSceneName = "Scene Lantai 6";
    private const string LegacySceneAlias = "Lantai 6";

    [Header("Backsound")]
    [SerializeField] private AudioClip backsoundClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.2f;
    [SerializeField] private bool loop = true;

    private static BacksoundPlayer instance;
    private AudioSource audioSource;

    // Set true saat video intro sedang diputar agar backsound tidak mulai duluan
    public static bool PausedForIntro = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static BacksoundPlayer EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        BacksoundPlayer existing = Object.FindFirstObjectByType<BacksoundPlayer>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject go = new GameObject("BacksoundPlayer");
        instance = go.AddComponent<BacksoundPlayer>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        TryAutoAssignClip();
        ConfigureSource();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsGameplayScene(scene.name))
        {
            EnsureAudioListener();
            PlayBacksound();
        }
        else
        {
            StopBacksound();
        }
    }

    public void SetClip(AudioClip clip)
    {
        backsoundClip = clip;
        if (audioSource != null)
        {
            audioSource.clip = backsoundClip;
        }
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }

    public void PlayBacksound()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            TryAutoAssignClip();
            ConfigureSource();
        }

        if (backsoundClip == null)
        {
            TryAutoAssignClip();
        }

        if (backsoundClip == null)
        {
            Debug.LogWarning("BacksoundPlayer: Backsound clip belum diassign. Letakkan clip di Resources/Backsound atau gunakan path default editor.");
            return;
        }

        audioSource.clip = backsoundClip;
        audioSource.volume = volume;
        audioSource.loop = loop;

        if (!audioSource.isPlaying && !PausedForIntro)
        {
            audioSource.Play();
            Debug.Log($"BacksoundPlayer: Memutar backsound '{backsoundClip.name}' di scene '{SceneManager.GetActiveScene().name}' dengan volume {volume:0.00}.");
        }
    }

    public void StopBacksound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void ConfigureSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;
        audioSource.clip = backsoundClip;
    }

    private static void EnsureAudioListener()
    {
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (listeners.Length > 0)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>();
            Debug.LogWarning("BacksoundPlayer: Tidak ada AudioListener. Menambahkan AudioListener ke Main Camera.");
            return;
        }

        GameObject listenerGO = new GameObject("AutoAudioListener");
        Object.DontDestroyOnLoad(listenerGO);
        listenerGO.AddComponent<AudioListener>();
        Debug.LogWarning("BacksoundPlayer: Tidak ada Main Camera dan AudioListener. Membuat AutoAudioListener sementara.");
    }

    private static bool IsGameplayScene(string sceneName)
    {
        return string.Equals(sceneName, PreferredSceneName, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(sceneName, LegacySceneAlias, System.StringComparison.OrdinalIgnoreCase);
    }

    private void TryAutoAssignClip()
    {
        if (backsoundClip != null)
        {
            return;
        }

        backsoundClip = Resources.Load<AudioClip>("Backsound");
#if UNITY_EDITOR
        if (backsoundClip == null)
        {
            backsoundClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultBacksoundPath);
        }
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignClip();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            ConfigureSource();
        }
    }

    [ContextMenu("Auto Assign Default Backsound")]
    private void AutoAssignDefaultBacksound()
    {
        backsoundClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultBacksoundPath);
        EditorUtility.SetDirty(this);
    }
#endif
}
