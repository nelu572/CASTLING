// Method body for Unity MCP execute_code. Creates assets only on the first run.
// See Tools/Art/README.md before running. Does not modify the open scene.
const string atlasPath = "Assets/Sprite/Map/Platforms/PlatformTiles_Mixed.png";
const string libraryPath = "Assets/TilePalette/Main/Tiles/Manual_Mixed.asset";
const string palettePath = "Assets/TilePalette/Main/Ground_Manual_Shapes_TilePalette.prefab";
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
    throw new System.InvalidOperationException("Run in Edit Mode.");
if (System.IO.File.Exists(atlasPath) || System.IO.File.Exists(libraryPath))
    throw new System.InvalidOperationException("Mixed assets already exist. Inspect user edits before updating them.");

var families = new[] { "OuterSmall", "OuterR1", "OuterR2", "InnerR1", "InnerR2", "InnerR3" };
var sizes = new[] { 1, 1, 2, 2, 3, 4 };
var rows = new[] { 0, -4, -8, -13, -18, -24 };
var colors = new[] { "Dark", "Ivory" };
var originals = new System.Collections.Generic.Dictionary<string, PlatformBorderTile>();
var squares = new PlatformBorderTile[2];
var sourceTextures = new UnityEngine.Texture2D[2];
var foregrounds = new System.Collections.Generic.List<UnityEngine.Color32[]>();
var backgrounds = new System.Collections.Generic.List<UnityEngine.Color32[]>();
var mixedNames = new System.Collections.Generic.List<string>();
var cellTiles = new System.Collections.Generic.Dictionary<string, UnityEngine.Tilemaps.TileBase>();
var palette = UnityEditor.PrefabUtility.LoadPrefabContents(palettePath);
try
{
    var map = palette.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>();
    // Reserve only new cells to the right of the existing palette.
    for (int color = 0; color < 2; color++)
        for (int family = 0; family < families.Length; family++)
            for (int y = 0; y < sizes[family]; y++)
                for (int x = 0; x < sizes[family]; x++)
                    if (map.HasTile(new UnityEngine.Vector3Int(76 + color * 16 + x, rows[family] + y, 0)))
                        throw new System.InvalidOperationException("The new palette area contains user tiles.");

    for (int color = 0; color < 2; color++)
    {
        var library = UnityEditor.AssetDatabase.LoadAssetAtPath<PlatformTileLibrary>(
            "Assets/TilePalette/Main/Tiles/Automatic_" + colors[color] + ".asset");
        foreach (var asset in library.tiles)
        {
            var tile = asset as PlatformBorderTile;
            if (tile == null) continue;
            originals.Add(tile.name, tile);
            if (tile.square) squares[color] = tile;
        }
        sourceTextures[color] = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
        UnityEngine.ImageConversion.LoadImage(sourceTextures[color], System.IO.File.ReadAllBytes(
            "Assets/Sprite/Map/Platforms/PlatformTiles_" + colors[color] + ".png"));
    }

    for (int color = 0; color < 2; color++)
    {
        var opposite = 1 - color;
        var fillSprite = squares[opposite].variants[0].sprite;
        var fill = sourceTextures[opposite].GetPixels((int)fillSprite.rect.x, (int)fillSprite.rect.y, 128, 128);
        var fillBytes = System.Array.ConvertAll(fill, pixel => (UnityEngine.Color32)pixel);
        foreach (var family in families)
        {
            var size = sizes[System.Array.IndexOf(families, family)];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var name = "Mixed_" + colors[color] + "On" + colors[opposite] + "_" + family + "_x" + x + "_y" + y;
                    PlatformBorderTile source;
                    if (!originals.TryGetValue(colors[color] + "_" + family + "_x" + x + "_y" + y, out source))
                    {
                        cellTiles.Add(name, squares[opposite].variants[0]);
                        continue;
                    }
                    var sprite = source.variants[0].sprite;
                    if (sprite.rect.width != 128 || sprite.rect.height != 128)
                        throw new System.InvalidOperationException("Expected a 128px source cell: " + sprite.name);
                    var pixels = sourceTextures[color].GetPixels((int)sprite.rect.x, (int)sprite.rect.y, 128, 128);
                    var bytes = System.Array.ConvertAll(pixels, pixel => (UnityEngine.Color32)pixel);
                    bool transparent = true, opaque = true;
                    foreach (var pixel in bytes) { transparent &= pixel.a == 0; opaque &= pixel.a == 255; }
                    if (opaque || transparent)
                    {
                        cellTiles.Add(name, opaque ? source.variants[0] : squares[opposite].variants[0]);
                        continue;
                    }
                    mixedNames.Add(name);
                    foregrounds.Add(bytes);
                    backgrounds.Add(fillBytes);
                }
        }
    }

    const int stride = 132, columns = 8;
    int atlasRows = (mixedNames.Count + columns - 1) / columns;
    var atlas = new UnityEngine.Texture2D(columns * stride, atlasRows * stride, UnityEngine.TextureFormat.RGBA32, false);
    try
    {
        atlas.SetPixels32(new UnityEngine.Color32[atlas.width * atlas.height]);
        for (int i = 0; i < mixedNames.Count; i++)
        {
            // Extrude edge colors through the 2px gutter for bilinear filtering.
            var padded = new UnityEngine.Color32[stride * stride];
            for (int y = 0; y < stride; y++)
                for (int x = 0; x < stride; x++)
                {
                    int sample = UnityEngine.Mathf.Clamp(y - 2, 0, 127) * 128 + UnityEngine.Mathf.Clamp(x - 2, 0, 127);
                    var front = foregrounds[i][sample];
                    var back = backgrounds[i][sample];
                    int alpha = front.a;
                    padded[y * stride + x] = new UnityEngine.Color32(
                        (byte)((front.r * alpha + back.r * (255 - alpha) + 127) / 255),
                        (byte)((front.g * alpha + back.g * (255 - alpha) + 127) / 255),
                        (byte)((front.b * alpha + back.b * (255 - alpha) + 127) / 255), 255);
                }
            atlas.SetPixels32((i % columns) * stride, (i / columns) * stride, stride, stride, padded);
        }
        atlas.Apply();
        System.IO.File.WriteAllBytes(atlasPath, UnityEngine.ImageConversion.EncodeToPNG(atlas));
    }
    finally { UnityEngine.Object.DestroyImmediate(atlas); }

    UnityEditor.AssetDatabase.ImportAsset(atlasPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(atlasPath);
    importer.textureType = UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
    importer.spritePixelsPerUnit = 128;
    importer.mipmapEnabled = false;
    importer.alphaIsTransparency = true;
    importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    importer.npotScale = UnityEditor.TextureImporterNPOTScale.None;
    importer.maxTextureSize = 2048;
    importer.filterMode = UnityEngine.FilterMode.Bilinear;
    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    var settings = new UnityEditor.TextureImporterSettings();
    importer.ReadTextureSettings(settings);
    settings.spriteMeshType = UnityEngine.SpriteMeshType.FullRect;
    importer.SetTextureSettings(settings);
    importer.SaveAndReimport();

    var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
    factories.Init();
    var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
    provider.InitSpriteEditorDataProvider();
    var rects = new UnityEditor.SpriteRect[mixedNames.Count];
    var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
    for (int i = 0; i < rects.Length; i++)
    {
        var id = UnityEditor.GUID.Generate();
        rects[i] = new UnityEditor.SpriteRect {
            name = mixedNames[i], spriteID = id, alignment = UnityEngine.SpriteAlignment.Center,
            pivot = new UnityEngine.Vector2(.5f, .5f),
            rect = new UnityEngine.Rect((i % columns) * stride + 2, (i / columns) * stride + 2, 128, 128)
        };
        pairs.Add(new UnityEditor.SpriteNameFileIdPair(mixedNames[i], id));
    }
    provider.SetSpriteRects(rects);
    provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
    var physics = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpritePhysicsOutlineDataProvider>();
    foreach (var rect in rects)
        physics.SetOutlines(rect.spriteID, new System.Collections.Generic.List<UnityEngine.Vector2[]> {
            new[] { new UnityEngine.Vector2(-64, -64), new UnityEngine.Vector2(64, -64),
                new UnityEngine.Vector2(64, 64), new UnityEngine.Vector2(-64, 64) }
        });
    provider.Apply();
    importer.SaveAndReimport();

    var output = UnityEngine.ScriptableObject.CreateInstance<PlatformTileLibrary>();
    output.name = "Manual_Mixed";
    UnityEditor.AssetDatabase.CreateAsset(output, libraryPath);
    var mixedTiles = new System.Collections.Generic.List<UnityEngine.Tilemaps.TileBase>();
    foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(atlasPath))
    {
        var sprite = asset as UnityEngine.Sprite;
        if (sprite == null) continue;
        var tile = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        tile.name = sprite.name;
        tile.sprite = sprite;
        tile.color = UnityEngine.Color.white;
        tile.transform = UnityEngine.Matrix4x4.identity;
        tile.flags = UnityEngine.Tilemaps.TileFlags.None;
        tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.Sprite;
        UnityEditor.AssetDatabase.AddObjectToAsset(tile, output);
        cellTiles.Add(tile.name, tile);
        mixedTiles.Add(tile);
    }
    output.tiles = mixedTiles.ToArray();
    UnityEditor.EditorUtility.SetDirty(output);
    UnityEditor.AssetDatabase.SaveAssets();

    var template = palette.GetComponentInChildren<UnityEngine.TextMesh>();
    for (int color = 0; color < 2; color++)
    {
        int left = 76 + color * 16;
        var title = UnityEngine.Object.Instantiate(template.gameObject, palette.transform);
        title.name = "Label_Mixed_" + colors[color];
        title.transform.localPosition = new UnityEngine.Vector3(left, 3, 0);
        title.GetComponent<UnityEngine.TextMesh>().text = colors[color] + " / " + colors[1 - color] + " Mixed";
        title.GetComponent<UnityEngine.TextMesh>().anchor = UnityEngine.TextAnchor.UpperLeft;
        title.GetComponent<UnityEngine.TextMesh>().alignment = UnityEngine.TextAlignment.Left;
        for (int family = 0; family < families.Length; family++)
        {
            var label = UnityEngine.Object.Instantiate(template.gameObject, palette.transform);
            label.name = "Label_Mixed_" + colors[color] + "_" + families[family];
            label.transform.localPosition = new UnityEngine.Vector3(left, rows[family] + sizes[family] + .7f, 0);
            label.GetComponent<UnityEngine.TextMesh>().text = families[family] + " " + sizes[family] + "x" + sizes[family];
            label.GetComponent<UnityEngine.TextMesh>().anchor = UnityEngine.TextAnchor.UpperLeft;
            label.GetComponent<UnityEngine.TextMesh>().alignment = UnityEngine.TextAlignment.Left;
            for (int y = 0; y < sizes[family]; y++)
                for (int x = 0; x < sizes[family]; x++)
                {
                    var name = "Mixed_" + colors[color] + "On" + colors[1 - color] + "_" + families[family] + "_x" + x + "_y" + y;
                    map.SetTile(new UnityEngine.Vector3Int(left + x, rows[family] + y, 0), cellTiles[name]);
                }
        }
    }
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(palette, palettePath);
    return new { sprites = mixedNames.Count, paletteCells = cellTiles.Count, atlasPath, libraryPath, palettePath };
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(palette);
    foreach (var texture in sourceTextures)
        if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
}
