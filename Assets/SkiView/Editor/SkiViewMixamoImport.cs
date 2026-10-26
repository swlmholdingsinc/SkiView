using UnityEditor;
using UnityEngine;

/// <summary>Import settings for the staged Mixamo Remy files: Humanoid rig, clips looped and baked in place
/// (Mixamo gave no "In Place" option), no root motion. One-shot clips (starts, stops, falls, stand-up) don't loop. Raw FBX files stay out of git (see .gitignore).</summary>
public class SkiViewMixamoImport : AssetPostprocessor
{
    const string DIR = "Assets/SkiView/ThirdParty/Mixamo/";
    bool Ours => assetPath.StartsWith(DIR) && assetPath.EndsWith(".fbx");

    void OnPreprocessModel()
    {
        if (!Ours) return;
        var mi = (ModelImporter)assetImporter;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importCameras = false; mi.importLights = false;
        mi.importBlendShapes = false;
        bool isChar = !assetPath.Contains("@");
        mi.importAnimation = !isChar;
        mi.materialImportMode = isChar ? ModelImporterMaterialImportMode.ImportViaMaterialDescription : ModelImporterMaterialImportMode.None;
        mi.meshCompression = ModelImporterMeshCompression.Medium;
        mi.animationCompression = ModelImporterAnimationCompression.Optimal;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(DIR + "Textures/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 1024;                     // rider is never more than ~1/3 of the screen
        ti.textureCompression = TextureImporterCompression.Compressed;
        if (assetPath.Contains("_Normal")) ti.textureType = TextureImporterType.NormalMap;
        if (assetPath.Contains("_Gloss") || assetPath.Contains("_Specular") || assetPath.Contains("_Opacity")) ti.sRGBTexture = false;
    }

    void OnPreprocessAnimation()
    {
        if (!Ours || !assetPath.Contains("@")) return;
        var mi = (ModelImporter)assetImporter;
        var clips = mi.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;
        // pass 11 (batch 3): one-shots (deep-water start, falls, stand-up) play once; loops are the stances and idles
        string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        bool oneShot = n.EndsWith(" Start") || n.EndsWith(" Stop") || n.Contains("Death") || n.Contains("Stand Up") || n.Contains("Climbing") || n.Contains("Flip");
        foreach (var c in clips)
        {
            c.loopTime = !oneShot; c.loopPose = !oneShot;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;     // bake root rotation
            c.lockRootHeightY = true; c.keepOriginalPositionY = true;        // bake Y
            c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = false;   // bake XZ at the centre of mass: stays in place
            c.name = System.IO.Path.GetFileNameWithoutExtension(assetPath).Substring(5); // "Remy@Pulling A Rope" -> "Pulling A Rope"
        }
        mi.clipAnimations = clips;
    }
}
