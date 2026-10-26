using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Pass 15: the other lakes. Each lake is its own scene (Assets/SkiView/Scenes/SkiViewLake_&lt;id&gt;.unity) built from
/// Tools/gen_venue_lake.py output in Assets/SkiView/Lakes/&lt;id&gt;/ (terrain, shore, trees, buildings, course, boat loop,
/// sky/light mood, boat palette). The Ski View home lake scene is built first (unchanged) and reused for the shared
/// boat / skier / camera / FX rigs; everything lake-specific is deleted and rebuilt, and every asset the lake writes goes into
/// its own folder so the home lake's assets are never replaced.</summary>
public static partial class SkiViewBuilder
{
    public static readonly string[] LAKES = { "cedarbend", "saltwhite", "villaserena", "tallpine", "heronmarsh", "saguaro", "harrowpond", "gulfwind", "marrowind" };
    public static readonly Dictionary<string, string> LAKE_NAMES = new Dictionary<string, string> { { "skiview", "Ski View" }, { "cedarbend", "Cedar Bend" }, { "saltwhite", "Saltwhite" },
        { "villaserena", "Villa Serena" }, { "tallpine", "Tall Pine" }, { "heronmarsh", "Heron Marsh" },
        { "saguaro", "Saguaro Flats" }, { "harrowpond", "Harrow Pond" }, { "gulfwind", "Gulfwind" }, { "marrowind", "Marrowind" } };   // pass 19
    public static string LakeScenePath(string id) => $"Assets/SkiView/Scenes/SkiViewLake_{id}.unity";
    public static string LakeDir(string id) => $"Assets/SkiView/Lakes/{id}/";
    static bool LakeReady(string id) => File.Exists(LakeDir(id) + "venue.json");

    [Serializable] class LakeBoat { public string deck, band, accent, livery, decal; public float accentMetallic; }
    [Serializable] class LakeWaterC { public float[] shallow, deep, refl; }
    [Serializable] class LakeMood
    {
        public string hdri; public float sunEl, sunAz, sunIntensity; public float[] sunColor; public float shadowStrength, skyExposure;
        public float[] ambientSky, ambientEquator, ambientGround, fogColor; public float fogDensity, postExposure, contrast, saturation, temperature, tint;
        public float[] foliageRim, skyTint;   // lake P1: skyTint = skybox _Tint rgb (0.5 = neutral); null = neutral   // lake P1: warm fresnel rim on sun-facing foliage (r, g, b, strength); null = off
    }
    [Serializable] class LakeInfo { public string id, name, style, trees; public bool hasRamp; public LakeBoat boat; public LakeWaterC water; public LakeMood mood; }
    [Serializable] class LakeBuilding { public string kind; public float x, z, y, yaw, w, d, h; public float[] wall, roof; public string kit, piece; public bool mirror, shore; }   // pass 19: kit pieces
    [Serializable] class LakeVenue
    {
        public LakeInfo lake; public TerrainInfo terrain; public CourseData course; public PathData path;
        public float[] lakeBounds, islandCentres, islandPolys, shore, riprap, treesFlat; public int[] islandCounts; public LakeBuilding[] buildings;
        public RampData ramp; public float rampFromSlalom, passWindowLate; public int jumpDir; public float[] aerialCentre;   // pass 17
        public bool jumpOnly;   // pass 18: jump-mode-only lake (no slalom course)
        public bool rampJumpModeOnly;   // T26: ramp only in jump mode
        public bool farWater; public float waterCell; public float[] farSil, farHouses, playBox;   // pass 19: lake bigger than its terrain, play box
    }

    static string LD;   // current lake folder
    static bool LakeIs(string id) => LD != null && LD.TrimEnd('/').EndsWith("/" + id);   // review #3: per-lake sun tuning
    static Color C3(float[] a, float k = 1f) => a == null || a.Length < 3 ? Color.white : new Color(a[0] * k, a[1] * k, a[2] * k);

    /// <summary>Scene names for the in-game LAKE button (home lake first, then every lake whose data exists).</summary>
    public static string[] LakeSceneNames() => new[] { Path.GetFileNameWithoutExtension(SCENE) }.Concat(LAKES.Where(LakeReady).Select(i => Path.GetFileNameWithoutExtension(LakeScenePath(i)))).ToArray();
    public static string[] LakeDisplayNames() => new[] { "Ski View" }.Concat(LAKES.Where(LakeReady).Select(i => LAKE_NAMES.TryGetValue(i, out var n) ? n : i)).ToArray();
    /// <summary>All lake scenes (home first). Pass 16: these are Addressables scenes, not player scenes.</summary>
    public static string[] LakeScenePaths() => new[] { SCENE }.Concat(LAKES.Where(i => File.Exists(LakeScenePath(i))).Select(LakeScenePath)).ToArray();
    /// <summary>Player build scenes. Pass 16: only the boot scene; every lake loads through Addressables (SkiViewContent).</summary>
    public static string[] BuildScenes() => new[] { SkiViewContent.BOOT };

    public static void BuildLakes()
    {
        foreach (var id in LAKES) if (LakeReady(id)) BuildLake(id);
        // pass 16: boot scene in the player; lake scenes listed disabled (they ship as Addressables content)
        SkiViewContent.EnsureBootScene();
        EditorBuildSettings.scenes = BuildScenes().Select(p => new EditorBuildSettingsScene(p, true)).Concat(LakeScenePaths().Select(p => new EditorBuildSettingsScene(p, false))).ToArray();
        SkiViewContent.Setup();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);   // captures expect the home lake open
    }

    /// <summary>T26: rebuild only the lakes named in SV_LAKES (comma list), then the same build-settings / content tail as BuildLakes.</summary>
    public static void BuildLakesEnv()
    {
        var ids = (Environment.GetEnvironmentVariable("SV_LAKES") ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        foreach (var id in ids) if (LakeReady(id)) BuildLake(id); else Debug.LogWarning("[SkiView] BuildLakesEnv: no lake " + id);
        SkiViewContent.EnsureBootScene();
        EditorBuildSettings.scenes = BuildScenes().Select(p => new EditorBuildSettingsScene(p, true)).Concat(LakeScenePaths().Select(p => new EditorBuildSettingsScene(p, false))).ToArray();
        SkiViewContent.Setup();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
    }

    public static void BuildLake(string id)
    {
        var t0 = DateTime.Now;
        LD = LakeDir(id); meshTag = id; vegCrossFade = (id == "tallpine" || id == "heronmarsh") && Environment.GetEnvironmentVariable("SV_XF") != "0";   // T2
        Directory.CreateDirectory(LD + "Meshes"); Directory.CreateDirectory(LD + "Materials");
        var lv = JsonUtility.FromJson<LakeVenue>(File.ReadAllText(LD + "venue.json"));
        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var root = GameObject.Find("SkiViewLake").transform;
        root.name = "Lake_" + id;
        // keep the shared rigs (boat, skier, rope, skier water FX) and the far hills ring; rebuild everything else
        var keep = new HashSet<string> { "Rope", "Handle", "SkierWaterFx" };   // pass 16: the home FarHills / SkylineFringe rings are replaced by the lake backdrop
        Transform TopOf(Transform t) { while (t != null && t.parent != root) t = t.parent; return t; }
        var keepT = new HashSet<Transform> { TopOf(game.boat), TopOf(game.skier) };
        var del = new List<GameObject>();
        foreach (Transform c in root) if (!keep.Contains(c.name) && !keepT.Contains(c)) del.Add(c.gameObject);
        Debug.Log($"[SkiView] lake {id}: removing {string.Join(", ", del.Select(g => g.name))}");
        foreach (var g in del) UnityEngine.Object.DestroyImmediate(g);
        foreach (var go in scene.GetRootGameObjects()) if (go.GetComponent<ReflectionProbe>() != null) UnityEngine.Object.DestroyImmediate(go);

        var sun = GameObject.Find("Sun").GetComponent<Light>();
        LakeSky(lv.lake.mood, sun);
        var terrain = LakeTerrain(lv, root);
        LakeWater(lv, root, game.cam);
        BuildCourse(new Venue { course = lv.course, terrain = lv.terrain }, root);
        { var sc = root.Find("SlalomCourse"); if (sc != null) MergeRig(sc, new[] { sc }, "course_" + id); }   // pass 18
        bool gulf = lv.lake.style == "gulf";
        // pass 19: the tree set is separate from the terrain style (Gulfwind / Marrowind: gulf ground, hardwood / live-oak trees)
        string treeSet = string.IsNullOrEmpty(lv.lake.trees) ? (gulf ? "gulf" : "pnw") : lv.lake.trees;
        if (treeSet == "gulf") GulfTrees(lv, terrain); else if (treeSet == "desert") DesertTrees(lv, terrain); else LakeTrees(lv, terrain);
        LakeShore(lv, root, terrain);
        LakeBuildings(lv, root, terrain);
        MergeBuildings(root.Find("Buildings"), id);
        KitBuildings(lv, root, terrain);   // pass 19: kit pieces keep their LODGroups (not merged)   // pass 17: one mesh per material per building (draw calls on phones / WebGL)
        LakeVolume(lv.lake.mood, root);
        if (gulf) GulfBackdrop(root, lv.lake.mood.fogColor, -sun.transform.forward, 1700 + id.Length);
        else
        {
            backdropTint = lv.lake.style == "desert" ? new Color(0.50f, 0.48f, 0.54f) : (Color?)null;   // pass 19: blue-grey / violet desert ridges #807c89
            backdropStyle = id == "cedarbend" ? "hills" : id == "heronmarsh" ? "flat" : id == "tallpine" ? "pnw" : null;   // T3: Tall Pine
            LakeBackdrop(root, lv.lake.mood.fogColor, -sun.transform.forward, 1600 + id.Length);
            backdropStyle = null;
            backdropTint = null;
        }
        if (lv.farSil != null && lv.farSil.Length > 0) FarShore(lv, root, lv.lake.mood.fogColor, -sun.transform.forward);
        // pass 17: phone / browser plant density + LOD tiers (runtime copy of the TerrainData; source untouched)
        foreach (var old in UnityEngine.Object.FindObjectsByType<LakeDetail>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(old);   // pass 19: the scene template carried a second LakeDetail (both ran Apply)
        var det = root.gameObject.AddComponent<LakeDetail>();
        det.terrain = terrain;
        det.foliageA2C = id == "tallpine" || id == "heronmarsh";   // T2
        var rim = lv.lake.mood.foliageRim; det.foliageRim = rim != null && rim.Length == 4 ? new Vector4(rim[0], rim[1], rim[2], rim[3]) : Vector4.zero;   // lake P1
        // phone tier: Christopher's _mobile trees where present, else the plain prefab
        det.phonePrototypes = terrain.terrainData.treePrototypes.Select(tp => tp.prefab != null && tp.prefab.name.EndsWith("_xf") ? (Veg(tp.prefab.name.Substring(0, tp.prefab.name.Length - 3) + "_mobile") ?? Veg(tp.prefab.name.Substring(0, tp.prefab.name.Length - 3))) : null).ToArray();
        det.lakeBounds = new Vector4(lv.lakeBounds[0], lv.lakeBounds[1], lv.lakeBounds[2], lv.lakeBounds[3]);
        det.plantPrototypes = VegPlants(terrain.terrainData, treeSet == "gulf" ? new[] { G_PALMETTO, G_SEAOATS } : treeSet == "desert" ? new[] { D_CREOSOTE } : new[] { 6, 7, P_FERN, P_BLACKBERRY, P_TALLGRASS, P_REEDS });
        if (terrain.terrainData.treePrototypes.Count(tp => tp.prefab != null && tp.prefab.name.StartsWith("veg_")) > 0) det.mobileLodBias = 0.42f;   // pass 19: pack trees reach billboards sooner on phones
        { var mo = root.Find("SpanishMoss"); if (mo != null) det.smallObjects = mo.GetComponentsInChildren<Renderer>(true); }
        if (game.cam != null) game.cam.farClipPlane = 7000f;   // the backdrop reaches 6.2 km

        var pathGo = new GameObject("BoatPath"); pathGo.transform.SetParent(root, false);
        var bp = pathGo.AddComponent<BoatPath>();
        bp.step = lv.path.step; bp.xs = lv.path.x; bp.zs = lv.path.z; bp.speeds = lv.path.v; bp.along = lv.path.along; bp.dirs = lv.path.dir; bp.spins = lv.path.spin;
        bp.courseFwdSouth = new Vector2(lv.course.fS[0], lv.course.fS[1]);
        game.path = bp; game.islandCentres = lv.islandCentres;
        game.jumpRamp = null; game.jumpCourse = null; game.rampFx = new ParticleSystem[0]; game.rampFoam = null; game.rampWaterline = null;
        game.jumpDir = 1; game.passWindowLate = lv.passWindowLate > 0f ? lv.passWindowLate : 186f;
        if (lv.lake.hasRamp && lv.ramp != null && lv.ramp.lip != null && lv.ramp.lip.Length >= 2)
        {
            // pass 17: lake jump ramp (shared ramp builder), placed rampFromSlalom off the slalom line; jumpDir = which way the jump runs
            RAMP_FROM_SLALOM = lv.rampFromSlalom > 0f ? lv.rampFromSlalom : 18.5f;
            BuildRamp(new Venue { ramp = lv.ramp, course = lv.course, terrain = lv.terrain }, root);
            RAMP_FROM_SLALOM = 18.5f;
            game.jumpRamp = builtRamp; game.jumpCourse = builtJumpCourse != null ? builtJumpCourse.gameObject : null;
            game.rampFx = builtRampFx.ToArray(); game.rampFoam = builtRampFoam; game.rampWaterline = builtWaterline;
            game.rampHeight = lv.ramp.height; game.rampLenOut = lv.ramp.length; game.rampWidth = lv.ramp.width; game.rampUnder = RAMP_UNDER;
            game.jumpDir = lv.jumpDir == 0 ? 1 : lv.jumpDir;
        }
        { int nr = 0; foreach (Transform c in root) if (c != game.skier && c.name != "SlalomCourse" && c.name != "Rope") nr += TagNoReflect(c); Debug.Log($"[SkiView] lake {id}: no-reflect layer {nr} small renderers"); }
        game.lakeId = id; game.lakeName = lv.lake.name.Replace(" Lake", "");
        game.ambientClip = SkiViewBuilderAudio.Ambient(id);   // pass 18: the lake's baked ambient bed (ships in Lake_<id>)
        game.jumpOnly = lv.jumpOnly && game.jumpRamp != null;
        game.rampJumpModeOnly = lv.rampJumpModeOnly && game.jumpRamp != null;
        game.playBox = lv.playBox != null && lv.playBox.Length >= 8 ? new[] { new Vector2(lv.playBox[0], lv.playBox[1]), new Vector2(lv.playBox[2], lv.playBox[3]), new Vector2(lv.playBox[4], lv.playBox[5]), new Vector2(lv.playBox[6], lv.playBox[7]) } : null;   // pass 19
        { var sc = root.Find("SlalomCourse"); if (sc != null) sc.gameObject.SetActive(!game.jumpOnly); }   // pass 18: jump-only lake: no slalom buoys
        game.lakeScenes = LakeSceneNames(); game.lakeNames = LakeDisplayNames();
        var pal = game.boat.GetComponentInChildren<BoatPalette>() ?? game.boat.GetComponentInParent<BoatPalette>();
        if (pal != null && lv.lake.boat != null)
        {
            var b = lv.lake.boat; pal.lake = id;
            pal.deck = Hex(b.deck, pal.deck); pal.band = Hex(b.band, pal.band); pal.accent = Hex(b.accent, pal.accent); pal.livery = Hex(b.livery, pal.livery); pal.accentMetallic = b.accentMetallic;
            pal.Apply(); EditorUtility.SetDirty(pal);
        }
        LakeLivery(game.boat, lv.lake.boat != null ? lv.lake.boat.decal : null);
        game.Init();
        EditorSceneManager.SaveScene(scene, LakeScenePath(id));
        AssetDatabase.SaveAssets();
        Debug.Log($"[SkiView] lake {id} ({lv.lake.name}) scene saved {LakeScenePath(id)} in {(DateTime.Now - t0).TotalSeconds:0.0}s; boat band {(pal != null ? ColorUtility.ToHtmlStringRGB(pal.band) : "-")} accent {(pal != null ? ColorUtility.ToHtmlStringRGB(pal.accent) : "-")} deck {(pal != null ? ColorUtility.ToHtmlStringRGB(pal.deck) : "-")}");
    }

    /// <summary>Pass 16: the "Ski View" script on the hull is the home lake's only. Other lakes get "SWLM" + the fictional
    /// registration (Generated/T_Livery_SWLM.png, Tools/gen_livery_lake.py) on a lake copy of M_Livery (name keeps the
    /// M_Livery prefix so BoatPalette still tints it). decal: "swlm" (default) or "skiview".</summary>
    static void LakeLivery(Transform boat, string decal)
    {
        if (decal == "skiview") return;
        const string TEX = "Assets/SkiView/Generated/T_Livery_SWLM.png";
        if (AssetImporter.GetAtPath(TEX) is TextureImporter ti && (ti.alphaIsTransparency == false || ti.maxTextureSize != 2048))
        { ti.alphaIsTransparency = true; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 2048; ti.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX);
        Transform top = boat; while (top.parent != null && top.parent.GetComponent<SkiViewGame>() == null && top.parent.name.StartsWith("Lake_") == false) top = top.parent;
        int swapped = 0; Material lm = null;
        foreach (var r in top.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats = r.sharedMaterials; bool ch = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].name.StartsWith("M_Livery") || mats[i].name.StartsWith("M_Livery_SWLM")) continue;
                if (lm == null)
                {
                    string p = LD + "Materials/M_Livery_SWLM.mat";
                    lm = AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (lm == null) { lm = new Material(mats[i]); AssetDatabase.CreateAsset(lm, p); } else lm.CopyPropertiesFromMaterial(mats[i]);
                    foreach (var prop in new[] { "_BaseMap", "_MainTex" }) if (lm.HasProperty(prop)) lm.SetTexture(prop, tex);
                    EditorUtility.SetDirty(lm);
                }
                mats[i] = lm; ch = true; swapped++;
            }
            if (ch) r.sharedMaterials = mats;
        }
        var pal = top.GetComponentInChildren<BoatPalette>(); if (pal != null) pal.Apply();
        Debug.Log($"[SkiView] lake livery: {decal ?? "swlm"} on {swapped} slot(s)" + (tex == null ? " (texture missing!)" : ""));
    }

    // ------------------------------------------------------------------ helpers (all writes go to the lake folder)
    static Mesh LakeMesh(Mesh m, string name)
    {
        string p = LD + "Meshes/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(p) != null) AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p); return m;
    }
    static Material LakeMat(string name, Texture2D albedo, Texture2D normal, Color tint, float smooth, Vector2 tiling, float bump = 1f, bool twoSided = false, float metal = 0f)
    {
        string p = LD + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetTexture("_BaseMap", albedo); m.SetTextureScale("_BaseMap", tiling); m.SetColor("_BaseColor", tint);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); } else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        m.SetFloat("_Cull", twoSided ? 0 : 2); m.doubleSidedGI = twoSided; m.enableInstancing = true;
        EditorUtility.SetDirty(m); return m;
    }
    static Material LakeFlat(string name, Color c, float smooth = 0.3f, float metal = 0f) => LakeMat(name, null, null, c, smooth, Vector2.one, 1f, false, metal);

    // ------------------------------------------------------------------ sky / light / fog
    static Cubemap HdrCube(string file, int max, bool convolve)
    {
        string p = ENV + "Sky/" + file;
        if (AssetImporter.GetAtPath(p) is TextureImporter ti && (ti.textureShape != TextureImporterShape.TextureCube || ti.maxTextureSize != max || ti.mipmapEnabled != convolve))
        {
            ti.textureShape = TextureImporterShape.TextureCube; ti.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
            ti.maxTextureSize = max; ti.mipmapEnabled = convolve; ti.sRGBTexture = false; ti.textureCompression = TextureImporterCompression.Compressed;
            var s = new TextureImporterSettings(); ti.ReadTextureSettings(s);
            s.cubemapConvolution = convolve ? TextureImporterCubemapConvolution.Specular : TextureImporterCubemapConvolution.None; s.seamlessCubemap = !convolve; ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Cubemap>(p);
    }

    static void LakeSky(LakeMood m, Light sun)
    {
        bool overcast = m.hdri.Contains("overcast"), clear = m.hdri.Contains("syferfontein"), sunset = m.hdri.Contains("sunset");
        // Phase 1 review #3: tamed sky variants (Tools/sky_suncap.py). Villa Serena is noon (sun 79.9 deg, out of frame), so its
        // clear sky has the low HDRI sun painted out; Tall Pine's sunset disc/glow is soft-capped. Other lakes unchanged.
        string bgFile = overcast ? "overcast_bg_cube.hdr" : sunset ? "sunset_bg_cube.hdr" : clear ? "clear_bg_cube.hdr" : "sky_bg_cube.hdr";
        if (LakeIs("villaserena") && clear) bgFile = "clear_bg_nosun.hdr";
        if (LakeIs("tallpine") && sunset) bgFile = "sunset_bg_cap.hdr";
        var bg = HdrCube(bgFile, 2048, false);   // pass 17: clear Gulf sky; pass 18: golden-hour sky (CC0)
        var refl = HdrCube(overcast ? "overcast_reflection_cube.hdr" : sunset ? (LakeIs("tallpine") ? "sunset_reflection_cap.hdr" : "sunset_reflection_cube.hdr") : "sky_reflection_cube.hdr", 512, true);   // review #3: capped for Tall Pine
        float el = m.sunEl * Mathf.Deg2Rad, az = m.sunAz * Mathf.Deg2Rad;   // compass azimuth (0 = north, 90 = east)
        Vector3 toSun = new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
        sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
        sun.color = C3(m.sunColor); sun.intensity = m.sunIntensity;
        sun.shadows = LightShadows.Soft; sun.shadowStrength = m.shadowStrength; sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
        string sp = LD + "Materials/Sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(sp);
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Cubemap")); AssetDatabase.CreateAsset(sky, sp); }
        sky.shader = Shader.Find("Skybox/Cubemap");
        sky.SetTexture("_Tex", bg); sky.SetFloat("_Rotation", Mathf.Repeat(180f - m.sunAz, 360f)); sky.SetFloat("_Exposure", m.skyExposure); sky.SetColor("_Tint", m.skyTint != null && m.skyTint.Length == 3 ? new Color(m.skyTint[0], m.skyTint[1], m.skyTint[2], 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky; RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = C3(m.ambientSky); RenderSettings.ambientEquatorColor = C3(m.ambientEquator); RenderSettings.ambientGroundColor = C3(m.ambientGround);
        RenderSettings.ambientIntensity = 1f;
        if (refl != null) { RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = refl; }
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = m.fogDensity; RenderSettings.fogColor = C3(m.fogColor);
        Debug.Log($"[SkiView] lake sky: {m.hdri}, sun el {m.sunEl} az {m.sunAz} -> euler {sun.transform.eulerAngles}");
    }

    static void LakeVolume(LakeMood md, Transform root)
    {
        string p = LD + "Volume.asset";
        if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(prof, p);
        prof.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bl = prof.Add<Bloom>(true); bl.intensity.Override(LakeIs("villaserena") || LakeIs("tallpine") ? 0.10f : 0.18f); bl.threshold.Override(LakeIs("villaserena") || LakeIs("tallpine") ? 1.5f : 1.2f);   // review #3: smaller sun bloom on the two flagged lakes
         bl.scatter.Override(0.6f); bl.highQualityFiltering.Override(false);
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(md.postExposure); ca.contrast.Override(md.contrast); ca.saturation.Override(md.saturation);
        var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(md.temperature); wb.tint.Override(md.tint);
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.14f); vg.smoothness.Override(0.45f);
        foreach (var c in prof.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, prof); }
        EditorUtility.SetDirty(prof); AssetDatabase.SaveAssets();
        var vgo = new GameObject("PostVolume"); vgo.transform.SetParent(root, false);
        var vol = vgo.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof;
    }

    // ------------------------------------------------------------------ terrain
    static TerrainLayer LakeLayer(string name, string color, string normal, float tile, float smooth, Color tint, float nscale = 1f)
    {
        string p = LD + "Materials/TL_" + name + ".terrainlayer";
        var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
        if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, p); }
        l.diffuseTexture = EnvTex(color, false, 1024, false, 512);
        l.normalMapTexture = EnvTex(normal, true, 512, false, 256); l.normalScale = nscale;
        l.tileSize = new Vector2(tile, tile); l.smoothness = smooth; l.metallic = 0;
        l.diffuseRemapMin = Vector4.zero; l.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1);
        l.smoothnessSource = TerrainLayerSmoothnessSource.Constant;
        EditorUtility.SetDirty(l); return l;
    }

    static Terrain LakeTerrain(LakeVenue v, Transform root)
    {
        int res = v.terrain.res; float size = v.terrain.size;
        string tdp = LD + "Terrain.asset";
        if (File.Exists(tdp)) AssetDatabase.DeleteAsset(tdp);
        var td = new TerrainData(); AssetDatabase.CreateAsset(td, tdp);
        td.heightmapResolution = res; td.size = new Vector3(size, v.terrain.hrange, size);
        var raw = File.ReadAllBytes(LD + "heightmap.r16");
        var h = new float[res, res];
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) { int i = (z * res + x) * 2; h[z, x] = (raw[i] | (raw[i + 1] << 8)) / 65535f; }
        td.SetHeights(0, 0, h);
        // R mown PNW lawn (cooler, deeper green than Ski View's watered lawns), G rough / summer-dry grass, B fir duff, A gravel
        if (v.lake.style == "desert") td.terrainLayers = DesertLayers();   // pass 19 Saguaro Flats
        else if (v.lake.style == "gulf")
            // pass 17 Gulf: R St Augustine lawn, G white quartz sand, B flatwoods floor (needle litter / wiregrass), A asphalt / shell
            td.terrainLayers = new[]
            {
                LakeLayer("Lawn", "lawn_color.jpg", "lawn_normal.png", 2.2f, 0.06f, new Color(0.84f, 0.92f, 0.62f)),   // b10.7: no G>1 bloom (Mamta)
                LakeLayer("Sand", "sand_white_color.jpg", "sand_white_normal.png", 3.0f, 0.05f, new Color(0.88f, 0.80f, 0.66f), 0.7f),   // b10.7: warmer darker sand (never chalky)
                LakeLayer("Flatwoods", "drygrass_color.jpg", "drygrass_normal.png", 3.2f, 0.04f, new Color(0.70f, 0.68f, 0.50f)),
                LakeLayer("Road", "gravel_color.jpg", "gravel_normal.png", 1.6f, 0.12f, new Color(0.78f, 0.80f, 0.82f), 0.6f),
            };
        else td.terrainLayers = new[]
        {
            LakeLayer("Lawn", "lawn_color.jpg", "lawn_normal.png", 2.4f, 0.05f, new Color(0.78f, 0.92f, 0.52f)),   // b10.7: no G>1 bloom (Mamta)
            LakeLayer("Rough", "drygrass_color.jpg", "drygrass_normal.png", 3.0f, 0.03f, new Color(0.66f, 0.80f, 0.46f)),   // b9: Ski View grass fix (pale straw read as snow)
            LakeLayer("Forest", "forest_color.jpg", "forest_normal.png", 3.5f, 0.05f, new Color(0.30f, 0.38f, 0.24f)),   // mossy PNW floor (pass 15 r2: darker - tan gaps read sparse from the air) (reads dark green under the canopy)
            LakeLayer("Gravel", "shore_color.jpg", "shore_normal.png", 2.2f, 0.06f, new Color(0.70f, 0.56f, 0.42f), 1.2f)   // b9: Ski View waterline fix: brown dirt / mud, matte (was pale glossy gravel),
        };
        td.alphamapResolution = 1024;
        var sp = new Texture2D(2, 2); sp.LoadImage(File.ReadAllBytes(LD + "splat.png"));
        int ar = td.alphamapResolution; var a = new float[ar, ar, 4];
        for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                Color c = sp.GetPixelBilinear((x + 0.5f) / ar, (y + 0.5f) / ar);
                float s = c.r + c.g + c.b + c.a + 1e-4f;
                a[y, x, 0] = c.r / s; a[y, x, 1] = c.g / s; a[y, x, 2] = c.b / s; a[y, x, 3] = c.a / s;
            }
        td.SetAlphamaps(0, 0, a);
        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
        var tgo = Terrain.CreateTerrainGameObject(td);
        tgo.name = "Terrain"; tgo.transform.SetParent(root, false);
        tgo.transform.position = new Vector3(-size / 2 + v.terrain.cx, v.terrain.ymin, -size / 2 + v.terrain.cz);   // pass 19: centre offset
        var t = tgo.GetComponent<Terrain>();
        t.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>(MAT + "TerrainLit.mat");
        t.heightmapPixelError = 4; t.basemapDistance = 20000; t.drawInstanced = true; t.shadowCastingMode = ShadowCastingMode.TwoSided;
        return t;
    }

    // ------------------------------------------------------------------ water
    static void LakeWater(LakeVenue v, Transform root, Camera cam)
    {
        float x0 = Mathf.Floor(v.lakeBounds[0]), x1 = Mathf.Ceil(v.lakeBounds[1]), z0 = Mathf.Floor(v.lakeBounds[2]), z1 = Mathf.Ceil(v.lakeBounds[3]), cell = v.waterCell > 0f ? v.waterCell : 2f;
        int nx = Mathf.CeilToInt((x1 - x0) / cell) + 1, nz = Mathf.CeilToInt((z1 - z0) / cell) + 1;
        var verts = new Vector3[nx * nz];
        for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++) verts[j * nx + i] = new Vector3(x0 + i * cell, 0, z0 + j * cell);
        var tris = new int[(nx - 1) * (nz - 1) * 6]; int k = 0;
        for (int j = 0; j < nz - 1; j++) for (int i = 0; i < nx - 1; i++)
            { int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1; tris[k++] = a; tris[k++] = c; tris[k++] = b; tris[k++] = b; tris[k++] = c; tris[k++] = d; }
        var m = new Mesh { name = "lake_water", indexFormat = IndexFormat.UInt32 };
        m.vertices = verts; m.triangles = tris; m.RecalculateNormals();
        m.bounds = new Bounds(new Vector3((x0 + x1) / 2, 0, (z0 + z1) / 2), new Vector3(x1 - x0, 4, z1 - z0));
        LakeMesh(m, "lake_water");
        string wp = LD + "Materials/Water.mat";
        var wm = AssetDatabase.LoadAssetAtPath<Material>(wp);
        if (wm == null) { wm = new Material(AssetDatabase.LoadAssetAtPath<Material>(MAT + "Water.mat")); AssetDatabase.CreateAsset(wm, wp); }
        else wm.CopyPropertiesFromMaterial(AssetDatabase.LoadAssetAtPath<Material>(MAT + "Water.mat"));
        var g = MeshObj("Water", root, m, wm);
        var r = g.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true;
        WaterV5(g, wm, cam);
        wm.SetColor("_ShallowColor", C3(v.lake.water.shallow)); wm.SetColor("_DeepColor", C3(v.lake.water.deep)); wm.SetColor("_ReflTint", C3(v.lake.water.refl));
        wm.SetFloat("_SunSpec", LakeIs("tallpine") ? 0.08f : 1f);   // review #3: Tall Pine low-sun glare streak
        EditorUtility.SetDirty(wm); AssetDatabase.SaveAssetIfDirty(wm);
        if (v.farWater) FarWater(v, root, wm);   // pass 19
    }

    // ------------------------------------------------------------------ trees (terrain-instanced, shared prefabs, not regenerated)
    static void LakeTrees(LakeVenue v, Terrain terrain)
    {
        string[] names = { "DouglasFir", "WesternRedCedar", "DouglasFir_B", "WesternRedCedar_B", "RedAlder", "BigleafMaple", "Understory", "Understory_B", "CanopyFill" };
        var td = terrain.terrainData;
        var kit = LakePlantKit();   // pass 16: cottonwood, sword fern, blackberry, unmowed grass, reeds (prototypes 9-13)
        var vp = new VegProtos(names.Select(n => new TreePrototype { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ENVPRE + n + ".prefab") })
            .Concat(kit.Select(k => new TreePrototype { prefab = k })));   // pass 19: vegetation-pack prototypes appended on first use
        bool veg = VegReady; string lid = v.lake.id;
        Vector3 tp = terrain.transform.position, size = td.size;
        var rnd = new System.Random(1515);
        float Fr() => (float)rnd.NextDouble();
        var list = new List<TreeInstance>();
        int[] byKind = new int[12]; int extra = 0; var mossAt = new List<Vector4>(); var mossScale = new List<Vector2>();
        void Add(float x, float y, float z, float hs, float ws, int proto)
        {
            float nx = (x - tp.x) / size.x, nz = (z - tp.z) / size.z;
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) return;
            if (y < 0) y = terrain.SampleHeight(new Vector3(x, 0, z)) + tp.y;
            list.Add(new TreeInstance { position = new Vector3(nx, (y - 0.3f - tp.y) / size.y, nz), heightScale = hs, widthScale = ws, rotation = Fr() * Mathf.PI * 2f, prototypeIndex = proto, color = Color.white, lightmapColor = Color.white });
        }
        int n = v.treesFlat.Length / 5;
        for (int i = 0; i < n; i++)
        {
            float x = v.treesFlat[i * 5], y = v.treesFlat[i * 5 + 1], z = v.treesFlat[i * 5 + 2], s = v.treesFlat[i * 5 + 3]; int kind = (int)v.treesFlat[i * 5 + 4];
            if (veg) { string vid = VegPick(lid, "pnw", kind, s, Fr, out float vs); if (vid != null) { Add(x, y, z, vs, vs * (0.9f + Fr() * 0.2f) * VegWidth(vid), vp[vid]); continue; } }
            if (kind >= 12) kind = VegFallback("pnw", kind);
            byKind[Mathf.Clamp(kind, 0, 11)]++;
            float clump = 0.65f * Mathf.PerlinNoise(x * 0.029f + 5.1f, z * 0.029f + 9.4f) + 0.35f * Mathf.PerlinNoise(x * 0.067f + 17.3f, z * 0.067f + 2.2f);
            float core = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.72f, clump));
            switch (kind)
            {
                case 0:   // the fir / cedar wall at the lawn edge: 30-45 m (LAKE-LAYOUTS), few gaps
                {
                    bool cedar = Fr() < 0.3f; float hs = (cedar ? 1.75f : 1.95f) + Fr() * 0.85f;
                    Add(x, y, z, hs, hs * (cedar ? 0.82f : 0.72f) * (0.9f + Fr() * 0.2f), cedar ? (Fr() < 0.5f ? 1 : 3) : (Fr() < 0.5f ? 0 : 2));
                    if (Fr() < 0.7f) { float a = Fr() * 6.283f, d = 3f + Fr() * 3f, hc = 0.8f + Fr() * 0.5f; Add(x + Mathf.Cos(a) * d, -1, z + Mathf.Sin(a) * d, hc, hc * 1.2f, 8); extra++; }
                    if (Fr() < 0.6f) { float a = Fr() * 6.283f, d = 1.5f + Fr() * 3f, hsS = 0.8f + Fr() * 1.0f; Add(x + Mathf.Cos(a) * d, -1, z + Mathf.Sin(a) * d, hsS, hsS * 1.3f, Fr() < 0.5f ? 6 : 7); extra++; }
                    break;
                }
                case 4:   // interior forest: clumped stands, 18-40 m
                {
                    // pass 16: continuous canopy - only small, rare clearings (the floor between crowns reads tan from the air)
                    float gapN = Mathf.PerlinNoise(x * 0.011f + 31.7f, z * 0.011f + 12.3f);
                    if (gapN < 0.10f && Fr() < 0.7f) continue;
                    bool cedar = Fr() < 0.22f; float hs = s * (1.15f + Fr() * 0.9f) * Mathf.Lerp(0.7f, 1.2f, core);
                    if (Fr() < 0.06f && core > 0.4f) hs *= 1.6f;
                    Add(x, y, z, hs, hs * (0.75f + Fr() * 0.25f), cedar ? (Fr() < 0.5f ? 1 : 3) : (Fr() < 0.5f ? 0 : 2));
                    if (Fr() < 0.16f) { float a = Fr() * 6.283f, d = 3f + Fr() * 4f, hc = (0.75f + Fr() * 0.5f); Add(x + Mathf.Cos(a) * d, -1, z + Mathf.Sin(a) * d, hc, hc * 1.2f, 8); extra++; }
                    if (Fr() < 0.10f) { float a = Fr() * 6.283f, d = 2f + Fr() * 3f, hsS = 0.8f + Fr() * 0.9f; Add(x + Mathf.Cos(a) * d, -1, z + Mathf.Sin(a) * d, hsS, hsS * 1.3f, Fr() < 0.5f ? 6 : 7); extra++; }
                    break;
                }
                case 1:   // bigleaf maple / red alder (west road strip, lawn trees)
                {
                    bool maple = Fr() < 0.5f; float hs = s * (0.9f + Fr() * 0.55f);
                    Add(x, y, z, hs, hs * (0.95f + Fr() * 0.3f), maple ? 5 : 4);
                    break;
                }
                case 2:   // solitary fir on the lawn: full low branches
                {
                    float hs = s * (1.2f + Fr() * 0.6f); Add(x, y, z, hs, hs * 0.85f, Fr() < 0.5f ? 0 : 2); break;
                }
                case 5: { float hs = s * (0.8f + Fr() * 0.5f); Add(x, y, z, hs, hs * (0.9f + Fr() * 0.4f), P_TALLGRASS); break; }   // unmowed grass
                case 6: { float hs = s * (0.8f + Fr() * 0.5f); Add(x, y, z, hs, hs * (0.9f + Fr() * 0.4f), P_REEDS); break; }       // reeds at the water
                case 7: { float hs = s * (0.8f + Fr() * 0.6f); Add(x, y, z, hs, hs * (1.0f + Fr() * 0.3f), P_FERN); break; }        // sword fern
                case 8: { float hs = s * (0.7f + Fr() * 0.6f); Add(x, y, z, hs, hs * (1.1f + Fr() * 0.5f), P_BLACKBERRY); break; }  // blackberry
                case 9: { float hs = s * (1.5f + Fr() * 0.6f); Add(x, y, z, hs, hs * (0.85f + Fr() * 0.25f), P_COTTONWOOD); break; } // black cottonwood (taller, 30-40 m)
                case 10:   // pass 18 Tall Pine: coast redwood windbreak - narrow spire crowns, 30-40 m (cedar prototypes stretched tall)
                {
                    float hs = s * (2.5f + Fr() * 0.6f); Add(x, y, z, hs, hs * (0.30f + Fr() * 0.05f), Fr() < 0.5f ? 1 : 3); break;
                }
                case 11:   // pass 18 Heron Marsh: live oak - broad, low, spreading crown (maple prototype squashed wide) + Spanish moss
                {
                    float hs = s * (0.85f + Fr() * 0.3f), ws = hs * (1.55f + Fr() * 0.35f); Add(x, y, z, hs, ws, 5);
                    mossAt.Add(new Vector4(x, y < 0 ? terrain.SampleHeight(new Vector3(x, 0, z)) + tp.y : y, z, 0f)); mossScale.Add(new Vector2(hs, ws)); break;
                }
                default:  // shrubs (islands)
                {
                    float hs = 0.8f + Fr() * 0.7f * s; Add(x, y, z, hs, hs * 1.3f, Fr() < 0.5f ? 6 : 7); break;
                }
            }
        }
        td.treePrototypes = vp.list.ToArray(); td.RefreshPrototypes();
        td.SetTreeInstances(list.ToArray(), true);
        if (mossAt.Count > 0) SpanishMoss(terrain.transform.parent, mossAt, mossScale, AssetDatabase.LoadAssetAtPath<GameObject>(ENVPRE + "BigleafMaple.prefab"));
        terrain.treeDistance = 2400f; terrain.treeBillboardDistance = 2400f; terrain.treeCrossFadeLength = 0f;
        terrain.treeMaximumFullLODCount = 50000; terrain.drawTreesAndFoliage = true; terrain.treeLODBiasMultiplier = 1f;
        EditorUtility.SetDirty(td);
        Debug.Log($"[SkiView] lake trees: {list.Count} instances (wall {byKind[0]}, forest {byKind[4]}, broadleaf {byKind[1]}, cottonwood {byKind[9]}, lawn firs {byKind[2]}, shrubs {byKind[3]}, grass {byKind[5]}, reeds {byKind[6]}, ferns {byKind[7]}, blackberry {byKind[8]}, +{extra} fillers/understory, {vp.count} vegetation-pack trees)");
    }

    /// <summary>Pass 18: Spanish moss on the live oaks - thin tapering grey-green strands hanging from the lower crown, merged into one
    /// mesh per 160 m cell (a handful of draws; off the reflection layer, LakeDetail drops them on phones).</summary>
    static void SpanishMoss(Transform root, List<Vector4> at, List<Vector2> sc, GameObject oakPrefab)
    {
        float H0 = 14f, R0 = 6f;
        if (oakPrefab != null) { var rr = oakPrefab.GetComponentsInChildren<Renderer>(); if (rr.Length > 0) { var b = rr[0].bounds; foreach (var r in rr) b.Encapsulate(r.bounds); H0 = b.size.y; R0 = Mathf.Max(b.extents.x, b.extents.z); } }
        var mat = LakeMat("SpanishMoss", null, null, new Color(0.50f, 0.54f, 0.44f), 0.1f, Vector2.one, 1f, true);
        var go = new GameObject("SpanishMoss").transform; go.SetParent(root, false);
        var cells = new Dictionary<(int, int), (List<Vector3>, List<int>, List<Vector3>)>();
        var rnd = new System.Random(4242);
        float Fr() => (float)rnd.NextDouble();
        for (int i = 0; i < at.Count; i++)
        {
            Vector3 c = at[i]; float hs = sc[i].x, ws = sc[i].y; var key = (Mathf.FloorToInt(c.x / 160f), Mathf.FloorToInt(c.z / 160f));
            if (!cells.TryGetValue(key, out var cl)) cells[key] = cl = (new List<Vector3>(), new List<int>(), new List<Vector3>());
            int n = 26 + rnd.Next(14);
            for (int k = 0; k < n; k++)
            {
                float a = Fr() * Mathf.PI * 2f, rr = R0 * ws * (0.35f + 0.6f * Mathf.Sqrt(Fr())), y = c.y + H0 * hs * (0.42f + 0.25f * Fr());
                Vector3 top = new Vector3(c.x + Mathf.Cos(a) * rr, y, c.z + Mathf.Sin(a) * rr);
                float len = 0.9f + Fr() * 1.8f, w = 0.18f + Fr() * 0.16f; Vector3 side = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * w;
                int v0 = cl.Item1.Count;
                cl.Item1.Add(top - side); cl.Item1.Add(top + side); cl.Item1.Add(top + Vector3.down * len + side * 0.15f); cl.Item1.Add(top + Vector3.down * len - side * 0.15f);
                Vector3 nn = new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)).normalized; for (int q = 0; q < 4; q++) cl.Item3.Add(nn);
                cl.Item2.AddRange(new[] { v0, v0 + 2, v0 + 1, v0, v0 + 3, v0 + 2 });
            }
        }
        int ci = 0;
        foreach (var kv in cells)
        {
            var m = new Mesh { name = "moss_" + ci, indexFormat = IndexFormat.UInt32 }; m.SetVertices(kv.Value.Item1); m.SetNormals(kv.Value.Item3); m.SetTriangles(kv.Value.Item2, 0); m.RecalculateBounds();
            LakeMesh(m, "moss_" + ci);
            var g = new GameObject("Moss_" + ci); g.transform.SetParent(go, false); g.layer = PlanarReflection.NO_REFLECT_LAYER;
            g.AddComponent<MeshFilter>().sharedMesh = m; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            ci++;
        }
        Debug.Log($"[SkiView] Spanish moss: {at.Count} live oaks, {ci} merged cells");
    }

    // ------------------------------------------------------------------ bank: grass edging strip all round (+ riprap at the moorage)
    static void LakeShore(LakeVenue v, Transform root, Terrain terrain)
    {
        float H(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
        var rings = new List<List<Vector2>>();
        var sh = new List<Vector2>(); for (int i = 0; i + 1 < v.shore.Length; i += 2) sh.Add(new Vector2(v.shore[i], v.shore[i + 1])); rings.Add(sh);
        int o = 0;
        foreach (int cnt in v.islandCounts ?? new int[0]) { var r = new List<Vector2>(); for (int i = 0; i < cnt; i++) r.Add(new Vector2(v.islandPolys[(o + i) * 2], v.islandPolys[(o + i) * 2 + 1])); o += cnt; rings.Add(r); }
        var rockM = ShoreRockMat();   // b9: shared definition (this line used to undo the home bank fix)
        bool gulfS = v.lake.style == "gulf";
        var grassM = gulfS ? EnvMat("ShoreSandWet", EnvTex("sand_white_color.jpg", false, 1024, false, 512), EnvTex("sand_white_normal.png", true, 512, false, 256), new Color(0.80f, 0.72f, 0.58f), 0.08f, new Vector2(0.35f, 0.35f))   // b10.7: matte match disk
            : EnvMat("ShoreMud", EnvTex("shore_strip_grass.png", false, 1024, true, 512, true), EnvTex("lawn_normal.png", true, 512, false, 256), new Color(0.50f, 0.38f, 0.27f), 0.12f, Vector2.one, true, 0.5f, false, 0.6f);
        var rip = new List<Vector2>(); if (v.riprap != null) for (int i = 0; i + 1 < v.riprap.Length; i += 2) rip.Add(new Vector2(v.riprap[i], v.riprap[i + 1]));
        bool NearRip(Vector2 p) { foreach (var q in rip) if ((q - p).sqrMagnitude < 9f) return true; return false; }
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tRock = new List<int>(); var tGrass = new List<int>();
        var rockPts = new List<Vector4>();
        float[] off = gulfS ? new[] { -1.4f, 0f, 0.6f, 1.4f } : new[] { -0.9f, 0f, 0.9f, 2.4f }, vv = { 0f, 0.33f, 0.62f, 1f };
        foreach (var ring in rings)
        {
            int N = ring.Count; if (N < 3) continue;
            // resample islands (12-24 pts) to ~1.5 m
            var pts = new List<Vector2>();
            for (int i = 0; i < N; i++) { Vector2 a = ring[i], b = ring[(i + 1) % N]; int s = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 1.5f)); for (int j = 0; j < s; j++) pts.Add(Vector2.Lerp(a, b, j / (float)s)); }
            N = pts.Count; float u = 0; int baseRow = -1, firstRow = verts.Count;
            for (int k = 0; k <= N; k++)
            {
                Vector2 p = pts[k % N], t = (pts[(k + 1) % N] - pts[(k - 1 + N) % N]).normalized, n = new Vector2(-t.y, t.x);
                if (H(p.x + n.x * 4f, p.y + n.y * 4f) < H(p.x - n.x * 4f, p.y - n.y * 4f)) n = -n;   // inland = uphill
                if (k > 0) u += Vector2.Distance(p, pts[(k - 1) % N]) / 4f;
                bool rock = NearRip(p);
                if (rock && k < N) rockPts.Add(new Vector4(p.x, p.y, n.x, n.y));
                int row = verts.Count;
                for (int c = 0; c < 4; c++)
                {
                    Vector2 w = p + n * off[c];
                    float y = c == 0 ? -0.35f : c == 1 ? 0.06f : c == 2 ? Mathf.Max(H(w.x, w.y), 0.12f) + 0.10f : H(w.x, w.y) + 0.04f;
                    verts.Add(new Vector3(w.x, y, w.y)); uvs.Add(new Vector2(u, vv[c]));
                }
                if (baseRow >= 0) { var tl = rock ? tRock : tGrass; for (int c = 0; c < 3; c++) { int a = baseRow + c, b = baseRow + c + 1, a2 = row + c, b2 = row + c + 1; tl.Add(a); tl.Add(b); tl.Add(a2); tl.Add(b); tl.Add(b2); tl.Add(a2); } }
                baseRow = row;
            }
        }
        foreach (var t in new[] { tRock, tGrass })
            for (int k = 0; k < t.Count; k += 3) { Vector3 nn = Vector3.Cross(verts[t[k + 1]] - verts[t[k]], verts[t[k + 2]] - verts[t[k]]); if (nn.y < 0) { int tmp = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = tmp; } }
        var m = new Mesh { name = "shoreline_edging", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.subMeshCount = 2; m.SetTriangles(tRock, 0); m.SetTriangles(tGrass, 1);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        LakeMesh(m, m.name);
        var g = MeshObj("ShorelineEdging", root, m, rockM, grassM);
        var mr = g.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true; g.isStatic = true;
        if (gulfS) { Debug.Log($"[SkiView] lake shore (gulf sand lip): {rings.Count} rings, {verts.Count} verts"); return; }
        // riprap rocks at the moorage: shared rock prefabs as terrain tree prototypes (no regeneration)
        var td = terrain.terrainData; var protos = new List<TreePrototype>(td.treePrototypes); int first = protos.Count;
        for (int k = 0; k < 4; k++) protos.Add(new TreePrototype { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ENVPRE + "RipRapRock_" + (char)('A' + k) + ".prefab") });
        td.treePrototypes = protos.ToArray(); td.RefreshPrototypes();
        var list = new List<TreeInstance>(td.treeInstances); Vector3 tp = terrain.transform.position, size = td.size;
        var rnd = new System.Random(1717); float Fr() => (float)rnd.NextDouble(); int n0 = list.Count;
        foreach (var q in rockPts)
        {
            Vector2 p = new Vector2(q.x, q.y), nn = new Vector2(q.z, q.w), tt = new Vector2(-nn.y, nn.x);
            for (int j = 0; j < 2 + (Fr() < 0.5f ? 1 : 0); j++)
            {
                Vector2 w = p + nn * Mathf.Lerp(-0.7f, 1.4f, Mathf.Pow(Fr(), 0.8f)) + tt * (Fr() - 0.5f) * 1.5f;
                float sz = Fr() < 0.1f ? 0.7f + Fr() * 0.4f : 0.3f + Fr() * 0.4f;
                list.Add(new TreeInstance { position = new Vector3((w.x - tp.x) / size.x, 0, (w.y - tp.z) / size.z), heightScale = sz * (0.8f + Fr() * 0.4f), widthScale = sz, rotation = Fr() * 6.283f, prototypeIndex = first + rnd.Next(4), color = Color.white, lightmapColor = Color.white });
            }
        }
        td.SetTreeInstances(list.ToArray(), true); EditorUtility.SetDirty(td);
        Debug.Log($"[SkiView] lake shore: {rings.Count} rings, {verts.Count} verts, grass tris {tGrass.Count / 3}, rock tris {tRock.Count / 3}, riprap rocks {list.Count - n0}");
    }

    // ------------------------------------------------------------------ buildings (procedural, textured in world metres)
    /// <summary>Gable roof: two slopes (submesh 0, u along the ridge, v down the slope, metres / tile) + gable-end triangles
    /// (submesh 1, wall material). Base w (across) x d (along the ridge) at y = 0, ridge at y = rise; eaves overhang ov.</summary>
    static Mesh GableMesh(string name, float w, float d, float rise, float ov, float tile, float wallTile)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t0 = new List<int>(); var t1 = new List<int>();
        float hw = w / 2 + ov, hd = d / 2 + ov, drop = rise * ov / (w / 2);
        float slope = Mathf.Sqrt(hw * hw + (rise + drop) * (rise + drop));
        for (int s = -1; s <= 1; s += 2)
        {
            int k = v.Count;
            v.Add(new Vector3(s * hw, -drop, -hd)); v.Add(new Vector3(0, rise, -hd)); v.Add(new Vector3(0, rise, hd)); v.Add(new Vector3(s * hw, -drop, hd));
            uv.Add(new Vector2(-hd / tile, slope / tile)); uv.Add(new Vector2(-hd / tile, 0)); uv.Add(new Vector2(hd / tile, 0)); uv.Add(new Vector2(hd / tile, slope / tile));
            if (s > 0) { t0.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 }); } else { t0.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 }); }   // outward / up
            // underside (soffit) so the overhang isn't see-through from below
            int k2 = v.Count;
            v.Add(new Vector3(s * hw, -drop - 0.06f, -hd)); v.Add(new Vector3(0, rise - 0.06f, -hd)); v.Add(new Vector3(0, rise - 0.06f, hd)); v.Add(new Vector3(s * hw, -drop - 0.06f, hd));
            for (int i = 0; i < 4; i++) uv.Add(uv[k + i]);
            if (s > 0) { t1.AddRange(new[] { k2, k2 + 2, k2 + 1, k2, k2 + 3, k2 + 2 }); } else { t1.AddRange(new[] { k2, k2 + 1, k2 + 2, k2, k2 + 2, k2 + 3 }); }   // soffit faces down
        }
        for (int e = -1; e <= 1; e += 2)
        {
            int k = v.Count; float z = e * d / 2;
            v.Add(new Vector3(-w / 2, 0, z)); v.Add(new Vector3(0, rise, z)); v.Add(new Vector3(w / 2, 0, z));
            uv.Add(new Vector2(-w / 2 / wallTile, 0)); uv.Add(new Vector2(0, rise / wallTile)); uv.Add(new Vector2(w / 2 / wallTile, 0));
            if (e < 0) t1.AddRange(new[] { k, k + 1, k + 2 }); else t1.AddRange(new[] { k, k + 2, k + 1 });
        }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetUVs(0, uv); m.subMeshCount = 2; m.SetTriangles(t0, 0); m.SetTriangles(t1, 1);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        return LakeMesh(m, name);
    }

    static void Window(Transform p, Vector3 pos, float w, float h, Material glass, Material trim, float yaw)
    {
        var g = new GameObject("Window").transform; g.SetParent(p, false); g.localPosition = pos; g.localEulerAngles = new Vector3(0, yaw, 0);
        Box("Glass", g, new Vector3(0, 0, 0.02f), new Vector3(w, h, 0.04f), glass);
        Box("TrimT", g, new Vector3(0, h / 2 + 0.06f, 0.05f), new Vector3(w + 0.24f, 0.12f, 0.08f), trim);
        Box("TrimB", g, new Vector3(0, -h / 2 - 0.07f, 0.07f), new Vector3(w + 0.30f, 0.10f, 0.12f), trim);
        Box("TrimL", g, new Vector3(-w / 2 - 0.05f, 0, 0.05f), new Vector3(0.10f, h, 0.08f), trim);
        Box("TrimR", g, new Vector3(w / 2 + 0.05f, 0, 0.05f), new Vector3(0.10f, h, 0.08f), trim);
        Box("Mullion", g, new Vector3(0, 0, 0.045f), new Vector3(0.05f, h, 0.03f), trim);
        Box("Rail", g, new Vector3(0, 0.02f, 0.045f), new Vector3(w, 0.05f, 0.03f), trim);
    }

    /// <summary>Pass 17: merge each building's static pieces into one mesh per (material, shadow mode). Pieces under a LODGroup or
    /// with a mirrored transform are left alone; colliders and the GameObjects stay (captures and lookups still find them).</summary>
    static void MergeBuildings(Transform bRoot, string id)
    {
        if (bRoot == null) return;
        int before = 0, after = 0, bi = 0;
        foreach (Transform b in bRoot)
        {
            var groups = new Dictionary<(Material, UnityEngine.Rendering.ShadowCastingMode), List<CombineInstance>>();
            var done = new List<MeshRenderer>();
            foreach (var r in b.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || r.GetComponentInParent<LODGroup>() != null) continue;
                var m = b.worldToLocalMatrix * r.transform.localToWorldMatrix; if (m.determinant < 0f) continue;
                var mats = r.sharedMaterials; before += mats.Length;
                for (int s = 0; s < mf.sharedMesh.subMeshCount && s < mats.Length; s++)
                {
                    var key = (mats[s], r.shadowCastingMode);
                    if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<CombineInstance>();
                    l.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = m });
                }
                done.Add(r);
            }
            int gi = 0;
            foreach (var kv in groups)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = $"lake_{id}_bldg{bi}_{gi}" };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true); mesh.RecalculateBounds();
                var go = new GameObject("Merged_" + (kv.Key.Item1 != null ? kv.Key.Item1.name : "none")); go.transform.SetParent(b, false);
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, mesh.name);
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = kv.Key.Item1; mr.shadowCastingMode = kv.Key.Item2;
                gi++; after++;
            }
            foreach (var r in done) { var mf = r.GetComponent<MeshFilter>(); UnityEngine.Object.DestroyImmediate(r); if (mf != null) UnityEngine.Object.DestroyImmediate(mf); }
            bi++;
        }
        Debug.Log($"[SkiView] lake buildings merged: {before} -> {after} draw items over {bi} buildings");
    }

    static void LakeBuildings(LakeVenue v, Transform root, Terrain terrain)
    {
        var bRoot = new GameObject("Buildings").transform; bRoot.SetParent(root, false);
        var siding = EnvTex("woodsiding_color.jpg", false, 1024, false, 512); var sidingN = EnvTex("woodsiding_normal.png", true, 512, false, 256);
        var shingle = EnvTex("shingle_color.jpg", false, 1024, false, 512); var shingleN = EnvTex("shingle_normal.png", true, 512, false, 256);
        var rib = EnvTex("metalrib_color.png", false, 512, false, 256); var ribN = EnvTex("metalrib_normal.png", true, 512, false, 256);
        var glass = LakeFlat("WindowGlass", new Color(0.05f, 0.06f, 0.065f), 0.92f);
        var trim = LakeFlat("TrimWhite", new Color(0.93f, 0.93f, 0.91f), 0.35f);
        var concrete = LakeMat("Foundation", EnvTex("shore_color.jpg", false, 1024, false, 512), null, new Color(0.62f, 0.62f, 0.60f), 0.1f, new Vector2(0.5f, 0.5f));
        if (dockMat == null) DockMaterials();
        gulfBldgN = 0;
        foreach (var b in v.buildings)
        {
            var go = new GameObject("Bldg_" + b.kind).transform; go.SetParent(bRoot, false);
            float y = terrain.SampleHeight(new Vector3(b.x, 0, b.z)) + terrain.transform.position.y;
            go.position = new Vector3(b.x, b.kind == "moorage" || b.kind == "pier" || b.kind == "commdock" || b.kind == "boathouse" ? 0f : y, b.z); go.rotation = Quaternion.Euler(0, b.yaw, 0);   // local +z faces the water
            Color wallC = C3(b.wall), roofC = C3(b.roof);
            if (b.kind == "farmhouse")
            {
                // 1930s two-storey farmhouse: white lap siding, steep dark-grey shingle gable (ridge across the front), front porch
                var wall = LakeMat("FarmhouseSiding", siding, sidingN, wallC * new Color(1.06f, 1.10f, 1.34f), 0.18f, Vector2.one, 0.9f);   // the CC0 siding texture is cream (mean 0.79/0.76/0.62): neutralise to white paint
                var roof = LakeMat("FarmhouseShingle", shingle, shingleN, roofC * 2.2f, 0.12f, Vector2.one, 0.8f);
                float W_ = b.w, D_ = b.d, H_ = b.h;
                PlankBox("Foundation", go, new Vector3(0, 0.25f, 0), new Vector3(W_ + 0.2f, 0.9f, D_ + 0.2f), concrete, 2f);
                PlankBox("Walls", go, new Vector3(0, 0.6f + H_ / 2, 0), new Vector3(W_, H_, D_), wall, 2.4f);
                var rm = GableMesh("farmhouse_roof", D_, W_, 3.9f, 0.45f, 2.0f, 2.4f);
                var r = MeshObj("Roof", go, rm, roof, wall); r.transform.localPosition = new Vector3(0, 0.6f + H_, 0); r.transform.localEulerAngles = new Vector3(0, 90, 0);
                FarmhouseDetail(go, W_, D_, H_, 3.9f, 0.45f, wall, roof, trim, glass, concrete);   // pass 16
            }
            else if (b.kind == "barn" || b.kind == "shed")
            {
                bool barn = b.kind == "barn";
                var wall = barn ? LakeMat("BarnMetalWall", rib, ribN, wallC * 1.1f, 0.45f, Vector2.one, 1f, false, 0.25f) : LakeMat("ShedSiding", siding, sidingN, wallC, 0.18f, Vector2.one, 0.9f);
                var roof = LakeMat(barn ? "BarnMetalRoofBlue" : "ShedMetalRoof", rib, ribN, roofC * 1.15f, 0.5f, Vector2.one, 1f, false, 0.35f);
                float W_ = b.w, D_ = b.d, H_ = b.h;
                PlankBox("Slab", go, new Vector3(0, 0.05f, 0), new Vector3(W_ + 0.6f, 0.3f, D_ + 0.6f), concrete, 2f);
                PlankBox("Walls", go, new Vector3(0, 0.2f + H_ / 2, 0), new Vector3(W_, H_, D_), wall, 1.2f);
                var rm = GableMesh(barn ? "barn_roof" : "shed_roof", D_, W_, barn ? 2.2f : 1.6f, 0.35f, 1.2f, 1.2f);
                var r = MeshObj("Roof", go, rm, roof, wall); r.transform.localPosition = new Vector3(0, 0.2f + H_, 0); r.transform.localEulerAngles = new Vector3(0, 90, 0);
                var door = LakeFlat("RollUpDoor", new Color(0.88f, 0.88f, 0.86f), 0.4f);
                int nd = barn ? 2 : 1;
                for (int i = 0; i < nd; i++)
                {
                    float dx = nd == 1 ? 0 : (i - 0.5f) * W_ * 0.45f;
                    Box("Door" + i, go, new Vector3(dx, 0.2f + 1.9f, D_ / 2 + 0.03f), new Vector3(barn ? 4.0f : 2.4f, barn ? 3.8f : 2.2f, 0.06f), door);
                    for (int s = 0; s < (barn ? 8 : 5); s++) Box("DoorRib", go, new Vector3(dx, 0.2f + 0.3f + s * 0.45f, D_ / 2 + 0.065f), new Vector3(barn ? 4.0f : 2.4f, 0.03f, 0.02f), trim);
                }
                Window(go, new Vector3(W_ * 0.36f, 0.2f + 1.6f, D_ / 2), 1.2f, 1.0f, glass, trim, 0);
                BarnDetail(go, barn, W_, D_, H_, barn ? 2.2f : 1.6f, 0.35f, roofC, roof, trim, concrete);   // pass 16
                Box("ManDoor", go, new Vector3(-W_ * 0.38f, 0.2f + 1.05f, D_ / 2 + 0.03f), new Vector3(0.95f, 2.1f, 0.06f), trim);
            }
            else if (b.kind == "estate" || b.kind == "pier" || b.kind == "commdock" || b.kind == "tent" || b.kind == "tower" || b.kind == "scoreboard" || b.kind == "arch")
                GulfBuilding(b, go, terrain, glass, trim, concrete);   // pass 17 Gulf kit
            else if (b.kind == "boathouse") Boathouse(b, go, trim, glass);   // pass 18 Tall Pine
            else if (b.kind == "kit") { UnityEngine.Object.DestroyImmediate(go.gameObject); continue; }   // pass 19: built by KitBuildings
            else if (b.kind == "bridge" || b.kind == "grandstand" || b.kind == "breakwater" || b.kind == "spire") P19Building(b, go, concrete, trim);
            else if (b.kind == "moorage")
            {
                // timber dock along the slip (12 x 3 m), shed-roof boat cover over one slip with a lift under it
                go.position = new Vector3(b.x, 0, b.z);
                float L = b.w, Wd = b.d;
                PlankBox("Dock", go, new Vector3(-3.4f, 0.32f, 0), new Vector3(Wd, 0.16f, L), dockMat, 1.2f);
                PlankBox("Gangway", go, new Vector3(-3.4f, 0.4f, -L / 2 - 2.0f), new Vector3(1.4f, 0.12f, 4.0f), dockMat, 1.2f, new Vector3(-4f, 0, 0));
                for (int i = 0; i <= 4; i++) { float z = -L / 2 + i * L / 4; Prim(PrimitiveType.Cylinder, "Pile" + i, go, new Vector3(-4.95f, -0.2f, z), new Vector3(0.24f, 0.9f, 0.24f), pileMat); Prim(PrimitiveType.Cylinder, "PileB" + i, go, new Vector3(-1.85f, -0.2f, z), new Vector3(0.24f, 0.9f, 0.24f), pileMat); }
                var post = LakeMat("CoverPost", AssetDatabase.LoadAssetAtPath<Texture2D>(ENVTEX + "dock_color.jpg"), null, new Color(0.62f, 0.55f, 0.48f), 0.12f, new Vector2(0.3f, 1.5f));
                var roof = LakeMat("CoverRoofWhite", rib, ribN, C3(b.roof), 0.55f, Vector2.one, 1f, true, 0.2f);
                float cx = 0.6f, cz = 0.5f, cl = 9.0f, cw = 4.6f;
                for (int i = 0; i < 6; i++) { float px = cx + (i % 2 == 0 ? -cw / 2 : cw / 2), pz = cz + (i / 2 - 1) * cl / 2; float ph = i % 2 == 0 ? 3.6f : 3.1f; Box("CoverPost" + i, go, new Vector3(px, ph / 2 - 0.3f, pz), new Vector3(0.2f, ph + 0.6f, 0.2f), post); }
                var cr = PlankBox("CoverRoof", go, new Vector3(cx, 3.45f, cz), new Vector3(cw + 0.9f, 0.08f, cl + 0.8f), roof, 1.2f, new Vector3(0, 0, -6.5f));
                cr.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                Box("CoverBeamF", go, new Vector3(cx, 3.2f, cz - cl / 2), new Vector3(cw + 0.3f, 0.22f, 0.16f), post, new Vector3(0, 0, -6.5f));
                Box("CoverBeamB", go, new Vector3(cx, 3.2f, cz + cl / 2), new Vector3(cw + 0.3f, 0.22f, 0.16f), post, new Vector3(0, 0, -6.5f));
                for (int i = 0; i < 4; i++) { float x = cx + (i % 2 == 0 ? -1.3f : 1.3f), z = cz + (i < 2 ? -2.9f : 2.9f); Prim(PrimitiveType.Cylinder, "LiftPost" + i, go, new Vector3(x, 0.9f, z), new Vector3(0.11f, 1.3f, 0.11f), frameMat); }
                Box("LiftBunkL", go, new Vector3(cx - 0.7f, 0.06f, cz), new Vector3(0.16f, 0.12f, 6.0f), frameMat);
                Box("LiftBunkR", go, new Vector3(cx + 0.7f, 0.06f, cz), new Vector3(0.16f, 0.12f, 6.0f), frameMat);
                Box("LiftWheel", go, new Vector3(cx + 1.3f, 2.1f, cz - 2.9f), new Vector3(0.06f, 0.7f, 0.7f), frameMat);
            }
        }
        Debug.Log($"[SkiView] lake buildings: {v.buildings.Length} ({string.Join(", ", v.buildings.Select(b => b.kind))})");
    }
}
