using UnityEngine;
using UnityEngine.Tilemaps;

// Tiles are subassets so border variants do not each require a separate file.
public sealed class PlatformTileLibrary : ScriptableObject
{
    public TileBase[] tiles;
}
