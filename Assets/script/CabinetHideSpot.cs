using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class CabinetHideSpot : MonoBehaviour
{
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private Transform hidePosition;
    [SerializeField] private Transform cameraViewPoint;
    [SerializeField] private TextMeshProUGUI interactionText;
    
    private bool playerInRange = false;
    private bool isHidden = false;
    private GameObject player;
    private Vector3 positionBeforeHide;
    private Renderer[] hiddenRenderers;
    private System.Collections.Generic.List<Collider> disabledColliders = new System.Collections.Generic.List<Collider>();

    public bool IsPlayerHidden => isHidden;
    private static CabinetHideSpot currentHidingSpot;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (this == null) return;
    }

    // Keyboard E atau tombol B (east) di gamepad.
    private static bool InteractPressedThisFrame()
    {
        bool key = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool pad = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        return key || pad;
    }

    void Update()
    {
        if (player == null) return;

        // Jika sudah tersembunyi, check input untuk keluar tanpa perlu cek jarak
        if (isHidden)
        {
            if (interactionText)
                interactionText.text = "E - Keluar";

            if (InteractPressedThisFrame())
            {
                UnhidePlayer();
            }
            return;
        }

        // Jika belum tersembunyi, cek jarak dan input untuk masuk
        float distance = Vector3.Distance(player.transform.position, transform.position);
        playerInRange = distance <= interactionRange;

        if (playerInRange)
        {
            if (interactionText)
                interactionText.text = "E - Sembunyi";

            if (InteractPressedThisFrame())
            {
                HidePlayer();
            }
        }
        else
        {
            if (interactionText)
                interactionText.text = "";
        }
    }

    private void HidePlayer()
    {
        positionBeforeHide = player.transform.position;

        // Matikan kontrol & collider utama agar player tidak bisa jalan saat sembunyi
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement) movement.enabled = false;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        disabledColliders.Clear();
        Collider[] colliders = player.GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            if (col.enabled)
            {
                col.enabled = false;
                disabledColliders.Add(col);
            }
        }

        // Sembunyikan semua renderer (model player ada di child)
        hiddenRenderers = player.GetComponentsInChildren<Renderer>(false);
        foreach (Renderer r in hiddenRenderers)
            r.enabled = false;

        // Pindahkan player ke dalam lemari
        if (hidePosition)
            player.transform.position = hidePosition.position;

        // Set camera view ke dalam lemari
        Camera mainCamera = Camera.main;
        if (mainCamera && cameraViewPoint)
        {
            mainCamera.transform.position = cameraViewPoint.position;
            mainCamera.transform.rotation = cameraViewPoint.rotation;
        }

        currentHidingSpot = this;
        isHidden = true;
    }

    public void UnhidePlayer()
    {
        if (player == null) return;

        // Kembalikan ke posisi sebelum masuk (titik yang pasti bebas tembok),
        // dilakukan SEBELUM CharacterController diaktifkan lagi agar teleport bersih.
        player.transform.position = positionBeforeHide;

        if (hiddenRenderers != null)
        {
            foreach (Renderer r in hiddenRenderers)
                if (r) r.enabled = true;
            hiddenRenderers = null;
        }

        foreach (Collider col in disabledColliders)
        {
            // CharacterController diaktifkan terpisah di bawah.
            if (col != null && !(col is CharacterController))
                col.enabled = true;
        }
        disabledColliders.Clear();

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = true;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement)
        {
            movement.enabled = true;
            movement.ResetVelocity();
        }

        // Reset text
        if (interactionText)
            interactionText.text = "";

        currentHidingSpot = null;
        isHidden = false;
    }

    // Cek apakah player sedang tersembunyi
    public static bool IsPlayerHiddenInCabinet()
    {
        return currentHidingSpot != null && currentHidingSpot.isHidden;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 2f);
    }
}
