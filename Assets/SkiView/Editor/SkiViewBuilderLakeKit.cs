using System;
using System.Collections.Generic;
using System.Linq;
using SkiView;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pass 16: PNW lake kit. Extra plant prototypes (black cottonwood, sword fern, blackberry, unmowed grass, reeds),
/// the layered foothill / Cascade backdrop with atmospheric haze, and the detailed farmhouse / barn parts.
/// Prefabs and materials are shared kit (Environment/Prefabs, Environment/Materials); nothing of the home lake is replaced.</summary>
public static partial class SkiViewBuilder
{
    // prototype order appended after the 9 shared tree prototypes
    public const int P_COTTONWOOD = 9, P_FERN = 10, P_BLACKBERRY = 11, P_TALLGRASS = 12, P_REEDS = 13;

    static GameObject[] LakePlantKit()
    {
        var bark = AssetDatabase.LoadAssetAtPath<Material>(ENVMAT + "Bark.mat");
        var cwLeaves = EnvMat("CottonwoodLeaves", EnvTex("alder_leaves.png", false, 512, true, 256), null, new Color(0.80f, 0.88f, 0.66f), 0.2f, Vector2.one, true, 0.45f, true, spec: false);
        var cwImp = EnvMat("CottonwoodImpostor", EnvTex("alder_impostor.png", false, 512, true, 256), null, new Color(0.82f, 0.90f, 0.70f), 0.08f, Vector2.one, true, 0.45f, true, spec: false);
        var cottonwood = DeciduousPrefab("BlackCottonwood", false, bark, cwLeaves, cwImp, 909);
        var fernM = EnvMat("SwordFern", EnvTex("fern_frond.png", false, 512, true, 256), null, new Color(0.62f, 0.74f, 0.52f), 0.18f, Vector2.one, true, 0.42f, true, spec: false);
        var berryM = EnvMat("BlackberryLeaves", EnvTex("maple_leaves.png", false, 512, true, 256), null, new Color(0.40f, 0.50f, 0.32f), 0.25f, Vector2.one, true, 0.45f, true, spec: false);
        var grassM = EnvMat("TallGrass", EnvTex("tallgrass.png", false, 512, true, 256), null, new Color(0.62f, 0.78f, 0.44f), 0.05f, Vector2.one, true, 0.38f, true, spec: false);  // b10.7 Mamta
        var reedM = EnvMat("Reeds", EnvTex("reed_blades.png", false, 512, true, 256), null, new Color(0.55f, 0.68f, 0.42f), 0.08f, Vector2.one, true, 0.38f, true, spec: false);  // b10.7 Mamta
        return new[] { cottonwood, PlantPrefab("SwordFern", fernM, 1001, "fern"), PlantPrefab("Blackberry", berryM, 1002, "bramble"),
                       PlantPrefab("TallGrass", grassM, 1003, "grass"), PlantPrefab("ReedClump", reedM, 1004, "reed") };
    }

    /// <summary>Card plants: fern = ~14 arched fronds from a crown; bramble = low tangled dome of leaf cards with arching canes;
    /// grass / reed = crossed vertical blade cards of mixed height. One LOD, culled when small on screen.</summary>
    static GameObject PlantPrefab(string name, Material mat, int seed, string kind)
    {
        var r = new System.Random(seed);
        float Rn(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var m = new CardMesh();
        Func<Vector3, Vector3> up = p => (Vector3.up * 0.8f + new Vector3(p.x, 0, p.z).normalized * 0.4f).normalized;
        if (kind == "fern")
        {
            int n = 14;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n + Rn(-0.2f, 0.2f), len = Rn(0.75f, 1.05f), lift = Rn(0.55f, 0.95f), w = 0.13f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), side = new Vector3(-dir.z, 0, dir.x);
                Vector3 p0 = dir * 0.05f, p1 = dir * len * 0.45f + Vector3.up * lift * 0.75f, p2 = dir * len + Vector3.up * lift * 0.55f;
                Quad(m, m.leaf, p0 - side * w * 0.5f, p1 - side * w, p1 + side * w, p0 + side * w * 0.5f, new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0), up);
                Quad(m, m.leaf, p1 - side * w, p2 - side * w * 0.25f, p2 + side * w * 0.25f, p1 + side * w, new Vector2(0, 0.5f), new Vector2(0.4f, 1), new Vector2(0.6f, 1), new Vector2(1, 0.5f), up);
            }
        }
        else if (kind == "bramble")
        {
            Vector3 cc = new Vector3(0, 0.55f, 0);
            for (int c = 0; c < 34; c++)
            {
                Vector3 dir = new Vector3(Rn(-1, 1), Rn(-0.1f, 0.8f), Rn(-1, 1)).normalized;
                Vector3 p = cc + Vector3.Scale(dir, new Vector3(1.6f, 0.75f, 1.6f)) * Rn(0.4f, 1f); p.y = Mathf.Max(p.y, 0.2f);
                Vector3 face = (dir + new Vector3(Rn(-0.4f, 0.4f), 0.5f, Rn(-0.4f, 0.4f))).normalized;
                Vector3 t1 = Vector3.Cross(face, Vector3.up); if (t1.sqrMagnitude < 0.01f) t1 = Vector3.right; t1.Normalize(); Vector3 t2 = Vector3.Cross(t1, face).normalized;
                float s = Rn(0.35f, 0.6f);
                Quad(m, m.leaf, p - t1 * s - t2 * s, p - t1 * s + t2 * s, p + t1 * s + t2 * s, p + t1 * s - t2 * s, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), p_ => ((p_ - cc).normalized * 0.6f + Vector3.up * 0.6f).normalized);
            }
        }
        else
        {
            bool reed = kind == "reed"; int n = reed ? 7 : 6; float hMin = reed ? 1.1f : 0.55f, hMax = reed ? 1.9f : 0.95f, rad = reed ? 0.55f : 0.45f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI / n + Rn(-0.2f, 0.2f), h = Rn(hMin, hMax), w = Rn(rad * 0.8f, rad * 1.2f);
                Vector3 off = new Vector3(Rn(-0.25f, 0.25f), 0, Rn(-0.25f, 0.25f)), s = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * w, lean = new Vector3(Rn(-0.15f, 0.15f), 0, Rn(-0.15f, 0.15f));
                float u0 = (i % 2) * 0.5f;
                Quad(m, m.leaf, off - s, off - s + Vector3.up * h + lean, off + s + Vector3.up * h + lean, off + s, new Vector2(u0, 0), new Vector2(u0, 1), new Vector2(u0 + 0.5f, 1), new Vector2(u0 + 0.5f, 0), p => Vector3.up);
            }
        }
        var mesh = new Mesh { name = "plant_" + name };
        mesh.SetVertices(m.v); mesh.SetNormals(m.n); mesh.SetUVs(0, m.uv); mesh.SetTriangles(m.leaf, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        SaveMesh(mesh, mesh.name);
        var go = new GameObject(name);
        var l0 = MeshObj("LOD0", go.transform, mesh, mat);
        l0.GetComponent<MeshRenderer>().shadowCastingMode = kind == "bramble" ? ShadowCastingMode.On : ShadowCastingMode.Off;
        var lg = go.AddComponent<LODGroup>();
        lg.SetLODs(new[] { new LOD(kind == "bramble" ? 0.006f : 0.012f, new Renderer[] { l0.GetComponent<Renderer>() }) });
        lg.RecalculateBounds();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ENVPRE + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ backdrop: rolling foothills + Cascade foothills
    /// <summary>Five ridge layers from 1.1 to 6.2 km, each higher and closer to the haze colour (SkiView/BackdropHaze, no fog),
    /// with a little valley mist at their feet. Ridge profile = ridged multi-octave noise per layer.</summary>
    static string backdropStyle;   // pass 19: null = foothill ranges, "hills" = near green hills, "flat" = low far tree line
    static void LakeBackdrop(Transform root, float[] fogColor, Vector3 sunDir, int seed)
    {
        var rnd = new System.Random(seed);
        float[] R = { 1100f, 1800f, 2800f, 4300f, 6200f };
        float[] H0 = { 110f, 230f, 380f, 600f, 900f }, H1 = { 220f, 400f, 640f, 1000f, 1500f };
        float[] HZ = { 0.34f, 0.48f, 0.60f, 0.72f, 0.82f };
        Color[] col = { new Color(0.62f, 0.72f, 0.62f), new Color(0.60f, 0.70f, 0.62f), new Color(0.58f, 0.66f, 0.64f), new Color(0.58f, 0.64f, 0.66f), new Color(0.60f, 0.64f, 0.70f) };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var t = new List<int>();
        int seg = 540;
        if (backdropStyle == "hills")
        {   // pass 19 punch list (Cedar Bend): close, fully green forested hills straight off the valley, no blue ranges, no haze band
            R = new[] { 860f, 1080f, 1350f }; H0 = new[] { 70f, 100f, 130f }; H1 = new[] { 120f, 155f, 190f }; HZ = new[] { 0.04f, 0.08f, 0.13f };
            col = new[] { new Color(0.18f, 0.29f, 0.17f), new Color(0.22f, 0.33f, 0.20f), new Color(0.29f, 0.39f, 0.25f) };   // #2f4a2c .. #4a6440
        }
        else if (backdropStyle == "flat")
        {   // pass 19 punch list (Heron Marsh): flat Lowcountry - a far, low tree line in humid haze #d9dfc8 .. #c3d4c6, no mountains
            // T3: + a near hazy tree line just outside the terrain corners (1.18 km) so no bare ground plane shows past the forest
            R = new[] { 1180f, 1300f, 2200f }; H0 = new[] { 26f, 14f, 9f }; H1 = new[] { 38f, 24f, 15f }; HZ = new[] { 0.14f, 0.22f, 0.32f };
            col = new[] { new Color(0.75f, 0.95f, 0.65f), new Color(0.80f, 0.98f, 0.72f), new Color(0.85f, 1.00f, 0.80f) };   // T3: greener, less haze (the tan fog read as a beige plain)
        }
        else if (backdropStyle == "pnw")
        {   // T3 (Tall Pine): no layered tan ranges - a near conifer tree line past the terrain, and one low pine foothill to the east only
            // Phase 1 review #4: the near ring read as a flat dark-green wall. Push it out past the farmland (1.9 km), make it a low,
            // hazy tree-line card, and lift the east foothill to the lighter haze value
            R = new[] { 1900f, 2600f }; H0 = new[] { 9f, 0f }; H1 = new[] { 16f, 150f }; HZ = new[] { 0.40f, 0.34f };
            col = new[] { new Color(0.70f, 0.95f, 0.72f), new Color(0.75f, 0.98f, 0.80f) };
        }
        float t3Gain = backdropStyle == "pnw" || backdropStyle == "flat" ? 3.2f : 1f;   // T3: the texture x 0.42 tint is ~0.03 linear, so the haze alone set the colour (tan / beige); lift the layer colour instead
        for (int L = 0; L < col.Length; L++) { col[L].r *= t3Gain; col[L].g *= t3Gain; col[L].b *= t3Gain; }
        if (backdropTint.HasValue) for (int L = 0; L < col.Length; L++) col[L] = Color.Lerp(backdropTint.Value * 1.25f, col[L], 0.1f * L);   // pass 19 desert ridges
        for (int L = 0; L < R.Length; L++)
        {
            var ph = new float[16]; for (int i = 0; i < ph.Length; i++) ph[i] = (float)rnd.NextDouble() * 6.283f;
            float Ridge(float th)
            {
                float s = 0, amp = 1, tot = 0;
                // pass 16 r2: rounded, forested foothill ridges (the first try was ridged noise and read alpine / jagged)
                for (int k = 0; k < 4; k++)
                {
                    float f = (2 + k * 3 + (L % 3)) * (1 + k * 0.9f);
                    float f2 = f * 1.6f;
                    if (backdropStyle == "pnw" || backdropStyle == "flat") { f = Mathf.Round(f); f2 = Mathf.Round(f2); }   // T3: whole-number frequencies so the ridge closes at th = 0 (no height step)
                    float n = 0.5f + 0.5f * (Mathf.Sin(th * f + ph[k]) * 0.65f + Mathf.Sin(th * f2 + ph[k + 5]) * 0.35f);
                    s += Mathf.SmoothStep(0f, 1f, n) * amp; tot += amp; amp *= 0.45f;
                }
                return s / tot;
            }
            int b0 = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float th = i * Mathf.PI * 2 / seg, top = Mathf.Lerp(H0[L], H1[L], Ridge(th));
                Vector3 d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                float u = th * R[L] / 420f;
                bool t3 = backdropStyle == "pnw" || backdropStyle == "flat";
                if (t3) u = th / (Mathf.PI * 2) * Mathf.Round(Mathf.PI * 2 * R[L] / 420f);   // T3: whole number of texture repeats round the ring (no vertical seam at the wrap)
                if (backdropStyle == "pnw" && L == 1) { float e = Mathf.Cos(th); top *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.9f, e)); top = Mathf.Max(top, 6f); }   // east (+x) foothill only
                // 3 rows: foot (hidden behind nearer layers, misty), shoulder, crest
                float[] ys = { -40f, top * 0.55f, top };
                float[] hz = { Mathf.Min(1f, HZ[L] + 0.16f), HZ[L] + 0.05f, HZ[L] };
                float[] rr = { R[L] * 0.97f, R[L] * 0.99f, R[L] };
                for (int k = 0; k < 3; k++)
                {
                    v.Add(d * rr[k] + Vector3.up * ys[k]); uv.Add(new Vector2(u, ys[k] / 420f));
                    var cc = col[L]; cc.a = hz[k]; c.Add(cc);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int k = 0; k < 2; k++)
                {
                    int a = b0 + i * 3 + k, bq = a + 1, cq = a + 3, dq = cq + 1;
                    t.Add(a); t.Add(cq); t.Add(bq); t.Add(bq); t.Add(cq); t.Add(dq);
                }
        }
        if (backdropStyle == "pnw") FarmSkirt(root, R[0] * 0.97f, sunDir);   // Phase 1 review #4: straw farmland N/S/W, pine toward the E foothill
        if (backdropStyle == "flat")
        {   // T3: ground skirt from under the terrain edge out to the near tree line, so no beige sky-cube ground shows past the terrain from aerial / oblique views
            int b1 = v.Count, ns = 180; float r0 = 760f, r1 = R[0] * 0.97f;
            for (int i = 0; i <= ns; i++)
            {
                float th = i * Mathf.PI * 2 / ns; Vector3 d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                // Tall Pine: farmland N/S/W (Holly), conifer toward the east foothill; Heron: marsh forest
                Color gc = backdropStyle == "flat" ? new Color(0.75f, 0.95f, 0.65f) : Color.Lerp(new Color(1.30f, 1.40f, 0.85f), new Color(0.70f, 0.95f, 0.72f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, Mathf.Cos(th))));
                foreach (var (rr, hz) in new[] { (r0, 0.10f), (r1, backdropStyle == "flat" ? 0.38f : 0.22f) })
                { var p = d * rr + Vector3.up * -3f; v.Add(p); uv.Add(new Vector2(p.x / 420f, p.z / 420f)); var g2 = gc * t3Gain; g2.a = hz; c.Add(g2); }
            }
            for (int i = 0; i < ns; i++) { int a = b1 + i * 2; t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3); }
        }
        var m = new Mesh { name = "lake_backdrop", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        LakeMesh(m, m.name);
        string mp = LD + "Materials/Backdrop.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("SkiView/BackdropHaze")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("SkiView/BackdropHaze");
        mat.SetTexture("_BaseMap", EnvTex("hills_forest.png", false, 512, false, 256));
        mat.SetColor("_BaseColor", backdropTint ?? new Color(0.42f, 0.46f, 0.42f));
        mat.SetColor("_HazeColor", backdropStyle == "pnw" ? new Color(0.62f, 0.68f, 0.66f) : backdropStyle == "flat" ? new Color(0.50f, 0.55f, 0.46f) : new Color(fogColor[0], fogColor[1], fogColor[2]));   // T3: green-grey distance haze (the tan fog colour read as tan mountains / a beige plain)
        mat.SetVector("_SunDir", sunDir); mat.SetFloat("_Light", 1f);
        EditorUtility.SetDirty(mat);
        var g = MeshObj("Backdrop", root, m, mat);
        var mr = g.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        Debug.Log($"[SkiView] lake backdrop: {R.Length} ridge layers {R[0] / 1000f:0.0}-{R[R.Length - 1] / 1000f:0.0} km, {v.Count} verts");
    }

    // Phase 1 review #4 (Tall Pine): flat straw farmland #c9b47a ring from the terrain edge out to the tree-line card, shading to
    // conifer green toward the east foothill only. Untextured (the forest texture is what made the old skirt read as a green wall).
    static void FarmSkirt(Transform root, float r1, Vector3 sunDir)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var t = new List<int>();
        int ns = 240; float r0 = 760f;
        Color straw = Color.white, pine = new Color(0.36f, 0.50f, 0.30f) / new Color(0.79f, 0.71f, 0.48f, 1f).maxColorComponent;
        for (int i = 0; i <= ns; i++)
        {
            float th = i * Mathf.PI * 2 / ns; Vector3 d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
            float e = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.9f, Mathf.Cos(th)));   // +x = east
            // gentle field banding so it reads as fields, not a plastic disc
            float band = 0.92f + 0.08f * Mathf.Sin(th * 37f) * Mathf.Sin(th * 11f + 1.3f);
            var gc = Color.Lerp(straw * band, pine, e);
            foreach (var (rr, hz) in new[] { (r0, 0.08f), (r1, 0.36f) })
            { var p = d * rr + Vector3.up * -3f; v.Add(p); uv.Add(Vector2.zero); var g2 = gc; g2.a = hz; c.Add(g2); }
        }
        for (int i = 0; i < ns; i++) { int a = i * 2; t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3); }
        var m = new Mesh { name = "lake_farmskirt", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        LakeMesh(m, m.name);
        string mp = LD + "Materials/BackdropFarm.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("SkiView/BackdropHaze")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("SkiView/BackdropHaze");
        mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
        mat.SetColor("_BaseColor", new Color(0xc9 / 255f, 0xb4 / 255f, 0x7a / 255f));   // straw #c9b47a
        mat.SetColor("_HazeColor", new Color(0.72f, 0.74f, 0.70f));                       // light warm haze
        mat.SetVector("_SunDir", sunDir); mat.SetFloat("_Light", 1f);
        EditorUtility.SetDirty(mat);
        var g = MeshObj("BackdropFarm", root, m, mat);
        var mr = g.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    // ------------------------------------------------------------------ building detail helpers
    static Material LakeGrimeMat()
    {
        string p = LD + "Materials/Grime.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetTexture("_BaseMap", EnvTex("grime_streaks.png", false, 512, true, 256, true)); m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent - 50;
        m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(m); return m;
    }

    /// <summary>Weathering overlay on one wall face: a quad 1.5 cm proud of the face (local +z out), grime at the base, streaks below the eaves.</summary>
    static void Grime(Transform p, Vector3 centre, float w, float h, float yaw, Material grime)
    {
        var q = new Mesh { name = "grime" };
        q.SetVertices(new List<Vector3> { new Vector3(-w / 2, -h / 2, 0), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(w / 2, -h / 2, 0) });
        q.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(w / 4f, 1), new Vector2(w / 4f, 0) });
        q.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0); q.RecalculateNormals(); q.RecalculateBounds();
        var g = new GameObject("Grime"); g.transform.SetParent(p, false); g.transform.localPosition = centre; g.transform.localEulerAngles = new Vector3(0, yaw, 0);
        g.AddComponent<MeshFilter>().sharedMesh = q;
        var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = grime; mr.shadowCastingMode = ShadowCastingMode.Off;
        LakeMesh(q, "grime_" + p.name + "_" + Mathf.RoundToInt(yaw) + "_" + Mathf.RoundToInt(centre.x * 10) + "_" + Mathf.RoundToInt(centre.z * 10));
    }

    /// <summary>Double-hung window with real depth: casing 10 cm proud of the wall, sashes and glass set back behind it, a projecting
    /// sill, a head cap, a meeting rail and 2x2 muntins per sash. Local +z out of the wall; pos = centre on the wall face.</summary>
    static void WindowDeep(Transform p, Vector3 pos, float w, float h, Material glass, Material trim, Material sash, float yaw)
    {
        var g = new GameObject("Window").transform; g.SetParent(p, false); g.localPosition = pos; g.localEulerAngles = new Vector3(0, yaw, 0);
        Box("Glass", g, new Vector3(0, 0, 0.005f), new Vector3(w, h, 0.01f), glass);
        Box("Reveal", g, new Vector3(0, 0, 0.004f), new Vector3(w + 0.02f, h + 0.02f, 0.006f), sash);
        float cw = 0.11f, cd = 0.10f;
        Box("CasingL", g, new Vector3(-w / 2 - cw / 2, 0, cd / 2), new Vector3(cw, h + 0.04f, cd), trim);
        Box("CasingR", g, new Vector3(w / 2 + cw / 2, 0, cd / 2), new Vector3(cw, h + 0.04f, cd), trim);
        Box("Head", g, new Vector3(0, h / 2 + 0.09f, cd / 2), new Vector3(w + 2 * cw + 0.04f, 0.16f, cd), trim);
        Box("HeadCap", g, new Vector3(0, h / 2 + 0.185f, cd / 2 + 0.02f), new Vector3(w + 2 * cw + 0.14f, 0.035f, cd + 0.05f), trim);
        Box("Sill", g, new Vector3(0, -h / 2 - 0.035f, 0.09f), new Vector3(w + 2 * cw + 0.10f, 0.05f, 0.18f), trim, new Vector3(-8, 0, 0));
        Box("Apron", g, new Vector3(0, -h / 2 - 0.12f, cd / 2), new Vector3(w + 0.04f, 0.10f, cd * 0.8f), trim);
        // sashes: frames set 6 cm behind the casing face
        float sd = 0.035f, z = 0.025f, rail = 0.06f;
        foreach (float sy in new[] { -h / 4, h / 4 })
        {
            Box("SashT", g, new Vector3(0, sy + h / 4 - rail / 2, z), new Vector3(w, rail, sd), sash);
            Box("SashB", g, new Vector3(0, sy - h / 4 + rail / 2, z), new Vector3(w, rail, sd), sash);
            Box("SashL", g, new Vector3(-w / 2 + rail / 2, sy, z), new Vector3(rail, h / 2, sd), sash);
            Box("SashR", g, new Vector3(w / 2 - rail / 2, sy, z), new Vector3(rail, h / 2, sd), sash);
            Box("MuntinV", g, new Vector3(0, sy, z - 0.005f), new Vector3(0.022f, h / 2 - rail, 0.02f), sash);
            Box("MuntinH", g, new Vector3(0, sy, z - 0.005f), new Vector3(w - rail, 0.022f, 0.02f), sash);
        }
    }

    static Material LakeBrick()
    {
        var m = LakeMat("ChimneyBrick", EnvTex("shore_color.jpg", false, 1024, false, 512), EnvTex("shore_normal.png", true, 512, false, 256), new Color(0.62f, 0.34f, 0.27f), 0.08f, new Vector2(0.6f, 0.6f), 0.8f);
        return m;
    }

    /// <summary>The farmhouse beyond the shell: corner boards, water table, frieze, rake boards, gutters + downspouts, deep windows,
    /// a proper porch (turned posts, railings + balusters, steps, beadboard ceiling), brick chimney with cap, ridge cap,
    /// a one-storey rear mudroom wing and weathering. Local +z faces the water; walls are W x D x H from y = 0.6.</summary>
    static void FarmhouseDetail(Transform go, float W_, float D_, float H_, float rise, float ov, Material wall, Material roof, Material trim, Material glass, Material concrete)
    {
        var sash = LakeFlat("SashOffWhite", new Color(0.86f, 0.86f, 0.83f), 0.3f);
        var gutter = LakeFlat("GutterAlu", new Color(0.88f, 0.88f, 0.86f), 0.55f, 0.3f);
        var door = LakeFlat("DoorGreen", new Color(0.16f, 0.24f, 0.20f), 0.35f);
        var ceil = LakeFlat("PorchCeilingBlue", new Color(0.72f, 0.82f, 0.84f), 0.3f);
        var grime = LakeGrimeMat();
        float y0 = 0.6f, yT = y0 + H_;
        // corner boards, water table, frieze
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
            Box("CornerBoard", go, new Vector3(sx * (W_ / 2 + 0.01f), y0 + H_ / 2, sz * (D_ / 2 + 0.01f)), new Vector3(0.20f, H_, 0.20f), trim);
        Box("WaterTableF", go, new Vector3(0, y0 + 0.10f, D_ / 2 + 0.02f), new Vector3(W_ + 0.2f, 0.22f, 0.05f), trim);
        Box("WaterTableB", go, new Vector3(0, y0 + 0.10f, -D_ / 2 - 0.02f), new Vector3(W_ + 0.2f, 0.22f, 0.05f), trim);
        Box("WaterTableL", go, new Vector3(-W_ / 2 - 0.02f, y0 + 0.10f, 0), new Vector3(0.05f, 0.22f, D_ + 0.2f), trim);
        Box("WaterTableR", go, new Vector3(W_ / 2 + 0.02f, y0 + 0.10f, 0), new Vector3(0.05f, 0.22f, D_ + 0.2f), trim);
        Box("FriezeF", go, new Vector3(0, yT - 0.16f, D_ / 2 + 0.02f), new Vector3(W_ + 0.2f, 0.30f, 0.05f), trim);
        Box("FriezeB", go, new Vector3(0, yT - 0.16f, -D_ / 2 - 0.02f), new Vector3(W_ + 0.2f, 0.30f, 0.05f), trim);
        Box("BeltCourseF", go, new Vector3(0, y0 + 2.95f, D_ / 2 + 0.02f), new Vector3(W_ + 0.2f, 0.16f, 0.05f), trim);
        Box("BeltCourseB", go, new Vector3(0, y0 + 2.95f, -D_ / 2 - 0.02f), new Vector3(W_ + 0.2f, 0.16f, 0.05f), trim);
        // rake boards on both gable ends (ridge runs along x; gables face +-x)
        float half = D_ / 2 + ov, drop = rise * ov / (D_ / 2), pitch = Mathf.Atan2(rise, D_ / 2) * Mathf.Rad2Deg, len = Mathf.Sqrt(half * half + (rise + drop) * (rise + drop));
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
        {
            Vector3 mid = new Vector3(sx * (W_ / 2 + ov + 0.03f), yT + (rise - drop) / 2f, sz * half / 2f);
            Box("Rake", go, mid, new Vector3(0.05f, 0.24f, len), trim, new Vector3(sz * pitch, 0, 0));
        }
        // ridge cap
        Box("RidgeCap", go, new Vector3(0, yT + rise + 0.05f, 0), new Vector3(W_ + 2 * ov, 0.10f, 0.30f), roof);
        // gutters along both eaves + 4 downspouts with kick-outs
        float ey = yT - drop - 0.08f, ez = D_ / 2 + ov + 0.04f;
        foreach (int sz in new[] { -1, 1 })
        {
            Box("Gutter", go, new Vector3(0, ey, sz * ez), new Vector3(W_ + 2 * ov, 0.13f, 0.13f), gutter);
            foreach (int sx in new[] { -1, 1 })
            {
                float dx = sx * (W_ / 2 + 0.10f), dz = sz * (D_ / 2 + 0.10f);
                Box("Downspout", go, new Vector3(dx, (ey + 0.15f) / 2f, dz), new Vector3(0.08f, ey - 0.15f, 0.08f), gutter);
                Box("DownspoutTop", go, new Vector3(dx, ey - 0.12f, (dz + sz * ez) / 2f), new Vector3(0.08f, 0.08f, Mathf.Abs(ez - Mathf.Abs(dz)) + 0.08f), gutter);
                Box("Kickout", go, new Vector3(dx, 0.12f, dz + sz * 0.25f), new Vector3(0.09f, 0.06f, 0.5f), gutter, new Vector3(sz * -8f, 0, 0));
            }
        }
        // deep windows: front (porch side) 3+3 minus the door, back 3+3, sides 2+2; a small gable window in each attic
        for (int fl = 0; fl < 2; fl++)
        {
            float wy = y0 + 1.55f + fl * 3.0f;
            for (int i = 0; i < 3; i++)
            {
                float wx = (i - 1) * W_ * 0.32f;
                if (!(fl == 0 && i == 1)) WindowDeep(go, new Vector3(wx, wy, D_ / 2), 0.95f, 1.5f, glass, trim, sash, 0);
                WindowDeep(go, new Vector3(wx, wy, -D_ / 2), 0.95f, 1.5f, glass, trim, sash, 180);
            }
            for (int i = 0; i < 2; i++)
            {
                float wz = (i - 0.5f) * D_ * 0.45f;
                WindowDeep(go, new Vector3(W_ / 2, wy, wz), 0.95f, 1.5f, glass, trim, sash, 90);
                WindowDeep(go, new Vector3(-W_ / 2, wy, wz), 0.95f, 1.5f, glass, trim, sash, -90);
            }
        }
        WindowDeep(go, new Vector3(W_ / 2, yT + 1.25f, 0), 0.6f, 0.9f, glass, trim, sash, 90);
        WindowDeep(go, new Vector3(-W_ / 2, yT + 1.25f, 0), 0.6f, 0.9f, glass, trim, sash, -90);
        // front door: casing, transom, step
        Box("Door", go, new Vector3(0, y0 + 1.05f, D_ / 2 + 0.02f), new Vector3(0.95f, 2.1f, 0.05f), door);
        for (int k = 0; k < 3; k++) Box("DoorPanel", go, new Vector3(0, y0 + 0.45f + k * 0.62f, D_ / 2 + 0.05f), new Vector3(0.62f, 0.42f, 0.02f), door);
        Box("DoorCasingL", go, new Vector3(-0.56f, y0 + 1.25f, D_ / 2 + 0.05f), new Vector3(0.14f, 2.5f, 0.09f), trim);
        Box("DoorCasingR", go, new Vector3(0.56f, y0 + 1.25f, D_ / 2 + 0.05f), new Vector3(0.14f, 2.5f, 0.09f), trim);
        Box("Transom", go, new Vector3(0, y0 + 2.32f, D_ / 2 + 0.02f), new Vector3(0.95f, 0.32f, 0.02f), glass);
        Box("DoorHead", go, new Vector3(0, y0 + 2.58f, D_ / 2 + 0.06f), new Vector3(1.30f, 0.18f, 0.10f), trim);
        // porch: deck on piers, 4 turned posts (base + shaft + capital), top/bottom rails with balusters, 3 steps, beam, ceiling
        float pw = W_ * 0.78f, pd = 2.6f, pz0 = D_ / 2, pz1 = D_ / 2 + pd, ph = 2.55f;
        PlankBox("PorchDeck", go, new Vector3(0, 0.52f, (pz0 + pz1) / 2), new Vector3(pw, 0.12f, pd), dockMat, 1.2f);
        Box("PorchSkirt", go, new Vector3(0, 0.25f, pz1 - 0.02f), new Vector3(pw, 0.45f, 0.04f), trim);
        float[] postX = { -pw / 2 + 0.1f, -0.9f, 0.9f, pw / 2 - 0.1f };
        foreach (float px in postX)
        {
            Box("PostBase", go, new Vector3(px, y0 + 0.12f, pz1 - 0.12f), new Vector3(0.24f, 0.24f, 0.24f), trim);
            Box("PostShaft", go, new Vector3(px, y0 + ph / 2, pz1 - 0.12f), new Vector3(0.14f, ph, 0.14f), trim);
            Box("PostCap", go, new Vector3(px, y0 + ph - 0.08f, pz1 - 0.12f), new Vector3(0.22f, 0.10f, 0.22f), trim);
        }
        Box("PorchBeam", go, new Vector3(0, y0 + ph + 0.10f, pz1 - 0.12f), new Vector3(pw + 0.1f, 0.26f, 0.18f), trim);
        Box("PorchCeiling", go, new Vector3(0, y0 + ph + 0.02f, (pz0 + pz1) / 2), new Vector3(pw, 0.03f, pd - 0.1f), ceil);
        var pr = PlankBox("PorchRoof", go, new Vector3(0, y0 + ph + 0.42f, (pz0 + pz1) / 2 + 0.15f), new Vector3(pw + 0.4f, 0.10f, pd + 0.7f), roof, 2f, new Vector3(13f, 0, 0));
        Box("PorchGutter", go, new Vector3(0, y0 + ph + 0.18f, pz1 + 0.42f), new Vector3(pw + 0.4f, 0.12f, 0.12f), gutter);
        // railings between posts (not across the steps)
        for (int s = 0; s < 3; s++)
        {
            if (s == 1) continue;
            float xa = postX[s] + 0.08f, xb = postX[s + 1] - 0.08f, xm = (xa + xb) / 2, lw = xb - xa;
            Box("RailTop", go, new Vector3(xm, y0 + 0.92f, pz1 - 0.12f), new Vector3(lw, 0.07f, 0.10f), trim);
            Box("RailBot", go, new Vector3(xm, y0 + 0.14f, pz1 - 0.12f), new Vector3(lw, 0.05f, 0.08f), trim);
            int nb = Mathf.FloorToInt(lw / 0.13f);
            for (int b = 1; b < nb; b++) Box("Baluster", go, new Vector3(xa + b * lw / nb, y0 + 0.53f, pz1 - 0.12f), new Vector3(0.04f, 0.74f, 0.04f), trim);
        }
        foreach (int sx in new[] { -1, 1 })
        {
            float xm = sx * (pw / 2 - 0.1f);
            Box("RailSide", go, new Vector3(xm, y0 + 0.92f, (pz0 + pz1) / 2), new Vector3(0.10f, 0.07f, pd - 0.3f), trim);
            for (int b = 1; b < 18; b++) Box("Baluster", go, new Vector3(xm, y0 + 0.53f, pz0 + 0.1f + b * (pd - 0.3f) / 18f), new Vector3(0.04f, 0.74f, 0.04f), trim);
        }
        for (int k = 0; k < 3; k++) PlankBox("Step" + k, go, new Vector3(0, 0.40f - k * 0.17f, pz1 + 0.15f + k * 0.28f), new Vector3(1.6f, 0.06f, 0.30f), dockMat, 1.2f);
        Box("StepStringerL", go, new Vector3(-0.82f, 0.22f, pz1 + 0.43f), new Vector3(0.06f, 0.36f, 0.9f), trim, new Vector3(-30, 0, 0));
        Box("StepStringerR", go, new Vector3(0.82f, 0.22f, pz1 + 0.43f), new Vector3(0.06f, 0.36f, 0.9f), trim, new Vector3(-30, 0, 0));
        Box("PorchLight", go, new Vector3(0.75f, y0 + 2.1f, D_ / 2 + 0.08f), new Vector3(0.14f, 0.24f, 0.12f), LakeFlat("LampBlack", new Color(0.08f, 0.08f, 0.08f), 0.4f));
        // brick chimney through the back slope near the ridge, corbelled cap, flashing
        var brick = LakeBrick();
        float chX = W_ * 0.28f, chZ = -0.55f, chTop = yT + rise + 1.0f, chBot = yT + rise * 0.55f;
        Box("Chimney", go, new Vector3(chX, (chTop + chBot) / 2, chZ), new Vector3(0.72f, chTop - chBot, 0.72f), brick);
        Box("ChimneyCorbel", go, new Vector3(chX, chTop - 0.12f, chZ), new Vector3(0.86f, 0.12f, 0.86f), brick);
        Box("ChimneyCap", go, new Vector3(chX, chTop + 0.04f, chZ), new Vector3(0.90f, 0.08f, 0.90f), concrete);
        Box("ChimneyFlue", go, new Vector3(chX, chTop + 0.20f, chZ), new Vector3(0.22f, 0.26f, 0.22f), LakeFlat("FlueTile", new Color(0.45f, 0.30f, 0.24f), 0.2f));
        // rear mudroom wing (one storey, shed roof) with a back door and a window
        float ww = 4.2f, wd = 2.8f, wh = 2.7f, wingZ = -D_ / 2 - wd / 2;
        PlankBox("WingFoundation", go, new Vector3(-W_ * 0.18f, 0.25f, wingZ), new Vector3(ww + 0.15f, 0.9f, wd + 0.1f), concrete, 2f);
        PlankBox("WingWalls", go, new Vector3(-W_ * 0.18f, y0 + wh / 2, wingZ), new Vector3(ww, wh, wd), wall, 2.4f);
        PlankBox("WingRoof", go, new Vector3(-W_ * 0.18f, y0 + wh + 0.35f, wingZ - 0.15f), new Vector3(ww + 0.5f, 0.10f, wd + 0.6f), roof, 2f, new Vector3(-14f, 0, 0));
        Box("WingDoor", go, new Vector3(-W_ * 0.18f - 1.0f, y0 + 1.0f, wingZ - wd / 2 - 0.02f), new Vector3(0.9f, 2.0f, 0.05f), door);
        WindowDeep(go, new Vector3(-W_ * 0.18f + 0.9f, y0 + 1.45f, wingZ - wd / 2), 0.8f, 1.1f, glass, trim, sash, 180);
        foreach (int sx in new[] { -1, 1 }) Box("WingCorner", go, new Vector3(-W_ * 0.18f + sx * (ww / 2 + 0.01f), y0 + wh / 2, wingZ - wd / 2 - 0.01f), new Vector3(0.16f, wh, 0.16f), trim);
        // foundation vents
        for (int i = -1; i <= 1; i += 2) Box("FoundationVent", go, new Vector3(i * W_ * 0.3f, 0.35f, -D_ / 2 - 0.11f), new Vector3(0.4f, 0.18f, 0.02f), LakeFlat("VentDark", new Color(0.12f, 0.12f, 0.12f), 0.2f));
        // weathering: splash-back + eave streaks on all four faces
        Grime(go, new Vector3(0, y0 + H_ / 2, D_ / 2 + 0.035f), W_, H_, 0, grime);
        Grime(go, new Vector3(0, y0 + H_ / 2, -D_ / 2 - 0.035f), W_, H_, 180, grime);
        Grime(go, new Vector3(W_ / 2 + 0.035f, y0 + H_ / 2, 0), D_, H_, 90, grime);
        Grime(go, new Vector3(-W_ / 2 - 0.035f, y0 + H_ / 2, 0), D_, H_, -90, grime);
    }

    /// <summary>Barn / shed beyond the shell: blue corner + eave + rake trim, a darker wainscot band, framed doors, gable louvre,
    /// ridge cap, gutters, an open lean-to on one side (barn) and weathering.</summary>
    static void BarnDetail(Transform go, bool barn, float W_, float D_, float H_, float rise, float ov, Color roofC, Material roof, Material trim, Material concrete)
    {
        var blue = LakeFlat(barn ? "BarnTrimBlue" : "ShedTrim", barn ? roofC * 1.1f : new Color(0.32f, 0.34f, 0.36f), 0.45f, 0.2f);
        var wains = LakeMat(barn ? "BarnWainscot" : "ShedWainscot", EnvTex("metalrib_color.png", false, 512, false, 256), EnvTex("metalrib_normal.png", true, 512, false, 256), barn ? roofC * 0.9f : new Color(0.40f, 0.40f, 0.38f), 0.4f, Vector2.one, 1f, false, 0.25f);
        var gutter = LakeFlat("GutterAlu", new Color(0.88f, 0.88f, 0.86f), 0.55f, 0.3f);
        var grime = LakeGrimeMat();
        float y0 = 0.2f, yT = y0 + H_, wh = 1.1f;
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
            Box("CornerTrim", go, new Vector3(sx * (W_ / 2 + 0.02f), y0 + H_ / 2, sz * (D_ / 2 + 0.02f)), new Vector3(0.16f, H_, 0.16f), blue);
        Box("WainF", go, new Vector3(0, y0 + wh / 2, D_ / 2 + 0.025f), new Vector3(W_, wh, 0.03f), wains);
        Box("WainB", go, new Vector3(0, y0 + wh / 2, -D_ / 2 - 0.025f), new Vector3(W_, wh, 0.03f), wains);
        Box("WainL", go, new Vector3(-W_ / 2 - 0.025f, y0 + wh / 2, 0), new Vector3(0.03f, wh, D_), wains);
        Box("WainR", go, new Vector3(W_ / 2 + 0.025f, y0 + wh / 2, 0), new Vector3(0.03f, wh, D_), wains);
        Box("WainCapF", go, new Vector3(0, y0 + wh, D_ / 2 + 0.05f), new Vector3(W_, 0.06f, 0.06f), blue);
        Box("WainCapB", go, new Vector3(0, y0 + wh, -D_ / 2 - 0.05f), new Vector3(W_, 0.06f, 0.06f), blue);
        float half = D_ / 2 + ov, drop = rise * ov / (D_ / 2), pitch = Mathf.Atan2(rise, D_ / 2) * Mathf.Rad2Deg, len = Mathf.Sqrt(half * half + (rise + drop) * (rise + drop));
        foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
            Box("RakeTrim", go, new Vector3(sx * (W_ / 2 + ov + 0.03f), yT + (rise - drop) / 2f, sz * half / 2f), new Vector3(0.05f, 0.18f, len), blue, new Vector3(sz * pitch, 0, 0));
        Box("RidgeCap", go, new Vector3(0, yT + rise + 0.04f, 0), new Vector3(W_ + 2 * ov, 0.08f, 0.40f), blue);
        float ey = yT - drop - 0.07f, ez = D_ / 2 + ov + 0.04f;
        foreach (int sz in new[] { -1, 1 })
        {
            Box("Gutter", go, new Vector3(0, ey, sz * ez), new Vector3(W_ + 2 * ov, 0.12f, 0.12f), gutter);
            foreach (int sx in new[] { -1, 1 }) Box("Downspout", go, new Vector3(sx * (W_ / 2 + 0.08f), ey / 2f, sz * (D_ / 2 + 0.10f)), new Vector3(0.08f, ey, 0.08f), gutter);
        }
        // door frames (the doors themselves are built by the caller on the +z face)
        int nd = barn ? 2 : 1; float dw = barn ? 4.0f : 2.4f, dh = barn ? 3.8f : 2.2f;
        for (int i = 0; i < nd; i++)
        {
            float dx = nd == 1 ? 0 : (i - 0.5f) * W_ * 0.45f;
            Box("DoorJambL", go, new Vector3(dx - dw / 2 - 0.08f, y0 + dh / 2, D_ / 2 + 0.06f), new Vector3(0.16f, dh, 0.10f), blue);
            Box("DoorJambR", go, new Vector3(dx + dw / 2 + 0.08f, y0 + dh / 2, D_ / 2 + 0.06f), new Vector3(0.16f, dh, 0.10f), blue);
            Box("DoorHeader", go, new Vector3(dx, y0 + dh + 0.10f, D_ / 2 + 0.06f), new Vector3(dw + 0.32f, 0.20f, 0.10f), blue);
        }
        // gable louvres
        foreach (int sx in new[] { -1, 1 })
        {
            var lv = new GameObject("Louvre").transform; lv.SetParent(go, false); lv.localPosition = new Vector3(sx * (W_ / 2 + 0.02f), yT + rise * 0.45f, 0); lv.localEulerAngles = new Vector3(0, sx * 90, 0);
            Box("LouvreFrame", lv, new Vector3(0, 0, 0.02f), new Vector3(0.9f, 0.7f, 0.04f), blue);
            for (int k = 0; k < 5; k++) Box("Slat", lv, new Vector3(0, -0.26f + k * 0.13f, 0.05f), new Vector3(0.8f, 0.05f, 0.04f), trim, new Vector3(35, 0, 0));
        }
        if (barn)
        {
            // open lean-to on the -x side: 5 m deep, posts, shed roof, gravel floor
            float lw = 5f, lh0 = 3.4f, lh1 = 2.6f;
            for (int i = 0; i < 4; i++) Box("LeanToPost", go, new Vector3(-W_ / 2 - lw + 0.1f, y0 + lh1 / 2, -D_ / 2 + 0.2f + i * (D_ - 0.4f) / 3f), new Vector3(0.18f, lh1, 0.18f), trim);
            float ang = Mathf.Atan2(lh0 - lh1, lw) * Mathf.Rad2Deg;
            PlankBox("LeanToRoof", go, new Vector3(-W_ / 2 - lw / 2, y0 + (lh0 + lh1) / 2 + 0.08f, 0), new Vector3(lw + 0.6f, 0.07f, D_ + 0.5f), roof, 1.2f, new Vector3(0, 0, ang));
            Box("LeanToBeam", go, new Vector3(-W_ / 2 - lw + 0.1f, y0 + lh1 - 0.05f, 0), new Vector3(0.16f, 0.22f, D_ + 0.2f), trim);
        }
        Grime(go, new Vector3(0, y0 + H_ / 2, D_ / 2 + 0.06f), W_, H_, 0, grime);
        Grime(go, new Vector3(0, y0 + H_ / 2, -D_ / 2 - 0.06f), W_, H_, 180, grime);
        Grime(go, new Vector3(W_ / 2 + 0.06f, y0 + H_ / 2, 0), D_, H_, 90, grime);
        Grime(go, new Vector3(-W_ / 2 - 0.06f, y0 + H_ / 2, 0), D_, H_, -90, grime);
    }
}
