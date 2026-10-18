using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlatformOutlineAuthoring
{
    private const int PixelsPerUnit = 128;
    private const int ChunkPixels = 1024;
    private const int WithoutUndo = int.MinValue;
    private const string DirtySessionPrefix = "CASTLING.PlatformOutlineDirty.";
    private const string ExportSessionPrefix = "CASTLING.PlatformOutlineExport.";
    private static readonly Dictionary<PlatformOutlineBake, int> Pending = new Dictionary<PlatformOutlineBake, int>();
    private static readonly HashSet<PlatformOutlineBake> DirtyBakes = new HashSet<PlatformOutlineBake>();
    private static readonly Dictionary<PlatformOutlineBake, HashSet<CellKey>> ChangedCells = new Dictionary<PlatformOutlineBake, HashSet<CellKey>>();
    private static readonly Dictionary<PlatformOutlineBake, InputCache> Inputs = new Dictionary<PlatformOutlineBake, InputCache>();
    private static readonly Dictionary<PlatformOutlineBake, Dictionary<Vector2Int, ChunkState>> ChunkStates
        = new Dictionary<PlatformOutlineBake, Dictionary<Vector2Int, ChunkState>>();
    private static readonly Dictionary<Texture2D, SourcePixels> PixelSources = new Dictionary<Texture2D, SourcePixels>();
    private static readonly Dictionary<PlatformOutlineBake, BackgroundWork> Background = new Dictionary<PlatformOutlineBake, BackgroundWork>();
    private static readonly Dictionary<int, PendingExport> PendingExports = new Dictionary<int, PendingExport>();
    private static Dictionary<Vector2Int, ChunkImage> completedImages;
    private static bool rebuilding;
    private static bool paletteWasOpen;
    private static int resavingSceneHandle;

    // During ExitingPlayMode, willChange can be false while runtime scenes are
    // still closing. Keep all authoring callbacks blocked until Play has ended.
    internal static bool IsPlayModeActive
    {
        get { return EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode; }
    }

    private sealed class PendingExport
    {
        public Scene scene;
        public string path;
        public List<SpriteRenderer> renderers;
        public List<string> names;
        public List<Sprite> previews;
        public bool sceneSaved;
        public int attempts;
    }

    [Serializable]
    private sealed class ExportSnapshot
    {
        public string[] names;
        public bool sceneSaved;
    }

    private sealed class ChunkState
    {
        public string fingerprint, spriteName;
    }

    private struct CellKey : IEquatable<CellKey>
    {
        public Tilemap map;
        public Vector3Int cell;
        private int mapId;
        public CellKey(Tilemap map, Vector3Int cell) { this.map = map; this.cell = cell; mapId = map.GetInstanceID(); }
        public bool Equals(CellKey other) { return mapId == other.mapId && cell == other.cell; }
        public override bool Equals(object other) { return other is CellKey && Equals((CellKey)other); }
        public override int GetHashCode() { return mapId * 397 ^ cell.GetHashCode(); }
    }

    private sealed class InputCache
    {
        public string context;
        public int width;
        public readonly Dictionary<CellKey, Piece> cells = new Dictionary<CellKey, Piece>();
        public readonly Dictionary<Vector2Int, HashSet<CellKey>> chunks = new Dictionary<Vector2Int, HashSet<CellKey>>();
    }

    private sealed class SourcePixels
    {
        public Hash128 dependency;
        public Color32[] pixels;
        public int width;
        public string fingerprint;
    }

    private sealed class ChunkImage
    {
        public string fingerprint;
        public int left, bottom, width, height;
        public Color32[] pixels;
    }

    private sealed class BackgroundWork
    {
        public CancellationTokenSource cancellation;
        public Task<Dictionary<Vector2Int, ChunkImage>> calculation;
    }

    private sealed class Piece
    {
        public Sprite sprite;
        public Color32[] pixels;
        public int textureWidth;
        public Matrix4x4 matrix, inverse;
        public Rect bounds;
        public Color tint;
        public int order, sequence;
        public Tilemap map;
        public Vector3Int cell;
        public int mapIndex;
        public string fingerprint;
        public Rect spriteRect;
        public Vector2 pivot;
        public float ppu;
    }

    static PlatformOutlineAuthoring()
    {
        Tilemap.tilemapTileChanged += Changed;
        Undo.undoRedoPerformed += RestoreAfterUndo;
        EditorSceneManager.sceneSaving += SaveScene;
        EditorSceneManager.sceneSaved += SavedScene;
        EditorSceneManager.sceneClosing += ReleaseScenePreviews;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        EditorApplication.delayCall += RestoreMissingPreviews;
        EditorApplication.update += RestoreExportsOnIdle;
        AssemblyReloadEvents.beforeAssemblyReload += ClearPending;
        AssemblyReloadEvents.beforeAssemblyReload += SuspendPendingExports;
        paletteWasOpen = UnityEditor.Tilemaps.GridPaintPaletteWindow.isActive;
        EditorApplication.update += WatchPalette;
    }

    private static void Changed(Tilemap map, Tilemap.SyncTile[] changes)
    {
        if (rebuilding || Undo.isProcessing || PlatformOutlineAuthoring.IsPlayModeActive || map == null) return;
        var bake = map.GetComponentInParent<PlatformOutlineBake>();
        if (bake == null) return;
        HashSet<CellKey> cells;
        if (!ChangedCells.TryGetValue(bake, out cells)) ChangedCells[bake] = cells = new HashSet<CellKey>();
        foreach (var change in changes) cells.Add(new CellKey(map, change.position));
        QueueUpdate(bake, Undo.GetCurrentGroup());
    }

    public static void RequestUpdate(PlatformOutlineBake bake)
    {
        if (bake != null && !PlatformOutlineAuthoring.IsPlayModeActive)
        {
            Inputs.Remove(bake); ChangedCells.Remove(bake);
            QueueUpdate(bake, WithoutUndo);
        }
    }

    public static bool NeedsApply(PlatformOutlineBake bake)
    {
        return bake != null && (DirtyBakes.Contains(bake)
            || SessionState.GetBool(DirtySessionPrefix + bake.GetInstanceID(), false) || bake.generatedRoot == null);
    }

    private static void QueueUpdate(PlatformOutlineBake bake, int undoGroup)
    {
        DirtyBakes.Add(bake);
        SessionState.SetBool(DirtySessionPrefix + bake.GetInstanceID(), true);
        CancelBackground(bake);
        if (!bake.rebuildWhileEditing) { Pending.Remove(bake); return; }
        int previous;
        Pending[bake] = Pending.TryGetValue(bake, out previous) && previous == WithoutUndo ? WithoutUndo : undoGroup;
        EditorApplication.update -= Flush;
        EditorApplication.update += Flush;
    }

    private static void WatchPalette()
    {
        bool open = UnityEditor.Tilemaps.GridPaintPaletteWindow.isActive;
        if (paletteWasOpen && !open) EditorApplication.delayCall += ApplyAfterPaletteClosed;
        paletteWasOpen = open;
    }

    private static void ApplyAfterPaletteClosed()
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive || UnityEditor.Tilemaps.GridPaintPaletteWindow.isActive) return;
        // Dirty flags survive script reloads. Opening and closing an unchanged
        // palette must not read every cell just to discover there is no work.
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var bake in Bakes(SceneManager.GetSceneAt(i)))
                if (!bake.rebuildWhileEditing && bake.rebuildWhenPaletteCloses && NeedsApply(bake))
                    ApplyEditedCells(bake, false);
    }

    private static void Flush()
    {
        FlushPending(false);
    }

    private static void FlushPending(bool force)
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) { ClearPending(); return; }
        if (!force && GUIUtility.hotControl != 0) return;
        var pending = new Dictionary<PlatformOutlineBake, int>(Pending);
        if (force)
        {
            ClearPending();
            foreach (var item in pending)
                if (item.Key != null) Rebuild(item.Key, item.Value == Undo.GetCurrentGroup() ? item.Value : WithoutUndo, false, false);
            return;
        }
        foreach (var item in pending)
        {
            var bake = item.Key;
            if (bake == null || !bake.gameObject.scene.isLoaded)
            { CancelBackground(bake); Pending.Remove(bake); continue; }
            if (!bake.rebuildWhileEditing)
            { CancelBackground(bake); Pending.Remove(bake); continue; }
            BackgroundWork work;
            try
            {
                if (!Background.TryGetValue(bake, out work))
                {
                    work = BeginBackground(bake, item.Value);
                    if (work != null) Background[bake] = work;
                    else Pending.Remove(bake);
                    continue;
                }
                if (!work.calculation.IsCompleted) continue;
                Background.Remove(bake); Pending.Remove(bake);
                work.cancellation.Dispose();
                if (work.calculation.IsCanceled) continue;
                completedImages = work.calculation.GetAwaiter().GetResult();
                // Derived output is applied without touching the user's later Undo groups.
                // Rebuild rechecks fingerprints against current Editor data before using it.
                Rebuild(bake, WithoutUndo, false, false);
            }
            catch (Exception exception)
            {
                CancelBackground(bake); Pending.Remove(bake);
                Debug.LogException(exception);
            }
            finally { completedImages = null; }
        }
        if (Pending.Count == 0) EditorApplication.update -= Flush;
    }

    private static void CancelBackground(PlatformOutlineBake bake)
    {
        BackgroundWork work;
        if (!Background.TryGetValue(bake, out work)) return;
        Background.Remove(bake);
        work.cancellation.Cancel(); work.cancellation.Dispose();
    }

    private static BackgroundWork BeginBackground(PlatformOutlineBake bake, int undoGroup)
    {
        var grid = bake.GetComponent<Grid>();
        var maps = bake.sources;
        if (maps == null || maps.Length == 0)
        {
            var sources = new List<Tilemap>();
            foreach (var map in grid.GetComponentsInChildren<Tilemap>())
                if (map.gameObject.activeInHierarchy && map.GetComponent<TilemapCollider2D>() != null) sources.Add(map);
            maps = sources.ToArray();
        }
        PlatformFillAuthoring.OpenLibrary();
        // Keep the first derived root stable before recording the brush Undo snapshot.
        bool convert = maps.Length == 0 || bake.generatedRoot == null;
        foreach (var map in maps)
        {
            if (map == null || SourceGrid(map) != grid) throw new InvalidOperationException("All sources must use this Grid.");
            foreach (var tile in UsedTiles(map)) if (PlatformFillAuthoring.GetFill(tile) != tile) convert = true;
        }
        if (convert)
        {
            PlatformFillAuthoring.Finish();
            if (bake.generatedRoot == null)
            {
                var root = new GameObject("PlatformOutlines");
                SceneManager.MoveGameObjectToScene(root, bake.gameObject.scene);
                root.transform.SetParent(bake.transform, false);
                bake.generatedRoot = root;
                bake.sources = maps;
            }
            Rebuild(bake, undoGroup == Undo.GetCurrentGroup() ? undoGroup : WithoutUndo, false, false);
            return null;
        }
        var pieces = ReadPieces(grid, maps);
        PlatformFillAuthoring.Finish();
        // Existing-root output is derived state and needs no new Undo record.
        // Delayed Tilemap refresh notifications can arrive after an Undo has completed.
        int width = Mathf.Clamp(bake.widthPixels, 1, 64), halo = width + 2, side = ChunkPixels + 2 * halo;
        var chunks = new HashSet<Vector2Int>();
        foreach (var piece in pieces)
            for (int y = Mathf.FloorToInt(piece.bounds.yMin * PixelsPerUnit / ChunkPixels);
                y <= Mathf.FloorToInt((piece.bounds.yMax * PixelsPerUnit - .01f) / ChunkPixels); y++)
                for (int x = Mathf.FloorToInt(piece.bounds.xMin * PixelsPerUnit / ChunkPixels);
                    x <= Mathf.FloorToInt((piece.bounds.xMax * PixelsPerUnit - .01f) / ChunkPixels); x++) chunks.Add(new Vector2Int(x, y));
        var inputs = new Dictionary<Vector2Int, string>();
        Dictionary<Vector2Int, ChunkState> states;
        ChunkStates.TryGetValue(bake, out states);
        foreach (var chunk in chunks)
        {
            string fingerprint = ChunkFingerprint(pieces, chunk.x * ChunkPixels - halo, chunk.y * ChunkPixels - halo, side, width);
            ChunkState previous;
            if (states != null && states.TryGetValue(chunk, out previous) && previous.fingerprint == fingerprint && bake.generatedRoot != null)
            {
                if (previous.spriteName == null) continue;
                var child = bake.generatedRoot.transform.Find("Outline_" + chunk.x + "_" + chunk.y);
                var renderer = child == null ? null : child.GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null && renderer.sprite.texture != null && renderer.sprite.name == previous.spriteName) continue;
            }
            inputs[chunk] = fingerprint;
        }
        if (inputs.Count == 0) { Rebuild(bake, WithoutUndo, false, false); return null; }
        var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        return new BackgroundWork {
            cancellation = cancellation,
            calculation = Task.Run(() => {
                var images = new Dictionary<Vector2Int, ChunkImage>();
                foreach (var input in inputs)
                { token.ThrowIfCancellationRequested(); images[input.Key] = CalculateChunk(pieces, input.Key, width, input.Value, token); }
                return images;
            }, token)
        };
    }

    private static ChunkImage CalculateChunk(List<Piece> pieces, Vector2Int chunk, int width, string fingerprint, CancellationToken cancellation)
    {
        int halo = width + 2, side = ChunkPixels + 2 * halo;
        var coverage = new byte[side * side];
        var ink = new Color32[side * side];
        Rasterize(pieces, chunk.x * ChunkPixels - halo, chunk.y * ChunkPixels - halo, side, coverage, ink, cancellation);
        var distances = DistanceToEmptyInternal(coverage, side, cancellation);
        var pixels = new Color32[ChunkPixels * ChunkPixels];
        int left = ChunkPixels, bottom = ChunkPixels, right = -1, top = -1;
        for (int y = 0; y < ChunkPixels; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < ChunkPixels; x++)
            {
                int sample = (y + halo) * side + x + halo;
                if (coverage[sample] == 0) continue;
                float strength = Mathf.Clamp01(width + .5f - Mathf.Sqrt(distances[sample]));
                var color = ink[sample]; color.a = (byte)Mathf.RoundToInt(strength * coverage[sample] * color.a / 255f);
                pixels[y * ChunkPixels + x] = color;
                if (color.a > 0) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
            }
        }
        var image = new ChunkImage { fingerprint = fingerprint, left = left, bottom = bottom };
        if (right < left) return image;
        image.width = right - left + 1; image.height = top - bottom + 1;
        image.pixels = new Color32[image.width * image.height];
        for (int y = 0; y < image.height; y++) Array.Copy(pixels, (y + bottom) * ChunkPixels + left, image.pixels, y * image.width, image.width);
        return image;
    }

    private static void ClearPending()
    {
        foreach (var bake in new List<PlatformOutlineBake>(Background.Keys)) CancelBackground(bake);
        Pending.Clear();
        EditorApplication.update -= Flush;
    }

    private static PlatformOutlineBake[] Bakes(Scene scene)
    {
        var result = new List<PlatformOutlineBake>();
        foreach (var root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<PlatformOutlineBake>(true));
        return result.ToArray();
    }

    private static void RestoreAfterUndo()
    {
        ClearPending();
        if (PlatformOutlineAuthoring.IsPlayModeActive) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var bake in Bakes(SceneManager.GetSceneAt(i)))
            {
                Inputs.Remove(bake); ChangedCells.Remove(bake);
                QueueUpdate(bake, WithoutUndo);
            }
    }

    private static void ReleaseScenePreviews(Scene scene, bool removingScene)
    {
        // Scene reload must not destroy previews before Unity snapshots them for Play.
        if (PlatformOutlineAuthoring.IsPlayModeActive) return;
        ClearExport(scene);
        foreach (var bake in Bakes(scene))
        {
            ChunkStates.Remove(bake);
            Inputs.Remove(bake); ChangedCells.Remove(bake);
            DirtyBakes.Remove(bake);
            SessionState.EraseBool(DirtySessionPrefix + bake.GetInstanceID());
            CancelBackground(bake); Pending.Remove(bake);
            if (bake.generatedRoot != null)
            {
                var sprites = new List<Sprite>();
                foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true)) sprites.Add(renderer.sprite);
                ReleasePreviews(sprites);
            }
        }
        if (ChunkStates.Count == 0) PixelSources.Clear();
    }

    private static void RestoreMissingPreviews()
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) return;
        RestorePendingExports();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var bake in Bakes(SceneManager.GetSceneAt(i)))
                if (NeedsApply(bake)) QueueUpdate(bake, WithoutUndo);
                else if (bake.generatedRoot != null)
                    foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true))
                        if (renderer.sprite == null) { QueueUpdate(bake, WithoutUndo); break; }
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Play consumes the currently applied previews. Never turn it into
            // an implicit terrain edit or a disk save for any loaded scene.
            ClearPending();
            SuspendPendingExports();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            RestoreMissingPreviews();
            QueueExportCompletion();
        }
    }

    private static void SuspendPendingExports()
    {
        // A delayed save may finish binding after Play or a script reload, but
        // must not inherit permission to save again across either boundary.
        foreach (var pending in PendingExports.Values)
        {
            pending.sceneSaved = false;
            RememberExport(pending);
        }
        // A script reload may have left only a SessionState snapshot.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (string.IsNullOrEmpty(scene.path)) continue;
            string key = ExportSessionPrefix + Path.ChangeExtension(scene.path, ".platformoutlines");
            string json = SessionState.GetString(key, "");
            if (string.IsNullOrEmpty(json)) continue;
            var snapshot = JsonUtility.FromJson<ExportSnapshot>(json);
            if (snapshot == null) continue;
            snapshot.sceneSaved = false;
            SessionState.SetString(key, JsonUtility.ToJson(snapshot));
        }
        EditorApplication.update -= CompletePendingExports;
    }

    private static void SaveScene(Scene scene, string path)
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive || scene.handle == resavingSceneHandle) return;
        foreach (var bake in Bakes(scene)) Rebuild(bake, WithoutUndo, false, false);
        SaveSceneOutlines(scene, path, true);
    }

    private static void SavedScene(Scene scene)
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) return;
        RestorePendingExports();
        PendingExport pending;
        if (PendingExports.TryGetValue(scene.handle, out pending)
            && pending.path == Path.ChangeExtension(scene.path, ".platformoutlines"))
        {
            pending.sceneSaved = true;
            RememberExport(pending);
        }
    }

    [MenuItem("Tools/CASTLING/플랫폼 외곽선/선택한 그리드 전체 재계산")]
    public static void RebuildSelected()
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) throw new InvalidOperationException("편집 모드에서만 사용할 수 있습니다.");
        var selected = Selection.activeGameObject;
        var grid = selected == null ? null : selected.GetComponentInParent<Grid>();
        if (grid == null) throw new InvalidOperationException("지형 Grid 또는 Ground 타일맵을 선택하세요.");
        var bake = grid.GetComponent<PlatformOutlineBake>();
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        if (bake == null) bake = Undo.AddComponent<PlatformOutlineBake>(grid.gameObject);
        Rebuild(bake, group, true);
    }

    public static void Rebuild(PlatformOutlineBake bake, int undoGroup = -1, bool forceFull = false, bool showProgress = true)
    {
        if (rebuilding) return;
        CancelBackground(bake); Pending.Remove(bake);
        if (PlatformOutlineAuthoring.IsPlayModeActive) throw new InvalidOperationException("Edit Mode only.");
        var grid = bake.GetComponent<Grid>();
        if (grid == null) throw new InvalidOperationException("PlatformOutlineBake requires a Grid.");
        int width = Mathf.Clamp(bake.widthPixels, 1, 64);
        int halo = width + 2;
        int side = ChunkPixels + 2 * halo;
        rebuilding = true;
        bool recordUndo = undoGroup != WithoutUndo;
        if (recordUndo && undoGroup < 0) { Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); }
        if (recordUndo) Undo.SetCurrentGroupName("Rebuild platform outlines");
        var newRoot = new GameObject("PlatformOutlines");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(newRoot, bake.gameObject.scene);
        newRoot.transform.SetParent(bake.transform, false);
        var sprites = new Dictionary<string, Sprite>();
        var previousSprites = new List<Sprite>();
        if (bake.generatedRoot != null)
            foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true)) previousSprites.Add(renderer.sprite);
        var retained = new HashSet<Transform>();
        Dictionary<Vector2Int, ChunkState> previousStates;
        ChunkStates.TryGetValue(bake, out previousStates);
        if (!forceFull && previousStates != null)
            foreach (var sprite in previousSprites)
                if (sprite != null && sprite.texture != null) sprites[sprite.name] = sprite;
        var nextStates = new Dictionary<Vector2Int, ChunkState>();
        try
        {
            PlatformFillAuthoring.OpenLibrary();
            var maps = bake.sources;
            if (maps == null || maps.Length == 0)
            {
                var list = new List<Tilemap>();
                foreach (var map in grid.GetComponentsInChildren<Tilemap>())
                    if (map.gameObject.activeInHierarchy && map.GetComponent<TilemapCollider2D>() != null) list.Add(map);
                maps = list.ToArray();
            }
            if (maps.Length == 0) throw new InvalidOperationException("No terrain Tilemaps on this Grid.");
            // Resolve everything before changing authored cells, so unsupported tiles fail safely.
            var replacements = new Dictionary<TileBase, Tile>();
            foreach (var map in maps)
            {
                if (map == null || SourceGrid(map) != grid) throw new InvalidOperationException("All sources must use this Grid.");
                foreach (var source in map.GetUsedTilesCount() == 0 ? new TileBase[0] : UsedTiles(map))
                    replacements[source] = PlatformFillAuthoring.GetFill(source);
            }
            foreach (var map in maps)
            {
                if (recordUndo) Undo.RegisterCompleteObjectUndo(map, "Rebuild platform outlines");
                bool replaced = false;
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    var source = map.GetTile(cell); if (source == null) continue;
                    var fill = replacements[source]; if (source == fill) continue;
                    var matrix = map.GetTransformMatrix(cell); var color = map.GetColor(cell);
                    map.SetTile(cell, fill); map.SetTileFlags(cell, TileFlags.None);
                    map.SetTransformMatrix(cell, matrix); map.SetColor(cell, color);
                    replaced = true;
                }
                if (replaced || forceFull) map.RefreshAllTiles();
            }
            var pieces = ReadPieces(grid, maps);
            var chunks = new HashSet<Vector2Int>();
            foreach (var piece in pieces)
            {
                int minX = Mathf.FloorToInt(piece.bounds.xMin * PixelsPerUnit / ChunkPixels);
                int maxX = Mathf.FloorToInt((piece.bounds.xMax * PixelsPerUnit - .01f) / ChunkPixels);
                int minY = Mathf.FloorToInt(piece.bounds.yMin * PixelsPerUnit / ChunkPixels);
                int maxY = Mathf.FloorToInt((piece.bounds.yMax * PixelsPerUnit - .01f) / ChunkPixels);
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) chunks.Add(new Vector2Int(x, y));
            }
            int finished = 0;
            foreach (var chunk in chunks)
            {
                int originX = chunk.x * ChunkPixels - halo, originY = chunk.y * ChunkPixels - halo;
                var fingerprint = ChunkFingerprint(pieces, originX, originY, side, width);
                ChunkState previous;
                string childName = "Outline_" + chunk.x + "_" + chunk.y;
                var existing = bake.generatedRoot == null ? null : bake.generatedRoot.transform.Find(childName);
                var existingRenderer = existing == null ? null : existing.GetComponent<SpriteRenderer>();
                if (!forceFull && previousStates != null && previousStates.TryGetValue(chunk, out previous)
                    && previous.fingerprint == fingerprint && bake.generatedRoot != null)
                {
                    if (previous.spriteName == null) { nextStates[chunk] = previous; continue; }
                    if (existingRenderer != null && existingRenderer.sprite != null && existingRenderer.sprite.texture != null
                        && existingRenderer.sprite.name == previous.spriteName)
                    {
                        var source = maps[0].GetComponent<TilemapRenderer>();
                        existingRenderer.sharedMaterial = source.sharedMaterial;
                        existingRenderer.sortingLayerID = source.sortingLayerID; existingRenderer.sortingOrder = source.sortingOrder;
                        sprites[previous.spriteName] = existingRenderer.sprite;
                        retained.Add(existing); nextStates[chunk] = previous; continue;
                    }
                }
                if (showProgress)
                    EditorUtility.DisplayProgressBar("플랫폼 외곽선", "노출 경계 계산 중", (float)finished / chunks.Count);
                finished++;
                var state = new ChunkState { fingerprint = fingerprint };
                nextStates[chunk] = state;
                ChunkImage image;
                if (completedImages == null || !completedImages.TryGetValue(chunk, out image) || image.fingerprint != fingerprint)
                    image = CalculateChunk(pieces, chunk, width, fingerprint, CancellationToken.None);
                if (image.pixels == null) continue;
                var texture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false) {
                    name = "PlatformOutline", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels32(image.pixels);
                var sprite = CreatePreview(texture, sprites);
                state.spriteName = sprite.name;
                var child = new GameObject(childName, typeof(SpriteRenderer));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child, newRoot.scene);
                child.transform.SetParent(newRoot.transform, false);
                child.transform.localPosition = new Vector3(chunk.x * 8 + (float)image.left / PixelsPerUnit, chunk.y * 8 + (float)image.bottom / PixelsPerUnit, -.01f);
                var renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                var sourceRenderer = maps[0].GetComponent<TilemapRenderer>();
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder;
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }
            if (recordUndo) Undo.RegisterCompleteObjectUndo(bake, "Rebuild platform outlines");
            // Generated children are derived state; Undo restores the tiles and recalculates them.
            // Keeping the root stable avoids Undo references to deleted preview objects.
            if (bake.generatedRoot != null)
            {
                while (newRoot.transform.childCount > 0)
                {
                    var child = newRoot.transform.GetChild(0);
                    var existing = bake.generatedRoot.transform.Find(child.name);
                    if (existing != null && existing.GetComponent<SpriteRenderer>() != null)
                    {
                        var source = child.GetComponent<SpriteRenderer>();
                        var renderer = existing.GetComponent<SpriteRenderer>();
                        renderer.sprite = source.sprite; renderer.sharedMaterial = source.sharedMaterial;
                        renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
                        existing.localPosition = child.localPosition;
                        retained.Add(existing); Object.DestroyImmediate(child.gameObject);
                    }
                    else { child.SetParent(bake.generatedRoot.transform, false); retained.Add(child); }
                }
                for (int i = bake.generatedRoot.transform.childCount - 1; i >= 0; i--)
                    if (!retained.Contains(bake.generatedRoot.transform.GetChild(i))) Object.DestroyImmediate(bake.generatedRoot.transform.GetChild(i).gameObject);
                Object.DestroyImmediate(newRoot);
                var liveSprites = new List<Sprite>();
                foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true)) liveSprites.Add(renderer.sprite);
                ReleasePreviews(previousSprites, liveSprites);
            }
            else
            {
                if (recordUndo) Undo.RegisterCreatedObjectUndo(newRoot, "Rebuild platform outlines");
                bake.generatedRoot = newRoot;
            }
            bake.sources = maps;
            EditorUtility.SetDirty(bake);
            PlatformFillAuthoring.Finish();
            if (recordUndo) Undo.CollapseUndoOperations(undoGroup);
            ChunkStates[bake] = nextStates;
            Inputs[bake] = BuildInputCache(grid, maps, pieces, width);
            ChangedCells.Remove(bake);
            DirtyBakes.Remove(bake);
            SessionState.EraseBool(DirtySessionPrefix + bake.GetInstanceID());
        }
        catch
        {
            if (newRoot != null) Object.DestroyImmediate(newRoot);
            ReleasePreviews(sprites.Values, previousSprites);
            if (recordUndo) Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
        finally { if (showProgress) EditorUtility.ClearProgressBar(); rebuilding = false; }
    }

    public static void ApplyEditedCells(PlatformOutlineBake bake, bool showProgress = true)
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) throw new InvalidOperationException("Edit Mode only.");
        if (!TryRebuildChangedCells(bake, showProgress)) Rebuild(bake, showProgress ? -1 : WithoutUndo, false, showProgress);
    }

    private static bool TryRebuildChangedCells(PlatformOutlineBake bake, bool showProgress)
    {
        InputCache input;
        HashSet<CellKey> changed;
        Dictionary<Vector2Int, ChunkState> previousStates;
        if (rebuilding || bake == null || bake.generatedRoot == null || bake.sources == null || bake.sources.Length == 0
            || !Inputs.TryGetValue(bake, out input) || !ChangedCells.TryGetValue(bake, out changed) || changed.Count == 0
            || !ChunkStates.TryGetValue(bake, out previousStates)) return false;
        var grid = bake.GetComponent<Grid>();
        var maps = bake.sources;
        int width = Mathf.Clamp(bake.widthPixels, 1, 64);
        if (grid == null || input.context != InputContext(grid, maps, width)) return false;
        var previousSprites = new List<Sprite>();
        var sprites = new Dictionary<string, Sprite>();
        var output = new Dictionary<string, SpriteRenderer>();
        foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null || renderer.sprite.texture == null) return false;
            previousSprites.Add(renderer.sprite); sprites[renderer.sprite.name] = renderer.sprite;
            output[renderer.name] = renderer;
        }
        foreach (var state in previousStates)
        {
            if (state.Value.spriteName == null) continue;
            SpriteRenderer renderer;
            if (!output.TryGetValue("Outline_" + state.Key.x + "_" + state.Key.y, out renderer)
                || renderer.sprite.name != state.Value.spriteName) return false;
        }
        var selected = new Dictionary<Tilemap, HashSet<Vector3Int>>();
        foreach (var key in changed)
        {
            if (Array.IndexOf(maps, key.map) < 0) continue;
            HashSet<Vector3Int> cells;
            if (!selected.TryGetValue(key.map, out cells)) selected[key.map] = cells = new HashSet<Vector3Int>();
            cells.Add(key.cell);
        }
        CancelBackground(bake); Pending.Remove(bake);
        rebuilding = true;
        var staging = new GameObject("PlatformOutlineUpdates");
        SceneManager.MoveGameObjectToScene(staging, bake.gameObject.scene);
        staging.transform.SetParent(bake.transform, false);
        var affected = new HashSet<Vector2Int>();
        var nextStates = new Dictionary<Vector2Int, ChunkState>(previousStates);
        var updated = new HashSet<Vector2Int>();
        try
        {
            PlatformFillAuthoring.OpenLibrary();
            // Resolve just the changed cells before replacing any authored tile.
            var fills = new Dictionary<TileBase, Tile>();
            foreach (var selection in selected)
                foreach (var cell in selection.Value)
                {
                    var tile = selection.Key.GetTile(cell);
                    if (tile != null && !fills.ContainsKey(tile)) fills[tile] = PlatformFillAuthoring.GetFill(tile);
                }
            foreach (var selection in selected)
                foreach (var cell in selection.Value)
                {
                    var tile = selection.Key.GetTile(cell);
                    if (tile == null || fills[tile] == tile) continue;
                    var matrix = selection.Key.GetTransformMatrix(cell); var color = selection.Key.GetColor(cell);
                    selection.Key.SetTile(cell, fills[tile]); selection.Key.SetTileFlags(cell, TileFlags.None);
                    selection.Key.SetTransformMatrix(cell, matrix); selection.Key.SetColor(cell, color);
                }
            var replacements = ReadPieces(grid, maps, selected);
            foreach (var key in changed)
            {
                Piece old;
                if (!input.cells.TryGetValue(key, out old)) continue;
                foreach (var chunk in CoveredChunks(old, width))
                {
                    affected.Add(chunk);
                    var bucket = input.chunks[chunk]; bucket.Remove(key);
                    if (bucket.Count == 0) input.chunks.Remove(chunk);
                }
                input.cells.Remove(key);
            }
            foreach (var piece in replacements)
            {
                var key = new CellKey(piece.map, piece.cell);
                input.cells[key] = piece;
                foreach (var chunk in CoveredChunks(piece, width))
                {
                    affected.Add(chunk);
                    HashSet<CellKey> bucket;
                    if (!input.chunks.TryGetValue(chunk, out bucket)) input.chunks[chunk] = bucket = new HashSet<CellKey>();
                    bucket.Add(key);
                }
            }
            int finished = 0;
            foreach (var chunk in affected)
            {
                var pieces = new List<Piece>();
                HashSet<CellKey> bucket;
                if (input.chunks.TryGetValue(chunk, out bucket))
                    foreach (var key in bucket) pieces.Add(input.cells[key]);
                pieces.Sort(ComparePieces);
                int halo = width + 2;
                var fingerprint = ChunkFingerprint(pieces, chunk.x * ChunkPixels - halo, chunk.y * ChunkPixels - halo,
                    ChunkPixels + 2 * halo, width);
                ChunkState previous;
                if (previousStates.TryGetValue(chunk, out previous) && previous.fingerprint == fingerprint) continue;
                if (showProgress) EditorUtility.DisplayProgressBar("플랫폼 외곽선", "변경한 셀 적용 중", (float)finished / affected.Count);
                finished++;
                var image = HasCoreGeometry(pieces, chunk)
                    ? CalculateChunk(pieces, chunk, width, fingerprint, CancellationToken.None) : new ChunkImage { fingerprint = fingerprint };
                var state = new ChunkState { fingerprint = fingerprint };
                nextStates[chunk] = state; updated.Add(chunk);
                if (image.pixels == null) continue;
                var texture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
                texture.SetPixels32(image.pixels);
                var sprite = CreatePreview(texture, sprites); state.spriteName = sprite.name;
                var child = new GameObject("Outline_" + chunk.x + "_" + chunk.y, typeof(SpriteRenderer));
                SceneManager.MoveGameObjectToScene(child, staging.scene); child.transform.SetParent(staging.transform, false);
                child.transform.localPosition = new Vector3(chunk.x * 8 + (float)image.left / PixelsPerUnit,
                    chunk.y * 8 + (float)image.bottom / PixelsPerUnit, -.01f);
                var renderer = child.GetComponent<SpriteRenderer>(); var source = maps[0].GetComponent<TilemapRenderer>();
                renderer.sprite = sprite; renderer.sharedMaterial = source.sharedMaterial;
                renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
            }
            PlatformFillAuthoring.Finish();
            var context = InputContext(grid, maps, width);
            foreach (var chunk in updated)
            {
                string name = "Outline_" + chunk.x + "_" + chunk.y;
                var existing = bake.generatedRoot.transform.Find(name); var replacement = staging.transform.Find(name);
                if (replacement == null) { if (existing != null) Object.DestroyImmediate(existing.gameObject); continue; }
                if (existing != null && existing.GetComponent<SpriteRenderer>() != null)
                {
                    var renderer = existing.GetComponent<SpriteRenderer>(); var source = replacement.GetComponent<SpriteRenderer>();
                    renderer.sprite = source.sprite; renderer.sharedMaterial = source.sharedMaterial;
                    renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
                    existing.localPosition = replacement.localPosition; Object.DestroyImmediate(replacement.gameObject);
                }
                else replacement.SetParent(bake.generatedRoot.transform, false);
            }
            Object.DestroyImmediate(staging);
            var live = new List<Sprite>();
            foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true)) live.Add(renderer.sprite);
            ReleasePreviews(previousSprites, live);
            input.context = context; ChunkStates[bake] = nextStates;
            ChangedCells.Remove(bake); DirtyBakes.Remove(bake);
            SessionState.EraseBool(DirtySessionPrefix + bake.GetInstanceID()); EditorUtility.SetDirty(bake);
            return true;
        }
        catch
        {
            Inputs.Remove(bake);
            if (staging != null) Object.DestroyImmediate(staging);
            ReleasePreviews(sprites.Values, previousSprites);
            throw;
        }
        finally { PlatformFillAuthoring.Finish(); if (showProgress) EditorUtility.ClearProgressBar(); rebuilding = false; }
    }

    private static InputCache BuildInputCache(Grid grid, Tilemap[] maps, List<Piece> pieces, int width)
    {
        var input = new InputCache { context = InputContext(grid, maps, width), width = width };
        foreach (var piece in pieces)
        {
            var key = new CellKey(piece.map, piece.cell); input.cells[key] = piece;
            foreach (var chunk in CoveredChunks(piece, width))
            {
                HashSet<CellKey> bucket;
                if (!input.chunks.TryGetValue(chunk, out bucket)) input.chunks[chunk] = bucket = new HashSet<CellKey>();
                bucket.Add(key);
            }
        }
        return input;
    }

    private static IEnumerable<Vector2Int> CoveredChunks(Piece piece, int width)
    {
        int halo = width + 2;
        int minX = Mathf.FloorToInt((float)(Mathf.FloorToInt(piece.bounds.xMin * PixelsPerUnit) - halo) / ChunkPixels);
        int minY = Mathf.FloorToInt((float)(Mathf.FloorToInt(piece.bounds.yMin * PixelsPerUnit) - halo) / ChunkPixels);
        int maxX = Mathf.FloorToInt((float)(Mathf.CeilToInt(piece.bounds.xMax * PixelsPerUnit) + halo - 1) / ChunkPixels);
        int maxY = Mathf.FloorToInt((float)(Mathf.CeilToInt(piece.bounds.yMax * PixelsPerUnit) + halo - 1) / ChunkPixels);
        for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) yield return new Vector2Int(x, y);
    }

    private static bool HasCoreGeometry(List<Piece> pieces, Vector2Int chunk)
    {
        foreach (var piece in pieces)
            if (Mathf.FloorToInt(piece.bounds.xMin * PixelsPerUnit) < (chunk.x + 1) * ChunkPixels
                && Mathf.CeilToInt(piece.bounds.xMax * PixelsPerUnit) > chunk.x * ChunkPixels
                && Mathf.FloorToInt(piece.bounds.yMin * PixelsPerUnit) < (chunk.y + 1) * ChunkPixels
                && Mathf.CeilToInt(piece.bounds.yMax * PixelsPerUnit) > chunk.y * ChunkPixels) return true;
        return false;
    }

    private static int ComparePieces(Piece a, Piece b)
    {
        int order = a.order.CompareTo(b.order); if (order != 0) return order;
        order = a.mapIndex.CompareTo(b.mapIndex); if (order != 0) return order;
        order = a.cell.z.CompareTo(b.cell.z); if (order != 0) return order;
        order = a.cell.y.CompareTo(b.cell.y); return order != 0 ? order : a.cell.x.CompareTo(b.cell.x);
    }

    private static Grid SourceGrid(Tilemap map)
    {
        // Inactive Tilemaps lose their native Grid link; their hierarchy still owns it.
        return map.layoutGrid != null ? map.layoutGrid : map.GetComponentInParent<Grid>(true);
    }

    private static string InputContext(Grid grid, Tilemap[] maps, int width)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(width); writer.Write((int)grid.cellLayout); writer.Write((int)grid.cellSwizzle);
                foreach (var vector in new[] { grid.cellSize, grid.cellGap }) { writer.Write(vector.x); writer.Write(vector.y); writer.Write(vector.z); }
                writer.Write(AssetDatabase.GetAssetDependencyHash(PlatformFillAuthoring.LibraryPath).ToString());
                foreach (var map in maps)
                {
                    if (map == null || SourceGrid(map) != grid) return null;
                    writer.Write(map.GetInstanceID());
                    foreach (var matrix in new[] { grid.transform.worldToLocalMatrix * map.transform.localToWorldMatrix, map.orientationMatrix })
                        for (int i = 0; i < 16; i++) writer.Write(matrix[i]);
                    var anchor = map.tileAnchor; writer.Write(anchor.x); writer.Write(anchor.y); writer.Write(anchor.z);
                    var color = map.color; writer.Write(color.r); writer.Write(color.g); writer.Write(color.b); writer.Write(color.a);
                    var renderer = map.GetComponent<TilemapRenderer>();
                    if (renderer == null) return null;
                    writer.Write(renderer.sortingOrder); writer.Write(renderer.sortingLayerID);
                    writer.Write(renderer.sharedMaterial == null ? 0 : renderer.sharedMaterial.GetInstanceID());
                }
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));
        }
    }

    private static IEnumerable<Vector3Int> AllCells(Tilemap map)
    {
        foreach (var cell in map.cellBounds.allPositionsWithin) yield return cell;
    }

    private static TileBase[] UsedTiles(Tilemap map)
    {
        var tiles = new TileBase[map.GetUsedTilesCount()]; map.GetUsedTilesNonAlloc(tiles); return tiles;
    }

    private static List<Piece> ReadPieces(Grid grid, Tilemap[] maps, Dictionary<Tilemap, HashSet<Vector3Int>> selected = null)
    {
        var result = new List<Piece>();
        var cache = new Dictionary<Texture2D, SourcePixels>();
        for (int mapIndex = 0; mapIndex < maps.Length; mapIndex++)
        {
            var map = maps[mapIndex];
            HashSet<Vector3Int> selectedCells = null;
            if (selected != null && !selected.TryGetValue(map, out selectedCells)) continue;
            var renderer = map.GetComponent<TilemapRenderer>();
            IEnumerable<Vector3Int> cells = selected == null ? AllCells(map) : selectedCells;
            foreach (var cell in cells)
            {
                var tile = map.GetTile<Tile>(cell); if (tile == null || tile.sprite == null) continue;
                var sprite = tile.sprite;
                SourcePixels source;
                if (!cache.TryGetValue(sprite.texture, out source))
                {
                    string path = AssetDatabase.GetAssetPath(sprite.texture);
                    var dependency = string.IsNullOrEmpty(path) ? new Hash128() : AssetDatabase.GetAssetDependencyHash(path);
                    if (string.IsNullOrEmpty(path) || !PixelSources.TryGetValue(sprite.texture, out source) || source.dependency != dependency)
                    {
                        var raw = PlatformFillAuthoring.ReadTexture(sprite.texture);
                        source = new SourcePixels { dependency = dependency, pixels = raw.GetPixels32(), width = raw.width };
                        using (var sha = SHA256.Create()) source.fingerprint = BitConverter.ToString(sha.ComputeHash(raw.GetRawTextureData()));
                        if (!string.IsNullOrEmpty(path)) PixelSources[sprite.texture] = source;
                    }
                    cache[sprite.texture] = source;
                }
                // Native Tilemap cell conversion also returns zero while its hierarchy is inactive.
                var cellPosition = map.layoutGrid != null
                    ? map.CellToLocalInterpolated((Vector3)cell + map.tileAnchor)
                    : grid.CellToLocalInterpolated((Vector3)cell + map.tileAnchor);
                var matrix = grid.transform.worldToLocalMatrix * map.transform.localToWorldMatrix
                    * Matrix4x4.Translate(cellPosition) * map.GetTransformMatrix(cell);
                var bounds = sprite.bounds;
                var a = matrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.min.y));
                var b = matrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y));
                var c = matrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y));
                var d = matrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y));
                var piece = new Piece { sprite = sprite, pixels = source.pixels,
                    map = map, cell = cell, mapIndex = mapIndex,
                    spriteRect = sprite.rect, pivot = sprite.pivot, ppu = sprite.pixelsPerUnit,
                    textureWidth = source.width, matrix = matrix, inverse = matrix.inverse,
                    bounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x, c.x, d.x), Mathf.Min(a.y, b.y, c.y, d.y),
                        Mathf.Max(a.x, b.x, c.x, d.x), Mathf.Max(a.y, b.y, c.y, d.y)),
                    tint = map.GetColor(cell) * map.color, order = renderer.sortingOrder, sequence = result.Count };
                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                    {
                        writer.Write(map.GetInstanceID()); writer.Write(cell.x); writer.Write(cell.y); writer.Write(cell.z);
                        writer.Write(sprite.GetInstanceID()); writer.Write(source.fingerprint);
                        writer.Write(piece.textureWidth); writer.Write(sprite.pixelsPerUnit);
                        writer.Write(sprite.rect.x); writer.Write(sprite.rect.y); writer.Write(sprite.rect.width); writer.Write(sprite.rect.height);
                        writer.Write(sprite.pivot.x); writer.Write(sprite.pivot.y);
                        writer.Write(bounds.min.x); writer.Write(bounds.min.y); writer.Write(bounds.max.x); writer.Write(bounds.max.y);
                        for (int i = 0; i < 16; i++) writer.Write(matrix[i]);
                        writer.Write(piece.tint.r); writer.Write(piece.tint.g); writer.Write(piece.tint.b); writer.Write(piece.tint.a);
                        writer.Write(piece.order);
                    }
                    using (var sha = SHA256.Create()) piece.fingerprint = BitConverter.ToString(sha.ComputeHash(stream.ToArray()));
                }
                result.Add(piece);
            }
        }
        // Equal sorting orders retain Tilemap/cell traversal order, so a distant edit
        // cannot shuffle unchanged contributors and invalidate every chunk.
        result.Sort(ComparePieces);
        return result;
    }

    private static string ChunkFingerprint(List<Piece> pieces, int originX, int originY, int side, int width)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(originX); writer.Write(originY); writer.Write(side); writer.Write(width);
                foreach (var piece in pieces)
                {
                    // Use the same integer pixel footprint as Rasterize, including the halo.
                    if (Mathf.FloorToInt(piece.bounds.xMin * PixelsPerUnit) >= originX + side
                        || Mathf.CeilToInt(piece.bounds.xMax * PixelsPerUnit) <= originX
                        || Mathf.FloorToInt(piece.bounds.yMin * PixelsPerUnit) >= originY + side
                        || Mathf.CeilToInt(piece.bounds.yMax * PixelsPerUnit) <= originY) continue;
                    writer.Write(piece.fingerprint);
                }
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));
        }
    }

    private static void Rasterize(List<Piece> pieces, int originX, int originY, int side, byte[] coverage, Color32[] ink, CancellationToken cancellation)
    {
        foreach (var piece in pieces)
        {
            cancellation.ThrowIfCancellationRequested();
            int minX = Mathf.Clamp(Mathf.FloorToInt(piece.bounds.xMin * PixelsPerUnit) - originX, 0, side);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(piece.bounds.xMax * PixelsPerUnit) - originX, 0, side);
            int minY = Mathf.Clamp(Mathf.FloorToInt(piece.bounds.yMin * PixelsPerUnit) - originY, 0, side);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(piece.bounds.yMax * PixelsPerUnit) - originY, 0, side);
            var rect = piece.spriteRect;
            float ppu = piece.ppu;
            var pivot = piece.pivot;
            Color32 darkInk = (Color)new Color32(92, 96, 95, 255) * piece.tint;
            Color32 lightInk = (Color)new Color32(255, 248, 236, 255) * piece.tint;
            for (int y = minY; y < maxY; y++) for (int x = minX; x < maxX; x++)
            {
                var local = piece.inverse.MultiplyPoint3x4(new Vector3((originX + x + .5f) / PixelsPerUnit, (originY + y + .5f) / PixelsPerUnit));
                int px = Mathf.FloorToInt(local.x * ppu + pivot.x);
                int py = Mathf.FloorToInt(local.y * ppu + pivot.y);
                if (px < 0 || py < 0 || px >= rect.width || py >= rect.height) continue;
                var pixel = piece.pixels[(py + (int)rect.y) * piece.textureWidth + px + (int)rect.x];
                if (pixel.a == 0 || piece.tint.a <= 0) continue;
                int index = y * side + x;
                if (pixel.a > coverage[index]) coverage[index] = pixel.a;
                ink[index] = pixel.r < 160 ? darkInk : lightInk;
            }
        }
    }

    // Exact squared Euclidean distance transform, in two separable linear passes.
    public static float[] DistanceToEmpty(byte[] coverage, int side)
    {
        return DistanceToEmptyInternal(coverage, side, CancellationToken.None);
    }

    private static float[] DistanceToEmptyInternal(byte[] coverage, int side, CancellationToken cancellation)
    {
        var output = new float[coverage.Length];
        var input = new float[side]; var result = new float[side];
        var vertices = new int[side]; var intersections = new float[side + 1];
        for (int y = 0; y < side; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < side; x++) input[x] = coverage[y * side + x] < 128 ? 0 : 1e12f;
            DistanceLine(input, result, vertices, intersections);
            for (int x = 0; x < side; x++) output[y * side + x] = result[x];
        }
        for (int x = 0; x < side; x++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int y = 0; y < side; y++) input[y] = output[y * side + x];
            DistanceLine(input, result, vertices, intersections);
            for (int y = 0; y < side; y++) output[y * side + x] = result[y];
        }
        return output;
    }

    private static void DistanceLine(float[] input, float[] result, int[] vertices, float[] intersections)
    {
        int k = 0; vertices[0] = 0; intersections[0] = float.NegativeInfinity; intersections[1] = float.PositiveInfinity;
        for (int q = 1; q < input.Length; q++)
        {
            float s;
            do
            {
                int v = vertices[k];
                s = ((input[q] + q * q) - (input[v] + v * v)) / (2f * (q - v));
                if (s > intersections[k]) break;
                k--;
            } while (k >= 0);
            k++; vertices[k] = q; intersections[k] = s; intersections[k + 1] = float.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < input.Length; q++)
        {
            while (intersections[k + 1] < q) k++;
            float distance = q - vertices[k]; result[q] = distance * distance + input[vertices[k]];
        }
    }

    private static string SpriteName(Texture2D texture)
    {
        using (var sha = SHA256.Create())
            return "Outline_" + BitConverter.ToString(sha.ComputeHash(ImageConversion.EncodeToPNG(texture))).Replace("-", "");
    }

    private static Sprite CreatePreview(Texture2D texture, Dictionary<string, Sprite> sprites)
    {
        texture = PlatformOutlineImporter.PrepareTexture(texture);
        string name = SpriteName(texture);
        Sprite sprite;
        if (sprites.TryGetValue(name, out sprite)) { Object.DestroyImmediate(texture); return sprite; }
        // Keep previews serializable by Unity's temporary Play snapshot, including untitled scenes.
        // The sceneSaving callback replaces them with imported subassets before a disk save.
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        sprites.Add(name, sprite);
        return sprite;
    }

    private static void ReleasePreviews(IEnumerable<Sprite> sprites, IEnumerable<Sprite> preserved = null)
    {
        var keep = preserved == null ? new HashSet<Sprite>() : new HashSet<Sprite>(preserved);
        var keptTextures = new HashSet<Texture2D>();
        foreach (var sprite in keep) if (sprite != null) keptTextures.Add(sprite.texture);
        var textures = new HashSet<Texture2D>();
        foreach (var sprite in new HashSet<Sprite>(sprites))
        {
            if (sprite == null || keep.Contains(sprite) || EditorUtility.IsPersistent(sprite)) continue;
            textures.Add(sprite.texture); Object.DestroyImmediate(sprite);
        }
        foreach (var texture in textures)
            if (texture != null && !keptTextures.Contains(texture) && !EditorUtility.IsPersistent(texture)) Object.DestroyImmediate(texture);
    }

    private static void SaveSceneOutlines(Scene scene, string scenePath, bool prune)
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) return;
        ClearExport(scene);
        var renderers = new List<SpriteRenderer>();
        foreach (var bake in Bakes(scene))
            if (bake.generatedRoot != null) renderers.AddRange(bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true));
        string path = Path.ChangeExtension(scenePath, ".platformoutlines");
        if (renderers.Count == 0 && !File.Exists(path)) return;
        var images = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var previous = AssetDatabase.LoadAssetAtPath<PlatformOutlineAsset>(path);
        if (!prune && previous != null)
            foreach (var sprite in previous.sprites) images[sprite.name] = ImageConversion.EncodeToPNG(sprite.texture);
        var names = new List<string>();
        var previews = new List<Sprite>();
        foreach (var renderer in renderers)
        {
            if (renderer.sprite == null) throw new InvalidOperationException("Missing platform outline. Rebuild before saving.");
            var texture = PlatformFillAuthoring.ReadTexture(renderer.sprite.texture);
            string name = SpriteName(texture);
            if (!images.ContainsKey(name)) images.Add(name, ImageConversion.EncodeToPNG(texture));
            names.Add(name); previews.Add(renderer.sprite);
        }
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write("CASTLING_PLATFORM_OUTLINES"); writer.Write(1); writer.Write(images.Count);
                foreach (var image in images) { writer.Write(image.Key); writer.Write(image.Value.Length); writer.Write(image.Value); }
            }
            bytes = stream.ToArray();
        }
        bool changed = !File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), bytes);
        if (changed)
        {
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
        Sprite[] savedSprites;
        if (!TryGetSavedSprites(path, names, out savedSprites))
        {
            // A previous save may have written these bytes while imports were
            // suspended. Identical file contents do not prove the asset is current.
            if (!changed)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            if (!TryGetSavedSprites(path, names, out savedSprites))
            {
                // Unity can defer imports during a save/asset-editing batch even
                // with ForceSynchronousImport. Keep serializable previews intact
                // until the whole imported set is ready; never bind a partial set.
                PendingExports[scene.handle] = new PendingExport {
                    scene = scene, path = path, renderers = renderers, names = names, previews = previews
                };
                RememberExport(PendingExports[scene.handle]);
                QueueExportCompletion();
                PlatformFillAuthoring.Finish();
                return;
            }
        }
        BindSavedSprites(renderers, savedSprites, previews);
        PlatformFillAuthoring.Finish();
    }

    private static bool TryGetSavedSprites(string path, List<string> names, out Sprite[] sprites)
    {
        sprites = new Sprite[names.Count];
        var saved = AssetDatabase.LoadAssetAtPath<PlatformOutlineAsset>(path);
        if (saved == null || saved.sprites == null) return false;
        var byName = new Dictionary<string, Sprite>();
        foreach (var sprite in saved.sprites) if (sprite != null) byName[sprite.name] = sprite;
        for (int i = 0; i < names.Count; i++)
            if (!byName.TryGetValue(names[i], out sprites[i])) return false;
        return true;
    }

    private static bool BindSavedSprites(List<SpriteRenderer> renderers, Sprite[] sprites, List<Sprite> previews)
    {
        bool changed = false;
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i].sprite != sprites[i]) { renderers[i].sprite = sprites[i]; changed = true; }
        ReleasePreviews(previews);
        return changed;
    }

    private static void QueueExportCompletion()
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) return;
        EditorApplication.update -= CompletePendingExports;
        EditorApplication.update += CompletePendingExports;
    }

    private static void RestoreExportsOnIdle()
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive || EditorApplication.isUpdating || EditorApplication.isCompiling) return;
        EditorApplication.update -= RestoreExportsOnIdle;
        RestorePendingExports();
    }

    private static void RememberExport(PendingExport pending)
    {
        SessionState.SetString(ExportSessionPrefix + pending.path,
            JsonUtility.ToJson(new ExportSnapshot { names = pending.names.ToArray(), sceneSaved = pending.sceneSaved }));
    }

    private static void ClearExport(Scene scene)
    {
        PendingExport pending;
        if (PendingExports.TryGetValue(scene.handle, out pending))
            SessionState.EraseString(ExportSessionPrefix + pending.path);
        PendingExports.Remove(scene.handle);
        if (!string.IsNullOrEmpty(scene.path))
            SessionState.EraseString(ExportSessionPrefix + Path.ChangeExtension(scene.path, ".platformoutlines"));
    }

    private static void RestorePendingExports()
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (string.IsNullOrEmpty(scene.path) || PendingExports.ContainsKey(scene.handle)) continue;
            string path = Path.ChangeExtension(scene.path, ".platformoutlines");
            string json = SessionState.GetString(ExportSessionPrefix + path, "");
            if (string.IsNullOrEmpty(json)) continue;
            var snapshot = JsonUtility.FromJson<ExportSnapshot>(json);
            var renderers = new List<SpriteRenderer>();
            foreach (var bake in Bakes(scene))
                if (bake.generatedRoot != null) renderers.AddRange(bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true));
            bool valid = snapshot != null && snapshot.names != null && snapshot.names.Length == renderers.Count;
            var previews = new List<Sprite>();
            for (int j = 0; valid && j < renderers.Count; j++)
            {
                var sprite = renderers[j].sprite;
                valid = sprite != null && sprite.name == snapshot.names[j];
                previews.Add(sprite);
            }
            if (!valid) { SessionState.EraseString(ExportSessionPrefix + path); continue; }
            PendingExports[scene.handle] = new PendingExport {
                scene = scene, path = path, renderers = renderers, names = new List<string>(snapshot.names),
                // A restored request belongs to an earlier lifecycle. Only a
                // new explicit sceneSaved event may grant permission to resave.
                previews = previews, sceneSaved = false
            };
            RememberExport(PendingExports[scene.handle]);
            QueueExportCompletion();
        }
    }

    internal static void ImportedOutlines(string[] paths)
    {
        if (PlatformOutlineAuthoring.IsPlayModeActive) return;
        foreach (var pending in PendingExports.Values)
            if (Array.IndexOf(paths, pending.path) >= 0) { QueueExportCompletion(); return; }
    }

    private static void CompletePendingExports()
    {
        if (PendingExports.Count == 0) { EditorApplication.update -= CompletePendingExports; return; }
        if (PlatformOutlineAuthoring.IsPlayModeActive)
        {
            SuspendPendingExports();
            return;
        }
        if (EditorApplication.isUpdating || EditorApplication.isCompiling) return;
        bool retry = false;
        foreach (var entry in new Dictionary<int, PendingExport>(PendingExports))
        {
            var pending = entry.Value;
            var scene = pending.scene;
            bool valid = scene.IsValid() && scene.isLoaded
                && Path.ChangeExtension(scene.path, ".platformoutlines") == pending.path;
            for (int i = 0; valid && i < pending.renderers.Count; i++)
                valid = pending.renderers[i] != null && pending.renderers[i].gameObject.scene == scene
                    && pending.renderers[i].sprite == pending.previews[i];
            if (!valid) { ClearExport(scene); continue; }
            Sprite[] sprites;
            if (!TryGetSavedSprites(pending.path, pending.names, out sprites))
            {
                if (pending.attempts >= 3) continue;
                pending.attempts++;
                AssetDatabase.ImportAsset(pending.path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                if (!TryGetSavedSprites(pending.path, pending.names, out sprites))
                {
                    if (pending.attempts < 3) retry = true;
                    // Do not spin on an external batch, and do not abandon the
                    // request. The postprocessor resumes it when import completes.
                    continue;
                }
            }
            // Only repeat the save when nothing was edited since that save.
            // Otherwise connect the derived sprites and leave the user's work dirty.
            bool resave = pending.sceneSaved && !scene.isDirty;
            foreach (var bake in Bakes(scene)) if (NeedsApply(bake)) resave = false;
            ClearExport(scene);
            if (!BindSavedSprites(pending.renderers, sprites, pending.previews)) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!resave) continue;
            try
            {
                resavingSceneHandle = scene.handle;
                EditorSceneManager.SaveScene(scene);
            }
            finally { resavingSceneHandle = 0; }
        }
        EditorApplication.update -= CompletePendingExports;
        if (retry) QueueExportCompletion();
    }
}

internal sealed class PlatformOutlineExportPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        PlatformOutlineAuthoring.ImportedOutlines(imported);
    }
}

[CustomEditor(typeof(PlatformOutlineBake))]
public sealed class PlatformOutlineBakeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var bake = (PlatformOutlineBake)target;
        int previousWidth = bake.widthPixels;
        var previousSources = bake.sources == null ? new Tilemap[0] : (Tilemap[])bake.sources.Clone();
        bool previousAuto = bake.rebuildWhileEditing;
        if (DrawDefaultInspector())
        {
            var sources = bake.sources ?? new Tilemap[0];
            bool inputsChanged = previousWidth != bake.widthPixels || !System.Linq.Enumerable.SequenceEqual(previousSources, sources);
            if (inputsChanged || (!previousAuto && bake.rebuildWhileEditing && PlatformOutlineAuthoring.NeedsApply(bake)))
                PlatformOutlineAuthoring.RequestUpdate(bake);
        }
        EditorGUILayout.HelpBox(bake.rebuildWhileEditing
            ? "칠하기·지우기가 끝나면 외곽선을 자동 갱신합니다. 흑백 내부 경계에는 선이 생기지 않습니다."
            : "타일 편집 중에는 계산하지 않습니다. 배치가 끝나면 외곽선 적용을 누르세요. 실제 씬 저장 때도 갱신하며, Play는 현재 적용된 결과를 사용합니다.", MessageType.Info);
        if (PlatformOutlineAuthoring.NeedsApply(bake))
            EditorGUILayout.HelpBox("외곽선에 아직 적용하지 않은 변경이 있습니다. 최신 지형으로 테스트하려면 Play 전에 외곽선 적용을 누르세요.", MessageType.Info);
        using (new EditorGUI.DisabledScope(PlatformOutlineAuthoring.IsPlayModeActive))
            if (GUILayout.Button("외곽선 적용")) PlatformOutlineAuthoring.ApplyEditedCells(bake);
    }
}
