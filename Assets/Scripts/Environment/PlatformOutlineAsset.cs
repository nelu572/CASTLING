using UnityEngine;

// Only the current scene's baked sprites/textures are saved as subassets.
public sealed class PlatformOutlineAsset : ScriptableObject
{
    public Sprite[] sprites = new Sprite[0];
}
