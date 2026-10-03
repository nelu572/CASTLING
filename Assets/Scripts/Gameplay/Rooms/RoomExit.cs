using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RoomExit : MonoBehaviour
{
    [SerializeField] private RoomEntry destination;

    private BoxCollider2D area;

    public RoomEntry Destination => destination;

    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
    }

    public bool ContainsBoth(Transform king, Transform rook)
    {
        return isActiveAndEnabled && area != null && area.enabled && area.isTrigger &&
               king != null && rook != null &&
               area.OverlapPoint(king.position) && area.OverlapPoint(rook.position);
    }
}
