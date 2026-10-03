using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerEyes : MonoBehaviour
{
    [Header("Eye References")]
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;

    [Header("Look Animation")]
    [Min(0f)] [SerializeField] private float largerEyeShift = 0.125f;
    [Min(0f)] [SerializeField] private float smallerEyeShift = 0.075f;
    [Min(0f)] [SerializeField] private float shiftSpeed = 1.5f;

    private Vector3 leftEyeBaseLocalPosition;
    private Vector3 rightEyeBaseLocalPosition;
    private Vector3 leftEyeTargetLocalPosition;
    private Vector3 rightEyeTargetLocalPosition;
    private bool isAnimating;
    private bool isInitialized;

    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        if (!isAnimating)
        {
            return;
        }

        leftEye.localPosition = Vector3.MoveTowards(
            leftEye.localPosition,
            leftEyeTargetLocalPosition,
            shiftSpeed * Time.deltaTime);
        rightEye.localPosition = Vector3.MoveTowards(
            rightEye.localPosition,
            rightEyeTargetLocalPosition,
            shiftSpeed * Time.deltaTime);

        isAnimating = leftEye.localPosition != leftEyeTargetLocalPosition
            || rightEye.localPosition != rightEyeTargetLocalPosition;
    }

    public void SetLookDirection(float horizontalDirection)
    {
        if (!Initialize())
        {
            return;
        }

        if (Mathf.Approximately(horizontalDirection, 0f))
        {
            leftEyeTargetLocalPosition = leftEyeBaseLocalPosition;
            rightEyeTargetLocalPosition = rightEyeBaseLocalPosition;
        }
        else if (horizontalDirection > 0f)
        {
            leftEyeTargetLocalPosition = OffsetX(leftEyeBaseLocalPosition, largerEyeShift);
            rightEyeTargetLocalPosition = OffsetX(rightEyeBaseLocalPosition, smallerEyeShift);
        }
        else
        {
            leftEyeTargetLocalPosition = OffsetX(leftEyeBaseLocalPosition, -smallerEyeShift);
            rightEyeTargetLocalPosition = OffsetX(rightEyeBaseLocalPosition, -largerEyeShift);
        }

        isAnimating = true;
    }

    private bool Initialize()
    {
        if (isInitialized)
        {
            return true;
        }

        if (leftEye == null || rightEye == null)
        {
            enabled = false;
            return false;
        }

        leftEyeBaseLocalPosition = leftEye.localPosition;
        rightEyeBaseLocalPosition = rightEye.localPosition;
        leftEyeTargetLocalPosition = leftEyeBaseLocalPosition;
        rightEyeTargetLocalPosition = rightEyeBaseLocalPosition;
        isInitialized = true;
        return true;
    }

    private static Vector3 OffsetX(Vector3 position, float amount)
    {
        position.x += amount;
        return position;
    }
}
