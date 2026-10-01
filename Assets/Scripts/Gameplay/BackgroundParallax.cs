using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BackgroundParallax : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform cameraOriginReference;
    [Tooltip("Only this child moves for parallax; the layer transform keeps its authored placement.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Screen scroll relative to terrain: 0 stays on screen, 1 moves with terrain.")]
    [SerializeField, Range(0f, 1f)] private float horizontalScrollRatio = 0.35f;
    [SerializeField, Range(0f, 1f)] private float verticalScrollRatio = 0.35f;

    private Transform activeVisualRoot;
    private Vector3 initialLocalPosition;
    private Vector3 cameraOrigin;
    private bool hasCameraOrigin;

    public Transform ParallaxTransform => visualRoot != null ? visualRoot : transform;
    // Authored placement belongs to this layer; the separate visual only stores a transient offset.
    public Vector3 RestLocalPosition => ParallaxTransform == transform ? transform.localPosition : Vector3.zero;

    private void OnEnable()
    {
        activeVisualRoot = ParallaxTransform;
        initialLocalPosition = RestLocalPosition;
        activeVisualRoot.localPosition = initialLocalPosition;
        hasCameraOrigin = cameraOriginReference != null;
        if (hasCameraOrigin)
        {
            cameraOrigin = cameraOriginReference.position;
            // Apply the starting offset before the first Cinemachine update can render the layer.
            if (targetCamera != null)
            {
                ApplyParallax(targetCamera.transform.position);
            }
        }
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        if (activeVisualRoot != null)
        {
            activeVisualRoot.localPosition = initialLocalPosition;
        }
        activeVisualRoot = null;
        hasCameraOrigin = false;
    }

    private void OnCameraUpdated(CinemachineBrain brain)
    {
        if (!Application.isPlaying || activeVisualRoot == null ||
            targetCamera == null || brain.OutputCamera != targetCamera)
        {
            return;
        }

        Vector3 cameraPosition = targetCamera.transform.position;
        if (!hasCameraOrigin)
        {
            // Anchor after Cinemachine has applied the starting camera bounds.
            cameraOrigin = cameraPosition;
            hasCameraOrigin = true;
        }

        ApplyParallax(cameraPosition);
    }

    private void ApplyParallax(Vector3 cameraPosition)
    {
        Vector3 delta = cameraPosition - cameraOrigin;
        Vector3 initialWorldPosition = activeVisualRoot.parent != null
            ? activeVisualRoot.parent.TransformPoint(initialLocalPosition)
            : initialLocalPosition;
        activeVisualRoot.position = initialWorldPosition + new Vector3(
            delta.x * (1f - horizontalScrollRatio),
            delta.y * (1f - verticalScrollRatio),
            0f);
    }
}
