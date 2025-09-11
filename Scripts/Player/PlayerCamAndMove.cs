using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamAndMove : MonoBehaviour
{
    public Transform firstPersonCamera;

    public float speed = 2.0f;
    [Range(0, 1)]
    public float mouseSensitivity = 1;
    public Vector2 pitchMinMax = new Vector2(-60, 85);

    private float gravity = 9.8f;
    private float verticalVelocity = 0.0f;

    private Vector3 currentRotation;
    private Vector3 currentRotationVelocity;

    private float yaw = 0.0f;
    private float pitch = 0.0f;
    private float maxRotationSpeed = 10;

    private CharacterController characterController;

    InputAction moveAction, lookAction;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        characterController = GetComponent<CharacterController>();

        moveAction = InputSystem.actions.FindAction("Move");
        lookAction = InputSystem.actions.FindAction("Look");

        // If no camera was assigned, all movement is with respect to self.
        if (firstPersonCamera == null)
            firstPersonCamera = this.transform;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 lookValue = lookAction.ReadValue<Vector2>();

        lookValue.x = Mathf.Clamp(lookValue.x * mouseSensitivity, -maxRotationSpeed, maxRotationSpeed);
        lookValue.y = Mathf.Clamp(lookValue.y * mouseSensitivity, -maxRotationSpeed, maxRotationSpeed);

        yaw += lookValue.x;
        pitch -= lookValue.y;

        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(pitch, yaw), ref currentRotationVelocity, 0.1f);
        firstPersonCamera.eulerAngles = currentRotation;

        Vector2 moveValue = moveAction.ReadValue<Vector2>().normalized;
        Vector3 moveDirection = firstPersonCamera.forward * moveValue.y + firstPersonCamera.right * moveValue.x;
        moveDirection.y = 0.0f;
        moveDirection = moveDirection.normalized;

        if (characterController.isGrounded)
            verticalVelocity = Mathf.Min(verticalVelocity, -0.5f); // small stick-to-ground force
        else
            verticalVelocity -= gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * speed;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }
}
