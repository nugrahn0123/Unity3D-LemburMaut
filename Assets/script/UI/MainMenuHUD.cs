using TMPro;
using UnityEngine;

public class MainMenuHUD : MonoBehaviour
{
    [Header("Referensi UI")]
    public TextMeshProUGUI clockText;
    public TextMeshProUGUI statusText;

    [Header("Format")]
    [SerializeField] private string clockFormat = "HH:mm";
    [SerializeField] private string statusTextValue = "1 KARYAWAN MASIH DI DALAM GEDUNG";

    private void OnEnable()
    {
        ApplyStatus();
        UpdateClock();
    }

    private void Update()
    {
        UpdateClock();
    }

    private void ApplyStatus()
    {
        if (statusText != null)
        {
            statusText.text = statusTextValue;
        }
    }

    private void UpdateClock()
    {
        if (clockText != null)
        {
            clockText.text = System.DateTime.Now.ToString(clockFormat);
        }
    }
}
