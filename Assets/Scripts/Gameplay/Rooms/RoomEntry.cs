using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomEntry : MonoBehaviour
{
    [SerializeField] private Transform kingPoint;
    [SerializeField] private Transform rookPoint;

    public RoomArea Room => GetComponentInParent<RoomArea>();
    public Transform KingPoint => kingPoint;
    public Transform RookPoint => rookPoint;
    public bool IsConfigured => Room != null && kingPoint != null && rookPoint != null;
}
