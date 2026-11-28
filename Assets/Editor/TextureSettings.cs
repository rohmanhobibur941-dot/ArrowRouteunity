using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArrowRoute.EditorTools
{
    /// <summary>
    /// Import settings for the pictures in Resources/UI: no resizing to power-of-two (the labels would get a wrong
    /// aspect ratio), no mip maps, small pictures uncompressed so text stays sharp.
    /// </summary>
    public sealed class UiTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/UI/")) return;
            TextureImporter ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.isReadable = false;
            ti.maxTextureSize = 4096;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            bool big = false;
            try { big = new FileInfo(assetPath).Length > 300000; } catch { }
            ti.textureCompression = big ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
        }
    }
}
