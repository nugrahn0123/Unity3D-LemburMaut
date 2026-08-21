using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ScanlineDrift : MonoBehaviour
{
    [SerializeField] private float verticalSpeed = 7f;
    [SerializeField] private float horizontalJitter = 3f;
    [SerializeField] private float tearChancePerSecond = 0.8f;
    [SerializeField] private float tearDecay = 8f;
    [SerializeField] private float alphaPulse = 0.06f;

    private Image image;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 basePos;
    private float tearOffset;
    private float seed;

    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        basePos = rectTransform.anchoredPosition;
        seed = Random.Range(0f, 999f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        if (Random.value < tearChancePerSecond * dt)
        {
            tearOffset = Random.Range(-0.08f, 0.08f);
        }

        tearOffset = Mathf.Lerp(tearOffset, 0f, dt * tearDecay);

        float xJitter = (Mathf.PerlinNoise(seed, t * 0.8f) - 0.5f) * horizontalJitter + tearOffset;
        float yDrift = Mathf.Repeat(t * verticalSpeed, 24f);
        rectTransform.anchoredPosition = basePos + new Vector2(xJitter, -yDrift);

        float pulse = 0.5f + (0.5f * Mathf.Sin((t * 6.5f) + seed));
        canvasGroup.alpha = Mathf.Clamp01(0.24f + (pulse * alphaPulse));
    }
}
