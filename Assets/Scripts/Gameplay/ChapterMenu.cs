using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ChapterMenu : MonoBehaviour
{
    [SerializeField] private VoidRespawnZone recovery;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject confirmPanel;

    private PlayerInputHandler[] handlers;
    private bool ownsPause;
    private float previousTimeScale;

    public bool IsOpen => ownsPause;

    private void Start()
    {
        if (recovery == null || recovery.King == null || recovery.Rook == null ||
            pausePanel == null || confirmPanel == null)
        {
            Debug.LogError("Chapter menu references are incomplete.", this);
            enabled = false;
            return;
        }

        handlers = new[] { recovery.King.GetComponent<PlayerInputHandler>(), recovery.Rook.GetComponent<PlayerInputHandler>() };
        if (handlers[0] == null || handlers[1] == null)
        {
            Debug.LogError("Chapter menu player input handlers are missing.", this);
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (ownsPause)
        {
            if (confirmPanel.activeSelf) CancelRestart();
            else Continue();
        }
        else if (!recovery.IsRespawning && Time.timeScale > 0f)
        {
            Open(pausePanel);
        }
    }

    private void Open(GameObject panel)
    {
        if (ownsPause || recovery.IsRespawning) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        foreach (PlayerInputHandler handler in handlers) handler.SetGameplayPaused(true);
        ownsPause = true;
        pausePanel.SetActive(panel == pausePanel);
        confirmPanel.SetActive(false);
        SelectFirstButton(panel);
    }

    public void Continue()
    {
        if (!ownsPause) return;
        pausePanel.SetActive(false);
        confirmPanel.SetActive(false);
        Time.timeScale = previousTimeScale;
        foreach (PlayerInputHandler handler in handlers)
        {
            if (handler != null) handler.SetGameplayPaused(false);
        }
        ownsPause = false;
    }

    public void AskRestart()
    {
        if (!ownsPause) return;
        pausePanel.SetActive(false);
        confirmPanel.SetActive(true);
        SelectFirstButton(confirmPanel);
    }

    public void CancelRestart()
    {
        if (!ownsPause) return;
        confirmPanel.SetActive(false);
        pausePanel.SetActive(true);
        SelectFirstButton(pausePanel);
    }

    public void ConfirmRestart()
    {
        if (!ownsPause) return;
        Continue();
        recovery.RequestRespawn();
    }

    private void OnDisable()
    {
        // Restore only the pause owned here; never leave the chapter frozen on teardown.
        if (ownsPause) Continue();
    }

    private static void SelectFirstButton(GameObject panel)
    {
        var button = panel.GetComponentInChildren<UnityEngine.UI.Button>();
        if (button != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
