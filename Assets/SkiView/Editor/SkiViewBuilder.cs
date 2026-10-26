using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SkiView;

/// <summary>Builds the SKI VIEW N1 greybox scene entirely from code (headless-friendly).
/// Unity -batchmode -nographics -projectPath /workspace/skiview-unity -executeMethod SkiViewBuilder.BuildAll -quit</summary>
public static partial class SkiViewBuilder
{
    const string GEN = "Assets/SkiView/Generated/";
    const string MAT = "Assets/SkiView/Materials/";
    const string MESH = "Assets/SkiView/Meshes/";
    public const string SCENE = "Assets/SkiView/Scenes/SkiViewLake.unity";

    [Serializable] class House { public string lot, arch; public float x, z, y, yaw; public float[] shore, normal, color; public bool dock; }
    [Serializable] class PathData { public float step; public float[] x, z, v, along; public int[] dir, spin; }
    [Serializable] class CourseData { public float[] fS; public float length, firstBuoy, spacing, buoyOffset, guideOffset, gateOffset, preGate; }
    [Serializable] class TerrainInfo { public float size, ymin, hrange; public int res; public float cx, cz; }   // pass 19: cx / cz terrain centre offset
    [Serializable] class RampData { public float[] lip, fwd; public float open_deg, ratio, length, height, width; }
    [Serializable] class Venue { public TerrainInfo terrain; public CourseData course; public PathData path; public float[] islandCentres; public House[] houses; public float[] treesFlat; public RampData ramp; public float[] lakeBounds; }

    static Dictionary<string, Material> mats = new Dictionary<string, Material>();

    [MenuItem("SkiView/Build All")]
    public static void BuildAll()
    {
        var t0 = DateTime.Now;
        ConfigureProject();
        SkiViewBuilderUI.Setup();   // pass 18
        SkiViewBuilderAudio.Setup();   // pass 18: James's sound pack (manifest.json) -> Assets/SkiView/Audio + Resources/SkiViewAudio/Bank: Julia's UI kit (fonts, icons, PanelSettings)
        SkiViewSkierSetup.WadeV3();   // b10.8: INTERNAL_ONLY Wade V3 materials before lake bake
        BuildScene();
        BuildLakes();   // pass 15: the other lakes (own scenes, reuse the home lake's rigs)
        Debug.Log($"[SkiView] BuildAll done in {(DateTime.Now - t0).TotalSeconds:0.0}s");
    }

    // ------------------------------------------------------------------ project settings
    public static void ConfigureProject()
    {
        PlayerSettings.companyName = "SWLM Holdings";
        PlayerSettings.productName = SkiView.SkiViewBranding.ProductName;   // Wade T2: from branding.json
        PlayerSettings.bundleVersion = "2.0.1";
        PlayerSettings.Android.bundleVersionCode = 201;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.swlm.cutline");
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.swlm.cutline");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        // pass 17: sideways in both directions on phones (auto-rotate between landscape left / right, never portrait)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.memorySize = 512;
        EditorUserBuildSettings.buildAppBundle = false;
        foreach (var path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (rp == null) { Debug.LogWarning("[SkiView] missing " + path); continue; }
            rp.supportsCameraDepthTexture = true;
            rp.supportsHDR = true;
            rp.shadowDistance = 220f;
            rp.shadowCascadeCount = path.Contains("PC") ? 4 : 2;
            rp.msaaSampleCount = path.Contains("PC") ? 4 : 2;
            try { rp.mainLightShadowmapResolution = path.Contains("PC") ? 4096 : 2048; } catch { }
            EditorUtility.SetDirty(rp);
        }
        AssetDatabase.SaveAssets();
        ConfigureQualityTiers();
    }

    // ------------------------------------------------------------------ helpers
    static Material Lit(string name, Color c, float smooth = 0.3f, float metal = 0f, Color? emission = null)
    {
        if (mats.TryGetValue(name, out var m) && m != null) return m;
        string p = MAT + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        mats[name] = m; return m;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, Vector3? euler = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = size;
        if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
        g.GetComponent<Renderer>().sharedMaterial = m;
        UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject MeshObj(string name, Transform parent, Mesh mesh, params Material[] m)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterials = m;
        return g;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string p = MESH + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old != null) AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    // unit gable roof prism: base 1x1 at y=0, ridge along local Z at y=1
    static Mesh gableMesh, hipMesh;
    static Mesh Gable()
    {
        if (gableMesh != null) return gableMesh;
        var v = new List<Vector3>(); var tri = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
        void Tri(Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); tri.AddRange(new[] { i, i + 1, i + 2 }); }
        Vector3 l0 = new Vector3(-.5f, 0, -.5f), l1 = new Vector3(-.5f, 0, .5f), r0 = new Vector3(.5f, 0, -.5f), r1 = new Vector3(.5f, 0, .5f);
        Vector3 t0 = new Vector3(0, 1, -.5f), t1 = new Vector3(0, 1, .5f);
        Quad(l0, l1, t1, t0); Quad(r1, r0, t0, t1); Tri(l0, t0, r0); Tri(r1, t1, l1);
        var m = new Mesh { name = "gable" }; m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return gableMesh = SaveMesh(m, "roof_gable");
    }
    static Mesh Hip()
    {
        if (hipMesh != null) return hipMesh;
        var v = new List<Vector3>(); var tri = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); }
        void Tri(Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); tri.AddRange(new[] { i, i + 1, i + 2 }); }
        Vector3 a0 = new Vector3(-.5f, 0, -.5f), a1 = new Vector3(-.5f, 0, .5f), b0 = new Vector3(.5f, 0, -.5f), b1 = new Vector3(.5f, 0, .5f);
        Vector3 t0 = new Vector3(0, 1, -.25f), t1 = new Vector3(0, 1, .25f);
        Quad(a0, a1, t1, t0); Quad(b1, b0, t0, t1); Tri(a0, t0, b0); Tri(b1, t1, a1);
        var m = new Mesh { name = "hip" }; m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return hipMesh = SaveMesh(m, "roof_hip");
    }

    static GameObject Roof(Transform p, bool hip, Vector3 pos, Vector3 size, Material m, float yaw = 0)
    {
        var g = MeshObj(hip ? "RoofHip" : "RoofGable", p, hip ? Hip() : Gable(), m);
        g.transform.localPosition = pos; g.transform.localScale = size; g.transform.localEulerAngles = new Vector3(0, yaw, 0);
        return g;
    }

    // ------------------------------------------------------------------ scene
    public static void BuildScene()
    {
        Directory.CreateDirectory(MAT); Directory.CreateDirectory(MESH); Directory.CreateDirectory("Assets/SkiView/Scenes");
        mats.Clear(); gableMesh = null; hipMesh = null; dockMat = null; meshTag = "home";
        var venue = JsonUtility.FromJson<Venue>(File.ReadAllText(GEN + "venue.json"));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- sun, sky, fog, ambient (one time-of-day preset: late-summer afternoon, sun in the SW over the west bank)
        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        SetupSky(sun);   // v5: HDRI sky (sun aligned to the HDRI sun), trilight ambient, custom reflection cubemap, exp2 haze

        var root = new GameObject("SkiViewLake").transform;
        BuildTerrain(venue, root);
        BuildWater(venue, root);
        BuildCourse(venue, root);
        BuildRamp(venue, root);
        BuildHouses(venue, root);
        BuildTreesV5(venue, root, root.GetComponentInChildren<Terrain>());
        BuildSouthEnd(venue, root, root.GetComponentInChildren<Terrain>());   // b10: basin lifts L1-L5, warming hut, start dock, lot 15 tree
        BuildShoreline(venue, root, root.GetComponentInChildren<Terrain>());   // pass 6
        BuildHouseProbes(venue, root);   // pass 8: v2 glass reflections
        var boat = BuildBoat(root);
        var skier = BuildSkier(root, out var body, out var ski, out var handle);

        // rope
        var ropeGo = new GameObject("Rope");
        ropeGo.transform.SetParent(root, false);
        var lr = ropeGo.AddComponent<LineRenderer>();
        lr.widthMultiplier = 0.022f; lr.positionCount = 5; lr.numCapVertices = 2; lr.numCornerVertices = 2;
        lr.sharedMaterial = Lit("Rope", new Color(0.95f, 0.85f, 0.25f), 0.2f);
        lr.shadowCastingMode = ShadowCastingMode.On;

        // camera + post
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 52f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 3000f; cam.allowHDR = true;
        camGo.AddComponent<AudioListener>();
        var acd = camGo.AddComponent<UniversalAdditionalCameraData>();
        acd.renderPostProcessing = true; acd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        acd.requiresDepthTexture = true;
        BuildVolumeV5(root);
        WaterV5(root.Find("Water").gameObject, AssetDatabase.LoadAssetAtPath<Material>(MAT + "Water.mat"), cam);

        // path + game
        var pathGo = new GameObject("BoatPath"); pathGo.transform.SetParent(root, false);
        var bp = pathGo.AddComponent<BoatPath>();
        bp.step = venue.path.step; bp.xs = venue.path.x; bp.zs = venue.path.z; bp.speeds = venue.path.v; bp.along = venue.path.along; bp.dirs = venue.path.dir; bp.spins = venue.path.spin;
        bp.courseFwdSouth = new Vector2(venue.course.fS[0], venue.course.fS[1]);
        var game = new GameObject("SkiViewGame").AddComponent<SkiViewGame>();
        game.ambientClip = SkiViewBuilderAudio.Ambient("skiview");   // pass 18: home-lake bed (Core_Home)
        game.path = bp; game.islandCentres = venue.islandCentres; game.boat = boat.transform; game.boatTow = FindDeep(boat.transform, "TowPoint"); game.boatWake = FindDeep(boat.transform, "WakeEmitter"); game.boatSprayPoint = FindDeep(boat.transform, "SprayEmitter");
        if (game.boatSprayPoint != null)
        {
            // pass 6: thin sheets + droplets (stretched streaks), not puffs
            game.boatSpray = SkiSpray.MakeSystem("Boat_PropDroplets", game.boatSprayPoint, SprayMaterial(), 1500, 0.75f, 1.0f, new Vector2(0.018f, 0.05f), 0.6f, 1.15f, true);
            game.boatSheet = SkiSpray.MakeSystem("Boat_TransomSheet", game.boatSprayPoint, SprayMaterial("sheet"), 2000, 0.5f, 1.0f, new Vector2(0.022f, 0.06f), 0.5f, 1.2f, true);
        }
        // pass 12: water reacting to the skier. Crash sheet (soft, larger), crash droplets (streaks), DWS plow spray (streaks)
        var fxRoot = new GameObject("SkierWaterFx").transform; fxRoot.SetParent(root, false);
        game.crashSheet = SkiSpray.MakeSystem("Crash_Sheet", fxRoot, SprayMaterial("sheet"), 900, 1.1f, 1.0f, new Vector2(0.35f, 0.8f), 0.22f, 2.4f, false);   // pass 14: torn translucent sheets as rotated billboards (no needles); pass 15: alpha 0.30 -> 0.22 (softer core)
        { var mm = game.crashSheet.main; mm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.283f); }
        game.crashMist = SkiSpray.MakeSystem("Crash_Mist", fxRoot, SprayMaterial("mist"), 700, 2.2f, 0.06f, new Vector2(0.3f, 0.8f), 0.11f, 3.6f, false);
        { var lv = game.crashMist.limitVelocityOverLifetime; lv.enabled = true; lv.dampen = 0.08f; lv.limit = 0.6f; }
        game.crashDrops = SkiSpray.MakeSystem("Crash_Droplets", fxRoot, SprayMaterial(), 1400, 1.3f, 1.0f, new Vector2(0.015f, 0.04f), 0.85f, 1.0f, true);
        { var rr = game.crashDrops.GetComponent<ParticleSystemRenderer>(); rr.velocityScale = 0.014f; rr.lengthScale = 1.0f; }   // pass 14: short motion streaks, not needles
        game.plowSpray = SkiSpray.MakeSystem("DWS_PlowSpray", fxRoot, SprayMaterial(), 1400, 0.8f, 1.0f, new Vector2(0.025f, 0.07f), 0.6f, 1.3f, true);
         game.skier = skier.transform; game.skierBody = body; game.ski = ski; game.handle = handle;
        game.rope = lr; game.cam = cam; game.rig = skier.GetComponentInChildren<SkierRig>();
        BuildJumpGear(skier.transform, game, ski);
        MergeSkier(game);                    // pass 18: 99 rigid skier pieces -> one mesh per bone/material
        BuildFirstPersonRig(cam, game);      // pass 18: first-person forearms, gloves, handle
        // pass 13: jump ramp + course
        game.jumpRamp = builtRamp; game.jumpCourse = builtJumpCourse != null ? builtJumpCourse.gameObject : null;
        game.rampFx = builtRampFx.ToArray(); game.rampFoam = builtRampFoam; game.rampWaterline = builtWaterline;
        if (venue.ramp != null) { game.rampHeight = venue.ramp.height; game.rampLenOut = venue.ramp.length; game.rampWidth = venue.ramp.width; game.rampUnder = RAMP_UNDER; }
        game.lakeScenes = LakeSceneNames(); game.lakeNames = LakeDisplayNames();   // pass 15: LAKE button
        // pass 18: course buoys merged (static), small pieces off the reflection, phone LOD tiers on the home lake too
        { var sc = root.Find("SlalomCourse"); if (sc != null) { var (b0, a0) = MergeRig(sc, new[] { sc }, "course_home"); Debug.Log($"[SkiView] course merged: {b0} -> {a0}"); } }
        int nr = 0; foreach (Transform c in root) if (c != skier.transform && c.name != "SlalomCourse") nr += TagNoReflect(c);
        Debug.Log($"[SkiView] no-reflect layer: {nr} small renderers");
        HomeLakeDetail(root, venue);
        game.Init();

        EditorSceneManager.SaveScene(scene, SCENE);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[SkiView] scene saved " + SCENE);
    }

    static void BuildVolume(Transform root)
    {
        string p = "Assets/SkiView/Settings_SkiViewVolume.asset";
        if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(prof, p);
        var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.35f); bl.threshold.Override(1.05f); bl.scatter.Override(0.65f);
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0.25f); ca.contrast.Override(8f); ca.saturation.Override(6f);
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.18f);
        foreach (var c in prof.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, prof); }
        EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();
        var vgo = new GameObject("PostVolume"); vgo.transform.SetParent(root, false);
        var vol = vgo.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof;
    }

    // ------------------------------------------------------------------ terrain (heightmap + splat from gen_venue.py)
    static Texture2D Tex(string file)
    {
        string p = GEN + file;
        var ti = AssetImporter.GetAtPath(p) as TextureImporter;
        if (ti != null && (ti.wrapMode != TextureWrapMode.Repeat || ti.textureCompression != TextureImporterCompression.Compressed))
        {
            ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Compressed; ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    static TerrainLayer Layer(string name, string tex, float tile, float smooth)
    {
        string p = MAT + "TL_" + name + ".terrainlayer";
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, p); }
        l.diffuseTexture = Tex(tex); l.tileSize = new Vector2(tile, tile); l.smoothness = smooth; l.metallic = 0;
        EditorUtility.SetDirty(l);
        return l;
    }

    static void BuildTerrain(Venue v, Transform root)
    {
        int res = v.terrain.res; float size = v.terrain.size;
        // create the asset first so the splat (alphamap) textures are saved as sub-assets
        string tdp = "Assets/SkiView/Generated/SkiViewTerrain.asset";
        if (File.Exists(tdp)) AssetDatabase.DeleteAsset(tdp);
        var td = new TerrainData();
        AssetDatabase.CreateAsset(td, tdp);
        td.heightmapResolution = res;
        td.size = new Vector3(size, v.terrain.hrange, size);
        var raw = File.ReadAllBytes(GEN + "heightmap.r16");
        var h = new float[res, res];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int i = (z * res + x) * 2;
                h[z, x] = (raw[i] | (raw[i + 1] << 8)) / 65535f;
            }
        lotPlaces = null;
        GradeLots(v, h);   // pass 7: pads under Christopher's lot FBXs (8017 drops 6.2 m to its shore)
        CarveSouthBasin(v, h);   // b10: community boat basin at the south end
        td.SetHeights(0, 0, h);
        IslandMask(v, h, res, size);
        td.terrainLayers = TerrainLayersV5();   // v5: CC0 PBR layers with normal maps (R lawn, G straw grass, B fir duff, A shoreline rock)
        td.alphamapResolution = 512;
        var splatBytes = File.ReadAllBytes(GEN + "splat.png");
        var sp = new Texture2D(2, 2); sp.LoadImage(splatBytes);
        int ar = td.alphamapResolution;
        var a = new float[ar, ar, 4];
        var lotC = LotCentres(v);
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                Color c = sp.GetPixelBilinear((x + 0.5f) / ar, (y + 0.5f) / ar);
                float s = c.r + c.g + c.b + c.a + 1e-4f;
                // pass 6: lawns are greener (watered) close to each house and straw-coloured out in the open fields;
                // per-lot greenness varies (some owners water, some don't), mottled with noise
                float gw = (c.r + c.g) / s;
                if (gw > 0.01f)
                {
                    float wx = -size / 2 + (x + 0.5f) / ar * size, wz = -size / 2 + (y + 0.5f) / ar * size;
                    float best = float.MaxValue, lotGreen = 0f;
                    foreach (var lc in lotC)
                    {
                        float dd = (wx - lc.c.x) * (wx - lc.c.x) + (wz - lc.c.y) * (wz - lc.c.y);
                        if (dd < best) { best = dd; lotGreen = 0.62f + 0.38f * Mathf.Abs(Mathf.Sin(lc.lot * 0.618f)); }
                    }
                    float d = Mathf.Sqrt(best);
                    // pass 7: stronger contrast - watered lawn holds full green out to ~25 m from the house, then turns to straw by ~55 m
                    float green = lotGreen * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 55f, d)));
                    green = Mathf.Clamp01(green * (0.85f + 0.3f * Mathf.PerlinNoise(wx * 0.05f + 3.1f, wz * 0.05f + 7.7f)));
                    green = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.7f, green));
                    a[y, x, 0] = gw * green; a[y, x, 1] = gw * (1f - green);
                }
                else { a[y, x, 0] = c.r / s; a[y, x, 1] = c.g / s; }
                a[y, x, 2] = c.b / s; a[y, x, 3] = c.a / s;
                // Scott (Oct 23): the turn islands are forest. Fir-needle duff under the trees, brown mud at the waterline.
                int hx = Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) / ar * (res - 1)), 0, res - 1), hz = Mathf.Clamp(Mathf.RoundToInt((y + 0.5f) / ar * (res - 1)), 0, res - 1);
                if (islandCell != null && islandCell[hz, hx])
                {
                    float hw = v.terrain.ymin + h[hz, hx] * v.terrain.hrange, mud = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.0f, hw));
                    a[y, x, 0] = 0f; a[y, x, 1] = 0f; a[y, x, 2] = 1f - mud; a[y, x, 3] = mud;
                }
            }
        td.SetAlphamaps(0, 0, a);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        var tgo = Terrain.CreateTerrainGameObject(td);
        tgo.name = "Terrain"; tgo.transform.SetParent(root, false);
        tgo.transform.position = new Vector3(-size / 2, v.terrain.ymin, -size / 2);
        var t = tgo.GetComponent<Terrain>();
        var tm = AssetDatabase.LoadAssetAtPath<Material>(MAT + "TerrainLit.mat");
        if (tm == null) { tm = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); AssetDatabase.CreateAsset(tm, MAT + "TerrainLit.mat"); }
        tm.EnableKeyword("_NORMALMAP"); EditorUtility.SetDirty(tm);
        t.materialTemplate = tm;
        t.heightmapPixelError = 4; t.basemapDistance = 20000; t.drawInstanced = true;
        t.shadowCastingMode = ShadowCastingMode.TwoSided;
    }

    // ------------------------------------------------------------------ water
    static void BuildWater(Venue v, Transform root)
    {
        // lake bounding box from venue.json (shore extents + 15 m), 2 m cells
        float x0 = -200, x1 = 120, z0 = -470, z1 = 400, cell = 2f;
        if (v.lakeBounds != null && v.lakeBounds.Length == 4)
        { x0 = Mathf.Floor(v.lakeBounds[0]); x1 = Mathf.Ceil(v.lakeBounds[1]); z0 = Mathf.Floor(v.lakeBounds[2]); z1 = Mathf.Ceil(v.lakeBounds[3]); }
        z0 = Mathf.Min(z0, -364f);   // b10: cover the south basin
        int nx = Mathf.CeilToInt((x1 - x0) / cell) + 1, nz = Mathf.CeilToInt((z1 - z0) / cell) + 1;
        var verts = new Vector3[nx * nz];
        for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++) verts[j * nx + i] = new Vector3(x0 + i * cell, 0, z0 + j * cell);
        var tris = new int[(nx - 1) * (nz - 1) * 6]; int k = 0;
        for (int j = 0; j < nz - 1; j++) for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                tris[k++] = a; tris[k++] = c; tris[k++] = b; tris[k++] = b; tris[k++] = c; tris[k++] = d;
            }
        var m = new Mesh { name = "LakeWater", indexFormat = IndexFormat.UInt32 };
        m.vertices = verts; m.triangles = tris; m.RecalculateNormals();
        m.bounds = new Bounds(new Vector3((x0 + x1) / 2, 0, (z0 + z1) / 2), new Vector3(x1 - x0, 4, z1 - z0));
        SaveMesh(m, "lake_water");
        var wm = AssetDatabase.LoadAssetAtPath<Material>(MAT + "Water.mat");
        if (wm == null) { wm = new Material(Shader.Find("SkiView/Water")); AssetDatabase.CreateAsset(wm, MAT + "Water.mat"); }
        wm.shader = Shader.Find("SkiView/Water");
        var g = MeshObj("Water", root, m, wm);
        var r = g.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
    }

    // ------------------------------------------------------------------ slalom course (IWWF dimensions)
    static void BuildCourse(Venue v, Transform root)
    {
        var c = new GameObject("SlalomCourse").transform; c.SetParent(root, false);
        Vector3 f = new Vector3(v.course.fS[0], 0, v.course.fS[1]);          // southbound
        Vector3 r = Vector3.Cross(Vector3.up, f);                               // skier-right when southbound (west)
        float half = v.course.length / 2;
        var turn = Lit("BuoyTurn", new Color(1f, 0.38f, 0.05f), 0.6f);
        var guide = Lit("BuoyGuide", new Color(1f, 0.85f, 0.1f), 0.6f);
        var gate = Lit("BuoyGate", new Color(0.95f, 0.12f, 0.1f), 0.6f);
        var pre = Lit("BuoyPreGate", new Color(0.2f, 0.85f, 0.3f), 0.6f);
        // pass 12: real sizes (IWWF rule 8.07 / AWSA): 23 cm (9 in) balls, skier turn buoys ~9 cm out of the water,
        // gate / pre-gate / boat guide balls ~11.5 cm out (half submerged). Was 28-40 cm balls riding high.
        const float BallD = 0.23f;
        void Ball(string n, Vector3 p, float above, Material m) => Prim(PrimitiveType.Sphere, n, c, p + Vector3.up * (above - BallD * 0.5f), Vector3.one * BallD, m);
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            Vector3 g = f * (sgn * half);
            Ball($"Gate{(sgn < 0 ? "N" : "S")}_L", g - r * v.course.gateOffset, 0.115f, gate);
            Ball($"Gate{(sgn < 0 ? "N" : "S")}_R", g + r * v.course.gateOffset, 0.115f, gate);
            Vector3 pg = f * (sgn * (half + v.course.preGate));
            Ball($"PreGate{(sgn < 0 ? "N" : "S")}_L", pg - r * v.course.gateOffset, 0.115f, pre);
            Ball($"PreGate{(sgn < 0 ? "N" : "S")}_R", pg + r * v.course.gateOffset, 0.115f, pre);
        }
        for (int i = 0; i < 6; i++)
        {
            float a = -half + v.course.firstBuoy + v.course.spacing * i;
            Vector3 ctr = f * a;
            float side = i % 2 == 0 ? 1 : -1;      // ball 1,3,5 skier-right (southbound)
            Ball($"Turn{i + 1}", ctr + r * side * v.course.buoyOffset, 0.09f, turn);
            Ball($"Guide{i + 1}_L", ctr - r * v.course.guideOffset, 0.115f, guide);
            Ball($"Guide{i + 1}_R", ctr + r * v.course.guideOffset, 0.115f, guide);
        }
    }

    // ------------------------------------------------------------------ houses (archetype kit, greybox)
    static void BuildHouses(Venue v, Transform root)
    {
        var hroot = new GameObject("Houses_EastBank_N_to_S").transform; hroot.SetParent(root, false);
        if (BuildHousesFbx(v, hroot)) return;   // pass 7: Christopher's QA-ready lot FBXs replace the greybox kit
        var glass = Lit("Glass", new Color(0.05f, 0.08f, 0.10f), 0.95f, 0.2f);
        var glassWarm = Lit("GlassWarm", new Color(0.10f, 0.09f, 0.07f), 0.9f, 0f, new Color(0.9f, 0.6f, 0.3f) * 0.6f);
        var trim = Lit("TrimWhite", new Color(0.92f, 0.92f, 0.90f), 0.3f);
        var roofGrey = Lit("RoofGrey", new Color(0.22f, 0.23f, 0.25f), 0.25f);
        var roofDark = Lit("RoofCharcoal", new Color(0.12f, 0.12f, 0.13f), 0.3f);
        var roofTile = Lit("RoofTile", new Color(0.45f, 0.25f, 0.18f), 0.2f);
        var stone = Lit("Stone", new Color(0.50f, 0.46f, 0.40f), 0.15f);
        var wood = Lit("DockWood", new Color(0.42f, 0.33f, 0.24f), 0.2f);
        var concrete = Lit("Concrete", new Color(0.62f, 0.62f, 0.60f), 0.2f);
        foreach (var hs in v.houses)
        {
            var h = new GameObject($"Lot_{hs.lot}_{hs.arch}").transform; h.SetParent(hroot, false);
            h.localPosition = new Vector3(hs.x, hs.y, hs.z); h.localEulerAngles = new Vector3(0, hs.yaw, 0);
            var wall = Lit("Siding_" + hs.lot, new Color(hs.color[0], hs.color[1], hs.color[2]), 0.2f);
            // pad / foundation
            Box("Pad", h, new Vector3(0, -0.6f, 0), new Vector3(24, 1.4f, 18), concrete);
            switch (hs.arch)
            {
                case "HERO": Hero(h, glassWarm, stone, roofDark, wood, concrete); break;
                case "A": // modern farmhouse, stacked gables
                    Box("Body", h, new Vector3(0, 3, 0), new Vector3(16, 6, 11), wall);
                    Roof(h, false, new Vector3(0, 6, 0), new Vector3(11.6f, 4.2f, 16.6f), roofDark, 90);
                    Box("Wing", h, new Vector3(-6, 2.2f, 4), new Vector3(6, 4.4f, 6), wall);
                    Roof(h, false, new Vector3(-6, 4.4f, 4), new Vector3(6.6f, 3.2f, 6.6f), roofDark);
                    Box("Porch", h, new Vector3(2, 0.3f, 6.5f), new Vector3(10, 0.4f, 2.6f), trim);
                    Windows(h, glass, 16, 6, 5.55f, 2, 5); break;
                case "B": // cream rambler, arched great-room windows, pool
                    Box("Body", h, new Vector3(0, 2.2f, 0), new Vector3(20, 4.4f, 13), wall);
                    Box("Centre", h, new Vector3(0, 3.6f, 0), new Vector3(8, 7.2f, 10), wall);
                    Roof(h, true, new Vector3(0, 4.4f, 0), new Vector3(20.8f, 2.4f, 13.8f), roofTile, 90);
                    Roof(h, true, new Vector3(0, 7.2f, 0), new Vector3(8.6f, 2.2f, 10.6f), roofTile, 90);
                    Windows(h, glass, 20, 4.4f, 6.55f, 1, 6);
                    Box("Pool", h, new Vector3(4, -0.05f, 14), new Vector3(8, 0.15f, 4), Lit("PoolWater", new Color(0.15f, 0.55f, 0.65f), 0.95f)); break;
                case "C": // dark modern NW, shed roofs + boathouse
                    Box("BodyA", h, new Vector3(-3, 3, 0), new Vector3(12, 6, 11), wall);
                    Box("BodyB", h, new Vector3(5, 2.4f, 2), new Vector3(8, 4.8f, 9), wall);
                    Box("ShedA", h, new Vector3(-3, 6.3f, 0), new Vector3(13, 0.35f, 12.4f), roofDark, new Vector3(-6, 0, 0));
                    Box("ShedB", h, new Vector3(5, 5.0f, 2), new Vector3(9, 0.35f, 10), roofDark, new Vector3(5, 0, 0));
                    Box("GlassWall", h, new Vector3(-3, 3, 5.55f), new Vector3(10, 5, 0.12f), glass);
                    Box("Boathouse", h, new Vector3(-6, 0.9f, 24), new Vector3(6, 2.4f, 8), wall); break;
                case "D": // blue-grey craftsman, many hips
                    Box("Body", h, new Vector3(0, 3, 0), new Vector3(18, 6, 12), wall);
                    Roof(h, true, new Vector3(0, 6, 0), new Vector3(19, 3.6f, 13), roofGrey, 90);
                    Box("Bay", h, new Vector3(-4, 2.2f, 6.6f), new Vector3(5, 4.4f, 2), wall);
                    Roof(h, true, new Vector3(-4, 4.4f, 6.6f), new Vector3(5.6f, 1.6f, 2.6f), roofGrey);
                    Box("StoneBase", h, new Vector3(4, 0.6f, 6.05f), new Vector3(6, 1.2f, 0.2f), stone);
                    Windows(h, glass, 18, 6, 6.05f, 2, 5); break;
                case "E": // navy single-level craftsman, gazebo + hot tub
                    Box("Body", h, new Vector3(0, 2, 0), new Vector3(22, 4, 13), wall);
                    Roof(h, true, new Vector3(0, 4, 0), new Vector3(23, 3.2f, 14), roofGrey, 90);
                    Windows(h, glass, 22, 4, 6.55f, 1, 6);
                    Prim(PrimitiveType.Cylinder, "Gazebo", h, new Vector3(7, 1.4f, 14), new Vector3(4, 1.4f, 4), trim);
                    Roof(h, true, new Vector3(7, 2.8f, 14), new Vector3(4.6f, 1.6f, 4.6f), roofGrey); break;
                case "F": // lodge with big glass gable
                    Box("Body", h, new Vector3(0, 2.6f, 0), new Vector3(18, 5.2f, 12), wall);
                    Roof(h, false, new Vector3(0, 5.2f, 0), new Vector3(18.6f, 5.5f, 12.6f), roofGrey);
                    Box("GlassGable", h, new Vector3(0, 4.2f, 6.1f), new Vector3(7, 7, 0.15f), glass);
                    Box("Patio", h, new Vector3(0, 0.15f, 9), new Vector3(14, 0.3f, 5), stone); break;
                case "G": // Mediterranean stucco, terraced stone, palms, beach
                    Box("Body", h, new Vector3(0, 2.1f, 0), new Vector3(22, 4.2f, 13), wall);
                    Roof(h, true, new Vector3(0, 4.2f, 0), new Vector3(23, 2.2f, 14), roofTile, 90);
                    for (int i = 0; i < 3; i++) Box("Terrace" + i, h, new Vector3(0, -0.2f - i * 0.5f, 10 + i * 4), new Vector3(20, 1.2f, 0.8f), stone);
                    for (int i = 0; i < 3; i++)
                    {
                        Prim(PrimitiveType.Cylinder, "PalmTrunk" + i, h, new Vector3(-9 + i * 9, 3, 9), new Vector3(0.35f, 3, 0.35f), wood);
                        Prim(PrimitiveType.Sphere, "PalmTop" + i, h, new Vector3(-9 + i * 9, 6.2f, 9), new Vector3(3.2f, 1.2f, 3.2f), Lit("PalmLeaf", new Color(0.20f, 0.40f, 0.15f), 0.2f));
                    }
                    Windows(h, glass, 22, 4.2f, 6.55f, 1, 6); break;
            }
            if (hs.dock) DockV5(hroot, hs);
        }
    }

    static void Windows(Transform h, Material glass, float w, float hgt, float zFace, int floors, int perFloor)
    {
        for (int f = 0; f < floors; f++)
            for (int i = 0; i < perFloor; i++)
            {
                float x = -w / 2 + (i + 0.5f) * w / perFloor;
                Box($"Win{f}_{i}", h, new Vector3(x, 1.4f + f * hgt / floors, zFace), new Vector3(w / perFloor * 0.55f, hgt / floors * 0.5f, 0.1f), glass);
            }
    }

    /// <summary>8017 hero lot: 3-level glass + stacked-stone NW contemporary with terraces, stairs and a dock fire pit
    /// (massing from Scott's concept render; simplified greybox, no names or numbers).</summary>
    static void Hero(Transform h, Material glass, Material stone, Material roof, Material wood, Material concrete)
    {
        var core = Lit("HeroCharcoal", new Color(0.16f, 0.16f, 0.17f), 0.3f);
        var rail = Lit("GlassRail", new Color(0.55f, 0.65f, 0.7f), 0.95f);
        // walkout lake level, main, upper — each steps back from the lake
        Box("L0_Core", h, new Vector3(0, 1.6f, 1), new Vector3(20, 3.2f, 13), core);
        Box("L0_Glass", h, new Vector3(0, 1.6f, 7.55f), new Vector3(16, 2.8f, 0.12f), glass);
        Box("L1_Core", h, new Vector3(0, 4.8f, -0.5f), new Vector3(22, 3.2f, 12), core);
        Box("L1_Glass", h, new Vector3(0, 4.8f, 5.55f), new Vector3(18, 2.8f, 0.12f), glass);
        Box("L2_Core", h, new Vector3(1, 8.0f, -2), new Vector3(16, 3.2f, 10), core);
        Box("L2_Glass", h, new Vector3(1, 8.0f, 3.05f), new Vector3(13, 2.8f, 0.12f), glass);
        Box("Roof1", h, new Vector3(0, 6.5f, 0.5f), new Vector3(24, 0.35f, 15), roof);
        Box("Roof2", h, new Vector3(1, 9.75f, -1), new Vector3(19, 0.35f, 13.5f), roof);
        Box("StoneTowerL", h, new Vector3(-11.5f, 5, 0), new Vector3(3, 10, 6), stone);
        Box("StoneTowerR", h, new Vector3(11.5f, 4, 1), new Vector3(3, 8, 6), stone);
        Box("Rail1", h, new Vector3(0, 6.9f, 7.8f), new Vector3(20, 0.9f, 0.06f), rail);
        Box("Rail2", h, new Vector3(1, 10.1f, 4.8f), new Vector3(14, 0.9f, 0.06f), rail);
        Box("Balcony1", h, new Vector3(0, 6.45f, 6.6f), new Vector3(20, 0.25f, 2.6f), concrete);
        for (int i = 0; i < 3; i++)
        {
            Box("TerraceWall" + i, h, new Vector3(0, -0.4f - i * 0.6f, 10.5f + i * 4.5f), new Vector3(22 - i * 2, 1.3f, 0.9f), stone);
            PlankBox("TerraceBed" + i, h, new Vector3(0, 0.1f - i * 0.6f, 12.5f + i * 4.5f), new Vector3(20 - i * 2, 0.4f, 3.4f), LawnBoxMat(), 2.6f);
        }
        for (int i = 0; i < 10; i++) Box("Stair" + i, h, new Vector3(0, 0.2f - i * 0.22f, 9 + i * 1.5f), new Vector3(2.4f, 0.3f, 1.5f), stone);
    }

    static void Dock(Transform root, House hs, Material wood)
    {
        Vector3 s = new Vector3(hs.shore[0], 0, hs.shore[1]);
        Vector3 toWater = -new Vector3(hs.normal[0], 0, hs.normal[1]);
        var d = new GameObject($"Dock_{hs.lot}").transform; d.SetParent(root, false);
        d.position = s - toWater * 2f; d.rotation = Quaternion.LookRotation(toWater);
        float len = hs.lot == "8017" ? 16f : 12f;
        Box("Deck", d, new Vector3(0, 0.45f, len / 2), new Vector3(2.2f, 0.18f, len), wood);
        Box("Head", d, new Vector3(0, 0.45f, len + 1.5f), new Vector3(6f, 0.18f, 3f), wood);
        for (int i = 0; i <= 4; i++)
        {
            float z = i * (len + 2.5f) / 4;
            Prim(PrimitiveType.Cylinder, "PostL" + i, d, new Vector3(-1.05f, 0, z), new Vector3(0.18f, 0.9f, 0.18f), wood);
            Prim(PrimitiveType.Cylinder, "PostR" + i, d, new Vector3(1.05f, 0, z), new Vector3(0.18f, 0.9f, 0.18f), wood);
        }
        if (hs.lot == "8017")
        {
            Prim(PrimitiveType.Cylinder, "FirePit", d, new Vector3(0, 0.65f, len + 1.5f), new Vector3(1.1f, 0.2f, 1.1f), Lit("Stone", Color.gray));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2 + 0.4f;
                Box("Adirondack" + i, d, new Vector3(Mathf.Cos(a) * 1.9f, 0.85f, len + 1.5f + Mathf.Sin(a) * 1.2f), new Vector3(0.7f, 0.6f, 0.8f), Lit("ChairWhite", new Color(0.9f, 0.9f, 0.88f), 0.3f), new Vector3(0, -a * Mathf.Rad2Deg + 90, 0));
            }
        }
    }

    // ------------------------------------------------------------------ trees (procedural firs combined per 100 m chunk)
    struct TreeMesh { public List<Vector3> v; public List<Vector3> n; public List<int> trunk, leaf; }

    static TreeMesh FirTemplate(int layers, int sides, bool lowPoly)
    {
        var t = new TreeMesh { v = new List<Vector3>(), n = new List<Vector3>(), trunk = new List<int>(), leaf = new List<int>() };
        void Cone(float y0, float y1, float r, List<int> idx, int s)
        {
            int baseI = t.v.Count;
            for (int i = 0; i < s; i++)
            {
                float a0 = i * Mathf.PI * 2 / s, a1 = (i + 1) * Mathf.PI * 2 / s, am = (a0 + a1) / 2;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
                Vector3 tip = new Vector3(0, y1, 0);
                Vector3 nn = new Vector3(Mathf.Cos(am), r / (y1 - y0) + 0.25f, Mathf.Sin(am)).normalized;
                int k = t.v.Count;
                t.v.Add(p0); t.v.Add(tip); t.v.Add(p1);
                Vector3 n0 = new Vector3(Mathf.Cos(a0), nn.y, Mathf.Sin(a0)).normalized, n1 = new Vector3(Mathf.Cos(a1), nn.y, Mathf.Sin(a1)).normalized;
                t.n.Add(n0); t.n.Add(Vector3.up); t.n.Add(n1);
                idx.Add(k); idx.Add(k + 1); idx.Add(k + 2);
                // underside
                int u = t.v.Count;
                t.v.Add(p0); t.v.Add(p1); t.v.Add(new Vector3(0, y0 + 0.35f, 0));
                t.n.Add(Vector3.down); t.n.Add(Vector3.down); t.n.Add(Vector3.down);
                idx.Add(u); idx.Add(u + 1); idx.Add(u + 2);
            }
        }
        // trunk
        Cone(0, 3.5f, 0.28f, t.trunk, 5);
        if (lowPoly) { Cone(1.0f, 15f, 3.4f, t.leaf, 6); return t; }
        float h = 2.2f;
        for (int l = 0; l < layers; l++)
        {
            float f = (float)l / layers;
            float r = Mathf.Lerp(3.3f, 0.9f, f);
            float y0 = h + l * 2.6f;
            Cone(y0, y0 + 4.6f - f * 1.2f, r, t.leaf, sides);
        }
        return t;
    }

    /// <summary>Heightmap cells of the two turn islands: flood fill over land (above the 0 m waterline) from each island centre,
    /// capped at 90 m, grown 1 m into the shallows. Ski View's islands are closed (~2,700 m2 each, ~41 m radius).</summary>
    static bool[,] islandCell; static List<Vector3> islandLand;
    static void IslandMask(Venue v, float[,] h, int res, float size)
    {
        islandCell = null; islandLand = new List<Vector3>(); if (v.islandCentres == null || v.islandCentres.Length < 2) return;
        var m = new bool[res, res]; float cell = size / (res - 1);
        float Hw(int z, int x) => v.terrain.ymin + h[z, x] * v.terrain.hrange;
        for (int i = 0; i + 1 < v.islandCentres.Length; i += 2)
        {
            int cx = Mathf.RoundToInt((v.islandCentres[i] + size / 2) / cell), cz = Mathf.RoundToInt((v.islandCentres[i + 1] + size / 2) / cell);
            if (cx < 1 || cz < 1 || cx >= res - 1 || cz >= res - 1 || Hw(cz, cx) <= 0f) continue;
            var q = new Queue<Vector2Int>(); q.Enqueue(new Vector2Int(cx, cz)); m[cz, cx] = true;
            while (q.Count > 0)
            {
                var p = q.Dequeue(); islandLand.Add(new Vector3(-size / 2 + p.x * cell, Hw(p.y, p.x), -size / 2 + p.y * cell));
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = p + d; if (n.x < 1 || n.y < 1 || n.x >= res - 1 || n.y >= res - 1 || m[n.y, n.x] || Hw(n.y, n.x) <= 0f) continue;
                    if (new Vector2(n.x - cx, n.y - cz).magnitude * cell > 90f) continue;
                    m[n.y, n.x] = true; q.Enqueue(n);
                }
            }
        }
        var g = (bool[,])m.Clone(); int r = Mathf.CeilToInt(1f / cell);
        for (int z = r; z < res - r; z++) for (int x = r; x < res - r; x++) if (m[z, x]) for (int dz = -r; dz <= r; dz++) for (int dx = -r; dx <= r; dx++) g[z + dz, x + dx] = true;
        islandCell = g;
        Debug.Log($"[SkiView] islands: {islandLand.Count} land cells");
    }

    static void BuildTrees(Venue v, Transform root)
    {
        var troot = new GameObject("Firs").transform; troot.SetParent(root, false);
        var trunkM = Lit("FirBark", new Color(0.24f, 0.17f, 0.12f), 0.1f);
        var leafM = Lit("FirNeedles", new Color(0.07f, 0.17f, 0.08f), 0.12f);
        var hi = FirTemplate(5, 8, false); var lo = FirTemplate(1, 6, true);
        var chunks = new Dictionary<Vector2Int, (List<Vector3> v, List<Vector3> n, List<int> a, List<int> b)>();
        var rnd = new System.Random(8017);
        int count = v.treesFlat.Length / 4;
        for (int i = 0; i < count; i++)
        {
            float x = v.treesFlat[i * 4], y = v.treesFlat[i * 4 + 1], z = v.treesFlat[i * 4 + 2], s = v.treesFlat[i * 4 + 3];
            bool near = Mathf.Abs(x) < 420 && z > -650 && z < 600;
            var tm = near ? hi : lo;
            var key = new Vector2Int(Mathf.FloorToInt(x / 120f), Mathf.FloorToInt(z / 120f));
            if (!chunks.TryGetValue(key, out var ch)) { ch = (new List<Vector3>(), new List<Vector3>(), new List<int>(), new List<int>()); chunks[key] = ch; }
            float hs = s * (1.4f + (float)rnd.NextDouble() * 0.9f);   // Douglas firs ~ 20-40 m
            Quaternion q = Quaternion.Euler((float)rnd.NextDouble() * 4 - 2, (float)rnd.NextDouble() * 360, (float)rnd.NextDouble() * 4 - 2);
            Vector3 sc = new Vector3(s, hs, s);
            int b0 = ch.v.Count;
            for (int k = 0; k < tm.v.Count; k++)
            {
                ch.v.Add(new Vector3(x, y - 0.3f, z) + q * Vector3.Scale(tm.v[k], sc));
                ch.n.Add(q * tm.n[k]);
            }
            foreach (var id in tm.trunk) ch.a.Add(b0 + id);
            foreach (var id in tm.leaf) ch.b.Add(b0 + id);
        }
        int ci = 0;
        foreach (var kv in chunks)
        {
            var m = new Mesh { name = $"firs_{kv.Key.x}_{kv.Key.y}" };
            if (kv.Value.v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(kv.Value.v); m.SetNormals(kv.Value.n);
            m.subMeshCount = 2; m.SetTriangles(kv.Value.a, 0); m.SetTriangles(kv.Value.b, 1);
            m.RecalculateBounds();
            SaveMesh(m, m.name);
            var g = MeshObj(m.name, troot, m, trunkM, leafM);
            g.isStatic = true;
            ci++;
        }
        Debug.Log($"[SkiView] trees {count} in {ci} chunks");
    }

    // ------------------------------------------------------------------ boat (unbranded blue/white tournament-style hull)
    // ------------------------------------------------------------------ boat: Christopher's SkiBoat.fbx (procedural fallback below)
    const string BOAT = "Assets/SkiView/Boat/";
    [Serializable] class PaletteFile { public PaletteLakes lakes; }
    [Serializable] class PaletteLakes { public PaletteEntry skiview; }
    [Serializable] class PaletteEntry { public string deck, band, accent, livery; public float accent_metallic; }

    /// Moves the chrome triangles of the pylon (post, swivel cap, tow ball, brace; everything within ~9 cm of the pylon axis
    /// from just under the floor to the ball) into an extra submesh with <paramref name="dark"/>. Mesh copies are saved under Generated/Meshes.
    static int SplitPylon(Transform boat, Vector3 towLocal, Material chrome, Material dark)
    {
        int total = 0;
        foreach (var mf in boat.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = mf.GetComponent<MeshRenderer>(); var src = mf.sharedMesh;
            if (r == null || src == null) continue;
            var sm = r.sharedMaterials; int ci = Array.IndexOf(sm, chrome);
            if (ci < 0 || ci >= src.subMeshCount) continue;
            var verts = src.vertices; var tris = src.GetTriangles(ci);
            var keep = new List<int>(); var pyl = new List<int>();
            for (int k = 0; k < tris.Length; k += 3)
            {
                bool inP = true;
                for (int j = 0; j < 3 && inP; j++)
                {
                    var w = boat.InverseTransformPoint(mf.transform.TransformPoint(verts[tris[k + j]]));
                    inP = Mathf.Abs(w.x - towLocal.x) < 0.09f && Mathf.Abs(w.z - towLocal.z) < 0.14f && w.y > -0.15f && w.y < towLocal.y + 0.06f;
                }
                (inP ? pyl : keep).AddRange(new[] { tris[k], tris[k + 1], tris[k + 2] });
            }
            if (pyl.Count == 0) continue;
            var m = UnityEngine.Object.Instantiate(src); m.name = src.name + "_pylonsplit";
            m.subMeshCount = src.subMeshCount + 1;
            m.SetTriangles(keep, ci); m.SetTriangles(pyl, src.subMeshCount);
            mf.sharedMesh = SaveMesh(m, "boat_" + src.name + "_pylonsplit");
            var nm = new List<Material>(sm); while (nm.Count < src.subMeshCount) nm.Add(null); nm.Add(dark);
            r.sharedMaterials = nm.ToArray();
            total += pyl.Count / 3;
        }
        return total;
    }

    static Material BoatMat(string slot, string tex, Color baseCol, bool normal = true, bool metal = true)
    {
        string mp = MAT + "Boat/" + slot + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mp); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BOAT + "textures/T_" + tex + "_BaseColor.png"));
        m.SetColor("_BaseColor", baseCol);
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(BOAT + "textures/T_" + (normal ? tex : "Flat") + "_Normal.png");
        if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); } else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        var ms = metal ? AssetDatabase.LoadAssetAtPath<Texture2D>(BOAT + "textures/T_" + tex + "_MetallicSmoothness.png") : null;
        if (ms != null) { m.SetTexture("_MetallicGlossMap", ms); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_SmoothnessTextureChannel", 0f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 1f); }
        else { m.SetTexture("_MetallicGlossMap", null); m.DisableKeyword("_METALLICSPECGLOSSMAP"); }
        EditorUtility.SetDirty(m); return m;
    }

    static Color Hex(string h, Color fallback) => ColorUtility.TryParseHtmlString(h, out var c) ? c : fallback;

    static GameObject BuildBoat(Transform root)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(BOAT + "SkiBoat.fbx");
        if (src == null) { Debug.LogWarning("[SkiView] SkiBoat.fbx missing, procedural boat"); return BuildBoatProcedural(root); }
        Directory.CreateDirectory(MAT + "Boat");
        var boat = (GameObject)UnityEngine.Object.Instantiate(src, root, false);
        boat.name = "Boat_SWLM_SkiView";
        boat.transform.localPosition = Vector3.zero; boat.transform.localRotation = Quaternion.identity; boat.transform.localScale = Vector3.one;
        // URP Lit materials per slot (the FBX importer only carries the base map and tint)
        var glass = BoatMat("M_Glass", "Glass", Color.white, false);
        glass.SetFloat("_Surface", 1f); glass.SetFloat("_Blend", 0f); glass.SetFloat("_ZWrite", 0f);
        glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.SetOverrideTag("RenderType", "Transparent"); glass.renderQueue = (int)RenderQueue.Transparent;
        var livery = BoatMat("M_Livery", "Livery", Color.white, false, false);
        livery.SetFloat("_AlphaClip", 1f); livery.SetFloat("_Cutoff", 0.4f); livery.EnableKeyword("_ALPHATEST_ON"); livery.renderQueue = (int)RenderQueue.AlphaTest;
        var mats = new Dictionary<string, Material> {
            { "M_Gelcoat_Deck", BoatMat("M_Gelcoat_Deck", "Gelcoat", Color.white) }, { "M_Gelcoat_Band", BoatMat("M_Gelcoat_Band", "Gelcoat", Color.white) },
            { "M_Gelcoat_Accent", BoatMat("M_Gelcoat_Accent", "Gelcoat", Color.white) }, { "M_Liner", BoatMat("M_Liner", "Gelcoat", Hex("#C3C8CC", Color.gray)) },
            { "M_Carpet", BoatMat("M_Carpet", "Carpet", Color.white) }, { "M_Vinyl", BoatMat("M_Vinyl", "Vinyl", Hex("#F0F0EE", Color.white)) },
            { "M_Dash", BoatMat("M_Dash", "Dash", Color.white) }, { "M_Chrome", BoatMat("M_Chrome", "Chrome", Color.white, false) },
            { "M_Glass", glass }, { "M_Trim", BoatMat("M_Trim", "Trim", Color.white) }, { "M_Teak", BoatMat("M_Teak", "Teak", Color.white) }, { "M_Livery", livery },
            // r4: driver figure slots (no dedicated maps yet — solid URP Lit so they are not pink)
            { "M_Driver_Clothes", BoatMat("M_Driver_Clothes", "Trim", Hex("#1E2A44", Color.blue), false, false) },
            { "M_Driver_Skin", BoatMat("M_Driver_Skin", "Trim", Hex("#C48A6A", Color.white), false, false) } };
        int swapped = 0;
        foreach (var r in boat.GetComponentsInChildren<MeshRenderer>(true))
        {
            var sm = r.sharedMaterials;
            for (int i = 0; i < sm.Length; i++)
                if (sm[i] != null) foreach (var kv in mats) if (sm[i].name.StartsWith(kv.Key)) { sm[i] = kv.Value; swapped++; break; }
            r.sharedMaterials = sm;
            r.shadowCastingMode = r.name.EndsWith("_LOD0") ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
        // pass 10: the slalom pylon reads as a slim dark post in broadcast footage (anodised, it mirrors the dark boat
        // interior), but it shares M_Chrome with the rails, so its triangles move to their own dark submesh
        var pylonMat = BoatMat("M_PylonAnodized", "Trim", Hex("#2B2E33", Color.gray), false, false);
        pylonMat.SetFloat("_Metallic", 0.7f); pylonMat.SetFloat("_Smoothness", 0.55f); EditorUtility.SetDirty(pylonMat);
        var towT = FindDeep(boat.transform, "TowPoint");
        int pylonTris = towT != null ? SplitPylon(boat.transform, boat.transform.InverseTransformPoint(towT.position), mats["M_Chrome"], pylonMat) : 0;
        // LOD group (README: 35 % / 8 %, then cull)
        var lod0 = FindDeep(boat.transform, "SkiBoat_LOD0"); var lod1 = FindDeep(boat.transform, "SkiBoat_LOD1");
        var lg = boat.GetComponent<LODGroup>(); if (lg == null) lg = boat.AddComponent<LODGroup>();
        if (lod0 != null && lod1 != null)
        {
            lg.SetLODs(new[] { new LOD(0.35f, lod0.GetComponentsInChildren<Renderer>()), new LOD(0.08f, lod1.GetComponentsInChildren<Renderer>()) });
            lg.RecalculateBounds();
        }
        // per-lake gelcoat tint (boat_palettes.json -> skiview), applied with his MaterialPropertyBlock snippet
        var pal = boat.AddComponent<BoatPalette>();
        var pj = File.Exists(BOAT + "boat_palettes.json") ? JsonUtility.FromJson<PaletteFile>(File.ReadAllText(BOAT + "boat_palettes.json")) : null;
        if (pj?.lakes?.skiview != null)
        {
            var e = pj.lakes.skiview; pal.lake = "skiview";
            pal.deck = Hex(e.deck, pal.deck); pal.band = Hex(e.band, pal.band); pal.accent = Hex(e.accent, pal.accent); pal.livery = Hex(e.livery, pal.livery); pal.accentMetallic = e.accent_metallic;
        }
        pal.Apply();
        Debug.Log($"[SkiView] boat: SkiBoat.fbx, {swapped} material slots, LOD0={(lod0 != null)} LOD1={(lod1 != null)}, palette band={ColorUtility.ToHtmlStringRGB(pal.band)} accent={ColorUtility.ToHtmlStringRGB(pal.accent)} deck={ColorUtility.ToHtmlStringRGB(pal.deck)}, tow={FindDeep(boat.transform, "TowPoint")?.localPosition}, dark pylon tris={pylonTris}");
        return boat;
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var f = FindDeep(c, n); if (f != null) return f; }
        return null;
    }

    static GameObject BuildBoatProcedural(Transform root)
    {
        var boat = new GameObject("Boat_SWLM_SkiView"); boat.transform.SetParent(root, false);
        var white = Lit("HullWhite", new Color(0.93f, 0.94f, 0.95f), 0.85f);
        var blue = Lit("HullBlue", new Color(0.05f, 0.22f, 0.62f), 0.85f, 0.1f);
        var vinyl = Lit("SeatVinyl", new Color(0.85f, 0.86f, 0.88f), 0.5f);
        var dark = Lit("CockpitFloor", new Color(0.18f, 0.2f, 0.22f), 0.2f);
        var chrome = Lit("Chrome", new Color(0.8f, 0.82f, 0.85f), 0.9f, 1f);
        var glassM = Lit("Windshield", new Color(0.12f, 0.16f, 0.2f), 0.95f);
        // lofted hull: stations from stern (z=-3.3) to bow (z=+3.4); section points deck edge, band top, band bottom, chine, keel
        var v = new List<Vector3>(); var tw = new List<int>(); var tb = new List<int>();
        int S = 16; float zs = -3.3f, ze = 3.4f;
        var secs = new List<Vector3[]>();
        for (int i = 0; i <= S; i++)
        {
            float t = (float)i / S, z = Mathf.Lerp(zs, ze, t);
            float bow = Mathf.Clamp01((z - 0.6f) / (ze - 0.6f));
            float b = 1.16f * Mathf.Sqrt(Mathf.Max(0.0001f, 1 - bow * bow)) ;
            float deck = 0.80f + bow * bow * 0.25f, keel = -0.18f + bow * 0.55f;
            secs.Add(new[] { new Vector3(b, deck, z), new Vector3(b * 1.0f, 0.58f + bow * 0.2f, z), new Vector3(b * 0.98f, 0.22f + bow * 0.3f, z), new Vector3(b * 0.9f, 0.02f + bow * 0.4f, z), new Vector3(0, keel, z) });
        }
        void Q(List<int> L, Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int k = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); L.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 }); }
        Vector3 M(Vector3 p) => new Vector3(-p.x, p.y, p.z);
        for (int i = 0; i < S; i++)
        {
            var A = secs[i]; var B = secs[i + 1];
            for (int j = 0; j < 4; j++)
            {
                var L = j == 1 ? tb : tw;
                Q(L, A[j], B[j], B[j + 1], A[j + 1]);                 // starboard (+x)
                Q(L, M(A[j + 1]), M(B[j + 1]), M(B[j]), M(A[j]));     // port
            }
            Q(tw, M(A[0]), M(B[0]), B[0], A[0]);                       // deck
        }
        // transom
        var T = secs[0];
        for (int j = 0; j < 4; j++) Q(j == 1 ? tb : tw, M(T[j]), T[j], T[j + 1], M(T[j + 1]));
        var hm = new Mesh { name = "hull" }; hm.SetVertices(v); hm.subMeshCount = 2; hm.SetTriangles(tw, 0); hm.SetTriangles(tb, 1);
        hm.RecalculateNormals(); hm.RecalculateBounds(); SaveMesh(hm, "boat_hull");
        MeshObj("Hull", boat.transform, hm, white, blue);
        Box("Cockpit", boat.transform, new Vector3(0, 0.78f, -0.6f), new Vector3(1.9f, 0.12f, 3.6f), dark);
        Box("SeatRear", boat.transform, new Vector3(0, 0.98f, -2.5f), new Vector3(2.0f, 0.35f, 0.6f), vinyl);
        Box("SeatSideL", boat.transform, new Vector3(-0.85f, 0.95f, -0.9f), new Vector3(0.4f, 0.3f, 2.2f), vinyl);
        Box("SeatSideR", boat.transform, new Vector3(0.85f, 0.95f, -0.9f), new Vector3(0.4f, 0.3f, 2.2f), vinyl);
        Box("Console", boat.transform, new Vector3(0.55f, 1.05f, 1.0f), new Vector3(0.7f, 0.5f, 0.6f), white);
        Box("Windshield", boat.transform, new Vector3(0, 1.22f, 1.35f), new Vector3(2.1f, 0.42f, 0.06f), glassM, new Vector3(-35, 0, 0));
        Box("DriverSeat", boat.transform, new Vector3(0.55f, 1.05f, 0.2f), new Vector3(0.6f, 0.5f, 0.6f), vinyl);
        // pylon (tow point 0.55 m aft of origin, 1.46 m above the water)
        Prim(PrimitiveType.Cylinder, "Pylon", boat.transform, new Vector3(0, 1.12f, -0.55f), new Vector3(0.09f, 0.34f, 0.09f), chrome);
        Prim(PrimitiveType.Sphere, "PylonBall", boat.transform, new Vector3(0, 1.46f, -0.55f), Vector3.one * 0.14f, chrome);
        // livery on both sides of the blue band: "SWLM" + slanted "Ski View" script + swoosh (same art as the 1.2.0
        // three.js boat, Tools/gen_livery.py, Barlow Condensed OFL). Quads sit on the straight part of the band.
        const string LIV = GEN + "livery_swlm_skiview.png";
        var livImp = AssetImporter.GetAtPath(LIV) as TextureImporter;
        if (livImp != null) { livImp.sRGBTexture = true; livImp.mipmapEnabled = true; livImp.anisoLevel = 8; livImp.wrapMode = TextureWrapMode.Clamp; livImp.maxTextureSize = 2048; livImp.textureCompression = TextureImporterCompression.CompressedHQ; livImp.SaveAndReimport(); }
        var livTex = AssetDatabase.LoadAssetAtPath<Texture2D>(LIV);
        var livM = Lit("HullLivery", Color.white, 0.85f, 0.1f);
        if (livTex != null) livM.SetTexture("_BaseMap", livTex); else Debug.LogWarning("[SkiView] livery texture missing");
        EditorUtility.SetDirty(livM);
        float bandY0 = 0.24f, bandY1 = 0.56f, zB = 0.55f;      // band 0.32 m tall; 2048x256 art -> 2.56 m long at 8:1
        float zA = zB - (bandY1 - bandY0) * 8f;
        foreach (int side in new[] { -1, 1 })
        {
            // text reads left to right from either side: stern->bow on starboard, bow->stern on port (never mirrored)
            float x = side * 1.165f;
            var lv = new List<Vector3>(); var uv = new List<Vector2>();
            lv.Add(new Vector3(x, bandY0, zA)); lv.Add(new Vector3(x, bandY1, zA)); lv.Add(new Vector3(x, bandY1, zB)); lv.Add(new Vector3(x, bandY0, zB));
            if (side > 0) { uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0)); }
            else { uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(0, 0)); }
            var lm = new Mesh { name = side > 0 ? "livery_stbd" : "livery_port" };
            lm.SetVertices(lv); lm.SetUVs(0, uv);
            lm.SetTriangles(side > 0 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 }, 0);
            lm.RecalculateNormals(); lm.RecalculateBounds(); SaveMesh(lm, lm.name);
            var lg = MeshObj(side > 0 ? "Livery_Stbd" : "Livery_Port", boat.transform, lm, livM);
            lg.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        return boat;
    }

    // ------------------------------------------------------------------ skier (Wade Cox): Mixamo Remy rig, capsule fallback
    const string MIXAMO = "Assets/SkiView/ThirdParty/Mixamo/";
    static AnimationClip MixClip(string name)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MIXAMO + "Remy@" + name + ".fbx"))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        Debug.LogWarning("[SkiView] mixamo clip missing: " + name); return null;
    }

    /// <summary>Pass 14: soft spray materials on SkiView/SprayParticle (forward-scattering, soft-particle, water-faded).
    /// kind: "drop" (default; droplets / streaks), "sheet" (torn translucent sheets), "mist" (fine mist puffs), "foam" (flat foam on the water).</summary>
    static Material SprayMaterial(string kind = "drop")
    {
        string tex = kind == "mist" || kind == "foam" ? "spray_mist" : kind == "sheet" ? "spray_sheet" : "spray_drop";
        string tp = "Assets/SkiView/Generated/" + tex + ".png", mp = "Assets/SkiView/Materials/Spray_" + kind + ".mat";
        int maxSize = tex == "spray_drop" ? 64 : 128;
        if (AssetImporter.GetAtPath(tp) is TextureImporter ti && (!ti.alphaIsTransparency || ti.maxTextureSize != maxSize || ti.wrapMode != TextureWrapMode.Clamp))
        { ti.alphaIsTransparency = true; ti.maxTextureSize = maxSize; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport(); }
        var sh = Shader.Find("SkiView/SprayParticle");
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, mp); }
        m.shader = sh;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
        // drops: bright glints; sheets: translucent, strongly backlit; mist: dim, wide, glows against the sun; foam: flat, matte
        float amb = 1.0f, sun = 0.45f, sc = 2.2f, soft = 0.35f, am = 1f, wf = 0.12f;
        if (kind == "drop") { sun = 0.6f; sc = 2.6f; soft = 0.15f; wf = 0.05f; }
        if (kind == "sheet") { sun = 0.4f; sc = 3.0f; soft = 0.35f; am = 0.6f; wf = 0.15f; }
        if (kind == "mist") { amb = 0.85f; sun = 0.3f; sc = 3.5f; soft = 0.9f; am = 0.35f; wf = 0.25f; }
        if (kind == "foam") { amb = 1.05f; sun = 0.5f; sc = 0.3f; soft = 0.05f; am = 0.9f; wf = -1f; }
        m.SetFloat("_Ambient", amb); m.SetFloat("_Sun", sun); m.SetFloat("_Scatter", sc); m.SetFloat("_SoftDist", soft); m.SetFloat("_AlphaMul", am);
        m.SetFloat("_WaterFade", wf < 0f ? 1e-4f : wf);
        m.SetColor("_Tint", new Color(0.93f, 0.96f, 1f, 1f));
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m); return m;
    }

    /// <summary>Christopher's slalom ski V2 (Oct 23, Karthik note): the bright 1.70 m FBX replaces the kit ski mesh. Its root is the ankle
    /// midpoint (deck top y=0, tip +Z), so it sits at the kit's ankle midpoint; the kit FrontBoot/RearBoot stay as the IK targets and the
    /// spray comes from the FBX SprayEmitter (tail, 0.645 m behind the root). Falls back to the kit ski if the FBX is missing.</summary>
    /// <summary>b10.6: Holly V3 toe-plate default on lake build. Runtime ApplySkiTo swaps DoubleBoot + colourways.</summary>
    static void UseSlalomSkiV3(Transform skiT, SkiSpray spray)
    {
        SkiViewSkierSetup.SlalomSkiV3();
        const string F = "Assets/SkiView/Skier/SkiV3/SkiV3_ToePlate.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(F); if (prefab == null) { Debug.LogWarning("[SkiView] ski V3 missing, kit ski kept"); return; }
        var v3 = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).transform; PrefabUtility.UnpackPrefabInstance(v3.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        v3.name = "SlalomSki_V3_ToePlate"; v3.SetParent(skiT, false);
        float mid = (SkiViewRiderKit.FRONT_ANKLE + SkiViewRiderKit.REAR_ANKLE) * 0.5f;
        v3.localPosition = new Vector3(0, 0, mid); v3.localRotation = Quaternion.identity;
        foreach (var r in v3.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        foreach (var nm in new[] { "SkiBody", "Fin", "FinWing", "FinBlock" }) { var t = skiT.Find(nm); if (t != null) t.gameObject.SetActive(false); }
        foreach (var nm in new[] { "FrontBoot", "RearBoot" }) { var t = skiT.Find(nm); if (t != null) foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false; }
        var em = FindDeep(v3, "SprayEmitter");
        if (em != null && spray != null) { float z = skiT.InverseTransformPoint(em.position).z; spray.tailZ = z; }
        Debug.Log($"[SkiView] ski V3 toe-plate in: sprayTailZ={(spray != null ? spray.tailZ : -1):0.000}");
    }

    static AnimationClip SkierClip(string file)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/SkiView/Skier/" + file + ".fbx")) if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
        return null;
    }
    static GameObject BuildSkier(Transform root, out Transform body, out Transform ski, out Transform handle)
    {
        var s = new GameObject("Skier_WadeCox"); s.transform.SetParent(root, false);
        // 67" slalom ski + double high-wrap boots (SkiViewRiderKit, Docs/SLALOM-SKIER-LOOK.md); ankle midpoint at the skier origin
        var skiT = SkiViewRiderKit.BuildSki(s.transform);
        skiT.localPosition = new Vector3(0, 0, -(SkiViewRiderKit.FRONT_ANKLE + SkiViewRiderKit.REAR_ANKLE) * 0.5f);
        ski = skiT;
        var spray = SkiSpray.Create(skiT, SprayMaterial(), SprayMaterial("sheet"), SprayMaterial("mist"), SprayMaterial("foam"));
        UseSlalomSkiV3(skiT, spray);
        handle = Prim(PrimitiveType.Cylinder, "Handle", root, Vector3.zero, new Vector3(0.028f, 0.165f, 0.028f), Lit("HandleGrip", new Color(0.1f, 0.1f, 0.1f), 0.3f)).transform;

        // Remy's textures are embedded in the FBX: extract once so the URP materials pick them up
        if (AssetImporter.GetAtPath(MIXAMO + "Remy.fbx") is ModelImporter rmi && !Directory.Exists(MIXAMO + "Textures"))
        {
            Directory.CreateDirectory(MIXAMO + "Textures");
            rmi.ExtractTextures(MIXAMO + "Textures");
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(MIXAMO + "Remy.fbx", ImportAssetOptions.ForceUpdate);
        }
        if (Directory.Exists(MIXAMO + "Textures"))
            foreach (var f in Directory.GetFiles(MIXAMO + "Textures", "*.png"))
                if (AssetImporter.GetAtPath(f.Replace('\\', '/')) is TextureImporter ti && ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; ti.textureCompression = TextureImporterCompression.Compressed; if (f.Contains("_Normal")) ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }   // same caps as SkiViewMixamoImport
        // b10.8: Wade V3 FINAL (INTERNAL ONLY) when likeness flag is on; same mixamorig humanoid as Remy so clips retarget.
        // Photo BaseColor/Normal/AO atlas; open vest + bare arms baked in the mesh (no Remy Reskin / procedural CompVest).
        bool useWade = SkiViewBranding.Flag.wadeLikenessEnabled;
        // Desktop V3 (4k BaseColor/Normal/AO) for photo face; Phone FBX stays imported for a later mobile LOD pass.
        string wadeFbx = "Assets/SkiView/Skier/INTERNAL_ONLY_Felderpuss_V3_WadeCox.fbx";
        string riderFbx = useWade ? wadeFbx : (MIXAMO + "Remy.fbx");
        var remy = AssetDatabase.LoadAssetAtPath<GameObject>(riderFbx);
        Avatar avatar = null;
        if (remy != null) foreach (var o in AssetDatabase.LoadAllAssetsAtPath(riderFbx)) if (o is Avatar av) avatar = av;
        if (remy != null && avatar != null && avatar.isHuman)
        {
            var rider = (GameObject)PrefabUtility.InstantiatePrefab(remy, s.transform);
            rider.name = useWade ? "Rider_WadeCox_V3" : "Rider_Remy";
            rider.transform.localPosition = Vector3.zero; rider.transform.localRotation = Quaternion.identity;
            var rends = rider.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var bb = rends[0].bounds; foreach (var r in rends) bb.Encapsulate(r.bounds);
                float hgt = bb.size.y; if (hgt > 0.5f && (hgt < 1.6f || hgt > 2.0f)) rider.transform.localScale *= 1.80f / hgt;
                foreach (var r in rends)
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = true; smr.forceMatrixRecalculationPerRender = true; }
                    // hide leftover eyelash / shoe helpers if present; V3 vest is mesh-baked (open, bare arms)
                    if (r.name == "Eyes" || r.name == "Eyelashes" || r.name.Contains("Eyelash") || r.name.Contains("Shoes")) r.enabled = false;
                }
            }
            var anim = rider.GetComponent<Animator>(); if (anim == null) anim = rider.AddComponent<Animator>();
            anim.avatar = avatar; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; anim.runtimeAnimatorController = null;
            if (!useWade) SkiViewRiderKit.Reskin(rider);
            // ankle height above the sole, from the bind pose (feet on y = 0, sneaker soles ~2.5 cm)
            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            float ankle = lf != null ? Mathf.Clamp(s.transform.InverseTransformPoint(lf.position).y - 0.025f, 0.05f, 0.12f) : 0.075f;
            if (!useWade) SkiViewRiderKit.BuildVest(rider, anim, s.transform);
            var rig = rider.AddComponent<SkierRig>();
            rig.animator = anim; rig.ankleAboveSole = ankle;
            rig.frontBoot = skiT.Find("FrontBoot"); rig.rearBoot = skiT.Find("RearBoot");
            // b10.8 Scott: ~58 deg lean sideways (not forward)
            if (useWade) { rig.leanTotalApexDeg = 58f; rig.stretchLeanDeg = 58f; rig.leanFwdShare = 0.08f; }
            // b10.10: Scott-cleared V3 leans (Christopher) on the live V3 body; V2 names kept for Remy path.
            // Lean58 FBX is imported under Skier/ but SkierRig has only leanL/leanR slots — 58° apex stays procedural (leanTotalApexDeg).
            string stanceClip = useWade ? "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance" : "WadeCox_V2@SlalomStance";
            string leanLClip = useWade ? "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance_LeanLeft" : "WadeCox_V2@SlalomStance_LeanLeft";
            string leanRClip = useWade ? "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance_LeanRight" : "WadeCox_V2@SlalomStance_LeanRight";
            rig.idle = MixClip("Breathing Idle"); rig.pull = SkierClip(stanceClip) ?? MixClip("Pull Heavy Object") ?? MixClip("Pulling A Rope");
            rig.edge = MixClip("Edge Slip"); rig.leanL = SkierClip(leanLClip); rig.leanR = SkierClip(leanRClip); rig.fallen = MixClip("Treading Water");
            rig.start = MixClip("Pull Heavy Object Start"); rig.fall = MixClip("Falling Forward Death"); rig.standUp = MixClip("Stand Up");
            body = rider.transform;
            Debug.Log($"[SkiView] rigged skier: {(useWade ? "Wade V3 INTERNAL_ONLY" : "Remy")} humanoid ({Path.GetFileName(riderFbx)}), ankle {ankle:0.000} m, clips idle={rig.idle != null} pull={(rig.pull != null ? rig.pull.name : "-")} fallen={rig.fallen != null} start={(rig.start != null ? rig.start.length : 0f):0.00}s fall={(rig.fall != null ? rig.fall.length : 0f):0.00}s standUp={rig.standUp != null}");
            return s;
        }
        Debug.LogWarning("[SkiView] Mixamo Remy not imported (or not Humanoid); using the capsule skier");
        var suit = Lit("Wetsuit", new Color(0.05f, 0.05f, 0.06f), 0.35f);
        var vestC = Lit("VestNavy", new Color(0.06f, 0.12f, 0.35f), 0.3f);
        var skin = Lit("Skin", new Color(0.78f, 0.58f, 0.45f), 0.3f);
        body = Prim(PrimitiveType.Capsule, "Body", s.transform, new Vector3(0, 0.95f, 0), new Vector3(0.36f, 0.78f, 0.3f), suit).transform;
        Prim(PrimitiveType.Capsule, "Vest", s.transform, new Vector3(0, 1.22f, 0), new Vector3(0.42f, 0.32f, 0.36f), vestC);
        Prim(PrimitiveType.Sphere, "Head", s.transform, new Vector3(0, 1.80f, 0.02f), new Vector3(0.22f, 0.25f, 0.24f), skin);
        Prim(PrimitiveType.Capsule, "Arms", s.transform, new Vector3(0, 1.25f, 0.25f), new Vector3(0.12f, 0.32f, 0.12f), suit).transform.localEulerAngles = new Vector3(70, 0, 0);
        return s;
    }
}
