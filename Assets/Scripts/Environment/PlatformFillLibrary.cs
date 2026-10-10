using UnityEngine;
using UnityEngine.Tilemaps;

// Maps existing tile assets to the same shape without authored border pixels.
public sealed class PlatformFillLibrary : ScriptableObject
{
    public TileBase[] sources;
    public Tile[] fills;
}
