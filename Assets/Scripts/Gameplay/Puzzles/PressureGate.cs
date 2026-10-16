using UnityEngine;

[DisallowMultipleComponent]
public sealed class PressureGate : MonoBehaviour
{
    [SerializeField] private PressurePlateSwitch[] switches;
    [SerializeField] private BoxCollider2D blockingCollider;
    [SerializeField] private Transform shutter;
    [SerializeField] private SpriteRenderer indicator;
    [SerializeField] private Vector3 openOffset = new(0f, 8f, 0f);
    [SerializeField, Min(0.01f)] private float visualSpeed = 32f;
    [SerializeField] private Color closedColor = new(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color openColor = Color.white;

    private readonly Collider2D[] overlaps = new Collider2D[8];
    private ContactFilter2D occupantFilter;
    private Vector3 closedShutterPosition;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        occupantFilter.SetLayerMask(LayerMask.GetMask(Layers.Player, Layers.Environment));
        occupantFilter.useTriggers = false;
        if (switches == null || switches.Length == 0 || blockingCollider == null ||
            blockingCollider.isTrigger || shutter == null || indicator == null ||
            LayerMask.GetMask(Layers.Player) == 0 || LayerMask.GetMask(Layers.Environment) == 0)
        {
            Debug.LogError("Pressure gate switch, collision, or visual references are incomplete.", this);
            enabled = false;
            return;
        }

        foreach (PressurePlateSwitch plate in switches)
        {
            if (plate == null)
            {
                Debug.LogError("Pressure gate has an unassigned switch.", this);
                enabled = false;
                return;
            }
        }

        closedShutterPosition = shutter.localPosition;
        SetOpen(false);
    }

    private void FixedUpdate()
    {
        bool requestedOpen = false;
        foreach (PressurePlateSwitch plate in switches)
        {
            requestedOpen |= plate.isActiveAndEnabled && plate.IsPressed;
        }

        // Wait for bodies to clear the doorway before restoring a solid collider.
        SetOpen(requestedOpen || (IsOpen && PassageOccupied()));
    }

    private bool PassageOccupied()
    {
        Transform shape = blockingCollider.transform;
        Vector3 scale = shape.lossyScale;
        Vector2 size = new(blockingCollider.size.x * Mathf.Abs(scale.x),
                           blockingCollider.size.y * Mathf.Abs(scale.y));
        Vector2 center = shape.TransformPoint(blockingCollider.offset);
        int count = Physics2D.OverlapBox(center, size, shape.eulerAngles.z, occupantFilter, overlaps);
        for (int i = 0; i < count; i++)
        {
            Collider2D occupant = overlaps[i];
            if (occupant.gameObject.layer == LayerMask.NameToLayer(Layers.Player)) return true;

            Rigidbody2D body = occupant.attachedRigidbody;
            if (body != null && body.TryGetComponent(out PushablePlatform platform) &&
                platform.isActiveAndEnabled) return true;
        }

        return false;
    }

    private void SetOpen(bool open)
    {
        IsOpen = open;
        blockingCollider.enabled = !open;
        indicator.color = open ? openColor : closedColor;
    }

    private void Update()
    {
        Vector3 target = closedShutterPosition + (IsOpen ? openOffset : Vector3.zero);
        shutter.localPosition = Vector3.MoveTowards(shutter.localPosition, target, visualSpeed * Time.deltaTime);
    }
}
