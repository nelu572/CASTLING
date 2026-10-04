using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput), typeof(PlayerMovement))]
public sealed class PlayerInputHandler : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerMovement movement;
    private CastlingAbility castling;
    private RookSlideAbility slide;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        movement = GetComponent<PlayerMovement>();
        castling = GetComponent<CastlingAbility>();
        slide = GetComponent<RookSlideAbility>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!playerInput.inputIsActive || (!context.performed && !context.canceled))
        {
            return;
        }

        float horizontalInput = context.ReadValue<float>();
        movement.SetHorizontalInput(horizontalInput);
        slide?.SetHorizontalInput(horizontalInput);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (playerInput.inputIsActive && context.performed)
        {
            movement.RequestJump();
        }
    }

    public void OnCastling(InputAction.CallbackContext context)
    {
        if (playerInput.inputIsActive && context.performed)
        {
            castling?.RequestCastling();
        }
    }

    public void OnSlide(InputAction.CallbackContext context)
    {
        if (!playerInput.inputIsActive || (!context.performed && !context.canceled))
        {
            return;
        }

        slide?.SetSlideHeld(context.ReadValueAsButton());
    }
}
