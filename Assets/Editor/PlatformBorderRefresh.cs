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

    [MenuItem("Tools/CASTLING/선택한 자동 타일 접합 검사")]
    private static void CheckSelected()
    {
        var map = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponent<Tilemap>();
        if (map == null) { UnityEngine.Debug.Log("접합을 검사할 지형 타일맵을 선택하세요."); return; }
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
            UnityEngine.Debug.LogWarning($"부분 접합: {map.name} {position}. 노출된 선을 유지하거나 수동 외곽선 타일을 선택하세요.", map);
        }
        UnityEngine.Debug.Log($"타일 접합 검사: 자동 타일 {checkedCells}칸, 부분 접합 {partialCells}곳.", map);
    }
}
