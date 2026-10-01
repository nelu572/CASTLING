using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerInput))]
public sealed class CastlingAbility : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rook;

    private Rigidbody2D king;

    private void Awake()
    {
        king = GetComponent<Rigidbody2D>();
        if (rook == null || rook == king)
        {
            Debug.LogError("CASTLING needs a separate rook body.", this);
            enabled = false;
        }
    }

    public void OnCastling(InputValue inputValue)
    {
        if (inputValue.isPressed && enabled && Time.timeScale > 0f)
        {
            ExchangePositions();
        }
    }

    private void ExchangePositions()
    {
        Vector2 kingPosition = king.position;
        Vector2 rookPosition = rook.position;
        king.position = rookPosition;
        rook.position = kingPosition;
        king.transform.position = new Vector3(rookPosition.x, rookPosition.y, king.transform.position.z);
        rook.transform.position = new Vector3(kingPosition.x, kingPosition.y, rook.transform.position.z);
        Physics2D.SyncTransforms();
    }
}
