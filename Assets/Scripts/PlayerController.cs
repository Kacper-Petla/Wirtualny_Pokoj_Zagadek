using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float rotationSpeed = 90.0f;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!IsServer) return;

        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float moveX = 0f;
        float moveZ = 0f;

        if (keyboard.dKey.isPressed) moveX += 1f;
        if (keyboard.aKey.isPressed) moveX -= 1f;
        if (keyboard.wKey.isPressed) moveZ += 1f;
        if (keyboard.sKey.isPressed) moveZ -= 1f;

        Vector3 moveDirection = (transform.right * moveX + transform.forward * moveZ).normalized;
        characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
    }

    private void HandleRotation()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float turn = 0f;
        if (keyboard.qKey.isPressed) turn -= 1f;
        if (keyboard.eKey.isPressed) turn += 1f;

        if (turn != 0f)
        {
            transform.Rotate(Vector3.up, turn * rotationSpeed * Time.deltaTime);
        }
    }
}