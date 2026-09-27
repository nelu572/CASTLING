using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Tilemaps;

// Unity refreshes neighbours within one Tilemap. Connect touching gameplay maps too.
[InitializeOnLoad]
public static class PlatformBorderRefresh
{
    private static readonly HashSet<Tilemap> Pending = new HashSet<Tilemap>();
    private static bool refreshing;

    static PlatformBorderRefresh()
    {
        Tilemap.tilemapTileChanged += Changed;
        Undo.undoRedoPerformed += RefreshOpenMaps;
        EditorApplication.projectChanged += PlatformBorderTile.ClearGeometryCache;
    }

    private static void Changed(Tilemap map, Tilemap.SyncTile[] changes)
    {
        if (refreshing || EditorApplication.isPlayingOrWillChangePlaymode || map == null) return;
        foreach (var other in PlatformBorderTile.ConnectedMaps(map)) Pending.Add(other);
        EditorApplication.delayCall -= Flush;
        EditorApplication.delayCall += Flush;
    }

    private static void Flush()
    {
        refreshing = true;
        try
        {
            foreach (var map in Pending)
                if (map != null) map.RefreshAllTiles();
        }
        finally { Pending.Clear(); refreshing = false; }
    }

    private static void RefreshOpenMaps()
    {
        foreach (var map in UnityEngine.Object.FindObjectsByType<Tilemap>(UnityEngine.FindObjectsSortMode.None))
            Pending.Add(map);
        Flush();
    }

    [MenuItem("Tools/CASTLING/Check Selected Platform Borders")]
    private static void CheckSelected()
    {
        var map = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponent<Tilemap>();
        if (map == null) { UnityEngine.Debug.Log("Select a gameplay Tilemap to check its borders."); return; }
        int checkedCells = 0, partialCells = 0;
        foreach (var position in map.cellBounds.allPositionsWithin)
        {
            var tile = map.GetTile<PlatformBorderTile>(position);
            if (tile == null) continue;
            checkedCells++;
            int partial;
            tile.ResolveMask(map, position, out partial);
            if (partial == 0) continue;
            partialCells++;
            UnityEngine.Debug.LogWarning($"Partial platform join at {map.name} {position}: keep the visible edge or select a manual border tile.", map);
        }
        UnityEngine.Debug.Log($"Platform borders: {checkedCells} automatic cells, {partialCells} partial joins.", map);
    }
}
