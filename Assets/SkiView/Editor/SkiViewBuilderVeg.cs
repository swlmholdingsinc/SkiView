using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pass 19: Christopher's vegetation pack (31 species, LOD0-2 + LOD3 cross-card billboard) as terrain tree prototypes.
/// One prefab per FBX in Vegetation/Prefabs with the pack's shared URP materials (veg_bark opaque; veg_leaf and veg_imp_* alpha clip
/// 0.5, both faces), GPU instancing on, LOD Group 0.30 / 0.12 / 0.05 / 0.012 (billboards carry the far rings). Pines (v1.1: slash,
/// longleaf, loblolly, ponderosa) use the second atlas pair veg_bark_b / veg_leaf_b; only Cedar Bend's PNW interior forest stays procedural.</summary>
public static partial class SkiViewBuilder
{
    const string VEG = "Assets/SkiView/Vegetation/";
    static readonly Dictionary<string, GameObject> vegPrefabs = new Dictionary<string, GameObject>();

    static Material VegMat(string name, string tex, bool cutout, float smooth)
    {
        Directory.CreateDirectory(VEG + "Materials");
        string p = VEG + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(VEG + "textures/" + tex)); m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
        m.SetFloat("_AlphaClip", cutout ? 1f : 0f); m.SetFloat("_Cutoff", 0.5f);
        if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", cutout ? 0f : 2f); m.doubleSidedGI = cutout;
        m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");   // sky reflections greyed the crowns from above
        m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
        m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
        var wind = Shader.Find("SkiView/VegWind");   // pass 19: wind sway (vertex colours R sway / G flutter / B phase); off on the phone tier
        if (wind != null)
        {
            m.shader = wind; m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(VEG + "textures/" + tex));
            m.SetFloat("_Cutoff", cutout ? 0.5f : 0f); m.SetFloat("_Cull", cutout ? 0f : 2f); m.SetFloat("_WindScale", 1f);
            m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
        }
        m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }

    /// <summary>Prefab for veg_&lt;species&gt;_&lt;a|b&gt; (null when the pack isn't in the project).</summary>
    public static GameObject Veg(string id)
    {
        if (vegPrefabs.TryGetValue(id, out var cached) && cached != null) return cached;
        string fbx = VEG + id + ".fbx"; var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (src == null) return null;
        Directory.CreateDirectory(VEG + "Prefabs");
        var bark = VegMat("veg_bark", "veg_bark_atlas.png", false, 0.1f);
        var leaf = VegMat("veg_leaf", "veg_leaf_atlas.png", true, 0.05f);
        var barkB = VegMat("veg_bark_b", "veg_bark_atlas_b.png", false, 0.1f);    // second atlas pair (the pines, later species)
        var leafB = VegMat("veg_leaf_b", "veg_leaf_atlas_b.png", true, 0.05f);
        var imp = VegMat("veg_imp_" + id, "imp_" + id + ".png", true, 0.05f);
        imp.SetColor("_BaseColor", new Color(0.86f, 0.88f, 0.84f));   // the near-flat-lit bakes read pale next to LOD2 under the sun (README gaps)
        var go = (GameObject)Object.Instantiate(src); go.name = id;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool bb = r.name.EndsWith("_LOD3");
            var sm = r.sharedMaterials;
            for (int i = 0; i < sm.Length; i++) { string n = sm[i] != null ? sm[i].name : ""; bool b2 = n.EndsWith("_b") || n.Contains("_b_") || n.Contains("atlas_b"); sm[i] = bb || n.Contains("imp") ? imp : n.Contains("leaf") ? (b2 ? leafB : leaf) : (b2 ? barkB : bark); }
            r.sharedMaterials = sm;
            r.shadowCastingMode = bb || r.name.EndsWith("_LOD2") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        Debug.Log($"[SkiView] veg {id}: " + string.Join(", ", go.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.EndsWith("_LOD0")).SelectMany(r => r.sharedMaterials).Select(m => m.name)));
        var lg = go.GetComponent<LODGroup>() ?? go.AddComponent<LODGroup>();
        var lods = new List<LOD>(); float[] tr = { 0.30f, 0.12f, 0.05f, 0.012f };
        for (int k = 0; k < 4; k++) { var t = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(c => c.name.EndsWith("_LOD" + k)); if (t) lods.Add(new LOD(tr[k], t.GetComponentsInChildren<Renderer>(true))); }
        lg.SetLODs(lods.ToArray()); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, VEG + "Prefabs/" + id + ".prefab");
        Object.DestroyImmediate(go);
        vegPrefabs[id] = prefab; return prefab;
    }
    /// <summary>T2: Tall Pine / Heron Marsh copy of a veg prefab with a dithered LOD cross-fade (0.12 transition); other lakes keep the shared prefab.</summary>
    public static bool vegCrossFade;
    public static GameObject VegXF(string id)
    {
        var src = Veg(id); if (src == null) return null;
        string p = VEG + "Prefabs/" + id + "_xf.prefab";
        if (vegPrefabs.TryGetValue(id + "_xf", out var c) && c != null) return c;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src); PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); go.name = id + "_xf";
        var lg = go.GetComponent<LODGroup>(); var lods = lg.GetLODs(); for (int k = 0; k < lods.Length; k++) lods[k].fadeTransitionWidth = 0.12f;
        lg.SetLODs(lods); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = false;
        var pf = PrefabUtility.SaveAsPrefabAsset(go, p); Object.DestroyImmediate(go); vegPrefabs[id + "_xf"] = pf; return pf;
    }

    /// <summary>T2 import settings for Christopher's soft-alpha leaf atlases and impostors (README T2).</summary>
    public static void VegTextureImport()
    {
        foreach (var path in Directory.GetFiles(VEG + "textures", "*.png"))
        {
            string f = Path.GetFileName(path); if (!f.StartsWith("veg_leaf_atlas")) continue;   // the v2 imp_ layout needs the v2 LOD3 meshes (held)
            var ti = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/')); if (ti == null) continue;
            ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.5f;
            ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; ti.wrapMode = TextureWrapMode.Clamp; ti.anisoLevel = 4; ti.maxTextureSize = Mathf.Max(ti.maxTextureSize, 1024);
            var a = ti.GetPlatformTextureSettings("Android"); a.overridden = true; a.format = TextureImporterFormat.ASTC_6x6; a.maxTextureSize = 1024; ti.SetPlatformTextureSettings(a);
            ti.SaveAndReimport();
        }
        Debug.Log("[SkiView] T2 veg texture import settings applied");
    }
    static bool VegReady => File.Exists(VEG + "veg_live_oak_a.fbx");
}

public static partial class SkiViewBuilder
{
    /// <summary>Terrain prototype list that grows a veg prefab on first use.</summary>
    class VegProtos
    {
        public readonly List<TreePrototype> list; readonly Dictionary<string, int> idx = new Dictionary<string, int>();
        public int count;
        public VegProtos(IEnumerable<TreePrototype> baseProtos) { list = baseProtos.ToList(); }
        public int this[string id] { get { if (!idx.TryGetValue(id, out int i)) { i = list.Count; list.Add(new TreePrototype { prefab = vegCrossFade ? VegXF(id) : Veg(id) }); idx[id] = i; } count++; return i; } }
    }
    static string AB(System.Func<float> fr, string sp, float pa = 0.5f) => "veg_" + sp + (fr() < pa ? "_a" : "_b");

    /// <summary>Which vegetation-pack prefab replaces a generator tree kind at a lake (null keeps the procedural tree; always null for
    /// Cedar Bend's PNW interior forest, alder, cottonwood, mesquite and creosote - none of them are in the pack). vs = height scale
    /// against the prefab's native height (the pack is modelled at real size, so ~1).</summary>
    static string VegPick(string lid, string set, int kind, float s, System.Func<float> fr, out float vs)
    {
        vs = s * (0.9f + fr() * 0.2f);
        if (set == "desert")
            switch (kind)
            {
                case 1: return "veg_palo_verde_a";
                case 2: return AB(fr, "fan_palm", 0.6f);
                case 3: { var id = AB(fr, "saguaro", 0.65f); return id; }
                case 5: return "veg_date_palm_a";
                case 7: vs = (0.75f + 0.35f * s) * (0.9f + fr() * 0.2f); return AB(fr, "ponderosa_pine", 0.5f);   // higher ground
                case 6: return "veg_ocotillo_a";
                default: return null;
            }
        if (set == "gulf")
            switch (kind)
            {
                case 0: case 1:   // pine flatwoods: slash pine (+ longleaf at Saltwhite) over saw palmetto
                    vs = s * (0.85f + fr() * 0.2f);
                    return lid == "saltwhite" && fr() < 0.45f ? AB(fr, "longleaf_pine", 0.65f) : AB(fr, "slash_pine", 0.6f);
                case 2: return AB(fr, "sabal_palm", 0.55f);
                case 3: return AB(fr, "royal_palm", 0.5f);
                case 4: return AB(fr, "saw_palmetto", 0.5f);
                default: return null;
            }
        switch (kind)
        {
            case 0: if (lid != "cedarbend") return null; { bool cedar = fr() < 0.3f; vs = (cedar ? 1.15f : 0.95f) + fr() * 0.4f; return cedar ? "veg_western_red_cedar_a" : "veg_douglas_fir_a"; }
            case 2: if (lid != "cedarbend") return null; vs = s * (0.95f + fr() * 0.4f); return "veg_douglas_fir_b";
            case 1: if (lid != "cedarbend" || fr() < 0.5f) return null; return "veg_maple_a";   // the alder half stays procedural
            case 4:   // pines behind the banks: loblolly (southeast), slash / longleaf in the Gulf woodland; Cedar Bend's PNW interior stays procedural
                if (lid == "cedarbend" || lid == "skiview") return null;
                vs = (0.7f + 0.4f * s) * (0.9f + fr() * 0.2f);
                if (lid == "gulfwind") return fr() < 0.6f ? AB(fr, "slash_pine", 0.6f) : AB(fr, "longleaf_pine", 0.6f);
                return AB(fr, "loblolly_pine", 0.55f);
            case 10:   // Tall Pine windbreak: redwoods with fuller crowns (VegWidth), every 4th a red cedar so the row reads as a dense green wall, not poles
            {
                float r = fr(); if (r < 0.25f) { vs = s * (1.05f + fr() * 0.2f); return "veg_western_red_cedar_a"; }
                bool a = r < 0.7f; vs = s * (a ? 0.85f + fr() * 0.15f : 1.1f + fr() * 0.2f); return a ? "veg_redwood_a" : "veg_redwood_b";
            }
            case 11: return "veg_live_oak_a";
            case 12: vs = s * (0.8f + fr() * 0.2f); return AB(fr, "bald_cypress", 0.6f);
            case 13: return AB(fr, "sabal_palm", 0.55f);
            case 14: vs = s * (0.9f + fr() * 0.4f); return AB(fr, "saw_palmetto", 0.4f);
            case 15: return "veg_maple_a";
            case 16: return "veg_oak_a";
            case 17: return AB(fr, "willow", 0.5f);
            case 18: return "veg_live_oak_b";
            case 19: return AB(fr, "western_red_cedar", 0.5f);
            default: return null;
        }
    }
    /// <summary>Crown width multiplier per prefab (the pack redwoods are narrow spires with a long clear bole; Tall Pine wants a fuller wall).</summary>
    static float VegWidth(string id) => id.StartsWith("veg_redwood") ? 1.45f : 1f;
    /// <summary>Kinds 12-19 (desert 5-6) are pack-only; without the pack they fall back to the nearest procedural kind.</summary>
    static int VegFallback(string set, int kind) => set == "desert" ? (kind == 5 ? 2 : kind == 6 ? 4 : kind == 7 ? 0 : kind)
        : kind switch { 12 => 11, 13 => 11, 14 => 7, 15 => 1, 16 => 1, 17 => 9, 18 => 11, 19 => 0, _ => kind };

    /// <summary>Small pack plants (palmetto) join LakeDetail's plant list (thinned first on phones).</summary>
    static int[] VegPlants(TerrainData td, int[] basePlants)
    {
        var l = new List<int>(basePlants); var pr = td.treePrototypes;
        for (int i = 0; i < pr.Length; i++) if (pr[i].prefab != null && pr[i].prefab.name.StartsWith("veg_saw_palmetto")) l.Add(i);
        return l.ToArray();
    }
}
