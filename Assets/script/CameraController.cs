using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;
    public Vector3 positionOffset = new Vector3(0, 3.5f, -5.5f);
    public Transform headTarget;
    public float headFocusHeightOffset = 0f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 3f;
    public float sensitivityCap = 6f;
    public float sensitivityScale = 0.35f;
    public float minPitchAngle = -60f;
    public float maxPitchAngle = 70f;

    [Header("Gamepad Look")]
    public float gamepadLookSpeed = 120f;
    [Range(0.05f, 0.5f)] public float gamepadLookDeadzone = 0.15f;

    [Header("Collision")]
    public float cameraCollisionRadius = 0.3f;
    public float cameraMinDistance = 0.5f;
    public LayerMask cameraCollisionLayer = -1;

    private float cameraPitch = 0f;
    private float cameraYaw = 0f;
    private Vector3 targetPosition;
    private CharacterController playerController;

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("Player tidak di-assign di CameraController!");
            return;
        }

        // Inisialisasi yaw ke arah player
        cameraYaw = player.eulerAngles.y;

        if (player != null)
            playerController = player.GetComponent<CharacterController>();
    }

    void Update()
    {
        if (player == null) return;

        HandleMouseLook();
        UpdateCameraPosition();
    }

    void HandleMouseLook()
    {
        bool isLooking = Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (isLooking)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float effectiveSensitivity = Mathf.Clamp(mouseSensitivity, 0.1f, Mathf.Max(0.1f, sensitivityCap))
                * Mathf.Max(0.01f, sensitivityScale);

            // Horizontal look (yaw)
            cameraYaw += mouseDelta.x * effectiveSensitivity * Time.deltaTime;

            // Vertical look (pitch)
            cameraPitch -= mouseDelta.y * effectiveSensitivity * Time.deltaTime;
            cameraPitch = Mathf.Clamp(cameraPitch, minPitchAngle, maxPitchAngle);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        HandleGamepadLook();
    }

    void HandleGamepadLook()
    {
        if (Gamepad.current == null)
            return;

        Vector2 stick = Gamepad.current.rightStick.ReadValue();
        if (stick.magnitude < gamepadLookDeadzone)
            return;

        cameraYaw += stick.x * gamepadLookSpeed * Time.deltaTime;
        cameraPitch -= stick.y * gamepadLookSpeed * Time.deltaTime;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitchAngle, maxPitchAngle);
    }

    void UpdateCameraPosition()
    {
        if (player == null) return;

        Vector3 focusPoint = GetFocusPoint();

        // Hanya rotate offset berdasarkan YAW (horizontal), jangan PITCH (vertical)
        Quaternion yawOnly = Quaternion.Euler(0, cameraYaw, 0);
        Vector3 targetCameraPos = focusPoint + yawOnly * positionOffset;

        // Cek collision dengan SphereCast
        Vector3 directionToCamera = (targetCameraPos - focusPoint).normalized;
        float desiredDistance = Vector3.Distance(focusPoint, targetCameraPos);

        RaycastHit hit;
        float finalDistance = desiredDistance;

        if (Physics.SphereCast(focusPoint, cameraCollisionRadius, directionToCamera, out hit, desiredDistance, cameraCollisionLayer))
        {
            // Ada obstacle, tarik kamera lebih dekat
            finalDistance = Mathf.Max(hit.distance - cameraCollisionRadius - 0.2f, cameraMinDistance);
        }

        // Set posisi kamera akhir
        Vector3 finalCameraPos = focusPoint + directionToCamera * finalDistance;

        // Smooth movement
        transform.position = Vector3.Lerp(transform.position, finalCameraPos, 0.15f);

        // Kembalikan feel kamera seperti semula: rotasi dikontrol oleh yaw + pitch mouse.
        Quaternion lookRotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        transform.rotation = lookRotation;
    }

    Vector3 GetFocusPoint()
    {
        if (headTarget != null)
            return headTarget.position + Vector3.up * headFocusHeightOffset;

        if (playerController != null)
        {
            float headHeight = playerController.center.y + (playerController.height * 0.5f);
            return player.position + Vector3.up * (headHeight + headFocusHeightOffset);
        }

        return player.position + Vector3.up * (1.7f + headFocusHeightOffset);
    }
}
