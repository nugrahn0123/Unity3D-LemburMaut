using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class FlickerLight : MonoBehaviour
{
    [SerializeField] private float minAlpha = 0.72f;
    [SerializeField] private float maxAlpha = 0.96f;
    [SerializeField] private float baseSpeed = 10f;
    [SerializeField] private float randomJitter = 0.08f;
    [SerializeField] private float pulseSpeed = 2.6f;
    [SerializeField] private Color lowColor = new Color(0.54f, 0.58f, 0.28f, 1f);
    [SerializeField] private Color highColor = new Color(0.86f, 0.95f, 0.45f, 1f);

    private CanvasGroup canvasGroup;
    private Image image;
    private float seed;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        image = GetComponent<Image>();
        seed = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        float n1 = Mathf.PerlinNoise(seed, time * baseSpeed * 0.1f);
        float n2 = Mathf.PerlinNoise(seed + 57f, time * baseSpeed * 0.27f);
        float blend = Mathf.Clamp01((n1 * 0.7f) + (n2 * 0.3f));
        float targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, blend);

        // Sesekali drop tipis untuk efek lampu tabung tua.
        if (Random.value < randomJitter * Time.unscaledDeltaTime)
        {
            targetAlpha *= Random.Range(0.4f, 0.8f);
        }

        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 18f);

        if (image != null)
        {
            float hum = 0.5f + (0.5f * Mathf.Sin(time * pulseSpeed));
            float colorMix = Mathf.Clamp01((targetAlpha * 0.75f) + (hum * 0.25f));
            image.color = Color.Lerp(lowColor, highColor, colorMix);
        }
    }
}
