using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput), typeof(PlayerMovement))]
public sealed class PlayerInputHandler : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerMovement movement;
    private CastlingAbility castling;
    private RookSlideAbility slide;
    private InputAction castlingAction;
    private bool hasInputFocus = true;
    private bool gameplayPaused;
    private int releaseCheckAfterFrame = -1;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        movement = GetComponent<PlayerMovement>();
        castling = GetComponent<CastlingAbility>();
        slide = GetComponent<RookSlideAbility>();
        castlingAction = playerInput.actions.FindAction("Castling", false);
    }

    private void OnEnable()
    {
        releaseCheckAfterFrame = Time.frameCount + 1;
    }

    private void Update()
    {
        if (castling == null || castlingAction == null || !hasInputFocus)
        {
            return;
        }

        if (!castling.IsAwaitingRelease || Time.frameCount <= releaseCheckAfterFrame)
        {
            return;
        }

        foreach (InputControl control in castlingAction.controls)
        {
            if (control.IsPressed()) return;
        }

        // A release while the action was disabled has no canceled callback.
        castling.CancelCastling();
        castling.SetCastlingHeld(false, InputState.currentTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (gameplayPaused || !playerInput.inputIsActive || (!context.performed && !context.canceled))
        {
            return;
        }

        float horizontalInput = context.ReadValue<float>();
        movement.SetHorizontalInput(horizontalInput);
        slide?.SetHorizontalInput(horizontalInput);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!gameplayPaused && playerInput.inputIsActive && context.performed)
        {
            if (slide == null || !slide.TryJump())
            {
                movement.RequestJump();
            }
        }
    }

    public void OnCastling(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            // Disabling an action also sends canceled before its enabled flags change.
            bool released = context.control != null && !context.control.IsPressed();
            if (released && isActiveAndEnabled && playerInput.inputIsActive && context.action.enabled)
            {
                castling?.SetCastlingHeld(false, context.time);
            }
            else
            {
                castling?.CancelCastling();
                releaseCheckAfterFrame = Time.frameCount + 1;
            }
        }
        else if (!gameplayPaused && hasInputFocus && isActiveAndEnabled && playerInput.inputIsActive && context.performed)
        {
            castling?.SetCastlingHeld(true, context.time);
        }
    }

    private void OnDisable()
    {
        castling?.CancelCastling();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        hasInputFocus = hasFocus;
        if (!hasFocus)
        {
            castling?.CancelCastling();
        }
        else
        {
            releaseCheckAfterFrame = Time.frameCount + 1;
        }
    }

    public void OnSlide(InputAction.CallbackContext context)
    {
        if (gameplayPaused || !playerInput.inputIsActive || (!context.performed && !context.canceled))
        {
            return;
        }

        slide?.SetSlideHeld(context.ReadValueAsButton());
    }

    public void SetGameplayPaused(bool paused)
    {
        gameplayPaused = paused;
        if (paused)
        {
            // Pause preserves movement state, but never queues a CASTLING attempt.
            castling?.CancelCastling();
            return;
        }

        if (playerInput == null || movement == null || !playerInput.inputIsActive) return;

        // Actions keep observing held controls while callbacks are gated. Use this
        // player's map: King and Rook both have an action named Move.
        InputActionMap map = playerInput.currentActionMap;
        float horizontal = map?.FindAction("Move", false)?.ReadValue<float>() ?? 0f;
        movement.SetHorizontalInput(horizontal);
        slide?.SetHorizontalInput(horizontal);
        slide?.SetSlideHeld(map?.FindAction("Slide", false)?.IsPressed() ?? false);
    }
}
