using UnityEditor;
using UnityEngine;

/// <summary>#125 Gear125 import: k11z_shipp_r3_m slalom/jump gear FBXs (4-bone skinning + corrective blendshapes)
/// and 2048px SetA/Vest/JumpVest/Shorts/Gloves PBR textures (NormalMap + linear MaskMS).
/// WebGL uses DXT5. Android and iPhone stay ASTC_6x6.</summary>
public class Gear125Import : AssetPostprocessor
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
        if (!assetPath.Contains("/Resources/Gear125/")) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        mi.optimizeBones = true;
        mi.importAnimation = false;
        mi.importBlendShapes = true;
        mi.importBlendShapeNormals = ModelImporterNormals.Calculate;
        mi.importBlendShapeDeformPercent = true;
        mi.skinWeights = ModelImporterSkinWeights.Standard;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/Gear125/")) return;
        ApplyTexture((TextureImporter)assetImporter, assetPath);
    }

    public static void ApplyTexture(TextureImporter ti, string assetPath)
    {
        ti.maxTextureSize = 2048;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        if (assetPath.Contains("_Normal")) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
        else if (assetPath.Contains("_MaskMS")) { ti.sRGBTexture = false; }
        foreach (var plat in new[] { "Android", "WebGL", "iPhone" })
        {
            var ps = ti.GetPlatformTextureSettings(plat);
            ps.overridden = true;
            ps.maxTextureSize = 2048;
            ps.format = PlatformFormat(plat);
            ti.SetPlatformTextureSettings(ps);
        }
    }
}
