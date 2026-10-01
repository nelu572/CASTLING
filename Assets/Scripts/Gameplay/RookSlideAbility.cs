using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerMovement), typeof(PlayerInput))]
public sealed class RookSlideAbility : MonoBehaviour
{
    [SerializeField] private PlayerMovementSettings settings;

    private Rigidbody2D body;
    private PlayerMovement movement;
    private float horizontalInput;
    private float originalGravity;
    private bool originalMovementEnabled;
    private bool shiftHeld;
    private bool ready = true;

    public bool IsMoving { get; private set; }
    public float Direction { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        if (settings == null)
        {
            Debug.LogError("Rook slide movement settings are missing.", this);
            enabled = false;
        }
    }

    public void OnMove(InputValue inputValue)
    {
        horizontalInput = inputValue.Get<float>();
        TryBeginSlide();
    }

    public void OnSlide(InputValue inputValue)
    {
        shiftHeld = inputValue.isPressed;
        if (!shiftHeld)
        {
            StopSlide();
            ready = true;
            return;
        }

        TryBeginSlide();
    }

    private void TryBeginSlide()
    {
        if (!enabled || IsMoving || !ready || !shiftHeld ||
            Mathf.Approximately(horizontalInput, 0f) || Time.timeScale <= 0f)
        {
            return;
        }

        Direction = Mathf.Sign(horizontalInput);
        originalGravity = body.gravityScale;
        originalMovementEnabled = movement.enabled;
        movement.enabled = false;
        body.gravityScale = 0f;
        IsMoving = true;
        ready = false;
        body.linearVelocity = Vector2.right * (Direction * settings.MaximumRunSpeed * 2f);
    }

    private void FixedUpdate()
    {
        if (IsMoving)
        {
            body.linearVelocity = Vector2.right * (Direction * settings.MaximumRunSpeed * 2f);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision) => StopAtTerrain(collision);
    private void OnCollisionStay2D(Collision2D collision) => StopAtTerrain(collision);

    private void StopAtTerrain(Collision2D collision)
    {
        if (!IsMoving || collision.gameObject.layer != LayerMask.NameToLayer(Layers.Environment))
        {
            return;
        }

        for (int index = 0; index < collision.contactCount; index++)
        {
            if (collision.GetContact(index).normal.x * Direction < -0.5f)
            {
                StopSlide();
                return;
            }
        }
    }

    public void StopSlide()
    {
        if (!IsMoving)
        {
            return;
        }

        IsMoving = false;
        body.gravityScale = originalGravity;
        body.linearVelocity = Vector2.zero;
        movement.enabled = originalMovementEnabled;
        movement.SetHorizontalInput(horizontalInput);
    }

    private void OnDisable()
    {
        StopSlide();
        shiftHeld = false;
        horizontalInput = 0f;
        ready = true;
    }
}
