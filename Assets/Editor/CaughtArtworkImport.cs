using UnityEditor;

public sealed class CaughtArtworkImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Resources/Owner/GirlCaught.png") return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
    }
}
