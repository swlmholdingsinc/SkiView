using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pass 6 tree-line variety: red alder and bigleaf maple card trees (early-fall tint on the maple) and a far ring of
/// forested hills with a ragged skyline fringe behind the terrain, which gives a second, hazier layer.</summary>
public static partial class SkiViewBuilder
{
    /// <summary>Broadleaf tree, 16 m template: tapered trunk + 3 limbs, ~46 leaf-cluster cards on a lumpy ellipsoid crown.</summary>
    static CardMesh DeciduousCards(bool maple, int seed)
    {
        var r = new System.Random(seed);
        float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var m = new CardMesh();
        Vector3 cc = new Vector3(0, maple ? 10.2f : 10.8f, 0);
        Vector3 rad = maple ? new Vector3(5.0f, 4.4f, 5.0f) : new Vector3(4.2f, 4.8f, 4.2f);
        Func<Vector3, Vector3> crownN = p => (Vector3.Scale(p - cc, new Vector3(1, 0.8f, 1)).normalized * 0.7f + Vector3.up * 0.5f).normalized;
        void Cyl(Vector3 a, Vector3 b, float ra, float rb, int sides)
        {
            Vector3 ax = (b - a).normalized, s1 = Vector3.Cross(ax, Vector3.forward).sqrMagnitude > 0.01f ? Vector3.Cross(ax, Vector3.forward).normalized : Vector3.right, s2 = Vector3.Cross(ax, s1);
            float len = (b - a).magnitude;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 d0 = s1 * Mathf.Cos(a0) + s2 * Mathf.Sin(a0), d1 = s1 * Mathf.Cos(a1) + s2 * Mathf.Sin(a1);
                int k = m.v.Count;
                m.v.Add(a + d0 * ra); m.v.Add(b + d0 * rb); m.v.Add(b + d1 * rb); m.v.Add(a + d1 * ra);
                float u0 = (float)i / sides, u1 = (float)(i + 1) / sides, vt = len / 2.5f;
                m.uv.Add(new Vector2(u0, 0)); m.uv.Add(new Vector2(u0, vt)); m.uv.Add(new Vector2(u1, vt)); m.uv.Add(new Vector2(u1, 0));
                m.n.Add(d0); m.n.Add(d0); m.n.Add(d1); m.n.Add(d1);
                m.trunk.Add(k); m.trunk.Add(k + 1); m.trunk.Add(k + 2); m.trunk.Add(k); m.trunk.Add(k + 2); m.trunk.Add(k + 3);
            }
        }
        Vector3 fork = new Vector3(Rnd(-0.3f, 0.3f), 5.2f, Rnd(-0.3f, 0.3f));
        Cyl(Vector3.down * 0.4f, fork, 0.32f, 0.22f, 7);
        for (int l = 0; l < 3; l++)
        {
            float az = l * 2.1f + Rnd(-0.4f, 0.4f);
            Vector3 end = cc + new Vector3(Mathf.Cos(az) * rad.x * 0.45f, Rnd(-0.5f, 2.5f), Mathf.Sin(az) * rad.z * 0.45f);
            Cyl(fork, end, 0.16f, 0.05f, 5);
        }
        // lumpy crown: 5 sub-lobes, cards on their surfaces facing outward
        var lobes = new List<Vector3>();
        for (int l = 0; l < 5; l++) lobes.Add(cc + new Vector3(Rnd(-0.45f, 0.45f) * rad.x, Rnd(-0.3f, 0.45f) * rad.y, Rnd(-0.45f, 0.45f) * rad.z));
        int cards = maple ? 48 : 44;
        for (int c = 0; c < cards; c++)
        {
            Vector3 lc = lobes[c % lobes.Count];
            Vector3 dir = new Vector3(Rnd(-1, 1), Rnd(-0.7f, 1), Rnd(-1, 1)).normalized;
            Vector3 p = lc + Vector3.Scale(dir, rad * 0.62f) * Rnd(0.6f, 1.0f);
            Vector3 face = ((p - cc).normalized + new Vector3(Rnd(-0.5f, 0.5f), Rnd(-0.3f, 0.6f), Rnd(-0.5f, 0.5f))).normalized;
            Vector3 t1 = Vector3.Cross(face, Vector3.up); if (t1.sqrMagnitude < 0.01f) t1 = Vector3.right; t1.Normalize();
            t1 = Quaternion.AngleAxis(Rnd(-35, 35), face) * t1; Vector3 t2 = Vector3.Cross(t1, face).normalized;
            float s = Rnd(2.6f, 3.9f) * 0.5f;
            Quad(m, m.leaf, p - t1 * s - t2 * s, p - t1 * s + t2 * s, p + t1 * s + t2 * s, p + t1 * s - t2 * s,
                 new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), crownN);
        }
        // core: 2 crossed impostor planes for density
        for (int c = 0; c < 2; c++)
        {
            float a = c * Mathf.PI / 2 + 0.6f; Vector3 sv = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (16f * 0.25f * 0.8f);
            Quad(m, m.core, -sv, -sv + Vector3.up * 16f, sv + Vector3.up * 16f, sv, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), crownN);
        }
        return m;
    }

    static GameObject DeciduousPrefab(string species, bool maple, Material bark, Material leaves, Material imp, int seed)
    {
        var cm = DeciduousCards(maple, seed);
        var mesh = new Mesh { name = "tree_" + species + "_lod0" };
        mesh.SetVertices(cm.v); mesh.SetNormals(cm.n); mesh.SetUVs(0, cm.uv);
        mesh.subMeshCount = 3; mesh.SetTriangles(cm.trunk, 0); mesh.SetTriangles(cm.leaf, 1); mesh.SetTriangles(cm.core, 2);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        SaveMesh(mesh, mesh.name);
        var lod1 = ImpostorMesh("tree_" + species + "_lod1", false);
        var go = new GameObject(species);
        var l0 = MeshObj("LOD0", go.transform, mesh, bark, leaves, imp);
        var l1 = MeshObj("LOD1", go.transform, lod1, imp);
        var lg = go.AddComponent<LODGroup>();
        lg.SetLODs(new[] { new LOD(0.18f, new Renderer[] { l0.GetComponent<Renderer>() }), new LOD(0.0015f, new Renderer[] { l1.GetComponent<Renderer>() }) });
        lg.RecalculateBounds();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ENVPRE + species + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>Pass 7 understory shrub, 2.4 m template: ~30 leaf cards on a lumpy low dome (salal / vine maple / young conifer),
    /// so the base of each stand is a dark, filled mass instead of bare trunks. LOD1 = 2 crossed cards, culled when tiny.</summary>
    static GameObject ShrubPrefab(string name, Material bark, int seed)
    {
        bool conifer = name.EndsWith("_B");
        var leaves = conifer
            ? EnvMat("UnderstoryConifer", EnvTex("cedar_spray.png", false, 512, true, 256), null, new Color(0.50f, 0.58f, 0.46f), 0.12f, Vector2.one, true, 0.42f, true, spec: false)
            : EnvMat("UnderstoryLeaves", EnvTex("alder_leaves.png", false, 512, true, 256), null, new Color(0.52f, 0.62f, 0.46f), 0.14f, Vector2.one, true, 0.45f, true, spec: false);
        var r = new System.Random(seed);
        float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var m = new CardMesh();
        Vector3 cc = new Vector3(0, 0.9f, 0); Vector3 rad = new Vector3(1.7f, 1.5f, 1.7f);
        Func<Vector3, Vector3> nrm = p => (Vector3.Scale(p - cc, new Vector3(1, 0.7f, 1)).normalized * 0.65f + Vector3.up * 0.55f).normalized;
        var lobes = new List<Vector3>();
        for (int l = 0; l < 4; l++) lobes.Add(cc + new Vector3(Rnd(-0.5f, 0.5f) * rad.x, Rnd(-0.1f, 0.4f) * rad.y, Rnd(-0.5f, 0.5f) * rad.z));
        for (int c = 0; c < 30; c++)
        {
            Vector3 lc = lobes[c % lobes.Count];
            Vector3 dir = new Vector3(Rnd(-1, 1), Rnd(-0.2f, 1), Rnd(-1, 1)).normalized;
            Vector3 p = lc + Vector3.Scale(dir, rad * 0.55f) * Rnd(0.5f, 1.0f); p.y = Mathf.Max(p.y, 0.35f);
            Vector3 face = ((p - cc).normalized + new Vector3(Rnd(-0.5f, 0.5f), Rnd(-0.2f, 0.6f), Rnd(-0.5f, 0.5f))).normalized;
            Vector3 t1 = Vector3.Cross(face, Vector3.up); if (t1.sqrMagnitude < 0.01f) t1 = Vector3.right; t1.Normalize();
            t1 = Quaternion.AngleAxis(Rnd(-40, 40), face) * t1; Vector3 t2 = Vector3.Cross(t1, face).normalized;
            float s = Rnd(0.9f, 1.4f) * 0.5f;
            Quad(m, m.leaf, p - t1 * s - t2 * s, p - t1 * s + t2 * s, p + t1 * s + t2 * s, p + t1 * s - t2 * s,
                 new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), nrm);
        }
        for (int c = 0; c < 2; c++)
        {
            float a = c * Mathf.PI / 2 + 0.4f; Vector3 sv = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.6f;
            Quad(m, m.core, -sv + Vector3.down * 0.2f, -sv + Vector3.up * 2.3f, sv + Vector3.up * 2.3f, sv + Vector3.down * 0.2f, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), nrm);
        }
        var mesh = new Mesh { name = "shrub_" + name + "_lod0" };
        mesh.SetVertices(m.v); mesh.SetNormals(m.n); mesh.SetUVs(0, m.uv);
        var all = new List<int>(m.leaf); all.AddRange(m.core);
        mesh.SetTriangles(all, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        SaveMesh(mesh, mesh.name);
        var lm = new Mesh { name = "shrub_" + name + "_lod1" };
        lm.SetVertices(m.v); lm.SetNormals(m.n); lm.SetUVs(0, m.uv); lm.SetTriangles(m.core, 0); lm.RecalculateBounds();
        SaveMesh(lm, lm.name);
        var go = new GameObject(name);
        var l0 = MeshObj("LOD0", go.transform, mesh, leaves);
        var l1 = MeshObj("LOD1", go.transform, lm, leaves);
        l1.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var lg = go.AddComponent<LODGroup>();
        lg.SetLODs(new[] { new LOD(0.025f, new Renderer[] { l0.GetComponent<Renderer>() }), new LOD(0.004f, new Renderer[] { l1.GetComponent<Renderer>() }) });
        lg.RecalculateBounds();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ENVPRE + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>Second layer: ring of forested hills 1.15-2.6 km out, rising above the near fir line, with a ragged fringe on the crest.
    /// Exp2 fog turns it hazy blue-green (aerial perspective).</summary>
    static void BuildHills(Transform root)
    {
        // pass 7: rings pulled in (fog is exp2, so 2 km+ was almost fully hazed out) and a darker canopy for more contrast against the haze
        int seg = 360; float[] rr = { 1000f, 1150f, 1300f, 1550f, 1950f };
        var rnd = new System.Random(2026);
        float[] ph = new float[12]; for (int i = 0; i < ph.Length; i++) ph[i] = (float)rnd.NextDouble() * 6.283f;
        float Noise(float th, int o) { float s = 0; for (int k = 0; k < 4; k++) s += Mathf.Sin(th * (2 + k * 3 + o) + ph[(k + o * 4) % 12]) / (1 + k); return s * 0.5f; }
        float Hgt(int ring, float th) => ring switch
        {
            0 => -8f,
            1 => 40f + 26f * Noise(th, 0),
            2 => 100f + 58f * Noise(th, 1),
            3 => 150f + 78f * Noise(th, 2),
            _ => 100f,
        };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float th = i * Mathf.PI * 2 / seg;
            for (int k = 0; k < rr.Length; k++)
            {
                v.Add(new Vector3(Mathf.Cos(th) * rr[k], Hgt(k, th), Mathf.Sin(th) * rr[k]));
                uv.Add(new Vector2(i * 0.5f, k * 2.5f));
            }
        }
        int R = rr.Length;
        for (int i = 0; i < seg; i++)
            for (int k = 0; k < R - 1; k++)
            {
                int a = i * R + k, b = a + 1, c = a + R, d = c + 1;
                t.Add(a); t.Add(b); t.Add(c); t.Add(b); t.Add(d); t.Add(c);
            }
        var m = new Mesh { name = "far_hills" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        // ensure faces point toward the lake (inward/up)
        var tri = m.triangles; var mv = m.vertices;
        for (int k = 0; k < tri.Length; k += 3) { Vector3 n = Vector3.Cross(mv[tri[k + 1]] - mv[tri[k]], mv[tri[k + 2]] - mv[tri[k]]); Vector3 c = (mv[tri[k]] + mv[tri[k + 1]] + mv[tri[k + 2]]) / 3; if (Vector3.Dot(n, -new Vector3(c.x, 0, c.z).normalized + Vector3.up * 0.2f) < 0) { int tmp = tri[k + 1]; tri[k + 1] = tri[k + 2]; tri[k + 2] = tmp; } }
        m.triangles = tri; m.RecalculateNormals(); m.RecalculateBounds();
        SaveMesh(m, m.name);
        var hm = EnvMat("HillsForest", EnvTex("hills_forest.png", false, 512, false, 256), null, new Color(0.55f, 0.62f, 0.56f), 0.05f, Vector2.one, spec: false);
        var g = MeshObj("FarHills", root, m, hm);
        var r = g.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;

        // skyline fringe on ring 2 and 3 crests: vertical alpha-clipped strips (ragged tree tops)
        var fv = new List<Vector3>(); var fuv = new List<Vector2>(); var ft = new List<int>();
        foreach (int k in new[] { 2, 3 })
        {
            float hgt = k == 2 ? 30f : 40f, u = 0;
            for (int i = 0; i <= seg; i++)
            {
                float th = i * Mathf.PI * 2 / seg;
                Vector3 p = new Vector3(Mathf.Cos(th) * rr[k], Hgt(k, th) - 6f, Mathf.Sin(th) * rr[k]);
                if (i > 0) u += (p - fv[fv.Count - 2]).magnitude / 260f;
                fv.Add(p); fv.Add(p + Vector3.up * hgt); fuv.Add(new Vector2(u, 0)); fuv.Add(new Vector2(u, 1));
                if (i > 0) { int a = fv.Count - 4; ft.Add(a); ft.Add(a + 1); ft.Add(a + 2); ft.Add(a + 1); ft.Add(a + 3); ft.Add(a + 2); }
            }
        }
        var fm = new Mesh { name = "skyline_fringe" }; fm.SetVertices(fv); fm.SetUVs(0, fuv); fm.SetTriangles(ft, 0);
        var fn = new Vector3[fv.Count]; for (int i = 0; i < fn.Length; i++) fn[i] = (-new Vector3(fv[i].x, 0, fv[i].z).normalized + Vector3.up).normalized;
        fm.normals = fn; fm.RecalculateBounds(); SaveMesh(fm, fm.name);
        var fmat = EnvMat("SkylineFringe", EnvTex("skyline_fringe.png", false, 2048, true, 1024, true), null, new Color(0.55f, 0.62f, 0.56f), 0.05f, Vector2.one, true, 0.5f, true, spec: false);
        var fg = MeshObj("SkylineFringe", root, fm, fmat);
        var fr = fg.GetComponent<MeshRenderer>(); fr.shadowCastingMode = ShadowCastingMode.Off; fr.receiveShadows = false;
    }
}
