using UnityEditor;
using UnityEngine;

/// <summary>r6 Face125: male/female sculpted heads + 2k PBR. NormalMap linear, MaskMS linear.
/// WebGL and Standalone use DXT5 (S3TC). Android and iPhone stay ASTC_6x6. Max 2048.</summary>
public class Face125Import : AssetPostprocessor
{
    public const uint PolicyVersion = 2;
    /// <summary>v2: WebGL is DXT5, not ASTC. Bump only when the platform policy changes.</summary>
    public override uint GetVersion() => PolicyVersion;

    /// <summary>Desktop WebGL samples S3TC. ASTC is the mobile-player format, not the WebGL one.</summary>
    public static TextureImporterFormat PlatformFormat(string platform)
    {
        if (platform == "Android" || platform == "iPhone") return TextureImporterFormat.ASTC_6x6;
        if (platform == "WebGL" || platform == "Standalone") return TextureImporterFormat.DXT5;
        return TextureImporterFormat.Automatic;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Resources/Face125/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        mi.optimizeBones = true;
        mi.importAnimation = false;
        mi.skinWeights = ModelImporterSkinWeights.Standard;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/Face125/")) return;
        ApplyTexture((TextureImporter)assetImporter, assetPath);
    }

    public static void ApplyTexture(TextureImporter ti, string assetPath)
    {
        ti.maxTextureSize = 2048;
        ti.mipmapEnabled = true;
        if (assetPath.Contains("Alpha") || assetPath.Contains("BaseColor")) ti.alphaIsTransparency = assetPath.Contains("Alpha");
        if (assetPath.Contains("_Normal")) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
        if (assetPath.Contains("_MaskMS")) ti.sRGBTexture = false;
        foreach (var plat in new[] { "Android", "WebGL", "iPhone", "Standalone" })
        {
            var ps = ti.GetPlatformTextureSettings(plat);
            ps.overridden = true;
            ps.maxTextureSize = 2048;
            ps.format = PlatformFormat(plat);
            ti.SetPlatformTextureSettings(ps);
        }
    }
}
