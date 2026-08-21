using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class CutScene1 : MonoBehaviour
{
    [Header("Waypoints Kamera")]
    public Transform[] waypoints;
    
    [Header("Pengaturan Movement")]
    public float movementSpeed = 3f;
    public float lookAheadDistance = 2f;
    public float waypointArrivalThreshold = 0.5f;
    
    [Header("Timing")]
    public float transitionDuration = 1f;

    [Header("Scene Setelah Cutscene")]
    public string nextSceneName = "Scene Lantai 6";
    public float delayBeforeLoad = 1f;
    
    private Camera mainCam;
    private int currentWaypoint = 0;
    private bool isCutscenePlaying = false;
    
    public bool IsCutscenePlaying => isCutscenePlaying;
    public bool IsDone => !isCutscenePlaying && currentWaypoint >= waypoints.Length;

    void Start()
    {
        mainCam = Camera.main;
        if (mainCam == null)
            Debug.LogError("CutScene1: Main camera tidak ditemukan!");
        
        // Auto-play cutscene saat scene load
        Invoke(nameof(PlayCutscene), 0.5f);
    }

    void Update()
    {
        // Debug: tekan R untuk restart cutscene
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            StopCutscene();
            PlayCutscene();
        }
    }

    // Panggil dari luar untuk mulai cutscene
    public void PlayCutscene()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("CutScene1: Belum ada waypoints yang diatur!");
            return;
        }
        
        currentWaypoint = 0;
        StartCoroutine(CutsceneRoutine());
    }

    IEnumerator CutsceneRoutine()
    {
        isCutscenePlaying = true;

        // Pindah ke setiap waypoint
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;

            yield return StartCoroutine(MoveToWaypoint(waypoints[i]));
            currentWaypoint++;
        }

        isCutscenePlaying = false;

        yield return new WaitForSeconds(delayBeforeLoad);
        if (!string.IsNullOrWhiteSpace(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator MoveToWaypoint(Transform target)
    {
        Vector3 startPos = mainCam.transform.position;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);

            // Smooth movement ke waypoint
            mainCam.transform.position = Vector3.Lerp(startPos, target.position, EaseInOutCubic(t));

            // Look at waypoint dengan smooth
            mainCam.transform.rotation = Quaternion.Lerp(
                mainCam.transform.rotation,
                Quaternion.LookRotation(target.forward),
                t * 0.5f
            );

            yield return null;
        }

        // Pastikan posisi dan rotasi final
        mainCam.transform.position = target.position;
        mainCam.transform.rotation = Quaternion.LookRotation(target.forward);

        // Tunggu sedikit di waypoint ini sebelum pindah ke berikutnya
        yield return new WaitForSeconds(0.5f);
    }

    // Easing function untuk smooth movement
    float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    // Reset untuk testing
    public void StopCutscene()
    {
        StopAllCoroutines();
        isCutscenePlaying = false;
        currentWaypoint = 0;
    }
}
