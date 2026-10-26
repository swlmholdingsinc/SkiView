using UnityEditor;
using UnityEngine;

// T8: skier import (Assets/SkiView/Skier; the Felderpuss rider is archived in _archive/Felderpuss, Wade Cox is the only skier). Rider FBXs are Humanoid (same 67 mixamorig bones as Remy,
// so the Remy@ clips retarget); the ski is a plain static model. Textures capped for phone.
class SkiViewSkierImport : AssetPostprocessor
{
    const string DIR = "Assets/SkiView/Skier/";
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(DIR)) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f; mi.useFileScale = true; mi.importBlendShapes = false; mi.importCameras = false; mi.importLights = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        if (assetPath.Contains("FelderpussSki") || assetPath.Contains("TrickSki") || assetPath.Contains("JumpSki") || assetPath.Contains("/Ramp/")) { mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false; return; }
        mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = assetPath.Contains("@");
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(DIR)) return;
        var ti = (TextureImporter)assetImporter;
        bool v3Body = assetPath.Contains("Felderpuss_V3_WadeCox") && (assetPath.Contains("BaseColor") || assetPath.Contains("Normal") || assetPath.Contains("_AO_"));
        bool ski = assetPath.Contains("Ski_Skin");
        bool nrm = assetPath.Contains("Normal") || assetPath.Contains("_normal");
        ti.textureType = nrm ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !nrm && !assetPath.Contains("_AO_");
        ti.maxTextureSize = v3Body ? (assetPath.Contains("Phone") ? 2048 : 4096) : (ski ? 2048 : 1024);
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.mipmapEnabled = true; ti.anisoLevel = (ski || v3Body) ? 4 : 1;
        if (assetPath.Contains("Hair") && assetPath.Contains("Diffuse")) ti.alphaIsTransparency = true;
    }
}

/// <summary>Wade V2 P1 (Karthik note): extract the V2 materials to Skier/Materials as URP Lit; hair = alpha clip 0.5 cutout,
/// double-sided; INTERNAL_ONLY_mat_eye_v2 = eye texture, smoothness 0.85. Run: -executeMethod SkiViewSkierSetup.WadeV2</summary>
public static class SkiViewSkierSetup
{
    public static void WadeV2()
    {
        const string D = "Assets/SkiView/Skier/", M = D + "Materials/";
        AssetDatabase.Refresh(); if (!AssetDatabase.IsValidFolder(M.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/SkiView/Skier", "Materials");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var fbx in new[] { D + "WadeCox_V2.fbx", D + "WadeCox_V2_Phone.fbx" })
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx); if (imp == null) { Debug.LogError("[SkiView] missing " + fbx); continue; }
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                if (!(o is Material src)) continue;
                string p = M + src.name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(lit) { name = src.name }; m.SetColor("_BaseColor", src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white); if (src.mainTexture != null) m.SetTexture("_BaseMap", src.mainTexture); AssetDatabase.CreateAsset(m, p); }
                if (fbx.EndsWith("_Phone.fbx")) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_WadeCox_V2_PhoneAtlas.png"));
                if (src.name.Contains("mat_hair_v2"))
                {
                    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_WadeCox_V2_Hair_Diffuse.png"));
                    m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Surface", 0f);
                    m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; m.SetOverrideTag("RenderType", "TransparentCutout");
                }
                if (src.name.Contains("mat_eye_v2")) { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_WadeCox_V2_Eye.png")); m.SetFloat("_Smoothness", 0.85f); }
                EditorUtility.SetDirty(m); imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m);
            }
            imp.SaveAndReimport();
        }
        AssetDatabase.SaveAssets(); Debug.Log("[SkiView] Wade V2 materials set up");
    }

    /// <summary>Slalom ski V2 (Christopher, Oct 23, INTERNAL ONLY): URP Lit materials for FelderpussSki_V2_WadeCox.fbx; the ski skin gets
    /// the bright V2 texture. Run: -executeMethod SkiViewSkierSetup.SlalomSkiV2 (also called from the builder).</summary>
    public static void SlalomSkiV2()
    {
        const string D = "Assets/SkiView/Skier/", M = D + "Materials/", F = D + "FelderpussSki_V2_WadeCox.fbx";
        if (!AssetDatabase.IsValidFolder(M.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/SkiView/Skier", "Materials");
        var imp = (ModelImporter)AssetImporter.GetAtPath(F); if (imp == null) { Debug.LogWarning("[SkiView] no " + F); return; }
        var lit = Shader.Find("Universal Render Pipeline/Lit"); bool changed = false;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(F))
        {
            if (!(o is Material src) || src.name.StartsWith("SkiV2_")) continue;
            string p = M + "SkiV2_" + src.name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(lit) { name = "SkiV2_" + src.name }; AssetDatabase.CreateAsset(m, p); }
            m.SetColor("_BaseColor", src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
            if (src.name.Contains("ski_skin")) { m.SetColor("_BaseColor", Color.white); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(D + "T_Ski_Skin_V2_WadeCox_Bright.png")); m.SetFloat("_Smoothness", 0.55f); }
            else { if (src.mainTexture != null) m.SetTexture("_BaseMap", src.mainTexture); m.SetFloat("_Smoothness", 0.35f); }
            EditorUtility.SetDirty(m); imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m); changed = true;
        }
        if (changed) imp.SaveAndReimport();
        AssetDatabase.SaveAssets(); Debug.Log("[SkiView] slalom ski V2 materials set up");
    }


    /// <summary>b10.6 slalom ski V3 (Holly staging): import ToePlate + DoubleBoot FBXs, URP Lit remap, green colourway maps by default.
    /// Run: -executeMethod SkiViewSkierSetup.SlalomSkiV3 (also from SkiViewSkiPreviews.Make).</summary>
    public static void SlalomSkiV3()
    {
        const string D = "Assets/SkiView/Skier/SkiV3/", T = D + "Textures/", M = D + "Materials/";
        if (!AssetDatabase.IsValidFolder("Assets/SkiView/Skier/SkiV3")) AssetDatabase.CreateFolder("Assets/SkiView/Skier", "SkiV3");
        if (!AssetDatabase.IsValidFolder(M.TrimEnd('/'))) AssetDatabase.CreateFolder(D.TrimEnd('/'), "Materials");
        AssetDatabase.Refresh();
        foreach (var tex in System.IO.Directory.GetFiles(T, "*.png"))
        {
            if (AssetImporter.GetAtPath(tex) is TextureImporter ti)
            {
                bool nrm = tex.Contains("_normal");
                ti.textureType = nrm ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !nrm && !tex.Contains("metallic") && !tex.Contains("roughness");
                ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
        }
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var fbxName in new[] { "SkiV3_ToePlate.fbx", "SkiV3_DoubleBoot.fbx" })
        {
            string F = D + fbxName;
            var imp = (ModelImporter)AssetImporter.GetAtPath(F); if (imp == null) { Debug.LogWarning("[SkiView] no " + F); continue; }
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            bool changed = false;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(F))
            {
                if (!(o is Material src) || src.name.StartsWith("SkiV3_")) continue;
                string pth = M + "SkiV3_" + src.name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(pth);
                if (m == null) { m = new Material(lit) { name = "SkiV3_" + src.name }; AssetDatabase.CreateAsset(m, pth); }
                m.shader = lit;
                m.SetColor("_BaseColor", Color.white);
                bool skin = src.name.Contains("top") || src.name.Contains("base") || src.name.Contains("skin");
                if (skin)
                {
                    ApplyV3Skin(m, "green");
                }
                else
                {
                    if (src.mainTexture != null) m.SetTexture("_BaseMap", src.mainTexture);
                    m.SetFloat("_Smoothness", src.name.Contains("hardware") ? 0.7f : 0.35f);
                    m.SetFloat("_Metallic", src.name.Contains("hardware") ? 0.6f : 0.05f);
                }
                EditorUtility.SetDirty(m);
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m);
                changed = true;
            }
            if (changed) imp.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        // Runtime prefabs under Resources/Skis
        const string RD = "Assets/SkiView/Resources/Skis";
        System.IO.Directory.CreateDirectory(RD);
        foreach (var (fbx, pref) in new[] { ("SkiV3_ToePlate.fbx", "SlalomSki_V3_ToePlate"), ("SkiV3_DoubleBoot.fbx", "SlalomSki_V3_DoubleBoot") })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(D + fbx); if (go == null) continue;
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(go);
            PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var r in tmp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            PrefabUtility.SaveAsPrefabAsset(tmp, RD + "/" + pref + ".prefab");
            Object.DestroyImmediate(tmp);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[SkiView] slalom ski V3 materials + Resources prefabs set up");
    }


    /// <summary>b10.8 Wade V3 FINAL (INTERNAL ONLY): URP Lit remap for INTERNAL_ONLY_Felderpuss_V3_WadeCox (+ Phone).
    /// Body atlas gets BaseColor/Normal/AO; hair cutout; eye glossy. Photo face on V3 body; open vest baked in mesh.
    /// Run: -executeMethod SkiViewSkierSetup.WadeV3</summary>
    public static void WadeV3()
    {
        const string D = "Assets/SkiView/Skier/", M = D + "Materials/";
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder(M.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/SkiView/Skier", "Materials");
        foreach (var tex in System.IO.Directory.GetFiles(D, "INTERNAL_ONLY_T_Felderpuss_V3_WadeCox*.png"))
        {
            if (AssetImporter.GetAtPath(tex.Replace('\\', '/')) is TextureImporter ti)
            {
                bool nrm = tex.Contains("Normal"); bool ao = tex.Contains("_AO_");
                ti.textureType = nrm ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !nrm && !ao;
                ti.maxTextureSize = tex.Contains("Phone") ? 2048 : 4096;
                ti.mipmapEnabled = true; ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
        }
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var (fbx, phone) in new[] {
            (D + "INTERNAL_ONLY_Felderpuss_V3_WadeCox.fbx", false),
            (D + "INTERNAL_ONLY_Felderpuss_V3_WadeCox_Phone.fbx", true) })
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (imp == null) { Debug.LogError("[SkiView] missing " + fbx); continue; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            string baseMap = phone
                ? D + "INTERNAL_ONLY_T_Felderpuss_V3_WadeCox_Phone_BaseColor_2k.png"
                : D + "INTERNAL_ONLY_T_Felderpuss_V3_WadeCox_BaseColor_4k.png";
            string nrmMap = phone
                ? D + "INTERNAL_ONLY_T_Felderpuss_V3_WadeCox_Phone_Normal_2k.png"
                : D + "INTERNAL_ONLY_T_Felderpuss_V3_WadeCox_Normal_4k.png";
            string aoMap = phone
                ? D + "INTERNAL_ONLY_T_Felderpuss_V3_WadeCox_Phone_AO_2k.png"
                : D + "INTERNAL_ONLY_T_Felderpuss_V3_WadeCox_AO_4k.png";
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap);
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmMap);
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(aoMap);
            var eye = AssetDatabase.LoadAssetAtPath<Texture2D>(D + "INTERNAL_ONLY_T_Felderpuss_V2_WadeCox_Eye.png");
            var hair = AssetDatabase.LoadAssetAtPath<Texture2D>(D + "INTERNAL_ONLY_T_Felderpuss_V2_WadeCox_Hair_Diffuse.png");
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                if (!(o is Material src)) continue;
                string pth = M + src.name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(pth);
                if (m == null) { m = new Material(lit) { name = src.name }; AssetDatabase.CreateAsset(m, pth); }
                m.shader = lit;
                bool isHair = src.name.Contains("mat_hair");
                bool isEye = src.name.Contains("mat_eye");
                bool isAtlas = src.name.Contains("atlas") || src.name.Contains("Phone") || src.name.Contains("Felderpuss_V3");
                if (isHair)
                {
                    if (hair != null) m.SetTexture("_BaseMap", hair);
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Surface", 0f); m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.SetFloat("_Smoothness", 0.35f);
                }
                else if (isEye)
                {
                    if (eye != null) m.SetTexture("_BaseMap", eye);
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Smoothness", 0.85f); m.SetFloat("_Metallic", 0f);
                }
                else if (isAtlas || src.name.Contains("Skin") || src.name.Contains("Merged") || src.name.StartsWith("INTERNAL_ONLY_mat_Felderpuss"))
                {
                    if (alb != null) m.SetTexture("_BaseMap", alb);
                    m.SetColor("_BaseColor", Color.white);
                    if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
                    if (ao != null && m.HasProperty("_OcclusionMap")) { m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 1f); }
                    m.SetFloat("_Smoothness", 0.42f); m.SetFloat("_Metallic", 0.02f);
                }
                else
                {
                    // vest / shorts / misc slots baked into atlas on V3 — still bind atlas so bare arms / open vest read
                    if (alb != null) m.SetTexture("_BaseMap", alb);
                    m.SetColor("_BaseColor", Color.white);
                    if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
                    m.SetFloat("_Smoothness", 0.4f);
                }
                EditorUtility.SetDirty(m);
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m);
            }
            imp.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[SkiView] Wade V3 INTERNAL_ONLY materials set up");
    }


    /// <summary>b10.11 Tricks Phase A: import finless TrickSki_V1.fbx, URP Lit remap, Resources/Skis/TrickSki_V1 prefab.
    /// Run: -executeMethod SkiViewSkierSetup.TrickSkiV1</summary>
    public static void TrickSkiV1()
    {
        const string D = "Assets/SkiView/Skier/TrickSki/", F = D + "TrickSki_V1.fbx", M = D + "Materials/";
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder("Assets/SkiView/Skier/TrickSki")) AssetDatabase.CreateFolder("Assets/SkiView/Skier", "TrickSki");
        if (!AssetDatabase.IsValidFolder(M.TrimEnd('/'))) AssetDatabase.CreateFolder(D.TrimEnd('/'), "Materials");
        var imp = (ModelImporter)AssetImporter.GetAtPath(F);
        if (imp == null) { Debug.LogError("[SkiView] missing " + F); return; }
        imp.animationType = ModelImporterAnimationType.None;
        imp.importAnimation = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.globalScale = 1f; imp.useFileScale = true;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        bool changed = false;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(F))
        {
            if (!(o is Material src) || src.name.StartsWith("TrickSki_")) continue;
            string pth = M + "TrickSki_" + src.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(pth);
            if (m == null) { m = new Material(lit) { name = "TrickSki_" + src.name }; AssetDatabase.CreateAsset(m, pth); }
            m.shader = lit;
            m.SetColor("_BaseColor", src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
            if (src.mainTexture != null) m.SetTexture("_BaseMap", src.mainTexture);
            bool boot = src.name.ToLowerInvariant().Contains("boot") || src.name.ToLowerInvariant().Contains("binding");
            bool hard = src.name.ToLowerInvariant().Contains("hardware") || src.name.ToLowerInvariant().Contains("plate");
            m.SetFloat("_Smoothness", hard ? 0.7f : boot ? 0.35f : 0.5f);
            m.SetFloat("_Metallic", hard ? 0.55f : 0.05f);
            EditorUtility.SetDirty(m);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m);
            changed = true;
        }
        if (changed) imp.SaveAndReimport();
        else imp.SaveAndReimport();
        AssetDatabase.SaveAssets();
        const string RD = "Assets/SkiView/Resources/Skis";
        System.IO.Directory.CreateDirectory(RD);
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(F);
        if (go == null) { Debug.LogError("[SkiView] TrickSki_V1.fbx failed to load"); return; }
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(go);
        PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        // hide LOD1 on desktop default; phone can swap later
        foreach (var tr in tmp.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name.Contains("LOD1")) tr.gameObject.SetActive(false);
            if (tr.name.Contains("LOD0")) tr.gameObject.SetActive(true);
        }
        foreach (var r in tmp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(tmp, RD + "/TrickSki_V1.prefab");
        Object.DestroyImmediate(tmp);
        AssetDatabase.SaveAssets();
        Debug.Log("[SkiView] TrickSki_V1 materials + Resources prefab set up");
    }

    /// <summary>b10.18: hot-swap TrickSki_V1 PBR maps (BaseColor/Normal/MetallicSmoothness) onto existing materials + refresh prefab.
    /// Run: -executeMethod SkiViewSkierSetup.TrickSkiV1Pbr</summary>
    public static void TrickSkiV1Pbr()
    {
        const string D = "Assets/SkiView/Skier/TrickSki/", M = D + "Materials/", T = D + "Textures/", F = D + "TrickSki_V1.fbx";
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder(T.TrimEnd('/'))) AssetDatabase.CreateFolder(D.TrimEnd('/'), "Textures");
        foreach (var n in new[] {
            "T_TrickSki_V1_Normal.png", "T_TrickBindings_V1_Normal.png",
            "T_TrickSki_V1_Metallic.png", "T_TrickSki_V1_Roughness.png", "T_TrickSki_V1_MetallicSmoothness.png",
            "T_TrickBindings_V1_Metallic.png", "T_TrickBindings_V1_Roughness.png", "T_TrickBindings_V1_MetallicSmoothness.png" })
        {
            var ti = AssetImporter.GetAtPath(T + n) as TextureImporter;
            if (ti == null) continue;
            bool isN = n.Contains("Normal"), isLin = n.Contains("Metallic") || n.Contains("Roughness") || n.Contains("Smoothness");
            bool want = (isN && ti.textureType != TextureImporterType.NormalMap) || (isLin && ti.sRGBTexture) || ti.maxTextureSize < 2048;
            if (!want) continue;
            if (isN) ti.textureType = TextureImporterType.NormalMap;
            if (isLin) ti.sRGBTexture = false;
            ti.maxTextureSize = n.Contains("Bindings") ? 1024 : 2048;
            ti.anisoLevel = 4; ti.SaveAndReimport();
        }
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        void BindPbr(Material m, string prefix)
        {
            if (m == null) return;
            m.shader = lit;
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_BaseColor.png");
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_Normal.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_MetallicSmoothness.png");
            if (alb != null) { m.SetTexture("_BaseMap", alb); m.SetColor("_BaseColor", Color.white); }
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f);
            }
            else { m.SetFloat("_Metallic", 0.08f); m.SetFloat("_Smoothness", 0.72f); }
            EditorUtility.SetDirty(m);
        }
        string PrefFor(string srcName)
        {
            var l = srcName.ToLowerInvariant();
            if (l.Contains("bind") || l.Contains("boot") || l.Contains("plate") || l.Contains("strap") || l.Contains("hardware")) return "T_TrickBindings_V1";
            return "T_TrickSki_V1";
        }
        int nMat = 0;
        foreach (var gu in AssetDatabase.FindAssets("t:Material", new[] { M.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(gu);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) continue;
            BindPbr(m, PrefFor(m.name));
            nMat++;
        }
        // refresh Resources prefab from FBX if present
        const string RD = "Assets/SkiView/Resources/Skis";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(F);
        if (go != null)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(go);
            PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var r in tmp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            PrefabUtility.SaveAsPrefabAsset(tmp, RD + "/TrickSki_V1.prefab");
            Object.DestroyImmediate(tmp);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SkiView] TrickSki_V1 PBR bound mats={nMat}");
    }

    /// <summary>b10.16: JumpSki_V1 pair + strict PBR. Prefab Resources/Skis/JumpSki_V1.
    /// Run: -executeMethod SkiViewSkierSetup.JumpSkiV1</summary>
    public static void JumpSkiV1()
    {
        const string D = "Assets/SkiView/Skier/JumpSki/", F = D + "JumpSki_V1_Pair.fbx", M = D + "Materials/", T = D + "Textures/";
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder("Assets/SkiView/Skier/JumpSki")) AssetDatabase.CreateFolder("Assets/SkiView/Skier", "JumpSki");
        if (!AssetDatabase.IsValidFolder(M.TrimEnd('/'))) AssetDatabase.CreateFolder(D.TrimEnd('/'), "Materials");
        if (!AssetDatabase.IsValidFolder(T.TrimEnd('/'))) AssetDatabase.CreateFolder(D.TrimEnd('/'), "Textures");
        foreach (var n in new[] {
            "T_JumpSki_V1_Normal.png", "T_JumpBindings_V1_Normal.png", "T_JumpFin_V1_Normal.png",
            "T_JumpSki_V1_Metallic.png", "T_JumpSki_V1_Roughness.png", "T_JumpSki_V1_MetallicSmoothness.png",
            "T_JumpBindings_V1_Metallic.png", "T_JumpBindings_V1_Roughness.png", "T_JumpBindings_V1_MetallicSmoothness.png",
            "T_JumpFin_V1_Metallic.png", "T_JumpFin_V1_Roughness.png" })
        {
            var ti = AssetImporter.GetAtPath(T + n) as TextureImporter;
            if (ti == null) continue;
            bool isN = n.Contains("Normal"), isLin = n.Contains("Metallic") || n.Contains("Roughness") || n.Contains("Smoothness");
            bool want = (isN && ti.textureType != TextureImporterType.NormalMap) || (isLin && ti.sRGBTexture) || ti.maxTextureSize < 2048;
            if (!want) continue;
            if (isN) ti.textureType = TextureImporterType.NormalMap;
            if (isLin) ti.sRGBTexture = false;
            ti.maxTextureSize = n.Contains("Fin") ? 512 : n.Contains("Bindings") ? 1024 : 2048;
            ti.anisoLevel = 4; ti.SaveAndReimport();
        }
        var imp = (ModelImporter)AssetImporter.GetAtPath(F);
        if (imp == null) { Debug.LogError("[SkiView] missing " + F); return; }
        imp.animationType = ModelImporterAnimationType.None;
        imp.importAnimation = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.globalScale = 1f; imp.useFileScale = true;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        void BindPbr(Material m, string prefix)
        {
            m.shader = lit;
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_BaseColor.png");
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_Normal.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_MetallicSmoothness.png");
            if (alb != null) { m.SetTexture("_BaseMap", alb); m.SetColor("_BaseColor", Color.white); }
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f);
            }
            else
            {
                m.SetFloat("_Metallic", 0.05f);
                m.SetFloat("_Smoothness", prefix.Contains("Fin") ? 0.55f : 0.78f);
            }
            EditorUtility.SetDirty(m);
        }
        string PrefFor(string srcName)
        {
            var l = srcName.ToLowerInvariant();
            if (l.Contains("bind") || l.Contains("boot")) return "T_JumpBindings_V1";
            if (l.Contains("fin")) return "T_JumpFin_V1";
            return "T_JumpSki_V1";
        }
        bool changed = false;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(F))
        {
            if (!(o is Material src) || src.name.StartsWith("JumpSki_")) continue;
            string pth = M + "JumpSki_" + src.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(pth);
            if (m == null) { m = new Material(lit) { name = "JumpSki_" + src.name }; AssetDatabase.CreateAsset(m, pth); }
            BindPbr(m, PrefFor(src.name));
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m);
            changed = true;
        }
        imp.SaveAndReimport();
        AssetDatabase.SaveAssets();
        const string RD = "Assets/SkiView/Resources/Skis";
        System.IO.Directory.CreateDirectory(RD);
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(F);
        if (go == null) { Debug.LogError("[SkiView] JumpSki_V1_Pair.fbx failed to load"); return; }
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(go);
        PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var tr in tmp.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name.Contains("LOD1")) tr.gameObject.SetActive(false);
            if (tr.name.Contains("LOD0")) tr.gameObject.SetActive(true);
        }
        foreach (var r in tmp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(tmp, RD + "/JumpSki_V1.prefab");
        Object.DestroyImmediate(tmp);
        AssetDatabase.SaveAssets();
        Debug.Log("[SkiView] JumpSki_V1 materials + Resources prefab set up");
    }

    /// <summary>b10.16: Ramp_V1 + strict PBR. Prefab Resources/Ramp/Ramp_V1.
    /// Run: -executeMethod SkiViewSkierSetup.RampV1</summary>
    public static void RampV1()
    {
        const string D = "Assets/SkiView/Ramp/", F = D + "Ramp_V1.fbx", M = D + "Materials/", T = D + "Textures/";
        AssetDatabase.Refresh();
        if (!AssetDatabase.IsValidFolder("Assets/SkiView/Ramp")) AssetDatabase.CreateFolder("Assets/SkiView", "Ramp");
        if (!AssetDatabase.IsValidFolder(M.TrimEnd('/'))) AssetDatabase.CreateFolder(D.TrimEnd('/'), "Materials");
        if (!AssetDatabase.IsValidFolder(T.TrimEnd('/'))) AssetDatabase.CreateFolder(D.TrimEnd('/'), "Textures");
        foreach (var n in System.IO.Directory.GetFiles(T.TrimEnd('/'), "*.png"))
        {
            string bn = System.IO.Path.GetFileName(n);
            var ti = AssetImporter.GetAtPath(T + bn) as TextureImporter;
            if (ti == null) continue;
            bool isN = bn.Contains("Normal"), isLin = bn.Contains("Metallic") || bn.Contains("Roughness") || bn.Contains("Smoothness");
            bool want = (isN && ti.textureType != TextureImporterType.NormalMap) || (isLin && ti.sRGBTexture);
            if (!want && ti.maxTextureSize >= 2048) continue;
            if (isN) ti.textureType = TextureImporterType.NormalMap;
            if (isLin) ti.sRGBTexture = false;
            ti.maxTextureSize = bn.Contains("Surface") ? 4096 : bn.Contains("Apron") ? 2048 : 1024;
            ti.anisoLevel = 4; ti.SaveAndReimport();
        }
        var imp = (ModelImporter)AssetImporter.GetAtPath(F);
        if (imp == null) { Debug.LogError("[SkiView] missing " + F); return; }
        imp.animationType = ModelImporterAnimationType.None;
        imp.importAnimation = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.globalScale = 1f; imp.useFileScale = true;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        void BindPbr(Material m, string prefix)
        {
            m.shader = lit;
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_BaseColor.png");
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_Normal.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(T + prefix + "_MetallicSmoothness.png");
            if (alb != null) { m.SetTexture("_BaseMap", alb); m.SetColor("_BaseColor", Color.white); }
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f);
            }
            else { m.SetFloat("_Metallic", 0.1f); m.SetFloat("_Smoothness", 0.74f); }
            EditorUtility.SetDirty(m);
        }
        string PrefFor(string srcName)
        {
            var l = srcName.ToLowerInvariant();
            if (l.Contains("apron")) return "T_Ramp_V1_Apron";
            if (l.Contains("post") || l.Contains("frame") || l.Contains("steel")) return "T_Ramp_V1_Frame";
            return "T_Ramp_V1_Surface";
        }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(F))
        {
            if (!(o is Material src) || src.name.StartsWith("RampV1_")) continue;
            if (src.name.ToLowerInvariant().Contains("collider")) continue;
            string pth = M + "RampV1_" + src.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(pth);
            if (m == null) { m = new Material(lit) { name = "RampV1_" + src.name }; AssetDatabase.CreateAsset(m, pth); }
            BindPbr(m, PrefFor(src.name));
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), m);
        }
        imp.SaveAndReimport();
        AssetDatabase.SaveAssets();
        const string RD = "Assets/SkiView/Resources/Ramp";
        System.IO.Directory.CreateDirectory(RD);
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(F);
        if (go == null) { Debug.LogError("[SkiView] Ramp_V1.fbx failed to load"); return; }
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(go);
        PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var tr in tmp.GetComponentsInChildren<Transform>(true))
        {
            if (!tr.name.Contains("Collider")) continue;
            var r = tr.GetComponent<Renderer>(); if (r != null) r.enabled = false;
        }
        foreach (var r in tmp.GetComponentsInChildren<Renderer>(true))
            if (r.enabled) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(tmp, RD + "/Ramp_V1.prefab");
        Object.DestroyImmediate(tmp);
        AssetDatabase.SaveAssets();
        Debug.Log("[SkiView] Ramp_V1 materials + Resources prefab set up");
    }

    public static void ApplyV3Skin(Material m, string colourId)
    {
        const string T = "Assets/SkiView/Skier/SkiV3/Textures/";
        var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(T + colourId + "_albedo.png");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(T + colourId + "_normal.png");
        var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(T + colourId + "_metallic_smoothness.png");
        if (alb != null) m.SetTexture("_BaseMap", alb);
        m.SetColor("_BaseColor", Color.white);
        if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
        if (ms != null)
        {
            m.SetTexture("_MetallicGlossMap", ms);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f);   // from alpha
            m.SetFloat("_Metallic", 1f);
        }
        else m.SetFloat("_Smoothness", 0.45f);
    }
}
