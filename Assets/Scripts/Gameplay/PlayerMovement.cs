using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(PlayerInput))]
public sealed class PlayerMovement : MonoBehaviour
{
    [SerializeField] private PlayerMovementSettings settings;

    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private PlayerInput playerInput;
    private PlayerGroundParticles groundParticles;
    private PlayerEyes eyes;
    private InputAction jumpAction;

    private readonly Collider2D[] groundCheckResults = new Collider2D[4];
    private ContactFilter2D groundFilter;

    private float horizontalInput;
    private float lastGroundedAt = float.NegativeInfinity;
    private float jumpBufferedUntil = float.NegativeInfinity;
    private bool wasGrounded;
    private bool hasInitializedGroundState;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        playerInput = GetComponent<PlayerInput>();
        groundParticles = GetComponent<PlayerGroundParticles>();
        eyes = GetComponent<PlayerEyes>();
        int groundLayerMask = LayerMask.GetMask(Layers.Environment, Layers.Player);
        if (body == null || bodyCollider == null || playerInput == null || settings == null || groundLayerMask == 0)
        {
            enabled = false;
            return;
        }

        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayerMask);
        groundFilter.useTriggers = false;
        body.gravityScale = settings.GravityScale;
    }

    private void Start()
    {
        jumpAction = playerInput.currentActionMap?.FindAction("Jump");
    }

    private void OnDisable()
    {
        horizontalInput = 0f;
        jumpBufferedUntil = float.NegativeInfinity;
    }

    public void OnMove(InputValue inputValue)
    {
        horizontalInput = inputValue.Get<float>();
        eyes?.SetLookDirection(horizontalInput);
    }

    public void OnJump(InputValue inputValue)
    {
        if (inputValue.isPressed)
        {
            jumpBufferedUntil = Time.time + settings.JumpBufferTime;
        }
    }

    private void FixedUpdate()
    {
        float targetSpeed = horizontalInput * settings.MaximumRunSpeed;
        bool hasMoveInput = !Mathf.Approximately(horizontalInput, 0f);
        bool hasGroundContact = IsGrounded();
        bool justLanded = hasInitializedGroundState && hasGroundContact && !wasGrounded;
        if (hasGroundContact)
        {
            lastGroundedAt = Time.time;
        }

        wasGrounded = hasGroundContact;
        hasInitializedGroundState = true;
        groundParticles?.SetWalking(hasMoveInput && hasGroundContact);
        if (justLanded)
        {
            groundParticles?.PlayLanding();
        }

        bool isGrounded = Time.time - lastGroundedAt <= settings.CoyoteTime;
        float acceleration = hasMoveInput
            ? (isGrounded ? settings.GroundAcceleration : settings.AirAcceleration)
            : settings.GroundDeceleration;

        Vector2 velocity = body.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);

        if (jumpBufferedUntil >= Time.time && isGrounded)
        {
            velocity.y = settings.JumpSpeed;
            jumpBufferedUntil = float.NegativeInfinity;
            lastGroundedAt = float.NegativeInfinity;
            groundParticles?.PlayJump();
        }

        bool shouldUseFallGravity = velocity.y < 0f
            || (velocity.y > 0f && jumpAction != null && !jumpAction.IsPressed());
        body.gravityScale = shouldUseFallGravity
            ? settings.GravityScale * settings.FallGravityMultiplier
            : settings.GravityScale;

        body.linearVelocity = velocity;
    }

    private bool IsGrounded()
    {
        Bounds bounds = bodyCollider.bounds;
        Vector2 size = new(
            bounds.size.x * settings.GroundCheckWidthMultiplier,
            settings.GroundCheckHeight);
        Vector2 center = new(
            bounds.center.x,
            bounds.min.y - size.y * 0.25f);

        int overlapCount = Physics2D.OverlapBox(center, size, 0f, groundFilter, groundCheckResults);
        for (int index = 0; index < overlapCount; index++)
        {
            Collider2D overlap = groundCheckResults[index];
            if (overlap != null && overlap.attachedRigidbody != body)
            {
                return true;
            }
        }

        return false;
    }
}
