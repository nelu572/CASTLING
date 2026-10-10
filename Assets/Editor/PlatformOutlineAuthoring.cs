using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlatformOutlineAuthoring
{
    private const int PixelsPerUnit = 128;
    private const int ChunkPixels = 1024;
    private const string OutputFolder = "Assets/Sprite/Map/Platforms/GeneratedOutlines";
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
        Undo.undoRedoPerformed += ClearPending;
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
        if (GUIUtility.hotControl != 0) return;
        var pending = new Dictionary<PlatformOutlineBake, int>(Pending);
        ClearPending();
        foreach (var item in pending) if (item.Key != null) Rebuild(item.Key, true, item.Value);
    }

    private static void ClearPending()
    {
        Pending.Clear();
        EditorApplication.update -= Flush;
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
        Rebuild(bake, true, group);
    }

    public static void Rebuild(PlatformOutlineBake bake, bool persist = true, int undoGroup = -1)
    {
        if (rebuilding) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
        var grid = bake.GetComponent<Grid>();
        if (grid == null) throw new InvalidOperationException("PlatformOutlineBake requires a Grid.");
        int width = Mathf.Clamp(bake.widthPixels, 1, 64);
        int halo = width + 2;
        int side = ChunkPixels + 2 * halo;
        rebuilding = true;
        if (undoGroup < 0) { Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); }
        Undo.SetCurrentGroupName("Rebuild platform outlines");
        var newRoot = new GameObject("PlatformOutlines");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(newRoot, bake.gameObject.scene);
        newRoot.transform.SetParent(bake.transform, false);
        Undo.RegisterCreatedObjectUndo(newRoot, "Rebuild platform outlines");
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
                Undo.RegisterCompleteObjectUndo(map, "Rebuild platform outlines");
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
                if (persist) EditorUtility.DisplayProgressBar("Platform outlines", "Calculating exposed boundaries", (float)finished++ / chunks.Count);
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
                Sprite sprite;
                if (persist)
                {
                    sprite = SaveChunk(texture, chunk);
                    Object.DestroyImmediate(texture);
                }
                else sprite = Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), Vector2.zero, PixelsPerUnit);
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
            Undo.RegisterCompleteObjectUndo(bake, "Rebuild platform outlines");
            if (bake.generatedRoot != null) Undo.DestroyObjectImmediate(bake.generatedRoot);
            bake.generatedRoot = newRoot; bake.sources = maps;
            EditorUtility.SetDirty(bake);
            if (persist) PlatformFillAuthoring.Finish();
            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
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

    private static Sprite SaveChunk(Texture2D texture, Vector2Int chunk)
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/Sprite/Map/Platforms", "GeneratedOutlines");
        byte[] bytes = ImageConversion.EncodeToPNG(texture);
        string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 16);
        var path = OutputFolder + "/Outline_" + chunk.x + "_" + chunk.y + "_" + hash + ".png";
        // Content-addressed files keep old Undo states valid instead of overwriting their images.
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, bytes); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit; importer.spritePivot = Vector2.zero;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = Vector2.zero;
            importer.SetTextureSettings(settings);
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
