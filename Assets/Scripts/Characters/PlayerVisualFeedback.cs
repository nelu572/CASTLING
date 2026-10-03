using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public sealed class PlayerVisualFeedback : MonoBehaviour
{
    [SerializeField] private Transform visuals;
    [SerializeField, Range(0f, 0.2f)] private float landingSquash = 0.09f;
    [SerializeField, Range(0f, 0.2f)] private float playerCollisionSquash = 0.07f;
    [SerializeField, Min(0.01f)] private float recoveryTime = 0.16f;
    [SerializeField, Min(0f)] private float minimumPlayerImpactSpeed = 0.5f;
    [SerializeField, Range(0f, 0.1f)] private float stepSquash = 0.025f;
    [SerializeField, Min(0.01f)] private float stepDuration = 0.12f;

    private Vector3 restPosition;
    private Vector3 restScale;
    private float footYFromVisuals;
    private float landingTimeLeft;
    private float collisionTimeLeft;
    private float stepTimeLeft;
    private bool initialized;

    private void Awake()
    {
        if (visuals == null)
        {
            Debug.LogError("PlayerVisualFeedback requires a Visuals reference.", this);
            enabled = false;
            return;
        }

        restPosition = visuals.localPosition;
        restScale = visuals.localScale;

        PolygonCollider2D bodyCollider = GetComponent<PolygonCollider2D>();
        Vector2[] points = bodyCollider.points;
        float lowestPoint = points[0].y;
        for (int index = 1; index < points.Length; index++)
        {
            lowestPoint = Mathf.Min(lowestPoint, points[index].y);
        }

        footYFromVisuals = lowestPoint + bodyCollider.offset.y - restPosition.y;
        initialized = true;
    }

    private void OnDisable()
    {
        landingTimeLeft = 0f;
        collisionTimeLeft = 0f;
        stepTimeLeft = 0f;
        if (!initialized || visuals == null)
        {
            return;
        }

        visuals.localPosition = restPosition;
        visuals.localScale = restScale;
    }

    public void PlayLanding()
    {
        landingTimeLeft = recoveryTime;
    }

    public void PlayStep()
    {
        stepTimeLeft = stepDuration;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer != LayerMask.NameToLayer(Layers.Player)
            || collision.relativeVelocity.sqrMagnitude < minimumPlayerImpactSpeed * minimumPlayerImpactSpeed)
        {
            return;
        }

        for (int index = 0; index < collision.contactCount; index++)
        {
            Vector2 normal = collision.GetContact(index).normal;
            if (Mathf.Abs(normal.y) > 0.5f)
            {
                PlayLanding();
                return;
            }

            if (Mathf.Abs(normal.x) > 0.5f)
            {
                collisionTimeLeft = recoveryTime;
                return;
            }
        }
    }

    private void LateUpdate()
    {
        landingTimeLeft = Mathf.Max(0f, landingTimeLeft - Time.deltaTime);
        collisionTimeLeft = Mathf.Max(0f, collisionTimeLeft - Time.deltaTime);
        stepTimeLeft = Mathf.Max(0f, stepTimeLeft - Time.deltaTime);

        float landingAmount = landingTimeLeft / recoveryTime;
        float collisionAmount = collisionTimeLeft / recoveryTime;
        float stepAmount = stepTimeLeft / stepDuration;
        float horizontalScale = 1f + landingSquash * 0.55f * landingAmount
            - playerCollisionSquash * collisionAmount + stepSquash * 0.5f * stepAmount;
        float verticalScale = 1f - landingSquash * landingAmount
            + playerCollisionSquash * 0.35f * collisionAmount - stepSquash * stepAmount;

        visuals.localScale = new Vector3(
            restScale.x * horizontalScale,
            restScale.y * verticalScale,
            restScale.z);

        Vector3 position = restPosition;
        position.y += (1f - verticalScale) * footYFromVisuals * restScale.y;
        visuals.localPosition = position;
    }
}
