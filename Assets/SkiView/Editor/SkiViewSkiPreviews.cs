using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using SkiView;

/// <summary>b10.6: import V3 materials/prefabs and mark Holly's colourway preview PNGs as sprites.
/// Previews are the staged 3/4 renders (ski_N.png toe plate, ski_N_db.png double boot). Run: unity_run.sh SkiViewSkiPreviews.Make</summary>
public static class SkiViewSkiPreviews
{
    public static void Make()
    {
        SkiViewSkierSetup.SlalomSkiV3();
        const string PD = "Assets/SkiView/UI/Resources/SkiViewUI/Skis";
        AssetDatabase.Refresh();
        for (int k = 0; k < SkiViewGame.SKI_NAMES.Length; k++)
        {
            foreach (var suffix in new[] { "", "_db" })
            {
                string p = $"{PD}/ski_{k}{suffix}.png";
                if (AssetImporter.GetAtPath(p) is TextureImporter ti)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.mipmapEnabled = false;
                    ti.maxTextureSize = 1024;
                    ti.SaveAndReimport();
                }
            }
            Debug.Log($"[SkiView] ski preview sprites {k} {SkiViewGame.SKI_NAMES[k]}");
            string th = $"{PD}/thumb_{k}.png";
            if (AssetImporter.GetAtPath(th) is TextureImporter thi)
            {
                thi.textureType = TextureImporterType.Sprite;
                thi.mipmapEnabled = false;
                thi.maxTextureSize = 256;
                thi.SaveAndReimport();
            }
        }
        // Runtime colourway maps under Resources/Skis/V3
        const string RT = "Assets/SkiView/Resources/Skis/V3";
        if (System.IO.Directory.Exists(RT))
        {
            foreach (var tex in System.IO.Directory.GetFiles(RT, "*.png"))
            {
                if (AssetImporter.GetAtPath(tex) is TextureImporter ti)
                {
                    bool nrm = tex.Contains("_normal");
                    ti.textureType = nrm ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    ti.sRGBTexture = !nrm && !tex.Contains("metallic");
                    ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 4;
                    ti.SaveAndReimport();
                }
            }
        }
        Debug.Log("[SkiView] ski V3 previews + Resources ready");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // keep batchmode happy
    }
}
