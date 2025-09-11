using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField]
    private bool lockCursor;

    [SerializeField, Range(0, 1)]
    private float mouseSensitivity = 1f;

    [SerializeField]
    private Vector2 pitchMinMax = new Vector2(-60, 85);

    [SerializeField]
    private float rotationSmoothTime = 0.12f;

    private Vector3 currentRotation;
    private Vector3 currentRotationVelocity;
    
    private float yaw = 0f;
    private float pitch = 0f;

    private InputAction look;

    private PlayerController playerController;
    private Transform firstPersonCamera;

    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        playerController = GetComponentInParent<PlayerController>();
        InputActionMap inputActionMap = playerController.playerActionMap;
        if (inputActionMap.enabled)
            look = inputActionMap["PlayerLook"];

        firstPersonCamera = playerController.firstPersonCamera;

        // If no camera was assigned, all movement is with respect to self.
        if (firstPersonCamera == null)
            firstPersonCamera = this.transform;

        // Set the rotation "reference frame" to match the initial state of the object.
        pitch = firstPersonCamera.eulerAngles.x;
        yaw = firstPersonCamera.eulerAngles.y;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        currentRotation = new Vector3(pitch, yaw, 0f);
        currentRotationVelocity = Vector3.zero;
    }

    void Update()
    {
        if (look == null)
            return;

        yaw += look.ReadValue<Vector2>().x * mouseSensitivity;
        pitch -= look.ReadValue<Vector2>().y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(pitch, yaw), ref currentRotationVelocity, rotationSmoothTime);
        firstPersonCamera.eulerAngles = currentRotation;
    }
}