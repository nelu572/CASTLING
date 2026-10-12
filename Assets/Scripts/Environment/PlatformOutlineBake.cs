using UnityEngine;
using UnityEngine.Tilemaps;

// The Editor saves the generated renderers and sprites; gameplay does no baking.
[DisallowMultipleComponent]
public sealed class PlatformOutlineBake : MonoBehaviour
{
    public Tilemap[] sources;
    [Range(1, 64)] public int widthPixels = 14;
    [InspectorName("칠할 때 자동 적용")] public bool rebuildWhileEditing = false;
    [InspectorName("팔레트 닫을 때 적용")] public bool rebuildWhenPaletteCloses = false;
    [HideInInspector] public GameObject generatedRoot;
}
