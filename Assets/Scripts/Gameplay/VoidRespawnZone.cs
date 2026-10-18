using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class VoidRespawnZone : MonoBehaviour
{
    [SerializeField] private Rigidbody2D king;
    [SerializeField] private Rigidbody2D rook;
    [SerializeField] private RoomEntry respawnEntry;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private Unity.Cinemachine.CinemachineTargetGroup targetGroup;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;

    private PlayerInput[] playerInputs;
    private PlayerMovement[] movements;
    private bool[] originalInputActive;
    private bool[] originalMovementEnabled;
    private float previousTimeScale;
    private RoomEntry checkpointEntry;
    private int checkpointOrder;

    public bool IsRespawning { get; private set; }
    public int RespawnCount { get; private set; }
    public RoomEntry RespawnEntry => checkpointEntry != null ? checkpointEntry : respawnEntry;
    public Rigidbody2D King => king;
    public Rigidbody2D Rook => rook;

    public bool SetCheckpoint(RoomEntry entry, int order)
    {
        if (IsRespawning || entry == null || !entry.IsConfigured || order <= checkpointOrder ||
            entry.Room != respawnEntry.Room)
        {
            return false;
        }

        checkpointEntry = entry;
        checkpointOrder = order;
        return true;
    }

    public bool RequestRespawn()
    {
        if (!isActiveAndEnabled || IsRespawning || Time.timeScale <= 0f)
        {
            return false;
        }

        StartCoroutine(Respawn());
        return true;
    }

    private void Awake()
    {
        if (king == null || rook == null || respawnEntry == null ||
            !respawnEntry.IsConfigured || fadeOverlay == null || targetGroup == null || !GetComponent<BoxCollider2D>().isTrigger)
        {
            Debug.LogError("Void respawn references or trigger are incomplete.", this);
            enabled = false;
            return;
        }

        playerInputs = new[] { king.GetComponent<PlayerInput>(), rook.GetComponent<PlayerInput>() };
        movements = new[] { king.GetComponent<PlayerMovement>(), rook.GetComponent<PlayerMovement>() };
        originalInputActive = new bool[2];
        originalMovementEnabled = new bool[2];
    }

    private void OnTriggerEnter2D(Collider2D other) => TryRespawn(other);
    private void OnTriggerStay2D(Collider2D other) => TryRespawn(other);

    private void TryRespawn(Collider2D other)
    {
        if (!enabled || IsRespawning || (other.attachedRigidbody != king && other.attachedRigidbody != rook))
        {
            return;
        }

        RequestRespawn();
    }

    private IEnumerator Respawn()
    {
        IsRespawning = true;
        RoomEntry destination = RespawnEntry;
        previousTimeScale = Time.timeScale;
        king.GetComponent<RookSlideAbility>()?.StopSlide();
        rook.GetComponent<RookSlideAbility>()?.StopSlide();
        for (int i = 0; i < playerInputs.Length; i++)
        {
            originalInputActive[i] = playerInputs[i].inputIsActive;
            originalMovementEnabled[i] = movements[i].enabled;
            playerInputs[i].DeactivateInput();
            movements[i].enabled = false;
        }

        Time.timeScale = 0f;
        yield return Fade(0f, 1f);

        MovePlayer(king, destination.KingPoint.position);
        MovePlayer(rook, destination.RookPoint.position);
        Physics2D.SyncTransforms();
        targetGroup.DoUpdate();
        destination.Room.Camera.PreviousStateIsValid = false;

        yield return null;
        yield return Fade(1f, 0f);
        RestorePlayers();
        Time.timeScale = previousTimeScale;
        RespawnCount++;
        IsRespawning = false;
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

    private static void MovePlayer(Rigidbody2D player, Vector3 point)
    {
        player.GetComponent<PlayerMovement>().ClearGroundedHistory();
        player.linearVelocity = Vector2.zero;
        player.angularVelocity = 0f;
        player.position = point;
        player.transform.position = new Vector3(point.x, point.y, player.transform.position.z);
    }

    private void RestorePlayers()
    {
        for (int i = 0; i < playerInputs.Length; i++)
        {
            if (movements[i] != null) movements[i].enabled = originalMovementEnabled[i];
            if (playerInputs[i] != null && originalInputActive[i]) playerInputs[i].ActivateInput();
        }
    }

    private void OnDisable()
    {
        if (!IsRespawning) return;
        StopAllCoroutines();
        RestorePlayers();
        Time.timeScale = previousTimeScale;
        if (fadeOverlay != null) fadeOverlay.alpha = 0f;
        IsRespawning = false;
    }
}
