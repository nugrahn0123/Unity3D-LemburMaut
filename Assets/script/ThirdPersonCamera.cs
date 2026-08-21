using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform player;

    public float distance = 5.75f;
    public float cameraHeightAboveHead = 0.9f;
    public float lookAtHeightOffset = -0.2f;
    public float smoothSpeed = 10f;

    [Header("Orbit Controls")]
    public float sensitivity = 200f;
    public float sensitivityCap = 280f;
    public float sensitivityScale = 0.35f;
    public float pitchMin = -20f;
    public float pitchMax = 60f;
    public bool requireRightMouse = true;
    public float initialPitch = 8f;

    private float yaw = 0f;
    private float pitch = 8f;
    private CharacterController playerController;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = Mathf.Approximately(angles.x, 0f) ? initialPitch : NormalizePitchAngle(angles.x);
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        if (player != null)
            playerController = player.GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Read input from mouse (right button + delta) or gamepad right stick
        Vector2 lookDelta = Vector2.zero;
        bool haveLookInput = false;

        if (Mouse.current != null)
        {
            if (!requireRightMouse || Mouse.current.rightButton.isPressed)
            {
                lookDelta = Mouse.current.delta.ReadValue();
                haveLookInput = lookDelta.sqrMagnitude > 0.0001f;
            }
        }

        // Keyboard IJLK as alternative for orbiting (I=up, K=down, J=left, L=right)
        if (Keyboard.current != null)
        {
            Vector2 keyDelta = Vector2.zero;
            if (Keyboard.current.iKey.isPressed) keyDelta.y += 1f;
            if (Keyboard.current.kKey.isPressed) keyDelta.y -= 1f;
            if (Keyboard.current.lKey.isPressed) keyDelta.x += 1f;
            if (Keyboard.current.jKey.isPressed) keyDelta.x -= 1f;
            if (keyDelta.sqrMagnitude > 0.0001f)
            {
                // scale keyboard to be similar magnitude to mouse/gamepad
                lookDelta += keyDelta * 100f;
                haveLookInput = true;
            }
        }

        if (!haveLookInput && Gamepad.current != null)
        {
            Vector2 rs = Gamepad.current.rightStick.ReadValue();
            if (rs.sqrMagnitude > 0.0001f)
            {
                // gamepad values are smaller; scale them similar to mouse
                lookDelta = rs * 100f;
                haveLookInput = true;
            }
        }

        if (haveLookInput)
        {
            float effectiveSensitivity = Mathf.Clamp(sensitivity, 1f, Mathf.Max(1f, sensitivityCap))
                * Mathf.Max(0.01f, sensitivityScale);

            yaw += lookDelta.x * effectiveSensitivity * Time.deltaTime / 100f;
            pitch -= lookDelta.y * effectiveSensitivity * Time.deltaTime / 100f;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        float headHeight = GetHeadHeight();
        Vector3 focusPoint = player.position + Vector3.up * Mathf.Max(0f, headHeight + lookAtHeightOffset);
        Vector3 desiredPosition = player.position + Vector3.up * (headHeight + cameraHeightAboveHead) + rot * new Vector3(0f, 0f, -distance);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        transform.LookAt(focusPoint);
    }

    float NormalizePitchAngle(float angle)
    {
        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    float GetHeadHeight()
    {
        if (playerController != null)
            return playerController.center.y + (playerController.height * 0.5f);

        return 1.8f;
    }
}