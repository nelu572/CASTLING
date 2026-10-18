using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CooperativeCheckpoint : MonoBehaviour
{
    [SerializeField] private VoidRespawnZone recovery;
    [SerializeField] private RoomEntry entry;
    [SerializeField, Min(1)] private int order = 1;
    [SerializeField] private GameObject activatedVisual;

    private BoxCollider2D area;
    private PlayerMovement kingMovement;
    private PlayerMovement rookMovement;

    public bool Activated { get; private set; }

    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        if (recovery == null || entry == null || !entry.IsConfigured || !area.isTrigger ||
            recovery.King == null || recovery.Rook == null)
        {
            Debug.LogError("Cooperative checkpoint references or trigger are incomplete.", this);
            enabled = false;
            return;
        }

        kingMovement = recovery.King.GetComponent<PlayerMovement>();
        rookMovement = recovery.Rook.GetComponent<PlayerMovement>();
    }

    private void FixedUpdate()
    {
        if (Activated || recovery.IsRespawning || Time.timeScale <= 0f ||
            !area.OverlapPoint(recovery.King.position) || !area.OverlapPoint(recovery.Rook.position) ||
            !kingMovement.HasGroundContact || !rookMovement.HasGroundContact ||
            Mathf.Abs(recovery.King.linearVelocity.y) > 0.3f || Mathf.Abs(recovery.Rook.linearVelocity.y) > 0.3f)
        {
            return;
        }

        if (recovery.SetCheckpoint(entry, order))
        {
            Activated = true;
            if (activatedVisual != null) activatedVisual.SetActive(true);
        }
    }
}
