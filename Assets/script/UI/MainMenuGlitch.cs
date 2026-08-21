using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class MainMenuGlitch : MonoBehaviour
{
    [Header("Gerakan halus")]
    [SerializeField] private Vector2 microJitter = new Vector2(0.4f, 0.25f);
    [SerializeField] private float microSpeed = 22f;

    [Header("Glitch burst")]
    [SerializeField] private float burstChancePerSecond = 1.8f;
    [SerializeField] private Vector2 burstOffset = new Vector2(8f, 2f);
    [SerializeField] private Vector2 burstDurationRange = new Vector2(0.03f, 0.12f);

    [Header("Flicker teks")]
    [SerializeField] private bool affectTextColor = true;
    [SerializeField] private Color burstTint = new Color(1f, 0.84f, 0.84f, 1f);

    private RectTransform rectTransform;
    private TMP_Text tmpText;
    private Vector2 basePos;
    private Color baseColor;
    private float seed;
    private float burstTimer;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        tmpText = GetComponent<TMP_Text>();
        basePos = rectTransform.anchoredPosition;
        if (tmpText != null)
        {
            baseColor = tmpText.color;
        }

        seed = Random.Range(20f, 900f);
    }

    private void OnEnable()
    {
        basePos = rectTransform.anchoredPosition;
        burstTimer = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        if (burstTimer <= 0f && Random.value < burstChancePerSecond * dt)
        {
            burstTimer = Random.Range(burstDurationRange.x, burstDurationRange.y);
        }

        Vector2 offset = GetMicroOffset(t);
        if (burstTimer > 0f)
        {
            burstTimer -= dt;
            offset += new Vector2(Random.Range(-burstOffset.x, burstOffset.x), Random.Range(-burstOffset.y, burstOffset.y));

            if (tmpText != null && affectTextColor)
            {
                float pulse = 0.5f + (0.5f * Mathf.Sin(t * 80f));
                tmpText.color = Color.Lerp(baseColor, burstTint, pulse);
            }
        }
        else if (tmpText != null)
        {
            tmpText.color = baseColor;
        }

        rectTransform.anchoredPosition = basePos + offset;
    }

    private Vector2 GetMicroOffset(float t)
    {
        float nx = (Mathf.PerlinNoise(seed, t * microSpeed * 0.1f) - 0.5f) * 2f;
        float ny = (Mathf.PerlinNoise(seed + 43.1f, t * microSpeed * 0.12f) - 0.5f) * 2f;
        return new Vector2(nx * microJitter.x, ny * microJitter.y);
    }
}
