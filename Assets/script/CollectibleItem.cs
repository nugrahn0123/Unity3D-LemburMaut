using UnityEngine;

// Tempelkan script ini di item penting (contoh: BUKU KEUANGAN, JADWAL RAPAT).
public class CollectibleItem : MonoBehaviour
{
    public static event System.Action<CollectibleItem> OnItemCollected;

    [Header("Info Item")]
    public string itemName = "Nama Item";
    [TextArea(3, 6)]
    public string itemDescription = "Deskripsi item.";

    [Header("Gambar Item")]
    [Tooltip("Sprite/texture item yang ditampilkan di pop-up. Bisa screenshot render item, atau foto dokumen.")]
    public Sprite itemSprite;

    [Header("Petunjuk (Opsional)")]
    [Tooltip("Isi jika item berisi kode sandi/petunjuk. Kosongkan jika tidak ada.")]
    public string secretCode = "";

    [Header("Interaksi")]
    public float interactRange = 2.5f;
    [Tooltip("Hancurkan objek setelah diambil. Jika false, hanya disembunyikan (SetActive false).")]
    public bool destroyOnPickup = false;

    private Transform player;
    private bool isPlayerNear;
    private bool isCollected;

    void Start()
    {
        FindPlayer();
    }

    void FindPlayer()
    {
        // Coba lewat komponen dulu, fallback ke tag "Player".
        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            player = playerMovement.transform;
            return;
        }

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    void Update()
    {
        if (isCollected)
            return;

        // Retry mencari player setiap frame jika belum ditemukan.
        if (player == null)
        {
            FindPlayer();
            return;
        }

        float distance = Vector3.Distance(player.position, transform.position);
        bool nearNow = distance <= interactRange;
        Debug.Log($"[{itemName}] Jarak ke player: {distance:F1} | Range: {interactRange} | Dekat: {nearNow}");

        if (nearNow && !isPlayerNear)
            ItemInspectionUI.Instance.ShowPrompt(this);
        else if (!nearNow && isPlayerNear)
            ItemInspectionUI.Instance.HidePrompt(this);

        isPlayerNear = nearNow;
    }

    // Dipanggil oleh ItemInspectionUI saat player menekan E.
    public void Collect()
    {
        if (isCollected)
            return;

        isCollected = true;
        ItemInspectionUI.Instance.ShowItemPopup(this);
        OnItemCollected?.Invoke(this);

        if (destroyOnPickup)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    void OnDisable()
    {
        if (isPlayerNear && ItemInspectionUI.Instance != null)
            ItemInspectionUI.Instance.HidePrompt(this);
        isPlayerNear = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
