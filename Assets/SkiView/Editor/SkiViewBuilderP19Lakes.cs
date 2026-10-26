using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiView;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pass 19: the four new lakes (Saguaro Flats, Harrow Pond, Gulfwind, Marrowind) and Christopher's regional kits.
/// - Kit pieces (Assets/SkiView/Kits/&lt;kit&gt;/&lt;piece&gt;.fbx, README pivots: +Z faces the lake, LOD0/1/2) placed from venue.json
///   buildings of kind "kit", under their own root (not merged; LODGroup 0.60 / 0.24 / 0.012 like the home-lake houses).
/// - Procedural extras: highway bridge spans, a generic show grandstand, breakwater floats and an observation-spire silhouette.
/// - Desert plant set (mesquite, palo verde, fan palm, saguaro, creosote) for Saguaro Flats.
/// - Far water ring + far-shore silhouette strip + house impostor blocks for a lake larger than its terrain (Marrowind).
/// No real-world names, logos or text anywhere.</summary>
public static partial class SkiViewBuilder
{
    const string KITS = "Assets/SkiView/Kits/";
    static readonly Dictionary<string, Material> kitMats = new Dictionary<string, Material>();
    static Color? backdropTint;

    static Material KitMat(Material src, string kit)
    {
        if (src == null) return null;
        if (kitMats.TryGetValue(src.name, out var m)) return m;
        string dir = KITS + "Materials/"; Directory.CreateDirectory(dir);
        string p = dir + src.name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        else m.shader = Shader.Find("Universal Render Pipeline/Lit");
        string nm = src.name;                                                   // kit_desert_modern_1s_body
        string surf = nm.Substring(nm.LastIndexOf('_') + 1).ToLowerInvariant();
        string set = nm.LastIndexOf('_') > 0 ? nm.Substring(0, nm.LastIndexOf('_')) : nm;
        Texture tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
        if (tex == null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
        if (tex == null) tex = AssetDatabase.LoadAssetAtPath<Texture2D>(KITS + kit + "/textures/" + set + "_" + (surf == "body" ? "albedo" : surf) + ".jpg");
        Color col = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
        if (tex != null) col = Color.white;
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(col.r, col.g, col.b, 1f));
        m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
        float smooth = 0.2f, metal = 0f;
        if (surf == "body")
        {
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(KITS + kit + "/textures/" + set + "_normal.jpg");
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        }
        else if (surf == "glass") { smooth = 0.9f; metal = 0.2f; }
        else if (surf == "impostor") smooth = 0.1f;
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        kitMats[src.name] = m;
        return m;
    }

    static Transform FindSuffixT(Transform t, string suf)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name.EndsWith(suf)) return c;
        return null;
    }

    /// <summary>One kit piece: FBX instance (unpacked), lake-folder-independent kit materials, LODGroup from the _LOD0/1/2 children.</summary>
    static int KitPiece(LakeBuilding b, Transform kRoot, Terrain terrain, ref int tris0)
    {
        string path = KITS + b.kit + "/" + b.piece + ".fbx";
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null) { Debug.LogWarning($"[SkiView] kit piece missing: {path}"); return 0; }
        var inst = (GameObject)UnityEngine.Object.Instantiate(src);
        inst.name = b.piece; inst.transform.SetParent(kRoot, false);
        float y = b.shore ? b.y : terrain.SampleHeight(new Vector3(b.x, 0, b.z)) + terrain.transform.position.y;
        inst.transform.position = new Vector3(b.x, y, b.z);
        inst.transform.rotation = Quaternion.Euler(0, b.yaw, 0);
        inst.transform.localScale = new Vector3(b.mirror ? -1f : 1f, 1f, 1f);
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
        {
            var sm = r.sharedMaterials; for (int i = 0; i < sm.Length; i++) sm[i] = KitMat(sm[i], b.kit); r.sharedMaterials = sm;
            bool lod2 = r.name.EndsWith("_LOD2");
            r.shadowCastingMode = lod2 ? ShadowCastingMode.Off : ShadowCastingMode.On; r.receiveShadows = !lod2;
            if (r.name.EndsWith("_LOD0")) { var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris0 += mf.sharedMesh.triangles.Length / 3; }
        }
        var lg = inst.GetComponent<LODGroup>() ?? inst.AddComponent<LODGroup>();
        var lods = new List<LOD>(); float[] tr = { 0.60f, 0.24f, 0.012f };
        for (int k = 0; k < 3; k++) { var t = FindSuffixT(inst.transform, "_LOD" + k); if (t) lods.Add(new LOD(tr[k], t.GetComponentsInChildren<Renderer>())); }
        if (lods.Count == 0) lods.Add(new LOD(0.012f, inst.GetComponentsInChildren<Renderer>()));
        else { var l = lods[lods.Count - 1]; l.screenRelativeTransitionHeight = 0.012f; lods[lods.Count - 1] = l; }
        lg.SetLODs(lods.ToArray()); lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.None;
        foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        return 1;
    }

    static void KitBuildings(LakeVenue v, Transform root, Terrain terrain)
    {
        if (v.buildings == null || !v.buildings.Any(b => b.kind == "kit")) return;
        var kRoot = new GameObject("KitBuildings").transform; kRoot.SetParent(root, false);
        int n = 0, tris0 = 0; var byKit = new Dictionary<string, int>();
        foreach (var b in v.buildings.Where(b => b.kind == "kit"))
        {
            n += KitPiece(b, kRoot, terrain, ref tris0);
            byKit[b.kit] = byKit.TryGetValue(b.kit, out var c) ? c + 1 : 1;
        }
        Debug.Log($"[SkiView] kit buildings: {n} pieces ({string.Join(", ", byKit.Select(kv => kv.Key + " " + kv.Value))}), LOD0 {tris0} tris, {kitMats.Count} kit materials");
    }

    /// <summary>Pass 19 procedural extras (generic, no text or logos).</summary>
    static void P19Building(LakeBuilding b, Transform go, Material concrete, Material trim)
    {
        Color wallC = C3(b.wall), roofC = C3(b.roof);
        if (b.kind == "bridge")
        {
            // four-lane divided highway span on two pier rows: deck, parapets, a grey band across the north end
            go.position = new Vector3(b.x, 0, b.z);
            var deck = LakeFlat("BridgeConcrete", wallC, 0.15f); var asph = LakeFlat("BridgeAsphalt", roofC, 0.1f);
            float L = b.d, Wd = b.w, H = b.h;
            Box("Deck", go, new Vector3(0, H, 0), new Vector3(Wd, 0.9f, L), deck);
            Box("Road", go, new Vector3(0, H + 0.47f, 0), new Vector3(Wd - 1.2f, 0.05f, L), asph);
            foreach (float s in new[] { -1f, 1f }) Box("Parapet", go, new Vector3(s * (Wd / 2 - 0.25f), H + 0.95f, 0), new Vector3(0.4f, 1.0f, L), deck);
            Box("Median", go, new Vector3(0, H + 0.8f, 0), new Vector3(0.6f, 0.7f, L), deck);
            for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j++) Box("Pier", go, new Vector3(j * Wd * 0.33f, H / 2 - 1.5f, i * L * 0.22f), new Vector3(1.2f, H + 3f, 1.6f), deck);
            foreach (float s in new[] { -1f, 1f }) Box("Abutment", go, new Vector3(0, H / 2 - 1f, s * (L / 2 - 1.5f)), new Vector3(Wd + 2f, H + 2f, 3f), deck);
        }
        else if (b.kind == "grandstand")
        {
            var seat = LakeFlat("StandSeats", wallC, 0.3f); var roof = LakeFlat("StandRoof", roofC, 0.45f, 0.2f); var steel = LakeFlat("StandSteel", new Color(0.55f, 0.57f, 0.6f), 0.4f, 0.5f);
            for (int r = 0; r < 8; r++) Box("Tier" + r, go, new Vector3(0, 0.4f + r * 0.55f, -r * 0.9f + 3f), new Vector3(b.w, 0.55f, 0.9f), r % 2 == 0 ? seat : trim);
            Box("Back", go, new Vector3(0, 2.6f, -4.6f), new Vector3(b.w, 5.2f, 0.3f), seat);
            Box("Canopy", go, new Vector3(0, b.h, -1.0f), new Vector3(b.w + 2f, 0.25f, 9f), roof, new Vector3(-6f, 0, 0));
            for (int i = 0; i < 7; i++) Box("Post" + i, go, new Vector3(-b.w / 2 + i * b.w / 6, b.h / 2, -4.4f), new Vector3(0.3f, b.h, 0.3f), steel);
        }
        else if (b.kind == "breakwater")
        {
            go.position = new Vector3(b.x, 0, b.z);
            var f1 = LakeFlat("FloatWhite", wallC, 0.5f); var f2 = LakeFlat("FloatOrange", roofC, 0.5f);
            int n = Mathf.RoundToInt(b.d / 2.4f);
            for (int i = 0; i < n; i++) Box("Float" + i, go, new Vector3(0, 0.12f, -b.d / 2 + i * 2.4f), new Vector3(b.w, 0.4f, 2.1f), i % 2 == 0 ? f1 : f2);
        }
        else if (b.kind == "spire")
        {
            // generic observation spire: tapered column, ring cabin part-way up, red cap (silhouette from the water)
            var col = LakeFlat("SpireWhite", wallC, 0.35f); var cap = LakeFlat("SpireCap", roofC, 0.4f);
            for (int i = 0; i < 6; i++) { float t = i / 6f, w = Mathf.Lerp(b.w, b.w * 0.45f, t); Box("Seg" + i, go, new Vector3(0, b.h * (t + 1f / 12f), 0), new Vector3(w, b.h / 6f + 0.05f, w), col); }
            var ring = Prim(PrimitiveType.Cylinder, "Cabin", go, new Vector3(0, b.h * 0.42f, 0), new Vector3(b.w * 2.4f, 1.6f, b.w * 2.4f), cap);
            Prim(PrimitiveType.Cylinder, "Top", go, new Vector3(0, b.h + 1.5f, 0), new Vector3(b.w * 0.6f, 1.5f, b.w * 0.6f), cap);
            foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        }
    }

    // ------------------------------------------------------------------ desert plants (Saguaro Flats)
    public const int D_MESQUITE = 0, D_PALOVERDE = 1, D_FANPALM = 2, D_SAGUARO = 3, D_CREOSOTE = 4;

    static GameObject DesertTreePrefab(string name, int seed, bool paloVerde, Material bark, Material leaf, Material imp)
    {
        // low multi-trunk desert tree: 2-4 leaning stems, flattened round crown of leaf cards (olive for mesquite, yellow-green for palo verde)
        var r = new System.Random(seed); float Rn(float a, float b) => a + (float)r.NextDouble() * (b - a);
        float H = paloVerde ? 6.0f : 5.5f; var m = new CardMesh();
        int ns = 2 + r.Next(3);
        for (int s = 0; s < ns; s++)
        {
            float a = Rn(0, Mathf.PI * 2); Vector3 lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Rn(1.0f, 2.2f);
            Trunk(m, -0.2f, H * 0.55f, 0.14f, 0.06f, 5, 3, lean, 1.2f);
        }
        Vector3 ctr = new Vector3(0, H * 0.68f, 0);
        int nc = 26;
        for (int k = 0; k < nc; k++)
        {
            float a = Rn(0, Mathf.PI * 2), rr = Mathf.Sqrt(Rn(0, 1)) * H * 0.62f;
            Vector3 c = ctr + new Vector3(Mathf.Cos(a) * rr, Rn(-0.6f, 0.9f) * (1f - rr / (H * 0.7f)) + 0.2f, Mathf.Sin(a) * rr);
            float s = Rn(1.0f, 1.5f);
            Func<Vector3, Vector3> nrm = q => ((q - ctr).normalized * 0.6f + Vector3.up * 0.55f).normalized;
            for (int q = 0; q < 2; q++)
            {
                float qa = a + q * Mathf.PI / 2; Vector3 sv = new Vector3(Mathf.Cos(qa), 0, Mathf.Sin(qa)) * s;
                Quad(m, m.leaf, c - sv + Vector3.down * s * 0.5f, c - sv + Vector3.up * s * 0.5f, c + sv + Vector3.up * s * 0.5f, c + sv + Vector3.down * s * 0.5f, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), nrm);
            }
            Vector3 hx = new Vector3(s, 0, 0), hz = new Vector3(0, 0, s);
            Quad(m, m.leaf, c - hx - hz, c - hx + hz, c + hx + hz, c + hx - hz, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), nrm);
        }
        return GulfTreePrefab(name, m, bark, leaf, imp, H, 0.12f);
    }

    static GameObject SaguaroPrefab(string name, int seed, Material skin, Material imp)
    {
        // saguaro: ribbed column (12-sided, 9 m) with 2-3 upturned arms; trunk-only mesh (no leaf cards)
        var r = new System.Random(seed); float Rn(float a, float b) => a + (float)r.NextDouble() * (b - a);
        float H = 9f; var m = new CardMesh();
        Trunk(m, -0.3f, H, 0.36f, 0.30f, 12, 6, Vector3.zero, 1.2f);
        AddCap(m, new Vector3(0, H, 0), 0.30f, 12);
        int na = 2 + r.Next(2);
        for (int k = 0; k < na; k++)
        {
            float a = k * Mathf.PI * 2 / na + Rn(-0.5f, 0.5f), y0 = Rn(H * 0.38f, H * 0.6f), out_ = Rn(0.9f, 1.4f), up = Rn(2.0f, 3.4f);
            Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            int b0 = m.v.Count;
            // elbow: horizontal stub then vertical arm, as a 10-sided tube through 5 rings
            Vector3[] path = { d * 0.2f + Vector3.up * y0, d * (out_ * 0.7f) + Vector3.up * (y0 + 0.1f), d * out_ + Vector3.up * (y0 + 0.6f), d * out_ + Vector3.up * (y0 + 1.6f), d * out_ + Vector3.up * (y0 + up) };
            for (int j = 0; j < path.Length; j++)
            {
                Vector3 tgt = (j < path.Length - 1 ? path[j + 1] - path[j] : path[j] - path[j - 1]).normalized;
                Vector3 s1 = Vector3.Cross(tgt, Vector3.up).sqrMagnitude < 1e-3f ? Vector3.Cross(tgt, d).normalized : Vector3.Cross(tgt, Vector3.up).normalized, s2 = Vector3.Cross(tgt, s1).normalized;
                for (int i = 0; i <= 10; i++) { float th = i * Mathf.PI * 2 / 10; Vector3 n = s1 * Mathf.Cos(th) + s2 * Mathf.Sin(th); m.v.Add(path[j] + n * 0.22f); m.n.Add(n); m.uv.Add(new Vector2(i / 10f * 2f, (y0 + j) / 1.2f)); }
            }
            for (int j = 0; j < path.Length - 1; j++)
                for (int i = 0; i < 10; i++) { int a0 = b0 + j * 11 + i, b1 = a0 + 1, c0 = a0 + 11, d0 = c0 + 1; m.trunk.Add(a0); m.trunk.Add(c0); m.trunk.Add(b1); m.trunk.Add(b1); m.trunk.Add(c0); m.trunk.Add(d0); }
            AddCap(m, path[path.Length - 1], 0.22f, 10);
        }
        return GulfTreePrefab(name, m, skin, skin, imp, H, 0.12f);
    }

    static void AddCap(CardMesh m, Vector3 c, float r, int sides)
    {
        int b0 = m.v.Count; m.v.Add(c + Vector3.up * r * 0.6f); m.n.Add(Vector3.up); m.uv.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= sides; i++) { float a = i * Mathf.PI * 2 / sides; Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); m.v.Add(c + d * r); m.n.Add((d + Vector3.up).normalized); m.uv.Add(new Vector2(0.5f + d.x * 0.5f, 0.5f + d.z * 0.5f)); }
        for (int i = 0; i < sides; i++) { m.trunk.Add(b0); m.trunk.Add(b0 + 2 + i); m.trunk.Add(b0 + 1 + i); }
    }

    static GameObject[] DesertPlantKit()
    {
        var bark = AssetDatabase.LoadAssetAtPath<Material>(ENVMAT + "Bark.mat");
        Texture2D bt = bark != null ? (Texture2D)bark.GetTexture("_BaseMap") : null, bn = bark != null ? (Texture2D)bark.GetTexture("_BumpMap") : null;
        var mesqBark = EnvMat("MesquiteBark", bt, bn, new Color(0.52f, 0.44f, 0.38f), 0.08f, Vector2.one);
        var pvBark = EnvMat("PaloVerdeBark", null, null, new Color(0.52f, 0.62f, 0.30f), 0.25f, Vector2.one);
        var mesqLeaf = EnvMat("MesquiteLeaf", EnvTex("pine_tuft.png", false, 512, true, 256), null, new Color(0.62f, 0.70f, 0.44f), 0.1f, Vector2.one, true, 0.42f, true, spec: false);
        var pvLeaf = EnvMat("PaloVerdeLeaf", EnvTex("pine_tuft.png", false, 512, true, 256), null, new Color(0.80f, 0.86f, 0.48f), 0.1f, Vector2.one, true, 0.42f, true, spec: false);
        var skin = EnvMat("SaguaroSkin", EnvTex("stucco_color.jpg", false, 512, false, 256), EnvTex("stucco_normal.png", true, 512, false, 256), new Color(0.38f, 0.50f, 0.34f), 0.2f, new Vector2(3f, 1f), bump: 1.2f);
        var imp = EnvMat("DesertImpostor", EnvTex("pine_impostor.png", false, 256, true, 128), null, new Color(0.72f, 0.74f, 0.56f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var palmImp = EnvMat("PalmImpostor", EnvTex("palm_impostor.png", false, 256, true, 128), null, new Color(0.9f, 0.95f, 0.85f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var sabalTrunk = EnvMat("SabalTrunk", bt, bn, new Color(0.62f, 0.58f, 0.54f), 0.08f, Vector2.one);
        var fan = EnvMat("SabalFan", EnvTex("fan_leaf.png", false, 512, true, 256), null, new Color(0.86f, 0.96f, 0.78f), 0.2f, Vector2.one, true, 0.42f, true, spec: false);
        return new[]
        {
            DesertTreePrefab("Mesquite", 1901, false, mesqBark, mesqLeaf, imp), DesertTreePrefab("PaloVerde", 1902, true, pvBark, pvLeaf, imp),
            PalmPrefab("SabalPalm", false, 1703, sabalTrunk, fan, palmImp, null), SaguaroPrefab("Saguaro", 1903, skin, imp),
            AssetDatabase.LoadAssetAtPath<GameObject>(ENVPRE + "Understory.prefab"),
        };
    }

    static void DesertTrees(LakeVenue v, Terrain terrain)
    {
        var td = terrain.terrainData;
        var kit = DesertPlantKit();
        var vp = new VegProtos(kit.Select(k => new TreePrototype { prefab = k })); bool veg = VegReady;   // pass 19: vegetation pack
        Vector3 tp = terrain.transform.position, size = td.size;
        var rnd = new System.Random(1919); float Fr() => (float)rnd.NextDouble();
        var list = new List<TreeInstance>(); int[] byKind = new int[5];
        int n = v.treesFlat.Length / 5;
        for (int i = 0; i < n; i++)
        {
            float x = v.treesFlat[i * 5], y = v.treesFlat[i * 5 + 1], z = v.treesFlat[i * 5 + 2], s = v.treesFlat[i * 5 + 3]; int kind = (int)v.treesFlat[i * 5 + 4];
            float nx = (x - tp.x) / size.x, nz = (z - tp.z) / size.z;
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
            float vs = 0f; string vid = veg ? VegPick(v.lake.id, "desert", kind, s, Fr, out vs) : null;
            if (vid != null) { list.Add(new TreeInstance { position = new Vector3(nx, (y - 0.25f - tp.y) / size.y, nz), heightScale = vs, widthScale = vs * (0.9f + Fr() * 0.2f), rotation = Fr() * Mathf.PI * 2f, prototypeIndex = vp[vid], color = Color.white, lightmapColor = Color.white }); continue; }
            kind = Mathf.Clamp(VegFallback("desert", kind), 0, 4);
            byKind[kind]++;
            float hs = s * (0.85f + Fr() * 0.3f), ws = hs * (kind == D_SAGUARO ? 1f : 0.95f + Fr() * 0.3f);
            if (kind == D_CREOSOTE) { hs = s * (0.5f + Fr() * 0.3f); ws = hs * 1.4f; }
            list.Add(new TreeInstance { position = new Vector3(nx, (y - 0.25f - tp.y) / size.y, nz), heightScale = hs, widthScale = ws, rotation = Fr() * Mathf.PI * 2f, prototypeIndex = kind, color = Color.white, lightmapColor = Color.white });
        }
        td.treePrototypes = vp.list.ToArray(); td.RefreshPrototypes();
        td.SetTreeInstances(list.ToArray(), true);
        terrain.treeDistance = 2400f; terrain.treeBillboardDistance = 2400f; terrain.treeCrossFadeLength = 0f; terrain.treeMaximumFullLODCount = 50000;
        EditorUtility.SetDirty(td);
        Debug.Log($"[SkiView] desert trees: {list.Count} ({vp.count} vegetation-pack; mesquite {byKind[0]}, palo verde {byKind[1]}, fan palm {byKind[2]}, saguaro {byKind[3]}, creosote {byKind[4]})");
    }

    static TerrainLayer[] DesertLayers() => new[]
    {
        LakeLayer("Alfalfa", "lawn_color.jpg", "lawn_normal.png", 2.6f, 0.05f, new Color(0.80f, 1.0f, 0.55f)),
        LakeLayer("Granite", "sand_white_color.jpg", "sand_white_normal.png", 3.0f, 0.08f, new Color(0.92f, 0.76f, 0.66f), 0.8f),   // decomposed granite #d1ae98
        LakeLayer("DesertFloor", "drygrass_color.jpg", "drygrass_normal.png", 3.4f, 0.04f, new Color(0.88f, 0.74f, 0.58f)),   // b10.7 Mamta: less chalky desert
        LakeLayer("Gravel", "gravel_color.jpg", "gravel_normal.png", 1.6f, 0.05f, new Color(0.72f, 0.60f, 0.48f), 0.8f),   // b9: dirt-toned, matte waterline
    };

    // ------------------------------------------------------------------ lake larger than the terrain (Marrowind)
    static void FarWater(LakeVenue v, Transform root, Material wm)
    {
        // a ring of 8 big quads round the clamped water grid out to 6 km (same water material; reflections and waves read the same)
        float x0 = Mathf.Floor(v.lakeBounds[0]), x1 = Mathf.Ceil(v.lakeBounds[1]), z0 = Mathf.Floor(v.lakeBounds[2]), z1 = Mathf.Ceil(v.lakeBounds[3]), R = 6000f;
        var verts = new List<Vector3>(); var tris = new List<int>();
        void Q(float ax, float az, float bx, float bz)
        {
            int k = verts.Count; verts.Add(new Vector3(ax, -0.02f, az)); verts.Add(new Vector3(ax, -0.02f, bz)); verts.Add(new Vector3(bx, -0.02f, bz)); verts.Add(new Vector3(bx, -0.02f, az));
            tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
        }
        Q(-R, -R, x0, R); Q(x1, -R, R, R); Q(x0, -R, x1, z0); Q(x0, z1, x1, R);
        var m = new Mesh { name = "lake_far_water" }; m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
        LakeMesh(m, m.name);
        var g = MeshObj("FarWater", root, m, wm); var r = g.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        Debug.Log($"[SkiView] far water ring to {R / 1000f:0} km round the {x1 - x0:0} x {z1 - z0:0} m water grid");
    }

    static void FarShore(LakeVenue v, Transform root, float[] fogColor, Vector3 sunDir)
    {
        if (v.farSil == null || v.farSil.Length < 8) return;
        // silhouette strip along the shore outside the terrain: cypress / oak treeline 12-24 m with a shore band, haze by distance
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var cs = new List<Color>(); var t = new List<int>();
        int n = v.farSil.Length / 4; float u = 0;
        Color tree = new Color(0.30f, 0.36f, 0.26f), shoreC = new Color(0.40f, 0.42f, 0.30f);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = new Vector3(v.farSil[i * 4], 0, v.farSil[i * 4 + 1]), nn = new Vector3(v.farSil[i * 4 + 2], 0, v.farSil[i * 4 + 3]);
            float d = new Vector2(p.x, p.z).magnitude, hz = Mathf.Clamp01(d / 4200f) * 0.75f + 0.12f;
            float top = 12f + 12f * Mathf.PerlinNoise(p.x * 0.013f, p.z * 0.013f) + 3f * Mathf.PerlinNoise(p.x * 0.08f, p.z * 0.08f);
            if (i > 0) u += Vector3.Distance(p, new Vector3(v.farSil[(i - 1) * 4], 0, v.farSil[(i - 1) * 4 + 1])) / 420f;
            Vector3[] row = { p + nn * 2f + Vector3.down * 0.5f, p + nn * 6f + Vector3.up * 1.2f, p + nn * 14f + Vector3.up * top, p + nn * 40f + Vector3.up * (top + 2f) };
            Color[] rc = { shoreC, tree, tree * 1.08f, tree };
            for (int k = 0; k < 4; k++) { vs.Add(row[k]); uv.Add(new Vector2(u, row[k].y / 420f)); var c = rc[k]; c.a = Mathf.Clamp01(hz + (k == 0 ? 0.05f : 0f)); cs.Add(c); }
            if (i > 0 && Vector3.Distance(p, new Vector3(v.farSil[(i - 1) * 4], 0, v.farSil[(i - 1) * 4 + 1])) < 60f)
                for (int k = 0; k < 3; k++) { int a = (i - 1) * 4 + k, b = a + 1, c2 = i * 4 + k, d2 = c2 + 1; t.AddRange(new[] { a, c2, b, b, c2, d2, a, b, c2, b, d2, c2 }); }
        }
        // house impostor blocks (pale stucco walls, grey / white roofs) along the far shore within 1 km of the play box
        if (v.farHouses != null)
            for (int i = 0; i + 2 < v.farHouses.Length; i += 3)
            {
                Vector3 c = new Vector3(v.farHouses[i], 0, v.farHouses[i + 1]); var q = Quaternion.Euler(0, v.farHouses[i + 2], 0);
                float w = 14f + (i % 5) * 2f, dd = 10f, h = 3.6f; float hz = Mathf.Clamp01(c.magnitude / 4200f) * 0.75f + 0.1f;
                Color wall = (i / 3) % 3 == 0 ? new Color(0.91f, 0.88f, 0.81f) : (i / 3) % 3 == 1 ? new Color(0.84f, 0.78f, 0.66f) : new Color(0.91f, 0.85f, 0.63f); wall.a = hz;
                Color roofC = (i / 3) % 2 == 0 ? new Color(0.42f, 0.43f, 0.44f, hz) : new Color(0.90f, 0.90f, 0.88f, hz);
                int b0 = vs.Count;
                Vector3[] box = { new Vector3(-w / 2, 0, dd / 2), new Vector3(w / 2, 0, dd / 2), new Vector3(w / 2, h, dd / 2), new Vector3(-w / 2, h, dd / 2), new Vector3(0, h + 1.8f, dd / 2) };
                foreach (var bp in box) { vs.Add(c + q * bp); uv.Add(Vector2.zero); cs.Add(bp.y > h + 0.1f ? roofC : wall); }
                t.AddRange(new[] { b0, b0 + 3, b0 + 2, b0, b0 + 2, b0 + 1, b0 + 3, b0 + 4, b0 + 2 });
            }
        var m = new Mesh { name = "lake_far_shore", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(vs); m.SetUVs(0, uv); m.SetColors(cs); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        LakeMesh(m, m.name);
        string mp = LD + "Materials/FarShore.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("SkiView/BackdropHaze")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("SkiView/BackdropHaze");
        mat.SetTexture("_BaseMap", EnvTex("hills_forest.png", false, 512, false, 256)); mat.SetColor("_BaseColor", new Color(0.46f, 0.50f, 0.44f));
        mat.SetColor("_HazeColor", new Color(fogColor[0], fogColor[1], fogColor[2])); mat.SetVector("_SunDir", sunDir); mat.SetFloat("_Light", 1f);
        EditorUtility.SetDirty(mat);
        var g = MeshObj("FarShore", root, m, mat); var mr = g.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        Debug.Log($"[SkiView] far shore silhouette: {n} points, {(v.farHouses?.Length ?? 0) / 3} house impostors, {vs.Count} verts");
    }
}
