using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using SkiView;

/// Pass 13: tournament jump ramp (replaces the pass-1 greybox placeholder).
/// Rules (AWSA 2026 9.16C / IWWF 10.02, research notes in N1-PROGRESS pass 13):
///  - inclined plane, 0.255 ratio: 6.85 m of surface out of the water rising to a 1.75 m top edge (max 1.785 m), 0.6 m of surface
///    under water (min 0.5 m), 4.22 m wide (3.70-4.30 m), flat, fibreglass, waxed and kept wet by a watering system at the top;
///  - aprons the full length of both sides, 45 deg from vertical (28-60), >= 20 cm under water, contrasting colour (navy);
///  - 6.50 m and 7.00 m length marks from the top edge; a front-face water line 455 mm below the top edge near the anchor eyes;
///  - height set by rotating the top edge on hydraulic jacks from the front "kneeler" deck; floats under the front; anchored
///    with lines from corner eyes to anchors 3-4x the water depth away.
///  - Course (AWSA p.94): ramp centreline 15 m from the jump course X axis, opened +1.75 deg (low end toward the course).
///    Holly's fit test (roadmap/LAKE-LAYOUTS.md 0.2): ramp centre >= 18 m from the slalom line, so the X axis sits 3.5 m off the
///    slalom line on the ramp side and the ramp centreline is 18.5 m out (Scott: south end, house side).
public static partial class SkiViewBuilder
{
    public const float RAMP_FROM_XAXIS = 15f, RAMP_UNDER = 0.6f, APRON_DEPTH = 0.25f;
    public static float RAMP_FROM_SLALOM = 18.5f;   // pass 17: per lake (Saltwhite 22 m)
    static Transform builtRamp, builtJumpCourse; static Vector3[] builtWaterline; static List<ParticleSystem> builtRampFx = new List<ParticleSystem>(); static ParticleSystem builtRampFoam;

    static Material RampMat(string name, string albedo, string mask, string normal, Color tint, float bump = 0.35f, float smoothScale = 1f)
    {
        foreach (var t in new[] { albedo, mask, normal })
        {
            if (t == null) continue;
            if (AssetImporter.GetAtPath(GEN + t) is TextureImporter ti)
            {
                bool isN = t == normal, want = ti.maxTextureSize != 1024 || ti.anisoLevel < 4 || (isN && ti.textureType != TextureImporterType.NormalMap) || (t == mask && ti.sRGBTexture);
                if (want) { ti.maxTextureSize = 1024; ti.anisoLevel = 4; ti.wrapMode = TextureWrapMode.Clamp; if (isN) ti.textureType = TextureImporterType.NormalMap; if (t == mask) ti.sRGBTexture = false; ti.SaveAndReimport(); }
            }
        }
        var m = Lit(name, tint, 0.9f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GEN + albedo));
        if (mask != null) { m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GEN + mask)); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", smoothScale); }
        if (normal != null) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GEN + normal)); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
        else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        EditorUtility.SetDirty(m); return m;
    }

    static int[] DoubleSided(List<int> t) { var o = new List<int>(t); for (int i = 0; i < t.Count; i += 3) { o.Add(t[i]); o.Add(t[i + 2]); o.Add(t[i + 1]); } return o.ToArray(); }

    static Material FilmMat(string name, float edgeFade, Color foam, Vector4 scroll, float opacity)
    {
        string tp = GEN + "ramp_waterfilm.png";
        if (AssetImporter.GetAtPath(tp) is TextureImporter ti && (ti.sRGBTexture || ti.wrapMode != TextureWrapMode.Repeat || ti.maxTextureSize != 512))
        { ti.sRGBTexture = false; ti.wrapMode = TextureWrapMode.Repeat; ti.maxTextureSize = 512; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
        string mp = "Assets/SkiView/Materials/" + name + ".mat";
        var sh = Shader.Find("SkiView/RampWaterFilm");
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, mp); }
        m.shader = sh;
        m.SetTexture("_FilmTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
        m.SetTextureScale("_FilmTex", edgeFade > 0 ? Vector2.one : new Vector2(0.45f, 0.35f));   // broader, sparser rivulets on the deck
        m.SetVector("_Scroll", scroll); m.SetFloat("_Opacity", opacity); m.SetFloat("_EdgeFade", edgeFade); m.SetColor("_FoamCol", foam);
        m.SetFloat("_Reflect", edgeFade > 0 ? 1f : 0.55f);   // pass 15: less white sky sheen over the red deck at low angles (read pink)
        EditorUtility.SetDirty(m); return m;
    }

    static void BuildRamp(Venue v, Transform root)
    {
        builtRamp = null; builtJumpCourse = null; builtRampFx.Clear(); builtRampFoam = null;
        if (v.ramp == null || v.ramp.lip == null || v.ramp.lip.Length < 2) { Debug.LogWarning("[SkiView] no ramp in venue.json"); return; }
        var rd = v.ramp;
        Vector3 f = new Vector3(rd.fwd[0], 0, rd.fwd[1]).normalized;            // jump direction (southbound course direction)
        Vector3 lip0 = new Vector3(rd.lip[0], 0, rd.lip[1]);
        float along = Vector3.Dot(lip0, f); Vector3 latV = lip0 - f * along; Vector3 side = latV.normalized;   // toward the ramp (east, house side)
        Vector3 lip = f * along + side * RAMP_FROM_SLALOM;
        var go = new GameObject("JumpRamp"); go.transform.SetParent(root, false); builtRamp = go.transform;
        go.transform.position = lip;
        // opened +1.75 deg: the low end turns toward the course. Heading along f, the course is on the -side side
        float openSign = Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, f), side));   // +1 if the ramp is on the skier's right
        go.transform.rotation = Quaternion.LookRotation(f) * Quaternion.Euler(0, openSign * rd.open_deg, 0);
        float sinA = rd.ratio, cosA = Mathf.Sqrt(1 - sinA * sinA), H = rd.height, Ls = rd.length, hw = rd.width * 0.5f, Lt = Ls + RAMP_UNDER;
        Vector3 S(float x, float s) => new Vector3(x, s * sinA, -(Ls - s) * cosA);          // surface point; s = metres up the surface from the waterline
        var surf = RampMat("RampSurfaceWet", "ramp_surface_albedo.png", "ramp_surface_mask.png", "ramp_surface_normal.png", Color.white, 0.6f, 0.74f);   // pass 14: red waxed fibreglass; pass 15: gloss x0.74 (pink at grazing angles)
        var apron = RampMat("RampApronGelcoat", "ramp_apron_albedo.png", "ramp_apron_mask.png", "ramp_apron_normal.png", Color.white, 0.9f);   // pass 14: white gelcoat on an aluminium frame; pass 15: + normal map (stiffeners, bolts, oil-canned panels)
        var alu = Lit("RampAluminium", new Color(0.70f, 0.72f, 0.74f), 0.75f, 0.9f);
        var steel = Lit("RampSteelDark", new Color(0.16f, 0.17f, 0.18f), 0.45f, 0.6f);
        var chrome = Lit("RampJackRod", new Color(0.85f, 0.86f, 0.88f), 0.9f, 1f);
        var deck = Lit("RampDeckBoards", new Color(0.36f, 0.36f, 0.34f), 0.35f);
        var floatM = Lit("RampFloatHDPE", new Color(0.13f, 0.13f, 0.14f), 0.3f);
        var pvc = Lit("RampPVC", new Color(0.78f, 0.79f, 0.80f), 0.45f);
        var hose = Lit("RampHose", new Color(0.05f, 0.05f, 0.06f), 0.4f);
        var lineM = Lit("RampAnchorLine", new Color(0.85f, 0.70f, 0.10f), 0.3f);
        var black = Lit("RampMarkBlack", new Color(0.03f, 0.03f, 0.035f), 0.4f);

        // ---------- mesh: surface (submesh 0), aprons + front skirt (submesh 1)
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var t0 = new List<int>(); var t1 = new List<int>();
        void Quad(List<int> L, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        { int k = vs.Count; vs.Add(a); vs.Add(b); vs.Add(c); vs.Add(d); uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud); L.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 }); }
        float s0 = -RAMP_UNDER;
        // surface, split along its length so the normal map / lighting interpolate nicely
        int NS = 12;
        for (int i = 0; i < NS; i++)
        {
            float sa = Mathf.Lerp(s0, Ls, i / (float)NS), sb = Mathf.Lerp(s0, Ls, (i + 1) / (float)NS);
            float va = (sa - s0) / Lt, vb = (sb - s0) / Lt;
            Quad(t0, S(-hw, sa), S(-hw, sb), S(hw, sb), S(hw, sa), new Vector2(0, va), new Vector2(0, vb), new Vector2(1, vb), new Vector2(1, va));
        }
        // slab edge under the top edge (the 8 cm thickness shows from the front, aluminium-capped separately)
        // aprons: three rows per column (top edge, waterline, bottom) so the texture's waterline band always sits at the water
        const float VW = 0.74f;
        int NA = 14;
        foreach (int sd in new[] { -1, 1 })
            for (int i = 0; i < NA; i++)
            {
                float sa = Mathf.Lerp(s0, Ls, i / (float)NA), sb = Mathf.Lerp(s0, Ls, (i + 1) / (float)NA);
                Vector3 ta = S(sd * hw, sa), tb = S(sd * hw, sb);
                Vector3 Down(Vector3 top, float y) { float dy = top.y - y; return new Vector3(top.x + sd * dy, y, top.z); }   // 45 deg out and down
                Vector3 wa = ta.y > 0.02f ? Down(ta, 0f) : ta, wb = tb.y > 0.02f ? Down(tb, 0f) : tb;
                Vector3 ba = Down(ta, -APRON_DEPTH), bb = Down(tb, -APRON_DEPTH);
                float ua = (ta.z + Ls * cosA) / 8.8f, ub = (tb.z + Ls * cosA) / 8.8f;
                if (sd > 0) { Quad(t1, ta, tb, wb, wa, new Vector2(ua, 0), new Vector2(ub, 0), new Vector2(ub, VW), new Vector2(ua, VW)); Quad(t1, wa, wb, bb, ba, new Vector2(ua, VW), new Vector2(ub, VW), new Vector2(ub, 1), new Vector2(ua, 1)); }
                else { Quad(t1, wa, wb, tb, ta, new Vector2(ua, VW), new Vector2(ub, VW), new Vector2(ub, 0), new Vector2(ua, 0)); Quad(t1, ba, bb, wb, wa, new Vector2(ua, 1), new Vector2(ub, 1), new Vector2(ub, VW), new Vector2(ua, VW)); }
            }
        // front skirt under the top edge, facing down-course (slightly raked), closes the apron ends
        {
            float ow = hw + H + APRON_DEPTH, owW = hw + H, z1 = 0.12f;
            Vector3 tl = new Vector3(-hw, H - 0.02f, 0.01f), tr = new Vector3(hw, H - 0.02f, 0.01f);
            Vector3 wl = new Vector3(-owW, 0f, z1 * H / (H + APRON_DEPTH)), wr = new Vector3(owW, 0f, z1 * H / (H + APRON_DEPTH));
            Vector3 bl = new Vector3(-ow, -APRON_DEPTH, z1), br = new Vector3(ow, -APRON_DEPTH, z1);
            Quad(t1, tr, tl, wl, wr, new Vector2(0.75f, 0), new Vector2(0.25f, 0), new Vector2(0f, VW), new Vector2(1f, VW));
            Quad(t1, wr, wl, bl, br, new Vector2(1f, VW), new Vector2(0f, VW), new Vector2(0f, 1), new Vector2(1f, 1));
        }
        var m = new Mesh { name = "jump_ramp_v14" }; m.SetVertices(vs); m.SetUVs(0, uv); m.subMeshCount = 2; m.SetTriangles(t0, 0); m.SetTriangles(t1, 1);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds(); SaveMesh(m, "jump_ramp_v14");
        var body = MeshObj("RampBody", go.transform, m, surf, apron);
        body.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        // underside of the surface slab (seen from behind the aprons only at grazing angles)
        Quaternion slope = Quaternion.Euler(-Mathf.Asin(sinA) * Mathf.Rad2Deg, 0, 0);
        Vector3 mid = (S(0, s0) + S(0, Ls)) * 0.5f;
        Box("SurfaceSlab", go.transform, mid - (slope * Vector3.up) * 0.05f, new Vector3(rd.width, 0.08f, Lt), steel, slope.eulerAngles);

        // ---------- pass 14: running water film over the surface (SkiView/RampWaterFilm), 6 mm above it
        {
            var fv = new List<Vector3>(); var fu = new List<Vector2>(); var fu1 = new List<Vector2>(); var ft = new List<int>();
            Vector3 nUp = Quaternion.Euler(-Mathf.Asin(sinA) * Mathf.Rad2Deg, 0, 0) * Vector3.up * 0.006f;
            int NX = 6, NY = 16;
            for (int j = 0; j <= NY; j++) for (int i = 0; i <= NX; i++)
            {
                float x = Mathf.Lerp(-hw, hw, i / (float)NX), s = Mathf.Lerp(-0.05f, Ls - 0.02f, j / (float)NY);
                fv.Add(S(x, s) + nUp); fu.Add(new Vector2((x + hw) / 1.2f, s / 1.2f)); fu1.Add(new Vector2(0, 0));
            }
            for (int j = 0; j < NY; j++) for (int i = 0; i < NX; i++) { int k = j * (NX + 1) + i; ft.AddRange(new[] { k, k + NX + 1, k + NX + 2, k, k + NX + 2, k + 1 }); }
            var fm = new Mesh { name = "jump_ramp_film_v14" }; fm.SetVertices(fv); fm.SetUVs(0, fu); fm.SetUVs(1, fu1); fm.SetTriangles(ft, 0);
            fm.RecalculateNormals(); fm.RecalculateTangents(); fm.RecalculateBounds(); SaveMesh(fm, fm.name);
            var filmM = FilmMat("RampWaterFilm", 0f, new Color(0, 0, 0, 0), new Vector4(0.015f, -0.55f, -0.01f, -0.95f), 0.32f);
            var fo = MeshObj("RampWaterFilmSheet", go.transform, fm, filmM); fo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        // ---------- trim, top edge, marks
        Box("TopEdgeCap", go.transform, new Vector3(0, H - 0.012f, -0.02f), new Vector3(rd.width + 0.04f, 0.035f, 0.07f), alu);
        foreach (int sd in new[] { -1, 1 })
            Box("ApronTrim", go.transform, S(sd * (hw + 0.012f), (s0 + Ls) * 0.5f) + Vector3.up * 0.012f, new Vector3(0.035f, 0.035f, Lt), alu, slope.eulerAngles);
        // front-face water line marks (18 mm, 455 mm below the top edge) near each anchor eye
        foreach (int sd in new[] { -1, 1 })
            Box("FrontWaterLine", go.transform, new Vector3(sd * (hw - 0.25f), H - 0.455f, 0.012f + 0.12f * (H - 0.455f > 0 ? (H - (H - 0.455f)) / (H + APRON_DEPTH) : 0f)), new Vector3(0.40f, 0.018f, 0.01f), black);

        // ---------- front "kneeler" deck on floats, hydraulic jacks to the top edge, safety chains
        float deckY = 0.38f, deckZ0 = 0.35f, deckZ1 = 1.25f, deckW = rd.width + 2f * H + 0.3f;
        Box("KneelerDeck", go.transform, new Vector3(0, deckY, (deckZ0 + deckZ1) * 0.5f), new Vector3(deckW, 0.06f, deckZ1 - deckZ0), deck);
        for (int k = -1; k <= 1; k += 2)
            Box("DeckStringer", go.transform, new Vector3(k * deckW * 0.45f, deckY - 0.1f, (deckZ0 + deckZ1) * 0.5f), new Vector3(0.08f, 0.14f, deckZ1 - deckZ0), steel);
        foreach (float z in new[] { deckZ0 + 0.2f, deckZ1 - 0.2f })
        {
            var fl = Prim(PrimitiveType.Cylinder, "Float", go.transform, new Vector3(0, 0.02f, z), new Vector3(0.42f, deckW * 0.5f, 0.42f), floatM);
            fl.transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        foreach (int sd in new[] { -1, 1 })
        {
            Vector3 jb = new Vector3(sd * (hw - 0.55f), deckY + 0.03f, deckZ0 + 0.15f), jt = new Vector3(sd * (hw - 0.55f), H - 0.12f, 0.06f);
            Vector3 jm = Vector3.Lerp(jb, jt, 0.45f);
            var cyl = Prim(PrimitiveType.Cylinder, "JackBody", go.transform, (jb + jm) * 0.5f, new Vector3(0.11f, Vector3.Distance(jb, jm) * 0.5f, 0.11f), steel);
            cyl.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (jt - jb).normalized);
            var rod = Prim(PrimitiveType.Cylinder, "JackRod", go.transform, (jm + jt) * 0.5f, new Vector3(0.05f, Vector3.Distance(jm, jt) * 0.5f, 0.05f), chrome);
            rod.transform.localRotation = cyl.transform.localRotation;
            Box("JackHead", go.transform, jt, new Vector3(0.16f, 0.06f, 0.12f), steel);
            // safety chain from the top-edge corner down to the deck
            Vector3 ca = new Vector3(sd * (hw - 0.1f), H - 0.05f, 0.04f), cb = new Vector3(sd * (hw + 0.4f), deckY + 0.03f, deckZ0 + 0.35f);
            var ch = Box("SafetyChain", go.transform, (ca + cb) * 0.5f, new Vector3(0.025f, 0.025f, Vector3.Distance(ca, cb)), steel);
            ch.transform.localRotation = Quaternion.LookRotation((cb - ca).normalized);
        }
        // ---------- watering system: spray bar under the top edge (water pumped over the top, sheeting down the surface), hose to a floating pump
        var bar = Prim(PrimitiveType.Cylinder, "WaterSprayBar", go.transform, new Vector3(0, H + 0.03f, -0.10f), new Vector3(0.05f, hw, 0.05f), pvc);
        bar.transform.localRotation = Quaternion.Euler(0, 0, 90);
        for (int k = 0; k < 7; k++) Box("SprayNozzle", go.transform, new Vector3(-hw + 0.3f + k * (rd.width - 0.6f) / 6f, H + 0.01f, -0.13f), new Vector3(0.03f, 0.03f, 0.04f), pvc);
        Vector3 hp0 = new Vector3(hw + 0.05f, H + 0.03f, -0.10f), hp1 = new Vector3(hw + H * 0.6f, deckY + 0.05f, deckZ1 - 0.2f), hp2 = new Vector3(hw + H + 1.4f, 0.12f, deckZ1 + 0.9f);
        foreach (var (a, b) in new[] { (hp0, hp1), (hp1, hp2) })
        { var hs = Prim(PrimitiveType.Cylinder, "PumpHose", go.transform, (a + b) * 0.5f, new Vector3(0.045f, Vector3.Distance(a, b) * 0.5f, 0.045f), hose); hs.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized); }
        var pumpF = Prim(PrimitiveType.Cylinder, "PumpFloat", go.transform, hp2 + new Vector3(0.4f, -0.1f, 0.2f), new Vector3(0.9f, 0.12f, 0.9f), floatM);
        Box("Pump", go.transform, hp2 + new Vector3(0.4f, 0.12f, 0.2f), new Vector3(0.45f, 0.3f, 0.6f), Lit("RampPumpBody", new Color(0.12f, 0.25f, 0.45f), 0.4f));
        // ---------- anchoring: lines from the corner eyes to bottom anchors 3-4x the depth away (only the part above water shows)
        foreach (int sd in new[] { -1, 1 })
        {
            Vector3 eyeF = new Vector3(sd * (deckW * 0.5f - 0.05f), deckY, deckZ1), anF = eyeF + new Vector3(sd * 2.2f, -2.6f, 4.5f);
            Vector3 eyeR = new Vector3(sd * (hw + 0.3f), 0.05f, -Ls * cosA * 0.55f), anR = eyeR + new Vector3(sd * 5.0f, -2.6f, -1.5f);
            Box("CornerEye", go.transform, eyeF, new Vector3(0.06f, 0.06f, 0.06f), steel);
            foreach (var (a, b) in new[] { (eyeF, anF), (eyeR, anR) })
            { var ln = Box("AnchorLine", go.transform, (a + b) * 0.5f, new Vector3(0.022f, 0.022f, Vector3.Distance(a, b)), lineM); ln.transform.localRotation = Quaternion.LookRotation((b - a).normalized); }
        }
        Debug.Log($"[SkiView] ramp v14 at {go.transform.position} ({Vector3.Distance(Vector3.ProjectOnPlane(lip, Vector3.up), f * along):0.0} m from the slalom line) yaw {go.transform.eulerAngles.y:0.0} H {H:0.00} ratio {H / Ls:0.0000} surface {Ls:0.00}+{RAMP_UNDER:0.0} m width {rd.width:0.00} angle {Mathf.Asin(sinA) * Mathf.Rad2Deg:0.0} deg");

        // ---------- water: film sheeting down the surface, drips off the top edge, droplets at the foot, foam along the waterline
        var spr = SprayMaterial();
        var film = SkiSpray.MakeSystem("Ramp_WaterFilm", go.transform, spr, 1200, 2.4f, 0f, new Vector2(0.008f, 0.02f), 0.45f, 1.2f, true);
        film.transform.localPosition = S(0, Ls - 0.12f) + slope * Vector3.up * 0.012f;
        film.transform.localRotation = Quaternion.LookRotation((S(0, 0) - S(0, Ls)).normalized, slope * Vector3.up);
        {
            var mm = film.main; mm.simulationSpace = ParticleSystemSimulationSpace.Local; mm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            var sh = film.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(rd.width * 0.96f, 0.002f, 0.05f);
            var fo = film.forceOverLifetime; fo.enabled = true; fo.space = ParticleSystemSimulationSpace.Local; fo.z = new ParticleSystem.MinMaxCurve(9.81f * sinA * 0.9f); fo.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var em = film.emission; em.rateOverTime = 350f;
            var r = film.GetComponent<ParticleSystemRenderer>(); r.velocityScale = 0.06f; r.lengthScale = 2.5f;
        }
        var drips = SkiSpray.MakeSystem("Ramp_EdgeDrips", go.transform, spr, 600, 0.9f, 1f, new Vector2(0.01f, 0.025f), 0.55f, 1f, true);
        drips.transform.localPosition = new Vector3(0, H - 0.03f, 0.05f); drips.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        { var sh = drips.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(rd.width, 0.01f, 0.01f); var mm = drips.main; mm.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f); var em = drips.emission; em.rateOverTime = 260f; }
        var foot = SkiSpray.MakeSystem("Ramp_FootSplash", go.transform, spr, 600, 0.55f, 1f, new Vector2(0.012f, 0.03f), 0.5f, 1.1f, true);
        foot.transform.localPosition = S(0, 0.05f); foot.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
        { var sh = foot.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(rd.width, 0.05f, 0.01f); var mm = foot.main; mm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f); var em = foot.emission; em.rateOverTime = 320f; }
        builtRampFx.AddRange(new[] { film, drips, foot });
        builtRampFoam = SkiSpray.MakeFlatSystem("Ramp_WaterlineFoam", go.transform, SprayMaterial("foam"), 900, 3.0f, new Vector2(0.18f, 0.42f), 0.4f, 1.8f);
        { var mm = builtRampFoam.main; mm.simulationSpace = ParticleSystemSimulationSpace.World; }
        // waterline polyline (world): toe, both aprons where they cross the water, the front skirt
        var wlp = new List<Vector3>();
        float sw = 0f;   // surface meets the water at s = 0
        Vector3 Wd(Vector3 lp) => go.transform.TransformPoint(lp);
        wlp.Add(Wd(new Vector3(-(hw), 0, S(0, sw).z))); wlp.Add(Wd(new Vector3(hw, 0, S(0, sw).z)));
        wlp.Add(Wd(new Vector3(hw + H, 0, 0.05f))); wlp.Add(Wd(new Vector3(-(hw + H), 0, 0.05f))); wlp.Add(wlp[0]);
        builtWaterline = wlp.ToArray();
        // pass 14: contact band where the ramp meets the lake: a strip lying on the water around the waterline polyline, foam and
        // churned glints fading outward (SkiView/RampWaterFilm with the edge fade on), so the ramp sits in the water
        {
            var bv = new List<Vector3>(); var bu = new List<Vector2>(); var bu1 = new List<Vector2>(); var bt = new List<int>();
            Vector3 cen = Vector3.zero; for (int i = 0; i < wlp.Count - 1; i++) cen += wlp[i]; cen /= (wlp.Count - 1);
            float dist = 0f;
            for (int i = 0; i < wlp.Count; i++)
            {
                Vector3 p = go.transform.InverseTransformPoint(wlp[i]); p.y = 0.012f;
                Vector3 outward = (wlp[i] - cen); outward.y = 0; outward = go.transform.InverseTransformDirection(outward.normalized);
                if (i > 0) dist += Vector3.Distance(wlp[i], wlp[i - 1]);
                bv.Add(p - outward * 0.04f); bv.Add(p + outward * 0.55f);
                bu.Add(new Vector2(dist / 1.2f, 0f)); bu.Add(new Vector2(dist / 1.2f, 0.45f)); bu1.Add(new Vector2(0, 0)); bu1.Add(new Vector2(0, 1));
                if (i > 0) { int k = bv.Count - 4; bt.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 }); }
            }
            var bm = new Mesh { name = "jump_ramp_waterline_v14" }; bm.SetVertices(bv); bm.SetUVs(0, bu); bm.SetUVs(1, bu1); bm.SetTriangles(DoubleSided(bt), 0);
            var nn = new Vector3[bv.Count]; var tg = new Vector4[bv.Count]; for (int i = 0; i < nn.Length; i++) { nn[i] = Vector3.up; tg[i] = new Vector4(1, 0, 0, 1); }
            bm.normals = nn; bm.tangents = tg; bm.RecalculateBounds(); SaveMesh(bm, bm.name);
            var bandM = FilmMat("RampWaterlineBand", 1f, new Color(0.85f, 0.9f, 0.92f, 0.85f), new Vector4(0.06f, 0.02f, -0.05f, 0.03f), 0.45f);
            var bo = MeshObj("RampWaterlineBand", go.transform, bm, bandM); bo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- jump course buoys (AWSA p.94), shown in jump mode only. s = metres down-course of the top edge, y toward the ramp
        var jc = new GameObject("JumpCourse"); jc.transform.SetParent(root, false); builtJumpCourse = jc.transform;
        var green = Lit("BuoyPreGate", new Color(0.2f, 0.85f, 0.3f), 0.6f); var red = Lit("BuoyGate", new Color(0.95f, 0.12f, 0.1f), 0.6f); var yellow = Lit("BuoyGuide", new Color(1f, 0.85f, 0.1f), 0.6f);
        Vector3 xaxis0 = f * along + side * (RAMP_FROM_SLALOM - RAMP_FROM_XAXIS);
        void JB(string n, float s, float y, Material mat) { Prim(PrimitiveType.Sphere, n, jc.transform, xaxis0 + f * s + side * y + Vector3.up * (0.115f - 0.115f), Vector3.one * 0.23f, mat); }
        JB("15PG", -210, 0, green); JB("19PG", -210, -4, green);
        JB("Dist210", -210, 15, green); JB("Dist180", -180, 15, red); JB("Dist150", -150, 15, yellow);
        JB("15ST", -72, 0, red); JB("19ST", -72, -4, red); JB("15MT", 10, 0, yellow); JB("19MT", 10, -4, yellow);
        JB("15ET", 51, 0, red); JB("19ET", 51, -4, red); JB("15EC", 100, 0, yellow); JB("19EC", 100, -4, yellow);
        // pass 18: phone draw calls - the ramp is one rigid body (it heaves as a whole), the jump buoys are static
        TryHotSwapRampV1(go.transform);
        var (mrB, mrA) = MergeRig(go.transform, new[] { go.transform }, "ramp_" + meshTag);
        var (mcB, mcA) = MergeRig(jc.transform, new[] { jc.transform }, "jumpcourse_" + meshTag);
        TagNoReflect(go.transform, 1.2f);
        Debug.Log($"[SkiView] ramp merged: {mrB} -> {mrA} draw items; jump course {mcB} -> {mcA}");
        jc.SetActive(false);
    }

    // ---------- pass 13: jump gear (two jump skis + helmet), inactive until jump mode ----------
    const float JSKI_L = 2.03f, JSKI_ANKLE = 0.95f;   // 80 in jump skis (max width <= 30% of length: ours ~21 cm); ankle ~0.95 m from the tail


    static Transform FindChildDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var f = FindChildDeep(t.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    static void BuildJumpGearAuthored(Transform skierRoot, SkiViewGame game, GameObject prefab)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "JumpSki_V1_Pair";
        Transform l = FindChildDeep(root.transform, "JumpSki_V1_L");
        Transform r = FindChildDeep(root.transform, "JumpSki_V1_R");
        if (l == null || r == null)
        {
            Debug.LogError("[SkiView] JumpSki_V1 prefab missing L/R roots");
            Object.DestroyImmediate(root);
            return;
        }
        // detach L/R onto skier; drop empty pair wrapper
        l.SetParent(skierRoot, false); r.SetParent(skierRoot, false);
        Object.DestroyImmediate(root);
        l.name = "JumpSki_L"; r.name = "JumpSki_R";
        float ankleZ = 0.85f; // fallback ~ FrontBoot along +Z from ski origin/tail
        foreach (var (ski, side) in new[] { (l, "L"), (r, "R") })
        {
            var boot = FindChildDeep(ski, "FrontBoot_" + side) ?? FindChildDeep(ski, "FrontBoot");
            if (boot != null)
            {
                boot.name = "Boot";
                ankleZ = boot.localPosition.z;
            }
            else
            {
                var b = new GameObject("Boot").transform; b.SetParent(ski, false);
                b.localPosition = new Vector3(0, 0.04f, ankleZ);
            }
            var sprayHost = FindChildDeep(ski, "SprayEmitter_" + side) ?? ski;
            var sp = SkiSpray.Create(sprayHost, SprayMaterial(), SprayMaterial("sheet"), SprayMaterial("mist"), SprayMaterial("foam"));
            sp.tailZ = 0.05f; sp.sheetZ = 0.8f; sp.bootsZ = 1.0f;
            ski.gameObject.SetActive(false);
        }
        game.jumpSkiL = l; game.jumpSkiR = r;
        game.jumpSprayL = l.GetComponentInChildren<SkiSpray>(true); game.jumpSprayR = r.GetComponentInChildren<SkiSpray>(true);
        game.jumpSkiAnkleZ = Mathf.Abs(ankleZ) > 0.05f ? Mathf.Abs(ankleZ) : 0.85f;
        game.jumpStanceHalf = 0.15f; // stance 0.30 m
        // helmet (same as procedural path)
        var anim = game.rig != null ? game.rig.animator : null;
        var head = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head != null)
        {
            var shellM = Lit("JumpHelmet", new Color(0.13f, 0.14f, 0.16f), 0.65f);
            var h = GameObject.CreatePrimitive(PrimitiveType.Sphere); h.name = "Helmet"; Object.DestroyImmediate(h.GetComponent<Collider>());
            h.GetComponent<Renderer>().sharedMaterial = shellM;
            Vector3 up = skierRoot.up, fwd = skierRoot.forward;
            h.transform.SetPositionAndRotation(head.position + up * 0.125f - fwd * 0.03f, skierRoot.rotation);
            h.transform.SetParent(head, true);
            Vector3 want = new Vector3(0.235f, 0.215f, 0.27f), ls = head.lossyScale;
            h.transform.localScale = new Vector3(want.x / ls.x, want.y / ls.y, want.z / ls.z);
            h.SetActive(false);
            game.helmet = h;
        }
        Debug.Log($"[SkiView] jump gear AUTHORED JumpSki_V1: ankleZ={game.jumpSkiAnkleZ:0.00} stanceHalf={game.jumpStanceHalf:0.00} helmet={(head != null)}");
    }

    static void TryHotSwapRampV1(Transform rampRoot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SkiView/Resources/Ramp/Ramp_V1.prefab");
        if (prefab == null) return;
        foreach (var mr in rampRoot.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.GetComponent<ParticleSystem>() != null) continue;
            if (mr.GetComponentInParent<ParticleSystem>() != null) continue;
            // keep FX; hide procedural plate / apron / posts
            string n = mr.gameObject.name;
            if (n.StartsWith("Ramp_Water") || n.StartsWith("Ramp_Edge") || n.StartsWith("Ramp_Foot") || n.Contains("Foam") || n.Contains("Film")) continue;
            mr.enabled = false;
        }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Ramp_V1";
        inst.transform.SetParent(rampRoot, false);
        var lip = FindChildDeep(inst.transform, "LipCentre");
        if (lip != null)
        {
            Vector3 delta = lip.position - rampRoot.position;
            inst.transform.position -= delta;
        }
        foreach (var tr in inst.GetComponentsInChildren<Transform>(true))
        {
            if (!tr.name.Contains("Collider")) continue;
            var r = tr.GetComponent<Renderer>(); if (r != null) r.enabled = false;
        }
        Debug.Log("[SkiView] Ramp_V1 hot-swap attached under JumpRamp (procedural visuals hidden)");
    }

    static Mesh JumpSkiMesh()
    {
        // the slalom ski outline stretched to 80 in, a little wider and flatter in the tail, with a long, high tip rocker
        var src = SkiViewRiderKit.SkiMesh();
        var vs = new List<Vector3>(src.vertices);
        float sz = JSKI_L / SkiViewRiderKit.L;
        for (int i = 0; i < vs.Count; i++)
        {
            var q = vs[i]; float t = Mathf.Clamp01(q.z / SkiViewRiderKit.L);
            float rk = 0.11f * Mathf.Pow(Mathf.Max(0f, (t - 0.72f) / 0.28f), 2.0f);   // extra tip rise
            vs[i] = new Vector3(q.x * Mathf.Lerp(1.22f, 1.10f, t), q.y + rk, q.z * sz);
        }
        var m = new Mesh { name = "jump_ski_80in" };
        m.SetVertices(vs); m.SetUVs(0, new List<Vector2>(src.uv)); m.subMeshCount = src.subMeshCount;
        for (int k = 0; k < src.subMeshCount; k++) m.SetTriangles(src.GetTriangles(k), k);
        m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, "jump_ski_80in");
    }

    static void BuildJumpGear(Transform skierRoot, SkiViewGame game, Transform slalomSki)
    {
        // b10.16 soft-optional: authored JumpSki_V1 pair (PBR) when prefab present
        var authored = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SkiView/Resources/Skis/JumpSki_V1.prefab");
        if (authored != null) { BuildJumpGearAuthored(skierRoot, game, authored); return; }
        var top = Lit("JumpSkiTop", new Color(0.62f, 0.10f, 0.08f), 0.55f);       // generic red topsheet, no graphics
        var bas = Lit("JumpSkiBase", new Color(0.12f, 0.12f, 0.13f), 0.7f);
        var finM = Lit("JumpSkiFin", new Color(0.55f, 0.57f, 0.60f), 0.7f, 0.9f);
        var mesh = JumpSkiMesh();
        var slBoot = slalomSki != null ? slalomSki.Find("FrontBoot") : null;
        var group = new GameObject("JumpGear").transform; group.SetParent(skierRoot, false);
        Transform[] skis = new Transform[2];
        for (int k = 0; k < 2; k++)
        {
            float sg = k == 0 ? -1f : 1f;
            var ski = new GameObject(k == 0 ? "JumpSki_L" : "JumpSki_R").transform; ski.SetParent(skierRoot, false);
            ski.localPosition = new Vector3(sg * 0.16f, 0f, -JSKI_ANKLE);
            var body = new GameObject("SkiBody"); body.transform.SetParent(ski, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = body.AddComponent<MeshRenderer>(); mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { top, bas } : new[] { top }; mr.shadowCastingMode = ShadowCastingMode.On;
            Box("Fin", ski, new Vector3(0, -0.025f, 0.10f), new Vector3(0.004f, 0.05f, 0.13f), finM);
            float t = JSKI_ANKLE / JSKI_L;
            float topY = SkiViewRiderKit.TopY(t * SkiViewRiderKit.L / SkiViewRiderKit.L);
            if (slBoot != null)
            {
                var b = Object.Instantiate(slBoot.gameObject, ski).transform; b.name = "Boot";
                b.localPosition = new Vector3(0, topY, JSKI_ANKLE - 0.065f); b.localRotation = Quaternion.identity; b.localScale = Vector3.one;
            }
            else { var b = new GameObject("Boot").transform; b.SetParent(ski, false); b.localPosition = new Vector3(0, topY, JSKI_ANKLE - 0.065f); }
            var sp = SkiSpray.Create(ski, SprayMaterial(), SprayMaterial("sheet"), SprayMaterial("mist"), SprayMaterial("foam")); sp.tailZ = 0.05f; sp.sheetZ = 0.8f; sp.bootsZ = 1.0f;
            ski.gameObject.SetActive(false);
            skis[k] = ski;
        }
        Object.DestroyImmediate(group.gameObject);
        game.jumpSkiL = skis[0]; game.jumpSkiR = skis[1];
        game.jumpSprayL = skis[0].GetComponent<SkiSpray>(); game.jumpSprayR = skis[1].GetComponent<SkiSpray>();
        game.jumpSkiAnkleZ = JSKI_ANKLE; game.jumpStanceHalf = 0.16f;
        // helmet (mandatory for jumping): plain dark shell over the top and back of the head, on the head bone
        var anim = game.rig != null ? game.rig.animator : null;
        var head = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head != null)
        {
            var shellM = Lit("JumpHelmet", new Color(0.13f, 0.14f, 0.16f), 0.65f);
            var h = GameObject.CreatePrimitive(PrimitiveType.Sphere); h.name = "Helmet"; Object.DestroyImmediate(h.GetComponent<Collider>());
            h.GetComponent<Renderer>().sharedMaterial = shellM;
            Vector3 up = skierRoot.up, fwd = skierRoot.forward;
            h.transform.SetPositionAndRotation(head.position + up * 0.125f - fwd * 0.03f, skierRoot.rotation);
            h.transform.SetParent(head, true);
            Vector3 want = new Vector3(0.235f, 0.215f, 0.27f), ls = head.lossyScale;
            h.transform.localScale = new Vector3(want.x / ls.x, want.y / ls.y, want.z / ls.z);
            h.SetActive(false);
            game.helmet = h;
        }
        Debug.Log($"[SkiView] jump gear: 2 x {JSKI_L:0.00} m jump skis (ankle {JSKI_ANKLE:0.00} m from the tail, stance 0.32 m), helmet={(head != null)}");
    }
}
