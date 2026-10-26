using System.Collections.Generic;
using SkiView;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Pass 19: first-person arms rebuilt for realism. A real slalom handle (30 cm aluminium bar, ribbed rubber grip, end plugs,
/// bridle eyelets), two gloves modelled as an overhand grip (four curled fingers wrapped round the grip, knuckle line, back of the
/// hand sloping to the wrist, thumb under the bar, cuff with a strap), skin forearms tapering to the elbow, and upper arms ending in
/// the rash-guard sleeve hem. The forearms and upper arms are separate pieces that SkiViewGame poses every frame with a two-bone
/// IK from fixed shoulders (slight elbow bend, lean- and load-driven handle motion). Camera-space, FP layer, no shadows.</summary>
public static partial class SkiViewBuilder
{
    /// <summary>Tube / loft through centre points; each section an ellipse (rx across, ry along the frame normal). The frame is
    /// parallel-transported from upHint. Caps both ends.</summary>
    static Mesh FpLoft(string name, IList<Vector3> c, IList<Vector2> r, Vector3 upHint, int sides = 12, bool caps = true)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int N = c.Count; Vector3 up = upHint.normalized; float acc = 0f;
        var frames = new List<(Vector3 tg, Vector3 nu, Vector3 bi)>();
        for (int i = 0; i < N; i++)
        {
            Vector3 tg = (c[Mathf.Min(i + 1, N - 1)] - c[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 nu = Vector3.ProjectOnPlane(up, tg); if (nu.sqrMagnitude < 1e-8f) nu = Vector3.ProjectOnPlane(Vector3.forward, tg); nu.Normalize();
            up = nu; frames.Add((tg, nu, Vector3.Cross(tg, nu).normalized));
        }
        for (int i = 0; i < N; i++)
        {
            if (i > 0) acc += Vector3.Distance(c[i], c[i - 1]);
            var f = frames[i];
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Vector3 off = f.bi * (ca * r[i].x) + f.nu * (sa * r[i].y);
                v.Add(c[i] + off); n.Add((f.bi * (ca / Mathf.Max(1e-4f, r[i].x)) + f.nu * (sa / Mathf.Max(1e-4f, r[i].y))).normalized);
                uv.Add(new Vector2(s / (float)sides, acc * 8f));
            }
        }
        int row = sides + 1;
        for (int i = 0; i < N - 1; i++) for (int s = 0; s < sides; s++)
        { int a = i * row + s, b = a + 1, a2 = a + row, b2 = b + row; t.AddRange(new[] { a, a2, b, b, a2, b2 }); }
        if (caps)
            foreach (int end in new[] { 0, N - 1 })
            {
                var f = frames[end]; Vector3 nn = end == 0 ? -f.tg : f.tg; int ci = v.Count; v.Add(c[end]); n.Add(nn); uv.Add(new Vector2(0.5f, 0.5f));
                int b0 = v.Count;
                for (int s = 0; s <= sides; s++) { v.Add(v[end * row + s]); n.Add(nn); uv.Add(Vector2.zero); }
                for (int s = 0; s < sides; s++) { if (end == 0) t.AddRange(new[] { ci, b0 + s, b0 + s + 1 }); else t.AddRange(new[] { ci, b0 + s + 1, b0 + s }); }
            }
        // winding check: outward normals
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        if (Vector3.Dot(Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]), n[t[0]]) < 0f) { for (int k = 0; k < t.Count; k += 3) { int q = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = q; } m.SetTriangles(t, 0); }
        m.RecalculateBounds(); m.RecalculateTangents();
        return SaveMesh(m, name);
    }

    static GameObject LoftObj(string name, Transform parent, Mesh m, Material mat)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat; return g;
    }

    static List<Vector3> Arc(float x, float d0, float d1, float a0, float a1, int k)
    {
        var l = new List<Vector3>();
        for (int i = 0; i <= k; i++) { float u = i / (float)k, a = Mathf.Lerp(a0, a1, u) * Mathf.Deg2Rad, d = Mathf.Lerp(d0, d1, u); l.Add(new Vector3(x, d * Mathf.Cos(a), d * Mathf.Sin(a))); }
        return l;
    }
    static List<Vector2> Taper(int n, float r0, float r1, float squash = 1f) { var l = new List<Vector2>(); for (int i = 0; i < n; i++) { float r = Mathf.Lerp(r0, r1, i / (float)Mathf.Max(1, n - 1)); l.Add(new Vector2(r, r * squash)); } return l; }

    public const float FP_HAND_X = 0.058f, FP_FOREARM = 0.265f, FP_UPPERARM = 0.30f;
    public static readonly Vector3 FP_WRIST = new Vector3(0f, -0.012f, -0.088f);   // wrist pivot in glove space

    static void BuildFirstPersonRig(Camera cam, SkiViewGame game)
    {
        var rig = new GameObject("FP_Rig").transform; rig.SetParent(cam.transform, false);
        var skin = Lit("FP_Skin19", new Color(0.78f, 0.56f, 0.46f), 0.52f);                 // wet skin, rider tone (Remy body albedo mean)
        var glove = Lit("FP_Glove19", new Color(0.541f, 0.565f, 0.596f), 0.38f);             // b10.19 T30 ~#8A9098 vs water
        var gloveAcc = Lit("FP_GloveAccent19", new Color(0.102f, 0.122f, 0.141f), 0.28f);   // b10.19 T30 rim #1A1F24
        var sleeve = Lit("FP_Sleeve19", new Color(0.10f, 0.11f, 0.13f), 0.38f);             // rash guard (Derived/rashguard.png mean)
        var rubber = Lit("FP_GripRubber19", new Color(0.07f, 0.07f, 0.075f), 0.18f);
        var alu = Lit("FP_HandleAlu19", new Color(0.72f, 0.73f, 0.75f), 0.7f, 0.9f);
        var plug = Lit("FP_HandlePlug19", new Color(0.10f, 0.20f, 0.42f), 0.35f);
        var hnd = new GameObject("FP_Handle").transform; hnd.SetParent(rig, false);
        // ---- handle: 30 cm bar along X, 2.7 cm ribbed rubber grip over 26 cm, aluminium showing at the ends, plugs, eyelets
        {
            var c = new List<Vector3>(); var r = new List<Vector2>();
            for (float x = -0.130f; x <= 0.1301f; x += 0.0025f) { float rib = 0.5f + 0.5f * Mathf.Cos(x / 0.005f * Mathf.PI); c.Add(new Vector3(x, 0, 0)); r.Add(Vector2.one * (0.0128f + 0.0007f * rib)); }
            LoftObj("Grip", hnd, FpLoft("fp19_grip", c, r, Vector3.up, 14), rubber);
            LoftObj("Bar", hnd, FpLoft("fp19_bar", new[] { new Vector3(-0.150f, 0, 0), new Vector3(0.150f, 0, 0) }, new[] { Vector2.one * 0.0115f, Vector2.one * 0.0115f }, Vector3.up, 12), alu);
            for (int k = -1; k <= 1; k += 2)
            {
                LoftObj("Plug", hnd, FpLoft("fp19_plug" + (k < 0 ? "L" : "R"), new[] { new Vector3(k * 0.149f, 0, 0), new Vector3(k * 0.156f, 0, 0), new Vector3(k * 0.158f, 0, 0) }, new[] { Vector2.one * 0.0135f, Vector2.one * 0.0135f, Vector2.one * 0.010f }, Vector3.up, 12), plug);
                var eye = Arc(0f, 0.0105f, 0.0105f, 0f, 360f, 14); for (int i = 0; i < eye.Count; i++) eye[i] = new Vector3(k * 0.141f, eye[i].y, eye[i].z);
                LoftObj("Eyelet", hnd, FpLoft("fp19_eyelet" + (k < 0 ? "L" : "R"), eye, Taper(eye.Count, 0.0028f, 0.0028f), Vector3.right, 6, false), alu);
            }
        }
        // ---- gloves: overhand grip round the bar (glove space: bar along X, +Z toward the boat, +Y up)
        const float RB = 0.0135f;
        for (int k = -1; k <= 1; k += 2)
        {
            var g = new GameObject(k < 0 ? "Glove_L" : "Glove_R").transform; g.SetParent(hnd, false); g.localPosition = new Vector3(k * FP_HAND_X, 0, 0);
            string s = k < 0 ? "L" : "R";
            // fingers, little finger outermost: x offsets from the hand centre (thumb side = toward the other hand = -k)
            float[] fx = { -0.031f, -0.0105f, 0.0105f, 0.031f }; float[] fr = { 0.0094f, 0.0098f, 0.0094f, 0.0082f }; float[] reach = { 205f, 212f, 205f, 192f };
            for (int i = 0; i < 4; i++)
            {
                float x = -k * fx[i];   // i = 0 index finger (inner), 3 little finger (outer)
                var path = new List<Vector3> { new Vector3(x, 0.030f, -0.006f) };
                path.AddRange(Arc(x, RB + fr[i] + 0.010f, RB + fr[i] * 0.95f, 18f, 70f, 3));
                path.AddRange(Arc(x, RB + fr[i] * 0.95f, RB + fr[i] * 0.88f, 95f, reach[i], 5));
                var rr = new List<Vector2>(); for (int q = 0; q < path.Count; q++) { float u = q / (float)(path.Count - 1); float rf = fr[i] * Mathf.Lerp(1.08f, 0.82f, u) * (q == 3 || q == 6 ? 1.07f : 1f); rr.Add(new Vector2(rf, rf * 0.9f)); }
                LoftObj("Finger" + i, g, FpLoft($"fp19_finger{s}{i}", path, rr, Vector3.right, 10), glove);
            }
            // back of the hand + palm (metacarpals): knuckle line on top-front of the grip, sloping back and down to the wrist
            {
                var c = new List<Vector3> { new Vector3(0, 0.029f, 0.004f), new Vector3(0, 0.026f, -0.020f), new Vector3(0, 0.016f, -0.046f), new Vector3(0, 0.002f, -0.070f), FP_WRIST + new Vector3(0, 0.003f, 0.004f) };
                var r = new List<Vector2> { new Vector2(0.043f, 0.013f), new Vector2(0.045f, 0.0165f), new Vector2(0.043f, 0.018f), new Vector2(0.037f, 0.019f), new Vector2(0.032f, 0.022f) };
                LoftObj("Hand", g, FpLoft("fp19_hand" + s, c, r, Vector3.up, 16), glove);
                // knuckle guard (raised padded strip over the knuckles) and the wrist strap
                LoftObj("KnuckleGuard", g, FpLoft("fp19_knuckle" + s, new[] { new Vector3(-0.036f, 0.039f, -0.004f), new Vector3(0, 0.0415f, -0.006f), new Vector3(0.036f, 0.039f, -0.004f) }, new[] { new Vector2(0.008f, 0.0045f), new Vector2(0.009f, 0.005f), new Vector2(0.008f, 0.0045f) }, Vector3.up, 8), gloveAcc);
            }
            // thumb: from the heel on the inner side, under the grip to the front-bottom
            {
                float xi = -k * 0.036f;
                var c = new List<Vector3> { new Vector3(xi, -0.002f, -0.060f), new Vector3(xi * 0.95f, -0.018f, -0.036f), new Vector3(xi * 0.80f, -0.027f, -0.012f), new Vector3(xi * 0.62f, -0.026f, 0.008f), new Vector3(xi * 0.48f, -0.019f, 0.021f) };
                LoftObj("Thumb", g, FpLoft("fp19_thumb" + s, c, Taper(c.Count, 0.0125f, 0.0088f, 0.9f), Vector3.up, 10), glove);
            }
            // forearm (wrist -> elbow along local +Z; posed by IK): glove cuff + strap, then skin, tapering out to the elbow
            var fa = new GameObject("Forearm_" + s).transform; fa.SetParent(rig, false);
            {
                LoftObj("Cuff", fa, FpLoft("fp19_cuff" + s, new[] { new Vector3(0, 0, -0.012f), new Vector3(0, 0, 0.012f), new Vector3(0, 0, 0.050f) }, new[] { new Vector2(0.031f, 0.025f), new Vector2(0.034f, 0.028f), new Vector2(0.036f, 0.030f) }, Vector3.up, 14), glove);
                LoftObj("Strap", fa, FpLoft("fp19_strap" + s, new[] { new Vector3(0, 0, 0.018f), new Vector3(0, 0, 0.040f) }, new[] { new Vector2(0.0375f, 0.0315f), new Vector2(0.0385f, 0.0325f) }, Vector3.up, 14), gloveAcc);
                var c = new List<Vector3>(); var r = new List<Vector2>();
                float[] zz = { 0.040f, 0.080f, 0.130f, 0.180f, 0.225f, 0.255f, FP_FOREARM + 0.03f };
                float[] rx = { 0.0325f, 0.0345f, 0.0385f, 0.0430f, 0.0450f, 0.0440f, 0.0420f }, ry = { 0.0265f, 0.0285f, 0.0320f, 0.0360f, 0.0385f, 0.0400f, 0.0410f };
                for (int i = 0; i < zz.Length; i++) { c.Add(new Vector3(0, 0, zz[i])); r.Add(new Vector2(rx[i], ry[i])); }
                LoftObj("Skin", fa, FpLoft("fp19_forearm" + s, c, r, Vector3.up, 16), skin);
            }
            // upper arm (elbow -> shoulder along local +Z): skin, then the rash-guard sleeve with a rolled hem
            var ua = new GameObject("UpperArm_" + s).transform; ua.SetParent(rig, false);
            {
                LoftObj("Skin", ua, FpLoft("fp19_upperskin" + s, new[] { new Vector3(0, 0, -0.03f), new Vector3(0, 0, 0.06f), new Vector3(0, 0, 0.13f) }, new[] { new Vector2(0.041f, 0.040f), new Vector2(0.044f, 0.042f), new Vector2(0.046f, 0.044f) }, Vector3.up, 14), skin);
                LoftObj("Hem", ua, FpLoft("fp19_hem" + s, new[] { new Vector3(0, 0, 0.112f), new Vector3(0, 0, 0.122f), new Vector3(0, 0, 0.132f) }, new[] { new Vector2(0.0505f, 0.0485f), new Vector2(0.0525f, 0.0505f), new Vector2(0.0515f, 0.0495f) }, Vector3.up, 14), sleeve);
                LoftObj("Sleeve", ua, FpLoft("fp19_sleeve" + s, new[] { new Vector3(0, 0, 0.128f), new Vector3(0, 0, 0.20f), new Vector3(0, 0, FP_UPPERARM + 0.02f) }, new[] { new Vector2(0.050f, 0.048f), new Vector2(0.054f, 0.051f), new Vector2(0.058f, 0.054f) }, Vector3.up, 14), sleeve);
            }
            if (k < 0) { game.fpForearmL = fa; game.fpUpperL = ua; game.fpGloveL = g; } else { game.fpForearmR = fa; game.fpUpperR = ua; game.fpGloveR = g; }
        }
        // bridle: two legs from the eyelets forward to the apex (the main rope starts at the apex)
        var rope = Lit("Rope", new Color(0.95f, 0.85f, 0.25f), 0.2f);
        Vector3 apex = new Vector3(0, 0.02f, 0.45f);
        for (int k = -1; k <= 1; k += 2)
        {
            Vector3 e = new Vector3(k * 0.141f, 0, 0.012f);
            LoftObj("Bridle", hnd, FpLoft("fp19_bridle" + (k < 0 ? "L" : "R"), new[] { e, Vector3.Lerp(e, apex, 0.5f) + new Vector3(0, -0.004f, 0), apex }, Taper(3, 0.0042f, 0.0042f), Vector3.up, 6), rope);
        }
        var ap = new GameObject("FP_Apex").transform; ap.SetParent(hnd, false); ap.localPosition = apex;
        foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PlanarReflection.FP_LAYER;
        foreach (var r in rig.GetComponentsInChildren<MeshRenderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true; }
        var (b, a) = MergeRig(rig, new[] { hnd, game.fpForearmL, game.fpForearmR, game.fpUpperL, game.fpUpperR }, "fp_rig19");
        rig.gameObject.SetActive(false);
        game.fpRig = rig; game.fpHandle = hnd; game.fpApex = ap;
        game.fpHandleLocal = new Vector3(0f, -0.34f, 0.70f);
        Debug.Log($"[SkiView] first-person rig v19: 30 cm handle, gloved overhand grip, IK forearms + sleeves ({b} -> {a} draws), layer {PlanarReflection.FP_LAYER}");
    }
}
