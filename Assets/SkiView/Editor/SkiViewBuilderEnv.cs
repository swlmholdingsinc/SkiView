using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SkiView;

/// <summary>Environment realism pass (v5): HDRI sky + sun alignment, fog, grading, textured terrain layers,
/// Douglas fir / western red cedar card trees as terrain tree prototypes (GPU-instanced, LODGroup with impostor LOD1),
/// plank docks, and planar-reflection water. All textures are CC0 (see Environment/LICENSES.md) or generated.</summary>
public static partial class SkiViewBuilder
{
    const string ENV = "Assets/SkiView/Environment/";
    const string ENVTEX = ENV + "Textures/";
    const string ENVMAT = ENV + "Materials/";
    const string ENVPRE = ENV + "Prefabs/";
    const int WATER_LAYER = 4;   // built-in "Water" layer; excluded from the planar reflection camera

    // ------------------------------------------------------------------ texture / material helpers
    static Texture2D EnvTex(string file, bool normal = false, int max = 1024, bool alpha = false, int androidMax = 0, bool repeatU = false)
    {
        string p = ENVTEX + file;
        var ti = AssetImporter.GetAtPath(p) as TextureImporter;
        if (ti == null) { AssetDatabase.ImportAsset(p); ti = AssetImporter.GetAtPath(p) as TextureImporter; }
        if (ti == null) { Debug.LogWarning("[SkiView] missing texture " + p); return null; }
        bool dirty = false;
        void Set<T>(T cur, T want, Action<T> a) { if (!EqualityComparer<T>.Default.Equals(cur, want)) { a(want); dirty = true; } }
        Set(ti.textureType, normal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => ti.textureType = v);
        Set(ti.maxTextureSize, max, v => ti.maxTextureSize = v);
        Set(ti.mipmapEnabled, true, v => ti.mipmapEnabled = v);
        if (repeatU) { Set(ti.wrapModeU, TextureWrapMode.Repeat, v => ti.wrapModeU = v); Set(ti.wrapModeV, TextureWrapMode.Clamp, v => ti.wrapModeV = v); }
        else Set(ti.wrapMode, alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, v => ti.wrapMode = v);
        Set(ti.alphaIsTransparency, alpha, v => ti.alphaIsTransparency = v);
        Set(ti.alphaSource, alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None, v => ti.alphaSource = v);
        Set(ti.textureCompression, TextureImporterCompression.Compressed, v => ti.textureCompression = v);
        Set(ti.anisoLevel, alpha ? 1 : 4, v => ti.anisoLevel = v);
        if (alpha) Set(ti.mipMapsPreserveCoverage, true, v => { ti.mipMapsPreserveCoverage = v; ti.alphaTestReferenceValue = 0.45f; });
        if (androidMax > 0)
        {
            var ap = ti.GetPlatformTextureSettings("Android");
            if (!ap.overridden || ap.maxTextureSize != androidMax) { ap.overridden = true; ap.maxTextureSize = androidMax; ap.format = TextureImporterFormat.Automatic; ti.SetPlatformTextureSettings(ap); dirty = true; }
        }
        if (dirty) ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    static Material EnvMat(string name, Texture2D albedo, Texture2D normal, Color tint, float smooth, Vector2 tiling,
                           bool clip = false, float cutoff = 0.45f, bool twoSided = false, float bump = 1f, bool spec = true)
    {
        Directory.CreateDirectory(ENVMAT);
        string p = ENVMAT + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", albedo); m.SetTextureScale("_BaseMap", tiling);
        m.SetColor("_BaseColor", tint);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
        else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
        m.SetFloat("_AlphaClip", clip ? 1 : 0); m.SetFloat("_Cutoff", cutoff);
        if (clip) { m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)RenderQueue.AlphaTest; m.SetOverrideTag("RenderType", "TransparentCutout"); }
        else { m.DisableKeyword("_ALPHATEST_ON"); m.renderQueue = -1; m.SetOverrideTag("RenderType", "Opaque"); }
        m.SetFloat("_Cull", twoSided ? 0 : 2);
        m.doubleSidedGI = twoSided;
        m.SetFloat("_SpecularHighlights", spec ? 1 : 0); if (spec) m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); else m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetFloat("_EnvironmentReflections", spec ? 1 : 0); if (spec) m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); else m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ sky, sun, ambient, fog
    [Serializable] class SunInfo { public float u, v, elevation_deg, azimuth_deg; }
    const float SUN_WORLD_AZ = -10f;   // pass 19 (Holly skiview_look_brief): 09:30 PDT July, compass bearing ~100 deg (east, a little south) -> atan2(z,x) = -10 deg
    const float SUN_EL = 38f;   // brief: elevation ~38 deg (the HDRI sun sits at 48 deg; its disc is rotated to the same bearing)

    static bool SeamlessOf(TextureImporter t) { var st = new TextureImporterSettings(); t.ReadTextureSettings(st); return st.seamlessCubemap; }
    const float SKY_CUBE_ROT_OFFSET = -90f;   // SkiViewCapture.SkyAlign: cubemap vs panoramic image match, best at -90 (err 2022 vs 18214 at 0)
    static void SetupSky(Light sun)
    {
        string hdr = ENV + "Sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr";
        var ti = AssetImporter.GetAtPath(hdr) as TextureImporter;
        if (ti != null && (ti.textureShape != TextureImporterShape.Texture2D || ti.mipmapEnabled || ti.maxTextureSize != 2048 || ti.wrapModeV != TextureWrapMode.Clamp))
        {
            ti.textureShape = TextureImporterShape.Texture2D; ti.mipmapEnabled = false; ti.maxTextureSize = 2048;
            ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.sRGBTexture = false;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
        }
        var pano = AssetDatabase.LoadAssetAtPath<Texture2D>(hdr);
        // reflection/ambient cubemap: a second, small import of the same HDRI (Cube, specular-convolved mips)
        string cubeP = ENV + "Sky/sky_reflection_cube.hdr";
        if (!File.Exists(cubeP)) { File.Copy(hdr, cubeP); AssetDatabase.ImportAsset(cubeP); }
        var ci = AssetImporter.GetAtPath(cubeP) as TextureImporter;
        if (ci != null && ci.textureShape != TextureImporterShape.TextureCube)
        {
            ci.textureShape = TextureImporterShape.TextureCube; ci.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
            ci.maxTextureSize = 512; ci.mipmapEnabled = true; ci.sRGBTexture = false;
            var s = new TextureImporterSettings(); ci.ReadTextureSettings(s); s.cubemapConvolution = TextureImporterCubemapConvolution.Specular; ci.SetTextureSettings(s);
            ci.textureCompression = TextureImporterCompression.Compressed;
            ci.SaveAndReimport();
        }
        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(cubeP);

        var si = JsonUtility.FromJson<SunInfo>(File.ReadAllText(ENV + "Sky/sun.json"));
        // Skybox/Panoramic latlong: tex dir angle atan2(z,x) = (0.5 - u) * 360 ; the shader rotates the sky geometry by +_Rotation
        float texAz = (0.5f - si.u) * 360f;
        float rot = Mathf.Repeat(SUN_WORLD_AZ - texAz, 360f);
        float el = SUN_EL * Mathf.Deg2Rad, az = SUN_WORLD_AZ * Mathf.Deg2Rad;
        Vector3 toSun = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az));
        sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
        sun.color = new Color(1f, 0.955f, 0.89f); sun.intensity = 1.55f;
        sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.82f; sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
        Debug.Log($"[SkiView] sky: HDRI sun u={si.u:0.000} el={si.elevation_deg:0.0} texAz={texAz:0.0} rot={rot:0.0} -> sun dir {toSun}");

        var sky = AssetDatabase.LoadAssetAtPath<Material>(MAT + "Sky.mat");
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(sky, MAT + "Sky.mat"); }
        sky.shader = Shader.Find("Skybox/Panoramic");
        sky.SetTexture("_MainTex", pano); sky.SetFloat("_Mapping", 1); sky.SetFloat("_ImageType", 0); sky.SetFloat("_MirrorOnBack", 0); sky.SetFloat("_Layout", 0);
        sky.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
        sky.SetFloat("_Rotation", rot); sky.SetFloat("_Exposure", 1.45f); sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        // pass 8: the latlong panorama drew a thin bright vertical seam where u wraps (world azimuth ~76 deg, also in the water
        // reflection). Draw the background from a cubemap import of the same HDRI instead: cube faces have no wrap seam.
        string bgP = ENV + "Sky/sky_bg_cube.hdr";
        if (!File.Exists(bgP)) { File.Copy(hdr, bgP); AssetDatabase.ImportAsset(bgP); }
        if (AssetImporter.GetAtPath(bgP) is TextureImporter bi && (bi.textureShape != TextureImporterShape.TextureCube || bi.mipmapEnabled || bi.maxTextureSize != 2048 || !SeamlessOf(bi)))
        {
            bi.textureShape = TextureImporterShape.TextureCube; bi.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
            bi.maxTextureSize = 2048; bi.mipmapEnabled = false; bi.sRGBTexture = false; bi.textureCompression = TextureImporterCompression.Compressed;
            var bs = new TextureImporterSettings(); bi.ReadTextureSettings(bs); bs.cubemapConvolution = TextureImporterCubemapConvolution.None; bs.seamlessCubemap = true; bi.SetTextureSettings(bs);   // fix up face-edge texels (a thin dark line at a face edge showed in the water reflection)
            bi.SaveAndReimport();
        }
        var bgCube = AssetDatabase.LoadAssetAtPath<Cubemap>(bgP);
        if (bgCube != null)
        {
            sky.shader = Shader.Find("Skybox/Cubemap");
            sky.SetTexture("_Tex", bgCube); sky.SetFloat("_Rotation", Mathf.Repeat(rot + SKY_CUBE_ROT_OFFSET, 360f));
            sky.SetFloat("_Exposure", 1.45f); sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            Debug.Log($"[SkiView] sky: background from cubemap (rotation {Mathf.Repeat(rot + SKY_CUBE_ROT_OFFSET, 360f):0.0})");
        }
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky; RenderSettings.sun = sun;
        // ambient: trilight fitted to the HDRI (upper hemisphere mean ~ (0.27,0.34,0.52) x exposure), ground = dry grass/firs bounce
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.50f, 0.60f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.42f, 0.40f);  // b10.22: less chalk fill on mid-bank shade
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.20f, 0.15f);
        RenderSettings.ambientIntensity = 1f;
        if (cube != null) { RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = cube; }
        RenderSettings.reflectionIntensity = 1f;
        // aerial perspective on the far fir line: exp2 haze matched to the HDRI horizon
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.00078f;
        RenderSettings.fogColor = new Color(0.64f, 0.71f, 0.79f);
    }

    static void BuildVolumeV5(Transform root)
    {
        string p = "Assets/SkiView/Settings_SkiViewVolume.asset";
        if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(prof, p);
        prof.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.22f); bl.threshold.Override(1.15f); bl.scatter.Override(0.6f); bl.highQualityFiltering.Override(false);
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0.15f); ca.contrast.Override(10f); ca.saturation.Override(4f);
        var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(8f); wb.tint.Override(1f);
        var smh = prof.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.96f, 1.0f, 1.03f, 0f)); smh.highlights.Override(new Vector4(1.02f, 1.0f, 0.97f, 0f));
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.16f); vg.smoothness.Override(0.45f);
        foreach (var c in prof.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, prof); }
        EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();
        var vgo = new GameObject("PostVolume"); vgo.transform.SetParent(root, false);
        var vol = vgo.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof;
    }

    /// <summary>Quality tiers: Mobile = hard shadows, 2 cascades, LOD bias 0.6, 1/4-res reflection; PC = soft shadows, 4 cascades, LOD bias 1.2, 1/2-res.</summary>
    static void ConfigureQualityTiers()
    {
        foreach (var path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (rp == null) continue;
            bool pc = path.Contains("PC");
            var so = new SerializedObject(rp); var sp = so.FindProperty("m_SoftShadowsSupported"); if (sp != null) { sp.boolValue = pc; so.ApplyModifiedPropertiesWithoutUndo(); }
            rp.shadowDistance = pc ? 260f : 160f;
            EditorUtility.SetDirty(rp);
        }
        int cur = QualitySettings.GetQualityLevel();
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            bool mobile = names[i].ToLowerInvariant().Contains("mobile");
            QualitySettings.lodBias = mobile ? 0.6f : 1.2f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.anisotropicFiltering = mobile ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
        }
        QualitySettings.SetQualityLevel(cur, false);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ terrain layers (CC0 PBR, normal maps)
    static TerrainLayer EnvLayer(string name, string color, string normal, float tile, float smooth, Color tint, float nscale = 1f)
    {
        string p = MAT + "TL_" + name + ".terrainlayer";
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, p); }
        l.diffuseTexture = EnvTex(color, false, 1024, false, 512);
        l.normalMapTexture = EnvTex(normal, true, 512, false, 256); l.normalScale = nscale;
        l.tileSize = new Vector2(tile, tile); l.smoothness = smooth; l.metallic = 0;
        l.diffuseRemapMin = Vector4.zero; l.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1);
        // smoothness from the layer constant, not the (opaque = 1.0) albedo alpha - otherwise the ground mirrors the sky like snow
        l.smoothnessSource = TerrainLayerSmoothnessSource.Constant;   // NOT DiffuseAlpha: jpg alpha=1 mirrored the sky as snow
        // Unity sometimes serializes the enum wrong on first write — force the Constant = 1 bit via SerializedObject
        var so = new SerializedObject(l); so.FindProperty("m_SmoothnessSource").intValue = 1; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(l);
        return l;
    }

    static TerrainLayer[] TerrainLayersV5() => new[]
    {
        // b10.22: mid-bank under trees — further albedo darken (no baked lighting in tex). Constant smoothness in EnvLayer.
        EnvLayer("Lawn", "lawn_color.jpg", "lawn_normal.png", 2.6f, 0.03f, new Color(0.44f, 0.55f, 0.30f)),
        EnvLayer("DryGrass", "drygrass_color.jpg", "drygrass_normal.png", 3.0f, 0.03f, new Color(0.40f, 0.48f, 0.28f)),
        EnvLayer("Forest", "forest_color.jpg", "forest_normal.png", 3.5f, 0.03f, new Color(0.36f, 0.32f, 0.25f)),
        EnvLayer("Mud", "shore_color.jpg", "shore_normal.png", 2.2f, 0.05f, new Color(0.32f, 0.25f, 0.18f), 1.2f),
    };

    // ------------------------------------------------------------------ trees: Douglas fir + western red cedar card trees
    class CardMesh { public List<Vector3> v = new(); public List<Vector3> n = new(); public List<Vector2> uv = new(); public List<int> trunk = new(), leaf = new(), core = new(); }

    static void Quad(CardMesh m, List<int> idx, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Func<Vector3, Vector3> nrm)
    {
        int k = m.v.Count;
        m.v.Add(a); m.v.Add(b); m.v.Add(c); m.v.Add(d);
        m.uv.Add(ua); m.uv.Add(ub); m.uv.Add(uc); m.uv.Add(ud);
        m.n.Add(nrm(a)); m.n.Add(nrm(b)); m.n.Add(nrm(c)); m.n.Add(nrm(d));
        idx.Add(k); idx.Add(k + 1); idx.Add(k + 2); idx.Add(k); idx.Add(k + 2); idx.Add(k + 3);
    }

    /// <summary>One tree, 16 m tall template (scaled per instance). Fir: narrow, high crown base, 5-branch whorls drooping 10-30 deg.
    /// Cedar: fuller, lower crown, strongly drooping lacy sprays. Spherical crown normals for soft volumetric shading.</summary>
    static CardMesh TreeCards(bool cedar, int seed)
    {
        var r = new System.Random(seed);
        float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var m = new CardMesh();
        const float H = 16f;
        float crownBase = cedar ? 1.0f : 1.8f, Rb = cedar ? 3.6f : 3.7f;   // pass 7: lake-edge trees keep their low branches (was 2.2 / 4.2 m of bare trunk)
        float yMid = crownBase + (H - crownBase) * 0.42f;
        Func<Vector3, Vector3> crownN = p => (new Vector3(p.x, (p.y - yMid) * 0.35f, p.z).normalized * 0.75f + Vector3.up * 0.45f).normalized;
        // trunk: 7-sided tapered cylinder, bark UV (2 m tile)
        int sides = 7; float trunkTop = H * 0.9f, r0 = cedar ? 0.36f : 0.30f;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            float rt = 0.05f;
            Vector3 p0 = d0 * r0 + Vector3.down * 0.4f, p1 = d1 * r0 + Vector3.down * 0.4f, p2 = d1 * rt + Vector3.up * trunkTop, p3 = d0 * rt + Vector3.up * trunkTop;
            float u0 = (float)i / sides * 1.5f, u1 = (float)(i + 1) / sides * 1.5f, vt = trunkTop / 2.5f;
            int k = m.v.Count;
            m.v.Add(p0); m.v.Add(p3); m.v.Add(p2); m.v.Add(p1);
            m.uv.Add(new Vector2(u0, 0)); m.uv.Add(new Vector2(u0, vt)); m.uv.Add(new Vector2(u1, vt)); m.uv.Add(new Vector2(u1, 0));
            m.n.Add(d0); m.n.Add(d0); m.n.Add(d1); m.n.Add(d1);
            m.trunk.Add(k); m.trunk.Add(k + 1); m.trunk.Add(k + 2); m.trunk.Add(k); m.trunk.Add(k + 2); m.trunk.Add(k + 3);
        }
        // core: 2 crossed vertical impostor planes inside the crown (fill density between sprays)
        for (int c = 0; c < 2; c++)
        {
            float a = c * Mathf.PI / 2 + 0.3f; Vector3 s = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (H * 0.29f);   // pass 7: wider dense core (was 0.195 H)
            Quad(m, m.core, -s, -s + Vector3.up * H, s + Vector3.up * H, s, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), crownN);
        }
        // branch whorls
        int whorls = cedar ? 14 : 13, per = cedar ? 6 : 5;
        float phase = Rnd(0, 6.28f);
        for (int w = 0; w < whorls; w++)
        {
            float f = (float)w / (whorls - 1);
            float y = Mathf.Lerp(crownBase, H - 0.7f, f) + Rnd(-0.2f, 0.2f);
            float rad = Rb * Mathf.Pow(1 - f, cedar ? 0.75f : 0.9f) + 0.25f;
            for (int b = 0; b < per; b++)
            {
                float az = phase + w * 2.4f + b * Mathf.PI * 2 / per + Rnd(-0.35f, 0.35f);
                float L = rad * Rnd(0.6f, 1.3f) * (cedar ? 1.05f : 1.12f);
                if (L < 0.35f || (!cedar && f > 0.15f && r.NextDouble() < 0.18)) continue;   // gappy, irregular fir crowns
                float droop = Mathf.Lerp(cedar ? 42f : 30f, cedar ? 18f : 8f, f) * Mathf.Deg2Rad * Rnd(0.8f, 1.2f);
                Vector3 hd = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                Vector3 dir = (hd * Mathf.Cos(droop) + Vector3.down * Mathf.Sin(droop)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, hd).normalized;
                float roll = Rnd(-25f, 25f);
                side = Quaternion.AngleAxis(roll, dir) * side;
                float W = L * (cedar ? 0.55f : 0.5f);
                Vector3 root = hd * 0.12f + Vector3.up * y;
                Vector3 tip = root + dir * L;
                Quad(m, m.leaf, root - side * W * 0.5f, root + side * W * 0.5f, tip + side * W * 0.5f, tip - side * W * 0.5f,
                     new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), crownN);
            }
        }
        // leader
        Quad(m, m.leaf, new Vector3(-0.35f, H - 1.8f, 0), new Vector3(-0.35f, H + 0.2f, 0), new Vector3(0.35f, H + 0.2f, 0), new Vector3(0.35f, H - 1.8f, 0),
             new Vector2(0.55f, 0.15f), new Vector2(1, 0.15f), new Vector2(1, 0.85f), new Vector2(0.55f, 0.85f), crownN);
        return m;
    }

    static Mesh ImpostorMesh(string name, bool cedar)
    {
        var m = new CardMesh();
        const float H = 16f; float half = H * 0.30f;   // pass 7: fuller impostor (was 0.25 H)
        Func<Vector3, Vector3> nrm = p => (new Vector3(p.x, 0.35f * (p.y - 7f), p.z).normalized * 0.6f + Vector3.up * 0.55f).normalized;
        for (int c = 0; c < 3; c++)
        {
            float a = c * Mathf.PI / 3 + 0.3f; Vector3 s = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * half;
            Quad(m, m.core, -s + Vector3.down * 0.4f, -s + Vector3.up * H, s + Vector3.up * H, s + Vector3.down * 0.4f, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), nrm);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(m.v); mesh.SetNormals(m.n); mesh.SetUVs(0, m.uv); mesh.SetTriangles(m.core, 0); mesh.RecalculateBounds();
        return SaveMesh(mesh, name);
    }

    static GameObject TreePrefab(string species, bool cedar, Material bark, Material spray, Material imp, int seed)
    {
        Directory.CreateDirectory(ENVPRE);
        var cm = TreeCards(cedar, seed);
        var mesh = new Mesh { name = "tree_" + species + "_lod0" };
        mesh.SetVertices(cm.v); mesh.SetNormals(cm.n); mesh.SetUVs(0, cm.uv);
        mesh.subMeshCount = 3; mesh.SetTriangles(cm.trunk, 0); mesh.SetTriangles(cm.leaf, 1); mesh.SetTriangles(cm.core, 2);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        SaveMesh(mesh, mesh.name);
        var lod1 = ImpostorMesh("tree_" + species + "_lod1", cedar);

        var go = new GameObject(species);
        var l0 = MeshObj("LOD0", go.transform, mesh, bark, spray, imp);
        var l1 = MeshObj("LOD1", go.transform, lod1, imp);
        l1.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        var lg = go.AddComponent<LODGroup>();
        // LOD0 while the tree is > 9% of screen height (~330 m for a 30 m fir at fov 52, x lodBias), impostor beyond; never culled
        lg.SetLODs(new[] { new LOD(0.2f, new Renderer[] { l0.GetComponent<Renderer>() }), new LOD(0.0015f, new Renderer[] { l1.GetComponent<Renderer>() }) });   // pass 7: dense impostor beyond ~150 m (the sparse card crowns read as spikes far away)
        lg.fadeMode = LODFadeMode.None;
        lg.RecalculateBounds();
        string pp = ENVPRE + species + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, pp);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static void BuildTreesV5(Venue v, Transform root, Terrain terrain)
    {
        var bark = EnvMat("Bark", EnvTex("bark_color.jpg", false, 512, false, 256), EnvTex("bark_normal.png", true, 512, false, 256), new Color(0.52f, 0.46f, 0.40f), 0.06f, Vector2.one, spec: false);
        // pass 7: darker conifer tints (the photos' tree line is a dense, dark mass)
        var firSpray = EnvMat("FirSpray", EnvTex("fir_spray.png", false, 512, true, 256), null, new Color(0.52f, 0.60f, 0.50f), 0.10f, Vector2.one, true, 0.42f, true, spec: false);
        var cedarSpray = EnvMat("CedarSpray", EnvTex("cedar_spray.png", false, 512, true, 256), null, new Color(0.52f, 0.58f, 0.48f), 0.10f, Vector2.one, true, 0.42f, true, spec: false);
        var firImp = EnvMat("FirImpostor", EnvTex("fir_impostor.png", false, 512, true, 256), null, new Color(0.55f, 0.62f, 0.52f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var cedarImp = EnvMat("CedarImpostor", EnvTex("cedar_impostor.png", false, 512, true, 256), null, new Color(0.54f, 0.60f, 0.50f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var fir = TreePrefab("DouglasFir", false, bark, firSpray, firImp, 7);
        var fir2 = TreePrefab("DouglasFir_B", false, bark, firSpray, firImp, 31);
        var cedar = TreePrefab("WesternRedCedar", true, bark, cedarSpray, cedarImp, 21);
        var cedar2 = TreePrefab("WesternRedCedar_B", true, bark, cedarSpray, cedarImp, 55);

        var alderLeaves = EnvMat("AlderLeaves", EnvTex("alder_leaves.png", false, 512, true, 256), null, new Color(0.72f, 0.78f, 0.70f), 0.12f, Vector2.one, true, 0.45f, true, spec: false);
        var mapleLeaves = EnvMat("MapleLeaves", EnvTex("maple_leaves.png", false, 512, true, 256), null, new Color(0.74f, 0.78f, 0.68f), 0.12f, Vector2.one, true, 0.45f, true, spec: false);
        var alderImp = EnvMat("AlderImpostor", EnvTex("alder_impostor.png", false, 512, true, 256), null, new Color(0.72f, 0.78f, 0.70f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var mapleImp = EnvMat("MapleImpostor", EnvTex("maple_impostor.png", false, 512, true, 256), null, new Color(0.74f, 0.78f, 0.68f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var alder = DeciduousPrefab("RedAlder", false, bark, alderLeaves, alderImp, 101);
        var maple = DeciduousPrefab("BigleafMaple", true, bark, mapleLeaves, mapleImp, 202);

        // pass 7: dark rounded canopy masses (cedar / bigleaf-maple crowns) that fill between the firs inside each stand
        var canopyLeaves = EnvMat("CanopyFillLeaves", EnvTex("cedar_spray.png", false, 512, true, 256), null, new Color(0.44f, 0.52f, 0.42f), 0.12f, Vector2.one, true, 0.42f, true, spec: false);
        var canopyImp = EnvMat("CanopyImpostor", EnvTex("canopy_impostor.png", false, 512, true, 256), null, new Color(0.70f, 0.76f, 0.70f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var canopy = DeciduousPrefab("CanopyFill", true, bark, canopyLeaves, canopyImp, 515);
        var td = terrain.terrainData;
        td.treePrototypes = new[] { new TreePrototype { prefab = fir }, new TreePrototype { prefab = cedar }, new TreePrototype { prefab = fir2 }, new TreePrototype { prefab = cedar2 },
                                    new TreePrototype { prefab = alder }, new TreePrototype { prefab = maple },
                                    new TreePrototype { prefab = ShrubPrefab("Understory", bark, 303) }, new TreePrototype { prefab = ShrubPrefab("Understory_B", bark, 404) },
                                    new TreePrototype { prefab = canopy } };
        td.RefreshPrototypes();
        Vector3 tp = terrain.transform.position, size = td.size;
        var rnd = new System.Random(8017);
        var list = new List<TreeInstance>();
        int count = v.treesFlat.Length / 4, skipped = 0, cedars = 0, broad = 0, giants = 0, gaps = 0, fillers = 0, shrubs = 0;
        int amr = td.alphamapResolution; var am = td.GetAlphamaps(0, 0, amr, amr);
        bool Add(float x, float y, float z, float hs, float ws, int proto, double ry, bool offset = false)
        {
            float nx = (x - tp.x) / size.x, nz = (z - tp.z) / size.z;
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) return false;
            if (InsideLot(x, z, offset ? 3f : 1.5f)) return false;   // pass 7: keep trees off the lot yards / houses
            if (offset)
            {
                // scattered extras must stay out of the water and off the lawns / open fields
                if (terrain.SampleHeight(new Vector3(x, 0, z)) + tp.y < 0.35f) return false;
                int ax = Mathf.Clamp((int)(nx * amr), 0, amr - 1), az = Mathf.Clamp((int)(nz * amr), 0, amr - 1);
                if (am[az, ax, 0] + am[az, ax, 1] + am[az, ax, 3] > 0.45f) return false;
            }
            list.Add(new TreeInstance { position = new Vector3(nx, (y - 0.3f - tp.y) / size.y, nz), heightScale = hs, widthScale = ws,
                rotation = (float)ry * Mathf.PI * 2f, prototypeIndex = proto, color = Color.white, lightmapColor = Color.white });
            return true;
        }
        float Fr() => (float)rnd.NextDouble();
        for (int i = 0; i < count; i++)
        {
            float x = v.treesFlat[i * 4], y = v.treesFlat[i * 4 + 1], z = v.treesFlat[i * 4 + 2], s = v.treesFlat[i * 4 + 3];
            float nx = (x - tp.x) / size.x, nz = (z - tp.z) / size.z;
            double rr = rnd.NextDouble(), rh = rnd.NextDouble(), rw = rnd.NextDouble(), ry = rnd.NextDouble(), rg = rnd.NextDouble();
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) { skipped++; continue; }
            // pass 6: irregular gaps (clumped clearings from low-frequency noise + a few random holes)
            float gapN = Mathf.PerlinNoise(x * 0.011f + 31.7f, z * 0.011f + 12.3f);
            if (gapN < 0.30f || (gapN < 0.38f && rg < 0.5) || rg < 0.03) { gaps++; continue; }
            // pass 7: natural clumps (~35 m stands, finer 15 m lobes). Thin between clumps; trees in a clump core are taller and
            // the edge trees smaller, so each stand reads as a rounded dome instead of a row of evenly spaced spikes.
            float clump = 0.65f * Mathf.PerlinNoise(x * 0.029f + 5.1f, z * 0.029f + 9.4f) + 0.35f * Mathf.PerlinNoise(x * 0.067f + 17.3f, z * 0.067f + 2.2f);
            if (clump < 0.32f && rg < 0.6) { gaps++; continue; }
            float core = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.72f, clump));
            float sizeMul = Mathf.Lerp(0.62f, 1.18f, core);
            // species: broadleaf (alder / maple) ~10%, ~30% on low ground near the water; cedar ~18%; rest Douglas fir
            float pBroad = y < 4f ? 0.30f : 0.10f;
            int proto; float hs, ws;
            if (rr < pBroad)
            {
                bool isMaple = rh < 0.45; proto = isMaple ? 5 : 4; broad++;
                hs = s * (0.85f + (float)rw * 0.5f); ws = s * (0.9f + (float)rw * 0.35f);
            }
            else if (rr < pBroad + 0.18)
            {
                proto = rw > 0.5 ? 3 : 1; cedars++;
                hs = s * (1.1f + (float)rh * 0.7f) * sizeMul; ws = hs * (0.78f + (float)rw * 0.25f);
            }
            else
            {
                proto = rw > 0.5 ? 2 : 0;
                bool giant = rh < 0.09 && core > 0.4f;   // old-growth emergents 40-60 m, standing out of the clump cores
                if (giant) giants++;
                hs = giant ? s * (2.6f + (float)rw * 1.1f) : s * (1.15f + (float)rh * 1.0f) * sizeMul;
                // fuller crowns: width ~0.7-0.95 of height (was ~0.5, which drew a narrow spike)
                ws = giant ? hs * 0.65f : hs * (0.80f + (float)rw * 0.25f);
            }
            Add(x, y, z, hs, ws, proto, ry);
            // clump cores get an extra, smaller tree 2.5-5 m away (mixed sizes, closes the gaps between crowns)
            if (core > 0.45f && Fr() < 0.75f)
            {
                float a = Fr() * 6.283f, d = 2.5f + Fr() * 2.5f, k = 0.5f + Fr() * 0.35f;
                int p2 = Fr() < 0.3f ? (Fr() < 0.5f ? 1 : 3) : (Fr() < 0.5f ? 0 : 2);
                if (Add(x + Mathf.Cos(a) * d, y, z + Mathf.Sin(a) * d, hs * k, hs * k * (0.8f + Fr() * 0.2f), p2, Fr(), true)) fillers++;
            }
            if (core > 0.45f && Fr() < 0.6f)
            {
                float a = Fr() * 6.283f, d = 3f + Fr() * 4f, hc = (0.75f + Fr() * 0.5f) * sizeMul;
                if (Add(x + Mathf.Cos(a) * d, y, z + Mathf.Sin(a) * d, hc, hc * (1.0f + Fr() * 0.35f), 8, Fr(), true)) fillers++;
            }
            // understory: salal / vine-maple / young-conifer shrubs under and in front of the stands (denser low by the water)
            int ns = (y < 5f ? 2 : 1) + (Fr() < 0.35f ? 1 : 0);
            for (int k2 = 0; k2 < ns; k2++)
            {
                if (Fr() > (y < 5f ? 0.9f : 0.55f)) continue;
                float a = Fr() * 6.283f, d = 1.2f + Fr() * 3.8f, hsS = 0.7f + Fr() * 1.1f;
                if (Add(x + Mathf.Cos(a) * d, y, z + Mathf.Sin(a) * d, hsS, hsS * (1.0f + Fr() * 0.7f), Fr() < 0.5f ? 6 : 7, Fr(), true)) shrubs++;
            }
        }
        PlantYardSpots(list, terrain, 5, 4, 6, 7);   // pass 14: maple / alder / understory where the lot FBX low-poly yard trees were
        int isl = PlantIslands(v, list, terrain);
        td.SetTreeInstances(list.ToArray(), true);
        terrain.treeDistance = 2400f; terrain.treeBillboardDistance = 2400f; terrain.treeCrossFadeLength = 0f;
        terrain.treeMaximumFullLODCount = 50000; terrain.drawTreesAndFoliage = true; terrain.treeLODBiasMultiplier = 1f;
        EditorUtility.SetDirty(td);
        Debug.Log($"[SkiView] trees v7: {list.Count} instances ({cedars} cedar, {broad} alder/maple, {giants} giants 40-60 m, {fillers} clump fillers, {shrubs} understory, {isl} on the turn islands), {gaps} removed for gaps/clumping, {skipped} outside");
        BuildHills(root);
    }

    /// <summary>Scott (Oct 23): "the turn islands are covered in trees". Dense Pacific Northwest stand edge to edge on both islands:
    /// Douglas fir ~50%, western red cedar ~28%, red alder / bigleaf maple ~14%, dark canopy fill between crowns, and salal /
    /// vine-maple undergrowth in a ring at the shoreline. Heights vary 0.6-1.5x with a taller core. No crown reaches over the boat's
    /// turn path: trunk-to-path distance >= 9 m + crown radius (the skier swings inside the boat in the turn). Terrain trees use
    /// the LODGroup prefabs (card LOD0, impostor LOD1) and draw instanced.</summary>
    static int PlantIslands(Venue v, List<TreeInstance> list, Terrain terrain)
    {
        if (islandLand == null || islandLand.Count == 0 || v.path?.x == null) return 0;
        var td = terrain.terrainData; Vector3 tp = terrain.transform.position, size = td.size;
        var px = v.path.x; var pz = v.path.z;
        float PathDist(float x, float z) { float b = float.MaxValue; for (int i = 0; i < px.Length; i += 2) { float dx = px[i] - x, dz = pz[i] - z; float d = dx * dx + dz * dz; if (d < b) b = d; } return Mathf.Sqrt(b); }
        var rnd = new System.Random(1714); float Fr() => (float)rnd.NextDouble();
        // centroid per island for the taller core
        var cs = new List<Vector2>(); for (int i = 0; i + 1 < v.islandCentres.Length; i += 2) cs.Add(new Vector2(v.islandCentres[i], v.islandCentres[i + 1]));
        // jittered 4.2 m grid over the island land
        var cellsTaken = new HashSet<Vector2Int>(); int n = 0;
        foreach (var p in islandLand)
        {
            var key = new Vector2Int(Mathf.FloorToInt(p.x / 4.2f), Mathf.FloorToInt(p.z / 4.2f)); if (!cellsTaken.Add(key)) continue;
            float x = (key.x + 0.15f + Fr() * 0.7f) * 4.2f, z = (key.y + 0.15f + Fr() * 0.7f) * 4.2f;
            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + tp.y; if (y < 0.25f) continue;
            float dc = float.MaxValue; foreach (var c in cs) dc = Mathf.Min(dc, Vector2.Distance(c, new Vector2(x, z)));
            float core = Mathf.Clamp01(1f - dc / 40f), pd = PathDist(x, z);
            float nx = (x - tp.x) / size.x, nz = (z - tp.z) / size.z; if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
            void Put(float wx, float wz, int proto, float hs, float ws)
            {
                float wy = terrain.SampleHeight(new Vector3(wx, 0, wz)) + tp.y;
                list.Add(new TreeInstance { position = new Vector3((wx - tp.x) / size.x, (wy - 0.3f - tp.y) / size.y, (wz - tp.z) / size.z), heightScale = hs, widthScale = ws,
                    rotation = Fr() * Mathf.PI * 2f, prototypeIndex = proto, color = Color.white, lightmapColor = Color.white }); n++;
            }
            bool shore = y < 1.3f;
            // undergrowth everywhere, denser at the shoreline
            for (int k = 0, ns = shore ? 3 : 1; k < ns; k++)
            {
                float a = Fr() * 6.283f, d = Fr() * 2.2f, hsS = 0.6f + Fr() * 1.0f, sx = x + Mathf.Cos(a) * d, sz = z + Mathf.Sin(a) * d;
                if (PathDist(sx, sz) > 7f + hsS * 1.2f) Put(sx, sz, Fr() < 0.5f ? 6 : 7, hsS, hsS * (1.0f + Fr() * 0.6f));
            }
            // tree: crown radius ~3.7 m x widthScale (16 m template)
            float r = Fr(); int proto; float hs, ws;
            float sizeMul = Mathf.Lerp(0.6f, 1.15f, core) * (0.85f + Fr() * 0.35f);
            if (r < 0.14f) { proto = Fr() < 0.45f ? 5 : 4; hs = 1.0f * sizeMul; ws = hs * (0.9f + Fr() * 0.3f); }
            else if (r < 0.42f) { proto = Fr() < 0.5f ? 1 : 3; hs = 1.35f * sizeMul; ws = hs * (0.75f + Fr() * 0.2f); }
            else if (r < 0.52f) { proto = 8; hs = 0.9f * sizeMul; ws = hs * 1.15f; }
            else { proto = Fr() < 0.5f ? 0 : 2; hs = (1.5f + Fr() * 0.7f) * sizeMul; ws = hs * (0.62f + Fr() * 0.2f); }
            if (pd < 9f + 3.7f * ws) { ws = (pd - 9f) / 3.7f; hs = Mathf.Min(hs, ws * 1.6f); if (ws < 0.35f) continue; }   // smaller trees at the edge; never over the path
            Put(x, z, proto, hs, ws);
        }
        return n;
    }

    // ------------------------------------------------------------------ docks: planks with world-metre UVs
    /// <summary>Box with per-face UVs in metres / tile, planks running across local X (dock width).</summary>
    static GameObject PlankBox(string name, Transform parent, Vector3 pos, Vector3 size, Material m, float tile = 1.2f, Vector3? euler = null)
    {
        string key = $"plankbox_{size.x:0.00}_{size.y:0.00}_{size.z:0.00}_{tile:0.0}";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MESH + key + ".asset");
        if (mesh == null)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 h = size * 0.5f;
            void Face(Vector3 nn, Vector3 u, Vector3 w, float du, float dw)
            {
                int k = v.Count; Vector3 c = Vector3.Scale(nn, h);
                Vector3 hu = u * du * 0.5f, hw = w * dw * 0.5f;
                v.Add(c - hu - hw); v.Add(c - hu + hw); v.Add(c + hu + hw); v.Add(c + hu - hw);
                uv.Add(new Vector2(-du / 2, -dw / 2) / tile); uv.Add(new Vector2(-du / 2, dw / 2) / tile); uv.Add(new Vector2(du / 2, dw / 2) / tile); uv.Add(new Vector2(du / 2, -dw / 2) / tile);
                for (int i = 0; i < 4; i++) n.Add(nn);
                t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k); t.Add(k + 2); t.Add(k + 3);
            }
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, MESH + key + ".asset");
        }
        var g = MeshObj(name, parent, mesh, m);
        g.transform.localPosition = pos; if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
        return g;
    }

    static Material dockMat, pileMat, canopyMat, frameMat;
    internal static Material LawnBoxMat() => EnvMat("LawnBox", EnvTex("lawn_color.jpg", false, 1024, false, 512), EnvTex("lawn_normal.png", true, 512, false, 256), new Color(0.55f, 0.70f, 0.40f), 0.04f, Vector2.one, spec: false);  // b10.22: darker lawn box under shade
    static void DockMaterials()
    {
        dockMat = EnvMat("DockPlanks", EnvTex("dock_color.jpg", false, 1024, false, 512), EnvTex("dock_normal.png", true, 512, false, 256), new Color(1.05f, 0.95f, 0.85f), 0.18f, Vector2.one, bump: 0.8f);
        pileMat = EnvMat("DockPile", EnvTex("dock_color.jpg", false, 1024, false, 512), EnvTex("dock_normal.png", true, 512, false, 256), new Color(0.45f, 0.40f, 0.36f), 0.1f, new Vector2(0.4f, 1.5f));
        canopyMat = EnvMat("CanopyGreen", EnvTex("canopy_metal.png", false, 256, false, 256), null, new Color(0.62f, 0.78f, 0.66f), 0.35f, new Vector2(2f, 1f), twoSided: true);
        frameMat = Lit("LiftAluminium", new Color(0.78f, 0.80f, 0.82f), 0.6f, 0.8f);
    }

    /// <summary>Low lake docks like Scott's photo: flush plank deck ~0.3 m above the water, planks across the width,
    /// U-shaped slip around a boat lift with a green canopy on the hero lot; simple deck + head on the others.</summary>
    static void DockV5(Transform root, House hs)
    {
        if (dockMat == null) DockMaterials();
        Vector3 s = new Vector3(hs.shore[0], 0, hs.shore[1]);
        Vector3 toWater = -new Vector3(hs.normal[0], 0, hs.normal[1]);
        var d = new GameObject($"Dock_{hs.lot}").transform; d.SetParent(root, false);
        d.position = s - toWater * 2f; d.rotation = Quaternion.LookRotation(toWater);
        bool hero = hs.lot == "8017";
        float len = hero ? 16f : 12f, deckY = 0.32f, th = 0.14f;
        if (hero)
        {
            // U slip: two fingers + head, lift + canopy in the middle
            PlankBox("Walk", d, new Vector3(-2.6f, deckY, len / 2), new Vector3(1.8f, th, len), dockMat);
            PlankBox("Finger", d, new Vector3(2.6f, deckY, len / 2 + 2f), new Vector3(1.2f, th, len - 4f), dockMat);
            PlankBox("Head", d, new Vector3(0, deckY, len + 1.0f), new Vector3(7.0f, th, 2.0f), dockMat);
            PlankBox("Shore", d, new Vector3(-2.6f, deckY, -0.6f), new Vector3(4.2f, th, 1.6f), dockMat);
            float lz = len * 0.52f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 1.55f, z = lz + (i < 2 ? -3.2f : 3.2f);
                Prim(PrimitiveType.Cylinder, "LiftPost" + i, d, new Vector3(x, 1.4f, z), new Vector3(0.12f, 1.7f, 0.12f), frameMat);
            }
            Box("LiftBunkL", d, new Vector3(-0.75f, 0.05f, lz), new Vector3(0.18f, 0.12f, 6.0f), frameMat);
            Box("LiftBunkR", d, new Vector3(0.75f, 0.05f, lz), new Vector3(0.18f, 0.12f, 6.0f), frameMat);
            // peaked green canopy (two pitched panels + ridge + short skirts), like the lift canopies in Scott's dock photo
            for (int sd = -1; sd <= 1; sd += 2)
            {
                var pnl = PlankBox("Canopy" + (sd < 0 ? "L" : "R"), d, new Vector3(sd * 0.92f, 3.42f, lz), new Vector3(2.0f, 0.04f, 7.8f), canopyMat, 1.8f, new Vector3(0, 0, -sd * 17f));
                pnl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                var sk = PlankBox("Skirt" + (sd < 0 ? "L" : "R"), d, new Vector3(sd * 1.86f, 3.0f, lz), new Vector3(0.03f, 0.35f, 7.8f), canopyMat, 1.8f);
                sk.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            }
            Box("CanopyRidge", d, new Vector3(0, 3.71f, lz), new Vector3(0.10f, 0.08f, 7.9f), frameMat);
            Box("CanopyFrameF", d, new Vector3(0, 3.12f, lz - 3.2f), new Vector3(3.3f, 0.08f, 0.08f), frameMat);
            Box("CanopyFrameB", d, new Vector3(0, 3.12f, lz + 3.2f), new Vector3(3.3f, 0.08f, 0.08f), frameMat);
            // fire pit + chairs on the head
            Prim(PrimitiveType.Cylinder, "FirePit", d, new Vector3(0, deckY + 0.2f, len + 1.0f), new Vector3(1.0f, 0.18f, 1.0f), Lit("Stone", Color.gray));
            var chairM = Lit("ChairWhite", new Color(0.86f, 0.86f, 0.84f), 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2 + 0.4f;
                var ch = new GameObject("Adirondack" + i).transform; ch.SetParent(d, false);
                ch.localPosition = new Vector3(Mathf.Cos(a) * 2.3f, deckY + 0.07f, len + 1.0f + Mathf.Sin(a) * 0.75f);
                ch.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0, -Mathf.Sin(a)));
                Box("Seat", ch, new Vector3(0, 0.36f, 0.05f), new Vector3(0.62f, 0.06f, 0.55f), chairM, new Vector3(-8, 0, 0));
                Box("Back", ch, new Vector3(0, 0.62f, -0.28f), new Vector3(0.6f, 0.62f, 0.05f), chairM, new Vector3(-22, 0, 0));
                Box("ArmL", ch, new Vector3(-0.36f, 0.52f, 0.02f), new Vector3(0.12f, 0.03f, 0.62f), chairM);
                Box("ArmR", ch, new Vector3(0.36f, 0.52f, 0.02f), new Vector3(0.12f, 0.03f, 0.62f), chairM);
            }
            for (int i = 0; i <= 5; i++)
            {
                float z = i * len / 5;
                Prim(PrimitiveType.Cylinder, "PileW" + i, d, new Vector3(-3.45f, -0.3f, z), new Vector3(0.2f, 0.75f, 0.2f), pileMat);
                if (z > 2f) Prim(PrimitiveType.Cylinder, "PileF" + i, d, new Vector3(3.15f, -0.3f, z), new Vector3(0.2f, 0.75f, 0.2f), pileMat);
            }
        }
        else
        {
            PlankBox("Deck", d, new Vector3(0, deckY, len / 2), new Vector3(1.8f, th, len), dockMat);
            PlankBox("Head", d, new Vector3(0, deckY, len + 1.2f), new Vector3(5.0f, th, 2.4f), dockMat);
            for (int i = 0; i <= 4; i++)
            {
                float z = i * (len + 2.2f) / 4;
                Prim(PrimitiveType.Cylinder, "PostL" + i, d, new Vector3(-0.95f, -0.3f, z), new Vector3(0.18f, 0.7f, 0.18f), pileMat);
                Prim(PrimitiveType.Cylinder, "PostR" + i, d, new Vector3(0.95f, -0.3f, z), new Vector3(0.18f, 0.7f, 0.18f), pileMat);
            }
        }
    }

    // ------------------------------------------------------------------ water material + planar reflection
    static void WaterV5(GameObject water, Material wm, Camera cam)
    {
        water.layer = WATER_LAYER;
        wm.SetColor("_ShallowColor", new Color(0.13f, 0.14f, 0.08f));   // pass 19 brief: olive-brown edge
        wm.SetColor("_DeepColor", new Color(0.011f, 0.036f, 0.031f));   // pass 19 brief: dark teal-green #1E3B36..#24463F
        wm.SetFloat("_DepthFade", 1.6f);
        wm.SetVector("_WaveA", new Vector4(1, 0.3f, 0.010f, 9.0f));
        wm.SetVector("_WaveB", new Vector4(-0.35f, 1, 0.008f, 5.3f));
        wm.SetVector("_WaveC", new Vector4(0.8f, -0.55f, 0.006f, 3.1f));
        wm.SetVector("_WaveD", new Vector4(-0.95f, -0.25f, 0.005f, 1.9f));
        wm.SetFloat("_RippleStrength", 0.08f); wm.SetFloat("_GlassyRipple", 0.12f); wm.SetFloat("_BreezeScale", 0.010f);
        wm.SetFloat("_ReflDistort", 0.22f); wm.SetColor("_ReflTint", new Color(0.92f, 0.95f, 0.96f));
        wm.SetFloat("_SunSharpness", 1400f); wm.SetFloat("_WakeHeight", 0.8f);
        // pass 9: tileable foam texture (Tools/gen_foam.py) for the prop-wash trough and the wake crest caps
        string foamP = ENV + "Water/foam.png";
        if (AssetImporter.GetAtPath(foamP) is TextureImporter fi && (fi.sRGBTexture || fi.maxTextureSize != 512 || fi.wrapMode != TextureWrapMode.Repeat || fi.anisoLevel != 8))
        {
            fi.textureType = TextureImporterType.Default; fi.sRGBTexture = false; fi.alphaSource = TextureImporterAlphaSource.None;
            fi.wrapMode = TextureWrapMode.Repeat; fi.mipmapEnabled = true; fi.anisoLevel = 8;   // seen at grazing angles from the boat cam
             fi.maxTextureSize = 512; fi.textureCompression = TextureImporterCompression.Compressed;
            fi.SaveAndReimport();
        }
        wm.SetTexture("_FoamTex", AssetDatabase.LoadAssetAtPath<Texture2D>(foamP));
        EditorUtility.SetDirty(wm);
        if (cam != null)
        {
            var pr = cam.GetComponent<PlanarReflection>(); if (pr == null) pr = cam.gameObject.AddComponent<PlanarReflection>();
            pr.source = cam; pr.waterY = 0f; pr.resolutionScale = 0.5f; pr.mobileResolutionScale = 0.25f;
            pr.reflectLayers = ~(1 << WATER_LAYER) & ~(1 << 5);   // no water, no UI
            pr.farClip = 3000f;   // include the far hills ring
        }
    }
}
