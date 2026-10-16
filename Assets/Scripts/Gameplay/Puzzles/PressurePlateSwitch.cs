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
    private ContactFilter2D occupantFilter;
    private Vector3 releasedSurfacePosition;

    public bool IsPressed { get; private set; }

    private void Awake()
    {
        sensor = GetComponent<BoxCollider2D>();
        occupantFilter.SetLayerMask(LayerMask.GetMask(Layers.Player, Layers.Environment));
        occupantFilter.useTriggers = false;
        if (!sensor.isTrigger || surface == null || indicator == null ||
            LayerMask.GetMask(Layers.Player) == 0 || LayerMask.GetMask(Layers.Environment) == 0)
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
            int count = Physics2D.OverlapBox(area.center, area.size, 0f, occupantFilter, overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D occupant = overlaps[i];
                Rigidbody2D body = occupant.attachedRigidbody;
                if (body == null) continue;

                bool isPlayer = occupant.gameObject.layer == LayerMask.NameToLayer(Layers.Player) &&
                                body.CompareTag(Tags.Player);
                bool isPlatform = body.TryGetComponent(out PushablePlatform platform) &&
                                  platform.isActiveAndEnabled;
                // Touching the side or jumping over the plate does not count as standing on it.
                float feet = occupant.bounds.min.y;
                if ((isPlayer || isPlatform) &&
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
