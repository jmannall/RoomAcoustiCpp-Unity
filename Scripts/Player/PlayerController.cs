using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]

public class PlayerController : MonoBehaviour
{

    [SerializeField, Range(0, 10)]
    private float speed = 1f;

    public Transform firstPersonCamera;

    private Vector2 direction;
    private Vector3 worldDirection;

    private float gravity = 9.8f;
    private float verticalVelocity = 0f;

    public InputActionAsset inputActions;
    private CharacterController controller;

    [HideInInspector]
    public InputActionMap playerActionMap;
    private InputAction move;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("Input Actions not set in the Player Controller");
            return;
        }
        playerActionMap = inputActions.FindActionMap("Player");
        playerActionMap.Enable();
    }
    private void Start()
    {
        controller = GetComponent<CharacterController>();
        worldDirection.y = 0.0f;

        if (inputActions == null)
            return;

        move = playerActionMap["PlayerMove"];
    }

    void Update()
    {
        if (inputActions == null)
            return;

        direction = move.ReadValue<Vector2>().normalized;
        transform.TransformDirection(direction);

        // transform direction to world space using the camera's transform
        worldDirection = firstPersonCamera.forward * direction.y + firstPersonCamera.right * direction.x;
        worldDirection.y = 0.0f;
        worldDirection = worldDirection.normalized;

        if (controller.isGrounded)
            verticalVelocity = Mathf.Min(verticalVelocity, -0.5f); // small stick-to-ground force
        else
            verticalVelocity -= gravity * Time.deltaTime;

        worldDirection.y = verticalVelocity;

        controller.Move(speed * worldDirection * Time.deltaTime);
    }
}