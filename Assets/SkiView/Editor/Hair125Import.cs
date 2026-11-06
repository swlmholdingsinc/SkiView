using UnityEditor;
using UnityEngine;

/// <summary>#125 Munoz gate #3: Hair125 conformed SHIPP meshes and textures. NormalMap, linear MaskMS.
/// WebGL uses DXT5. Android and iPhone stay ASTC_6x6. Max 1024.</summary>
public class Hair125Import : AssetPostprocessor
{
    public const uint PolicyVersion = 2;
    /// <summary>v2: WebGL is DXT5, not ASTC. Bump only when the platform policy changes.</summary>
    public override uint GetVersion() => PolicyVersion;

    public static TextureImporterFormat PlatformFormat(string platform)
    {
        if (platform == "WebGL") return TextureImporterFormat.DXT5;
        if (platform == "Android" || platform == "iPhone") return TextureImporterFormat.ASTC_6x6;
        return TextureImporterFormat.Automatic;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Resources/Hair125/")) return;
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
        if (!assetPath.Contains("/Resources/Hair125/")) return;
        ApplyTexture((TextureImporter)assetImporter, assetPath);
    }

    public static void ApplyTexture(TextureImporter ti, string assetPath)
    {
        ti.maxTextureSize = 1024;
        ti.mipmapEnabled = true;
        if (assetPath.Contains("Alpha")) ti.alphaIsTransparency = true;
        if (assetPath.Contains("_Normal")) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
        if (assetPath.Contains("_MaskMS")) ti.sRGBTexture = false;
        foreach (var plat in new[] { "Android", "WebGL", "iPhone" })
        {
            var ps = ti.GetPlatformTextureSettings(plat);
            ps.overridden = true;
            ps.maxTextureSize = 1024;
            ps.format = PlatformFormat(plat);
            ti.SetPlatformTextureSettings(ps);
        }
    }
}
