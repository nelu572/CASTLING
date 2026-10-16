using UnityEngine;

/// <summary>A physical foothold moved by contact, also recognized by pressure plates.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class PushablePlatform : MonoBehaviour
{
    private void Awake()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        BoxCollider2D surface = GetComponent<BoxCollider2D>();
        if (body.bodyType != RigidbodyType2D.Dynamic || surface.isTrigger ||
            gameObject.layer != LayerMask.NameToLayer(Layers.Environment))
        {
            Debug.LogError("Pushable platform requires a dynamic body and solid Environment collider.", this);
            enabled = false;
        }
    }
}
