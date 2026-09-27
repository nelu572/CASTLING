using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BackgroundParallax : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [Tooltip("Screen scroll relative to terrain: 0 stays on screen, 1 moves with terrain.")]
    [SerializeField, Range(0f, 1f)] private float horizontalScrollRatio = 0.35f;
    [SerializeField, Range(0f, 1f)] private float verticalScrollRatio = 0.35f;

    private Vector3 initialLocalPosition;
    private Vector3 initialWorldPosition;
    private Vector3 cameraOrigin;
    private bool hasCameraOrigin;

    private void OnEnable()
    {
        initialLocalPosition = transform.localPosition;
        initialWorldPosition = transform.position;
        hasCameraOrigin = false;
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
    }

    private void OnDisable()
    {
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        transform.localPosition = initialLocalPosition;
        hasCameraOrigin = false;
    }

    private void OnCameraUpdated(CinemachineBrain brain)
    {
        if (!Application.isPlaying || targetCamera == null || brain.OutputCamera != targetCamera)
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

        Vector3 delta = cameraPosition - cameraOrigin;
        transform.position = initialWorldPosition + new Vector3(
            delta.x * (1f - horizontalScrollRatio),
            delta.y * (1f - verticalScrollRatio),
            0f);
    }
}
