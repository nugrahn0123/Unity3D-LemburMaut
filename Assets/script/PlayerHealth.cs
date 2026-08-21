using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayerDeathTracker
{
    public static int MaxDeathsBeforeGameOver { get; set; } = 3;
    public static int TotalDeaths { get; private set; }

    public static void Reset()
    {
        TotalDeaths = 0;
    }

    public static bool RegisterDeath()
    {
        if (TotalDeaths >= MaxDeathsBeforeGameOver)
            return true;

        TotalDeaths++;
        return TotalDeaths >= MaxDeathsBeforeGameOver;
    }
}

public class PlayerHealth : MonoBehaviour
{
    [Header("Nyawa Player")]
    public int maxLives = 3;
    public float hitCooldown = 0.8f;

    [Header("Respawn")]
    public bool respawnOnDeath = true;
    public float respawnDelay = 1.2f;
    public bool useRespawnFade = true;
    public float respawnFadeDuration = 0.45f;
    public Color respawnFadeColor = new Color(0f, 0f, 0f, 1f);

    [Header("Game Over Scene")]
    public bool loadGameOverSceneOnDeath = true;
    public string gameOverSceneName = "GameOver";

    [Header("Stats untuk Game Over")]
    public bool trackSurvivalTime = true;
    public int floorReached = 1;
    public int spottedCount = 0;

    private float survivalStartTime;
    private int currentLives;
    private float lastHitTime = -999f;
    private int lastDamageSourceId = -1;
    private float lastDamageSourceHitTime = -999f;
    private bool respawnInProgress;
    private bool gameOverTriggered;
    private CanvasGroup respawnFadeGroup;

    public int CurrentLives => currentLives;
    public int MaxLives => Mathf.Max(1, maxLives);
    public bool IsDead => currentLives <= 0 || gameOverTriggered;

    void Awake()
    {
        PlayerDeathTracker.MaxDeathsBeforeGameOver = 3;
        currentLives = MaxLives;
        survivalStartTime = Time.time;
    }

    public bool TryTakeHit(int damage = 1, int sourceId = -1)
    {
        if (IsDead || respawnInProgress)
            return false;

        if (sourceId >= 0 && sourceId == lastDamageSourceId && Time.time - lastDamageSourceHitTime < Mathf.Max(0f, hitCooldown))
            return false;

        if (Time.time - lastHitTime < Mathf.Max(0f, hitCooldown))
            return false;

        int finalDamage = Mathf.Max(1, damage);
        currentLives = Mathf.Max(0, currentLives - finalDamage);
        lastHitTime = Time.time;

        if (sourceId >= 0)
        {
            lastDamageSourceId = sourceId;
            lastDamageSourceHitTime = Time.time;
        }

        if (currentLives > 0)
            return true;

        if (PlayerDeathTracker.RegisterDeath())
        {
            TriggerGameOver();
            return true;
        }

        if (respawnOnDeath)
        {
            currentLives = MaxLives;
            StartCoroutine(RespawnCurrentLevelRoutine());
        }

        return true;
    }

    private IEnumerator RespawnCurrentLevelRoutine()
    {
        respawnInProgress = true;

        if (useRespawnFade)
        {
            yield return StartCoroutine(PlayRespawnFade());
        }
        else
        {
            yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));
        }

        respawnInProgress = false;

        if (this == null)
            yield break;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(activeScene.name))
            SceneManager.LoadScene(activeScene.name);
    }

    private IEnumerator PlayRespawnFade()
    {
        float totalFadeDuration = Mathf.Max(0.08f, respawnFadeDuration);
        float holdDuration = Mathf.Max(0f, respawnDelay - totalFadeDuration);

        CanvasGroup fadeGroup = CreateRespawnFadeCanvas();
        if (fadeGroup == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));
            yield break;
        }

        float timer = 0f;
        while (timer < totalFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(timer / totalFadeDuration);
            fadeGroup.alpha = alpha;
            yield return null;
        }

        fadeGroup.alpha = 1f;

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);
    }

    private CanvasGroup CreateRespawnFadeCanvas()
    {
        GameObject overlay = new GameObject("RespawnFadeOverlay");
        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        Image image = overlay.AddComponent<Image>();
        image.color = respawnFadeColor;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CanvasGroup group = overlay.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        respawnFadeGroup = group;
        return group;
    }

    void TriggerGameOver()
    {
        if (gameOverTriggered)
            return;

        gameOverTriggered = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!loadGameOverSceneOnDeath)
        {
            Time.timeScale = 0f;
            Debug.Log("GAME OVER: Nyawa player habis.");
            return;
        }

        if (trackSurvivalTime)
            GameOverStats.SurvivalTime = Mathf.Max(0f, Time.time - survivalStartTime);

        GameOverStats.FloorReached = Mathf.Max(0, floorReached);
        GameOverStats.SpottedCount = Mathf.Max(0, spottedCount);

        PlayerDeathTracker.Reset();
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameOverSceneName);
    }

    public void ResetRunProgress()
    {
        PlayerDeathTracker.Reset();
        currentLives = MaxLives;
        gameOverTriggered = false;
        respawnInProgress = false;
    }

    public void SetFloorReached(int value)
    {
        floorReached = Mathf.Max(0, value);
    }

    public void AddSpottedCount(int value = 1)
    {
        spottedCount = Mathf.Max(0, spottedCount + Mathf.Max(0, value));
    }
}
