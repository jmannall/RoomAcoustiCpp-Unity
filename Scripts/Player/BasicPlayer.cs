using UnityEngine;
using UnityEngine.InputSystem;

public class BasicPlayer : MonoBehaviour
{
    public float speed = 2.0f;
    [Range(0, 1)]
    public float mouseSensitivity = 1;
    public Vector2 pitchMinMax = new Vector2(-60, 85);

    //private float gravity = 9.8f;

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
        Camera.main.transform.eulerAngles = currentRotation;

        Vector2 moveValue = moveAction.ReadValue<Vector2>().normalized;
        Vector3 moveDirection = Camera.main.transform.forward * moveValue.y + Camera.main.transform.right * moveValue.x;
        moveDirection.y = 0.0f;
        moveDirection = moveDirection.normalized;

        //if (!characterController.isGrounded)
        //    moveDirection.y -= gravity * Time.deltaTime;
        characterController.Move(moveDirection * Time.deltaTime * speed);
    }
}
