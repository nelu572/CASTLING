using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CastlingAbility : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rook;
    [SerializeField, Min(0.05f)] private float chargeDuration = 0.25f;

    private Rigidbody2D king;
    private PlayerInput playerInput;
    private bool castlingHeld;
    private double chargeStartedAt;
    private readonly CastlingPlacementQuery placementQuery = new();

    public Rigidbody2D King => king;
    public Rigidbody2D Rook => rook;
    public bool IsCharging { get; private set; }
    public bool IsAwaitingRelease => castlingHeld && !IsCharging;
    public float ChargeProgress { get; private set; }

    public event Action<CastlingResult> Attempted;

    private void Awake()
    {
        king = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        if (rook == null || rook == king)
        {
            Debug.LogError("CASTLING needs a separate rook body.", this);
            enabled = false;
        }
    }

    public void SetCastlingHeld(bool held, double inputTime)
    {
        if (!held)
        {
            bool complete = IsCharging && CanCharge() && inputTime - chargeStartedAt >= ChargeDuration;
            castlingHeld = false;
            CancelCharge();
            if (complete)
            {
                ExchangePositions();
            }
            return;
        }

        if (castlingHeld)
        {
            return;
        }

        // Consume the press even when interrupted: a fresh press starts the next attempt.
        castlingHeld = true;
        if (CanCharge())
        {
            chargeStartedAt = inputTime;
            ChargeProgress = 0f;
            IsCharging = true;
        }
    }

    private void Update()
    {
        if (!IsCharging)
        {
            return;
        }

        if (!CanCharge())
        {
            CancelCharge();
            return;
        }

        ChargeProgress = Mathf.Clamp01((float)((InputState.currentTime - chargeStartedAt) / ChargeDuration));
        if (ChargeProgress >= 1f)
        {
            CancelCharge();
            ExchangePositions();
        }
    }

    private float ChargeDuration => Mathf.Max(0.05f, chargeDuration);

    public void CancelCastling()
    {
        // Synthetic input cancellation can be followed by a held-key device sync.
        // Only an observed release clears the press latch.
        CancelCharge();
    }

    private bool CanCharge()
    {
        return isActiveAndEnabled && Time.timeScale > 0f
            && king != null && rook != null && king.simulated && rook.simulated
            && king.gameObject.activeInHierarchy && rook.gameObject.activeInHierarchy
            && (playerInput == null || (playerInput.isActiveAndEnabled && playerInput.inputIsActive));
    }

    private void CancelCharge()
    {
        IsCharging = false;
        ChargeProgress = 0f;
    }

    private void OnDisable()
    {
        CancelCharge();
        placementQuery.Dispose();
    }

    private void OnDestroy()
    {
        placementQuery.Dispose();
    }

    private void ExchangePositions()
    {
        CastlingResult result = placementQuery.Evaluate(king, rook);
        if (result.Succeeded)
        {
            king.position = result.KingDestination;
            rook.position = result.RookDestination;
            king.transform.position = new Vector3(
                result.KingDestination.x, result.KingDestination.y, king.transform.position.z);
            rook.transform.position = new Vector3(
                result.RookDestination.x, result.RookDestination.y, rook.transform.position.z);
            Physics2D.SyncTransforms();
            king.GetComponent<PlayerMovement>()?.ClearGroundedHistory();
            rook.GetComponent<PlayerMovement>()?.ClearGroundedHistory();
        }

        Attempted?.Invoke(result);
    }

    public readonly struct CastlingResult
    {
        public bool Succeeded => !KingBlocked && !RookBlocked;
        public Vector2 KingOrigin { get; }
        public Vector2 RookOrigin { get; }
        public Vector2 KingDestination { get; }
        public Vector2 RookDestination { get; }
        public bool KingBlocked { get; }
        public bool RookBlocked { get; }

        public CastlingResult(
            Vector2 kingOrigin, Vector2 rookOrigin,
            Vector2 kingDestination, Vector2 rookDestination,
            bool kingBlocked, bool rookBlocked)
        {
            KingOrigin = kingOrigin;
            RookOrigin = rookOrigin;
            KingDestination = kingDestination;
            RookDestination = rookDestination;
            KingBlocked = kingBlocked;
            RookBlocked = rookBlocked;
        }
    }
}
