using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "CASTLING/Player Movement Settings")]
public sealed class PlayerMovementSettings : ScriptableObject
{
    [Header("Move")]
    [Min(0f)] [SerializeField] private float maximumRunSpeed = 9f;
    [Min(0f)] [SerializeField] private float groundAcceleration = 70f;
    [Min(0f)] [SerializeField] private float groundDeceleration = 90f;
    [Min(0f)] [SerializeField] private float airAcceleration = 32f;

    [Header("Jump")]
    [Min(0f)] [SerializeField] private float jumpSpeed = 8.5f;
    [Min(0f)] [SerializeField] private float coyoteTime = 0.12f;
    [Min(0f)] [SerializeField] private float jumpBufferTime = 0.12f;

    [Header("Ground Check")]
    [Range(0.1f, 1f)] [SerializeField] private float groundCheckWidthMultiplier = 0.8f;
    [Min(0.01f)] [SerializeField] private float groundCheckHeight = 0.12f;

    [Header("Gravity")]
    [Min(0f)] [SerializeField] private float gravityScale = 9f;
    [Min(1f)] [SerializeField] private float fallGravityMultiplier = 3.5f;

    public float MaximumRunSpeed => maximumRunSpeed;
    public float GroundAcceleration => groundAcceleration;
    public float GroundDeceleration => groundDeceleration;
    public float AirAcceleration => airAcceleration;
    public float JumpSpeed => jumpSpeed;
    public float CoyoteTime => coyoteTime;
    public float JumpBufferTime => jumpBufferTime;
    public float GroundCheckWidthMultiplier => groundCheckWidthMultiplier;
    public float GroundCheckHeight => groundCheckHeight;
    public float GravityScale => gravityScale;
    public float FallGravityMultiplier => fallGravityMultiplier;
}
