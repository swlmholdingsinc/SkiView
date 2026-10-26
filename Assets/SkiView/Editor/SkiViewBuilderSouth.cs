using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>b10 (Scott, Oct 23 2026): Ski View south end - community boat basin with lifts L1-L5 (each an unbranded
/// runabout in its own colour), octagonal warming hut with French doors facing the lake, 2.4 x 8 m start dock in front of
/// the hut, no lamp posts / lights. Geometry from roadmap/venue/skiview_layout_m_v2_basin.json, fitted rigidly to the
/// built (rescaled) shore: rotated -50.2 deg and translated so the basin mouth sits on the shore 26 m south-west of the
/// lot-18 shore point. The built south end is shorter than the layout's, so this keeps the dock tip ~29 m off the
/// turnaround boat path and the basin >= 28 m from it (layout: 41 / 15 m).</summary>
public static partial class SkiViewBuilder
{
    static readonly Vector2[] SOUTH_NOTCH = {
        new Vector2(-28.72f, -302.19f), new Vector2(-23.15f, -310.90f), new Vector2(-63.61f, -336.74f), new Vector2(-50.14f, -357.80f),
        new Vector2(0.42f, -325.50f), new Vector2(-13.05f, -304.44f), new Vector2(-17.64f, -297.23f) };
    static readonly Vector2 HUT_C = new Vector2(-6.8f, -296.9f);
    static readonly Vector2 LAKEWARD = new Vector2(-0.640f, 0.768f);          // layout west (the lake) after the fit rotation
    static readonly Vector2 DOCK_ROOT = new Vector2(-14.40f, -291.6f), DOCK_DIR = new Vector2(-0.5384f, 0.8427f);
    static readonly Vector2 LIFT_AXIS = new Vector2(0.8427f, 0.5384f);
    static readonly Vector2[] LIFT_C = { new Vector2(-14.68f, -310.23f), new Vector2(-53.17f, -341.93f), new Vector2(-51.12f, -345.13f), new Vector2(-49.08f, -348.34f), new Vector2(-47.03f, -351.54f) };
    static readonly string[][] LIFT_BOAT = { new[] { "F2F2EE", "B3262B" }, new[] { "1F2D4A", "" }, new[] { "A7ADB3", "" }, new[] { "E0B32A", "" }, new[] { "1C1E20", "2A8C8C" } };
    const float BASIN_BED_Y = -1.7f, BASIN_BANK_M = 2.5f;

    static bool InPoly(Vector2 p, Vector2[] poly)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    }
    static float PolyDist(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i], ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, (a + ab * t - p).magnitude);
        }
        return best;
    }

    /// Trees and scatter stay off the basin, the hut and the dock approach.
    static bool InsideSouth(float x, float z, float margin)
    {
        var p = new Vector2(x, z);
        if (InPoly(p, SOUTH_NOTCH) || PolyDist(p, SOUTH_NOTCH) < 3f + margin) return true;
        return (p - HUT_C).magnitude < 9f + margin || (p - DOCK_ROOT).magnitude < 6f + margin;
    }

    /// Cuts the basin into the heightmap (bed at BASIN_BED_Y, banks blended over BASIN_BANK_M) and flattens the hut pad.
    static void CarveSouthBasin(Venue v, float[,] h)
    {
        int res = v.terrain.res; float size = v.terrain.size, ymin = v.terrain.ymin, hr = v.terrain.hrange, cell = size / (res - 1);
        int n = 0;
        for (int zi = 0; zi < res; zi++)
        {
            float wz = -size / 2 + zi * cell; if (wz > -285f || wz < -370f) continue;
            for (int xi = 0; xi < res; xi++)
            {
                float wx = -size / 2 + xi * cell; if (wx < -75f || wx > 12f) continue;
                var p = new Vector2(wx, wz); float y = ymin + h[zi, xi] * hr;
                bool inside = InPoly(p, SOUTH_NOTCH); float d = PolyDist(p, SOUTH_NOTCH);
                float want = y;
                if (inside) want = Mathf.Lerp(-0.25f, BASIN_BED_Y, Mathf.Clamp01(d / BASIN_BANK_M));
                else if (d < BASIN_BANK_M) want = Mathf.Min(y, Mathf.Lerp(-0.25f, Mathf.Max(y, 0.45f), d / BASIN_BANK_M));
                float dh = (p - HUT_C).magnitude;
                if (!inside && dh < 9f) want = Mathf.Lerp(0.45f, want, Smooth01((dh - 5.5f) / 3.5f));   // hut pad at street grade
                if (want != y) { h[zi, xi] = Mathf.Clamp01((want - ymin) / hr); n++; }
            }
        }
        Debug.Log($"[SkiView] south end: basin carved ({n} heightmap cells)");
    }

    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out var c); return c; }
    static Vector3 V3(Vector2 p, float y) => new Vector3(p.x, y, p.y);

    static void BuildSouthEnd(Venue v, Transform root, Terrain terrain)
    {
        var sroot = new GameObject("SouthEnd_Basin_Hut_Dock").transform; sroot.SetParent(root, false);
        var wood = Lit("SouthDeck", Hex("AE845E"), 0.25f);
        var post = Lit("SouthPost", new Color(0.30f, 0.24f, 0.18f), 0.2f);
        var canopy = Lit("LiftCanopy", Hex("3F6E4E"), 0.45f, 0.3f);
        var alu = Lit("LiftAlu", new Color(0.72f, 0.73f, 0.74f), 0.55f, 0.8f);
        var siding = Lit("HutSiding", new Color(0.62f, 0.52f, 0.40f), 0.25f);
        var trim = Lit("HutTrim", new Color(0.93f, 0.93f, 0.91f), 0.3f);
        var roof = Lit("HutRoof", new Color(0.20f, 0.27f, 0.22f), 0.35f, 0.2f);
        var glass = Lit("HutGlass", new Color(0.05f, 0.08f, 0.10f), 0.95f, 0.2f);
        var stone = Lit("HutStone", new Color(0.52f, 0.49f, 0.44f), 0.15f);

        // --- warming hut: octagon 9 m across the flats, pyramid roof + cupola, French doors on the lake face
        float gy = terrain.SampleHeight(V3(HUT_C, 0)) + terrain.transform.position.y;
        var hut = new GameObject("WarmingHut").transform; hut.SetParent(sroot, false);
        hut.position = V3(HUT_C, gy); hut.rotation = Quaternion.LookRotation(V3(LAKEWARD, 0), Vector3.up);   // +Z = lake
        float ap = 4.5f, side = 2f * ap * Mathf.Tan(Mathf.PI / 8f), wallH = 3.1f;
        OctPrism("Floor", hut, ap + 0.35f, -0.6f, 0.75f, stone);
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f; var dir = Quaternion.Euler(0, a, 0);
            var face = new GameObject("Face" + k).transform; face.SetParent(hut, false);
            face.localPosition = dir * new Vector3(0, 0, ap - 0.1f); face.localRotation = dir;
            if (k == 0)
            {   // French doors: two glazed leaves, transom, white frames, a step down to the dock path
                Box("DoorHeadWall", face, new Vector3(0, 0.15f + 2.65f + 0.25f, 0), new Vector3(side, wallH - 2.65f - 0.15f, 0.2f), siding);
                for (int s = -1; s <= 1; s += 2)
                {
                    Box("Jamb" + s, face, new Vector3(s * (side / 2 - 0.18f), 1.5f, 0), new Vector3(0.36f, 2.7f, 0.22f), siding);
                    Box("DoorGlass" + s, face, new Vector3(s * 0.42f, 1.4f, 0.0f), new Vector3(0.76f, 2.3f, 0.04f), glass);
                    Box("DoorStile" + s, face, new Vector3(s * 0.82f, 1.4f, 0.04f), new Vector3(0.08f, 2.4f, 0.08f), trim);
                    for (int m = 0; m < 3; m++) Box($"Muntin{s}_{m}", face, new Vector3(s * 0.42f, 0.75f + m * 0.6f, 0.04f), new Vector3(0.76f, 0.04f, 0.05f), trim);
                }
                Box("DoorMeet", face, new Vector3(0, 1.4f, 0.05f), new Vector3(0.08f, 2.4f, 0.08f), trim);
                Box("DoorHead", face, new Vector3(0, 2.62f, 0.05f), new Vector3(1.8f, 0.1f, 0.1f), trim);
                Box("Step", hut, new Vector3(0, 0.05f, ap + 0.75f), new Vector3(2.4f, 0.12f, 0.9f), stone);
            }
            else
            {
                Box("Wall", face, new Vector3(0, wallH / 2 + 0.15f, 0), new Vector3(side, wallH, 0.2f), siding);
                if (k % 2 == 0) { Box("Win", face, new Vector3(0, 1.9f, 0.11f), new Vector3(1.4f, 1.1f, 0.03f), glass); Box("WinFrame", face, new Vector3(0, 1.9f, 0.105f), new Vector3(1.55f, 1.25f, 0.02f), trim); }
            }
            Box("Corner", hut, Quaternion.Euler(0, a + 22.5f, 0) * new Vector3(0, wallH / 2 + 0.15f, ap / Mathf.Cos(Mathf.PI / 8f) - 0.1f), new Vector3(0.18f, wallH, 0.18f), trim, new Vector3(0, a + 22.5f, 0));
        }
        OctPrism("Fascia", hut, ap + 0.45f, wallH + 0.15f, 0.25f, trim);
        MeshObj("Roof", hut, OctPyramid("hut_roof", ap + 0.6f, 2.4f), roof).transform.localPosition = new Vector3(0, wallH + 0.4f, 0);
        OctPrism("Cupola", hut, 0.65f, wallH + 2.15f, 0.9f, trim);
        MeshObj("CupolaRoof", hut, OctPyramid("hut_cupola_roof", 0.85f, 0.7f), roof).transform.localPosition = new Vector3(0, wallH + 3.05f, 0);

        // --- start dock 2.4 x 8 m, freeboard 0.35 m, posts every 2 m, no lights
        var dock = new GameObject("StartDock").transform; dock.SetParent(sroot, false);
        dock.position = V3(DOCK_ROOT, 0f); dock.rotation = Quaternion.LookRotation(V3(DOCK_DIR, 0), Vector3.up);
        Box("Deck", dock, new Vector3(0, 0.35f, 4f), new Vector3(2.4f, 0.1f, 8f), wood);
        Box("Landing", dock, new Vector3(0, 0.38f, -1.2f), new Vector3(3.2f, 0.12f, 2.4f), wood);
        for (int i = 0; i <= 4; i++) for (int s = -1; s <= 1; s += 2)
                Box($"Post{i}_{s}", dock, new Vector3(s * 1.1f, -0.4f, i * 2f), new Vector3(0.18f, 1.6f, 0.18f), post);
        Box("StepOff", dock, new Vector3(0, 0.12f, 8.35f), new Vector3(1.6f, 0.06f, 0.6f), wood);

        // --- lifts L1-L5 with canopies, each holding an unbranded open-bow runabout in its own colour
        var canopyMesh = Gable("lift_canopy", 3.4f, 8.5f, 0.55f); var hullMesh = Hull("runabout_hull");   // once: SaveMesh replaces an asset of the same name
        for (int i = 0; i < LIFT_C.Length; i++)
        {
            var lift = new GameObject("Lift_L" + (i + 1)).transform; lift.SetParent(sroot, false);
            lift.position = V3(LIFT_C[i], 0f); lift.rotation = Quaternion.LookRotation(V3(LIFT_AXIS, 0), Vector3.up);
            for (int c = 0; c < 4; c++) { float sx = (c % 2 == 0 ? -1 : 1) * 1.5f, sz = (c < 2 ? -1 : 1) * 3.9f; Box("Leg" + c, lift, new Vector3(sx, 1.0f, sz), new Vector3(0.12f, 4.4f, 0.12f), alu); }
            for (int s = -1; s <= 1; s += 2) { Box("Bunk" + s, lift, new Vector3(s * 0.55f, 0.55f, 0), new Vector3(0.14f, 0.12f, 6.0f), post); Box("Rail" + s, lift, new Vector3(s * 1.5f, 3.2f, 0), new Vector3(0.1f, 0.1f, 8.0f), alu); }
            MeshObj("Canopy", lift, canopyMesh, canopy).transform.localPosition = new Vector3(0, 3.25f, 0);
            var hull = Lit("BoatHull_L" + (i + 1), Hex(LIFT_BOAT[i][0]), 0.75f);
            var boat = MeshObj("Boat", lift, hullMesh, hull).transform; boat.localPosition = new Vector3(0, 0.62f, 0.2f);
            boat.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            var inner = Lit("BoatInterior", new Color(0.85f, 0.83f, 0.78f), 0.3f);
            Box("Cockpit", boat, new Vector3(0, 0.80f, -0.3f), new Vector3(1.75f, 0.06f, 4.2f), inner);
            Box("Windshield", boat, new Vector3(0, 1.05f, 0.75f), new Vector3(1.8f, 0.42f, 0.04f), glass, new Vector3(-25f, 0, 0));
            if (LIFT_BOAT[i][1].Length > 0)
            {
                var stripe = Lit("BoatStripe_L" + (i + 1), Hex(LIFT_BOAT[i][1]), 0.6f);
                for (int s = -1; s <= 1; s += 2) Box("Stripe" + s, boat, new Vector3(s * 1.07f, 0.62f, -0.4f), new Vector3(0.02f, 0.12f, 4.6f), stripe);
            }
        }
        foreach (var t in sroot.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        foreach (var r in sroot.GetComponentsInChildren<MeshRenderer>(true)) { r.shadowCastingMode = ShadowCastingMode.On; r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox; }

        // --- Lot 15 (8409): open lawn with one tree in the middle
        if (lotPlaces != null)
            foreach (var lp in lotPlaces)
                if (lp.vacant)
                {
                    var c = lp.World(0.5f * (lp.x0 + lp.x1), lp.shoreZ * 0.5f);
                    var td = terrain.terrainData; var tp = terrain.transform.position;
                    int proto = 5;   // maple (broadleaf), see BuildTreesV5 prototypes
                    terrain.AddTreeInstance(new TreeInstance
                    {
                        position = new Vector3((c.x - tp.x) / td.size.x, 0f, (c.z - tp.z) / td.size.z), prototypeIndex = proto,
                        heightScale = 1.15f, widthScale = 1.2f, rotation = 0.7f, color = Color.white, lightmapColor = Color.white
                    });
                    terrain.Flush();
                    Debug.Log($"[SkiView] south end: lot 15 ({lp.lot}) lawn, one central tree at ({c.x:0.0},{c.z:0.0})");
                }
        Debug.Log("[SkiView] south end: warming hut, start dock, lifts L1-L5 built (no lamp posts)");
    }

    static GameObject OctPrism(string name, Transform parent, float apothem, float y0, float hgt, Material m)
    {
        float R = apothem / Mathf.Cos(Mathf.PI / 8f);
        var v = new List<Vector3>(); var t = new List<int>();
        for (int k = 0; k < 8; k++)
        {
            float a0 = (k * 45f + 22.5f) * Mathf.Deg2Rad, a1 = ((k + 1) * 45f + 22.5f) * Mathf.Deg2Rad;
            Vector3 p0 = new Vector3(Mathf.Sin(a0) * R, 0, Mathf.Cos(a0) * R), p1 = new Vector3(Mathf.Sin(a1) * R, 0, Mathf.Cos(a1) * R);
            int b = v.Count; v.Add(p0); v.Add(p1); v.Add(p1 + Vector3.up * hgt); v.Add(p0 + Vector3.up * hgt);
            t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            int c = v.Count; v.Add(Vector3.up * hgt); v.Add(p0 + Vector3.up * hgt); v.Add(p1 + Vector3.up * hgt); t.AddRange(new[] { c, c + 1, c + 2 });
        }
        var mesh = new Mesh { name = "oct_" + name }; mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var g = MeshObj(name, parent, SaveMesh(mesh, "south_oct_" + name.ToLower()), m); g.transform.localPosition = new Vector3(0, y0, 0); return g;
    }

    static Mesh OctPyramid(string name, float apothem, float rise)
    {
        float R = apothem / Mathf.Cos(Mathf.PI / 8f);
        var v = new List<Vector3>(); var t = new List<int>();
        for (int k = 0; k < 8; k++)
        {
            float a0 = (k * 45f + 22.5f) * Mathf.Deg2Rad, a1 = ((k + 1) * 45f + 22.5f) * Mathf.Deg2Rad;
            int b = v.Count; v.Add(new Vector3(Mathf.Sin(a0) * R, 0, Mathf.Cos(a0) * R)); v.Add(Vector3.up * rise); v.Add(new Vector3(Mathf.Sin(a1) * R, 0, Mathf.Cos(a1) * R));
            t.AddRange(new[] { b, b + 1, b + 2 });
        }
        TwoSided(v, t);   // eave soffit
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, name);
    }

    /// Back faces on their own vertices so the recalculated normals don't cancel (shared-vertex two-siding rendered black),
    /// dropped slightly so they don't shadow the top surface.
    static void TwoSided(List<Vector3> v, List<int> t, float drop = 0.04f)
    {
        int n = v.Count, tc = t.Count;
        for (int i = 0; i < n; i++) v.Add(v[i] - Vector3.up * drop);   // underside 4 cm lower: no coplanar self-shadowing
        for (int i = 0; i < tc; i += 3) { t.Add(t[i] + n); t.Add(t[i + 2] + n); t.Add(t[i + 1] + n); }
    }

    static Mesh Gable(string name, float w, float l, float rise)
    {
        float hw = w / 2, hl = l / 2;
        var v = new List<Vector3> { new Vector3(-hw, 0, -hl), new Vector3(0, rise, -hl), new Vector3(0, rise, hl), new Vector3(-hw, 0, hl),
                                    new Vector3(hw, 0, -hl), new Vector3(0, rise, -hl), new Vector3(0, rise, hl), new Vector3(hw, 0, hl) };
        var t = new List<int> { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 }; TwoSided(v, t);
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, name);
    }

    /// 6.4 m open-bow runabout hull: flat sheer at 0.75 m, vee bottom, pointed bow at +Z. Plain colour, no livery or text.
    static Mesh Hull(string name)
    {
        float[] zs = { -3.1f, -1.5f, 0.5f, 2.0f, 3.3f }; float[] hw = { 1.05f, 1.1f, 1.08f, 0.8f, 0.05f }; float[] keel = { 0.05f, 0f, 0.02f, 0.15f, 0.55f };
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < zs.Length; i++) { v.Add(new Vector3(-hw[i], 0.75f, zs[i])); v.Add(new Vector3(-hw[i] * 0.8f, keel[i] + 0.22f, zs[i])); v.Add(new Vector3(0, keel[i], zs[i])); v.Add(new Vector3(hw[i] * 0.8f, keel[i] + 0.22f, zs[i])); v.Add(new Vector3(hw[i], 0.75f, zs[i])); }
        for (int i = 0; i < zs.Length - 1; i++) for (int k = 0; k < 4; k++) { int a = i * 5 + k, b = a + 1, c = a + 5, d = c + 1; t.AddRange(new[] { a, b, c, b, d, c }); }
        int s = v.Count; for (int k = 0; k < 5; k++) v.Add(v[k]); t.AddRange(new[] { s, s + 1, s + 4, s + 1, s + 3, s + 4, s + 1, s + 2, s + 3 }); TwoSided(v, t, 0f);   // transom; open hull seen from inside too
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, name);
    }
}
