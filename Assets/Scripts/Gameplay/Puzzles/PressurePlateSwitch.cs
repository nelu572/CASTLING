using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PressurePlateSwitch : MonoBehaviour
{
    [SerializeField] private Transform surface;
    [SerializeField] private SpriteRenderer indicator;
    [SerializeField] private Color releasedColor = new(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color pressedColor = Color.white;

    private readonly Collider2D[] overlaps = new Collider2D[8];
    private BoxCollider2D sensor;
    private ContactFilter2D playerFilter;
    private Vector3 releasedSurfacePosition;

    public bool IsPressed { get; private set; }

    private void Awake()
    {
        sensor = GetComponent<BoxCollider2D>();
        playerFilter.SetLayerMask(LayerMask.GetMask(Layers.Player));
        playerFilter.useTriggers = false;
        if (!sensor.isTrigger || surface == null || indicator == null || playerFilter.layerMask == 0)
        {
            Debug.LogError("Pressure plate sensor or visual references are incomplete.", this);
            enabled = false;
            return;
        }

        releasedSurfacePosition = surface.localPosition;
        SetPressed(false);
    }

    private void FixedUpdate()
    {
        bool pressed = false;
        if (sensor.enabled)
        {
            Bounds area = sensor.bounds;
            int count = Physics2D.OverlapBox(area.center, area.size, 0f, playerFilter, overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D player = overlaps[i];
                Rigidbody2D body = player.attachedRigidbody;
                // Touching the side or jumping over the plate does not count as standing on it.
                float feet = player.bounds.min.y;
                if (body != null && body.CompareTag(Tags.Player) &&
                    feet >= area.min.y - 0.08f && feet <= area.max.y && body.linearVelocity.y <= 0.3f)
                {
                    pressed = true;
                    break;
                }
            }
        }

        SetPressed(pressed);
    }

    private void SetPressed(bool pressed)
    {
        IsPressed = pressed;
        if (surface != null)
        {
            surface.localPosition = releasedSurfacePosition + (pressed ? Vector3.down * 0.08f : Vector3.zero);
        }

        if (indicator != null) indicator.color = pressed ? pressedColor : releasedColor;
    }

    private void OnDisable() => SetPressed(false);
}
