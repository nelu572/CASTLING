using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(PlayerInput))]
public sealed class PlayerMovement : MonoBehaviour
{
    [SerializeField] private PlayerMovementSettings settings;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private PlayerInput playerInput;
    private PlayerGroundParticles groundParticles;
    private PlayerEyes eyes;
    private PlayerVisualFeedback visualFeedback;

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
        bodyCollider = GetComponent<PolygonCollider2D>();
        playerInput = GetComponent<PlayerInput>();
        groundParticles = GetComponent<PlayerGroundParticles>();
        eyes = GetComponent<PlayerEyes>();
        visualFeedback = GetComponent<PlayerVisualFeedback>();
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
        bool hasGroundContact = IsGrounded(out Rigidbody2D groundBody);
        float targetSpeed = horizontalInput * settings.MaximumRunSpeed;
        if (hasGroundContact && groundBody != null)
        {
            targetSpeed += groundBody.linearVelocity.x;
        }

        bool hasMoveInput = !Mathf.Approximately(horizontalInput, 0f);
        bool justLanded = hasInitializedGroundState && hasGroundContact && !wasGrounded;
        if (hasGroundContact)
        {
            lastGroundedAt = Time.time;
        }

        wasGrounded = hasGroundContact;
        hasInitializedGroundState = true;
        if (groundParticles != null && groundParticles.SetWalking(hasMoveInput && hasGroundContact))
        {
            visualFeedback?.PlayStep();
        }
        if (justLanded)
        {
            groundParticles?.PlayLanding();
            visualFeedback?.PlayLanding();
        }

        bool canJump = Time.time - lastGroundedAt <= settings.CoyoteTime;
        float acceleration = hasMoveInput
            ? (hasGroundContact ? settings.GroundAcceleration : settings.AirAcceleration)
            : (hasGroundContact ? settings.GroundDeceleration : settings.AirAcceleration);

        Vector2 velocity = body.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);

        if (jumpBufferedUntil >= Time.time && canJump)
        {
            velocity.y = settings.JumpSpeed;
            jumpBufferedUntil = float.NegativeInfinity;
            lastGroundedAt = float.NegativeInfinity;
            groundParticles?.PlayJump();
        }

        body.gravityScale = velocity.y < 0f
            ? settings.GravityScale * settings.FallGravityMultiplier
            : settings.GravityScale;

        body.linearVelocity = velocity;
    }

    private bool IsGrounded(out Rigidbody2D groundBody)
    {
        groundBody = null;
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
                groundBody = overlap.attachedRigidbody;
                return true;
            }
        }

        return false;
    }
}
