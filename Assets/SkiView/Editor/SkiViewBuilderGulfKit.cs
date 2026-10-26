using System;
using System.Collections.Generic;
using System.Linq;
using SkiView;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pass 17: Gulf-coast lake kit (Saltwhite). Slash pine / sabal palm / royal palm / saw palmetto / sea-oat prefabs (shared
/// kit, Environment/Prefabs), the flat pine-flatwoods treeline backdrop, and the estate buildings (stucco houses with hip tile
/// roofs, lanai, screened pool cage), timber docks, the community dock and generic tournament dressing (tents, judges' tower,
/// scoreboard, inflatable arch; no logos or text). All textures are procedural or CC0 (Tools/gen_gulf_textures.py).</summary>
public static partial class SkiViewBuilder
{
    // gulf prototype order (LakeTrees gulf)
    public const int G_PINE = 0, G_PINE_B = 1, G_SABAL = 2, G_ROYAL = 3, G_PALMETTO = 4, G_SEAOATS = 5, G_HEDGE = 6;

    static Mesh GulfImpostor(string name, float H, float wr)
    {
        var m = new CardMesh(); float half = H * wr * 0.5f;
        Func<Vector3, Vector3> nrm = p => (new Vector3(p.x, 0.3f * (p.y - H * 0.6f), p.z).normalized * 0.6f + Vector3.up * 0.55f).normalized;
        for (int c = 0; c < 3; c++)
        {
            float a = c * Mathf.PI / 3 + 0.3f; Vector3 s = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * half;
            Quad(m, m.core, -s + Vector3.down * 0.3f, -s + Vector3.up * H, s + Vector3.up * H, s + Vector3.down * 0.3f, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), nrm);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(m.v); mesh.SetNormals(m.n); mesh.SetUVs(0, m.uv); mesh.SetTriangles(m.core, 0); mesh.RecalculateBounds();
        return SaveMesh(mesh, name);
    }

    /// <summary>Tapered n-gon trunk from y0 to y1 along a gently bent axis (into m.trunk), u around, v up in metres / vTile.</summary>
    static void Trunk(CardMesh m, float y0, float y1, float r0, float r1, int sides, int rings, Vector3 bend, float vTile)
    {
        int b0 = m.v.Count;
        for (int j = 0; j <= rings; j++)
        {
            float t = j / (float)rings, y = Mathf.Lerp(y0, y1, t), r = Mathf.Lerp(r0, r1, t);
            Vector3 c = new Vector3(0, y, 0) + bend * (t * t);
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides; Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                m.v.Add(c + d * r); m.n.Add(d); m.uv.Add(new Vector2(i / (float)sides * 2f, y / vTile));
            }
        }
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < sides; i++)
            {
                int a = b0 + j * (sides + 1) + i, b = a + 1, c = a + sides + 1, d = c + 1;
                m.trunk.Add(a); m.trunk.Add(c); m.trunk.Add(b); m.trunk.Add(b); m.trunk.Add(c); m.trunk.Add(d);
            }
    }

    static GameObject GulfTreePrefab(string name, CardMesh cm, Material trunk, Material leaf, Material imp, float H, float lod0, bool shadowLod1 = false)
    {
        var mesh = new Mesh { name = "tree_" + name + "_lod0" };
        mesh.SetVertices(cm.v); mesh.SetNormals(cm.n); mesh.SetUVs(0, cm.uv);
        mesh.subMeshCount = 2; mesh.SetTriangles(cm.trunk, 0); mesh.SetTriangles(cm.leaf, 1);
        mesh.RecalculateBounds(); mesh.RecalculateTangents(); SaveMesh(mesh, mesh.name);
        var lod1 = GulfImpostor("tree_" + name + "_lod1", H, 0.5f);
        var go = new GameObject(name);
        var l0 = MeshObj("LOD0", go.transform, mesh, trunk, leaf);
        var l1 = MeshObj("LOD1", go.transform, lod1, imp);
        if (!shadowLod1) l1.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        var lg = go.AddComponent<LODGroup>();
        lg.SetLODs(new[] { new LOD(lod0, new Renderer[] { l0.GetComponent<Renderer>() }), new LOD(0.0015f, new Renderer[] { l1.GetComponent<Renderer>() }) });
        lg.RecalculateBounds();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ENVPRE + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>Slash pine (20 m template): tall bare, slightly bent trunk; irregular flat-topped crown of needle tufts in the top third.</summary>
    static GameObject SlashPinePrefab(string name, int seed, Material bark, Material tuft, Material imp)
    {
        var r = new System.Random(seed); float Rn(float a, float b) => a + (float)r.NextDouble() * (b - a);
        const float H = 20f; var m = new CardMesh();
        Vector3 bend = new Vector3(Rn(-0.7f, 0.7f), 0, Rn(-0.7f, 0.7f));
        Trunk(m, -0.3f, H * 0.96f, 0.24f, 0.07f, 7, 6, bend, 1.6f);
        Vector3 Axis(float y) { float t = Mathf.Clamp01((y + 0.3f) / (H * 0.96f + 0.3f)); return new Vector3(0, y, 0) + bend * (t * t); }
        int nb = 7 + r.Next(4);
        for (int k = 0; k < nb; k++)
        {
            float y = Rn(H * 0.62f, H * 0.93f), a = Rn(0, Mathf.PI * 2), len = Mathf.Lerp(3.4f, 1.2f, (y - H * 0.62f) / (H * 0.31f)) * Rn(0.8f, 1.2f);
            Vector3 dir = new Vector3(Mathf.Cos(a), Rn(0.15f, 0.45f), Mathf.Sin(a)).normalized, p0 = Axis(y), p1 = p0 + dir * len;
            // limb: thin 4-sided prism (bark)
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * 0.06f, up = Vector3.Cross(side, dir).normalized * 0.06f;
            foreach (var (s1, s2) in new[] { (side, up), (up, -side), (-side, -up), (-up, side) })
                Quad(m, m.trunk, p0 + s1, p1 + s1 * 0.4f, p1 + s2 * 0.4f, p0 + s2, new Vector2(0, 0), new Vector2(0, len / 1.6f), new Vector2(0.25f, len / 1.6f), new Vector2(0.25f, 0), q => (q - (p0 + p1) * 0.5f).normalized);
            // tufts along the outer half of the limb and at its tip
            int nt = 2 + r.Next(2);
            for (int j = 0; j < nt; j++)
            {
                Vector3 c = Vector3.Lerp(p0, p1, Rn(0.55f, 1.05f)) + new Vector3(Rn(-0.3f, 0.3f), Rn(0f, 0.4f), Rn(-0.3f, 0.3f));
                float s = Rn(1.1f, 1.6f);
                Vector3 ctr = Axis(H * 0.8f);
                Func<Vector3, Vector3> nrm = q => ((q - ctr).normalized * 0.65f + Vector3.up * 0.5f).normalized;
                for (int q = 0; q < 2; q++)
                {
                    float qa = a + q * Mathf.PI / 2 + Rn(-0.3f, 0.3f); Vector3 sv = new Vector3(Mathf.Cos(qa), 0, Mathf.Sin(qa)) * s;
                    Quad(m, m.leaf, c - sv + Vector3.down * s * 0.7f, c - sv + Vector3.up * s * 0.7f, c + sv + Vector3.up * s * 0.7f, c + sv + Vector3.down * s * 0.7f, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), nrm);
                }
                Vector3 hx = new Vector3(s, 0, 0), hz = new Vector3(0, 0, s);
                Quad(m, m.leaf, c - hx - hz, c - hx + hz, c + hx + hz, c + hx - hz, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), nrm);
            }
        }
        // top tuft cluster
        Vector3 top = Axis(H * 0.95f);
        for (int q = 0; q < 3; q++)
        {
            float qa = q * Mathf.PI / 3; Vector3 sv = new Vector3(Mathf.Cos(qa), 0, Mathf.Sin(qa)) * 1.5f;
            Quad(m, m.leaf, top - sv + Vector3.down * 0.9f, top - sv + Vector3.up * 1.0f, top + sv + Vector3.up * 1.0f, top + sv + Vector3.down * 0.9f, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), q2 => Vector3.up);
        }
        return GulfTreePrefab(name, m, bark, tuft, imp, H, 0.14f);
    }

    /// <summary>Palms. royal: 16 m smooth pale-grey trunk, swollen low, green crownshaft, ~15 arching pinnate fronds.
    /// sabal: 10 m rough grey-brown trunk, round crown of ~18 costapalmate fan leaves on petioles, a few drooping.</summary>
    static GameObject PalmPrefab(string name, bool royal, int seed, Material trunk, Material frond, Material imp, Material shaft)
    {
        var r = new System.Random(seed); float Rn(float a, float b) => a + (float)r.NextDouble() * (b - a);
        float H = royal ? 16f : 10f; var m = new CardMesh();
        Vector3 bend = new Vector3(Rn(-0.5f, 0.5f), 0, Rn(-0.5f, 0.5f)) * (royal ? 0.4f : 1.2f);
        if (royal)
        {
            Trunk(m, -0.3f, H * 0.3f, 0.34f, 0.38f, 9, 3, bend * 0.1f, 1.2f);
            Trunk(m, H * 0.3f, H * 0.80f, 0.38f, 0.27f, 9, 4, bend * 0.6f, 1.2f);
        }
        else Trunk(m, -0.3f, H * 0.93f, 0.26f, 0.23f, 8, 6, bend, 0.9f);
        Vector3 top = new Vector3(0, H * (royal ? 0.93f : 0.95f), 0) + bend;
        var sub = new List<int>();   // crownshaft (royal) into the leaf submesh later via its own material slot: emulate by trunk ring
        if (royal)
        {
            // crownshaft: smooth green column
            int k0 = m.trunk.Count; Trunk(m, H * 0.80f, H * 0.93f, 0.28f, 0.25f, 9, 2, bend * 0.0f, 0.8f);
            for (int i = m.v.Count - 3 * 10; i < m.v.Count; i++) m.v[i] += bend;
            sub.AddRange(m.trunk.GetRange(k0, m.trunk.Count - k0)); m.trunk.RemoveRange(k0, m.trunk.Count - k0);
        }
        int nf = royal ? 15 : 18;
        for (int f = 0; f < nf; f++)
        {
            float a = f * Mathf.PI * 2 / nf * 2.618f + Rn(-0.2f, 0.2f);
            float elev = royal ? Mathf.Lerp(55f, -30f, (f % 5) / 4f) + Rn(-8f, 8f) : Mathf.Lerp(65f, -45f, (f % 6) / 5f) + Rn(-8f, 8f);
            Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            if (royal)
            {
                float L = Rn(3.6f, 4.4f), w = 0.55f; int seg = 4;
                Vector3 p = top, dirv = (d * Mathf.Cos(elev * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(elev * Mathf.Deg2Rad)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                for (int s = 0; s < seg; s++)
                {
                    Vector3 dn = (dirv + Vector3.down * (0.32f * (s + 1))).normalized, p2 = p + dn * (L / seg);
                    float w0 = w * (s == 0 ? 0.5f : 1f), w1 = w * (s == seg - 1 ? 0.35f : 1f);
                    Vector3 nrm = Vector3.Cross(dn, side).normalized; if (nrm.y < 0) nrm = -nrm;
                    Vector3 droop = Vector3.down * 0.18f;   // leaflets hang: a shallow V
                    Quad(m, m.leaf, p - side * w0 + droop, p2 - side * w1 + droop, p2 + side * w1 + droop, p + side * w0 + droop,
                         new Vector2(s / (float)seg, 0), new Vector2((s + 1) / (float)seg, 0), new Vector2((s + 1) / (float)seg, 1), new Vector2(s / (float)seg, 1), q => (nrm + Vector3.up * 0.3f).normalized);
                    p = p2; dirv = dn;
                }
            }
            else
            {
                float pet = Rn(0.9f, 1.4f), S = Rn(1.3f, 1.7f);
                Vector3 dirv = (d * Mathf.Cos(elev * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(elev * Mathf.Deg2Rad)).normalized;
                Vector3 b = top + dirv * pet, side = Vector3.Cross(Vector3.up, d).normalized;
                Vector3 fdir = (dirv + Vector3.down * 0.25f).normalized, tip = b + fdir * S * 1.4f;
                Vector3 nrm = Vector3.Cross(fdir, side).normalized; if (nrm.y < 0) nrm = -nrm;
                // petiole (thin card, trunk material) + fan (bottom-centre of the texture at the petiole end)
                Quad(m, m.trunk, top - side * 0.05f, b - side * 0.04f, b + side * 0.04f, top + side * 0.05f, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.1f, 1), new Vector2(0.1f, 0), q => Vector3.up);
                Quad(m, m.leaf, b - side * 0.15f, tip - side * S, tip + side * S, b + side * 0.15f, new Vector2(0.45f, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.55f, 0), q => (nrm + Vector3.up * 0.35f).normalized);
                // second, folded half so the fan isn't a flat sheet
                Vector3 tip2 = tip + Vector3.up * 0.35f;
                Quad(m, m.leaf, b, tip2 - side * S * 0.6f, tip2 + side * S * 0.6f, b + side * 0.01f, new Vector2(0.48f, 0), new Vector2(0.2f, 0.9f), new Vector2(0.8f, 0.9f), new Vector2(0.52f, 0), q => (nrm + Vector3.up * 0.5f).normalized);
            }
        }
        // mesh with 3 submeshes for royal (trunk, fronds, crownshaft)
        var mesh = new Mesh { name = "tree_" + name + "_lod0" };
        mesh.SetVertices(m.v); mesh.SetNormals(m.n); mesh.SetUVs(0, m.uv);
        mesh.subMeshCount = royal ? 3 : 2; mesh.SetTriangles(m.trunk, 0); mesh.SetTriangles(m.leaf, 1); if (royal) mesh.SetTriangles(sub, 2);
        mesh.RecalculateBounds(); mesh.RecalculateTangents(); SaveMesh(mesh, mesh.name);
        var lod1 = GulfImpostor("tree_" + name + "_lod1", H, 0.55f);
        var go = new GameObject(name);
        var l0 = royal ? MeshObj("LOD0", go.transform, mesh, trunk, frond, shaft) : MeshObj("LOD0", go.transform, mesh, trunk, frond);
        var l1 = MeshObj("LOD1", go.transform, lod1, imp);
        var lg = go.AddComponent<LODGroup>();
        lg.SetLODs(new[] { new LOD(0.10f, new Renderer[] { l0.GetComponent<Renderer>() }), new LOD(0.0015f, new Renderer[] { l1.GetComponent<Renderer>() }) });
        lg.RecalculateBounds();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ENVPRE + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>Saw palmetto clump (~1.3 m): 2-3 low crowns of stiff fan leaves on upright petioles. One LOD, culled small.</summary>
    static GameObject PalmettoPrefab(string name, int seed, Material fan)
    {
        var r = new System.Random(seed); float Rn(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var m = new CardMesh();
        int crowns = 3;
        for (int c = 0; c < crowns; c++)
        {
            Vector3 b0 = new Vector3(Rn(-0.6f, 0.6f), 0.05f, Rn(-0.6f, 0.6f));
            for (int f = 0; f < 6; f++)
            {
                float a = Rn(0, Mathf.PI * 2), elev = Rn(30f, 70f), pet = Rn(0.35f, 0.7f), S = Rn(0.38f, 0.55f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), dirv = (d * Mathf.Cos(elev * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(elev * Mathf.Deg2Rad)).normalized;
                Vector3 b = b0 + dirv * pet, side = Vector3.Cross(Vector3.up, d).normalized, tip = b + (dirv + Vector3.up * 0.2f).normalized * S * 1.5f;
                Vector3 nrm = Vector3.Cross((tip - b).normalized, side).normalized; if (nrm.y < 0) nrm = -nrm;
                Quad(m, m.leaf, b - side * 0.06f, tip - side * S, tip + side * S, b + side * 0.06f, new Vector2(0.45f, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.55f, 0), q => (nrm + Vector3.up * 0.4f).normalized);
            }
        }
        var mesh = new Mesh { name = "plant_" + name };
        mesh.SetVertices(m.v); mesh.SetNormals(m.n); mesh.SetUVs(0, m.uv); mesh.SetTriangles(m.leaf, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        SaveMesh(mesh, mesh.name);
        var go = new GameObject(name);
        var l0 = MeshObj("LOD0", go.transform, mesh, fan); l0.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        var lg = go.AddComponent<LODGroup>(); lg.SetLODs(new[] { new LOD(0.008f, new Renderer[] { l0.GetComponent<Renderer>() }) }); lg.RecalculateBounds();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ENVPRE + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject[] GulfPlantKit()
    {
        var bark = AssetDatabase.LoadAssetAtPath<Material>(ENVMAT + "Bark.mat");
        var pineBark = EnvMat("SlashPineBark", bark != null ? (Texture2D)bark.GetTexture("_BaseMap") : null, bark != null ? (Texture2D)bark.GetTexture("_BumpMap") : null, new Color(0.86f, 0.70f, 0.60f), 0.1f, Vector2.one);
        var tuft = EnvMat("SlashPineNeedles", EnvTex("pine_tuft.png", false, 512, true, 256), null, new Color(0.84f, 0.92f, 0.70f), 0.12f, Vector2.one, true, 0.42f, true, spec: false);
        var pineImp = EnvMat("SlashPineImpostor", EnvTex("pine_impostor.png", false, 256, true, 128), null, new Color(0.86f, 0.92f, 0.76f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var palmTrunk = EnvMat("PalmTrunkGrey", EnvTex("stucco_color.jpg", false, 512, false, 256), EnvTex("stucco_normal.png", true, 512, false, 256), new Color(0.66f, 0.64f, 0.60f), 0.12f, new Vector2(1f, 1f), bump: 1.4f);
        var sabalTrunk = EnvMat("SabalTrunk", bark != null ? (Texture2D)bark.GetTexture("_BaseMap") : null, bark != null ? (Texture2D)bark.GetTexture("_BumpMap") : null, new Color(0.62f, 0.58f, 0.54f), 0.08f, Vector2.one);
        var frond = EnvMat("PalmFrond", EnvTex("palm_frond.png", false, 1024, true, 512), null, new Color(0.92f, 1.0f, 0.80f), 0.22f, Vector2.one, true, 0.40f, true, spec: false);
        var fan = EnvMat("SabalFan", EnvTex("fan_leaf.png", false, 512, true, 256), null, new Color(0.86f, 0.96f, 0.78f), 0.2f, Vector2.one, true, 0.42f, true, spec: false);
        var palmetto = EnvMat("SawPalmettoFan", EnvTex("fan_leaf.png", false, 512, true, 256), null, new Color(0.80f, 0.94f, 0.86f), 0.25f, Vector2.one, true, 0.42f, true, spec: false);   // silvery blue-green
        var palmImp = EnvMat("PalmImpostor", EnvTex("palm_impostor.png", false, 256, true, 128), null, new Color(0.9f, 0.95f, 0.85f), 0.06f, Vector2.one, true, 0.45f, true, spec: false);
        var shaft = EnvMat("RoyalCrownshaft", null, null, new Color(0.36f, 0.50f, 0.26f), 0.45f, Vector2.one);
        var oats = EnvMat("SeaOats", EnvTex("seaoats.png", false, 256, true, 128), null, new Color(0.72f, 0.68f, 0.48f), 0.06f, Vector2.one, true, 0.40f, true, spec: false);  // b10.7 Mamta
        return new[]
        {
            SlashPinePrefab("SlashPine", 1701, pineBark, tuft, pineImp), SlashPinePrefab("SlashPine_B", 1702, pineBark, tuft, pineImp),
            PalmPrefab("SabalPalm", false, 1703, sabalTrunk, fan, palmImp, null), PalmPrefab("RoyalPalm", true, 1704, palmTrunk, frond, palmImp, shaft),
            PalmettoPrefab("SawPalmetto", 1705, palmetto), PlantPrefab("SeaOats", oats, 1706, "grass"),
            AssetDatabase.LoadAssetAtPath<GameObject>(ENVPRE + "Understory.prefab"),
        };
    }

    static void GulfTrees(LakeVenue v, Terrain terrain)
    {
        var td = terrain.terrainData;
        var kit = GulfPlantKit();
        var vp = new VegProtos(kit.Select(k => new TreePrototype { prefab = k })); bool veg = VegReady;   // pass 19: vegetation pack
        Vector3 tp = terrain.transform.position, size = td.size;
        var rnd = new System.Random(1717); float Fr() => (float)rnd.NextDouble();
        var list = new List<TreeInstance>(); int[] byKind = new int[8];
        int n = v.treesFlat.Length / 5;
        for (int i = 0; i < n; i++)
        {
            float x = v.treesFlat[i * 5], y = v.treesFlat[i * 5 + 1], z = v.treesFlat[i * 5 + 2], s = v.treesFlat[i * 5 + 3]; int kind = (int)v.treesFlat[i * 5 + 4];
            float nx = (x - tp.x) / size.x, nz = (z - tp.z) / size.z;
            if (nx < 0 || nx > 1 || nz < 0 || nz > 1) continue;
            byKind[Mathf.Clamp(kind, 0, 7)]++;
            int proto; float hs, ws; float vs = 0f; string vid = veg ? VegPick(v.lake.id, "gulf", kind, s, Fr, out vs) : null;
            if (vid != null) { proto = vp[vid]; hs = vs; ws = vs * (0.9f + Fr() * 0.2f); }
            else switch (kind)
            {
                case 0: case 1: proto = Fr() < 0.5f ? G_PINE : G_PINE_B; hs = s * (kind == 0 ? 1.0f + Fr() * 0.3f : 0.9f + Fr() * 0.25f); ws = hs * (0.85f + Fr() * 0.3f); break;
                case 2: proto = G_SABAL; hs = s * (0.85f + Fr() * 0.35f); ws = hs * (0.9f + Fr() * 0.2f); break;
                case 3: proto = G_ROYAL; hs = s * (0.9f + Fr() * 0.25f); ws = hs * (0.95f + Fr() * 0.1f); break;
                case 4: proto = G_PALMETTO; hs = s * (0.8f + Fr() * 0.5f); ws = hs * (1.0f + Fr() * 0.4f); break;
                case 5: proto = G_SEAOATS; hs = s * (0.8f + Fr() * 0.4f); ws = hs; break;
                default: proto = G_HEDGE; hs = s; ws = s * 1.4f; break;
            }
            if (y < -50f) y = terrain.SampleHeight(new Vector3(x, 0, z)) + tp.y;
            list.Add(new TreeInstance { position = new Vector3(nx, (y - 0.25f - tp.y) / size.y, nz), heightScale = hs, widthScale = ws, rotation = Fr() * Mathf.PI * 2f, prototypeIndex = proto, color = Color.white, lightmapColor = Color.white });
        }
        td.treePrototypes = vp.list.ToArray(); td.RefreshPrototypes();
        td.SetTreeInstances(list.ToArray(), true);
        terrain.treeDistance = 2400f; terrain.treeBillboardDistance = 2400f; terrain.treeCrossFadeLength = 0f;
        terrain.treeMaximumFullLODCount = 50000; terrain.drawTreesAndFoliage = true; terrain.treeLODBiasMultiplier = 1f;
        EditorUtility.SetDirty(td);
        Debug.Log($"[SkiView] gulf trees ({vp.count} vegetation-pack): {list.Count} instances (flatwoods pines {byKind[0]}, lawn pines {byKind[1]}, sabal {byKind[2]}, royal {byKind[3]}, palmetto {byKind[4]}, sea oats {byKind[5]}, hedge {byKind[6]})");
    }

    /// <summary>Flat Gulf-coast horizon: five pine-flatwoods treelines from 0.9 to 5.2 km, 14-40 m high with a crown-scale
    /// silhouette, hazing toward the sky colour (SkiView/BackdropHaze).</summary>
    static void GulfBackdrop(Transform root, float[] fogColor, Vector3 sunDir, int seed)
    {
        var rnd = new System.Random(seed);
        float[] R = { 900f, 1400f, 2200f, 3500f, 5200f };
        float[] H0 = { 13f, 16f, 20f, 24f, 28f }, H1 = { 26f, 30f, 34f, 38f, 42f };
        float[] HZ = { 0.16f, 0.32f, 0.48f, 0.62f, 0.74f };
        Color[] col = { new Color(0.40f, 0.48f, 0.34f), new Color(0.42f, 0.50f, 0.38f), new Color(0.46f, 0.54f, 0.44f), new Color(0.52f, 0.58f, 0.54f), new Color(0.58f, 0.64f, 0.66f) };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var t = new List<int>();
        int seg = 1440;
        for (int L = 0; L < R.Length; L++)
        {
            var ph = new float[12]; for (int i = 0; i < ph.Length; i++) ph[i] = (float)rnd.NextDouble() * 6.283f;
            float Line(float th)
            {
                // crown-scale bumps (pines ~8-15 m apart at this range) over slow stand-height variation, rare gaps
                float crowns = 0.5f + 0.5f * Mathf.Sin(th * (R[L] / 9f) + ph[0]) * Mathf.Sin(th * (R[L] / 23f) + ph[1]);
                float stands = 0.5f + 0.5f * Mathf.Sin(th * 7f + ph[2]) * 0.6f + 0.2f * Mathf.Sin(th * 19f + ph[3]);
                float gap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.85f, -0.6f, Mathf.Sin(th * 5f + ph[4]) * Mathf.Sin(th * 11f + ph[5])));
                return Mathf.Clamp01(0.55f * stands + 0.35f * crowns) * Mathf.Lerp(0.55f, 1f, gap);
            }
            int b0 = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float th = i * Mathf.PI * 2 / seg, top = Mathf.Lerp(H0[L], H1[L], Line(th));
                Vector3 d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
                float u = th * R[L] / 120f;
                float[] ys = { -12f, top * 0.6f, top };
                float[] hz = { Mathf.Min(1f, HZ[L] + 0.12f), HZ[L] + 0.04f, HZ[L] };
                for (int k = 0; k < 3; k++) { v.Add(d * R[L] + Vector3.up * ys[k]); uv.Add(new Vector2(u, ys[k] / 60f)); var cc = col[L]; cc.a = hz[k]; c.Add(cc); }
            }
            for (int i = 0; i < seg; i++)
                for (int k = 0; k < 2; k++)
                {
                    int a = b0 + i * 3 + k, bq = a + 1, cq = a + 3, dq = cq + 1;
                    t.Add(a); t.Add(cq); t.Add(bq); t.Add(bq); t.Add(cq); t.Add(dq);
                }
        }
        var m = new Mesh { name = "lake_backdrop", indexFormat = IndexFormat.UInt32 };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        LakeMesh(m, m.name);
        string mp = LD + "Materials/Backdrop.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("SkiView/BackdropHaze")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("SkiView/BackdropHaze");
        mat.SetTexture("_BaseMap", EnvTex("hills_forest.png", false, 512, false, 256));
        mat.SetColor("_BaseColor", new Color(0.36f, 0.42f, 0.32f));
        mat.SetColor("_HazeColor", new Color(fogColor[0], fogColor[1], fogColor[2]));
        mat.SetVector("_SunDir", sunDir); mat.SetFloat("_Light", 1f);
        EditorUtility.SetDirty(mat);
        var g = MeshObj("Backdrop", root, m, mat);
        var mr = g.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        Debug.Log($"[SkiView] gulf backdrop: {R.Length} flatwoods treelines {R[0] / 1000f:0.0}-{R[R.Length - 1] / 1000f:0.0} km, {v.Count} verts");
    }

    // ------------------------------------------------------------------ buildings
    /// <summary>Hip roof: eaves w x d at y = 0, ridge along the longer side at y = rise; submesh 0 = tiles (u along the eave,
    /// v up the slope, metres / tile), submesh 1 = soffit.</summary>
    static Mesh HipMesh(string name, float w, float d, float rise, float tile)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t0 = new List<int>(); var t1 = new List<int>();
        bool alongZ = d >= w; float S = Mathf.Min(w, d) / 2, Lh = Mathf.Max(w, d) / 2, rh = Lh - S;
        Vector3 r0 = alongZ ? new Vector3(0, rise, -rh) : new Vector3(-rh, rise, 0), r1 = alongZ ? new Vector3(0, rise, rh) : new Vector3(rh, rise, 0);
        Vector3 c00 = new Vector3(-w / 2, 0, -d / 2), c10 = new Vector3(w / 2, 0, -d / 2), c11 = new Vector3(w / 2, 0, d / 2), c01 = new Vector3(-w / 2, 0, d / 2);
        void Face(Vector3[] p)
        {
            Vector3 e = (p[1] - p[0]).normalized, nn = Vector3.Cross(p[1] - p[0], p[p.Length - 1] - p[0]).normalized; if (nn.y < 0) nn = -nn;
            Vector3 up = Vector3.Cross(nn, e).normalized; if (Vector3.Dot(up, p[p.Length - 1] - p[0]) < 0) up = -up;
            int k = v.Count;
            foreach (var q in p) { v.Add(q); uv.Add(new Vector2(Vector3.Dot(q - p[0], e) / tile, Vector3.Dot(q - p[0], up) / tile)); }
            for (int i = 1; i + 1 < p.Length; i++)
            {
                Vector3 cr = Vector3.Cross(p[i] - p[0], p[i + 1] - p[0]);
                if (cr.y >= 0) { t0.Add(k); t0.Add(k + i + 1); t0.Add(k + i); } else { t0.Add(k); t0.Add(k + i); t0.Add(k + i + 1); }
            }
        }
        if (alongZ)
        {
            Face(new[] { c10, c11, r1, r0 }); Face(new[] { c01, c00, r0, r1 }); Face(new[] { c00, c10, r0 }); Face(new[] { c11, c01, r1 });
        }
        else
        {
            Face(new[] { c00, c10, r1, r0 }); Face(new[] { c11, c01, r0, r1 }); Face(new[] { c10, c11, r1 }); Face(new[] { c01, c00, r0 });
        }
        int ks = v.Count;
        foreach (var q in new[] { c00, c10, c11, c01 }) { v.Add(q + Vector3.down * 0.04f); uv.Add(new Vector2(q.x, q.z) / tile); }
        t1.AddRange(new[] { ks, ks + 2, ks + 1, ks, ks + 3, ks + 2 });   // faces down
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetUVs(0, uv); m.subMeshCount = 2; m.SetTriangles(t0, 0); m.SetTriangles(t1, 1);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        return LakeMesh(m, name);
    }

    static Material LakeTransparent(string name, Color c)
    {
        string p = LD + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_Cull", 0); m.SetFloat("_ZWrite", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.2f); m.SetFloat("_Metallic", 0f);
        m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(m); return m;
    }

    static GameObject Quads(string name, Transform parent, List<Vector3[]> quads, Material mat)
    {
        var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
        foreach (var q in quads) { int k = v.Count; v.AddRange(q); uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right); t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 }); }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        LakeMesh(m, name);
        var g = MeshObj(name, parent, m, mat); g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off; return g;
    }

    static int gulfBldgN;

    /// <summary>Pass 18 (Tall Pine): two-storey covered dock / boathouse at the waterline: pale composite lower deck on white round
    /// columns, upper deck with white baluster railings, hip roof (grey shingle) with a pale beige soffit. Local +z = out over the water.</summary>
    static void Boathouse(LakeBuilding b, Transform go, Material trim, Material glass)
    {
        if (dockMat == null) DockMaterials();
        float W_ = b.w, D_ = b.d, H_ = b.h, zc = D_ / 2 - 2f, y1 = 0.45f, y2 = 3.4f;
        var deck = LakeMat("CompositeDeck", EnvTex("sand_white_color.jpg", false, 1024, false, 512), null, new Color(0.78f, 0.74f, 0.66f), 0.25f, new Vector2(0.5f, 0.5f));
        var shingle = LakeMat("BoathouseShingle", EnvTex("shingle_color.jpg", false, 1024, false, 512), EnvTex("shingle_normal.png", true, 512, false, 256), C3(b.roof) * 2.2f, 0.12f, Vector2.one, 0.8f);
        var soffit = LakeFlat("SoffitBeige", new Color(0.86f, 0.80f, 0.68f), 0.3f);
        PlankBox("LowerDeck", go, new Vector3(0, y1, zc), new Vector3(W_, 0.16f, D_), deck, 1.2f);
        PlankBox("UpperDeck", go, new Vector3(0, y2, zc), new Vector3(W_, 0.24f, D_), deck, 1.2f);
        Box("UpperSoffit", go, new Vector3(0, y2 - 0.14f, zc), new Vector3(W_ - 0.1f, 0.03f, D_ - 0.1f), soffit);
        int nx = Mathf.Max(3, Mathf.RoundToInt(W_ / 3.6f)), nz = 3;
        for (int i = 0; i <= nx; i++) for (int k = 0; k < nz; k++)
        {
            float x = -W_ / 2 + 0.3f + i * (W_ - 0.6f) / nx, z = zc - D_ / 2 + 0.3f + k * (D_ - 0.6f) / (nz - 1);
            Prim(PrimitiveType.Cylinder, "Column", go, new Vector3(x, (y2 + y1) / 2 - 0.4f, z), new Vector3(0.3f, (y2 - y1) / 2 + 0.8f, 0.3f), trim);
            if (k == nz - 1 || k == 0) Prim(PrimitiveType.Cylinder, "UpperPost", go, new Vector3(x, y2 + (H_ - y2) / 2, z), new Vector3(0.22f, (H_ - y2) / 2, 0.22f), trim);
        }
        // white turned-baluster railings round the upper deck (rail + bottom rail + balusters every 0.14 m, merged later)
        foreach (var (a, bq) in new[] { (new Vector3(-W_ / 2, 0, zc + D_ / 2), new Vector3(W_ / 2, 0, zc + D_ / 2)), (new Vector3(-W_ / 2, 0, zc - D_ / 2), new Vector3(-W_ / 2, 0, zc + D_ / 2)), (new Vector3(W_ / 2, 0, zc - D_ / 2), new Vector3(W_ / 2, 0, zc + D_ / 2)) })
        {
            Vector3 mid = (a + bq) / 2, dir = (bq - a); float L = dir.magnitude; dir /= L; var rot = Quaternion.LookRotation(dir);
            var r1 = Box("Rail", go, mid + Vector3.up * (y2 + 1.05f), new Vector3(0.09f, 0.08f, L), trim); r1.transform.localRotation = rot;
            var r2 = Box("BottomRail", go, mid + Vector3.up * (y2 + 0.2f), new Vector3(0.07f, 0.06f, L), trim); r2.transform.localRotation = rot;
            for (float t = 0.07f; t < L; t += 0.14f) Box("Baluster", go, a + dir * t + Vector3.up * (y2 + 0.62f), new Vector3(0.045f, 0.84f, 0.045f), trim);
        }
        var rm = HipMesh("boathouse_roof", W_ + 1.2f, D_ + 1.2f, 1.9f, 1.4f);
        var r = MeshObj("Roof", go, rm, shingle, soffit); r.transform.localPosition = new Vector3(0, H_ + 0.1f, zc);
        Box("RoofFascia", go, new Vector3(0, H_ + 0.02f, zc), new Vector3(W_ + 1.25f, 0.22f, D_ + 1.25f), trim);
        // stair from the shore up to the upper deck (landward side), lift + boat slip under one bay
        for (int st = 0; st < 14; st++) Box("Stair", go, new Vector3(W_ / 2 + 0.7f, y1 + 0.2f + st * (y2 - y1) / 14f, zc - D_ / 2 + 0.6f + st * 0.27f), new Vector3(1.2f, 0.06f, 0.3f), deck);
        for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cylinder, "Pile", go, new Vector3(-W_ / 2 + 0.3f + i * (W_ - 0.6f) / 3f, -0.4f, zc + D_ / 2 - 0.2f), new Vector3(0.28f, 0.9f, 0.28f), pileMat);
    }

    /// <summary>Round bar from a to b (a cylinder primitive stretched along the segment).</summary>
    static GameObject Seg(string name, Transform p, Vector3 a, Vector3 b, float r, Material m)
    {
        var g = Prim(PrimitiveType.Cylinder, name, p, (a + b) * 0.5f, new Vector3(r * 2f, Vector3.Distance(a, b) * 0.5f, r * 2f), m);
        g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized); return g;
    }

    /// <summary>Pass 18: barrel-tile caps on a HipMesh roof placed at c (ridge + four hips) and a tile-end course along the eaves.</summary>
    static void HipCaps(Transform go, Vector3 c, float w, float d, float rise, Material roof, float r)
    {
        bool alongZ = d >= w; float S = Mathf.Min(w, d) / 2, rh = Mathf.Max(w, d) / 2 - S;
        Vector3 r0 = c + (alongZ ? new Vector3(0, rise, -rh) : new Vector3(-rh, rise, 0)), r1 = c + (alongZ ? new Vector3(0, rise, rh) : new Vector3(rh, rise, 0));
        Vector3 up = Vector3.up * (r * 0.55f);
        if (rh > 0.05f) Seg("RidgeCap", go, r0 + up, r1 + up, r, roof);
        foreach (var (cx, cz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
        {
            Vector3 corner = c + new Vector3(cx * w / 2, 0, cz * d / 2);
            Vector3 top = alongZ ? (cz < 0 ? r0 : r1) : (cx < 0 ? r0 : r1);
            Seg("HipCap", go, corner + up, top + up, r, roof);
        }
        // tile ends: a slightly thicker roll just above each eave edge
        Vector3 e0 = c + new Vector3(-w / 2, 0.06f, -d / 2), e1 = c + new Vector3(w / 2, 0.06f, -d / 2), e2 = c + new Vector3(w / 2, 0.06f, d / 2), e3 = c + new Vector3(-w / 2, 0.06f, d / 2);
        Seg("EaveRoll", go, e0, e1, r * 0.7f, roof); Seg("EaveRoll", go, e1, e2, r * 0.7f, roof); Seg("EaveRoll", go, e2, e3, r * 0.7f, roof); Seg("EaveRoll", go, e3, e0, r * 0.7f, roof);
    }

    /// <summary>Pass 18: Gulf-coast window: the plain window plus a raised stucco surround, a projecting sill, a keystone over the
    /// big lake-side openings and (road side / ends) louvred shutters in the house accent colour.</summary>
    static void GulfWindow(Transform p, Vector3 pos, float w, float h, Material glass, Material trim, Material wall, Material shutter, bool keystone, float yaw)
    {
        Window(p, pos, w, h, glass, trim, yaw);
        var g = new GameObject("GulfTrim").transform; g.SetParent(p, false); g.localPosition = pos; g.localEulerAngles = new Vector3(0, yaw, 0);
        float b = 0.17f, z = 0.04f;
        Box("SurroundT", g, new Vector3(0, h / 2 + 0.12f + b / 2, z), new Vector3(w + 0.34f + 2 * b, b, 0.08f), wall);
        Box("SurroundL", g, new Vector3(-w / 2 - 0.12f - b / 2, 0, z), new Vector3(b, h + 0.24f, 0.08f), wall);
        Box("SurroundR", g, new Vector3(w / 2 + 0.12f + b / 2, 0, z), new Vector3(b, h + 0.24f, 0.08f), wall);
        Box("SillLedge", g, new Vector3(0, -h / 2 - 0.14f, 0.11f), new Vector3(w + 0.5f, 0.07f, 0.2f), trim);
        if (keystone) Box("Keystone", g, new Vector3(0, h / 2 + 0.2f, 0.07f), new Vector3(0.28f, 0.34f, 0.1f), trim);
        if (shutter != null)
            foreach (float sx in new[] { -1f, 1f })
            {
                float shw = w * 0.5f;
                Box("Shutter", g, new Vector3(sx * (w / 2 + 0.32f + shw / 2), 0, 0.06f), new Vector3(shw, h + 0.1f, 0.04f), shutter);
                for (int k = 0; k < 6; k++) Box("Louvre", g, new Vector3(sx * (w / 2 + 0.32f + shw / 2), -h / 2 + (k + 0.5f) * h / 6f, 0.085f), new Vector3(shw - 0.08f, 0.025f, 0.02f), trim);
            }
    }

    /// <summary>Pass 18: lanai life: tongue-and-groove ceiling beams, two ceiling fans, pendant lights, a dining set, an outdoor
    /// kitchen counter, a tile roof over the slab, and chaise lounges + an umbrella on the pool deck. Origin at the lake wall (base).</summary>
    static void LanaiDetail(Transform go, Vector3 o, float lw, Material roof, Material trim, Material wall, Material accent, Material alu, int id)
    {
        var l = new GameObject("LanaiDetail").transform; l.SetParent(go, false); l.localPosition = o;
        var wood = LakeFlat("EntryDoorWood", new Color(0.30f, 0.20f, 0.13f), 0.35f);
        // tile shed roof over the lanai slab (low hip against the house)
        var lr = HipMesh("lanai_roof_" + id, lw + 0.7f, 5.4f, 0.8f, 1.4f);
        var r = MeshObj("LanaiTileRoof", l, lr, roof, trim); r.transform.localPosition = new Vector3(0, 3.25f, 2.4f);
        HipCaps(l, r.transform.localPosition, lw + 0.7f, 5.4f, 0.8f, roof, 0.09f);
        // beam header over the columns + ceiling joists
        Box("Header", l, new Vector3(0, 2.86f, 4.6f), new Vector3(lw + 0.3f, 0.32f, 0.42f), trim);
        for (int i = 0; i < 6; i++) Box("Joist", l, new Vector3((i - 2.5f) * lw / 6f, 2.92f, 2.4f), new Vector3(0.10f, 0.14f, 4.6f), wood);
        // ceiling fans + pendants
        foreach (float fx in new[] { -lw * 0.22f, lw * 0.22f })
        {
            Prim(PrimitiveType.Cylinder, "FanRod", l, new Vector3(fx, 2.7f, 2.4f), new Vector3(0.03f, 0.18f, 0.03f), alu);
            Prim(PrimitiveType.Cylinder, "FanHub", l, new Vector3(fx, 2.5f, 2.4f), new Vector3(0.22f, 0.05f, 0.22f), alu);
            for (int k = 0; k < 4; k++) { var bl = Box("FanBlade", l, new Vector3(fx, 2.49f, 2.4f), new Vector3(1.15f, 0.015f, 0.13f), wood); bl.transform.localEulerAngles = new Vector3(0, k * 45f + id * 11f, 0); }
        }
        // dining set under the lanai (left), outdoor kitchen counter against the wall (right)
        float dx = -lw * 0.22f;
        Prim(PrimitiveType.Cylinder, "TableTop", l, new Vector3(dx, 0.74f, 2.6f), new Vector3(1.3f, 0.025f, 1.3f), trim);
        Prim(PrimitiveType.Cylinder, "TableLeg", l, new Vector3(dx, 0.37f, 2.6f), new Vector3(0.1f, 0.37f, 0.1f), alu);
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.PI / 2f + 0.4f; Vector3 c = new Vector3(dx + Mathf.Cos(a) * 0.95f, 0, 2.6f + Mathf.Sin(a) * 0.95f);
            var seat = Box("ChairSeat", l, c + Vector3.up * 0.45f, new Vector3(0.48f, 0.06f, 0.48f), accent); seat.transform.localEulerAngles = new Vector3(0, -a * Mathf.Rad2Deg, 0);
            var back = Box("ChairBack", l, c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.22f + Vector3.up * 0.72f, new Vector3(0.06f, 0.5f, 0.46f), alu); back.transform.localEulerAngles = new Vector3(0, -a * Mathf.Rad2Deg, 0);
        }
        Box("KitchenBase", l, new Vector3(lw * 0.3f, 0.45f, 0.45f), new Vector3(2.6f, 0.9f, 0.7f), wall);
        Box("KitchenTop", l, new Vector3(lw * 0.3f, 0.92f, 0.45f), new Vector3(2.7f, 0.05f, 0.78f), trim);
        Box("Grill", l, new Vector3(lw * 0.3f + 0.4f, 1.0f, 0.42f), new Vector3(0.8f, 0.14f, 0.5f), alu);
        // pool deck: three chaise lounges facing the lake and a market umbrella
        for (int k = 0; k < 3; k++)
        {
            Vector3 c = new Vector3(lw * 0.18f + (k - 1) * 1.0f, 0, 10.6f);
            Box("ChaiseSeat", l, c + new Vector3(0, 0.32f, 0.3f), new Vector3(0.7f, 0.08f, 1.5f), accent);
            var bk = Box("ChaiseBack", l, c + new Vector3(0, 0.55f, -0.62f), new Vector3(0.7f, 0.08f, 0.7f), accent); bk.transform.localEulerAngles = new Vector3(-50f, 0, 0);
            Box("ChaiseFrame", l, c + new Vector3(0, 0.15f, 0.1f), new Vector3(0.6f, 0.3f, 1.8f), alu);
        }
        Prim(PrimitiveType.Cylinder, "UmbrellaPole", l, new Vector3(lw * 0.18f + 1.9f, 1.2f, 10.2f), new Vector3(0.05f, 1.2f, 0.05f), alu);
        Prim(PrimitiveType.Cylinder, "UmbrellaCanopy", l, new Vector3(lw * 0.18f + 1.9f, 2.3f, 10.2f), new Vector3(2.6f, 0.08f, 2.6f), accent);
    }
    static void GulfBuilding(LakeBuilding b, Transform go, Terrain terrain, Material glass, Material trim, Material concrete)
    {
        Color wallC = C3(b.wall), roofC = C3(b.roof);
        int id = gulfBldgN++;
        var stucco = EnvTex("stucco_color.jpg", false, 512, false, 256); var stuccoN = EnvTex("stucco_normal.png", true, 512, false, 256);
        var tile = EnvTex("rooftile_color.jpg", false, 1024, false, 512); var tileN = EnvTex("rooftile_normal.png", true, 512, false, 256);
        var alu = LakeFlat("AluminiumWhite", new Color(0.92f, 0.93f, 0.93f), 0.5f, 0.6f);
        if (b.kind == "estate")
        {
            bool white = wallC.r > 0.93f;
            var wall = LakeMat(white ? "EstateStuccoWhite" : "EstateStuccoIvory", stucco, stuccoN, new Color(wallC.r / 0.89f, wallC.g / 0.875f, wallC.b / 0.835f), 0.12f, Vector2.one, 0.6f);
            bool greyRoof = roofC.r < 0.7f;
            bool terra = !greyRoof && roofC.b < 0.5f;   // pass 18: Villa Serena terracotta barrel tile (#c8714e)
            var roof = LakeMat(greyRoof ? "EstateRoofGrey" : terra ? "EstateRoofTerracotta" : "EstateRoofWhite", tile, tileN, roofC * (greyRoof ? 2.7f : 4.4f), 0.22f, Vector2.one, 0.9f);
            float W_ = b.w, D_ = b.d, H_ = b.h; bool two = H_ > 5f; float base_ = 0.5f;
            PlankBox("Foundation", go, new Vector3(0, 0.15f, 0), new Vector3(W_ + 0.3f, 0.7f, D_ + 0.3f), concrete, 2f);
            PlankBox("Walls", go, new Vector3(0, base_ + H_ / 2, 0), new Vector3(W_, H_, D_), wall, 3f);
            if (two) PlankBox("Band", go, new Vector3(0, base_ + H_ / 2, 0), new Vector3(W_ + 0.12f, 0.2f, D_ + 0.12f), trim, 2f);
            PlankBox("Fascia", go, new Vector3(0, base_ + H_ + 0.12f, 0), new Vector3(W_ + 1.5f, 0.24f, D_ + 1.5f), trim, 2f);
            float rise = Mathf.Min(W_, D_) * 0.24f;
            var rm = HipMesh("estate_roof_" + id, W_ + 1.6f, D_ + 1.6f, rise, 1.4f);
            var r = MeshObj("Roof", go, rm, roof, trim); r.transform.localPosition = new Vector3(0, base_ + H_ + 0.24f, 0);
            // pass 18: barrel-tile ridge and hip caps, plus a tile-end course along the eaves
            HipCaps(go, r.transform.localPosition, W_ + 1.6f, D_ + 1.6f, rise, roof, 0.13f);
            var accent = LakeFlat(new[] { "ShutterTeal", "ShutterSage", "ShutterNavy" }[id % 3], new[] { new Color(0.24f, 0.45f, 0.47f), new Color(0.47f, 0.55f, 0.45f), new Color(0.17f, 0.24f, 0.35f) }[id % 3], 0.35f);
            // garage wing on one side, set back toward the road
            float gs = (id % 2 == 0) ? -1f : 1f, gw = 8.5f, gd = D_ * 0.82f, gh = 3.5f; Vector3 gc = new Vector3(gs * (W_ / 2 + gw / 2 - 0.2f), 0, -D_ * 0.09f);
            PlankBox("GarageWalls", go, gc + new Vector3(0, base_ + gh / 2, 0), new Vector3(gw, gh, gd), wall, 3f);
            PlankBox("GarageFascia", go, gc + new Vector3(0, base_ + gh + 0.1f, 0), new Vector3(gw + 1.2f, 0.2f, gd + 1.2f), trim, 2f);
            var grm = HipMesh("estate_garage_roof_" + id, gw + 1.3f, gd + 1.3f, gw * 0.24f, 1.4f);
            var gr = MeshObj("GarageRoof", go, grm, roof, trim); gr.transform.localPosition = gc + new Vector3(0, base_ + gh + 0.2f, 0);
            HipCaps(go, gr.transform.localPosition, gw + 1.3f, gd + 1.3f, gw * 0.24f, roof, 0.11f);
            var door = LakeFlat("GarageDoorWhite", new Color(0.9f, 0.9f, 0.88f), 0.4f);
            for (int i = 0; i < 2; i++) { Box("GarageDoor" + i, go, gc + new Vector3((i - 0.5f) * 3.6f, base_ + 1.2f, -gd / 2 - 0.03f), new Vector3(2.9f, 2.3f, 0.06f), door); for (int s = 0; s < 4; s++) Box("GDRib", go, gc + new Vector3((i - 0.5f) * 3.6f, base_ + 0.3f + s * 0.58f, -gd / 2 - 0.065f), new Vector3(2.9f, 0.03f, 0.02f), trim); }
            // windows: tall sliders / French doors on the lake side, smaller on the road side and ends
            int nw = Mathf.Max(3, Mathf.FloorToInt(W_ / 3.6f));
            for (int s = 0; s < (two ? 2 : 1); s++)
            {
                float y0 = base_ + (s == 0 ? 1.35f : H_ / 2 + 1.55f), wh = s == 0 ? 2.3f : 1.7f;
                for (int i = 0; i < nw; i++)
                {
                    float x = (i - (nw - 1) / 2f) * (W_ - 3f) / Mathf.Max(1, nw - 1) * 0.92f;
                    GulfWindow(go, new Vector3(x, y0, D_ / 2), s == 0 ? 1.8f : 1.4f, wh, glass, trim, wall, null, s == 0, 0);
                    if (Mathf.Abs(x) > 2.5f) GulfWindow(go, new Vector3(x, y0 - (s == 0 ? 0.2f : 0f), -D_ / 2), 1.2f, s == 0 ? 1.6f : 1.4f, glass, trim, wall, accent, false, 180);
                }
                foreach (float ex in new[] { -1f, 1f })
                    for (int i = 0; i < 2; i++) GulfWindow(go, new Vector3(ex * W_ / 2, y0, (i - 0.5f) * D_ * 0.5f), 1.2f, s == 0 ? 1.7f : 1.4f, glass, trim, wall, accent, false, ex * 90);
            }
            // entry portico (road side)
            float ph = two ? 4.6f : 3.4f;
            PlankBox("PorticoSlab", go, new Vector3(0, base_ - 0.05f, -D_ / 2 - 2.0f), new Vector3(5.4f, 0.3f, 4.0f), concrete, 2f);
            foreach (float px in new[] { -2.1f, 2.1f }) Prim(PrimitiveType.Cylinder, "Column", go, new Vector3(px, base_ + ph / 2, -D_ / 2 - 3.6f), new Vector3(0.42f, ph / 2, 0.42f), trim);
            PlankBox("PorticoBeam", go, new Vector3(0, base_ + ph + 0.2f, -D_ / 2 - 1.9f), new Vector3(5.6f, 0.4f, 3.8f), wall, 2f);
            var prm = HipMesh("estate_portico_roof_" + id, 6.2f, 4.4f, 1.1f, 1.4f);
            var pr = MeshObj("PorticoRoof", go, prm, roof, trim); pr.transform.localPosition = new Vector3(0, base_ + ph + 0.4f, -D_ / 2 - 1.9f);
            HipCaps(go, pr.transform.localPosition, 6.2f, 4.4f, 1.1f, roof, 0.10f);
            var wood = LakeFlat("EntryDoorWood", new Color(0.30f, 0.20f, 0.13f), 0.35f);
            Box("EntryDoor", go, new Vector3(0, base_ + 1.35f, -D_ / 2 - 0.03f), new Vector3(1.8f, 2.7f, 0.07f), wood);
            // lanai (covered patio) + screened pool cage on the lake side
            float lw = W_ * 0.62f, cd = 12f, cw = W_ * 0.82f, ch = 4.0f, ctop = 5.6f;
            var pav = LakeMat("PoolPavers", EnvTex("sand_white_color.jpg", false, 1024, false, 512), null, new Color(1.05f, 1.02f, 0.95f), 0.25f, new Vector2(0.6f, 0.6f));
            PlankBox("PoolDeck", go, new Vector3(0, base_ - 0.12f, D_ / 2 + cd / 2), new Vector3(cw, 0.24f, cd), pav, 2f);
            PlankBox("LanaiRoof", go, new Vector3(0, base_ + 3.1f, D_ / 2 + 2.4f), new Vector3(lw, 0.3f, 4.8f), wall, 3f);
            for (int i = 0; i < 4; i++) Box("LanaiColumn", go, new Vector3((i - 1.5f) * lw / 3.2f, base_ + 1.5f, D_ / 2 + 4.6f), new Vector3(0.36f, 3.0f, 0.36f), trim);
            LanaiDetail(go, new Vector3(0, base_, D_ / 2), lw, roof, trim, wall, accent, alu, id);
            var poolW = LakeFlat("PoolWater", new Color(0.18f, 0.62f, 0.70f), 0.95f);
            Box("Pool", go, new Vector3(gs * -cw * 0.12f, base_ + 0.005f, D_ / 2 + 8.4f), new Vector3(9.5f, 0.02f, 4.6f), poolW);
            Box("PoolCoping", go, new Vector3(gs * -cw * 0.12f, base_ - 0.005f, D_ / 2 + 8.4f), new Vector3(10.1f, 0.02f, 5.2f), trim);
            // cage: aluminium frame on a grid + dark screen panels, mansard top
            var screen = LakeTransparent("PoolScreen", new Color(0.10f, 0.11f, 0.12f, 0.30f));
            var cage = new GameObject("PoolCage").transform; cage.SetParent(go, false); cage.localPosition = new Vector3(0, base_, D_ / 2);
            float hx = cw / 2, z1 = cd, ins = 2.2f;
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
            var q = new List<Vector3[]>
            {
                new[] { P(-hx, 0, z1), P(-hx, ch, z1), P(hx, ch, z1), P(hx, 0, z1) },
                new[] { P(-hx, 0, 0.2f), P(-hx, ch, 0.2f), P(-hx, ch, z1), P(-hx, 0, z1) },
                new[] { P(hx, 0, z1), P(hx, ch, z1), P(hx, ch, 0.2f), P(hx, 0, 0.2f) },
                new[] { P(-hx, ch, z1), P(-hx + ins, ctop, z1 - ins), P(hx - ins, ctop, z1 - ins), P(hx, ch, z1) },
                new[] { P(-hx, ch, 0.2f), P(-hx + ins, ctop, 0.2f), P(-hx + ins, ctop, z1 - ins), P(-hx, ch, z1) },
                new[] { P(hx, ch, z1), P(hx - ins, ctop, z1 - ins), P(hx - ins, ctop, 0.2f), P(hx, ch, 0.2f) },
                new[] { P(-hx + ins, ctop, 0.2f), P(hx - ins, ctop, 0.2f), P(hx - ins, ctop, z1 - ins), P(-hx + ins, ctop, z1 - ins) },
            };
            Quads("cage_screen_" + id, cage, q, screen);
            int nx = Mathf.CeilToInt(cw / 1.8f), nz = Mathf.CeilToInt(cd / 1.8f);
            for (int i = 0; i <= nx; i++) { float x = -hx + i * cw / nx; Box("Post", cage, P(x, ch / 2, z1), new Vector3(0.07f, ch, 0.07f), alu); }
            for (int i = 1; i <= nz; i++) { float z = i * cd / nz; Box("PostL", cage, P(-hx, ch / 2, z), new Vector3(0.07f, ch, 0.07f), alu); Box("PostR", cage, P(hx, ch / 2, z), new Vector3(0.07f, ch, 0.07f), alu); }
            foreach (float y in new[] { 1.0f, ch })
            {
                Box("RailF", cage, P(0, y, z1), new Vector3(cw, 0.08f, 0.08f), alu);
                Box("RailL", cage, P(-hx, y, z1 / 2), new Vector3(0.08f, 0.08f, z1), alu); Box("RailR", cage, P(hx, y, z1 / 2), new Vector3(0.08f, 0.08f, z1), alu);
            }
            Box("TopF", cage, P(0, ctop, z1 - ins), new Vector3(cw - 2 * ins, 0.08f, 0.08f), alu);
            Box("TopL", cage, P(-hx + ins, ctop, (z1 - ins) / 2), new Vector3(0.08f, 0.08f, z1 - ins), alu); Box("TopR", cage, P(hx - ins, ctop, (z1 - ins) / 2), new Vector3(0.08f, 0.08f, z1 - ins), alu);
            for (int i = 0; i <= nx; i++)
            {
                float x = -hx + i * cw / nx, xt = Mathf.Lerp(-hx + ins, hx - ins, i / (float)nx);
                var a = P(x, ch, z1); var bq = P(xt, ctop, z1 - ins); var mid = (a + bq) / 2; var bar = Box("Rafter", cage, mid, new Vector3(0.06f, 0.06f, Vector3.Distance(a, bq)), alu); bar.transform.localRotation = Quaternion.LookRotation(bq - a);
                var c1 = P(xt, ctop, 0.2f); var bar2 = Box("Purlin", cage, (bq + c1) / 2, new Vector3(0.05f, 0.05f, Vector3.Distance(bq, c1)), alu); bar2.transform.localRotation = Quaternion.LookRotation(c1 - bq);
            }
        }
        else if (b.kind == "pier" || b.kind == "commdock")
        {
            if (dockMat == null) DockMaterials();
            bool comm = b.kind == "commdock"; float L = b.w, Wd = b.d, yd = 0.45f;
            go.position = new Vector3(b.x, 0, b.z);
            PlankBox("Deck", go, new Vector3(0, yd, (L - 2f) / 2), new Vector3(Wd, 0.14f, L + 2f), dockMat, 1.2f);
            float th = comm ? 7f : 4.6f, tdp = comm ? 3.2f : 2.4f;
            PlankBox("Head", go, new Vector3(0, yd, L + tdp / 2 - 0.2f), new Vector3(th, 0.14f, tdp), dockMat, 1.2f);
            for (float z = 0f; z <= L; z += 2.4f) foreach (float sx in new[] { -1f, 1f }) Prim(PrimitiveType.Cylinder, "Pile", go, new Vector3(sx * (Wd / 2 - 0.1f), -0.25f, z), new Vector3(0.2f, 0.85f, 0.2f), pileMat);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { 0f, 1f }) Prim(PrimitiveType.Cylinder, "PileH", go, new Vector3(sx * (th / 2 - 0.1f), -0.25f, L - 0.2f + sz * tdp), new Vector3(0.2f, 0.95f, 0.2f), pileMat);
            if (b.h > 0.5f)
            {
                // boat lift beside the head (+x), with a white cover on the community dock
                float lx = th / 2 + 1.8f, lz = L + 1.0f;
                for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cylinder, "LiftPost" + i, go, new Vector3(lx + (i % 2 == 0 ? -1.35f : 1.35f), 0.8f, lz + (i < 2 ? -2.9f : 2.9f)), new Vector3(0.11f, 1.3f, 0.11f), frameMat);
                Box("LiftBunkL", go, new Vector3(lx - 0.7f, 0.08f, lz), new Vector3(0.16f, 0.12f, 6.0f), frameMat);
                Box("LiftBunkR", go, new Vector3(lx + 0.7f, 0.08f, lz), new Vector3(0.16f, 0.12f, 6.0f), frameMat);
                Box("LiftWheel", go, new Vector3(lx + 1.35f, 2.0f, lz - 2.9f), new Vector3(0.06f, 0.7f, 0.7f), frameMat);
                if (comm)
                {
                    var roof = LakeFlat("DockCoverWhite", C3(b.roof), 0.5f, 0.2f);
                    for (int i = 0; i < 4; i++) Box("CoverPost" + i, go, new Vector3(lx + (i % 2 == 0 ? -1.6f : 1.6f), 1.7f, lz + (i < 2 ? -3.6f : 3.6f)), new Vector3(0.14f, 3.4f, 0.14f), alu);
                    var cr = PlankBox("CoverRoof", go, new Vector3(lx, 3.45f, lz), new Vector3(4.2f, 0.08f, 8.4f), roof, 1.2f, new Vector3(0, 0, -5f));
                    cr.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                    PlankBox("Bench", go, new Vector3(-th / 2 + 0.5f, yd + 0.45f, L + 1.2f), new Vector3(0.45f, 0.08f, 2.0f), dockMat, 1.2f);
                    for (int i = 0; i < 3; i++) Box("LadderRung", go, new Vector3(-th / 2 - 0.05f, yd - 0.3f * i, L + 0.3f), new Vector3(0.05f, 0.04f, 0.5f), alu);
                }
            }
        }
        else if (b.kind == "tent")
        {
            var fabric = LakeFlat("TentFabricWhite", new Color(0.96f, 0.96f, 0.95f), 0.3f);
            float w = b.w, d = b.d, h = b.h;
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Box("Leg", go, new Vector3(sx * w / 2, h / 2, sz * d / 2), new Vector3(0.06f, h, 0.06f), alu);
            var cm = HipMesh("tent_canopy_" + id, w + 0.2f, d + 0.2f, 1.0f, 2f);
            var c = MeshObj("Canopy", go, cm, fabric, fabric); c.transform.localPosition = new Vector3(0, h, 0);
            c.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            Box("ValanceF", go, new Vector3(0, h - 0.12f, d / 2 + 0.1f), new Vector3(w + 0.2f, 0.25f, 0.02f), fabric); Box("ValanceB", go, new Vector3(0, h - 0.12f, -d / 2 - 0.1f), new Vector3(w + 0.2f, 0.25f, 0.02f), fabric);
            Box("ValanceL", go, new Vector3(-w / 2 - 0.1f, h - 0.12f, 0), new Vector3(0.02f, 0.25f, d + 0.2f), fabric); Box("ValanceR", go, new Vector3(w / 2 + 0.1f, h - 0.12f, 0), new Vector3(0.02f, 0.25f, d + 0.2f), fabric);
            var tbl = LakeFlat("TableWhite", new Color(0.92f, 0.92f, 0.9f), 0.3f);
            Box("Table", go, new Vector3(0, 0.75f, 0), new Vector3(Mathf.Min(2.4f, w - 0.8f), 0.05f, 0.8f), tbl);
            foreach (float sx in new[] { -1f, 1f }) Box("TableLeg", go, new Vector3(sx * Mathf.Min(1.1f, w / 2 - 0.5f), 0.37f, 0), new Vector3(0.04f, 0.74f, 0.7f), alu);
        }
        else if (b.kind == "tower")
        {
            // two-storey judges' tower: steel legs, deck at 3.2 m, glazed cab, flat roof, outside stair
            var steel = LakeFlat("TowerSteelGrey", new Color(0.55f, 0.57f, 0.58f), 0.4f, 0.5f);
            var panel = LakeFlat("TowerPanelWhite", C3(b.wall), 0.35f);
            var roofM = LakeFlat("TowerRoofGrey", C3(b.roof), 0.3f, 0.2f);
            float w = b.w, d = b.d, deck = 3.2f;
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Box("Leg", go, new Vector3(sx * (w / 2 - 0.1f), deck / 2, sz * (d / 2 - 0.1f)), new Vector3(0.22f, deck, 0.22f), steel);
            Box("BraceA", go, new Vector3(0, deck / 2, -d / 2 + 0.1f), new Vector3(0.08f, 0.08f, Mathf.Sqrt(w * w + deck * deck)), steel).transform.localRotation = Quaternion.Euler(0, 90, Mathf.Atan2(deck, w) * Mathf.Rad2Deg);
            PlankBox("Deck", go, new Vector3(0, deck, 0.6f), new Vector3(w + 0.6f, 0.25f, d + 1.8f), concrete, 2f);
            PlankBox("CabLower", go, new Vector3(0, deck + 0.6f, 0), new Vector3(w, 1.0f, d), panel, 2f);
            Box("CabGlass", go, new Vector3(0, deck + 1.8f, 0), new Vector3(w - 0.1f, 1.4f, d - 0.1f), glass);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Box("Mullion", go, new Vector3(sx * w / 2, deck + 1.8f, sz * d / 2), new Vector3(0.12f, 1.4f, 0.12f), panel);
            PlankBox("CabHeader", go, new Vector3(0, deck + 2.65f, 0), new Vector3(w, 0.3f, d), panel, 2f);
            PlankBox("Roof", go, new Vector3(0, deck + 2.9f, 0), new Vector3(w + 1.0f, 0.2f, d + 1.0f), roofM, 2f);
            // railing on the open front landing
            for (int i = 0; i < 5; i++) Box("RailPost", go, new Vector3(-w / 2 - 0.2f + i * (w + 0.4f) / 4, deck + 0.55f, d / 2 + 1.45f), new Vector3(0.06f, 1.0f, 0.06f), steel);
            Box("Rail", go, new Vector3(0, deck + 1.05f, d / 2 + 1.45f), new Vector3(w + 0.5f, 0.06f, 0.06f), steel);
            var st = PlankBox("Stair", go, new Vector3(w / 2 + 0.7f, deck / 2, -0.4f), new Vector3(1.0f, 0.12f, deck / Mathf.Sin(40f * Mathf.Deg2Rad)), dockMat ?? concrete, 1.2f, new Vector3(-40f, 0, 0));
            Box("StairRail", go, new Vector3(w / 2 + 1.2f, deck / 2 + 0.9f, -0.4f), new Vector3(0.05f, 0.05f, deck / Mathf.Sin(40f * Mathf.Deg2Rad)), steel).transform.localRotation = Quaternion.Euler(-40f, 0, 0);
        }
        else if (b.kind == "scoreboard")
        {
            var steel = LakeFlat("TowerSteelGrey", new Color(0.55f, 0.57f, 0.58f), 0.4f, 0.5f);
            var board = LakeFlat("ScoreboardBlack", C3(b.wall), 0.4f);
            var frame = LakeFlat("ScoreboardFrame", C3(b.roof), 0.4f);
            float w = b.w, h = b.h;
            foreach (float sx in new[] { -1f, 1f }) Box("Post", go, new Vector3(sx * (w / 2 - 0.3f), h / 2, -0.1f), new Vector3(0.16f, h, 0.16f), steel);
            Box("Board", go, new Vector3(0, h - 1.2f, 0), new Vector3(w, 2.2f, 0.16f), board);
            Box("FrameT", go, new Vector3(0, h - 0.07f, 0.02f), new Vector3(w + 0.1f, 0.1f, 0.2f), frame); Box("FrameB", go, new Vector3(0, h - 2.33f, 0.02f), new Vector3(w + 0.1f, 0.1f, 0.2f), frame);
            var seg = LakeFlat("ScoreboardSegment", new Color(0.16f, 0.17f, 0.18f), 0.6f);
            for (int r = 0; r < 3; r++) for (int i = 0; i < 6; i++) Box("Cell", go, new Vector3(-w / 2 + 0.45f + i * (w - 0.9f) / 5, h - 0.55f - r * 0.62f, 0.085f), new Vector3(0.5f, 0.44f, 0.01f), seg);
        }
        else if (b.kind == "arch")
        {
            // generic inflatable arch (white with a blue band), half torus + anchor pads
            var white = LakeMat("ArchWhite", null, null, C3(b.wall), 0.35f, Vector2.one, 1f, true); var blue = LakeMat("ArchBlue", null, null, C3(b.roof), 0.35f, Vector2.one, 1f, true);
            float R = b.w / 2, tr = 0.55f; int nu = 28, nv = 10;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t0 = new List<int>(); var t1 = new List<int>();
            for (int i = 0; i <= nu; i++)
            {
                float u = Mathf.PI * i / nu; Vector3 c = new Vector3(Mathf.Cos(u) * R, Mathf.Sin(u) * (b.h - tr), 0), rd = new Vector3(Mathf.Cos(u), Mathf.Sin(u) * (b.h - tr) / R, 0).normalized;
                for (int j = 0; j <= nv; j++) { float a = Mathf.PI * 2 * j / nv; Vector3 dn = rd * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a); v.Add(c + dn * tr); n.Add(dn); }
            }
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    int a = i * (nv + 1) + j, bq = a + 1, cq = a + nv + 1, dq = cq + 1;
                    var tl = (i > nu * 0.36f && i < nu * 0.64f) ? t1 : t0;
                    tl.Add(a); tl.Add(bq); tl.Add(cq); tl.Add(bq); tl.Add(dq); tl.Add(cq);
                }
            var m = new Mesh { name = "arch_" + id }; m.SetVertices(v); m.SetNormals(n); m.subMeshCount = 2; m.SetTriangles(t0, 0); m.SetTriangles(t1, 1); m.RecalculateBounds();
            LakeMesh(m, m.name);
            MeshObj("Arch", go, m, white, blue);
            foreach (float sx in new[] { -1f, 1f }) Box("Pad", go, new Vector3(sx * R, 0.15f, 0), new Vector3(1.4f, 0.3f, 1.4f), white);
        }
    }
}
