using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomArea : MonoBehaviour
{
    [SerializeField] private CinemachineCamera roomCamera;
    [SerializeField] private Collider2D cameraBounds;
    [SerializeField] private GameObject background;

    private RoomExit[] exits;

    public CinemachineCamera Camera => roomCamera;
    public Collider2D CameraBounds => cameraBounds;
    public GameObject Background => background;
    public RoomExit[] Exits => exits ??= GetComponentsInChildren<RoomExit>(true);
}
