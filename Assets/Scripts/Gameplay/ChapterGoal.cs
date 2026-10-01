using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ChapterGoal : MonoBehaviour
{
    [SerializeField] private Transform king;
    [SerializeField] private Transform rook;
    [SerializeField] private GameObject completionMessage;

    private BoxCollider2D area;

    public bool Completed { get; private set; }

    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        if (king == null || rook == null || completionMessage == null || !area.isTrigger)
        {
            Debug.LogError("Chapter goal references or trigger are incomplete.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (!Completed && area.OverlapPoint(king.position) && area.OverlapPoint(rook.position))
        {
            Completed = true;
            completionMessage.SetActive(true);
        }
    }
}
