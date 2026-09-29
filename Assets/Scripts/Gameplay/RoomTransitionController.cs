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
        activeRoom = startingEntry.Room;
        bool foundStartingRoom = false;
        foreach (RoomArea room in rooms)
        {
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
        fadeOverlay.alpha = 0f;
    }

    private void Update()
    {
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
        if (!transitioning)
        {
            return;
        }

        Time.timeScale = previousTimeScale;
        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
        }

        transitioning = false;
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
