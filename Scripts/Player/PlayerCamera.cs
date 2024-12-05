using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField]
    private bool lockCursor;

    [SerializeField, Range(0, 1)]
    private float mouseSensitivity = 1;

    [SerializeField]
    private Vector2 pitchMinMax = new Vector2(-40, 85);

    [SerializeField]
    private float rotationSmoothTime = 0.12f;

    Vector3 rotationSmoothVelocity;
    Vector3 currentRotation;

    float yaw;
    float pitch;

    private InputAction look;

    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        InputActionMap inputActionMap = GetComponentInParent<PlayerController>().playerActionMap;
        if (inputActionMap.enabled)
            look = inputActionMap["PlayerLook"];
    }

    void Update()
    {
        if (look == null)
            return;
        UpdateYawPitch();
        UpdateCurrentRotation();
        transform.eulerAngles = currentRotation;
    }

    void UpdateYawPitch()
    {
        yaw += look.ReadValue<Vector2>().x * mouseSensitivity;
        pitch -= look.ReadValue<Vector2>().y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);
    }

    void UpdateCurrentRotation()
    {
        currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(pitch, yaw), ref rotationSmoothVelocity, rotationSmoothTime);
    }
}