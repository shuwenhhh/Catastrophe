using UnityEditor;
using UnityEngine;

public sealed class NoiseHudArtworkImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/Hud/NoisePaw.png") return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Default;
        t.sRGBTexture = true;
        t.alphaIsTransparency = true;
        t.mipmapEnabled = false;
        t.maxTextureSize = 512;
        t.textureCompression = TextureImporterCompression.Uncompressed;
        t.wrapMode = TextureWrapMode.Clamp;
    }
}
