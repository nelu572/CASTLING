using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

// One compressed source file per scene; Unity imports its images as sprite subassets.
[ScriptedImporter(1, "platformoutlines")]
public sealed class PlatformOutlineImporter : ScriptedImporter
{
    public static Texture2D PrepareTexture(Texture2D source)
    {
        // Match the old PNG importer's alpha bleed so bilinear edges keep their color.
        var settings = new TextureGenerationSettings(UnityEditor.TextureImporterType.Default);
        var importer = settings.textureImporterSettings;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.readable = true;
        importer.npotScale = UnityEditor.TextureImporterNPOTScale.None;
        settings.textureImporterSettings = importer;
        settings.sourceTextureInformation = new SourceTextureInformation {
            width = source.width, height = source.height, containsAlpha = true, hdr = false
        };
        var platform = settings.platformSettings;
        platform.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
        platform.format = UnityEditor.TextureImporterFormat.RGBA32;
        settings.platformSettings = platform; settings.enablePostProcessor = false;
        using (var pixels = new Unity.Collections.NativeArray<Color32>(source.GetPixels32(), Unity.Collections.Allocator.Temp))
        {
            var result = TextureGenerator.GenerateTexture(settings, pixels);
            Object.DestroyImmediate(source);
            if (result.thumbNail != null) Object.DestroyImmediate(result.thumbNail);
            result.texture.filterMode = FilterMode.Bilinear; result.texture.wrapMode = TextureWrapMode.Clamp;
            return result.texture;
        }
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
