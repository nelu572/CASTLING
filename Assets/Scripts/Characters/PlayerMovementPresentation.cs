using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerMovementPresentation : MonoBehaviour
{
    private PlayerMovement movement;
    private PlayerEyes eyes;
    private PlayerGroundParticles groundParticles;
    private PlayerVisualFeedback visualFeedback;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        eyes = GetComponent<PlayerEyes>();
        groundParticles = GetComponent<PlayerGroundParticles>();
        visualFeedback = GetComponent<PlayerVisualFeedback>();
    }

    private void OnEnable()
    {
        movement.HorizontalInputChanged += OnHorizontalInputChanged;
        movement.WalkingUpdated += OnWalkingUpdated;
        movement.Jumped += OnJumped;
        movement.Landed += OnLanded;
    }

    private void OnDisable()
    {
        movement.HorizontalInputChanged -= OnHorizontalInputChanged;
        movement.WalkingUpdated -= OnWalkingUpdated;
        movement.Jumped -= OnJumped;
        movement.Landed -= OnLanded;
    }

    private void OnHorizontalInputChanged(float horizontalInput)
    {
        eyes?.SetLookDirection(horizontalInput);
    }

    private void OnWalkingUpdated(bool isWalking)
    {
        if (groundParticles != null && groundParticles.SetWalking(isWalking))
        {
            visualFeedback?.PlayStep();
        }
    }

    private void OnJumped()
    {
        groundParticles?.PlayJump();
    }

    private void OnLanded()
    {
        groundParticles?.PlayLanding();
        visualFeedback?.PlayLanding();
    }
}
