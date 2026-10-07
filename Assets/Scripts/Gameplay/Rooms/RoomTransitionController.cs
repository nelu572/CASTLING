using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomTransitionController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D king;
    [SerializeField] private Rigidbody2D rook;
    [SerializeField] private Transform roomsRoot;
    [SerializeField] private RoomEntry startingEntry;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;

    private RoomArea[] rooms;
    private CinemachineGroupFraming[] roomFramings;
    private Vector2[] originalOrthoSizeRanges;
    private CinemachineTargetGroup targetGroup;
    private float[] originalTargetWeights;
    private bool[] includedTargets;
    private Camera outputCamera;
    private RoomArea activeRoom;
    private bool transitioning;
    private bool waitingForExitClear;
    private float previousTimeScale;

    public RoomArea ActiveRoom => activeRoom;
    public RoomEntry StartingEntry => startingEntry;

    private void Start()
    {
        if (king == null || rook == null || roomsRoot == null ||
            startingEntry == null || !startingEntry.IsConfigured || fadeOverlay == null)
        {
            Debug.LogError("Room transition references are incomplete.", this);
            enabled = false;
            return;
        }

        rooms = roomsRoot.GetComponentsInChildren<RoomArea>(true);
        roomFramings = new CinemachineGroupFraming[rooms.Length];
        originalOrthoSizeRanges = new Vector2[rooms.Length];
        targetGroup = GetComponent<CinemachineTargetGroup>();
        if (targetGroup == null)
        {
            Debug.LogError("Room transition target group is missing.", this);
            enabled = false;
            return;
        }

        originalTargetWeights = new float[targetGroup.Targets.Count];
        includedTargets = new bool[targetGroup.Targets.Count];
        for (int i = 0; i < targetGroup.Targets.Count; i++)
        {
            originalTargetWeights[i] = targetGroup.Targets[i].Weight;
        }

        outputCamera = Camera.main;
        activeRoom = startingEntry.Room;
        bool foundStartingRoom = false;
        for (int i = 0; i < rooms.Length; i++)
        {
            RoomArea room = rooms[i];
            foundStartingRoom |= room == activeRoom;
            if (room.Camera == null || room.CameraBounds == null || room.Background == null)
            {
                Debug.LogError($"Room '{room.name}' is missing its camera, bounds, or background.", room);
                enabled = false;
                return;
            }

            CinemachineConfiner2D confiner = room.Camera.GetComponent<CinemachineConfiner2D>();
            if (confiner == null || confiner.BoundingShape2D != room.CameraBounds)
            {
                Debug.LogError($"Room '{room.name}' camera bounds are not assigned to its camera confiner.", room);
                enabled = false;
                return;
            }

            roomFramings[i] = room.Camera.GetComponent<CinemachineGroupFraming>();
            if (roomFramings[i] == null)
            {
                Debug.LogError($"Room '{room.name}' camera is missing group framing.", room);
                enabled = false;
                return;
            }

            originalOrthoSizeRanges[i] = roomFramings[i].OrthoSizeRange;
        }

        if (!foundStartingRoom)
        {
            Debug.LogError("The starting entry is outside the configured rooms.", this);
            enabled = false;
            return;
        }

        foreach (RoomArea room in rooms)
        {
            room.Background.SetActive(room == activeRoom);
            SetPriority(room.Camera, room == activeRoom ? 20 : 0);
        }

        MovePlayer(king, startingEntry.KingPoint.position);
        MovePlayer(rook, startingEntry.RookPoint.position);
        Physics2D.SyncTransforms();
        ApplyCameraZoomLimits();
        fadeOverlay.alpha = 0f;
    }

    private void Update()
    {
        ApplyCameraZoomLimits();
        if (transitioning || activeRoom == null)
        {
            return;
        }

        bool insideAnyExit = false;
        foreach (RoomExit exit in activeRoom.Exits)
        {
            if (!exit.ContainsBoth(king.transform, rook.transform))
            {
                continue;
            }

            insideAnyExit = true;
            if (!waitingForExitClear && exit.Destination != null &&
                exit.Destination.IsConfigured && exit.Destination.Room != activeRoom)
            {
                StartCoroutine(EnterRoom(exit.Destination));
                return;
            }
        }

        if (!insideAnyExit)
        {
            waitingForExitClear = false;
        }
    }

    private void OnDisable()
    {
        RestoreCameraTargets();
        RestoreCameraZoomLimits();
        if (!transitioning)
        {
            return;
        }

        StopAllCoroutines();
        Time.timeScale = previousTimeScale;
        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
        }

        transitioning = false;
    }

    private void LateUpdate()
    {
        UpdateCameraTargets();
    }

    private void UpdateCameraTargets()
    {
        if (activeRoom == null || activeRoom.CameraBounds == null ||
            targetGroup == null || targetGroup.Targets.Count != originalTargetWeights.Length)
        {
            return;
        }

        Bounds bounds = activeRoom.CameraBounds.bounds;
        bool anyInside = false;
        int nearestTarget = -1;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < targetGroup.Targets.Count; i++)
        {
            CinemachineTargetGroup.Target target = targetGroup.Targets[i];
            includedTargets[i] = false;
            if (target.Object == null || originalTargetWeights[i] <= 0f) continue;

            Vector3 point = target.Object.position;
            bool inside = point.x >= bounds.min.x && point.x <= bounds.max.x &&
                          point.y >= bounds.min.y && point.y <= bounds.max.y;
            includedTargets[i] = inside;
            anyInside |= inside;

            float dx = point.x - Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
            float dy = point.y - Mathf.Clamp(point.y, bounds.min.y, bounds.max.y);
            float distance = dx * dx + dy * dy;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = i;
            }
        }

        // Cinemachine needs a target even if both characters leave the camera area.
        if (!anyInside && nearestTarget >= 0) includedTargets[nearestTarget] = true;

        bool changed = false;
        for (int i = 0; i < targetGroup.Targets.Count; i++)
        {
            float weight = includedTargets[i] ? originalTargetWeights[i] : 0f;
            if (targetGroup.Targets[i].Weight == weight) continue;
            targetGroup.Targets[i].Weight = weight;
            changed = true;
        }

        if (changed) targetGroup.DoUpdate();
    }

    private void RestoreCameraTargets()
    {
        if (targetGroup == null || originalTargetWeights == null) return;
        int count = Mathf.Min(targetGroup.Targets.Count, originalTargetWeights.Length);
        for (int i = 0; i < count; i++)
        {
            targetGroup.Targets[i].Weight = originalTargetWeights[i];
        }

        targetGroup.DoUpdate();
    }

    private void ApplyCameraZoomLimits()
    {
        if (outputCamera == null || !outputCamera.orthographic || rooms == null || roomFramings == null)
        {
            return;
        }

        float aspect = Mathf.Max(0.01f, outputCamera.aspect);
        for (int i = 0; i < rooms.Length; i++)
        {
            CinemachineGroupFraming framing = roomFramings[i];
            Collider2D cameraBounds = rooms[i].CameraBounds;
            if (framing == null || cameraBounds == null || !cameraBounds.enabled)
            {
                continue;
            }

            Bounds bounds = cameraBounds.bounds;
            float maximumFittingSize = Mathf.Min(bounds.size.x / (2f * aspect), bounds.size.y * 0.5f);
            if (maximumFittingSize <= 0f)
            {
                continue;
            }

            Vector2 original = originalOrthoSizeRanges[i];
            Vector2 limited = new Vector2(
                Mathf.Min(original.x, maximumFittingSize),
                Mathf.Min(original.y, maximumFittingSize));
            if (framing.OrthoSizeRange != limited)
            {
                framing.OrthoSizeRange = limited;
            }
        }
    }

    private void RestoreCameraZoomLimits()
    {
        if (roomFramings == null || originalOrthoSizeRanges == null)
        {
            return;
        }

        for (int i = 0; i < roomFramings.Length; i++)
        {
            if (roomFramings[i] != null)
            {
                roomFramings[i].OrthoSizeRange = originalOrthoSizeRanges[i];
            }
        }
    }

    private IEnumerator EnterRoom(RoomEntry destination)
    {
        transitioning = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        yield return Fade(0f, 1f);

        RoomArea nextRoom = destination.Room;
        MovePlayer(king, destination.KingPoint.position);
        MovePlayer(rook, destination.RookPoint.position);
        Physics2D.SyncTransforms();

        activeRoom.Background.SetActive(false);
        nextRoom.Background.SetActive(true);
        SetPriority(activeRoom.Camera, 0);
        SetPriority(nextRoom.Camera, 20);
        activeRoom = nextRoom;

        Time.timeScale = previousTimeScale;
        yield return null;
        yield return Fade(1f, 0f);
        waitingForExitClear = true;
        transitioning = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        fadeOverlay.alpha = to;
    }

    private static void MovePlayer(Rigidbody2D player, Vector3 destination)
    {
        player.GetComponent<RookSlideAbility>()?.StopSlide();
        player.linearVelocity = Vector2.zero;
        player.angularVelocity = 0f;
        player.position = destination;
        player.transform.position = new Vector3(destination.x, destination.y, player.transform.position.z);
    }

    private static void SetPriority(CinemachineCamera camera, int value)
    {
        PrioritySettings priority = camera.Priority;
        priority.Enabled = true;
        priority.Value = value;
        camera.Priority = priority;
    }
}
