using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraAspectRatio : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float targetAspect = 2f;

    private Camera outputCamera;

    private void OnEnable()
    {
        outputCamera = GetComponent<Camera>();
        UpdateViewport();
    }

    private void Update()
    {
        UpdateViewport();
    }

    private void UpdateViewport()
    {
        if (outputCamera == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        float aspect = Mathf.Max(0.01f, targetAspect);
        float screenAspect = (float)Screen.width / Screen.height;
        Rect viewport = new Rect(0f, 0f, 1f, 1f);
        if (screenAspect < aspect)
        {
            viewport.height = screenAspect / aspect;
            viewport.y = (1f - viewport.height) * 0.5f;
        }
        else if (screenAspect > aspect)
        {
            viewport.width = aspect / screenAspect;
            viewport.x = (1f - viewport.width) * 0.5f;
        }

        if (outputCamera.rect != viewport) outputCamera.rect = viewport;
        if (outputCamera.aspect != aspect) outputCamera.aspect = aspect;
    }

    private void OnGUI()
    {
        if (outputCamera == null || Event.current.type != EventType.Repaint) return;

        Rect viewport = outputCamera.rect;
        if (viewport.width >= 1f && viewport.height >= 1f) return;

        // Clear unused edges after a resize; URP can retain the previous frame outside the viewport.
        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;
        GUI.color = Color.white;
        GUI.depth = int.MaxValue;
        float horizontalMargin = viewport.x * Screen.width;
        float verticalMargin = viewport.y * Screen.height;
        if (horizontalMargin > 0f)
        {
            GUI.DrawTexture(new Rect(0f, 0f, horizontalMargin, Screen.height), Texture2D.blackTexture);
            GUI.DrawTexture(new Rect(Screen.width - horizontalMargin, 0f, horizontalMargin, Screen.height), Texture2D.blackTexture);
        }
        if (verticalMargin > 0f)
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, verticalMargin), Texture2D.blackTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - verticalMargin, Screen.width, verticalMargin), Texture2D.blackTexture);
        }
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void OnDisable()
    {
        if (outputCamera == null) return;
        outputCamera.rect = new Rect(0f, 0f, 1f, 1f);
        outputCamera.ResetAspect();
    }
}
