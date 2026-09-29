using System.Collections.Generic;
using System.Globalization;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class SceneFramingPreview
{
    private const string MenuPath = "Tools/CASTLING/Live Camera & Parallax Preview";
    private const string PreferenceKey = "CASTLING.LiveCameraParallaxPreview";

    private struct Layer
    {
        public Transform Transform;
        public Vector3 WorldPosition;
        public Vector3 LocalPosition;
        public Vector3 CameraOrigin;
        public BackgroundParallax Parallax;
    }

    private static readonly List<Layer> Layers = new List<Layer>();
    private static AnimationModeDriver driver;
    private static Scene previewScene;
    private static Camera camera;
    private static CinemachineBrain brain;
    private static CinemachineTargetGroup group;
    private static CinemachineGroupFraming framing;
    private static CinemachinePositionComposer composer;
    private static CinemachineConfiner2D confiner;
    private static Vector3 cameraOrigin;

    static SceneFramingPreview()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        bool enabled = !EditorPrefs.GetBool(PreferenceKey, true);
        EditorPrefs.SetBool(PreferenceKey, enabled);
        if (!enabled) Stop();
        else Update();
        Menu.SetChecked(MenuPath, enabled);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PreferenceKey, true));
        return true;
    }

    private static void Update()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!EditorPrefs.GetBool(PreferenceKey, true) ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            !scene.IsValid() || scene.name != SceneNames.Development.GameplaySandbox)
        {
            Stop();
            return;
        }

        if (driver == null)
        {
            if (AnimationMode.InAnimationMode() || !TryStart(scene)) return;
        }

        if (!AnimationMode.InAnimationMode(driver) || scene != previewScene ||
            camera == null || group == null || confiner == null)
        {
            Stop();
            return;
        }

        Apply();
    }

    private static bool TryStart(Scene scene)
    {
        camera = Camera.main;
        brain = camera != null ? camera.GetComponent<CinemachineBrain>() : null;
        CameraFramingFloor floor = Object.FindFirstObjectByType<CameraFramingFloor>();
        group = floor != null ? floor.GetComponent<CinemachineTargetGroup>() : null;
        CinemachineCamera virtualCamera = Object.FindFirstObjectByType<CinemachineCamera>();
        framing = virtualCamera != null ? virtualCamera.GetComponent<CinemachineGroupFraming>() : null;
        composer = virtualCamera != null ? virtualCamera.GetComponent<CinemachinePositionComposer>() : null;
        confiner = virtualCamera != null ? virtualCamera.GetComponent<CinemachineConfiner2D>() : null;

        if (camera == null || brain == null || group == null || framing == null ||
            composer == null || confiner == null || confiner.BoundingShape2D == null ||
            !camera.orthographic || virtualCamera.Follow != group.transform ||
            camera.gameObject.scene != scene)
        {
            ClearReferences();
            return false;
        }

        if (!TryCalculateFrame(out cameraOrigin, out _))
        {
            ClearReferences();
            return false;
        }

        Layers.Clear();
        foreach (BackgroundParallax parallax in Object.FindObjectsByType<BackgroundParallax>(FindObjectsSortMode.None))
        {
            if (!parallax.isActiveAndEnabled || parallax.gameObject.scene != scene) continue;
            Transform originReference = new SerializedObject(parallax)
                .FindProperty("cameraOriginReference").objectReferenceValue as Transform;
            Layers.Add(new Layer
            {
                Transform = parallax.transform,
                WorldPosition = parallax.transform.position,
                LocalPosition = parallax.transform.localPosition,
                CameraOrigin = originReference != null ? originReference.position : cameraOrigin,
                Parallax = parallax
            });
        }

        previewScene = scene;
        driver = ScriptableObject.CreateInstance<AnimationModeDriver>();
        driver.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            AnimationMode.StartAnimationMode(driver);
            RegisterAxis(camera.transform, "m_LocalPosition.x", camera.transform.localPosition.x);
            RegisterAxis(camera.transform, "m_LocalPosition.y", camera.transform.localPosition.y);
            Register(camera, "orthographic size", camera.orthographicSize);
            Register(brain, "m_Enabled", brain.enabled ? 1f : 0f);
            foreach (Layer layer in Layers)
            {
                RegisterAxis(layer.Transform, "m_LocalPosition.x", layer.LocalPosition.x);
                RegisterAxis(layer.Transform, "m_LocalPosition.y", layer.LocalPosition.y);
            }
            brain.enabled = false;
            return true;
        }
        catch
        {
            Stop();
            throw;
        }
    }

    private static void Apply()
    {
        if (!TryCalculateFrame(out Vector3 position, out float size)) return;

        bool changed = (camera.transform.position - position).sqrMagnitude > 0.000001f ||
                       Mathf.Abs(camera.orthographicSize - size) > 0.0001f;
        if (changed)
        {
            camera.transform.position = position;
            camera.orthographicSize = size;
        }

        foreach (Layer layer in Layers)
        {
            if (layer.Transform == null || layer.Parallax == null) continue;
            Vector3 delta = position - layer.CameraOrigin;
            SerializedObject properties = new SerializedObject(layer.Parallax);
            float horizontalRatio = properties.FindProperty("horizontalScrollRatio").floatValue;
            float verticalRatio = properties.FindProperty("verticalScrollRatio").floatValue;
            Vector3 layerPosition = layer.WorldPosition + new Vector3(
                delta.x * (1f - horizontalRatio),
                delta.y * (1f - verticalRatio), 0f);
            if ((layer.Transform.position - layerPosition).sqrMagnitude <= 0.000001f) continue;
            layer.Transform.position = layerPosition;
            changed = true;
        }

        if (changed)
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    private static bool TryCalculateFrame(out Vector3 position, out float size)
    {
        position = default;
        size = 0f;
        if (camera == null || group == null || framing == null || confiner == null ||
            confiner.BoundingShape2D == null) return false;

        Bounds bounds = confiner.BoundingShape2D.bounds;
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float minimumY = float.PositiveInfinity;
        float maximumY = float.NegativeInfinity;
        Vector2 weightedCenter = Vector2.zero;
        float totalWeight = 0f;

        foreach (CinemachineTargetGroup.Target target in group.Targets)
        {
            if (target.Object == null || target.Weight <= 0f ||
                target.Object.position.y < bounds.min.y) continue;

            Vector3 point = target.Object.position;
            float radius = Mathf.Max(0f, target.Radius);
            minimumX = Mathf.Min(minimumX, point.x - radius);
            maximumX = Mathf.Max(maximumX, point.x + radius);
            minimumY = Mathf.Min(minimumY, point.y - radius);
            maximumY = Mathf.Max(maximumY, point.y + radius);
            weightedCenter += new Vector2(point.x, point.y) * target.Weight;
            totalWeight += target.Weight;
        }

        if (totalWeight <= 0f) return false;
        weightedCenter /= totalWeight;

        float aspect = Mathf.Max(0.01f, camera.aspect);
        float framingSize = Mathf.Max(0.01f, framing.FramingSize);
        float requiredSize = Mathf.Max(
            (maximumX - minimumX) / (2f * aspect * framingSize),
            (maximumY - minimumY) / (2f * framingSize));
        float maximumFittingSize = Mathf.Min(bounds.size.x / (2f * aspect), bounds.size.y * 0.5f);
        float minimumSize = Mathf.Min(framing.OrthoSizeRange.x, maximumFittingSize);
        float maximumSize = Mathf.Min(framing.OrthoSizeRange.y, maximumFittingSize);
        size = Mathf.Clamp(requiredSize, minimumSize, maximumSize);

        Vector2 screenPosition = composer.Composition.ScreenPosition;
        Vector2 center = weightedCenter + framing.CenterOffset;
        float x = center.x + screenPosition.x * 2f * size * aspect;
        float y = center.y + screenPosition.y * 2f * size;
        float halfWidth = size * aspect;
        x = Mathf.Clamp(x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        y = Mathf.Clamp(y, bounds.min.y + size, bounds.max.y - size);
        position = new Vector3(x, y, camera.transform.position.z);
        return true;
    }

    private static void RegisterAxis(Transform transform, string path, float originalValue)
    {
        Register(transform, path, originalValue);
    }

    private static void Register(Object target, string path, float originalValue)
    {
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve("", target.GetType(), path);
        PropertyModification modification = new PropertyModification
        {
            target = target,
            propertyPath = path,
            value = originalValue.ToString("R", CultureInfo.InvariantCulture)
        };
        AnimationMode.AddPropertyModification(binding, modification, false);
    }

    private static void Stop()
    {
        if (driver != null)
        {
            if (AnimationMode.InAnimationMode(driver)) AnimationMode.StopAnimationMode(driver);
            Object.DestroyImmediate(driver);
            driver = null;
            SceneView.RepaintAll();
        }
        Layers.Clear();
        ClearReferences();
    }

    private static void ClearReferences()
    {
        camera = null;
        brain = null;
        group = null;
        framing = null;
        composer = null;
        confiner = null;
        previewScene = default;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode) Stop();
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next)
    {
        Stop();
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (scene == previewScene) Stop();
    }
}
