using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Pass 14: the lot FBXs carry low-poly faceted yard trees (a blocky green crown on a stick) and spiky low-poly hedges merged
/// into the body mesh. They read as placeholder geometry next to the card trees, so each lot's LOD0/LOD1 body mesh is
/// rebuilt without them: triangles are welded into connected parts, each part's colour is sampled from the lot atlas,
/// green crowns sitting on a brown trunk and green hedge strips are dropped, and their spots are handed to the tree builder,
/// which plants the regular bigleaf-maple / alder card trees and understory shrubs there instead.
public static partial class SkiViewBuilder
{
    public struct YardSpot { public Vector3 pos; public float height, width; public bool hedge; public Vector3 axis; public float length; }
    public static bool yardDiag = false;
    static readonly List<YardSpot> yardSpots = new List<YardSpot>();
    static readonly Dictionary<string, Texture2D> yardAtlas = new Dictionary<string, Texture2D>();

    static Texture2D YardAtlas(string set)
    {
        if (yardAtlas.TryGetValue(set, out var t)) return t;
        string p = HOUSES + "textures/" + set + "_albedo.jpg";
        t = null;
        if (File.Exists(p)) { t = new Texture2D(2, 2, TextureFormat.RGB24, false); t.LoadImage(File.ReadAllBytes(p)); }
        yardAtlas[set] = t; return t;
    }

    class Part { public List<int> tris = new List<int>(); public Bounds wb; public bool init; public Color col; public int n; public bool drop; public string kind = ""; }

    /// Strips yard trees / hedges from one renderer's body submesh; returns the number of parts removed.
    static int CleanYard(MeshFilter mf, MeshRenderer rnd, string lot, float groundY, bool collectSpots)
    {
        var src = mf.sharedMesh; if (src == null) return 0;
        int bodySub = -1; var mats = rnd.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name.EndsWith("_body")) bodySub = i;
        if (bodySub < 0 || bodySub >= src.subMeshCount) return 0;
        string set = mats[bodySub].name.Substring(0, mats[bodySub].name.LastIndexOf('_'));
        var atlas = YardAtlas(set); if (atlas == null) return 0;
        var v = src.vertices; var uv = src.uv; var tri = src.GetTriangles(bodySub);
        if (uv == null || uv.Length != v.Length) return 0;
        var wv = new Vector3[v.Length]; var M = mf.transform.localToWorldMatrix;
        for (int i = 0; i < v.Length; i++) wv[i] = M.MultiplyPoint3x4(v[i]);
        // weld by position, union triangles
        var key = new Dictionary<Vector3Int, int>(); var weld = new int[v.Length];
        for (int i = 0; i < v.Length; i++) { var k = Vector3Int.RoundToInt(v[i] * 500f); if (!key.TryGetValue(k, out int w)) { w = key.Count; key[k] = w; } weld[i] = w; }
        var par = new int[key.Count]; for (int i = 0; i < par.Length; i++) par[i] = i;
        int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
        for (int t = 0; t < tri.Length; t += 3) { int a = Find(weld[tri[t]]), b = Find(weld[tri[t + 1]]), c = Find(weld[tri[t + 2]]); par[b] = a; par[Find(c)] = a; }
        var parts = new Dictionary<int, Part>();
        for (int t = 0; t < tri.Length; t += 3)
        {
            int r = Find(weld[tri[t]]); if (!parts.TryGetValue(r, out var p)) { p = new Part(); parts[r] = p; }
            p.tris.Add(t);
            for (int k = 0; k < 3; k++) { var q = wv[tri[t + k]]; if (!p.init) { p.wb = new Bounds(q, Vector3.zero); p.init = true; } else p.wb.Encapsulate(q); }
            Vector2 u = (uv[tri[t]] + uv[tri[t + 1]] + uv[tri[t + 2]]) / 3f;
            p.col += atlas.GetPixelBilinear(u.x - Mathf.Floor(u.x), u.y - Mathf.Floor(u.y)); p.n++;
        }
        bool Green(Color c) => c.g > c.r * 1.12f && c.g > c.b * 1.15f;
        var list = new List<Part>(parts.Values);
        foreach (var p in list) p.col /= Mathf.Max(1, p.n);
        // crowns: green lumps lifted well off the ground (a faceted crown is several lumps); trunks: thin non-green parts under them;
        // hedge blocks: the ~1.3 x 2 m green boxes the lot hedges are made of (round 1 m shrubs and the slim cones stay)
        var crowns = new List<Part>();
        foreach (var p in list)
        {
            if (!Green(p.col)) continue;
            Vector3 s = p.wb.size; float lo = p.wb.min.y - groundY, xz = Mathf.Max(s.x, s.z), xzMin = Mathf.Min(s.x, s.z);
            if (lo > 1.5f && s.y > 2f && xz >= 2f && xz < 14f && xzMin > 0.45f * xz) { p.drop = true; p.kind = "crown"; crowns.Add(p); }
            else if (lo < 0.5f && ((s.y > 1.5f && s.y < 2.7f && xzMin >= 1.1f && xz <= 2.2f) || (s.y > 0.4f && s.y < 2.6f && xz > 4f && xzMin < 0.35f * xz + 1.0f)))
            {
                p.drop = true; p.kind = "hedge";
                if (collectSpots) yardSpots.Add(new YardSpot { pos = new Vector3(p.wb.center.x, groundY, p.wb.center.z), height = s.y, width = xzMin, hedge = true, axis = s.x > s.z ? Vector3.right : Vector3.forward, length = xz });
            }
        }
        int trunks = 0, hedges = 0; foreach (var p in list) if (p.kind == "hedge") hedges++;
        if (crowns.Count > 0)
            foreach (var q in list)
            {
                if (q.drop || Green(q.col)) continue; Vector3 qs = q.wb.size;
                if (Mathf.Max(qs.x, qs.z) > 4f || q.wb.min.y - groundY > 1.0f) continue;
                foreach (var c in crowns)
                {
                    Vector2 d = new Vector2(q.wb.center.x - c.wb.center.x, q.wb.center.z - c.wb.center.z);
                    if (d.magnitude < 3f && q.wb.max.y > c.wb.min.y - 1f) { q.drop = true; q.kind = "trunk"; trunks++; break; }
                }
            }
        // one replacement tree per cluster of crown lumps
        var clusters = new List<Bounds>();
        foreach (var c in crowns)
        {
            int k = clusters.FindIndex(b => Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(c.wb.center.x, c.wb.center.z)) < 7f);
            if (k < 0) clusters.Add(c.wb); else { var b = clusters[k]; b.Encapsulate(c.wb); clusters[k] = b; }
        }
        foreach (var b in clusters)
        {
            if (collectSpots) yardSpots.Add(new YardSpot { pos = new Vector3(b.center.x, groundY, b.center.z), height = b.max.y - groundY, width = Mathf.Max(b.size.x, b.size.z) });
            Debug.Log($"[SkiView] yard: lot {lot} {mf.name} low-poly tree removed (crown {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0} m in {crowns.Count} lumps, top {b.max.y - groundY:0.0} m, {trunks} trunk parts) at ({b.center.x:0.0}, {b.center.z:0.0})");
        }
        if (hedges > 0) Debug.Log($"[SkiView] yard: lot {lot} {mf.name} {hedges} low-poly hedge blocks removed");
        int dropped = 0; var keep = new List<int>();
        foreach (var p in list) { if (p.drop) { dropped++; continue; } foreach (int t in p.tris) { keep.Add(tri[t]); keep.Add(tri[t + 1]); keep.Add(tri[t + 2]); } }
        if (dropped == 0) return 0;
        var m = Object.Instantiate(src); m.name = src.name + "_noyard";
        m.SetTriangles(keep, bodySub); m.RecalculateBounds();
        mf.sharedMesh = SaveMesh(m, "lot_" + lot + "_" + mf.name + "_noyard");
        return dropped;
    }

    /// Called by the tree builder: card trees / understory where the low-poly yard trees and hedges were.
    static void PlantYardSpots(List<TreeInstance> list, Terrain terrain, int treeProtoA, int treeProtoB, int shrubA, int shrubB)
    {
        var td = terrain.terrainData; Vector3 tp = terrain.transform.position, size = td.size;
        float hA = ProtoHeight(td, treeProtoA), hB = ProtoHeight(td, treeProtoB);
        var rnd = new System.Random(1414); int trees = 0, shrubs = 0;
        void Put(Vector3 p, float hs, float ws, int proto)
        {
            float nx = (p.x - tp.x) / size.x, nz = (p.z - tp.z) / size.z; if (nx < 0 || nx > 1 || nz < 0 || nz > 1) return;
            list.Add(new TreeInstance { position = new Vector3(nx, (p.y - 0.1f - tp.y) / size.y, nz), heightScale = hs, widthScale = ws, rotation = (float)rnd.NextDouble() * 6.283f,
                prototypeIndex = proto, color = Color.white, lightmapColor = Color.white });
        }
        foreach (var s in yardSpots)
        {
            if (!s.hedge) { bool a = rnd.NextDouble() < 0.5; float h = Mathf.Clamp(s.height * 1.15f, 6f, 13f); float hs = h / (a ? hA : hB); Put(s.pos, hs, hs * (0.9f + 0.2f * (float)rnd.NextDouble()), a ? treeProtoA : treeProtoB); trees++; }
            else
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(s.length / 1.3f));
                for (int i = 0; i < n; i++) { Vector3 p = s.pos + s.axis * ((i + 0.5f) / n - 0.5f) * s.length; float hs = Mathf.Clamp(s.height, 0.8f, 1.6f) * (0.85f + 0.3f * (float)rnd.NextDouble()); Put(p, hs, hs * 1.2f, i % 2 == 0 ? shrubA : shrubB); shrubs++; }
            }
        }
        Debug.Log($"[SkiView] yard: planted {trees} card trees and {shrubs} understory shrubs in place of the low-poly yard trees / hedges");
    }

    static float ProtoHeight(TerrainData td, int i)
    {
        var go = td.treePrototypes[i].prefab; float h = 0f;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.max.y - go.transform.position.y);
        return h > 0.5f ? h : 10f;
    }
}
