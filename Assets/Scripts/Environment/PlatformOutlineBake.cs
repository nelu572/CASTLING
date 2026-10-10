using UnityEngine;
using UnityEngine.Tilemaps;

// The Editor saves the generated renderers and sprites; gameplay does no baking.
[DisallowMultipleComponent]
public sealed class PlatformOutlineBake : MonoBehaviour
{
    public Tilemap[] sources;
    [Range(1, 64)] public int widthPixels = 14;
    public bool rebuildWhileEditing = true;
    [HideInInspector] public GameObject generatedRoot;
}
