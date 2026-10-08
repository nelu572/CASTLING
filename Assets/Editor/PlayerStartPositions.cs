using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
    private static RoomTransitionController selectedController;
    private static bool previewEnabled;

    internal static bool PreviewEnabled => previewEnabled;

    internal static RoomTransitionController SelectedController
    {
        get
        {
            if (selectedController != null &&
                (selectedController.gameObject.scene != EditorSceneManager.GetActiveScene() ||
                 !selectedController.gameObject.activeInHierarchy))
                SelectController(null);

            if (selectedController == null)
            {
                if (previewEnabled) SetPreviewEnabled(false);
                RoomTransitionController[] controllers = GetSceneControllers();
                if (controllers.Length == 1) selectedController = controllers[0];
            }
            return selectedController;
        }
    }

    static PlayerStartPositions()
    {
        EditorApplication.update += UpdatePreviews;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
        SceneView.duringSceneGui += DrawStartHandles;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += ClearPreviews;
        EditorApplication.quitting += ClearPreviews;
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next)
    {
        SelectController(null);
        SetPreviewEnabled(false);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        SetPreviewEnabled(false);
        selectedController = null;
    }

    internal static void SelectController(RoomTransitionController controller)
    {
        if (controller != null &&
            (controller.gameObject.scene != EditorSceneManager.GetActiveScene() ||
             !controller.gameObject.activeInHierarchy)) return;

        if (selectedController == controller) return;
        SetPreviewEnabled(false);
        selectedController = controller;
    }

    internal static void SetPreviewEnabled(bool enabled)
    {
        previewEnabled = enabled && !EditorApplication.isPlayingOrWillChangePlaymode &&
                         TryGetContext(out _, out _, out _, out _);
        if (!previewEnabled) ClearPreviews();
        SceneView.RepaintAll();
    }

    internal static RoomTransitionController[] GetSceneControllers()
    {
        var controllers = new List<RoomTransitionController>();
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return controllers.ToArray();

        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (RoomTransitionController controller in
                     root.GetComponentsInChildren<RoomTransitionController>())
                if (controller.gameObject.activeInHierarchy) controllers.Add(controller);

        controllers.Sort((a, b) => string.CompareOrdinal(
            GetHierarchyPath(a.transform), GetHierarchyPath(b.transform)));
        return controllers.ToArray();
    }

    internal static string GetHierarchyPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }
        return path;
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
        if (!previewEnabled) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            !TryGetContext(out Transform king, out Transform rook, out _, out _))
        {
            SetPreviewEnabled(false);
            return;
        }

        VisibleMarkers.Clear();
        foreach (RoomEntry entry in GetSceneEntries())
        {
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
        SetPreviewsEnabled(previewEnabled && camera.cameraType == CameraType.SceneView &&
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
        if (!previewEnabled || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        foreach (RoomEntry entry in GetSceneEntries())
        {
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
        if (EditorApplication.isPlayingOrWillChangePlaymode || marker == null ||
            marker.position == position) return;

        bool isTargetMarker = false;
        foreach (RoomEntry entry in GetSceneEntries())
            isTargetMarker |= marker == entry.KingPoint || marker == entry.RookPoint;
        if (!isTargetMarker) return;

        Undo.RecordObject(marker, "Move room entry position");
        marker.position = position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
        EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
        SceneView.RepaintAll();
    }

    [MenuItem(RestoreMenu)]
    private static void RestoreStartPositions()
    {
        RoomTransitionController transition = SelectedController;
        MovePlayersToEntry(transition != null ? transition.StartingEntry : null);
    }

    internal static void MovePlayersToEntry(RoomEntry entry)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || entry == null || !entry.IsConfigured ||
            !TryGetContext(out Transform king, out Transform rook, out Transform roomsRoot, out _) ||
            !IsEntryInRoot(entry, roomsRoot)) return;

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
               TryGetContext(out _, out _, out _, out _);
    }

    internal static bool TryGetContext(out Transform king, out Transform rook,
        out Transform roomsRoot, out string message)
    {
        king = null;
        rook = null;
        roomsRoot = null;
        message = null;

        RoomTransitionController controller = SelectedController;
        if (controller == null)
        {
            message = GetSceneControllers().Length == 0
                ? "현재 씬에 사용 가능한 RoomTransitionController가 없습니다."
                : "시작 위치를 편집할 대상 컨트롤러를 선택하세요.";
            return false;
        }

        var serialized = new SerializedObject(controller);
        Rigidbody2D kingBody = serialized.FindProperty("king").objectReferenceValue as Rigidbody2D;
        Rigidbody2D rookBody = serialized.FindProperty("rook").objectReferenceValue as Rigidbody2D;
        roomsRoot = serialized.FindProperty("roomsRoot").objectReferenceValue as Transform;
        if (kingBody == null || rookBody == null || roomsRoot == null)
        {
            message = "대상 컨트롤러의 King, Rook, Rooms Root 참조를 연결하세요.";
            return false;
        }

        king = kingBody.transform;
        rook = rookBody.transform;
        Scene scene = EditorSceneManager.GetActiveScene();
        if (king.gameObject.scene != scene || rook.gameObject.scene != scene ||
            roomsRoot.gameObject.scene != scene)
        {
            message = "King, Rook, Rooms Root는 현재 씬의 오브젝트에 연결해야 합니다.";
            return false;
        }

        if (!IsEntryInRoot(controller.StartingEntry, roomsRoot))
        {
            message = "Starting Entry를 Rooms Root 안의 유효한 룸 입장 위치에 연결하세요.";
            return false;
        }
        return true;
    }

    private static bool IsEntryInRoot(RoomEntry entry, Transform roomsRoot)
    {
        return entry != null && entry.IsConfigured &&
               entry.gameObject.scene == roomsRoot.gameObject.scene &&
               entry.Room.transform.IsChildOf(roomsRoot) &&
               entry.transform.IsChildOf(roomsRoot) &&
               entry.KingPoint.IsChildOf(roomsRoot) && entry.RookPoint.IsChildOf(roomsRoot);
    }

    internal static RoomEntry[] GetSceneEntries()
    {
        if (!TryGetContext(out _, out _, out Transform roomsRoot, out _))
            return new RoomEntry[0];

        var entries = new List<RoomEntry>();
        foreach (RoomEntry entry in roomsRoot.GetComponentsInChildren<RoomEntry>(true))
            if (IsEntryInRoot(entry, roomsRoot))
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

    private void OnDisable()
    {
        PlayerStartPositions.SetPreviewEnabled(false);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("편집 씬", EditorSceneManager.GetActiveScene().name);
        DrawControllerSelection();
        bool configured = PlayerStartPositions.TryGetContext(out _, out _, out _, out string message);
        bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
        using (new EditorGUI.DisabledGroupScope(!configured || playing))
        {
            bool enabled = EditorGUILayout.Toggle("미리보기 표시", PlayerStartPositions.PreviewEnabled);
            if (enabled != PlayerStartPositions.PreviewEnabled)
                PlayerStartPositions.SetPreviewEnabled(enabled);
        }
        if (!configured)
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
            return;
        }

        RoomEntry[] entries = PlayerStartPositions.GetSceneEntries();
        if (entries.Length == 0)
        {
            EditorGUILayout.HelpBox("Rooms Root 안에 유효한 룸 입장 위치가 없습니다.", MessageType.Info);
            return;
        }

        int selectedIndex = GetSelectedIndex(entries);
        var names = new string[entries.Length];
        RoomTransitionController transition = PlayerStartPositions.SelectedController;
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
        string moveLabel = transition != null && selectedEntry == transition.StartingEntry
            ? "킹·룩 시작 위치로 복귀" : "킹·룩을 선택한 입장 위치로 이동";
        using (new EditorGUI.DisabledGroupScope(playing))
        {
            DrawPositionField("킹", selectedEntry.KingPoint);
            DrawPositionField("룩", selectedEntry.RookPoint);
            if (GUILayout.Button(moveLabel))
                PlayerStartPositions.MovePlayersToEntry(selectedEntry);
        }
        if (GUILayout.Button("선택한 위치로 씬 뷰 이동"))
            PlayerStartPositions.FocusSceneView(selectedEntry);
    }

    private void DrawControllerSelection()
    {
        RoomTransitionController[] controllers = PlayerStartPositions.GetSceneControllers();
        RoomTransitionController selected = PlayerStartPositions.SelectedController;
        var names = new string[controllers.Length + 1];
        names[0] = "대상 선택";
        int selectedIndex = 0;
        for (int i = 0; i < controllers.Length; i++)
        {
            names[i + 1] = PlayerStartPositions.GetHierarchyPath(controllers[i].transform);
            if (controllers[i] == selected) selectedIndex = i + 1;
        }

        using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            int nextIndex = EditorGUILayout.Popup("대상 컨트롤러", selectedIndex, names);
            if (nextIndex == selectedIndex) return;
            PlayerStartPositions.SelectController(nextIndex == 0 ? null : controllers[nextIndex - 1]);
            selectedEntry = null;
        }
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

        RoomTransitionController transition = PlayerStartPositions.SelectedController;
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
