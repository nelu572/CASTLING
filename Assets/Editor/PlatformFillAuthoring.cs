using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public static class PlatformFillAuthoring
{
    public const string LibraryPath = "Assets/TilePalette/Main/Tiles/NoOutline.asset";
    public const string PalettePath = "Assets/TilePalette/Main/Ground_NoOutline_TilePalette.prefab";
    private static readonly Dictionary<Texture2D, Texture2D> Raw = new Dictionary<Texture2D, Texture2D>();
    private static readonly Dictionary<TileBase, Tile> Fills = new Dictionary<TileBase, Tile>();
    private static readonly Dictionary<string, Sprite> Canonical = new Dictionary<string, Sprite>();
    private static readonly Dictionary<Sprite, Sprite> Cleaned = new Dictionary<Sprite, Sprite>();
    private static PlatformFillLibrary library;

    public static PlatformFillLibrary OpenLibrary()
    {
        library = AssetDatabase.LoadAssetAtPath<PlatformFillLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<PlatformFillLibrary>();
            library.sources = new TileBase[0]; library.fills = new Tile[0];
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        Fills.Clear(); Canonical.Clear(); Cleaned.Clear();
        for (int i = 0; i < library.sources.Length; i++)
            if (library.sources[i] != null && library.fills[i] != null) Fills[library.sources[i]] = library.fills[i];
        foreach (var color in new[] { "Dark", "Ivory" })
        {
            var source = AssetDatabase.LoadAssetAtPath<PlatformTileLibrary>("Assets/TilePalette/Main/Tiles/Automatic_" + color + ".asset");
            foreach (var tile in source.tiles)
            {
                var automatic = tile as PlatformBorderTile;
                if (automatic != null) Canonical[tile.name] = automatic.variants[0].sprite;
            }
        }
        return library;
    }

    public static Tile GetFill(TileBase source)
    {
        if (source == null) return null;
        if (library == null) OpenLibrary();
        int fillIndex = Array.IndexOf(library.fills, source);
        if (fillIndex >= 0) return library.fills[fillIndex];
        Tile existing;
        if (Fills.TryGetValue(source, out existing)) return existing;
        var automatic = source as PlatformBorderTile;
        var tile = source as Tile;
        if (automatic == null && tile == null)
            throw new InvalidOperationException("Unsupported platform tile: " + source.name);
        Sprite sprite = automatic != null ? automatic.variants[0].sprite : tile.sprite;
        if (sprite == null) throw new InvalidOperationException("Missing sprite: " + source.name);
        var key = Regex.Replace(source.name, @"_(?:None|[TRBLC]+)_x", "_x");
        Sprite canonical;
        if (Canonical.TryGetValue(key, out canonical) && SamePhysics(sprite, canonical)) sprite = canonical;
        bool knownPlain = Canonical.ContainsValue(sprite)
            || AssetDatabase.GetAssetPath(sprite).EndsWith("PlatformTiles_Mixed.png", StringComparison.OrdinalIgnoreCase);
        if (!knownPlain) sprite = Clean(sprite);
        var fill = ScriptableObject.CreateInstance<Tile>();
        fill.name = "Fill_" + source.name;
        fill.sprite = sprite;
        fill.color = automatic != null ? Color.white : tile.color;
        fill.colliderType = automatic != null ? Tile.ColliderType.Sprite : tile.colliderType;
        fill.flags = TileFlags.None;
        fill.transform = Matrix4x4.identity;
        AssetDatabase.AddObjectToAsset(fill, library);
        var sources = new List<TileBase>(library.sources) { source };
        var fills = new List<Tile>(library.fills) { fill };
        library.sources = sources.ToArray(); library.fills = fills.ToArray();
        Fills[source] = fill;
        EditorUtility.SetDirty(library);
        return fill;
    }

    private static bool SamePhysics(Sprite a, Sprite b)
    {
        if (a.GetPhysicsShapeCount() != b.GetPhysicsShapeCount()) return false;
        var p = new List<Vector2>(); var q = new List<Vector2>();
        for (int i = 0; i < a.GetPhysicsShapeCount(); i++)
        {
            a.GetPhysicsShape(i, p); b.GetPhysicsShape(i, q);
            if (p.Count != q.Count) return false;
            for (int j = 0; j < p.Count; j++) if ((p[j] - q[j]).sqrMagnitude > .00000001f) return false;
        }
        return true;
    }

    public static Texture2D ReadTexture(Texture2D texture)
    {
        Texture2D raw;
        if (Raw.TryGetValue(texture, out raw)) return raw;
        var path = AssetDatabase.GetAssetPath(texture);
        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return texture;
        raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(raw, File.ReadAllBytes(path));
        Raw[texture] = raw;
        return raw;
    }

    private static Sprite Clean(Sprite source)
    {
        Sprite result;
        if (Cleaned.TryGetValue(source, out result)) return result;
        var rect = source.rect;
        var colors = ReadTexture(source.texture).GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        bool dark = AssetDatabase.GetAssetPath(source).Contains("Dark");
        // Match the existing art's flat stroke colors; preserve the source alpha silhouette.
        var fill = dark ? new Color32(36, 39, 41, 255) : new Color32(240, 232, 221, 255);
        for (int i = 0; i < colors.Length; i++)
        {
            var c = (Color32)colors[i];
            if (c.a == 0) continue;
            bool stroke = dark ? c.r >= 54 && c.r < 150 : c.r >= 245 && c.g >= 239;
            if (stroke) colors[i] = new Color32(fill.r, fill.g, fill.b, c.a);
        }
        var texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false) {
            name = "FillTexture_" + source.name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(colors); texture.Apply();
        const string folder = "Assets/Sprite/Map/Platforms/GeneratedFills";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Sprite/Map/Platforms", "GeneratedFills");
        byte[] bytes = ImageConversion.EncodeToPNG(texture);
        Object.DestroyImmediate(texture);
        string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 16);
        string guid; long fileId; AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out guid, out fileId);
        string path = folder + "/Fill_" + guid + "_" + fileId + "_" + hash + ".png";
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, bytes); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = source.pixelsPerUnit; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true; importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var id = GUID.Generate(); string name = "FillSprite_" + source.name;
            provider.SetSpriteRects(new[] { new SpriteRect { name = name, spriteID = id,
                rect = new Rect(0, 0, rect.width, rect.height), alignment = SpriteAlignment.Custom,
                pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height) } });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(name, id) });
            var paths = new List<Vector2[]>(); var points = new List<Vector2>();
            for (int i = 0; i < source.GetPhysicsShapeCount(); i++)
            {
                source.GetPhysicsShape(i, points);
                paths.Add(points.ConvertAll(p => p * source.pixelsPerUnit + source.pivot - rect.size * .5f).ToArray());
            }
            provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>().SetOutlines(id, paths);
            provider.Apply(); importer.SaveAndReimport();
        }
        result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Cleaned[source] = result;
        return result;
    }

    public static void Finish()
    {
        if (library != null) { EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library); }
        foreach (var texture in Raw.Values) Object.DestroyImmediate(texture);
        Raw.Clear();
    }

    [MenuItem("Tools/CASTLING/Platform Outlines/Create No Outline Palette")]
    public static void CreatePalette()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) != null) return;
        OpenLibrary();
        var root = PrefabUtility.LoadPrefabContents("Assets/TilePalette/Main/Ground_Auto_TilePalette.prefab");
        try
        {
            root.name = "Ground_NoOutline_TilePalette";
            var map = root.GetComponentInChildren<Tilemap>();
            foreach (var position in map.cellBounds.allPositionsWithin)
            {
                var source = map.GetTile(position); if (source == null) continue;
                var transform = map.GetTransformMatrix(position); var color = map.GetColor(position);
                map.SetTile(position, GetFill(source)); map.SetTileFlags(position, TileFlags.None);
                map.SetTransformMatrix(position, transform); map.SetColor(position, color);
            }
            var mixed = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TilePalette/Main/Ground_Manual_Shapes_TilePalette.prefab");
            var mixedMap = mixed.GetComponentInChildren<Tilemap>();
            var sizes = new[] { 1, 1, 2, 2, 3, 4 }; var rows = new[] { 0, -4, -8, -13, -18, -24 };
            for (int c = 0; c < 2; c++) for (int f = 0; f < sizes.Length; f++)
                for (int y = 0; y < sizes[f]; y++) for (int x = 0; x < sizes[f]; x++)
                {
                    var from = new Vector3Int(76 + 16 * c + x, rows[f] + y, 0);
                    var to = new Vector3Int(48 + 16 * c + x, rows[f] + y, 0);
                    map.SetTile(to, GetFill(mixedMap.GetTile(from)));
                }
            foreach (var label in mixed.GetComponentsInChildren<TextMesh>())
            {
                if (!label.name.StartsWith("Label_Mixed_")) continue;
                var copy = Object.Instantiate(label.gameObject, root.transform);
                copy.transform.localPosition = label.transform.localPosition - new Vector3(28, 0, 0);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
            // A new palette needs its own GridPalette subasset (not a reference to the old settings).
            var settings = ScriptableObject.CreateInstance<GridPalette>();
            settings.name = "Palette Settings"; settings.cellSizing = GridPalette.CellSizing.Manual;
            AssetDatabase.AddObjectToAsset(settings, PalettePath);
            Finish();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
