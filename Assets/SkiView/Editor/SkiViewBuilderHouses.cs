using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Houses (pass 7, top priority): Christopher's 18 QA-ready lot FBXs (staging houses README) replace the greybox kit.
/// Each lot-<addr>.fbx has its pivot at street-grade ground, centred along the shore at the street edge; local +Z points at
/// the lake and the shoreline sits at local Z = shoreZ. Placement: n = into-water shore normal, rotation LookRotation(n),
/// pivot = S - n * shoreZ, where S is the lot's shore point (venue.json; 8409, the vacant Lot 15, is interpolated between
/// 8405 and 8415). Lots are kept in catalog order (north to south) and nudged along the shoreline so that the modelled
/// houses don't overlap. The terrain is graded to each lot (flat pad at street grade; 8017 drops 6.2 m to its shore).
public static partial class SkiViewBuilder
{
    const string HOUSES = "Assets/SkiView/Houses/";
    const float WATER_Y = 0f;

    // lot, shoreZ, X extent (x0..x1, incl. dock), modelled house W x D, dock X range (README "Per lot")
    static readonly object[][] LOT_TABLE =
    {
        new object[] { "8017", 44.40f, -18.13f, 18.13f, 24.0f, 22.5f, -17.95f, 17.95f },
        new object[] { "8035", 38.95f, -11.25f, 11.10f, 21.0f, 15.2f, 2.92f, 5.48f },
        new object[] { "8049", 37.55f, -11.4f, 11.6f, 22.0f, 12.6f, 3.13f, 5.69f },
        new object[] { "8107", 50.11f, -12.5f, 11.6f, 22.0f, 17.6f, 3.12f, 5.68f },
        new object[] { "8121", 36.36f, -10.3f, 13.2f, 19.0f, 14.0f, 2.52f, 5.08f },
        new object[] { "8143", 38.56f, -13.9f, 10.6f, 20.0f, 14.0f, 2.73f, 5.29f },
        new object[] { "8201", 37.96f, -9.8f, 9.4f, 18.0f, 13.0f, 2.33f, 4.89f },
        new object[] { "8215", 39.96f, -11.9f, 11.1f, 21.0f, 16.1f, 2.92f, 5.48f },
        new object[] { "8221", 38.76f, -12.6f, 12.6f, 24.0f, 16.0f, 3.52f, 6.08f },
        new object[] { "8229", 36.36f, -13.4f, 13.4f, 26.0f, 14.0f, 3.92f, 12.5f },
        new object[] { "8235", 38.86f, -12.8f, 12.6f, 24.0f, 16.5f, 3.52f, 6.08f },
        new object[] { "8311", 36.20f, -10.8f, 10.6f, 20.0f, 13.8f, 2.72f, 5.28f },
        new object[] { "8317", 37.75f, -12.3f, 12.1f, 23.0f, 14.5f, 3.32f, 5.88f },
        new object[] { "8405", 40.96f, -14.8f, 14.6f, 28.0f, 16.2f, 4.32f, 6.88f },
        new object[] { "8409", 60.00f, -20.0f, 20.0f, 40.0f, 60.0f, -1.28f, 1.28f },
        new object[] { "8415", 43.78f, -13.8f, 13.1f, 25.0f, 15.0f, 3.72f, 6.28f },
        new object[] { "8419", 37.74f, -18.0f, 13.6f, 26.0f, 15.0f, 3.92f, 6.48f },
        new object[] { "8505", 36.13f, -15.3f, 15.1f, 29.1f, 13.6f, 4.53f, 7.09f },
    };

    class LotPlace
    {
        public string lot, arch; public bool hero, vacant;
        public float shoreZ, x0, x1, w, d, dock0, dock1, arc, arc0;
        public Vector3 S, n, r, pivot;
        public Vector3 Local(Vector3 w) { var q = w - pivot; return new Vector3(Vector3.Dot(q, r), 0, Vector3.Dot(q, n)); }
        public Vector3 World(float lx, float lz) => pivot + r * lx + n * lz;
    }
    static List<LotPlace> lotPlaces;

    static bool HousesReady() => File.Exists(HOUSES + "lot-8017.fbx");

    static List<List<Vector4>> ShoreLines()
    {
        string path = GEN + "shoreline.json";
        if (!File.Exists(path)) return null;
        // JsonUtility can't read nested arrays: parse "p":[[x,z,nx,nz],...] by hand
        string txt = File.ReadAllText(path);
        var lines = new List<List<Vector4>>();
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        int i = 0;
        while ((i = txt.IndexOf("\"p\":", i, StringComparison.Ordinal)) >= 0)
        {
            int s = txt.IndexOf('[', i) + 1, depth = 1, j = s;
            while (depth > 0) { if (txt[j] == '[') depth++; else if (txt[j] == ']') depth--; j++; }
            var nums = txt.Substring(s, j - s - 1).Replace("[", "").Replace("]", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var pl = new List<Vector4>();
            for (int k = 0; k + 3 < nums.Length; k += 4)
                pl.Add(new Vector4(float.Parse(nums[k].Trim(), ci), float.Parse(nums[k + 1].Trim(), ci), float.Parse(nums[k + 2].Trim(), ci), float.Parse(nums[k + 3].Trim(), ci)));
            lines.Add(pl); i = j;
        }
        return lines;
    }

    static List<LotPlace> LotPlaces(Venue v)
    {
        if (lotPlaces != null) return lotPlaces;
        lotPlaces = new List<LotPlace>();
        if (!HousesReady()) return lotPlaces;
        var lines = ShoreLines();
        var byLot = new Dictionary<string, House>();
        foreach (var hs in v.houses) byLot[hs.lot] = hs;
        // the shoreline polyline that carries the east-bank lots
        var h0 = byLot["8017"];
        List<Vector4> L = null; float best = float.MaxValue;
        foreach (var pl in lines) foreach (var q in pl) { float dd = (q.x - h0.shore[0]) * (q.x - h0.shore[0]) + (q.y - h0.shore[1]) * (q.y - h0.shore[1]); if (dd < best) { best = dd; L = pl; } }
        var cum = new float[L.Count];
        for (int k = 1; k < L.Count; k++) cum[k] = cum[k - 1] + Vector2.Distance(new Vector2(L[k].x, L[k].y), new Vector2(L[k - 1].x, L[k - 1].y));
        float ArcOf(float x, float z)
        {
            float bd = float.MaxValue, a = 0;
            for (int k = 0; k < L.Count - 1; k++)
            {
                Vector2 p0 = new Vector2(L[k].x, L[k].y), p1 = new Vector2(L[k + 1].x, L[k + 1].y), p = new Vector2(x, z);
                Vector2 e = p1 - p0; float t = Mathf.Clamp01(Vector2.Dot(p - p0, e) / Mathf.Max(e.sqrMagnitude, 1e-6f));
                float dd = (p0 + e * t - p).sqrMagnitude;
                if (dd < bd) { bd = dd; a = Mathf.Lerp(cum[k], cum[k + 1], t); }
            }
            return a;
        }
        Vector2 PosAt(float a)
        {
            int k = 0; while (k < L.Count - 2 && cum[k + 1] < a) k++;
            float t = Mathf.Clamp01((a - cum[k]) / Mathf.Max(cum[k + 1] - cum[k], 1e-6f));
            return Vector2.Lerp(new Vector2(L[k].x, L[k].y), new Vector2(L[k + 1].x, L[k + 1].y), t);
        }
        Vector2 InlandAt(float a, float win)
        {
            Vector2 acc = Vector2.zero;
            for (int k = 0; k < L.Count; k++) if (Mathf.Abs(cum[k] - a) <= win) acc += new Vector2(L[k].z, L[k].w).normalized;
            if (acc.sqrMagnitude < 1e-6f) { int k = 0; while (k < L.Count - 1 && cum[k] < a) k++; acc = new Vector2(L[k].z, L[k].w); }
            return acc.normalized;
        }
        foreach (var row in LOT_TABLE)
        {
            var lp = new LotPlace
            {
                lot = (string)row[0], shoreZ = (float)row[1], x0 = (float)row[2], x1 = (float)row[3], w = (float)row[4], d = (float)row[5],
                dock0 = (float)row[6], dock1 = (float)row[7]
            };
            lp.hero = lp.lot == "8017"; lp.vacant = lp.lot == "8409";
            if (byLot.TryGetValue(lp.lot, out var hs)) { lp.arch = hs.arch; lp.arc = ArcOf(hs.shore[0], hs.shore[1]); }
            else lp.arch = lp.vacant ? "VACANT" : "X";
            lotPlaces.Add(lp);
        }
        for (int k = 0; k < lotPlaces.Count; k++)
            if (!byLot.ContainsKey(lotPlaces[k].lot)) lotPlaces[k].arc = 0.5f * (lotPlaces[k - 1].arc + lotPlaces[k + 1].arc);   // 8409 between 8405 and 8415
        foreach (var lp in lotPlaces) lp.arc0 = lp.arc;
        var extra = new float[lotPlaces.Count];
        int round = 0;
        for (; round < 8; round++)
        {
            foreach (var lp in lotPlaces) lp.arc = lp.arc0;
            Respace(extra);
            foreach (var lp in lotPlaces) PlaceOnShore(lp);
            bool any = false;
            for (int k = 0; k < lotPlaces.Count - 1; k++) if (Overlap(lotPlaces[k], lotPlaces[k + 1])) { extra[k] += 3f; any = true; }
            if (!any) break;
        }
        var sb = new System.Text.StringBuilder();
        foreach (var lp in lotPlaces) sb.Append($"{lp.lot}:{lp.arc - lp.arc0:+0.0;-0.0} ");
        Debug.Log($"[SkiView] houses: lot shifts along the shore (m, {round + 1} rounds) " + sb);
        foreach (var lp in lotPlaces)
        {
            var o = PosAt(lp.arc0);
            Debug.Log($"[SkiView] houses: lotpos {lp.lot} venueShore=({o.x:0.0},{o.y:0.0}) finalShore=({lp.S.x:0.0},{lp.S.z:0.0}) along={lp.arc - lp.arc0:+0.0;-0.0} pivot=({lp.pivot.x:0.0},{lp.pivot.z:0.0}) facing={Mathf.Atan2(lp.n.x, lp.n.z) * Mathf.Rad2Deg:0}");
        }
        for (int k = 0; k < lotPlaces.Count - 1; k++) if (Overlap(lotPlaces[k], lotPlaces[k + 1])) Debug.LogWarning($"[SkiView] houses: {lotPlaces[k].lot} and {lotPlaces[k + 1].lot} footprints overlap");
        return lotPlaces;

        void Respace(float[] ex)
        {
            // respace along the shore (ends pinned): modelled widths + 3 m, 14 m beside the hero so its 6 m grade change has room
            float Span(LotPlace a) => a.hero ? (a.x1 - a.x0) : a.w;
            for (int it = 0; it < 2000; it++)
                for (int k = 0; k < lotPlaces.Count - 1; k++)
                {
                    var a = lotPlaces[k]; var b = lotPlaces[k + 1];
                    float need = 0.5f * (Span(a) + Span(b)) + (a.hero || b.hero ? 14f : 3f) + ex[k];
                    float dd = need - (b.arc - a.arc);
                    if (dd <= 0) continue;
                    float wa = k == 0 ? 0 : 0.5f, wb = k + 1 == lotPlaces.Count - 1 ? 0 : 0.5f;
                    if (wa + wb == 0) continue;
                    a.arc -= dd * wa / (wa + wb); b.arc += dd * wb / (wa + wb);
                }
        }
        void PlaceOnShore(LotPlace lp)
        {
            Vector2 s = PosAt(lp.arc), inl = InlandAt(lp.arc, Mathf.Max(15f, 0.5f * lp.w));
            lp.S = new Vector3(s.x, WATER_Y, s.y);
            lp.n = new Vector3(-inl.x, 0, -inl.y).normalized;       // into the water
            lp.r = Vector3.Cross(Vector3.up, lp.n);
            float grade = lp.hero ? 6.55f : 0.45f;                    // street grade above the water (hero: shore grade -6.2)
            lp.pivot = lp.S - lp.n * lp.shoreZ; lp.pivot.y = WATER_Y + grade;
        }
    }

    static bool Overlap(LotPlace a, LotPlace b)
    {
        // modelled house footprints (8409: its hedged lawn), sampled both ways, 1 m clearance
        bool Test(LotPlace p, LotPlace q)
        {
            for (float t = 0; t <= 1.001f; t += 0.125f)
                for (float u = 0; u <= 1.001f; u += 0.125f)
                {
                    var w = q.World(Mathf.Lerp(-q.w / 2, q.w / 2, t), Mathf.Lerp(0, q.d, u));
                    var l = p.Local(w);
                    if (Mathf.Abs(l.x) < p.w / 2 + 1f && l.z > -1f && l.z < p.d + 1f) return true;
                }
            return false;
        }
        return Test(a, b) || Test(b, a);
    }

    // v2: every lot's lake-side mowing curb / shore edging spans x = +/-19.5 m, so grading, tree exclusion and the rip-rap
    // scatter use at least that width (the modelled house W stays the respacing width)
    const float V2_EDGE = 19.5f;
    static float XL(LotPlace lp) => Mathf.Min(lp.x0, -V2_EDGE);
    static float XR(LotPlace lp) => Mathf.Max(lp.x1, V2_EDGE);

    static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

    /// Terrain grading under the lots (heights h are normalised: world y = ymin + h * hrange).
    static void GradeLots(Venue v, float[,] h)
    {
        var lots = LotPlaces(v);
        if (lots.Count == 0) return;
        int res = v.terrain.res; float size = v.terrain.size, ymin = v.terrain.ymin, hr = v.terrain.hrange, cell = size / (res - 1);
        var order = new List<LotPlace>(lots); order.Sort((a, b) => (b.hero ? 1 : 0) - (a.hero ? 1 : 0));   // hero first, neighbours override
        foreach (var lp in order)
        {
            float back = lp.hero ? 30f : 18f, side = lp.hero ? 14f : 8f, pad = lp.hero ? 0f : (lp.vacant ? 0.5f : 3f);
            float reach = lp.shoreZ + back + side + 25f;
            int cx = Mathf.RoundToInt((lp.pivot.x + size / 2) / cell), cz = Mathf.RoundToInt((lp.pivot.z + size / 2) / cell), rr = Mathf.CeilToInt(reach / cell);
            for (int zi = Mathf.Max(0, cz - rr); zi <= Mathf.Min(res - 1, cz + rr); zi++)
                for (int xi = Mathf.Max(0, cx - rr); xi <= Mathf.Min(res - 1, cx + rr); xi++)
                {
                    var wpos = new Vector3(-size / 2 + xi * cell, 0, -size / 2 + zi * cell);
                    var q = lp.Local(wpos); float lx = q.x, lz = q.z;
                    float dx = Mathf.Max(0, Mathf.Max(XL(lp) - pad - lx, lx - (XR(lp) + pad)));
                    float wx = 1f - Smooth01(dx / side);
                    if (wx <= 0) continue;
                    float wz, rel;
                    if (lp.hero)
                    {
                        // street grade to lz 10, down to the -3.2 walkout under/at the lake face, patio, then the terrace treads to -6.2 at the shore
                        if (lz < 10f) rel = 0f;
                        else if (lz < 21f) rel = -3.2f * Smooth01((lz - 10f) / 11f);
                        else if (lz < 27f) rel = -3.2f;
                        else { float t = Mathf.Clamp01((lz - 27f) / (lp.shoreZ - 1.5f - 27f)); rel = Mathf.Lerp(-3.2f, -6.25f, t) - 0.45f * Mathf.Sin(Mathf.PI * t); }
                        rel -= 0.06f;
                        wz = lz < -2f ? 1f - Smooth01((-2f - lz) / back) : (lz > lp.shoreZ - 1.5f ? 1f - Smooth01((lz - (lp.shoreZ - 1.5f)) / 2.5f) : 1f);
                    }
                    else
                    {
                        rel = lp.vacant ? -0.07f : -0.05f;
                        float f0 = lp.shoreZ - 5f;
                        wz = lz < -2f ? 1f - Smooth01((-2f - lz) / back) : (lz > f0 ? 1f - Smooth01((lz - f0) / 4.5f) : 1f);
                    }
                    float wgt = wx * wz;
                    if (wgt <= 0) continue;
                    float target = (lp.pivot.y + rel - ymin) / hr;
                    h[zi, xi] = Mathf.Lerp(h[zi, xi], Mathf.Clamp01(target), wgt);
                }
        }
        Debug.Log($"[SkiView] houses: terrain graded under {lots.Count} lots (8017 drops 6.2 m to its shore)");
    }

    /// True inside a lot (street edge to just past the shore) plus a margin: keeps trees off the yards and houses.
    static bool InsideLot(float x, float z, float margin)
    {
        if (InsideSouth(x, z, margin)) return true;   // b10
        if (lotPlaces == null) return false;
        foreach (var lp in lotPlaces)
        {
            var q = lp.Local(new Vector3(x, 0, z));
            if (q.x > XL(lp) - margin && q.x < XR(lp) + margin && q.z > -margin - 3f && q.z < lp.shoreZ + 2f) return true;
        }
        return false;
    }

    /// True near a lot's dock or anywhere along a lot's frontage: v2 lots model their own rock / beach / bulkhead shore edge
    /// (8017: two-row riprap), so the generic rip-rap scatter stays off them.
    static bool NearLotDock(Vector2 p)
    {
        if (lotPlaces == null) return false;
        foreach (var lp in lotPlaces)
        {
            var q = lp.Local(new Vector3(p.x, 0, p.y));
            if (q.z > lp.shoreZ - 6f && q.z < lp.shoreZ + 6f && q.x > Mathf.Min(lp.dock0, XL(lp)) - 1f && q.x < Mathf.Max(lp.dock1, XR(lp)) + 1f) return true;
        }
        return false;
    }

    /// Centre of each lot's house (for the watered-lawn splat); falls back to the venue greybox positions.
    static List<(Vector2 c, int lot)> LotCentres(Venue v)
    {
        var res = new List<(Vector2, int)>();
        var lots = LotPlaces(v);
        if (lots.Count > 0) foreach (var lp in lots) { var c = lp.World(0.5f * (lp.x0 + lp.x1), lp.vacant ? 25f : lp.d * 0.5f + 6f); res.Add((new Vector2(c.x, c.z), int.Parse(lp.lot))); }
        else foreach (var hs in v.houses) res.Add((new Vector2(hs.x, hs.z), int.TryParse(hs.lot, out int ln) ? ln : 7));
        return res;
    }

    static readonly Dictionary<string, Material> houseMats = new Dictionary<string, Material>();

    /// v2 materials: hv2_lotNN_body (albedo + normal atlas), hv2_lotNN_glass (opaque dark tint), hv2_lotNN_impostor (LOD2).
    static Material HouseMat(Material src)
    {
        if (src == null) return null;
        if (houseMats.TryGetValue(src.name, out var m)) return m;
        string dir = HOUSES + "Materials/"; Directory.CreateDirectory(dir);
        string p = dir + src.name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        else m.shader = Shader.Find("Universal Render Pipeline/Lit");
        string nm = src.name;                                              // hv2_lot07_body
        string surf = nm.Substring(nm.LastIndexOf('_') + 1).ToLowerInvariant();
        string set = nm.LastIndexOf('_') > 0 ? nm.Substring(0, nm.LastIndexOf('_')) : nm;   // hv2_lot07
        Texture tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
        if (tex == null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
        if (tex == null) tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HOUSES + "textures/" + set + "_" + (surf == "body" ? "albedo" : surf) + ".jpg");
        Color col = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
        if (tex != null) col = Color.white;
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(col.r, col.g, col.b, 1f));
        m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureOffset("_BaseMap", Vector2.zero);
        m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
        float smooth = 0.2f, metal = 0f;
        if (surf == "body")
        {
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(HOUSES + "textures/" + set + "_normal.jpg");
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            // b10.28: prefer URP MetallicSmoothness when Christopher ships it (Lot 17+); Metallic/Roughness stay Unreal-only archives
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(HOUSES + "textures/" + set + "_metallicSmoothness.jpg");
            if (ms != null)
            {
                m.SetTexture("_MetallicGlossMap", ms);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                smooth = 1f; metal = 1f;   // map drives both
            }
            else
            {
                m.SetTexture("_MetallicGlossMap", null);
                m.DisableKeyword("_METALLICSPECGLOSSMAP");
            }
        }
        else if (surf == "glass") { smooth = 0.9f; metal = 0.2f; }    // opaque; reflections from the house-row probes / sky cubemap
        else if (surf == "impostor") smooth = 0.1f;
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        m.SetFloat("_EnvironmentReflections", 1f); m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.SetFloat("_SpecularHighlights", 1f); m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        houseMats[src.name] = m;
        return m;
    }

    /// Baked box reflection probes over the house row (north / middle / south thirds) so the v2 glass reflects the lake,
    /// far shore and sky instead of only the sky cubemap. 128 px HDR cubes, baked at build time (no runtime cost).
    static void BuildHouseProbes(Venue v, Transform root)
    {
        var lots = LotPlaces(v);
        if (lots.Count == 0) return;
        var pr = new GameObject("HouseReflectionProbes").transform; pr.SetParent(root, false);
        int n = 0;
        for (int g = 0; g < 3; g++)
        {
            int i0 = g * lots.Count / 3, i1 = (g + 1) * lots.Count / 3;
            var b = new Bounds(lots[i0].pivot, Vector3.zero);
            for (int k = i0; k < i1; k++) { b.Encapsulate(lots[k].pivot); b.Encapsulate(lots[k].S + lots[k].n * 12f); b.Encapsulate(lots[k].pivot - lots[k].n * 5f); }
            b.Expand(new Vector3(30f, 0f, 30f)); b.min = new Vector3(b.min.x, -2f, b.min.z); b.max = new Vector3(b.max.x, 30f, b.max.z);
            var go = new GameObject("HouseProbe_" + g); go.transform.SetParent(pr, false);
            // capture point over the water in front of the group, at window height
            Vector3 c = Vector3.zero; for (int k = i0; k < i1; k++) c += lots[k].S + lots[k].n * 8f; c /= (i1 - i0); c.y = 4f;
            go.transform.position = c;
            var rp = go.AddComponent<ReflectionProbe>();
            rp.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom; rp.resolution = 128; rp.hdr = true; rp.boxProjection = false;
            rp.size = b.size; rp.center = b.center - c; rp.blendDistance = 8f; rp.importance = 1; rp.farClipPlane = 2500f; rp.nearClipPlane = 0.5f;
            rp.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox; rp.intensity = 1f;
            string path = GEN + "HouseProbe_" + g + ".exr";
            try
            {
                if (Lightmapping.BakeReflectionProbe(rp, path))
                {
                    AssetDatabase.ImportAsset(path);
                    var cube = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    if (cube != null) { rp.customBakedTexture = cube; n++; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[SkiView] houses: probe bake failed " + e.Message); }
        }
        Debug.Log($"[SkiView] houses: {n}/3 house-row reflection probes baked");
    }

    /// Instantiates the 18 lot FBXs; returns false (greybox kit used) if they aren't in the project.
    static bool BuildHousesFbx(Venue v, Transform hroot)
    {
        var lots = LotPlaces(v);
        if (lots.Count == 0) return false;
        houseMats.Clear(); yardSpots.Clear(); int yardParts = 0;
        int tris0 = 0, slots0 = 0, placed = 0;
        foreach (var lp in lots)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HOUSES + "lot-" + lp.lot + ".fbx");
            if (model == null) { Debug.LogWarning("[SkiView] houses: missing lot-" + lp.lot + ".fbx"); continue; }
            var wrap = new GameObject($"Lot_{lp.lot}_{lp.arch}").transform; wrap.SetParent(hroot, false);
            wrap.position = lp.pivot; wrap.rotation = Quaternion.LookRotation(lp.n, Vector3.up);
            if (lp.vacant) { placed++; continue; }   // b10 (Scott): Lot 15 is open grass with one central tree (BuildSouthEnd), no lot model
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(wrap, false); inst.transform.localRotation = Quaternion.identity;
            // v2 mowing curbs span +/-19.5 m and neighbouring lots sit 28-43 m apart, so curb ends can overlap at the lot line:
            // every other lot sits 1.5 cm higher so the overlap resolves cleanly instead of z-fighting
            inst.transform.localPosition = new Vector3(0f, (placed % 2) * 0.015f, 0f);
            foreach (var rnd in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var sm = rnd.sharedMaterials;
                for (int i = 0; i < sm.Length; i++) sm[i] = HouseMat(sm[i]);
                rnd.sharedMaterials = sm;
                rnd.shadowCastingMode = ShadowCastingMode.On; rnd.receiveGI = ReceiveGI.LightProbes;
                rnd.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                var ymf = rnd.GetComponent<MeshFilter>();
                if (ymf && (rnd.name.EndsWith("_LOD0") || rnd.name.EndsWith("_LOD1"))) yardParts += CleanYard(ymf, rnd, lp.lot, lp.pivot.y + (placed % 2) * 0.015f, rnd.name.EndsWith("_LOD0"));
                if (rnd.name.EndsWith("_LOD0")) { var mf = rnd.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) tris0 += mf.sharedMesh.triangles.Length / 3; slots0 += sm.Length; }
            }
            // LOD group: README transitions 0.70 / 0.28 / 0.02 at 60 deg; the game camera runs ~45-55 deg with mobile LOD bias 0.6
            var lg = inst.GetComponent<LODGroup>();
            if (lg == null)
            {
                lg = inst.AddComponent<LODGroup>();
                var l = new List<LOD>();
                for (int k = 0; k < 3; k++) { var t = inst.transform.Find(inst.name + "_LOD" + k) ?? FindSuffix(inst.transform, "_LOD" + k); if (t) l.Add(new LOD(0, t.GetComponentsInChildren<Renderer>())); }
                lg.SetLODs(l.ToArray());
            }
            var lods = lg.GetLODs();
            float[] tr = { 0.60f, 0.24f, 0.012f };
            for (int k = 0; k < lods.Length && k < 3; k++) lods[k].screenRelativeTransitionHeight = tr[k];
            if (lods.Length >= 3) { lods[0].fadeTransitionWidth = 0.1f; lods[1].fadeTransitionWidth = 0.1f; }
            lg.SetLODs(lods); lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.None;
            foreach (var t in wrap.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            // dock marker (camera framing hooks; the dock itself is part of the FBX)
            var dm = new GameObject($"Dock_{lp.lot}").transform; dm.SetParent(hroot, false);
            var dpos = lp.S + lp.r * (lp.hero ? 0f : 0.5f * (lp.dock0 + lp.dock1)) - lp.n * 2f; dpos.y = lp.pivot.y + (lp.hero ? -6.2f : 0f);
            dm.position = dpos; dm.rotation = Quaternion.LookRotation(lp.n);
            placed++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SkiView] houses: {placed} lot FBXs placed (LOD0 {tris0} tris, {slots0} material slots; {houseMats.Count} URP Lit materials, v2); yard cleanup removed {yardParts} low-poly parts, {yardSpots.Count} spots");
        return placed > 0;
    }

    static Transform FindSuffix(Transform t, string suffix)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name.EndsWith(suffix)) return c;
        return null;
    }
}
