using UnityEditor;
using UnityEngine;

/// Pass 19: import settings for Christopher's vegetation pack (Assets/SkiView/Vegetation, README "Unity setup"): scale 1, no axis
/// bake, Normals Import (custom bent leaf normals), Mikktspace tangents, vertex colours kept (wind R sway / G flutter / B phase),
/// LOD Group from the _LOD0.._LOD3 suffixes, no lightmap UVs, mesh compression Low. Textures: the leaf atlas and the billboards are
/// cutouts (Alpha Is Transparency, mip maps preserve coverage at 0.5); Android / WebGL caps: leaf atlas 1024, bark 512, billboards 256.
public class SkiViewVegImport : AssetPostprocessor
{
    const string DIR = "Assets/SkiView/Vegetation/";
    public override uint GetVersion() => 1;
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(DIR)) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f; mi.useFileScale = true; mi.bakeAxisConversion = false;
        mi.importBlendShapes = false; mi.importVisibility = false; mi.importCameras = false; mi.importLights = false;
        mi.meshCompression = ModelImporterMeshCompression.Low; mi.isReadable = false;
        mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.generateSecondaryUV = false; mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(DIR)) return;
        var ti = (TextureImporter)assetImporter; string f = System.IO.Path.GetFileName(assetPath);
        bool leaf = f.StartsWith("veg_leaf"), imp = f.StartsWith("imp_"), bark = f.StartsWith("veg_bark");
        ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.mipmapEnabled = true;
        ti.alphaIsTransparency = leaf || imp; ti.mipMapsPreserveCoverage = leaf || imp; ti.alphaTestReferenceValue = 0.5f;
        ti.wrapMode = imp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        ti.maxTextureSize = leaf ? 2048 : imp ? 512 : 1024;
        foreach (var plat in new[] { "Android", "WebGL" })
        {
            var s = ti.GetPlatformTextureSettings(plat); s.overridden = true;
            s.maxTextureSize = leaf ? 1024 : imp ? 256 : 512;
            s.format = plat == "Android" ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.Automatic;
            ti.SetPlatformTextureSettings(s);
        }
    }
}
