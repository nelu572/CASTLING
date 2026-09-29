using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
internal static class PlayerStartPositions
{
    private const string MenuRoot = "Tools/CASTLING/시작 위치/";
    private const string RestoreMenu = MenuRoot + "킹·룩 시작 위치로 복귀";
    private const string CoordinatesMenu = MenuRoot + "시작 좌표 보기";
    private const int OutlineWidthPixels = 24;
    private static readonly Color32 OutlineColor = new Color32(96, 185, 195, 255);
    private static readonly Dictionary<Texture2D, Color32[]> SourcePixels =
        new Dictionary<Texture2D, Color32[]>();
    private static readonly Dictionary<Sprite, Texture2D> OutlineTextures =
        new Dictionary<Sprite, Texture2D>();
    private static readonly Dictionary<Sprite, Sprite> OutlineSprites =
        new Dictionary<Sprite, Sprite>();
    private static SpriteRenderer kingPreview;
    private static SpriteRenderer rookPreview;

    static PlayerStartPositions()
    {
        EditorApplication.update += UpdatePreviews;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        SceneView.duringSceneGui += DrawStartHandles;
        AssemblyReloadEvents.beforeAssemblyReload += ClearPreviews;
    }

    private static void ClearPreviews()
    {
        if (kingPreview != null) Object.DestroyImmediate(kingPreview.gameObject);
        if (rookPreview != null) Object.DestroyImmediate(rookPreview.gameObject);
        kingPreview = null;
        rookPreview = null;
        foreach (Sprite sprite in OutlineSprites.Values)
            if (sprite != null) Object.DestroyImmediate(sprite);
        OutlineSprites.Clear();
        foreach (Texture2D texture in OutlineTextures.Values)
            if (texture != null) Object.DestroyImmediate(texture);
        OutlineTextures.Clear();
        SourcePixels.Clear();
    }

    private static void UpdatePreviews()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            !TryGetTransforms(out Transform king, out Transform rook,
                out Transform kingStart, out Transform rookStart))
        {
            SetPreviewsEnabled(false);
            return;
        }

        UpdatePreview(ref kingPreview, king, kingStart, "KingStartPreview");
        UpdatePreview(ref rookPreview, rook, rookStart, "RookStartPreview");
    }

    private static void UpdatePreview(ref SpriteRenderer preview, Transform player,
        Transform start, string name)
    {
        SpriteRenderer body = null;
        foreach (SpriteRenderer candidate in player.GetComponentsInChildren<SpriteRenderer>())
        {
            if (candidate.name != "Body") continue;
            body = candidate;
            break;
        }
        if (body == null || body.sprite == null)
        {
            if (preview != null) preview.enabled = false;
            return;
        }

        if (preview == null)
        {
            var previewObject = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            preview = previewObject.AddComponent<SpriteRenderer>();
            preview.color = Color.white;
            preview.enabled = false;
        }

        Sprite outline = GetOutlineSprite(body.sprite);
        if (preview.sprite != outline) preview.sprite = outline;
        if (preview.sharedMaterial != body.sharedMaterial) preview.sharedMaterial = body.sharedMaterial;
        if (preview.sortingLayerID != body.sortingLayerID) preview.sortingLayerID = body.sortingLayerID;
        if (preview.sortingOrder != body.sortingOrder - 1) preview.sortingOrder = body.sortingOrder - 1;
        if (preview.flipX != body.flipX) preview.flipX = body.flipX;
        if (preview.flipY != body.flipY) preview.flipY = body.flipY;

        Vector3 position = start.position + body.transform.position - player.position;
        Quaternion rotation = body.transform.rotation;
        if (preview.transform.position != position || preview.transform.rotation != rotation)
            preview.transform.SetPositionAndRotation(position, rotation);
        if (preview.transform.localScale != body.transform.lossyScale)
            preview.transform.localScale = body.transform.lossyScale;
    }

    private static Sprite GetOutlineSprite(Sprite source)
    {
        if (OutlineSprites.TryGetValue(source, out Sprite outline) && outline != null)
            return outline;

        Rect rect = source.textureRect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        int originX = Mathf.RoundToInt(rect.x);
        int originY = Mathf.RoundToInt(rect.y);
        int atlasWidth = source.texture.width;
        Color32[] sourcePixels = GetSourcePixels(source.texture);
        var outlinePixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte alpha = sourcePixels[(originY + y) * atlasWidth + originX + x].a;
                if (alpha == 0) continue;

                bool edge = false;
                for (int dy = -1; dy <= 1 && !edge; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx * OutlineWidthPixels;
                        int ny = y + dy * OutlineWidthPixels;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height ||
                            sourcePixels[(originY + ny) * atlasWidth + originX + nx].a < 128)
                        {
                            edge = true;
                            break;
                        }
                    }
                }
                if (edge) outlinePixels[y * width + x] =
                    new Color32(OutlineColor.r, OutlineColor.g, OutlineColor.b, alpha);
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(outlinePixels);
        texture.Apply(false, true);
        texture.filterMode = source.texture.filterMode;
        texture.hideFlags = HideFlags.HideAndDontSave;
        Vector2 pivot = new Vector2(source.pivot.x / source.rect.width,
            source.pivot.y / source.rect.height);
        outline = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, source.pixelsPerUnit,
            0, SpriteMeshType.FullRect);
        outline.hideFlags = HideFlags.HideAndDontSave;
        OutlineTextures.Add(source, texture);
        OutlineSprites.Add(source, outline);
        return outline;
    }

    private static Color32[] GetSourcePixels(Texture2D source)
    {
        if (SourcePixels.TryGetValue(source, out Color32[] pixels)) return pixels;

        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D readable = null;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            pixels = readable.GetPixels32();
            SourcePixels.Add(source, pixels);
            return pixels;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            if (readable != null) Object.DestroyImmediate(readable);
        }
    }

    private static void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        SetPreviewsEnabled(camera.cameraType == CameraType.SceneView &&
            !EditorApplication.isPlayingOrWillChangePlaymode);
    }

    private static void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        SetPreviewsEnabled(false);
    }

    private static void SetPreviewsEnabled(bool enabled)
    {
        if (kingPreview != null) kingPreview.enabled = enabled;
        if (rookPreview != null) rookPreview.enabled = enabled;
    }

    private static void DrawStartHandles(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            !TryGetTransforms(out _, out _, out Transform kingStart, out Transform rookStart))
            return;

        DrawStartHandle(kingStart);
        DrawStartHandle(rookStart);
    }

    private static void DrawStartHandle(Transform marker)
    {
        Vector3 position = marker.position;
        float size = HandleUtility.GetHandleSize(position) * 0.15f;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(position, size, Vector3.zero, GhostHandleCap);
        if (EditorGUI.EndChangeCheck())
            SetStartPosition(marker, new Vector3(moved.x, moved.y, position.z));
    }

    private static void GhostHandleCap(int controlId, Vector3 position, Quaternion rotation,
        float size, EventType eventType)
    {
        if (eventType == EventType.Layout)
            HandleUtility.AddControl(controlId, HandleUtility.DistanceToCircle(position, size));
    }

    [MenuItem(CoordinatesMenu)]
    private static void ShowCoordinates()
    {
        EditorWindow.GetWindow<StartPositionCoordinatesWindow>("시작 좌표");
        FocusSceneView();
    }

    internal static void FocusSceneView()
    {
        if (!TryGetTransforms(out _, out _, out Transform kingStart, out Transform rookStart))
            return;

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) view = EditorWindow.GetWindow<SceneView>();

        Vector3 center = (kingStart.position + rookStart.position) * 0.5f;
        float aspect = view.camera != null ? Mathf.Max(1f, view.camera.aspect) : 2f;
        float horizontalSize = (Mathf.Abs(kingStart.position.x - rookStart.position.x) + 6f) /
            (2f * aspect);
        float verticalSize = (Mathf.Abs(kingStart.position.y - rookStart.position.y) + 6f) * 0.5f;
        float size = Mathf.Max(8f, horizontalSize, verticalSize);

        view.LookAt(center, view.rotation, size, view.orthographic, true);
        view.Focus();
        view.Repaint();
    }

    internal static void SetStartPosition(Transform marker, Vector3 position)
    {
        if (marker.position == position) return;

        Undo.RecordObject(marker, "Move player start position");
        marker.position = position;
        EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
        SceneView.RepaintAll();
    }

    [MenuItem(RestoreMenu)]
    private static void RestoreStartPositions()
    {
        if (!TryGetTransforms(out Transform king, out Transform rook,
                out Transform kingStart, out Transform rookStart)) return;

        Undo.IncrementCurrentGroup();
        Undo.RecordObjects(new Object[] { king, rook }, "Restore player start positions");
        king.position = kingStart.position;
        rook.position = rookStart.position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(king);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rook);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem(RestoreMenu, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode &&
               TryGetTransforms(out _, out _, out _, out _);
    }

    internal static bool TryGetTransforms(out Transform king, out Transform rook,
        out Transform kingStart, out Transform rookStart)
    {
        king = null;
        rook = null;
        kingStart = null;
        rookStart = null;

        if (EditorSceneManager.GetActiveScene().name != SceneNames.Development.GameplaySandbox)
            return false;

        GameObject kingObject = GameObject.Find("King");
        GameObject rookObject = GameObject.Find("Rook");
        GameObject markers = GameObject.Find("PlayerStartPositions");
        if (kingObject == null || rookObject == null || markers == null) return false;

        kingStart = markers.transform.Find("KingStart");
        rookStart = markers.transform.Find("RookStart");
        if (kingStart == null || rookStart == null) return false;

        king = kingObject.transform;
        rook = rookObject.transform;
        return true;
    }
}

internal sealed class StartPositionCoordinatesWindow : EditorWindow
{
    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        if (!PlayerStartPositions.TryGetTransforms(out _, out _,
                out Transform kingStart, out Transform rookStart))
        {
            EditorGUILayout.HelpBox("Dev_Gameplay 씬에서 시작 좌표를 볼 수 있습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("시작 좌표 (X, Y)", EditorStyles.boldLabel);
        DrawPositionField("킹", kingStart);
        DrawPositionField("룩", rookStart);
        if (GUILayout.Button("시작 위치로 씬 뷰 이동"))
            PlayerStartPositions.FocusSceneView();
    }

    private static void DrawPositionField(string label, Transform marker)
    {
        Vector3 position = marker.position;
        EditorGUI.BeginChangeCheck();
        Vector2 next = EditorGUILayout.Vector2Field(label, new Vector2(position.x, position.y));
        if (EditorGUI.EndChangeCheck())
            PlayerStartPositions.SetStartPosition(marker, new Vector3(next.x, next.y, position.z));
    }
}
