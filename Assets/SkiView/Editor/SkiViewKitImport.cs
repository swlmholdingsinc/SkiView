using UnityEditor;
using UnityEngine;

/// Pass 19: import settings for Christopher's regional kits (Assets/SkiView/Kits/<kit>/, same format as the v2 lots, see each README):
/// scale 1 + Convert Units, no axis bake, mesh compression Low and no lightmap UVs (pass 9 size cuts: the houses are lit by light
/// probes and baked reflection probes, never lightmapped), no rig/anim, materials via MaterialDescription;
/// textures hv2_lotNN_{albedo,normal,glass,impostor}: *_normal are normal maps (OpenGL convention = Unity's);
/// mobile/web caps: body albedo atlas 1024, normal atlas 512, impostor 1024, glass gradient as is (Android ASTC 6x6, normals 8x8).
public class SkiViewKitImport : AssetPostprocessor
{
    const string DIR = "Assets/SkiView/Kits/";
    public override uint GetVersion() => 1;   // bump to reimport the kits after a settings change
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(DIR)) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f; mi.useFileScale = true; mi.bakeAxisConversion = false;
        mi.importBlendShapes = false; mi.importVisibility = false; mi.importCameras = false; mi.importLights = false;
        mi.meshCompression = ModelImporterMeshCompression.Low; mi.isReadable = false;   // README asked for none; Low keeps the 60 m lots crack-free
        mi.optimizeMeshPolygons = true; mi.optimizeMeshVertices = true;
        mi.importNormals = ModelImporterNormals.Import;
        mi.generateSecondaryUV = false;   // UV2 only serves lightmaps, which the houses never use
        mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(DIR)) return;
        var ti = (TextureImporter)assetImporter;
        string f = System.IO.Path.GetFileName(assetPath);
        bool imp = f.EndsWith("_impostor.jpg"), nrm = f.EndsWith("_normal.jpg");
        if (nrm) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; }
        else { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; }
        ti.mipmapEnabled = true; ti.wrapMode = imp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        ti.maxTextureSize = 2048;
        // v2 atlases are 2048 (4x4 cells of 512 px, tiles repeat inside a cell). Full size on PC/editor; mobile and web keep the
        // pass 7 budget idea: albedo atlas 1024 (256 px cells; houses are seen from the water at 30 m+), normals 512, impostors 1024
        foreach (var plat in new[] { "Android", "WebGL" })
        {
            var a = ti.GetPlatformTextureSettings(plat);
            a.overridden = true; a.maxTextureSize = nrm ? 512 : 1024;
            a.format = plat == "Android" ? (nrm ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6) : TextureImporterFormat.Automatic;
            ti.SetPlatformTextureSettings(a);
        }
    }
}
