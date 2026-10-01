using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class RoomAuthoring
{
    private const string CreateMenu = "Tools/CASTLING/룸/선택한 룸 복제";
    private const float RoomGap = 10f;

    [MenuItem(CreateMenu)]
    private static void CreateRoom()
    {
        if (!TryGetSelectedRoom(out RoomArea source, out Transform roomsRoot)) return;
        if (AnimationMode.InAnimationMode())
        {
            EditorUtility.DisplayDialog("룸 복제", "Animation Mode 또는 Live Camera & Parallax Preview를 끈 뒤 다시 실행하세요.", "확인");
            return;
        }

        string name = NextRoomName(roomsRoot);
        float rightEdge = float.NegativeInfinity;
        foreach (Transform child in roomsRoot)
        {
            RoomArea room = child.GetComponent<RoomArea>();
            if (room != null)
                rightEdge = Mathf.Max(rightEdge, GetFootprint(room).max.x);
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create " + name);
        GameObject copy = null;
        try
        {
            copy = UnityEngine.Object.Instantiate(source.gameObject);
            Undo.RegisterCreatedObjectUndo(copy, "Create " + name);
            Undo.RecordObject(copy, "Name " + name);
            copy.name = name;
            Undo.SetTransformParent(copy.transform, roomsRoot, "Parent " + name);
            Undo.RecordObject(copy.transform, "Position " + name);
            copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            copy.transform.localScale = source.transform.localScale;

            RoomArea newRoom = copy.GetComponent<RoomArea>();
            ConnectRoomReferences(source, newRoom);

            Physics2D.SyncTransforms();
            Bounds footprint = GetFootprint(newRoom);
            copy.transform.position += Vector3.right * (rightEdge + RoomGap - footprint.min.x);
            Physics2D.SyncTransforms();

            Selection.activeGameObject = copy;
            EditorGUIUtility.PingObject(copy);
            EditorSceneManager.MarkSceneDirty(copy.scene);
            SceneView.RepaintAll();
        }
        catch (Exception exception)
        {
            if (copy != null) Undo.DestroyObjectImmediate(copy);
            Debug.LogException(exception);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    [MenuItem(CreateMenu, true)]
    private static bool ValidateCreateRoom()
    {
        return TryGetSelectedRoom(out _, out _);
    }

    private static bool TryGetSelectedRoom(out RoomArea room, out Transform roomsRoot)
    {
        room = null;
        roomsRoot = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorSceneManager.GetActiveScene().name != SceneNames.Development.GameplaySandbox ||
            Selection.activeGameObject == null)
            return false;

        room = Selection.activeGameObject.GetComponentInParent<RoomArea>();
        RoomTransitionController controller = UnityEngine.Object.FindAnyObjectByType<RoomTransitionController>();
        if (room == null || controller == null || room.gameObject.scene != EditorSceneManager.GetActiveScene())
            return false;

        roomsRoot = new SerializedObject(controller).FindProperty("roomsRoot").objectReferenceValue as Transform;
        return roomsRoot != null && room.transform.parent == roomsRoot &&
               room.Camera != null && room.CameraBounds != null && room.Background != null;
    }

    private static void ConnectRoomReferences(RoomArea source, RoomArea copy)
    {
        Transform sourceRoot = source.transform;
        Transform copyRoot = copy.transform;
        CinemachineCamera roomCamera = MapComponent(source.Camera, sourceRoot, copyRoot);
        Collider2D cameraBounds = MapComponent(source.CameraBounds, sourceRoot, copyRoot);
        GameObject background = MapTransform(source.Background.transform, sourceRoot, copyRoot).gameObject;

        var roomProperties = new SerializedObject(copy);
        roomProperties.FindProperty("roomCamera").objectReferenceValue = roomCamera;
        roomProperties.FindProperty("cameraBounds").objectReferenceValue = cameraBounds;
        roomProperties.FindProperty("background").objectReferenceValue = background;
        roomProperties.ApplyModifiedProperties();

        CinemachineConfiner2D confiner = roomCamera.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
            throw new InvalidOperationException("The copied room camera has no CinemachineConfiner2D.");
        Undo.RecordObject(confiner, "Connect room camera bounds");
        confiner.BoundingShape2D = cameraBounds;

        foreach (RoomEntry sourceEntry in source.GetComponentsInChildren<RoomEntry>(true))
        {
            if (!sourceEntry.IsConfigured)
                throw new InvalidOperationException("A source room entry is missing a player marker.");

            RoomEntry newEntry = MapComponent(sourceEntry, sourceRoot, copyRoot);
            var entryProperties = new SerializedObject(newEntry);
            entryProperties.FindProperty("kingPoint").objectReferenceValue =
                MapTransform(sourceEntry.KingPoint, sourceRoot, copyRoot);
            entryProperties.FindProperty("rookPoint").objectReferenceValue =
                MapTransform(sourceEntry.RookPoint, sourceRoot, copyRoot);
            entryProperties.ApplyModifiedProperties();
        }

        foreach (RoomExit exit in copy.GetComponentsInChildren<RoomExit>(true))
        {
            var exitProperties = new SerializedObject(exit);
            exitProperties.FindProperty("destination").objectReferenceValue = null;
            exitProperties.ApplyModifiedProperties();
        }

        foreach (BackgroundParallax sourceLayer in source.GetComponentsInChildren<BackgroundParallax>(true))
        {
            BackgroundParallax newLayer = MapComponent(sourceLayer, sourceRoot, copyRoot);
            var sourceProperties = new SerializedObject(sourceLayer);
            Transform origin = sourceProperties.FindProperty("cameraOriginReference").objectReferenceValue as Transform;
            Transform visual = sourceProperties.FindProperty("visualRoot").objectReferenceValue as Transform;
            var layerProperties = new SerializedObject(newLayer);
            layerProperties.FindProperty("cameraOriginReference").objectReferenceValue =
                origin != null ? MapTransform(origin, sourceRoot, copyRoot) : null;
            layerProperties.FindProperty("visualRoot").objectReferenceValue =
                visual != null ? MapTransform(visual, sourceRoot, copyRoot) : null;
            layerProperties.ApplyModifiedProperties();
        }
    }

    private static T MapComponent<T>(T original, Transform sourceRoot, Transform copyRoot)
        where T : Component
    {
        T mapped = MapTransform(original.transform, sourceRoot, copyRoot).GetComponent<T>();
        if (mapped == null)
            throw new InvalidOperationException("A copied room component could not be found: " + original.name);
        return mapped;
    }

    private static Transform MapTransform(Transform original, Transform sourceRoot, Transform copyRoot)
    {
        var names = new Stack<string>();
        for (Transform current = original; current != sourceRoot; current = current.parent)
        {
            if (current == null)
                throw new InvalidOperationException("A room reference points outside the source room: " + original.name);
            names.Push(current.name);
        }

        Transform mapped = copyRoot;
        while (names.Count > 0)
        {
            mapped = mapped.Find(names.Pop());
            if (mapped == null)
                throw new InvalidOperationException("A referenced child was not copied: " + original.name);
        }
        return mapped;
    }

    private static Bounds GetFootprint(RoomArea room)
    {
        Bounds footprint = room.CameraBounds.bounds;
        foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>(true))
            if (renderer.bounds.size.sqrMagnitude > 0.0001f)
                footprint.Encapsulate(renderer.bounds);
        return footprint;
    }

    private static string NextRoomName(Transform roomsRoot)
    {
        int highest = 0;
        foreach (Transform child in roomsRoot)
            if (child.name.StartsWith("Room_", StringComparison.Ordinal) &&
                int.TryParse(child.name.Substring(5), out int number))
                highest = Mathf.Max(highest, number);
        return "Room_" + (highest + 1).ToString("D2");
    }
}
