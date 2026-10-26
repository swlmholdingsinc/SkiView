using UnityEditor;
using UnityEngine;

/// Import settings for Christopher's SkiBoat.fbx (Assets/SkiView/Boat, see staging README):
/// Generate Lightmap UVs ticked, no cameras/lights/animation, textures capped for mobile, data maps linear.
public class SkiBoatImport : AssetPostprocessor
{
    const string DIR = "Assets/SkiView/Boat/";
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(DIR)) return;
        var mi = (ModelImporter)assetImporter;
        mi.generateSecondaryUV = true;
        mi.importCameras = false; mi.importLights = false; mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.globalScale = 1f; mi.useFileScale = true; mi.bakeAxisConversion = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.isReadable = false; mi.meshCompression = ModelImporterMeshCompression.Off;
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(DIR)) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 1024;
        if (assetPath.EndsWith("_Normal.png")) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
        else if (assetPath.EndsWith("_MetallicSmoothness.png")) { ti.sRGBTexture = false; }
        if (assetPath.Contains("T_Livery") || assetPath.Contains("T_Glass")) ti.alphaIsTransparency = true;
    }
}
