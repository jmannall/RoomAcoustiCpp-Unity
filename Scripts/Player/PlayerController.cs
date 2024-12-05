using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(CharacterController))]

public class PlayerController : MonoBehaviour
{

    [SerializeField, Range(0, 10)]
    private float speed;

    public Transform cameraTransform;

    private Vector2 direction;

    private Vector3 worldDirection;

    private float gravity = 9.8f;

    public InputActionAsset inputActions;
    private CharacterController controller;

    [HideInInspector]
    public InputActionMap playerActionMap;
    private InputAction move;

    private void Awake()
    {
        playerActionMap = inputActions.FindActionMap("Player");
        playerActionMap.Enable();
    }
    private void Start()
    {
        move = playerActionMap["PlayerMove"];

        controller = GetComponent<CharacterController>();
        worldDirection.y = 0.0f;
    }

    void Update()
    {
        UpdateDirection();
        UpdateWorldDirection();
        controller.Move(speed * worldDirection * Time.deltaTime);
    }

    void UpdateDirection()
    {
        direction = move.ReadValue<Vector2>().normalized;
        transform.TransformDirection(direction);
    }

    void UpdateWorldDirection()
    {
        // transform direction to world space using the camera's transform
        worldDirection.x = cameraTransform.forward.x * direction.y + cameraTransform.right.x * direction.x;
        worldDirection.z = cameraTransform.forward.z * direction.y + cameraTransform.right.z * direction.x;

        if (controller.isGrounded)
            worldDirection.y = 0.0f;
        else
            worldDirection.y -= gravity * Time.deltaTime;
    }
}