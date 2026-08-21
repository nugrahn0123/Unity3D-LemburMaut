using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuButtons : MonoBehaviour
{
    private const string PreferredSceneName = "Scene Lantai 6";
    private const string LegacySceneAlias = "Lantai 6";
    private const string PreferredScenePath = "Assets/Scenes/Lantai 6/Scene Lantai 6.unity";
    private const string MainMenuSceneName = "MainMenu";

    [Header("Scene tujuan saat tombol MULAI ditekan")]
    [SerializeField] private string gameplaySceneName = PreferredSceneName;

    [Header("Cutscene sebelum gameplay")]
    [Tooltip("Nama scene cutscene yang diputar sebelum masuk gameplay. Kosongkan jika tidak ada.")]
    [SerializeField] private string cutsceneSceneName = "";

    [Header("Shortcut keyboard")]
    [SerializeField] private bool allowEnterToStart = true;
    [SerializeField] private bool allowEscapeToExit = true;
    [SerializeField] private bool keyboardShortcutOnlyInMainMenu = true;

    private bool isLoadingScene;

    private void Update()
    {
        if (keyboardShortcutOnlyInMainMenu && !IsMainMenuSceneActive())
        {
            return;
        }

        if (allowEnterToStart && IsStartPressed())
        {
            Mulai();
        }

        if (allowEscapeToExit && IsExitPressed())
        {
            Keluar();
        }
    }

    private static bool IsMainMenuSceneActive()
    {
        string activeScene = SceneManager.GetActiveScene().name;
        return string.Equals(activeScene, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    public void SetGameplaySceneName(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            gameplaySceneName = sceneName.Trim();
        }
    }

    public void Mulai()
    {
        if (isLoadingScene)
        {
            return;
        }

        PlayerDeathTracker.Reset();

        string targetScene = ResolveTargetScene();
        if (string.IsNullOrEmpty(targetScene) && !TryLoadSceneInEditorByPath())
        {
            Debug.LogError("MainMenuButtons: Gagal memuat scene gameplay. Tambahkan 'Scene Lantai 6' ke Build Settings atau pastikan path scene benar.");
            return;
        }

        isLoadingScene = true;
        Time.timeScale = 1f;

        // Jika ada scene cutscene, load itu dulu
        if (!string.IsNullOrEmpty(cutsceneSceneName) && Application.CanStreamedLevelBeLoaded(cutsceneSceneName))
        {
            SceneManager.LoadScene(cutsceneSceneName);
            return;
        }

        if (!string.IsNullOrEmpty(targetScene))
        {
            SceneManager.LoadScene(targetScene);
        }
    }

    public void Keluar()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private string ResolveTargetScene()
    {
        if (Application.CanStreamedLevelBeLoaded(PreferredSceneName))
        {
            return PreferredSceneName;
        }

        if (Application.CanStreamedLevelBeLoaded(LegacySceneAlias))
        {
            return LegacySceneAlias;
        }

        if (!string.IsNullOrWhiteSpace(gameplaySceneName) && Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            return gameplaySceneName;
        }

        // Fallback: pilih scene build index pertama yang bukan scene aktif.
        Scene activeScene = SceneManager.GetActiveScene();
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(name, activeScene.name, System.StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    private bool TryLoadSceneInEditorByPath()
    {
#if UNITY_EDITOR
        if (!Application.isEditor)
        {
            return false;
        }

        if (!System.IO.File.Exists(PreferredScenePath))
        {
            return false;
        }

        isLoadingScene = true;
        Time.timeScale = 1f;
        EditorSceneManager.LoadSceneInPlayMode(PreferredScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        return true;
#else
        return false;
#endif
    }

    private static bool IsStartPressed()
    {
#if ENABLE_INPUT_SYSTEM
    Keyboard keyboard = Keyboard.current;
    if (keyboard == null)
    {
        return false;
    }

    return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
    return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
    return false;
#endif
    }

    private static bool IsExitPressed()
    {
#if ENABLE_INPUT_SYSTEM
    Keyboard keyboard = Keyboard.current;
    return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
    return Input.GetKeyDown(KeyCode.Escape);
#else
    return false;
#endif
    }
}
