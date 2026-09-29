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
    private static readonly Dictionary<Transform, SpriteRenderer> Previews =
        new Dictionary<Transform, SpriteRenderer>();
    private static readonly HashSet<Transform> VisibleMarkers = new HashSet<Transform>();
    private static readonly List<Transform> RemovedMarkers = new List<Transform>();

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
        foreach (SpriteRenderer preview in Previews.Values)
            if (preview != null) Object.DestroyImmediate(preview.gameObject);
        Previews.Clear();
        VisibleMarkers.Clear();
        RemovedMarkers.Clear();
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
            !TryGetTransforms(out Transform king, out Transform rook, out _, out _))
        {
            SetPreviewsEnabled(false);
            return;
        }

        VisibleMarkers.Clear();
        foreach (RoomEntry entry in Object.FindObjectsByType<RoomEntry>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (entry.gameObject.scene != EditorSceneManager.GetActiveScene() || !entry.IsConfigured)
                continue;

            UpdateEntryPreview(king, entry.KingPoint, entry.name + "_KingStartPreview");
            UpdateEntryPreview(rook, entry.RookPoint, entry.name + "_RookStartPreview");
        }

        RemovedMarkers.Clear();
        foreach (KeyValuePair<Transform, SpriteRenderer> pair in Previews)
            if (pair.Key == null || !VisibleMarkers.Contains(pair.Key))
                RemovedMarkers.Add(pair.Key);
        foreach (Transform marker in RemovedMarkers)
        {
            if (Previews[marker] != null) Object.DestroyImmediate(Previews[marker].gameObject);
            Previews.Remove(marker);
        }
    }

    private static void UpdateEntryPreview(Transform player, Transform marker, string name)
    {
        VisibleMarkers.Add(marker);
        Previews.TryGetValue(marker, out SpriteRenderer preview);
        UpdatePreview(ref preview, player, marker, name);
        if (preview != null) Previews[marker] = preview;
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
        foreach (SpriteRenderer preview in Previews.Values)
            if (preview != null) preview.enabled = enabled;
    }

    private static void DrawStartHandles(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().name != SceneNames.Development.GameplaySandbox)
            return;

        foreach (RoomEntry entry in Object.FindObjectsByType<RoomEntry>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (entry.gameObject.scene != EditorSceneManager.GetActiveScene() || !entry.IsConfigured)
                continue;
            DrawStartHandle(entry.KingPoint);
            DrawStartHandle(entry.RookPoint);
        }
    }

    private static void DrawStartHandle(Transform marker)
    {
        Vector3 position = marker.position;
        float size = HandleUtility.GetHandleSize(position) * 0.15f;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(position, size, Vector3.zero, GhostHandleCap);
        if (EditorGUI.EndChangeCheck())
            SetEntryPosition(marker, new Vector3(moved.x, moved.y, position.z));
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
        StartPositionCoordinatesWindow window =
            EditorWindow.GetWindow<StartPositionCoordinatesWindow>("시작 좌표");
        window.FocusSelectedEntry();
    }

    internal static void FocusSceneView(RoomEntry entry)
    {
        if (entry == null || !entry.IsConfigured) return;
        FocusSceneView(entry.KingPoint, entry.RookPoint);
    }

    private static void FocusSceneView(Transform kingStart, Transform rookStart)
    {
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

    internal static void SetEntryPosition(Transform marker, Vector3 position)
    {
        if (marker.position == position) return;

        Undo.RecordObject(marker, "Move room entry position");
        marker.position = position;
        EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
        SceneView.RepaintAll();
    }

    [MenuItem(RestoreMenu)]
    private static void RestoreStartPositions()
    {
        RoomTransitionController transition = Object.FindAnyObjectByType<RoomTransitionController>();
        MovePlayersToEntry(transition != null ? transition.StartingEntry : null);
    }

    internal static void MovePlayersToEntry(RoomEntry entry)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || entry == null || !entry.IsConfigured ||
            !TryGetTransforms(out Transform king, out Transform rook, out _, out _)) return;

        Vector3 kingPosition = entry.KingPoint.position;
        Vector3 rookPosition = entry.RookPoint.position;
        if (king.position == kingPosition && rook.position == rookPosition) return;

        Undo.IncrementCurrentGroup();
        Undo.RecordObjects(new Object[] { king, rook }, "Move players to room entry");
        king.position = kingPosition;
        rook.position = rookPosition;
        PrefabUtility.RecordPrefabInstancePropertyModifications(king);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rook);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        SceneView.RepaintAll();
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
        RoomTransitionController transition = Object.FindAnyObjectByType<RoomTransitionController>();
        RoomEntry entry = transition != null ? transition.StartingEntry : null;
        if (kingObject == null || rookObject == null || entry == null || !entry.IsConfigured)
            return false;

        kingStart = entry.KingPoint;
        rookStart = entry.RookPoint;

        king = kingObject.transform;
        rook = rookObject.transform;
        return true;
    }

    internal static RoomEntry[] GetSceneEntries()
    {
        if (EditorSceneManager.GetActiveScene().name != SceneNames.Development.GameplaySandbox)
            return new RoomEntry[0];

        var entries = new List<RoomEntry>();
        foreach (RoomEntry entry in Object.FindObjectsByType<RoomEntry>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (entry.gameObject.scene == EditorSceneManager.GetActiveScene() && entry.IsConfigured)
                entries.Add(entry);

        entries.Sort((a, b) =>
        {
            int roomOrder = string.CompareOrdinal(a.Room.name, b.Room.name);
            return roomOrder != 0 ? roomOrder : string.CompareOrdinal(a.name, b.name);
        });
        return entries.ToArray();
    }
}

internal sealed class StartPositionCoordinatesWindow : EditorWindow
{
    [SerializeField] private RoomEntry selectedEntry;

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        RoomEntry[] entries = PlayerStartPositions.GetSceneEntries();
        if (entries.Length == 0)
        {
            EditorGUILayout.HelpBox("Dev_Gameplay 씬에서 룸 입장 좌표를 볼 수 있습니다.", MessageType.Info);
            return;
        }

        int selectedIndex = GetSelectedIndex(entries);
        var names = new string[entries.Length];
        RoomTransitionController transition = Object.FindAnyObjectByType<RoomTransitionController>();
        for (int i = 0; i < entries.Length; i++)
            names[i] = entries[i].Room.name + " / " + entries[i].name +
                       (transition != null && entries[i] == transition.StartingEntry ? " (게임 시작)" : "");

        int nextIndex = EditorGUILayout.Popup("룸 입장 위치", selectedIndex, names);
        if (nextIndex != selectedIndex)
        {
            selectedEntry = entries[nextIndex];
            PlayerStartPositions.FocusSceneView(selectedEntry);
        }

        EditorGUILayout.LabelField("킹·룩 입장 좌표 (X, Y)", EditorStyles.boldLabel);
        DrawPositionField("킹", selectedEntry.KingPoint);
        DrawPositionField("룩", selectedEntry.RookPoint);
        string moveLabel = transition != null && selectedEntry == transition.StartingEntry
            ? "킹·룩 시작 위치로 복귀" : "킹·룩을 선택한 입장 위치로 이동";
        using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button(moveLabel))
                PlayerStartPositions.MovePlayersToEntry(selectedEntry);
        }
        if (GUILayout.Button("선택한 위치로 씬 뷰 이동"))
            PlayerStartPositions.FocusSceneView(selectedEntry);
    }

    internal void FocusSelectedEntry()
    {
        RoomEntry[] entries = PlayerStartPositions.GetSceneEntries();
        if (entries.Length > 0)
            PlayerStartPositions.FocusSceneView(entries[GetSelectedIndex(entries)]);
    }

    private int GetSelectedIndex(RoomEntry[] entries)
    {
        for (int i = 0; i < entries.Length; i++)
            if (entries[i] == selectedEntry) return i;

        RoomTransitionController transition = Object.FindAnyObjectByType<RoomTransitionController>();
        selectedEntry = transition != null && System.Array.IndexOf(entries, transition.StartingEntry) >= 0
            ? transition.StartingEntry : entries[0];
        return System.Array.IndexOf(entries, selectedEntry);
    }

    private static void DrawPositionField(string label, Transform marker)
    {
        Vector3 position = marker.position;
        EditorGUI.BeginChangeCheck();
        Vector2 next = EditorGUILayout.Vector2Field(label, new Vector2(position.x, position.y));
        if (EditorGUI.EndChangeCheck())
            PlayerStartPositions.SetEntryPosition(marker, new Vector3(next.x, next.y, position.z));
    }
}
