using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using SkiView;

/// <summary>Slalom rider kit built from code (see Docs/SLALOM-SKIER-LOOK.md): 67" slalom ski with rocker, tunnel,
/// bevels, tapered thickness, fin + wing; double high-wrap boots; a segmented competition vest fitted to the rider's
/// torso; board-short / rash-guard reskins. All dimensions in metres.</summary>
public static class SkiViewRiderKit
{
    const string MESH = "Assets/SkiView/Meshes/";
    const string MAT = "Assets/SkiView/Materials/";
    const string MIX = "Assets/SkiView/ThirdParty/Mixamo/";

    // ---- ski geometry (67" Radar/HO/D3 class)
    public const float L = 1.702f;           // 67 in
    const float MAX_HALF = 0.0884f;          // 6.96 in max width
    const float TAIL_HALF = 0.0712f;         // ~5.6 in tail
    public const float FRONT_HEEL = 0.762f;  // front boot heel 30 in from the tail
    public const float BOOT_LEN = 0.30f;
    public const float FRONT_ANKLE = FRONT_HEEL + 0.065f;
    public const float REAR_ANKLE = FRONT_HEEL - 0.012f - BOOT_LEN + 0.065f;   // rear boot right behind, ~12 in ankle to ankle
    public const float REAR_YAW = 12f;

    static float Smooth(float a, float b, float t) { t = Mathf.Clamp01((t - a) / (b - a)); return t * t * (3 - 2 * t); }
    public static float HalfW(float t)
    {
        float hw;
        if (t <= 0.62f) hw = Mathf.Lerp(TAIL_HALF, MAX_HALF, Mathf.Sin(Mathf.PI * 0.5f * t / 0.62f));
        else if (t <= 0.82f) hw = Mathf.Lerp(MAX_HALF, 0.0835f, (t - 0.62f) / 0.20f);
        else { float u = (t - 0.82f) / 0.18f; hw = 0.0835f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(u, 2.2f))); }
        float z = t * L, r = 0.012f;                         // rounded square tail corners
        if (z < r) hw -= r - Mathf.Sqrt(Mathf.Max(0f, r * r - (r - z) * (r - z)));
        return Mathf.Max(hw, 0.004f);
    }
    public static float Rocker(float t) => 0.006f * Mathf.Pow(Mathf.Max(0f, 1f - t / 0.06f), 2f) + 0.062f * Mathf.Pow(Mathf.Max(0f, (t - 0.55f) / 0.45f), 2.2f);
    public static float Thick(float t) => 0.009f + 0.009f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.01f) / 0.95f))), 0.6f);
    public static float TopY(float t) => Rocker(t) + Thick(t);

    /// <summary>Ski mesh: origin at the tail, centre of the base; +Z toward the tip. Submesh 0 = top sheet + sidewalls, 1 = base.</summary>
    public static Mesh SkiMesh()
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tTop = new List<int>(); var tBot = new List<int>();
        int NS = 72;
        var ts = new List<float>();
        for (int i = 0; i <= NS; i++) { float s = (float)i / NS; ts.Add(s < 0.8f ? s * 0.8f / 0.8f : s); }
        // denser near the tip
        ts.Clear(); for (int i = 0; i <= NS; i++) { float s = (float)i / NS; ts.Add(1f - Mathf.Pow(1f - s, 1.35f)); }
        const int NA = 10;   // across samples for the top
        List<Vector2> TopLoop(float t)
        {
            float hw = HalfW(t), yb = Rocker(t), th = Thick(t), bev = Mathf.Min(0.008f, hw * 0.3f), rr = Mathf.Min(0.006f, th * 0.45f);
            var p = new List<Vector2>();
            p.Add(new Vector2(hw, yb + bev * 0.6f));                         // sidewall from the bevel up
            for (int k = 0; k <= 3; k++) { float a = k / 3f * Mathf.PI * 0.5f; p.Add(new Vector2(hw - rr + rr * Mathf.Cos(a), yb + th - rr + rr * Mathf.Sin(a))); }
            for (int k = 1; k < NA; k++) { float x = Mathf.Lerp(hw - rr, -(hw - rr), (float)k / NA); p.Add(new Vector2(x, yb + th + 0.0015f * (1 - Mathf.Pow(x / hw, 2)))); }
            for (int k = 0; k <= 3; k++) { float a = k / 3f * Mathf.PI * 0.5f; p.Add(new Vector2(-(hw - rr) - rr * Mathf.Sin(a), yb + th - rr + rr * Mathf.Cos(a))); }
            p.Add(new Vector2(-hw, yb + bev * 0.6f));
            return p;
        }
        List<Vector2> BotLoop(float t)
        {
            float hw = HalfW(t), yb = Rocker(t), bev = Mathf.Min(0.008f, hw * 0.3f);
            float tun = 0.005f * (1f - Smooth(0.70f, 0.86f, t)) * Smooth(0.0f, 0.04f, t), th2 = hw * 0.55f;
            var p = new List<Vector2>();
            p.Add(new Vector2(-hw, yb + bev * 0.6f)); p.Add(new Vector2(-hw + bev, yb));
            for (int k = 0; k <= 8; k++) { float x = Mathf.Lerp(-th2, th2, k / 8f); float u = x / th2; p.Add(new Vector2(x, yb + tun * (1 - u * u))); }
            p.Add(new Vector2(hw - bev, yb)); p.Add(new Vector2(hw, yb + bev * 0.6f));
            return p;
        }
        void Strip(System.Func<float, List<Vector2>> loop, List<int> tri, bool top)
        {
            int start = v.Count, n = -1;
            foreach (var t in ts)
            {
                var p = loop(t); n = p.Count; float hw = HalfW(t);
                foreach (var q in p) { v.Add(new Vector3(q.x, q.y, t * L)); uv.Add(top ? new Vector2(t, 0.5f + 0.5f * q.x / Mathf.Max(hw, 1e-4f)) : new Vector2(t, q.x)); }
            }
            for (int i = 0; i < ts.Count - 1; i++)
                for (int k = 0; k < n - 1; k++)
                {
                    int a = start + i * n + k, b = a + 1, a2 = a + n, b2 = b + n;
                    tri.AddRange(new[] { a, b, b2, a, b2, a2 });
                }
        }
        Strip(TopLoop, tTop, true);
        Strip(BotLoop, tBot, false);
        // tail cap (top material)
        {
            var tl = TopLoop(0f); var bl = BotLoop(0f); var loop = new List<Vector2>(tl); for (int k = 1; k < bl.Count - 1; k++) loop.Add(bl[k]);
            int c = v.Count; v.Add(new Vector3(0, Rocker(0) + Thick(0) * 0.5f, 0)); uv.Add(new Vector2(0, 0.5f));
            int s0 = v.Count; foreach (var q in loop) { v.Add(new Vector3(q.x, q.y, 0)); uv.Add(new Vector2(0, 0.5f)); }
            for (int k = 0; k < loop.Count; k++) { int a = s0 + k, b = s0 + (k + 1) % loop.Count; tTop.AddRange(new[] { c, b, a }); }
        }
        var m = new Mesh { name = "slalom_ski_67in" };
        m.SetVertices(v); m.SetUVs(0, uv); m.subMeshCount = 2; m.SetTriangles(tTop, 0); m.SetTriangles(tBot, 1);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    /// <summary>Lofted shell: stations (z, halfWidth, yBottom, yTop), superellipse top half + flat sole.</summary>
    static Mesh Loft(string name, List<Vector4> st, int nA = 14, float pw = 3f)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        int n = nA + 1;
        foreach (var s in st)
            for (int k = 0; k <= nA; k++)
            {
                float a = Mathf.PI * k / nA;   // 0 = +x side, pi = -x side, over the top
                float c = Mathf.Cos(a), sn = Mathf.Max(0f, Mathf.Sin(a));
                float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / pw) * s.y;
                float y = s.z + Mathf.Pow(sn, 2f / pw) * (s.w - s.z);
                v.Add(new Vector3(x, y, s.x));
            }
        for (int i = 0; i < st.Count - 1; i++)
            for (int k = 0; k < nA; k++)
            {
                int a = i * n + k, b = a + 1, a2 = a + n, b2 = b + n;
                tri.AddRange(new[] { a, b2, a2, a, b, b2 });
            }
        // end caps
        foreach (int i in new[] { 0, st.Count - 1 })
        {
            int c = v.Count; v.Add(new Vector3(0, (st[i].z + st[i].w) * 0.5f, st[i].x));
            int r0 = v.Count; for (int k = 0; k <= nA; k++) v.Add(v[i * n + k]);
            for (int k = 0; k < nA; k++) { int a = r0 + k, b = a + 1; if (i == 0) tri.AddRange(new[] { c, b, a }); else tri.AddRange(new[] { c, a, b }); }
        }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Mesh Save(Mesh m, string n)
    {
        string p = MESH + n + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(p) != null) AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p); return m;
    }
    static Material Mat(string name, Color c, float smooth, Texture tex = null, bool twoSided = false)
    {
        string p = MAT + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
        m.SetTexture("_BaseMap", tex);
        if (twoSided) m.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(m); return m;
    }
    static GameObject Obj(string name, Transform parent, Mesh mesh, params Material[] mats)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterials = mats;
        return g;
    }
    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, Vector3 euler = default)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = size; g.transform.localEulerAngles = euler;
        g.GetComponent<Renderer>().sharedMaterial = m; Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
    }

    /// <summary>Ski + fin + wing + double high-wrap boots. Returns the ski root (origin at the tail).</summary>
    public static Transform BuildSki(Transform parent)
    {
        string topPath = "Assets/SkiView/Generated/ski_topsheet.png";
        if (AssetImporter.GetAtPath(topPath) is TextureImporter ti && (ti.wrapMode != TextureWrapMode.Clamp || ti.anisoLevel < 8)) { ti.wrapMode = TextureWrapMode.Clamp; ti.anisoLevel = 8; ti.maxTextureSize = 2048; ti.SaveAndReimport(); }
        var top = Mat("SkiTopSheet", Color.white, 0.50f, AssetDatabase.LoadAssetAtPath<Texture2D>(topPath), true);
        var bas = Mat("SkiBase", new Color(0.16f, 0.17f, 0.19f), 0.7f, null, true);
        var fin = Mat("SkiFinAlu", new Color(0.62f, 0.64f, 0.67f), 0.75f); fin.SetFloat("_Metallic", 0.9f);
        var shell = Mat("BootShell", new Color(0.07f, 0.07f, 0.08f), 0.30f);   // rubber
        var liner = Mat("BootLiner", new Color(0.16f, 0.17f, 0.19f), 0.25f);
        var strap = Mat("BootStrapTeal", new Color(0.05f, 0.47f, 0.50f), 0.4f);
        var plate = Mat("BindingPlate", new Color(0.10f, 0.11f, 0.12f), 0.35f);

        var ski = new GameObject("SlalomSki_67in").transform; ski.SetParent(parent, false);
        var sm = Save(SkiMesh(), "slalom_ski_67in");
        var sg = Obj("SkiBody", ski, sm, top, bas); sg.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        // fin (6.9" x 2.47", leading edge raked) and wing at 8 deg
        var finM = new Mesh { name = "ski_fin" };
        {
            float t = 0.0022f; var p = new[] { new Vector2(0.020f, 0f), new Vector2(0.195f, 0f), new Vector2(0.168f, -0.063f), new Vector2(0.032f, -0.063f) };
            var vv = new List<Vector3>(); var tt = new List<int>();
            foreach (var q in p) vv.Add(new Vector3(t, q.y, q.x)); foreach (var q in p) vv.Add(new Vector3(-t, q.y, q.x));
            tt.AddRange(new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 });
            for (int k = 0; k < 4; k++) { int a = k, b = (k + 1) % 4; tt.AddRange(new[] { a, a + 4, b + 4, a, b + 4, b }); }
            finM.SetVertices(vv); finM.SetTriangles(tt, 0); finM.RecalculateNormals(); finM.RecalculateBounds();
        }
        Obj("Fin", ski, Save(finM, "ski_fin"), fin).transform.localPosition = new Vector3(0, Rocker(0.08f) + 0.002f, 0);
        Box("FinWing", ski, new Vector3(0, -0.052f, 0.075f), new Vector3(0.14f, 0.003f, 0.032f), fin, new Vector3(8f, 0, 0));
        Box("FinBlock", ski, new Vector3(0, Rocker(0.1f) - 0.001f, 0.14f), new Vector3(0.022f, 0.006f, 0.20f), fin);

        // boots: lofted high-wrap shell, liner collar, teal power strap, binding plate
        // pass 3: low-profile rubber high-wrap that hugs the foot and ankle (was a ski-boot block)
        Mesh bootM = Save(Loft("hiwrap_boot", new List<Vector4> {
            new Vector4(-0.012f, 0.030f, 0.0f, 0.170f), new Vector4(0.010f, 0.036f, 0.0f, 0.198f), new Vector4(0.055f, 0.040f, 0.0f, 0.205f),
            new Vector4(0.100f, 0.042f, 0.0f, 0.172f), new Vector4(0.150f, 0.046f, 0.0f, 0.104f), new Vector4(0.200f, 0.048f, 0.0f, 0.070f),
            new Vector4(0.240f, 0.046f, 0.0f, 0.056f), new Vector4(0.270f, 0.037f, 0.0f, 0.047f), new Vector4(0.288f, 0.020f, 0.0f, 0.035f) }, 16, 2.4f), "hiwrap_boot");
        Mesh collarM = Save(Loft("hiwrap_collar", new List<Vector4> {
            new Vector4(0.000f, 0.033f, 0.168f, 0.196f), new Vector4(0.050f, 0.043f, 0.190f, 0.214f), new Vector4(0.095f, 0.043f, 0.160f, 0.182f) }, 10, 2.2f), "hiwrap_collar");
        foreach (var (nm, ankle, yaw) in new[] { ("FrontBoot", FRONT_ANKLE, 0f), ("RearBoot", REAR_ANKLE, REAR_YAW) })
        {
            float heel = ankle - 0.065f, t = (heel + 0.15f) / L;
            var b = new GameObject(nm).transform; b.SetParent(ski, false);
            b.localPosition = new Vector3(0, TopY(t), heel); b.localEulerAngles = new Vector3(0, yaw, 0);
            Box("Plate", b, new Vector3(0, 0.004f, 0.14f), new Vector3(0.098f, 0.008f, 0.31f), plate);
            Obj("Shell", b, bootM, shell).transform.localPosition = new Vector3(0, 0.010f, 0);
            Obj("Collar", b, collarM, liner).transform.localPosition = new Vector3(0, 0.010f, 0);
            Box("PowerStrap", b, new Vector3(0, 0.168f, 0.105f), new Vector3(0.090f, 0.026f, 0.010f), strap, new Vector3(-35, 0, 0));
            Box("Laces", b, new Vector3(0, 0.113f, 0.160f), new Vector3(0.040f, 0.008f, 0.070f), liner, new Vector3(-30, 0, 0));
        }
        return ski;
    }

    /// <summary>Recolour Remy into slalom gear: board shorts, dark rash guard, sneakers hidden (feet live in the boots).</summary>
    public static void Reskin(GameObject rider)
    {
        foreach (var f in new[] { "boardshorts.png", "rashguard.png" })
            if (AssetImporter.GetAtPath(MIX + "Derived/" + f) is TextureImporter ti && ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
        var shorts = AssetDatabase.LoadAssetAtPath<Texture2D>(MIX + "Derived/boardshorts.png");
        var rash = AssetDatabase.LoadAssetAtPath<Texture2D>(MIX + "Derived/rashguard.png");
        foreach (var r in rider.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials; bool hide = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (m == null) continue;
                string tn = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : m.name;
                if (tn.Contains("Shoes")) hide = true;
                else if (tn.Contains("Bottom") && shorts != null) { var n = new Material(m) { name = "Rider_BoardShorts" }; n.SetTexture("_BaseMap", shorts); n.SetTexture("_BumpMap", null); n.DisableKeyword("_NORMALMAP"); n.SetFloat("_Smoothness", 0.45f); n.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.88f)); mats[i] = SaveMat(n); }   // wet fabric: darker, a little glossier
                else if (tn.Contains("Top") && rash != null) { var n = new Material(m) { name = "Rider_RashGuard" }; n.SetTexture("_BaseMap", rash); n.SetFloat("_Smoothness", 0.55f); n.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.88f)); mats[i] = SaveMat(n); }
                else if (tn.Contains("Body") && !r.name.Contains("Eye")) mats[i] = WetSkin(m);
                else if (tn.Contains("Hair")) mats[i] = WetHair(m);
            }
            r.sharedMaterials = mats;
            // pass 7: Remy's separate eyeball/eyelash meshes skin badly once the head is levelled toward the boat (one eye swung
            // out to the temple in the new face close-up); the face texture already carries the eyes, so these two are hidden
            if (r.name == "Eyes" || r.name == "Eyelashes") hide = true;
            if (hide) r.enabled = false;
            Debug.Log($"[SkiView] rider renderer {r.name}: {(hide ? "hidden" : string.Join(",", System.Array.ConvertAll(mats, x => x ? x.name : "null")))}");
        }
    }
    static Texture2D Derived(string f, bool linear, bool alpha)
    {
        string p = MIX + "Derived/" + f;
        if (AssetImporter.GetAtPath(p) is TextureImporter ti)
        {
            bool d = false;
            if (ti.sRGBTexture == linear) { ti.sRGBTexture = !linear; d = true; }
            if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; d = true; }
            if (f.EndsWith("_n.png") && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; d = true; }
            if (alpha && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.35f; d = true; }
            if (d) ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    /// <summary>Pass 7 wet skin (Tools/gen_rider_wet.py): smoothness from a gloss-map + droplet-speckle map instead of one uniform
    /// value (the uniform highlight is what read as plastic), slightly darker albedo, and a faint warm emission from the albedo
    /// that stands in for subsurface scattering (warm, soft shadow side instead of grey).</summary>
    static Material WetSkin(Material src)
    {
        var n = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Rider_SkinWet" };
        var baseMap = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
        n.SetTexture("_BaseMap", baseMap); n.SetColor("_BaseColor", new Color(0.93f, 0.91f, 0.90f));
        var bump = src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
        if (bump != null) { n.SetTexture("_BumpMap", bump); n.SetFloat("_BumpScale", 0.8f); n.EnableKeyword("_NORMALMAP"); }
        var ms = Derived("skin_wet_ms.png", true, false);
        n.SetFloat("_WorkflowMode", 1f); n.SetFloat("_Metallic", 0f);
        if (ms != null) { n.SetTexture("_MetallicGlossMap", ms); n.EnableKeyword("_METALLICSPECGLOSSMAP"); n.SetFloat("_Smoothness", 1f); }
        else n.SetFloat("_Smoothness", 0.5f);
        n.SetFloat("_SmoothnessTextureChannel", 0f); n.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        n.SetTexture("_EmissionMap", baseMap); n.SetColor("_EmissionColor", new Color(0.055f, 0.022f, 0.014f)); n.EnableKeyword("_EMISSION");
        n.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return SaveMat(n);
    }

    /// <summary>Pass 7 wet hair: ~40% darker, clumped strands, alpha-clipped cards (two-sided), strand-streak smoothness.</summary>
    static Material WetHair(Material src)
    {
        var n = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Rider_HairWet" };
        var alb = Derived("hair_wet.png", false, true);
        n.SetTexture("_BaseMap", alb != null ? alb : src.GetTexture("_BaseMap")); n.SetColor("_BaseColor", new Color(0.86f, 0.74f, 0.60f));   // warm it back toward wet dark-blond (read as grey)
        // pass 8: strand normal map (Tools/gen_rider_wet.py) breaks the single smooth "helmet" highlight into strands and clumps
        var strandN = Derived("hair_wet_n.png", true, false);
        var bump = strandN != null ? strandN : (src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null);
        if (bump != null) { n.SetTexture("_BumpMap", bump); n.SetFloat("_BumpScale", 1.15f); n.EnableKeyword("_NORMALMAP"); }
        var ms = Derived("hair_wet_ms.png", true, false);
        n.SetFloat("_WorkflowMode", 1f); n.SetFloat("_Metallic", 0f);
        if (ms != null) { n.SetTexture("_MetallicGlossMap", ms); n.EnableKeyword("_METALLICSPECGLOSSMAP"); n.SetFloat("_Smoothness", 0.8f); }
        else n.SetFloat("_Smoothness", 0.6f);
        n.SetFloat("_AlphaClip", 1f); n.SetFloat("_Cutoff", 0.35f); n.EnableKeyword("_ALPHATEST_ON");
        n.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; n.SetOverrideTag("RenderType", "TransparentCutout");
        n.SetFloat("_Cull", 0f); n.doubleSidedGI = true;
        return SaveMat(n);
    }

    static Material SaveMat(Material m)
    {
        string p = MAT + m.name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(p) != null) AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p); return m;
    }

    /// <summary>Segmented competition vest fitted to the rider's T-pose torso (sliced from the baked skin).
    /// Rows parented to Spine and Chest so the panels articulate like the real siped foam.</summary>
    public static void BuildVest(GameObject rider, Animator anim, Transform skierRoot)
    {
        // pass 7: the vest is wet - slightly darker and glossier, and the lower row (which takes the most spray) darker still
        var navy = Mat("VestNavy", new Color(0.035f, 0.115f, 0.35f), 0.50f);
        var slate = Mat("VestSlate", new Color(0.26f, 0.30f, 0.36f), 0.48f);
        var navyWet = Mat("VestNavyWet", new Color(0.027f, 0.090f, 0.28f), 0.60f);
        var slateWet = Mat("VestSlateWet", new Color(0.21f, 0.24f, 0.29f), 0.56f);
        var black = Mat("VestStrap", new Color(0.03f, 0.03f, 0.035f), 0.3f);
        var buckle = Mat("VestBuckle", new Color(0.55f, 0.57f, 0.6f), 0.7f); buckle.SetFloat("_Metallic", 0.8f);
        var hips = anim.GetBoneTransform(HumanBodyBones.Hips); var spine = anim.GetBoneTransform(HumanBodyBones.Spine);
        var chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest) ?? spine;
        var lArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (hips == null || spine == null || lArm == null) { Debug.LogWarning("[SkiView] vest: missing bones"); return; }
        // Fitted ellipse per slice from the skeleton (robust; the baked-skin slicing picked up stray verts).
        // Remy at 1.80 m: chest ~0.36 m wide x 0.25 m deep, waist ~0.31 x 0.23, plus a 1 cm gap for the rash guard.
        Vector3 hipL = skierRoot.InverseTransformPoint(hips.position), armL = skierRoot.InverseTransformPoint(lArm.position);
        var rArmT = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Vector3 armR = rArmT != null ? skierRoot.InverseTransformPoint(rArmT.position) : new Vector3(-armL.x, armL.y, armL.z);
        float shoulderHalf = Mathf.Abs(armL.x - armR.x) * 0.5f;
        float yH = hipL.y, yA = armL.y, scale = Mathf.Clamp(yA / 1.45f, 0.8f, 1.25f);
        float y0 = yH + 0.05f * scale, y1 = yA - 0.075f * scale;
        const int NSL = 8, NANG = 40;
        var cen = new Vector2[NSL]; var rad = new float[NSL, NANG]; var ys = new float[NSL];
        for (int s = 0; s < NSL; s++)
        {
            float u = s / (NSL - 1f), y = Mathf.Lerp(y0, y1, u); ys[s] = y;
            var sp = Vector3.Lerp(hipL, (armL + armR) * 0.5f, Mathf.InverseLerp(yH, yA, y));
            cen[s] = new Vector2(sp.x, sp.z + 0.005f);
            float hw = Mathf.Lerp(0.160f * scale, Mathf.Max(0.170f * scale, shoulderHalf * 0.98f), Mathf.SmoothStep(0, 1, u)) + 0.012f;
            float hdF = Mathf.Lerp(0.115f, 0.135f, u) * scale + 0.012f, hdB = Mathf.Lerp(0.110f, 0.120f, u) * scale + 0.012f;
            for (int a = 0; a < NANG; a++)
            {
                float ang = a * 2 * Mathf.PI / NANG, sx = Mathf.Sin(ang), cz = Mathf.Cos(ang);
                float hd = cz >= 0 ? hdF : hdB;
                rad[s, a] = 1f / Mathf.Sqrt((sx * sx) / (hw * hw) + (cz * cz) / (hd * hd));
            }
        }
        var pts = new List<Vector3>();
        float R(int s, float ang) { float f = (ang / (2 * Mathf.PI)) * NANG; int i0 = Mathf.FloorToInt(f); float u = f - i0; return Mathf.Lerp(rad[s, ((i0 % NANG) + NANG) % NANG], rad[s, (((i0 + 1) % NANG) + NANG) % NANG], u); }
        Vector3 P(int s, float ang, float off) { float r = R(s, ang) + off; return new Vector3(cen[s].x + Mathf.Sin(ang) * r, ys[s], cen[s].y + Mathf.Cos(ang) * r); }

        var vestRoot = new GameObject("CompVest").transform; vestRoot.SetParent(skierRoot, false);
        const int NP = 10; float gapA = 2.2f * Mathf.Deg2Rad;
        int[][] rows = { new[] { 0, 3 }, new[] { 4, NSL - 1 } };
        for (int ri = 0; ri < 2; ri++)
        {
            var row = new GameObject(ri == 0 ? "VestLowerRow" : "VestUpperRow").transform; row.SetParent(vestRoot, false);
            for (int pi = 0; pi < NP; pi++)
            {
                float a0 = pi * 2 * Mathf.PI / NP + gapA, a1 = (pi + 1) * 2 * Mathf.PI / NP - gapA, ac = (a0 + a1) * 0.5f;
                bool side = Mathf.Abs(Mathf.Sin(ac)) > 0.93f;
                var v = new List<Vector3>(); var tri = new List<int>(); int NA = 6, s0 = rows[ri][0], s1 = rows[ri][1], NV = s1 - s0 + 1;
                float yLo = ri == 0 ? 0f : 0.006f, yHi = ri == 0 ? -0.006f : 0f;
                for (int layer = 0; layer < 2; layer++)
                    for (int s = s0; s <= s1; s++)
                        for (int k = 0; k <= NA; k++)
                        {
                            float u = k / (float)NA, w = (s - s0) / Mathf.Max(1f, NV - 1f), ang = Mathf.Lerp(a0, a1, u);
                            float pillow = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.4f) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 0.92f, w))), 0.4f);
                            var p = P(s, ang, layer == 0 ? 0.010f + 0.020f * pillow : 0.006f);
                            p.y += Mathf.Lerp(yLo, yHi, w);
                            v.Add(p);
                        }
                int per = NV * (NA + 1);
                for (int layer = 0; layer < 2; layer++)
                    for (int s = 0; s < NV - 1; s++)
                        for (int k = 0; k < NA; k++)
                        {
                            int a = layer * per + s * (NA + 1) + k, b = a + 1, c = a + NA + 1, d = c + 1;
                            if (layer == 0) tri.AddRange(new[] { a, b, d, a, d, c }); else tri.AddRange(new[] { a, d, b, a, c, d });
                        }
                // rim walls (outer to inner) all round the panel
                var rim = new List<int>();
                for (int k = 0; k <= NA; k++) rim.Add(k);
                for (int s = 1; s < NV; s++) rim.Add(s * (NA + 1) + NA);
                for (int k = NA - 1; k >= 0; k--) rim.Add((NV - 1) * (NA + 1) + k);
                for (int s = NV - 2; s > 0; s--) rim.Add(s * (NA + 1));
                var triRim = new List<int>();
                for (int i = 0; i < rim.Count; i++)
                {
                    int a = rim[i], b = rim[(i + 1) % rim.Count], q = v.Count;
                    v.Add(v[a]); v.Add(v[a + per]); v.Add(v[b + per]); v.Add(v[b]);
                    triRim.AddRange(new[] { q, q + 1, q + 2, q, q + 2, q + 3 });
                }
                var m = new Mesh { name = $"vest_r{ri}_p{pi}" }; m.SetVertices(v); m.subMeshCount = 2; m.SetTriangles(tri, 0); m.SetTriangles(triRim, 1); m.RecalculateNormals(); m.RecalculateBounds();
                var g = Obj(m.name, row, Save(m, m.name), ri == 0 ? (side ? slateWet : navyWet) : (side ? slate : navy), black);
                black.SetFloat("_Cull", 0f);
            }
            {   // continuous neoprene shell under the siped foam panels (what shows in the panel gaps)
                int s0 = rows[ri][0], s1 = rows[ri][1], NA2 = 48; var bv = new List<Vector3>(); var bt = new List<int>();
                for (int sI = s0; sI <= s1; sI++) for (int kk = 0; kk <= NA2; kk++) bv.Add(P(sI, kk * 2 * Mathf.PI / NA2 % (2 * Mathf.PI), 0.009f));
                for (int sI = 0; sI < s1 - s0; sI++) for (int kk = 0; kk < NA2; kk++)
                    { int q = sI * (NA2 + 1) + kk, q2 = q + NA2 + 1; bt.AddRange(new[] { q, q2, q2 + 1, q, q2 + 1, q + 1 }); }
                var bm = new Mesh { name = $"vest_base_r{ri}" }; bm.SetVertices(bv); bm.SetTriangles(bt, 0); bm.RecalculateNormals(); bm.RecalculateBounds();
                var neo = Mat("VestNeoprene", new Color(0.025f, 0.04f, 0.10f), 0.45f, null, true);
                Obj(bm.name, row, Save(bm, bm.name), neo);
            }
            row.SetParent(ri == 0 ? spine : chest, true);
        }
        // front zip, waist strap with buckle, shoulder straps
        var zip = Box("VestZip", vestRoot, P(NSL / 2, 0f, 0.016f), new Vector3(0.014f, ys[NSL - 1] - ys[0], 0.008f), black);
        zip.transform.SetParent(chest, true);
        int sw = 1;
        var strap = new GameObject("VestWaistStrap").transform; strap.SetParent(vestRoot, false);
        for (int k = 0; k < 24; k++)
        {
            float a0 = k * 2 * Mathf.PI / 24, a1 = (k + 1) * 2 * Mathf.PI / 24;
            Vector3 p0 = P(sw, a0, 0.034f), p1 = P(sw, a1, 0.034f);
            var seg = Box("s" + k, strap, (p0 + p1) * 0.5f, new Vector3(Vector3.Distance(p0, p1) + 0.004f, 0.032f, 0.006f), black);
            seg.transform.rotation = skierRoot.rotation * Quaternion.LookRotation(Vector3.Cross(p1 - p0, Vector3.up).normalized * -1f);
        }
        Box("Buckle", strap, P(sw, 0f, 0.042f), new Vector3(0.05f, 0.036f, 0.012f), buckle);
        strap.SetParent(spine, true);
        foreach (int sd in new[] { -1, 1 })
        {
            float af = sd * 22f * Mathf.Deg2Rad, ab = sd * 158f * Mathf.Deg2Rad;
            Vector3 F = P(NSL - 1, (af + 2 * Mathf.PI) % (2 * Mathf.PI), 0.02f), B = P(NSL - 1, (ab + 2 * Mathf.PI) % (2 * Mathf.PI), 0.02f);
            Vector3 top = new Vector3((F.x + B.x) * 0.5f, yA + 0.05f, (F.z + B.z) * 0.5f);
            Vector3 prev = F;
            for (int k = 1; k <= 8; k++)
            {
                float t = k / 8f; Vector3 p = (1 - t) * (1 - t) * F + 2 * (1 - t) * t * top + t * t * B;
                var seg = Box($"ShoulderStrap{sd}_{k}", vestRoot, (prev + p) * 0.5f, new Vector3(0.055f, 0.016f, Vector3.Distance(prev, p) + 0.006f), navy);
                seg.transform.rotation = skierRoot.rotation * Quaternion.LookRotation((p - prev).normalized, Vector3.up);
                seg.transform.SetParent(chest, true);
                prev = p;
            }
        }
        // cleanup: everything except moved parts was re-parented; drop the empty root
        if (vestRoot.childCount == 0) Object.DestroyImmediate(vestRoot.gameObject);
        foreach (var r in skierRoot.GetComponentsInChildren<MeshRenderer>()) if (r.name.StartsWith("vest_") || r.name.StartsWith("ShoulderStrap") || r.name.StartsWith("s") && r.transform.parent.name == "VestWaistStrap") r.shadowCastingMode = ShadowCastingMode.On;
        Debug.Log($"[SkiView] vest: y {y0:0.00}-{y1:0.00}, shoulderHalf {shoulderHalf:0.000}, r front {R(3, 0):0.000} side {R(3, Mathf.PI / 2):0.000}");
    }
}
