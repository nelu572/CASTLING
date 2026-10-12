using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

// One compressed source file per scene; Unity imports its images as sprite subassets.
[ScriptedImporter(1, "platformoutlines")]
public sealed class PlatformOutlineImporter : ScriptedImporter
{
    public static Texture2D PrepareTexture(Texture2D source)
    {
        // Bilinear filtering needs color just beyond the visible alpha edge.
        // Keep this in memory: TextureGenerator opens an import progress dialog,
        // and its returned texture has not uploaded its pixels to the GPU yet.
        var pixels = source.GetPixels32();
        int width = source.width, height = source.height;
        var filled = new bool[pixels.Length];
        var frontier = new System.Collections.Generic.Queue<int>();
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].a != 0) { filled[i] = true; frontier.Enqueue(i); }
        for (int distance = 0; distance < 2; distance++)
        {
            int count = frontier.Count;
            for (int i = 0; i < count; i++)
            {
                int index = frontier.Dequeue(), x = index % width, y = index / width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int next = ny * width + nx;
                    if (filled[next]) continue;
                    var color = pixels[index]; color.a = 0;
                    pixels[next] = color; filled[next] = true; frontier.Enqueue(next);
                }
            }
        }
        source.SetPixels32(pixels);
        source.Apply(false, false);
        source.filterMode = FilterMode.Bilinear; source.wrapMode = TextureWrapMode.Clamp;
        return source;
    }

    public override void OnImportAsset(AssetImportContext context)
    {
        using (var reader = new BinaryReader(File.OpenRead(context.assetPath)))
        {
            if (reader.ReadString() != "CASTLING_PLATFORM_OUTLINES" || reader.ReadInt32() != 1)
                throw new InvalidDataException("Unsupported platform outline format.");
            var asset = ScriptableObject.CreateInstance<PlatformOutlineAsset>();
            asset.name = Path.GetFileNameWithoutExtension(context.assetPath) + " Outlines";
            asset.sprites = new Sprite[reader.ReadInt32()];
            context.AddObjectToAsset("Outlines", asset); context.SetMainObject(asset);
            for (int i = 0; i < asset.sprites.Length; i++)
            {
                string name = reader.ReadString();
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, reader.ReadBytes(reader.ReadInt32())))
                    throw new InvalidDataException("Invalid platform outline texture.");
                texture.name = name + "_Texture";
                texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, 128, 0, SpriteMeshType.FullRect);
                sprite.name = name;
                context.AddObjectToAsset(texture.name, texture); context.AddObjectToAsset(name, sprite);
                asset.sprites[i] = sprite;
            }
        }
    }
}
