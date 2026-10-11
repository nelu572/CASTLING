using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
    private static readonly Dictionary<PlatformOutlineBake, int> Pending = new Dictionary<PlatformOutlineBake, int>();
    private static bool rebuilding;

    private sealed class Piece
    {
        public Sprite sprite;
        public Color32[] pixels;
        public int textureWidth;
        public Matrix4x4 matrix, inverse;
        public Rect bounds;
        public Color tint;
        public int order;
    }

    static PlatformOutlineAuthoring()
    {
        Tilemap.tilemapTileChanged += Changed;
        Undo.undoRedoPerformed += RestoreAfterUndo;
        EditorSceneManager.sceneSaving += SaveScene;
        EditorSceneManager.sceneClosing += ReleaseScenePreviews;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        EditorApplication.delayCall += RestoreMissingPreviews;
    }

    private static void Changed(Tilemap map, Tilemap.SyncTile[] changes)
    {
        if (rebuilding || EditorApplication.isPlayingOrWillChangePlaymode || map == null) return;
        var bake = map.GetComponentInParent<PlatformOutlineBake>();
        if (bake == null || !bake.rebuildWhileEditing) return;
        if (!Pending.ContainsKey(bake)) Pending.Add(bake, Undo.GetCurrentGroup());
        EditorApplication.update -= Flush;
        EditorApplication.update += Flush;
    }

    private static void Flush()
    {
        FlushPending(false);
    }

    private static void FlushPending(bool force)
    {
        if (!force && GUIUtility.hotControl != 0) return;
        var pending = new Dictionary<PlatformOutlineBake, int>(Pending);
        ClearPending();
        foreach (var item in pending) if (item.Key != null) Rebuild(item.Key, item.Value);
    }

    private static void ClearPending()
    {
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
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var bake in Bakes(SceneManager.GetSceneAt(i))) Pending[bake] = WithoutUndo;
        EditorApplication.update += Flush;
    }

    private static void ReleaseScenePreviews(Scene scene, bool removingScene)
    {
        foreach (var bake in Bakes(scene))
            if (bake.generatedRoot != null)
            {
                var sprites = new List<Sprite>();
                foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true)) sprites.Add(renderer.sprite);
                ReleasePreviews(sprites);
            }
    }

    private static void RestoreMissingPreviews()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var bake in Bakes(SceneManager.GetSceneAt(i)))
                if (bake.generatedRoot != null)
                    foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true))
                        if (renderer.sprite == null) { Rebuild(bake, WithoutUndo); break; }
    }

    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        FlushPending(true);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            // Keep the saved scene's older subassets until that scene is actually saved.
            if (!string.IsNullOrEmpty(scene.path)) SaveSceneOutlines(scene, scene.path, false);
        }
    }

    private static void SaveScene(Scene scene, string path)
    {
        FlushPending(true);
        SaveSceneOutlines(scene, path, true);
    }

    [MenuItem("Tools/CASTLING/Platform Outlines/Rebuild Selected Grid")]
    public static void RebuildSelected()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
        var selected = Selection.activeGameObject;
        var grid = selected == null ? null : selected.GetComponentInParent<Grid>();
        if (grid == null) throw new InvalidOperationException("Select the terrain Grid or Ground Tilemap.");
        var bake = grid.GetComponent<PlatformOutlineBake>();
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        if (bake == null) bake = Undo.AddComponent<PlatformOutlineBake>(grid.gameObject);
        Rebuild(bake, group);
    }

    public static void Rebuild(PlatformOutlineBake bake, int undoGroup = -1)
    {
        if (rebuilding) return;
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode only.");
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
                if (map == null || map.layoutGrid != grid) throw new InvalidOperationException("All sources must use this Grid.");
                foreach (var source in map.GetUsedTilesCount() == 0 ? new TileBase[0] : UsedTiles(map))
                    replacements[source] = PlatformFillAuthoring.GetFill(source);
            }
            foreach (var map in maps)
            {
                if (recordUndo) Undo.RegisterCompleteObjectUndo(map, "Rebuild platform outlines");
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    var source = map.GetTile(cell); if (source == null) continue;
                    var fill = replacements[source]; if (source == fill) continue;
                    var matrix = map.GetTransformMatrix(cell); var color = map.GetColor(cell);
                    map.SetTile(cell, fill); map.SetTileFlags(cell, TileFlags.None);
                    map.SetTransformMatrix(cell, matrix); map.SetColor(cell, color);
                }
                map.RefreshAllTiles();
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
                EditorUtility.DisplayProgressBar("Platform outlines", "Calculating exposed boundaries", (float)finished++ / chunks.Count);
                int originX = chunk.x * ChunkPixels - halo, originY = chunk.y * ChunkPixels - halo;
                var coverage = new byte[side * side];
                var ink = new Color32[side * side];
                Rasterize(pieces, originX, originY, side, coverage, ink);
                var distances = DistanceToEmpty(coverage, side);
                var pixels = new Color32[ChunkPixels * ChunkPixels];
                bool visible = false;
                int left = ChunkPixels, bottom = ChunkPixels, right = -1, top = -1;
                for (int y = 0; y < ChunkPixels; y++) for (int x = 0; x < ChunkPixels; x++)
                {
                    int sample = (y + halo) * side + x + halo;
                    if (coverage[sample] == 0) continue;
                    float strength = Mathf.Clamp01(width + .5f - Mathf.Sqrt(distances[sample]));
                    var color = ink[sample]; color.a = (byte)Mathf.RoundToInt(strength * coverage[sample] * color.a / 255f);
                    pixels[y * ChunkPixels + x] = color; visible |= color.a > 0;
                    if (color.a > 0) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                }
                if (!visible) continue;
                int textureWidth = right - left + 1, textureHeight = top - bottom + 1;
                var cropped = new Color32[textureWidth * textureHeight];
                for (int y = 0; y < textureHeight; y++) Array.Copy(pixels, (y + bottom) * ChunkPixels + left, cropped, y * textureWidth, textureWidth);
                var texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false) {
                    name = "PlatformOutline", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels32(cropped); texture.Apply();
                var sprite = CreatePreview(texture, sprites);
                var child = new GameObject("Outline_" + chunk.x + "_" + chunk.y, typeof(SpriteRenderer));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child, newRoot.scene);
                child.transform.SetParent(newRoot.transform, false);
                child.transform.localPosition = new Vector3(chunk.x * 8 + (float)left / PixelsPerUnit, chunk.y * 8 + (float)bottom / PixelsPerUnit, -.01f);
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
                var oldSprites = new List<Sprite>();
                foreach (var renderer in bake.generatedRoot.GetComponentsInChildren<SpriteRenderer>(true)) oldSprites.Add(renderer.sprite);
                var retained = new HashSet<Transform>();
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
                ReleasePreviews(oldSprites);
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
        }
        catch
        {
            if (newRoot != null) Object.DestroyImmediate(newRoot);
            ReleasePreviews(sprites.Values);
            if (recordUndo) Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
        finally { EditorUtility.ClearProgressBar(); rebuilding = false; }
    }

    private static TileBase[] UsedTiles(Tilemap map)
    {
        var tiles = new TileBase[map.GetUsedTilesCount()]; map.GetUsedTilesNonAlloc(tiles); return tiles;
    }

    private static List<Piece> ReadPieces(Grid grid, Tilemap[] maps)
    {
        var result = new List<Piece>();
        var cache = new Dictionary<Texture2D, Color32[]>();
        foreach (var map in maps)
        {
            var renderer = map.GetComponent<TilemapRenderer>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile<Tile>(cell); if (tile == null || tile.sprite == null) continue;
                var sprite = tile.sprite;
                Color32[] pixels;
                if (!cache.TryGetValue(sprite.texture, out pixels))
                    cache[sprite.texture] = pixels = PlatformFillAuthoring.ReadTexture(sprite.texture).GetPixels32();
                var matrix = grid.transform.worldToLocalMatrix * map.transform.localToWorldMatrix
                    * Matrix4x4.Translate(map.CellToLocalInterpolated((Vector3)cell + map.tileAnchor)) * map.GetTransformMatrix(cell);
                var bounds = sprite.bounds;
                var a = matrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.min.y));
                var b = matrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.min.y));
                var c = matrix.MultiplyPoint3x4(new Vector3(bounds.min.x, bounds.max.y));
                var d = matrix.MultiplyPoint3x4(new Vector3(bounds.max.x, bounds.max.y));
                result.Add(new Piece { sprite = sprite, pixels = pixels,
                    textureWidth = PlatformFillAuthoring.ReadTexture(sprite.texture).width, matrix = matrix, inverse = matrix.inverse,
                    bounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x, c.x, d.x), Mathf.Min(a.y, b.y, c.y, d.y),
                        Mathf.Max(a.x, b.x, c.x, d.x), Mathf.Max(a.y, b.y, c.y, d.y)),
                    tint = map.GetColor(cell) * map.color, order = renderer.sortingOrder });
            }
        }
        result.Sort((a, b) => a.order.CompareTo(b.order));
        return result;
    }

    private static void Rasterize(List<Piece> pieces, int originX, int originY, int side, byte[] coverage, Color32[] ink)
    {
        foreach (var piece in pieces)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(piece.bounds.xMin * PixelsPerUnit) - originX, 0, side);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(piece.bounds.xMax * PixelsPerUnit) - originX, 0, side);
            int minY = Mathf.Clamp(Mathf.FloorToInt(piece.bounds.yMin * PixelsPerUnit) - originY, 0, side);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(piece.bounds.yMax * PixelsPerUnit) - originY, 0, side);
            var sprite = piece.sprite; var rect = sprite.rect;
            for (int y = minY; y < maxY; y++) for (int x = minX; x < maxX; x++)
            {
                var local = piece.inverse.MultiplyPoint3x4(new Vector3((originX + x + .5f) / PixelsPerUnit, (originY + y + .5f) / PixelsPerUnit));
                int px = Mathf.FloorToInt(local.x * sprite.pixelsPerUnit + sprite.pivot.x);
                int py = Mathf.FloorToInt(local.y * sprite.pixelsPerUnit + sprite.pivot.y);
                if (px < 0 || py < 0 || px >= rect.width || py >= rect.height) continue;
                var pixel = piece.pixels[(py + (int)rect.y) * piece.textureWidth + px + (int)rect.x];
                if (pixel.a == 0 || piece.tint.a <= 0) continue;
                int index = y * side + x;
                coverage[index] = (byte)Mathf.Max(coverage[index], pixel.a);
                var border = pixel.r < 160 ? new Color32(92, 96, 95, 255) : new Color32(255, 248, 236, 255);
                ink[index] = (Color)border * piece.tint;
            }
        }
    }

    // Exact squared Euclidean distance transform, in two separable linear passes.
    public static float[] DistanceToEmpty(byte[] coverage, int side)
    {
        var output = new float[coverage.Length];
        var input = new float[side]; var result = new float[side];
        var vertices = new int[side]; var intersections = new float[side + 1];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++) input[x] = coverage[y * side + x] < 128 ? 0 : 1e12f;
            DistanceLine(input, result, vertices, intersections);
            for (int x = 0; x < side; x++) output[y * side + x] = result[x];
        }
        for (int x = 0; x < side; x++)
        {
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

    private static void ReleasePreviews(IEnumerable<Sprite> sprites)
    {
        var textures = new HashSet<Texture2D>();
        foreach (var sprite in new HashSet<Sprite>(sprites))
        {
            if (sprite == null || EditorUtility.IsPersistent(sprite)) continue;
            textures.Add(sprite.texture); Object.DestroyImmediate(sprite);
        }
        foreach (var texture in textures)
            if (texture != null && !EditorUtility.IsPersistent(texture)) Object.DestroyImmediate(texture);
    }

    private static void SaveSceneOutlines(Scene scene, string scenePath, bool prune)
    {
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
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        var saved = AssetDatabase.LoadAssetAtPath<PlatformOutlineAsset>(path);
        if (saved == null) throw new InvalidOperationException("Failed to import platform outlines: " + path);
        var byName = new Dictionary<string, Sprite>();
        foreach (var sprite in saved.sprites) byName.Add(sprite.name, sprite);
        for (int i = 0; i < renderers.Count; i++) renderers[i].sprite = byName[names[i]];
        ReleasePreviews(previews);
        PlatformFillAuthoring.Finish();
    }
}

[CustomEditor(typeof(PlatformOutlineBake))]
public sealed class PlatformOutlineBakeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var bake = (PlatformOutlineBake)target;
        EditorGUILayout.HelpBox(bake.rebuildWhileEditing
            ? "칠하기·지우기가 끝나면 외곽선을 자동 갱신합니다. 흑백 내부 경계에는 선이 생기지 않습니다."
            : "자동 갱신이 꺼져 있습니다. 지형을 수정한 뒤 아래 버튼으로 외곽선을 다시 계산하세요.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("외곽선 다시 계산")) PlatformOutlineAuthoring.Rebuild((PlatformOutlineBake)target);
    }
}
