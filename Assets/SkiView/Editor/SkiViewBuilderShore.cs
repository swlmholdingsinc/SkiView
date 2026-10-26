using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pass 6 shoreline edging: a continuous strip all the way round the lake and islands (from Tools/gen_shoreline.py),
/// starting 0.9 m under water and running 2.6 m inland. It has a small raised bank lip, a dark wet band at the waterline and a
/// ragged alpha edge into the lawn. Rock rip-rap or a grass bank is chosen per ~40 m section (grass is more likely in front of houses).</summary>
public static partial class SkiViewBuilder
{
    /// <summary>b9 (Scott Oct 23): ONE definition of the shared ShoreRock asset. The b6 bank fix regressed because the lake builder
    /// re-wrote the same ShoreRock.mat with Color.white / 0.25 smoothness after the home build, so Ski View's banks went pale (snow /
    /// water) again. Both builders now call this: brown dirt / mud, matte.</summary>
    internal static Material ShoreRockMat() => EnvMat("ShoreRock", EnvTex("shore_strip_rock.png", false, 1024, true, 512, true), EnvTex("shore_normal.png", true, 512, false, 256), new Color(0.34f, 0.26f, 0.18f), 0.05f, Vector2.one, true, 0.5f, false, 0.7f, false);  // b10.5: dirt, no env reflections

    static void BuildShoreline(Venue v, Transform root, Terrain terrain)
    {
        string path = GEN + "shoreline.json";
        if (!File.Exists(path)) { Debug.LogWarning("[SkiView] no shoreline.json (run Tools/gen_shoreline.py)"); return; }
        var lines = ShoreLines();
        float H(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
        var rockM = ShoreRockMat();
        var grassM = EnvMat("ShoreGrass", EnvTex("shore_strip_grass.png", false, 1024, true, 512, true), EnvTex("lawn_normal.png", true, 512, false, 256), new Color(0.38f, 0.50f, 0.26f), 0.03f, Vector2.one, true, 0.5f, false, 0.6f, false);

        // Scott Oct 23: the turn islands get a brown dirt/mud bank at the waterline (no grey rip-rap)
        var mudM = EnvMat("ShoreMud", EnvTex("shore_strip_grass.png", false, 1024, true, 512, true), EnvTex("lawn_normal.png", true, 512, false, 256), new Color(0.30f, 0.22f, 0.15f), 0.05f, Vector2.one, true, 0.5f, false, 0.6f, false);
        var isl = new List<Vector2>(); if (v.islandCentres != null) for (int i = 0; i + 1 < v.islandCentres.Length; i += 2) isl.Add(new Vector2(v.islandCentres[i], v.islandCentres[i + 1]));
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tRock = new List<int>(); var tGrass = new List<int>(); var tMud = new List<int>();
        var rockPts = new List<Vector4>();   // pass 7: (x, z, nx, nz) samples along the rock sections, for the 3D rip-rap scatter
        var rnd = new System.Random(606);
        float[] off = { -0.9f, 0f, 1.0f, 2.6f }, vv = { 0f, 0.33f, 0.62f, 1f };
        foreach (var pl in lines)
        {
            float u = 0; bool grass = false; float nextSwitch = 0;
            bool onIsland = false; if (pl.Count > 0) { Vector2 c0 = Vector2.zero; foreach (var q0 in pl) c0 += new Vector2(q0.x, q0.y); c0 /= pl.Count; foreach (var ic in isl) if (Vector2.Distance(c0, ic) < 90f) onIsland = true; }
            int baseRow = -1;
            for (int k = 0; k < pl.Count; k++)
            {
                var q = pl[k]; Vector2 p = new Vector2(q.x, q.y), n = new Vector2(q.z, q.w).normalized;
                if (k > 0) u += Vector2.Distance(p, new Vector2(pl[k - 1].x, pl[k - 1].y)) / 4f;
                if (u * 4f >= nextSwitch)
                {
                    float nearHouse = float.MaxValue;
                    foreach (var hs in v.houses) nearHouse = Mathf.Min(nearHouse, Vector2.Distance(p, new Vector2(hs.shore[0], hs.shore[1])));
                    double r = rnd.NextDouble();
                    grass = nearHouse < 60f ? r < 0.75 : r < 0.3;
                    nextSwitch = u * 4f + 30f + (float)rnd.NextDouble() * 25f;
                }
                if (!grass && !onIsland && !NearLotDock(p)) rockPts.Add(new Vector4(p.x, p.y, n.x, n.y));
                int row = verts.Count;
                for (int c = 0; c < 4; c++)
                {
                    Vector2 w = p + n * off[c];
                    float y = c == 0 ? -0.35f : c == 1 ? 0.07f : c == 2 ? Mathf.Max(H(w.x, w.y), 0.12f) + 0.16f : H(w.x, w.y) + 0.04f;   // c2 = bank lip
                    verts.Add(new Vector3(w.x, y, w.y)); uvs.Add(new Vector2(u, vv[c]));
                }
                if (baseRow >= 0)
                {
                    var t = onIsland ? tMud : grass ? tGrass : tRock;
                    for (int c = 0; c < 3; c++)
                    {
                        int a = baseRow + c, b = baseRow + c + 1, a2 = row + c, b2 = row + c + 1;
                        // winding chosen so the face points up for either normal orientation (checked below)
                        t.Add(a); t.Add(b); t.Add(a2); t.Add(b); t.Add(b2); t.Add(a2);
                    }
                }
                baseRow = row;
            }
        }
        // fix winding per triangle: face normal must point up
        foreach (var t in new[] { tRock, tGrass, tMud })
            for (int k = 0; k < t.Count; k += 3)
            {
                Vector3 nn = Vector3.Cross(verts[t[k + 1]] - verts[t[k]], verts[t[k + 2]] - verts[t[k]]);
                if (nn.y < 0) { int tmp = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = tmp; }
            }
        var m = new Mesh { name = "shoreline_edging", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.subMeshCount = 3; m.SetTriangles(tRock, 0); m.SetTriangles(tGrass, 1); m.SetTriangles(tMud, 2);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        SaveMesh(m, m.name);
        var g = MeshObj("ShorelineEdging", root, m, rockM, grassM, mudM);
        var mr = g.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
        g.isStatic = true;
        Debug.Log($"[SkiView] shoreline edging: {lines.Count} lines, {verts.Count} verts, rock tris {tRock.Count / 3}, grass tris {tGrass.Count / 3}, island mud tris {tMud.Count / 3}");
        ScatterRocks(terrain, rockPts);
    }

    /// <summary>Pass 7: real 3D rip-rap along the rock sections. Four angular rock variants are added as terrain tree prototypes,
    /// so the terrain draws them GPU-instanced and culls them by distance. Rocks sit from just under the waterline up to the bank lip,
    /// partly buried, at random sizes and yaws.</summary>
    static void ScatterRocks(Terrain terrain, List<Vector4> pts)
    {
        if (terrain == null || pts.Count == 0) return;
        var mat = EnvMat("RipRap", EnvTex("shore_color.jpg", false, 1024, false, 512), EnvTex("shore_normal.png", true, 512, false, 256), new Color(0.32f, 0.28f, 0.25f), 0.05f, Vector2.one, spec: true);
        var td = terrain.terrainData;
        var protos = new List<TreePrototype>(td.treePrototypes);
        int first = protos.Count;
        for (int k = 0; k < 4; k++) protos.Add(new TreePrototype { prefab = RockPrefab("RipRapRock_" + (char)('A' + k), mat, 900 + k * 37) });
        td.treePrototypes = protos.ToArray(); td.RefreshPrototypes();
        var list = new List<TreeInstance>(td.treeInstances);
        Vector3 tp = terrain.transform.position, size = td.size;
        var rnd = new System.Random(1717);
        float Fr() => (float)rnd.NextDouble();
        int n0 = list.Count;
        foreach (var q in pts)
        {
            Vector2 p = new Vector2(q.x, q.y), n = new Vector2(q.z, q.w), t = new Vector2(-n.y, n.x);
            int per = 2 + (Fr() < 0.5f ? 1 : 0);   // samples are 1.5 m apart -> ~1.7-2 rocks per metre of rock shore
            for (int j = 0; j < per; j++)
            {
                float across = Mathf.Lerp(-0.7f, 1.5f, Mathf.Pow(Fr(), 0.8f)), along = (Fr() - 0.5f) * 1.5f;
                Vector2 w = p + n * across + t * along;
                float nx = (w.x - tp.x) / size.x, nz = (w.y - tp.z) / size.z;
                if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
                float sz = Fr() < 0.10f ? 0.7f + Fr() * 0.4f : 0.3f + Fr() * 0.4f;   // a few big boulders among cobble-sized rip-rap
                list.Add(new TreeInstance { position = new Vector3(nx, 0, nz), heightScale = sz * (0.8f + Fr() * 0.4f), widthScale = sz,
                    rotation = Fr() * Mathf.PI * 2f, prototypeIndex = first + rnd.Next(4), color = new Color(0.72f, 0.68f, 0.62f), lightmapColor = new Color(0.72f, 0.68f, 0.62f) });  // b10.23: far tree-line flecks
            }
        }
        td.SetTreeInstances(list.ToArray(), true);
        EditorUtility.SetDirty(td);
        Debug.Log($"[SkiView] rip-rap rocks: {list.Count - n0} instances on {pts.Count} rock-shore samples (4 variants, terrain-instanced)");
    }

    /// <summary>Angular rock, ~1 m wide template: displaced icosphere (80 faces), flattened, flat-shaded, box-projected UVs.
    /// The pivot is ~30% up from the bottom so each rock sits partly buried.</summary>
    static GameObject RockPrefab(string name, Material mat, int seed)
    {
        var r = new System.Random(seed);
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var pv = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        var f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        var mid = new Dictionary<long, int>();
        int Mid(int a, int b) { long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; if (mid.TryGetValue(key, out int i)) return i; pv.Add((pv[a] + pv[b]) * 0.5f); mid[key] = pv.Count - 1; return pv.Count - 1; }
        var f2 = new List<int>();
        for (int i = 0; i < f.Count; i += 3) { int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a); f2.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca }); }
        Vector3 sc = new Vector3(0.5f, 0.32f + (float)r.NextDouble() * 0.1f, 0.38f + (float)r.NextDouble() * 0.1f);
        Vector3 bias = new Vector3((float)r.NextDouble() - 0.5f, 0, (float)r.NextDouble() - 0.5f);
        for (int i = 0; i < pv.Count; i++)
        {
            Vector3 d = pv[i].normalized;
            float k = 0.78f + (float)r.NextDouble() * 0.34f + 0.15f * Vector3.Dot(d, bias);
            Vector3 v = Vector3.Scale(d * k, sc);
            if (v.y < -0.12f) v.y = -0.12f - (v.y + 0.12f) * 0.25f;   // flattened base
            pv[i] = v + Vector3.up * 0.08f;
        }
        var verts = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
        for (int i = 0; i < f2.Count; i += 3)
        {
            Vector3 a = pv[f2[i]], b = pv[f2[i + 1]], c = pv[f2[i + 2]];
            Vector3 nn = Vector3.Cross(b - a, c - a); if (Vector3.Dot(nn, (a + b + c) / 3f - Vector3.up * 0.08f) < 0) { var tmp = b; b = c; c = tmp; nn = -nn; }
            Vector3 an = new Vector3(Mathf.Abs(nn.x), Mathf.Abs(nn.y), Mathf.Abs(nn.z));
            Vector2 U(Vector3 p) => an.y >= an.x && an.y >= an.z ? new Vector2(p.x, p.z) : an.x >= an.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
            int k0 = verts.Count; verts.Add(a); verts.Add(b); verts.Add(c);
            Vector2 o = new Vector2(seed * 0.137f % 1f, seed * 0.291f % 1f);
            uv.Add(U(a) * 1.4f + o); uv.Add(U(b) * 1.4f + o); uv.Add(U(c) * 1.4f + o);
            tris.Add(k0); tris.Add(k0 + 1); tris.Add(k0 + 2);
        }
        var mesh = new Mesh { name = "rock_" + name };
        mesh.SetVertices(verts); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        SaveMesh(mesh, mesh.name);
        var go = new GameObject(name);
        var l0 = MeshObj("LOD0", go.transform, mesh, mat);
        l0.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        var lg = go.AddComponent<LODGroup>();
        lg.SetLODs(new[] { new LOD(0.006f, new Renderer[] { l0.GetComponent<Renderer>() }) });   // culled when tiny (~250 m for a 1 m rock)
        lg.RecalculateBounds();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ENVPRE + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }
}
