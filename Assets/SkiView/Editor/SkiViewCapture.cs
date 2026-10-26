using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SkiView;

/// <summary>Headless screenshot capture: runs the sim in edit mode and renders the main camera to a RenderTexture.
/// Needs a GPU context (run under Xvfb without -nographics); software GL is fine.</summary>
public static partial class SkiViewCapture
{
    static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens"));
    const int W = 1600, H = 900;

    public static void BuildAndCapture() { SkiViewBuilder.BuildAll(); Run(); }

    [Serializable] class VenueLite { public float[] islandCentres; public CourseLite course; public RampLite ramp; }
    [Serializable] class CourseLite { public float[] fS; }
    [Serializable] class RampLite { public float[] lip, fwd; }

    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var v = JsonUtility.FromJson<VenueLite>(File.ReadAllText("Assets/SkiView/Generated/venue.json"));
        Vector3 fS = new Vector3(v.course.fS[0], 0, v.course.fS[1]);            // southbound course direction
        Vector3 east = new Vector3(-fS.z, 0, fS.x);                                // venue R = (-fS.z, fS.x)
        Vector3 islA = new Vector3(v.islandCentres[0], 0, v.islandCentres[1]), islB = new Vector3(v.islandCentres[2], 0, v.islandCentres[3]);
        Vector3 islS = islA.z < islB.z ? islA : islB, islN = islA.z < islB.z ? islB : islA;
        float Along(Vector3 p) => Vector3.Dot(p, fS);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true;
        game.Init();

        // 1. aerial overview, north up, aligned with the lake axis
        game.SimulateFor(4f);
        RenderSettings.fog = false;
        float yaw = Mathf.Atan2(-fS.x, -fS.z) * Mathf.Rad2Deg;
        Shot(cam, game, "01_overview_aerial", () => { cam.transform.position = new Vector3(-46, 600, -60); cam.transform.rotation = Quaternion.Euler(82, yaw, 0); cam.fieldOfView = 70; cam.farClipPlane = 3000; });
        cam.fieldOfView = 52; RenderSettings.fog = true;

        // 2. chase cam during pass 1 (approaching ball 3)
        int guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2 && game.sim.z < SkierSim.BuoyZ(2) + 12f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        Shot(cam, game, "02_chase_pass1_ball3", null);

        // 3. low side angle on the carve at ball 4 (rigged skier)
        guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3 && game.sim.z < SkierSim.BuoyZ(3) + 4f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        Shot(cam, game, "03_carve_ball4_low", () =>
        {
            Vector3 s = game.SkierPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.transform.position = s + f * 9f + r * 14f + Vector3.up * 1.6f;
            cam.transform.LookAt(s + Vector3.up * 1.0f - f * 2f);
        });
        // pass 7: a real close-up (front 3/4, telephoto, like the TWBC replay angle) so face, hair, vest and leg spray are readable
        // pass 9: three-quarter FRONT view across the lean plane. The camera azimuth is searched (front half of the chest only) to
        // maximise the on-screen lean of the ski-to-head line, so the 30 deg+ lean and the edge read like the reference replay angle.
        LogBodyTilt(game, "before 04");
        Shot(cam, game, "04_rider_closeup", () =>
        {
            Vector3 s = game.SkierPos; Transform sk = game.skier;
            var anim = game.rig != null ? game.rig.GetComponentInChildren<Animator>() : null;
            Transform head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Transform la = anim ? anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null, ra = anim ? anim.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            Vector3 hp = head != null ? head.position : s + sk.up * 1.5f;
            Vector3 chest = (la != null && ra != null) ? Vector3.Cross(ra.position - la.position, Vector3.up) : sk.forward; chest.y = 0; chest = chest.sqrMagnitude > 1e-4f ? chest.normalized : game.BoatFwd;   // chest normal from the shoulder line
            cam.fieldOfView = 24f;
            float bestScore = -1f, bestAz = 0f, bestLean = 0f, bestFront = 0f;
            for (float az = 0f; az < 360f; az += 5f)
            {
                Vector3 dir = Quaternion.Euler(0, az, 0) * Vector3.forward;          // skier -> camera
                float front = Vector3.Dot(dir, chest);
                if (front < 0.45f) continue;                                         // in front of the chest: 3/4 front at most ~63 deg off
                cam.transform.position = s + dir * 7.5f + Vector3.up * 1.2f;
                cam.transform.LookAt(Vector3.Lerp(s, hp, 0.5f));
                Vector3 a0 = cam.WorldToViewportPoint(s), a1 = cam.WorldToViewportPoint(hp);
                Vector2 d2 = new Vector2((a1.x - a0.x) * cam.aspect, a1.y - a0.y);
                float lean = Mathf.Atan2(Mathf.Abs(d2.x), Mathf.Max(1e-4f, d2.y)) * Mathf.Rad2Deg;
                float score = lean + 6f * (1f - Mathf.Abs(front - 0.7f));               // prefer a true 3/4 (front ~45 deg) when lean ties
                if (score > bestScore) { bestScore = score; bestAz = az; bestLean = lean; bestFront = front; }
            }
            Vector3 bd = Quaternion.Euler(0, bestAz, 0) * Vector3.forward;
            cam.transform.position = s + bd * 7.5f + Vector3.up * 1.2f;
            cam.transform.LookAt(Vector3.Lerp(s, hp, 0.5f));
            Debug.Log($"[SkiView] closeup 04: on-screen lean {bestLean:0.0} deg, camera {Mathf.Acos(Mathf.Clamp(bestFront, -1f, 1f)) * Mathf.Rad2Deg:0} deg off the chest normal");
        });
        LogBodyTilt(game, "after 04");
        Shot(cam, game, "04b_rider_face", () =>
        {
            Transform head = game.rig != null ? game.rig.GetComponentInChildren<Animator>()?.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 hp = head != null ? head.position + Vector3.up * 0.06f : game.SkierPos + Vector3.up * 1.2f, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 22f;
            cam.transform.position = hp + f * 1.6f - r * 0.9f + Vector3.up * 0.15f;
            cam.transform.LookAt(hp);
        });
        cam.fieldOfView = 52f;

        // 15/16. pass 7 cameras: broadcast boat camera (default) and the legacy skier chase, at the same moment
        game.camMode = SkiViewGame.CamMode.Broadcast;
        BoatGeomDiag(game);
        Shot(cam, game, "15b_broadcast_at_buoy", null);
        game.camMode = SkiViewGame.CamMode.SkierChase;
        Shot(cam, game, "16_skier_chase_legacy", null);
        // pass 8: the reference boat-cam frame (TWBC f_011) has the skier crossing the wake behind the boat; match that moment
        game.camMode = SkiViewGame.CamMode.Broadcast;
        int g15 = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && Mathf.Abs(game.sim.Lateral) < 1.2f) && g15++ < 3000) game.SimulateFor(1f / 30f);
        Shot(cam, game, "15_broadcast_chase", null);
        if (Environment.GetEnvironmentVariable("SV_WATER_DEBUG") == "1") { Shader.SetGlobalFloat("_SVWaterDebug", 1f); Shot(cam, game, "dbg_15_wake_masks", null); Shader.SetGlobalFloat("_SVWaterDebug", 0f); }
        cam.fieldOfView = 52f;

        // 5. boat + wake + livery from behind/above
        game.SimulateFor(2.5f);
        Shot(cam, game, "05_boat_wake_livery", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.transform.position = b + f * 6f + r * 7.5f + Vector3.up * 2.2f;
            cam.transform.LookAt(b - f * 1.5f + Vector3.up * 0.4f);
        });

        // 14. chase: boat and skier together (rope on the TowPoint, wake, prop wash, skier spray)
        {
            float fov0 = cam.fieldOfView;
            Shot(cam, game, "14_chase_boat_and_skier", () =>
            {
                Vector3 sk = game.SkierPos, b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
                cam.fieldOfView = 46f;
                cam.transform.position = sk - f * 7.5f + r * 2.8f + Vector3.up * 3.4f;
                cam.transform.LookAt(Vector3.Lerp(sk, b, 0.45f) + Vector3.up * 0.7f);
            });
            cam.fieldOfView = fov0;
        }

        // 6. jump ramp (south end, house side), from the course
        var ramp = GameObject.Find("JumpRamp");
        if (ramp != null)
            Shot(cam, game, "06_jump_ramp_south_east", () =>
            {
                Vector3 lip = ramp.transform.position, f = ramp.transform.forward;
                cam.transform.position = lip - f * 13f - east * 9f + Vector3.up * 3.6f;
                cam.transform.LookAt(lip - f * 3.5f + Vector3.up * 0.5f);
            });

        // 7. south island turnaround (counterclockwise: right turn, down the west gap, back up the east gap)
        guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Turn && Along(game.BoatPos) > Along(islS) + 12f && Vector3.Dot(game.BoatPos - islS, east) < 0) && guard++ < 20000) game.SimulateFor(1f / 30f);
        Shot(cam, game, "07_south_island_turn", () =>
        {
            cam.transform.position = islS + east * 45f - fS * 85f + Vector3.up * 150f;
            cam.transform.LookAt(islS + east * 25f - fS * 5f);
        });

        // 8. pass 2 northbound (after the turn)
        guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.passDir < 0 && game.sim.nextBuoy >= 1) && guard++ < 20000) game.SimulateFor(1f / 30f);
        Shot(cam, game, "08_chase_pass2_northbound", null);

        // 9. east bank houses from the water
        Shot(cam, game, "09_east_bank_houses", () =>
        {
            cam.transform.position = -fS * 60f - east * 25f + Vector3.up * 6f;
            cam.transform.rotation = Quaternion.LookRotation(east * 0.97f - fS * 0.25f) * Quaternion.Euler(4, 0, 0);
        });

        // 10. north island turnaround (clockwise: left turn, up the west gap, back down the east gap)
        guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Turn && Along(game.BoatPos) < Along(islN) - 12f && Vector3.Dot(game.BoatPos - islN, east) < 0) && guard++ < 20000) game.SimulateFor(1f / 30f);
        Shot(cam, game, "10_north_island_turn", () =>
        {
            cam.transform.position = islN + east * 45f + fS * 85f + Vector3.up * 150f;
            cam.transform.LookAt(islN + east * 25f + fS * 5f);
        });

        // 11. hero lot (first house north end)
        var hero = GameObject.Find("Lot_8017_HERO");
        if (hero != null)
            Shot(cam, game, "11_hero_lot_8017", () =>
            {
                // pass 7: lot FBX pivot is at the street edge, the shore 44.4 m toward the lake (local +Z) and 6.2 m lower
                Vector3 p = hero.transform.position, fwd = hero.transform.forward, rt = hero.transform.right;
                cam.fieldOfView = 50f;
                cam.transform.position = p + fwd * 95f - rt * 30f + Vector3.up * -2.5f;
                cam.transform.LookAt(p + fwd * 24f + Vector3.up * -2.5f);
            });
        LineDiag("after 11");
        if (hero != null)
            Shot(cam, game, "11b_hero_lot_8017_terraces", () =>
            {
                Vector3 p = hero.transform.position, fwd = hero.transform.forward, rt = hero.transform.right;
                cam.fieldOfView = 55f;
                cam.transform.position = p + fwd * 62f + rt * 22f + Vector3.up * -3.5f;
                cam.transform.LookAt(p + fwd * 30f + Vector3.up * -3.8f);
            });
        if (hero != null)
            Shot(cam, game, "11c_hero_lot_8017_street", () =>
            {
                Vector3 p = hero.transform.position, fwd = hero.transform.forward, rt = hero.transform.right;
                cam.fieldOfView = 55f;
                cam.transform.position = p - fwd * 28f + rt * 22f + Vector3.up * 5f;
                cam.transform.LookAt(p + fwd * 10f + Vector3.up * 2f);
            });

        Debug.Log("[SkiView] capture done -> " + OutDir);
    }

    /// Pass 8 diagnostic: which skybox _Rotation offset makes the cubemap background match the panorama it replaced?
    /// Renders the panoramic sky once, then the cubemap at many offsets, and logs the best image match.
    public static void SkyAlign()
    {
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var cam = Camera.main; var cube = RenderSettings.skybox; float rotC = cube.GetFloat("_Rotation");
        var pano = new Material(Shader.Find("Skybox/Panoramic"));
        pano.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SkiView/Environment/Sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr"));
        pano.SetFloat("_Mapping", 1); pano.SetFloat("_ImageType", 0); pano.SetFloat("_MirrorOnBack", 0); pano.SetFloat("_Layout", 0);
        pano.SetFloat("_Rotation", rotC); pano.SetFloat("_Exposure", 0.4f); pano.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        var acd = cam.GetComponent<UniversalAdditionalCameraData>(); if (acd) acd.renderPostProcessing = false;
        RenderSettings.fog = false;
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) r.enabled = false;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)) t.enabled = false;
        cam.fieldOfView = 100f; cam.transform.position = new Vector3(0, 50, 0);
        var rt = new RenderTexture(128, 128, 24); rt.Create(); var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        float[] Grab(float yaw, float pitch)
        {
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); tex.Apply(); RenderTexture.active = null;
            var px = tex.GetPixels(); var o = new float[px.Length]; for (int k = 0; k < px.Length; k++) o[k] = px[k].r + px[k].g + px[k].b; return o;
        }
        var views = new[] { (0f, -20f), (90f, -20f), (180f, -20f), (270f, -20f) };
        RenderSettings.skybox = pano;
        var refs = new System.Collections.Generic.List<float[]>(); foreach (var v in views) refs.Add(Grab(v.Item1, v.Item2));
        RenderSettings.skybox = cube; cube.SetFloat("_Exposure", 0.4f);
        float bestOff = 0, bestErr = float.MaxValue;
        for (float off = -180f; off < 180f; off += 2.5f)
        {
            cube.SetFloat("_Rotation", Mathf.Repeat(rotC + off, 360f));
            float err = 0;
            for (int vi = 0; vi < views.Length; vi++) { var g = Grab(views[vi].Item1, views[vi].Item2); for (int k = 0; k < g.Length; k++) err += Mathf.Abs(g[k] - refs[vi][k]); }
            if (err < bestErr) { bestErr = err; bestOff = off; }
            if (Mathf.Abs(off % 30f) < 0.01f) Debug.Log($"[SkiView] skyalign off={off} err={err:0}");
        }
        Debug.Log($"[SkiView] skyalign BEST offset={bestOff} err={bestErr:0}");
    }

    static void LineDiag(string tag)
    {
        if (Environment.GetEnvironmentVariable("SV_LINE_DIAG") != "1") return;
        foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            var arr = new ParticleSystem.Particle[ps.particleCount]; int n = ps.GetParticles(arr);
            float vmax = 0, ymax = float.MinValue, ymin = float.MaxValue, smax = 0;
            for (int i = 0; i < n; i++) { vmax = Mathf.Max(vmax, arr[i].velocity.magnitude); ymax = Mathf.Max(ymax, arr[i].position.y); ymin = Mathf.Min(ymin, arr[i].position.y); smax = Mathf.Max(smax, arr[i].GetCurrentSize(ps)); }
            var r = ps.GetComponent<ParticleSystemRenderer>();
            Debug.Log($"[SkiView] linediag {tag} PS {ps.name} n={n} vmax={vmax:0.0} y=[{ymin:0.0},{ymax:0.0}] size={smax:0.00} mode={r.renderMode} vs={r.velocityScale} ls={r.lengthScale}");
        }
        foreach (var lr in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
        {
            var pts = new Vector3[lr.positionCount]; lr.GetPositions(pts);
            float ymax = float.MinValue, ymin = float.MaxValue; bool nan = false;
            foreach (var q in pts) { ymax = Mathf.Max(ymax, q.y); ymin = Mathf.Min(ymin, q.y); nan |= float.IsNaN(q.x + q.y + q.z); }
            Debug.Log($"[SkiView] linediag {tag} LR {lr.name} n={pts.Length} y=[{ymin:0.00},{ymax:0.00}] nan={nan} enabled={lr.enabled} first={(pts.Length > 0 ? pts[0] : Vector3.zero)}");
        }
        foreach (var mr in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var b = mr.bounds;
            if (mr.enabled && b.size.y > 25f && b.size.x < 3f && b.size.z < 3f) Debug.Log($"[SkiView] linediag {tag} tall thin renderer {mr.name} {b}");
        }
    }

    /// Pylon top and stern / gunwale heights from the boat mesh around the TowPoint (tunes the broadcast camera mount).
    /// Pass 10: which renderers does the mirror show at a given pixel? Casts the eye ray to the water plane (y = 0), reflects it
    /// and lists the renderer bounds it crosses (env_b streak hunt).
    static void ReflDiag(Camera cam, string tag, float px, float py)
    {
        var ray = cam.ViewportPointToRay(new Vector3(px, py, 0));
        if (ray.direction.y >= 0) { Debug.Log($"[SkiView] refldiag {tag} ({px:0.000},{py:0.000}) above the horizon"); return; }
        float t = -ray.origin.y / ray.direction.y; Vector3 hit = ray.origin + ray.direction * t;
        var rr = new Ray(hit + Vector3.up * 0.01f, Vector3.Reflect(ray.direction, Vector3.up));
        var hits = new System.Collections.Generic.List<(float, string)>();
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.enabled && r.gameObject.activeInHierarchy && r.bounds.IntersectRay(rr, out float dd))
                hits.Add((dd, $"{r.name} [{r.GetType().Name}] size={r.bounds.size} centre={r.bounds.center}"));
        if (px > 0 && tag == "env_b" && py > 0.3f)   // once: renderers whose mirror image lands in this screen column
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds; if (b.size.x > 40f || b.size.z > 40f) continue;
                Vector3 lo = cam.WorldToViewportPoint(new Vector3(b.center.x, -b.max.y, b.center.z)), hi = cam.WorldToViewportPoint(new Vector3(b.center.x, -b.min.y, b.center.z));
                if (lo.z > 0 && Mathf.Abs(lo.x - px) < 0.04f && Mathf.Max(lo.y, hi.y) > 0.1f && Mathf.Min(lo.y, hi.y) < 0.4f)
                    Debug.Log($"[SkiView] refldiag env_b column: {r.name} [{r.GetType().Name}] layer={r.gameObject.layer} size={b.size} centre={b.center} mirrorVP x={lo.x:0.000} y=[{lo.y:0.000},{hi.y:0.000}] dist={lo.z:0.0}");
            }
        hits.Sort((x, y) => x.Item1.CompareTo(y.Item1));
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Mathf.Min(8, hits.Count); i++) sb.Append($"\n   {hits[i].Item1:0.0} m  {hits[i].Item2}");
        Debug.Log($"[SkiView] refldiag {tag} ({px:0.000},{py:0.000}) water hit {hit} at {t:0.0} m, reflected dir {rr.direction}:{sb}");
    }

    static void BoatGeomDiag(SkiViewGame game)
    {
        if (game.boat == null || game.boatTow == null) return;
        var mf = game.boat.GetComponentInChildren<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return;
        try
        {
            var vs = mf.sharedMesh.vertices; var tr = mf.transform; var tow = game.boat.InverseTransformPoint(game.boatTow.position);
            float pyl = float.MinValue, stern = float.MinValue, sternZ = float.MaxValue, fwdTop = float.MinValue, wsZ = float.MaxValue;
            foreach (var v0 in vs)
            {
                var v = game.boat.InverseTransformPoint(tr.TransformPoint(v0));
                if (Mathf.Abs(v.x) < 0.25f && Mathf.Abs(v.z - tow.z) < 0.25f) pyl = Mathf.Max(pyl, v.y);
                if (v.z < tow.z - 1.5f) { stern = Mathf.Max(stern, v.y); sternZ = Mathf.Min(sternZ, v.z); }
                if (v.z > tow.z + 0.3f && v.z < tow.z + 2.5f && v.y > tow.y) { fwdTop = Mathf.Max(fwdTop, v.y); wsZ = Mathf.Min(wsZ, v.z); }
            }
            var grid = new float[12, 8];   // top view, boat local: z from tow-4 to tow+3.2 (0.6 m rows), x -1.6..1.6 (0.4 m cols); max y
            for (int r = 0; r < 12; r++) for (int c = 0; c < 8; c++) grid[r, c] = -9f;
            foreach (var v0 in vs)
            {
                var v = game.boat.InverseTransformPoint(tr.TransformPoint(v0));
                int r = Mathf.FloorToInt((v.z - (tow.z - 4f)) / 0.6f), c = Mathf.FloorToInt((v.x + 1.6f) / 0.4f);
                if (r >= 0 && r < 12 && c >= 0 && c < 8) grid[r, c] = Mathf.Max(grid[r, c], v.y);
            }
            var gsb = new System.Text.StringBuilder();
            for (int r = 11; r >= 0; r--) { gsb.Append($"\n z={tow.z - 4f + r * 0.6f + 0.3f:+0.0;-0.0}: "); for (int c = 0; c < 8; c++) gsb.Append(grid[r, c] < -8f ? "  .  " : $"{grid[r, c]:0.00} "); }
            Debug.Log("[SkiView] boatgeom height grid (max y, rows fwd->aft, cols x -1.4..+1.4)" + gsb);
            Debug.Log($"[SkiView] boatgeom tow=({tow.x:0.00},{tow.y:0.00},{tow.z:0.00}) pylonTop={pyl:0.00} sternTop={stern:0.00} sternZ={sternZ:0.00} aboveTowFwd(top {fwdTop:0.00}, nearest z {wsZ:0.00})");
        }
        catch (Exception e) { Debug.Log("[SkiView] boatgeom n/a " + e.Message); }
    }

    static void LogBodyTilt(SkiViewGame game, string tag)
    {
        var anim = game.rig != null ? game.rig.GetComponentInChildren<Animator>() : null;
        Transform head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head == null || game.skier == null) return;
        Vector3 v = head.position - game.SkierPos;
        Debug.Log($"[SkiView] body tilt {tag}: head-over-ski {Vector3.Angle(v, Vector3.up):0.0} deg, root roll {Vector3.Angle(game.skier.up, Vector3.up):0.0} deg, sim lean {game.LeanDeg:0.0}");
    }

    static void Shot(Camera cam, SkiViewGame game, string name, Action place)
    {
        game.cameraFollow = place == null;
        if (place != null) cam.nearClipPlane = 0.2f;   // the broadcast camera uses a 1.3 m near clip
        cam.aspect = (float)W / H;   // the shot's aspect, so the broadcast camera's aspect-aware zoom matches what is rendered
        game.ApplyVisuals(true);
        place?.Invoke();
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        rt.Create();
        bool ok = false;
        var pr = cam.GetComponent<PlanarReflection>();
        if (pr != null && pr.enabled) { cam.targetTexture = rt; pr.RenderNow(); cam.targetTexture = null; }   // same aspect as the shot
        try
        {
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) { RenderPipeline.SubmitRenderRequest(cam, req); ok = true; }
        }
        catch (Exception e) { Debug.LogWarning("[SkiView] render request failed: " + e.Message); }
        if (!ok) { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        if (Environment.GetEnvironmentVariable("SV_SHOTPERF") == "1" && new[] { "_course_chase", "_close_chase", "_mood", "_course_drone", "_bank_plus", "_bank_minus" }.Any(name.EndsWith)) ShotPerf(cam, name);   // lake P1: per-camera desktop + phone frame time at this exact framing
        game.cameraFollow = true;
        cam.ResetAspect();
        if (name.StartsWith("15")) { var vp = cam.WorldToViewportPoint(game.SkierPos + Vector3.up * 0.8f); Debug.Log($"[SkiView] camdiag {name} fov={cam.fieldOfView:0.0} aspect={cam.aspect:0.00} cam={cam.transform.position} skierVP=({vp.x:0.00},{vp.y:0.00},{vp.z:0.0}) skier={game.SkierPos} boat={game.BoatPos}"); }
        Debug.Log($"[SkiView] shot {name}  mode={game.mode} pass={game.passes} ball={game.sim.nextBuoy} buoys={game.sim.passBuoys} msg={game.sim.message} lean={game.LeanDeg:F0} hdRate={game.HeadingRate:F2} clr={(game.rig != null ? game.rig.WaterClearance : -1f):F2} low={(game.rig != null ? game.rig.LowestBone : "")} rootY={(game.rig != null ? game.rig.RootY : 0f):F2} {(game.rig != null ? game.rig.Debug_ : "")}");
    }

    /// <summary>Rider close-ups (front, side, rear 3/4, carving at the buoy, pulling through the wakes). Rebuilds the scene first.</summary>
    public static void RiderTest() { SkiViewBuilder.BuildAll(); RiderShots(); }
    public static void RampTest() { SkiViewBuilder.BuildAll(); RampShots(); }
    /// <summary>Pass 13: the tournament jump ramp close up (course side, front deck / jacks / anchors, the toe).</summary>
    public static void RampShots()
    {
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); var cam = Camera.main;
        if (game == null || game.jumpRamp == null) { Debug.LogWarning("[SkiView] no ramp"); return; }
        game.cameraFollow = false;
        for (int i = 0; i < 90; i++) game.SimulateFor(1f / 30f);   // let the ramp water run
        var R = game.jumpRamp; float H = game.rampHeight;
        Vector3 L(float x, float y, float z) => R.TransformPoint(new Vector3(x, y, z));
        Shot(cam, game, "jump_ramp_course_side", () => { cam.fieldOfView = 38f; cam.transform.position = L(-9.5f, 1.6f, -9.0f); cam.transform.LookAt(L(0f, 0.7f, -3.6f)); });
        Shot(cam, game, "jump_ramp_front", () => { cam.fieldOfView = 40f; cam.transform.position = L(-5.0f, 1.9f, 9.0f); cam.transform.LookAt(L(0f, 0.8f, 0.2f)); });
        Shot(cam, game, "jump_ramp_skier_view", () => { cam.fieldOfView = 50f; cam.transform.position = L(-1.2f, 1.25f, -16.0f); cam.transform.LookAt(L(0f, H * 0.6f, -2f)); });
        Shot(cam, game, "jump_ramp_surface_low", () => { cam.fieldOfView = 40f; cam.transform.position = L(-3.6f, 2.6f, -6.5f); cam.transform.LookAt(L(0.2f, H - 0.2f, -0.6f)); });
        game.cameraFollow = true;
    }
    public static void JumpTest() { SkiViewBuilder.BuildAll(); JumpShots(); }
    /// <summary>Pass 14 diagnostic: placeholder-geometry audit + what sits under a pixel of the ride-out still.</summary>
    public static void AuditTest()
    {
        SkiViewBuilder.BuildAll();
        var prims = new HashSet<string> { "Cube", "Sphere", "Cylinder", "Capsule", "Quad", "Plane" };
        var counts = new Dictionary<string, int>();
        foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var m = mf.sharedMesh; if (m == null) continue;
            var r = mf.GetComponent<Renderer>(); string mat = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "-";
            string path = mf.name; var t = mf.transform.parent; int d = 0; while (t != null && d < 3) { path = t.name + "/" + path; t = t.parent; d++; }
            string key = (prims.Contains(m.name) ? "PRIM " : "") + m.name + " [" + mat + "] " + System.Text.RegularExpressions.Regex.Replace(path, "[0-9]+", "#");
            counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
        }
        foreach (var kv in counts) if (kv.Key.StartsWith("PRIM ")) Debug.Log($"[SkiView] audit {kv.Value}x {kv.Key}");
        var terr = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        if (terr != null) { int i = 0; foreach (var tp in terr.terrainData.treePrototypes) Debug.Log($"[SkiView] audit treeproto {i++} {(tp.prefab != null ? tp.prefab.name : "-")}"); }
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); var cam = Camera.main;
        game.BeginJumpRun(false);
        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.RideOut && game.JumpT >= 1.0f, 30f);
        game.cameraFollow = true; cam.aspect = (float)W / H; game.ApplyVisuals(true);
        foreach (var vp in new[] { new Vector2(0.26f, 0.76f), new Vector2(0.26f, 0.70f), new Vector2(0.24f, 0.55f), new Vector2(0.26f, 0.82f) })
        {
            var ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0)); float best = 1e9f; string hit = "none";
            foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var r = mf.GetComponent<Renderer>(); if (r == null || !r.enabled || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                if (!r.bounds.IntersectRay(ray, out float bd) || bd > best) continue;
                var mm = mf.sharedMesh; var vs = mm.vertices; var tr = mm.triangles; var M = mf.transform.localToWorldMatrix;
                for (int k = 0; k < tr.Length; k += 3)
                {
                    Vector3 a0 = M.MultiplyPoint3x4(vs[tr[k]]), b0 = M.MultiplyPoint3x4(vs[tr[k + 1]]), c0 = M.MultiplyPoint3x4(vs[tr[k + 2]]);
                    Vector3 e1 = b0 - a0, e2 = c0 - a0, pv = Vector3.Cross(ray.direction, e2); float det = Vector3.Dot(e1, pv); if (Mathf.Abs(det) < 1e-9f) continue;
                    float inv = 1f / det; Vector3 tv = ray.origin - a0; float u = Vector3.Dot(tv, pv) * inv; if (u < 0 || u > 1) continue;
                    Vector3 qv = Vector3.Cross(tv, e1); float v = Vector3.Dot(ray.direction, qv) * inv; if (v < 0 || u + v > 1) continue;
                    float tt = Vector3.Dot(e2, qv) * inv; if (tt > 0.3f && tt < best) { best = tt; string path = mf.name; var t = mf.transform.parent; int d = 0; while (t != null && d < 4) { path = t.name + "/" + path; t = t.parent; d++; } hit = $"{path} mesh={mm.name} verts={mm.vertexCount} mat={(r.sharedMaterial != null ? r.sharedMaterial.name : "-")}"; }
                }
            }
            Debug.Log($"[SkiView] audit pick {vp} dist {best:0.0} -> {hit} at {ray.GetPoint(Mathf.Min(best, 500f))}");
        }
        TreeDiag(cam, new Vector2(0.26f, 0.76f));
    }
    /// <summary>Terrain tree instances nearest a view ray (terrain trees are not GameObjects), plus a close-up of the nearest.</summary>
    static void TreeDiag(Camera cam, Vector2 vp)
    {
        var terr = UnityEngine.Object.FindFirstObjectByType<Terrain>(); if (terr == null) return;
        var td = terr.terrainData; var ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0));
        var list = new List<(float d, int proto, Vector3 p, float h)>();
        foreach (var ti in td.treeInstances)
        {
            Vector3 p = Vector3.Scale(ti.position, td.size) + terr.transform.position;
            float h = ti.heightScale * 16f; Vector3 c = p + Vector3.up * h * 0.6f;
            Vector3 q = c - ray.origin; float t = Vector3.Dot(q, ray.direction); if (t < 5f || t > 200f) continue;
            list.Add((Vector3.Cross(ray.direction, q).magnitude, ti.prototypeIndex, p, ti.heightScale));
        }
        list.Sort((x, y) => x.d.CompareTo(y.d));
        for (int i = 0; i < Mathf.Min(6, list.Count); i++) Debug.Log($"[SkiView] audit tree {i}: off-ray {list[i].d:0.0} m proto {list[i].proto} ({td.treePrototypes[list[i].proto].prefab.name}) at {list[i].p} scale {list[i].h:0.00}");
        if (list.Count > 0)
        {
            var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); var tp = list[0].p;
            Shot(cam, game, "dbg_tree_closeup", () => { cam.fieldOfView = 40f; Vector3 c = tp + (cam.transform.position - tp).normalized * 25f; c.y = tp.y + 6f; cam.transform.position = c; cam.transform.LookAt(tp + Vector3.up * 8f); });
        }
    }
    public static void P14Test() { SkiViewBuilder.BuildAll(); var cam = Camera.main; var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.BeginJumpRun(false); StepUntil(game, () => game.jphase == SkiViewGame.JPhase.RideOut && game.JumpT >= 1.0f, 30f); game.cameraFollow = true; cam.aspect = (float)W / H; game.ApplyVisuals(true); TreeDiag(cam, new Vector2(0.26f, 0.76f)); game.Init(); RampShots(); JumpShots(); }
    static bool StepUntil(SkiViewGame g, Func<bool> done, float maxT, float dt = 1f / 30f)
    {
        for (float t = 0f; t < maxT; t += dt) { if (done()) return true; g.SimulateFor(dt); }
        return done();
    }
    /// <summary>Pass 13/14: jump runs with scripted button timing (cut on the approach, pop on the ramp). The ideal run gives the
    /// stills (boat camera on the approach, ramp-side replay angles, the playable shore jump camera, a close side view of the flight,
    /// the boat camera again after the ride-out); then a late pop (out the front), early / late timing samples and a missed cut.
    /// Writes Screens/jump_samples_v14.json for the sheet.</summary>
    public static void JumpShots()
    {
        Directory.CreateDirectory(OutDir);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); var cam = Camera.main;
        if (game == null || game.jumpRamp == null) { Debug.LogWarning("[SkiView] no ramp"); return; }
        bool ap0 = game.autopilot; game.autopilot = false;
        var R = game.jumpRamp; Vector3 lip = R.position; Vector3 fwd = R.forward; fwd.y = 0f; fwd.Normalize();
        float Fov(Vector3 camPos, Vector3 at, float frameH) => Mathf.Clamp(2f * Mathf.Atan(frameH * 0.5f / Vector3.Distance(camPos, at)) * Mathf.Rad2Deg, 8f, 60f);
        var samples = new List<string>();
        // name, cut time [s before the toe], pop (ramp fraction; > 1 = after the lip; NaN = no press), stills?
        var runs = new (string name, float cut, float pop, bool stills)[] { ("ideal", -4.3f, 0.86f, true), ("late_pop", -4.3f, 1.2f, true), ("early", -5.7f, 0.70f, false), ("late_cut", -3.0f, 0.90f, false), ("no_cut", float.NaN, float.NaN, false) };
        foreach (var rc in runs)
        {
            bool crash = rc.name == "late_pop";
            game.BeginJumpRun(false); game.jumpCutAt = float.IsNaN(rc.cut) ? 99f : rc.cut; game.jumpPopAt = rc.pop; game.SimulateFor(1f / 30f);
            Vector3 side = game.JumpSide;   // from the course toward the ramp / house shore
            Vector3 Sk(float up = 0.9f) => game.SkierPos + Vector3.up * up;
            if (rc.name == "no_cut")
            {
                StepUntil(game, () => game.mode != SkiViewGame.Mode.Jump, 20f);
                Debug.Log($"[SkiView] jumpsample no_cut: result '{game.JumpResult}' mode {game.mode} banner '{game.banner}'");
                samples.Add("{\"name\": \"no_cut\", \"result\": \"" + game.JumpResult + "\"}");
                continue;
            }
            if (rc.name == "ideal")
            {
                StepUntil(game, () => game.JumpRunT >= -6.0f, 8f);
                Shot(cam, game, "jump_1_approach", null);   // boat camera (counter-cut through the wakes)
                StepUntil(game, () => game.JumpRunT >= -1.4f, 6f);
                Shot(cam, game, "jump_2_cut", () => { Vector3 c = lip - side * 12f + fwd * 10f + Vector3.up * 3.0f, at = Vector3.Lerp(lip - fwd * 5f, Sk(0.8f), 0.5f); cam.transform.position = c; cam.transform.LookAt(at); cam.fieldOfView = Fov(c, at, Vector3.Distance(lip, game.SkierPos) * 0.8f + 5f); });
            }
            StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Ramp && game.RampProgress >= 0.45f, 14f, 1f / 60f);
            if (rc.name == "ideal")
            {
                Shot(cam, game, "jump_3_on_ramp", () => { Vector3 c = lip - side * 9f - fwd * 7f + Vector3.up * 1.3f; cam.transform.position = c; cam.transform.LookAt(Sk(0.9f)); cam.fieldOfView = Fov(c, Sk(0.9f), 6.5f); });
                StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Flight && game.JumpT >= 0.08f, 2f, 1f / 60f);
                Shot(cam, game, "jump_4_takeoff", () => { Vector3 c = lip - side * 12f + fwd * 1.0f + Vector3.up * 0.8f; cam.transform.position = c; cam.transform.LookAt(Vector3.Lerp(Sk(0.6f), lip, 0.25f)); cam.fieldOfView = Fov(c, Sk(0.6f), 8f); });
            }
            StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Flight && game.JumpVel.y <= 0.4f, 3f, 1f / 60f);
            if (rc.stills)
            {
                if (rc.name == "ideal")
                {
                    Shot(cam, game, "jump_5_airborne", null);   // the playable shore jump camera
                    // close side view square to the flight line (pose check: the body folds forward over the skis)
                    Vector3 v = game.JumpVel; v.y = 0f; v.Normalize(); Vector3 sv = Vector3.Cross(Vector3.up, v); if (Vector3.Dot(sv, side) > 0) sv = -sv;
                    Shot(cam, game, "jump_5b_side", () => { Vector3 c = Sk(0.4f) + sv * 11f + Vector3.up * 0.3f; cam.transform.position = c; cam.transform.LookAt(Sk(0.5f)); cam.fieldOfView = Fov(c, Sk(0.5f), 4.2f); });
                }
            }
            else Shot(cam, game, "jump_s_" + rc.name, null);
            if (crash)
            {
                StepUntil(game, () => game.mode == SkiViewGame.Mode.Fallen && game.ActionT >= game.fallImpactT + 0.25f, 4f, 1f / 60f);
                Shot(cam, game, "jump_8_crash", null);
                StepUntil(game, () => game.ActionT >= 1.8f, 3f);
                Shot(cam, game, "jump_9_crash_after", null);
                Debug.Log($"[SkiView] jumptest crash: mode {game.mode} msg '{game.sim.message}' banner '{game.banner}'");
            }
            else
            {
                StepUntil(game, () => game.mode != SkiViewGame.Mode.Jump || (game.jphase == SkiViewGame.JPhase.Landing && game.JumpT >= 0.15f), 4f, 1f / 60f);
                if (rc.name == "ideal")
                {
                    Shot(cam, game, "jump_6_landing", null);
                    StepUntil(game, () => game.jphase == SkiViewGame.JPhase.RideOut && game.JumpT >= 1.0f, 4f);
                    Shot(cam, game, "jump_7_rideout", null);
                    Debug.Log($"[SkiView] jumptest good: dist {game.JumpDistance:0.0} m air {game.JumpAirTime:0.00} s apex {game.JumpApex:0.0} m lip {game.JumpLipSpeed:0.0} m/s banner '{game.banner}'");
                }
            }
            samples.Add("{" + $"\"name\": \"{rc.name}\", \"cut\": {rc.cut:0.00}, \"pop\": {(float.IsNaN(rc.pop) ? -1f : rc.pop):0.00}, \"popHit\": {game.JumpPopT:0.00}, \"toeSpeed\": {game.JumpToeSpeed:0.0}, \"cross\": {game.JumpCrossDeg:0.0}, \"lip\": {game.JumpLipSpeed:0.0}, \"dist\": {game.JumpDistance:0.0}, \"air\": {game.JumpAirTime:0.00}, \"apex\": {game.JumpApex:0.0}, \"landLat\": {game.JumpLandLat:0.0}, \"result\": \"{game.JumpResult}\"" + "}");
            Debug.Log($"[SkiView] jumpsample {rc.name}: cut {rc.cut:0.0} pop {rc.pop:0.00} -> toe {game.JumpToeSpeed:0.0} m/s, crossing {game.JumpCrossDeg:0.0} deg, {game.JumpDistance:0.0} m ({Mathf.FloorToInt(game.JumpDistance / 0.3048f + 0.5f)} ft), touchdown {game.JumpLandLat:0.0} m from the slalom line, {game.JumpResult}");
            StepUntil(game, () => game.mode == SkiViewGame.Mode.Turn, 10f);
            if (rc.name == "ideal")
            {
                game.SimulateFor(1f / 30f);
                Shot(cam, game, "jump_7b_boatcam", null);   // back on the boat camera after the ride-out
                Debug.Log($"[SkiView] jumptest handover: mode {game.mode} rope {game.sim.ropeM:0.0} theta {game.sim.theta:0.00}");
            }
        }
        File.WriteAllText(Path.Combine(OutDir, "jump_samples_v14.json"), "[\n  " + string.Join(",\n  ", samples) + "\n]\n");
        game.jumpCutAt = float.NaN; game.jumpPopAt = float.NaN; game.autopilot = ap0;
        game.jumpMode = false; game.SetJumpGear(false); game.Init();
    }
    /// <summary>One editor session: build scene, all captures, rider shots, then the Android player (no second import).</summary>
    public static void FullPipeline() { SkiViewBuilder.BuildAll(); Run(); EnvShots(); RiderShots(); RampShots(); JumpShots(); LakeShotsAll(); BoatShots(); CameraShots(); LakePerfAll(); SkiViewBuild.Android(); }
    public static void EnvTest() { SkiViewBuilder.BuildAll(); EnvShots(); }
    /// <summary>Environment realism v5: in-game angles matched to Scott's lake photos (for Screens/env_compare_v5.png) + checks.</summary>
    public static void EnvShots()
    {
        Directory.CreateDirectory(OutDir);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null) { EditorSceneManager.OpenScene(SkiViewBuilder.SCENE); game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.Init(); }
        if (!game.autopilot) { game.autopilot = true; game.Init(); }
        var cam = game.cam; var terrain = Terrain.activeTerrain;
        int q0 = QualitySettings.GetQualityLevel();
        int pcQ = Array.FindIndex(QualitySettings.names, n => n.Contains("PC")); if (pcQ >= 0) QualitySettings.SetQualityLevel(pcQ, true);
        float Ground(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : 0f;
        float fov0 = cam.fieldOfView;
        int guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2) && guard++ < 20000) game.SimulateFor(1f / 30f);

        // A (nautique-01): low, close side view of the boat on the calm lake, far fir line + houses behind
        Shot(cam, game, "env_a_boat_low", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            Vector3 side = Vector3.Dot(r, Vector3.right) > 0 ? r : -r;   // shoot from the west side, toward the east bank (houses, far shore)
            cam.fieldOfView = 50f;
            cam.transform.position = b - side * 10.5f + f * 1.0f + Vector3.up * 1.25f;
            cam.transform.LookAt(b + Vector3.up * 0.9f + side * 2f);
            cam.transform.rotation = Quaternion.Euler(-3f, cam.transform.eulerAngles.y, 0);
        });
        // E: wake check from behind/above (V-wake, foam fade, ripples)
        Shot(cam, game, "env_e_wake_v", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd;
            cam.fieldOfView = 55f;
            cam.transform.position = b - f * 26f + Vector3.up * 9f;
            cam.transform.LookAt(b - f * 6f);
        });
        var hero = GameObject.Find("Lot_8017_HERO");
        var dock = GameObject.Find("Dock_8017");
        if (dock != null)
        {
            Transform d = dock.transform;
            // B (8017 listing p01): from the lawn above the shore, looking down the lake (lake to the right, firs on the left bank)
            Shot(cam, game, "env_b_lawn_down_lake", () =>
            {
                Vector3 inland = -d.forward, along = Vector3.Cross(Vector3.up, inland);   // shoreline tangent
                if (along.z > 0) along = -along;                                            // the southward one
                Vector3 p = d.position + along * 10f + inland * 0.8f; p.y = Ground(p) + 2.3f;   // pass 8: v2 terrace planting fills the old spot
                cam.fieldOfView = 58f;
                cam.transform.position = p;
                cam.transform.rotation = Quaternion.LookRotation((along * 0.55f + d.forward * 0.8f).normalized) * Quaternion.Euler(3f, 0, 0);
            });
            for (int k = 0; k < 4; k++) ReflDiag(cam, "env_b", 1288f / 1600f, 1f - (610f + k * 45f) / 900f);   // the thin dark streak
            if (System.Environment.GetEnvironmentVariable("SV_STREAK_TEST") == "1")
            {
                // pass 12: does the streak follow the sky cubemap (rotate it 30 deg), or the big meshes (hide everything > 40 m)?
                Action placeB = () =>
                {
                    Vector3 inland = -d.forward, along = Vector3.Cross(Vector3.up, inland); if (along.z > 0) along = -along;
                    Vector3 p = d.position + along * 10f + inland * 0.8f; p.y = Ground(p) + 2.3f;
                    cam.fieldOfView = 58f; cam.transform.position = p;
                    cam.transform.rotation = Quaternion.LookRotation((along * 0.55f + d.forward * 0.8f).normalized) * Quaternion.Euler(3f, 0, 0);
                };
                var skyM = RenderSettings.skybox; float rot0 = skyM.GetFloat("_Rotation");
                skyM.SetFloat("_Rotation", Mathf.Repeat(rot0 + 30f, 360f)); Shot(cam, game, "dbg_env_b_skyrot30", placeB); skyM.SetFloat("_Rotation", rot0);
                var hidden = new System.Collections.Generic.List<Renderer>();
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.enabled && r.gameObject.layer != 4 && (r.bounds.size.x > 40f || r.bounds.size.z > 40f) && !(r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("Water")))
                    { Debug.Log($"[SkiView] streaktest hide big {r.name} size={r.bounds.size}"); r.enabled = false; hidden.Add(r); }
                var terr = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None); foreach (var t in terr) t.drawHeightmap = false;
                Shot(cam, game, "dbg_env_b_nobig", placeB);
                // direct view of the sky along the reflected ray (-0.77, 0.29, -0.60), everything else hidden
                var allR = new System.Collections.Generic.List<Renderer>();
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.enabled) { r.enabled = false; allR.Add(r); }
                Shot(cam, game, "dbg_env_b_skydirect", () => { cam.fieldOfView = 40f; cam.transform.position = new Vector3(0, 50, 0); cam.transform.rotation = Quaternion.LookRotation(new Vector3(-0.77f, 0.29f, -0.60f)); });
                foreach (var r in allR) r.enabled = true;
                foreach (var r in hidden) r.enabled = true; foreach (var t in terr) t.drawHeightmap = true;
            }
            // C (8017 listing p14): the dock + canopy slip from the lawn behind it
            Shot(cam, game, "env_c_dock", () =>
            {
                Vector3 p = d.position - d.forward * 8f + d.right * 8f; p.y = Ground(p) + 3.4f;
                cam.fieldOfView = 62f;
                cam.transform.position = p;
                cam.transform.LookAt(d.position + d.forward * 9f - d.right * 1.5f + Vector3.up * 0.2f);
            });
        }
        // D (8505 lake video f_013): across the water at the east-bank houses and their fir backdrop
        Transform target = null;
        foreach (Transform h in GameObject.Find("Houses_EastBank_N_to_S").transform) if (h.name.StartsWith("Lot_") && h.position.z < 60f && h.position.z > -60f) { target = h; break; }
        if (target != null)
            Shot(cam, game, "env_d_houses_across", () =>
            {
                Vector3 hp = target.position;
                cam.fieldOfView = 45f;
                cam.transform.position = new Vector3(hp.x - 150f, 1.8f, hp.z - 25f);
                cam.transform.LookAt(new Vector3(hp.x, 6f, hp.z));
            });
        // H/I/J (pass 7): Christopher's lot FBXs - hero 8017 from the lake, its terraces, and a long row of houses across the water
        if (hero != null)
        {
            Shot(cam, game, "env_h_hero_8017_lake", () =>
            {
                Vector3 p = hero.transform.position, fwd = hero.transform.forward, rt = hero.transform.right;
                cam.fieldOfView = 50f;
                cam.transform.position = new Vector3(0, 1.6f, 0) + new Vector3(p.x, 0, p.z) + fwd * 100f - rt * 28f;
                cam.transform.LookAt(new Vector3(p.x, 3.0f, p.z) + fwd * 22f);
            });
            Shot(cam, game, "env_i_hero_8017_terraces", () =>
            {
                Vector3 p = hero.transform.position, fwd = hero.transform.forward, rt = hero.transform.right;
                cam.fieldOfView = 55f;
                cam.transform.position = new Vector3(p.x, 3.2f, p.z) + fwd * 64f + rt * 20f;
                cam.transform.LookAt(new Vector3(p.x, 2.0f, p.z) + fwd * 30f);
            });
        }
        if (target != null)
            Shot(cam, game, "env_j_houses_row", () =>
            {
                Vector3 hp = target.position;
                cam.fieldOfView = 40f;
                cam.transform.position = new Vector3(hp.x - 95f, 2.2f, hp.z - 70f);
                cam.transform.LookAt(new Vector3(hp.x + 5f, 3.5f, hp.z + 10f));
            });
        var lot8409 = GameObject.Find("Lot_8409_VACANT");
        if (lot8409 != null)
            Shot(cam, game, "env_k_lots_topdown_south", () =>
            {
                Vector3 p = lot8409.transform.position;
                cam.fieldOfView = 50f;
                cam.transform.position = p + Vector3.up * 190f + Vector3.right * -10f;
                cam.transform.rotation = Quaternion.Euler(89f, 0f, 0f);
            });
        // F (pass 6): shoreline edging close-up along a lawn bank (wet band, lip, rock/grass sections)
        if (target != null)
            Shot(cam, game, "env_f_shoreline", () =>
            {
                Vector3 hp = target.position;
                cam.fieldOfView = 50f;
                cam.transform.position = new Vector3(hp.x - 62f, 1.6f, hp.z - 22f);   // from the water, like the 8505 lake video
                cam.transform.LookAt(new Vector3(hp.x - 24f, 1.2f, hp.z + 4f));
            });
        // sun alignment check: look toward the light's source; bright HDRI sun should sit at frame centre-top
        Shot(cam, game, "env_sky_suncheck", () =>
        {
            Vector3 toSun = -RenderSettings.sun.transform.forward;
            cam.fieldOfView = 80f;
            cam.transform.position = new Vector3(-40, 4, 0);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(toSun.x, 0, toSun.z).normalized) * Quaternion.Euler(-Mathf.Asin(toSun.y) * Mathf.Rad2Deg + 25f, 0, 0);
        });
        // mobile tier version of A (1/4-res reflection, hard shadows, lower LOD bias)
        QualitySettings.SetQualityLevel(0, true);
        Shot(cam, game, "env_a_boat_low_mobile", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            Vector3 side = Vector3.Dot(r, Vector3.right) > 0 ? r : -r;
            cam.fieldOfView = 50f;
            cam.transform.position = b - side * 10.5f + f * 1.0f + Vector3.up * 1.25f;
            cam.transform.LookAt(b + Vector3.up * 0.9f + side * 2f);
            cam.transform.rotation = Quaternion.Euler(-3f, cam.transform.eulerAngles.y, 0);
        });
        QualitySettings.SetQualityLevel(q0, true);
        cam.fieldOfView = fov0;
        Debug.Log("[SkiView] env shots done");
    }

    public static void RiderShots()
    {
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam; game.autopilot = true; game.Init();
        int guard = 0;
        // pulling through the wakes between ball 1 and 2 (max lean / load)
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 1 && Mathf.Abs(game.sim.Lateral) < 2.5f) && guard++ < 8000) game.SimulateFor(1f / 60f);
        Transform k = game.skier;
        void At(string n, Vector3 local, Vector3 lookLocal, float fov = 40f)
            => Shot(cam, game, n, () => { cam.fieldOfView = fov; cam.transform.position = k.position + Quaternion.LookRotation(Flat(k.forward)) * local; cam.transform.LookAt(k.position + Quaternion.LookRotation(Flat(k.forward)) * lookLocal); });
        // boat-camera style (like the reference frames: from ahead, a little to the side) so the lean reads as tilt in the image
        At("rider_pull_front", new Vector3(1.4f, 1.3f, 5.5f), new Vector3(0, 0.75f, 0));
        At("rider_pull_behind", new Vector3(-1.2f, 1.4f, -5.5f), new Vector3(0, 0.75f, 0));   // sun behind the camera
        At("rider_pull_side", new Vector3(4.2f, 1.0f, 0.3f), new Vector3(0, 0.85f, 0));
        At("rider_pull_rear34", new Vector3(-2.8f, 1.6f, -3.0f), new Vector3(0, 0.8f, 0.3f));
        // carving at the next buoy (outside hand released)
        guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && Mathf.Abs(game.sim.Lateral) > 10.6f && Mathf.Abs(game.sim.omega) < 0.9f) && guard++ < 8000) game.SimulateFor(1f / 60f);
        for (int i = 0; i < 12; i++) game.SimulateFor(1f / 60f);   // into the turn (peak heading rate)
        At("rider_carve_buoy", new Vector3(1.8f, 1.0f, 5.5f), new Vector3(0, 0.6f, 0));
        for (int i = 0; i < 21; i++) game.SimulateFor(1f / 60f);
        At("rider_carve_exit", new Vector3(-1.8f, 1.1f, 5.5f), new Vector3(0, 0.6f, 0));
        // pass 11: fall -> treading water -> deep-water start (Mixamo batch 3 clips + the DeepStart state)
        game.BeginFall("Capture: fall");
        void RunTo(SkiViewGame.Mode m, float t) { int g = 0; while (!(game.mode == m && game.ActionT >= t) && g++ < 2000) game.SimulateFor(1f / 60f); }
        // camera heights are above the water (the skier root sinks during the fall and the start)
        void Side(string n, Vector3 local, Vector3 look, float fov) => Shot(cam, game, n, () =>
        {
            var q = Quaternion.LookRotation(Flat(k.forward)); Vector3 w0 = game.SkierPos; w0.y = 0f;
            cam.fieldOfView = fov; cam.transform.position = w0 + q * local; cam.transform.LookAt(w0 + q * look);
        });
        RunTo(SkiViewGame.Mode.Fallen, 0.35f); Side("fall_1_catch", new Vector3(4.5f, 1.3f, 2.0f), new Vector3(0, 0.6f, 0), 42f);
        RunTo(SkiViewGame.Mode.Fallen, 1.15f); Side("fall_2_into_water", new Vector3(4.5f, 1.3f, 2.0f), new Vector3(0, 0.3f, 0), 42f);
        RunTo(SkiViewGame.Mode.Fallen, 2.9f); Side("fall_3_treading", new Vector3(3.0f, 1.0f, 2.5f), new Vector3(0, 0.1f, 0), 38f);
        RunTo(SkiViewGame.Mode.DeepStart, 0.4f); Side("dws_1_in_water", new Vector3(1.4f, 0.8f, 3.4f), new Vector3(0, 0.15f, 0), 40f);
        RunTo(SkiViewGame.Mode.DeepStart, 1.7f); Side("dws_2_rising", new Vector3(1.8f, 1.1f, 5.0f), new Vector3(0, 0.3f, 0), 38f);
        RunTo(SkiViewGame.Mode.DeepStart, 2.6f); Side("dws_3_almost_up", new Vector3(2.0f, 1.2f, 5.5f), new Vector3(0, 0.6f, 0), 38f);
        { int g = 0; while (game.mode == SkiViewGame.Mode.DeepStart && g++ < 600) game.SimulateFor(1f / 60f); }
        for (int i = 0; i < 20; i++) game.SimulateFor(1f / 60f);
        Side("dws_4_up", new Vector3(2.0f, 1.3f, 5.5f), new Vector3(0, 0.75f, 0), 38f);
        cam.fieldOfView = 52f;
    }
    static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

    /// <summary>Pure-logic smoke test (works with -nographics): runs 3 laps on autopilot and logs pass results.</summary>
    public static void SimTest()
    {
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        game.autopilot = true; game.Init();
        var lastMode = game.mode; int lastPasses = 0; float t = 0;
        while (t < 600f && game.passes < 6)
        {
            game.SimulateFor(1f / 60f); t += 1f / 60f;
            if (game.mode != lastMode) { Debug.Log($"[SkiView][sim] t={t:0.0}s v={game.path.Speed(game.boatS):0.0} {lastMode}->{game.mode} boat=({game.BoatPos.x:0},{game.BoatPos.z:0}) rope={game.sim.ropeM:0.00} msg={game.sim.message}"); lastMode = game.mode; }
            if (game.passes != lastPasses) { Debug.Log($"[SkiView][sim] pass {game.passes} done: {game.banner}"); lastPasses = game.passes; }
        }
        Debug.Log($"[SkiView][sim] END t={t:0}s passes={game.passes} clean={game.cleanPasses} rope={SkierSim.ROPE_LABEL[game.sim.ropeIndex]}");
    }

    /// <summary>b10.8: Wade V3 materials + rebuild lakes, then chase + lean closeups into Screens/wade_v3/.</summary>
    public static void WadeV3Pipeline()
    {
        SkiViewSkierSetup.WadeV3();
        SkiViewBuilder.BuildAll();
        Directory.CreateDirectory(Path.Combine(OutDir, "wade_v3"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        game.SimulateFor(4f);
        int guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2 && game.sim.z < SkierSim.BuoyZ(2) + 12f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        Shot(cam, game, "wade_v3/b10.8_chase_ball3", null);
        guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3 && game.sim.z < SkierSim.BuoyZ(3) + 4f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        LogBodyTilt(game, "wade_v3 lean");
        Shot(cam, game, "wade_v3/b10.8_lean_closeup", () =>
        {
            Vector3 s = game.SkierPos; Transform sk = game.skier;
            var anim = game.rig != null ? game.rig.GetComponentInChildren<Animator>() : null;
            Transform head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Transform la = anim ? anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null, ra = anim ? anim.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            Vector3 hp = head != null ? head.position : s + sk.up * 1.5f;
            Vector3 chest = (la != null && ra != null) ? Vector3.Cross(ra.position - la.position, Vector3.up) : sk.forward; chest.y = 0; chest = chest.sqrMagnitude > 1e-4f ? chest.normalized : game.BoatFwd;
            cam.fieldOfView = 24f;
            float bestScore = -1f, bestAz = 0f;
            for (float az = 0f; az < 360f; az += 5f)
            {
                Vector3 dir = Quaternion.Euler(0, az, 0) * Vector3.forward;
                float front = Vector3.Dot(dir, chest);
                if (front < 0.45f) continue;
                cam.transform.position = s + dir * 7.5f + Vector3.up * 1.2f;
                cam.transform.LookAt(Vector3.Lerp(s, hp, 0.5f));
                Vector3 a0 = cam.WorldToViewportPoint(s), a1 = cam.WorldToViewportPoint(hp);
                Vector2 d2 = new Vector2((a1.x - a0.x) * cam.aspect, a1.y - a0.y);
                float lean = Mathf.Atan2(Mathf.Abs(d2.x), Mathf.Max(1e-4f, d2.y)) * Mathf.Rad2Deg;
                float score = lean + 6f * (1f - Mathf.Abs(front - 0.7f));
                if (score > bestScore) { bestScore = score; bestAz = az; }
            }
            Vector3 bd = Quaternion.Euler(0, bestAz, 0) * Vector3.forward;
            cam.transform.position = s + bd * 7.5f + Vector3.up * 1.2f;
            cam.transform.LookAt(Vector3.Lerp(s, hp, 0.5f));
        });
        Shot(cam, game, "wade_v3/b10.8_face", () =>
        {
            Transform head = game.rig != null ? game.rig.GetComponentInChildren<Animator>()?.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 hp = head != null ? head.position + Vector3.up * 0.06f : game.SkierPos + Vector3.up * 1.2f, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 22f;
            cam.transform.position = hp + f * 1.6f - r * 0.9f + Vector3.up * 0.15f;
            cam.transform.LookAt(hp);
        });
        game.camMode = SkiViewGame.CamMode.SkierChase;
        Shot(cam, game, "wade_v3/b10.8_skier_chase", null);
        Debug.Log("[SkiView] Wade V3 pipeline shots -> Screens/wade_v3/");
    }

    /// <summary>b10.9: reimport SkiBoat r4 (continuous windshield), rebuild lakes, chase + helm/bow screens into Screens/boat/.</summary>
    public static void BoatR4Pipeline()
    {
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset("Assets/SkiView/Boat/SkiBoat.fbx", ImportAssetOptions.ForceUpdate);
        SkiViewBuilder.BuildAll();
        Directory.CreateDirectory(Path.Combine(OutDir, "boat"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        game.SimulateFor(4f);
        int guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2 && game.sim.z < SkierSim.BuoyZ(2) + 12f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        var tow = game.boatTow;
        Debug.Log($"[SkiView] b10.9 boat TowPoint local={(tow != null ? tow.localPosition.ToString("F3") : "NULL")} world={(tow != null ? tow.position.ToString("F3") : "NULL")} boatWake={(game.boatWake != null)} boatSpray={(game.boatSprayPoint != null)}");
        Shot(cam, game, "boat/b10.9_chase", () => { game.camMode = SkiViewGame.CamMode.SkierChase; game.ApplyVisuals(true); });
        Shot(cam, game, "boat/b10.9_side_34", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 28f;
            cam.transform.position = b - r * 9f + f * 4.5f + Vector3.up * 2.2f;
            cam.transform.LookAt(b + Vector3.up * 0.9f + f * 0.3f);
        });
        Shot(cam, game, "boat/b10.9_helm", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            // looking aft-ish at the windshield / helm from bow walk
            cam.fieldOfView = 40f;
            cam.transform.position = b + f * 2.6f + Vector3.up * 1.55f - r * 0.15f;
            cam.transform.LookAt(b - f * 0.4f + Vector3.up * 1.15f);
        });
        Shot(cam, game, "boat/b10.9_bow", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 45f;
            cam.transform.position = b - f * 0.8f + Vector3.up * 1.7f + r * 0.2f;
            cam.transform.LookAt(b + f * 2.4f + Vector3.up * 1.1f);
        });
        Shot(cam, game, "boat/b10.9_broadcast", () => { game.camMode = SkiViewGame.CamMode.Broadcast; game.ApplyVisuals(true); });
        Debug.Log("[SkiView] Boat r4 pipeline shots -> Screens/boat/");
    }

    /// <summary>b10.24: Scott-cleared confirm of boat r4 continuous windshield (mesh already live since b10.9).
    /// ForceUpdate SkiBoat.fbx, BuildAll, same angles as BoatR4Pipeline → Screens/boat/b10.24_*.
    /// Run: unity_run.sh SkiViewCapture.BoatR4ConfirmPipeline</summary>
    public static void BoatR4ConfirmPipeline()
    {
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset("Assets/SkiView/Boat/SkiBoat.fbx", ImportAssetOptions.ForceUpdate);
        SkiViewBuilder.BuildAll();
        Directory.CreateDirectory(Path.Combine(OutDir, "boat"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        game.SimulateFor(4f);
        int guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2 && game.sim.z < SkierSim.BuoyZ(2) + 12f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        var tow = game.boatTow;
        Debug.Log($"[SkiView] b10.24 boat TowPoint local={(tow != null ? tow.localPosition.ToString("F3") : "NULL")} world={(tow != null ? tow.position.ToString("F3") : "NULL")} boatWake={(game.boatWake != null)} boatSpray={(game.boatSprayPoint != null)}");
        Shot(cam, game, "boat/b10.24_chase", () => { game.camMode = SkiViewGame.CamMode.SkierChase; game.ApplyVisuals(true); });
        Shot(cam, game, "boat/b10.24_side_34", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 28f;
            cam.transform.position = b - r * 9f + f * 4.5f + Vector3.up * 2.2f;
            cam.transform.LookAt(b + Vector3.up * 0.9f + f * 0.3f);
        });
        Shot(cam, game, "boat/b10.24_helm", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 40f;
            cam.transform.position = b + f * 2.6f + Vector3.up * 1.55f - r * 0.15f;
            cam.transform.LookAt(b - f * 0.4f + Vector3.up * 1.15f);
        });
        Shot(cam, game, "boat/b10.24_bow", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 45f;
            cam.transform.position = b - f * 0.8f + Vector3.up * 1.7f + r * 0.2f;
            cam.transform.LookAt(b + f * 2.4f + Vector3.up * 1.1f);
        });
        Shot(cam, game, "boat/b10.24_broadcast", () => { game.camMode = SkiViewGame.CamMode.Broadcast; game.ApplyVisuals(true); });
        Debug.Log("[SkiView] Boat r4 confirm pipeline shots -> Screens/boat/b10.24_*");
    }

    /// <summary>b10.12: hot-swap Scott-cleared Wade V3 lean pro-match clips; capture left/right (+ buoy apex/chase) into Screens/wade_lean/.</summary>
    public static void LeanPipeline()
    {
        AssetDatabase.Refresh();
        foreach (var f in new[] {
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance.fbx",
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance_LeanLeft.fbx",
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance_LeanRight.fbx",
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox@Lean58.fbx" })
            AssetDatabase.ImportAsset("Assets/SkiView/Skier/" + f, ImportAssetOptions.ForceUpdate);
        SkiViewBuilder.BuildAll();
        Directory.CreateDirectory(Path.Combine(OutDir, "wade_lean"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        game.SimulateFor(4f);
        int guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2 && game.sim.z < SkierSim.BuoyZ(2) + 12f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        var rig = game.rig;
        Debug.Log($"[SkiView] b10.12 lean clips pull={(rig != null && rig.pull != null ? rig.pull.name : "-")} leanL={(rig != null && rig.leanL != null ? rig.leanL.name : "-")} leanR={(rig != null && rig.leanR != null ? rig.leanR.name : "-")} apex={((rig != null) ? rig.leanTotalApexDeg : -1f):0} stretch={((rig != null) ? rig.stretchLeanDeg : -1f):0}");

        void LeanCam()
        {
            Vector3 s = game.SkierPos; Transform sk = game.skier;
            var anim = rig != null ? rig.GetComponentInChildren<Animator>() : null;
            Transform head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Transform la = anim ? anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null, ra = anim ? anim.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            Vector3 hp = head != null ? head.position : s + sk.up * 1.5f;
            Vector3 chest = (la != null && ra != null) ? Vector3.Cross(ra.position - la.position, Vector3.up) : sk.forward; chest.y = 0; chest = chest.sqrMagnitude > 1e-4f ? chest.normalized : game.BoatFwd;
            cam.fieldOfView = 26f;
            float bestScore = -1f, bestAz = 0f;
            for (float az = 0f; az < 360f; az += 5f)
            {
                Vector3 dir = Quaternion.Euler(0, az, 0) * Vector3.forward;
                float front = Vector3.Dot(dir, chest);
                if (front < 0.35f) continue;
                cam.transform.position = s + dir * 7.2f + Vector3.up * 1.15f;
                cam.transform.LookAt(Vector3.Lerp(s, hp, 0.55f));
                Vector3 a0 = cam.WorldToViewportPoint(s), a1 = cam.WorldToViewportPoint(hp);
                Vector2 d2 = new Vector2((a1.x - a0.x) * cam.aspect, a1.y - a0.y);
                float lean = Mathf.Atan2(Mathf.Abs(d2.x), Mathf.Max(1e-4f, d2.y)) * Mathf.Rad2Deg;
                float score = lean + 5f * (1f - Mathf.Abs(front - 0.65f));
                if (score > bestScore) { bestScore = score; bestAz = az; }
            }
            Vector3 bd = Quaternion.Euler(0, bestAz, 0) * Vector3.forward;
            cam.transform.position = s + bd * 7.2f + Vector3.up * 1.15f;
            cam.transform.LookAt(Vector3.Lerp(s, hp, 0.55f));
        }

        // forceClip 7/8 = leanL / leanR mixer slots (prove the Scott-cleared V3 pose FBXs)
        if (rig != null) { rig.forceClip = 7; for (int i = 0; i < 8; i++) game.SimulateFor(1f / 60f); }
        LogBodyTilt(game, "b10.12 leanL force");
        Shot(cam, game, "wade_lean/b10.12_lean_left", LeanCam);

        if (rig != null) { rig.forceClip = 8; for (int i = 0; i < 8; i++) game.SimulateFor(1f / 60f); }
        LogBodyTilt(game, "b10.12 leanR force");
        Shot(cam, game, "wade_lean/b10.12_lean_right", LeanCam);

        // natural turn lean (no forceClip): procedural ~58° apex + blended leanL/leanR — Lean58 FBX has no runtime slot
        if (rig != null) rig.forceClip = -1;
        guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3 && game.sim.z < SkierSim.BuoyZ(3) + 4f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        LogBodyTilt(game, "b10.12 lean58 apex");
        Shot(cam, game, "wade_lean/b10.12_lean58_apex", LeanCam);
        Shot(cam, game, "wade_lean/b10.12_chase", () => { game.camMode = SkiViewGame.CamMode.SkierChase; game.ApplyVisuals(true); });
        Debug.Log("[SkiView] b10.12 lean pipeline shots -> Screens/wade_lean/");
    }

    /// <summary>b10.11: import TrickSki_V1, enable Tricks Phase A, capture title/setup/on-water/results into Screens/tricks/.</summary>
    public static void TricksPipeline()
    {
        AssetDatabase.Refresh();
        SkiViewSkierSetup.TrickSkiV1();
        Directory.CreateDirectory(Path.Combine(OutDir, "tricks"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        // Title with Tricks enabled (UI may be hidden in capture; still shoot lake + gear)
        game.trickMode = true; game.SetTrickGear(true);
        game.BeginTrickRound();
        // settle deep-water start a bit
        for (int i = 0; i < 90; i++) game.SimulateFor(1f / 60f);
        Shot(cam, game, "tricks/b10.11_setup_dock", () =>
        {
            game.camMode = SkiViewGame.CamMode.Broadcast;
            game.ApplyVisuals(true);
            // pull cam back toward hut / dock
            Vector3 b = game.BoatPos; Vector3 f = game.BoatFwd;
            cam.transform.position = b - f * 18f + Vector3.up * 6f + Vector3.Cross(Vector3.up, f).normalized * 8f;
            cam.transform.LookAt(b + Vector3.up * 1.2f);
            cam.fieldOfView = 50f;
        });
        // advance toward entry / armed
        int guard = 0;
        while (game.tphase != SkiViewGame.TPhase.Armed && game.tphase != SkiViewGame.TPhase.Active && guard++ < 8000)
            game.SimulateFor(1f / 60f);
        Shot(cam, game, "tricks/b10.11_on_water", () =>
        {
            game.camMode = SkiViewGame.CamMode.SkierChase;
            game.ApplyVisuals(true);
        });
        // force a surface 180 for ski yaw pose
        typeof(SkiViewGame).GetMethod("BeginSpin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(game, new object[] { 180f, 1f, false, false });
        for (int i = 0; i < 40; i++) game.SimulateFor(1f / 60f);
        Shot(cam, game, "tricks/b10.11_spin", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
        });
        // results card path: end pass
        typeof(SkiViewGame).GetMethod("EndTrickPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(game, new object[] { false, "capture" });
        // if pass 1 done goes to approach, force results
        if (!game.TrickResultsOpen)
            typeof(SkiViewGame).GetMethod("EndTrickPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(game, new object[] { false, "capture" });
        Shot(cam, game, "tricks/b10.11_results", () =>
        {
            game.camMode = SkiViewGame.CamMode.Broadcast;
            game.ApplyVisuals(true);
        });
        // title-ish wide shot with trick ski visible
        game.BeginTrickRound();
        for (int i = 0; i < 30; i++) game.SimulateFor(1f / 60f);
        Shot(cam, game, "tricks/b10.11_title_enabled", () =>
        {
            Vector3 s = game.SkierPos;
            cam.transform.position = s + game.BoatFwd * -6f + Vector3.up * 2.2f + Vector3.Cross(Vector3.up, game.BoatFwd) * 3f;
            cam.transform.LookAt(s + Vector3.up * 0.8f);
            cam.fieldOfView = 40f;
            game.ApplyVisuals(true);
        });
        Debug.Log("[SkiView] b10.11 tricks pipeline shots -> Screens/tricks/");
    }


    /// <summary>b10.13: refresh V3 ski materials/previews, capture on-water colourway + binding shots into Screens/skis/.</summary>
    public static void SkiPipeline()
    {
        AssetDatabase.Refresh();
        SkiViewSkiPreviews.Make();
        Directory.CreateDirectory(Path.Combine(OutDir, "skis"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        // settle into a carve-ish pose mid-course (same pattern as LeanPipeline)
        game.SimulateFor(4f);
        int guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2) && guard++ < 6000) game.SimulateFor(1f / 60f);
        for (int i = 0; i < 30; i++) game.SimulateFor(1f / 60f);

        void PlaceSki(int ski, int bind)
        {
            SkiViewGame.SaveSetup(ski, bind, SkiViewGame.SetupRope, SkiViewGame.SetupMph);
            game.ApplySki(ski);
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
        }

        // Green toe plate / double boot + Red toe / double — on-water
        PlaceSki(0, 0);
        Shot(cam, game, "skis/b10.13_water_green_toe", () => { game.camMode = SkiViewGame.CamMode.CloseChase; game.ApplyVisuals(true); });
        PlaceSki(0, 1);
        Shot(cam, game, "skis/b10.13_water_green_db", () => { game.camMode = SkiViewGame.CamMode.CloseChase; game.ApplyVisuals(true); });
        PlaceSki(1, 0);
        Shot(cam, game, "skis/b10.13_water_red_toe", () => { game.camMode = SkiViewGame.CamMode.CloseChase; game.ApplyVisuals(true); });
        PlaceSki(1, 1);
        Shot(cam, game, "skis/b10.13_water_red_db", () => { game.camMode = SkiViewGame.CamMode.CloseChase; game.ApplyVisuals(true); });
        // wider chase green toe for picker-context water plate
        PlaceSki(0, 0);
        Shot(cam, game, "skis/b10.13_water_green_chase", () => { game.camMode = SkiViewGame.CamMode.SkierChase; game.ApplyVisuals(true); });
        Debug.Log("[SkiView] b10.13 ski pipeline shots -> Screens/skis/");
    }


    /// <summary>b10.15 Jump Phase A re-shoot: fix approach (no half-black), ramp mesh in frame, results context.
    /// Writes Screens/jump/b10.15_*.png. UI Toolkit HUD still does not composite via Shot() — refresh results_ref.</summary>
    public static void JumpPhaseAPipeline()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(Path.Combine(OutDir, "jump"));
        string mock = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/ui/mockups"));
        void CopyRef(string src, string dst)
        {
            string a = Path.Combine(mock, src), b = Path.Combine(OutDir, "jump", dst);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] ref " + dst); }
            else Debug.LogWarning("[SkiView] missing mockup " + a);
        }
        CopyRef("31a_title_jump_focused_desktop.png", "b10.15_title_jump_ref.png");
        CopyRef("31b_jump_setup_desktop.png", "b10.15_setup_ref.png");
        CopyRef("31c_jump_results_desktop.png", "b10.15_results_ref.png");

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        if (game.jumpRamp == null) { Debug.LogError("[SkiView] JumpPhaseA: no ramp on lake"); return; }
        SkiViewGame.SaveJumpSetup(3, 1); // 54 km/h, 1.65 m
        // Match JumpShots: BeginJumpRun only. BeginJumpRound first put the boat on a heading where lake water
        // rendered pure black in batch RT (diag_j1_boat with JumpShots path is fine).
        bool ap0 = game.autopilot; game.autopilot = false;
        game.Init();

        var R = game.jumpRamp; Vector3 lip = R.position; Vector3 fwd = R.forward; fwd.y = 0f; fwd.Normalize();
        float H = game.rampHeight;
        Vector3 L(float x, float y, float z) => R.TransformPoint(new Vector3(x, y, z));
        Vector3 Sk(float up = 0.9f) => game.SkierPos + Vector3.up * up;

        game.BeginJumpRun(false);
        game.jumpCutAt = -4.3f; game.jumpPopAt = 0.86f;
        game.SimulateFor(1f / 30f);

        // Approach: boat camera at JumpRunT≈-6 (JumpShots jump_1_approach) — full-frame water + skier + course.
        StepUntil(game, () => game.JumpRunT >= -6.0f, 12f);
        Shot(cam, game, "jump/b10.15_approach", null);

        // Ramp: wait until mid-ramp (RampProgress) so the deck is under the skier; course-side framing from RampShots + skier.
        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Ramp && game.RampProgress >= 0.45f, 14f, 1f / 60f);
        Shot(cam, game, "jump/b10.15_ramp", () =>
        {
            Vector3 side = game.JumpSide;
            // Prefer course-side ramp plate, biased to keep skier + deck in frame (jump_3_on_ramp / jump_ramp_course_side).
            Vector3 c = lip - side * 9f - fwd * 7f + Vector3.up * 1.6f;
            Vector3 at = Vector3.Lerp(L(0f, Mathf.Max(0.5f, H * 0.45f), -2.5f), Sk(0.9f), 0.55f);
            cam.transform.position = c;
            cam.transform.LookAt(at);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.2f;
        });

        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Flight && game.JumpT >= 0.15f, 4f, 1f / 60f);
        Shot(cam, game, "jump/b10.15_flight", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase; game.ApplyVisuals(true);
        });
        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Landing || game.jphase == SkiViewGame.JPhase.RideOut || game.mode == SkiViewGame.Mode.Fallen || game.mode == SkiViewGame.Mode.Turn, 8f, 1f / 60f);
        Shot(cam, game, "jump/b10.15_landing", () =>
        {
            game.camMode = SkiViewGame.CamMode.SkierChase; game.ApplyVisuals(true);
        });

        if (!game.JumpResultsOpen)
        {
            typeof(SkiViewGame).GetMethod("RegisterJumpPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(game, new object[] { "SCORED", Mathf.Max(25f, game.JumpDistance) });
            while (game.JumpPassCount < SkiViewGame.JUMP_PASSES)
                typeof(SkiViewGame).GetMethod("RegisterJumpPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(game, new object[] { game.JumpPassCount == 1 ? "BALK" : "SCORED", game.JumpPassCount == 1 ? 0f : 38.6f });
        }
        // Shot() is camera RT only — UI Toolkit HUD does not composite (see results_ref). Frame results-context wide plate.
        Shot(cam, game, "jump/b10.15_results", () =>
        {
            Vector3 s = game.SkierPos;
            Vector3 c = s - game.BoatFwd * 16f + Vector3.up * 6.5f + game.JumpSide * 3f;
            cam.transform.position = c;
            cam.transform.LookAt(s + Vector3.up * 1.2f);
            cam.fieldOfView = 52f;
            game.ApplyVisuals(true);
        });
        game.autopilot = ap0;
        Debug.Log($"[SkiView] b10.15 jump pipeline -> Screens/jump/ resultsOpen={game.JumpResultsOpen} dist={game.JumpDistance:0.0} best={game.JumpBest:0.0} passes={game.JumpPassCount}");
    }

    /// <summary>b10.13/15: compose missing picker_red_db (red + double boot) mirroring green_db / red_toe plates.</summary>
    public static void SkiPickerRedDb()
    {
        string skis = Path.Combine(OutDir, "skis");
        Directory.CreateDirectory(skis);
        string redToePath = Path.Combine(skis, "b10.13_picker_red_toe.png");
        string greenDbPath = Path.Combine(skis, "b10.13_picker_green_db.png");
        string greenToePath = Path.Combine(skis, "b10.13_picker_green_toe.png");
        string skiDbPath = Path.GetFullPath(Path.Combine(Application.dataPath, "SkiView/UI/Resources/SkiViewUI/Skis/ski_1_db.png"));
        string outPath = Path.Combine(skis, "b10.13_picker_red_db.png");
        if (!File.Exists(redToePath) || !File.Exists(greenDbPath) || !File.Exists(greenToePath) || !File.Exists(skiDbPath))
        {
            Debug.LogError($"[SkiView] SkiPickerRedDb missing inputs toe={File.Exists(redToePath)} gdb={File.Exists(greenDbPath)} gtoe={File.Exists(greenToePath)} ski={File.Exists(skiDbPath)}");
            return;
        }
        Texture2D Load(string p) { var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); t.LoadImage(File.ReadAllBytes(p)); return t; }
        var redToe = Load(redToePath); var greenDb = Load(greenDbPath); var greenToe = Load(greenToePath); var skiDb = Load(skiDbPath);
        int W = redToe.width, H = redToe.height;
        var outPx = redToe.GetPixels32();
        var gdb = greenDb.GetPixels32(); var gtoe = greenToe.GetPixels32();
        // Hero placement matched to existing picker plates (scale 0.7 of 1280x720 preview @ 711,210 in top-left coords).
        const int hx = 711, hyTop = 210, tw = 896, th = 503;
        // Unity GetPixels32 is bottom-up; convert top-left y to bottom-up row.
        int hy = H - hyTop - th;
        // Resize ski_1_db -> tw x th via bilinear into temp
        var skiPx = skiDb.GetPixels32();
        Color32 SampleSki(float u, float v)
        {
            float x = u * (skiDb.width - 1), y = v * (skiDb.height - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, skiDb.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, skiDb.height - 1);
            int x1 = Mathf.Min(x0 + 1, skiDb.width - 1), y1 = Mathf.Min(y0 + 1, skiDb.height - 1);
            float tx = x - x0, ty = y - y0;
            Color32 a = skiPx[y0 * skiDb.width + x0], b = skiPx[y0 * skiDb.width + x1], c = skiPx[y1 * skiDb.width + x0], d = skiPx[y1 * skiDb.width + x1];
            Color ca = Color.Lerp(a, b, tx), cb = Color.Lerp(c, d, tx); return Color.Lerp(ca, cb, ty);
        }
        for (int j = 0; j < th; j++)
            for (int i = 0; i < tw; i++)
            {
                // j=0 is bottom of hero in Unity space
                float u = (i + 0.5f) / tw;
                float v = (j + 0.5f) / th;
                Color32 s = SampleSki(u, v);
                int di = (hy + j) * W + (hx + i);
                float a = s.a / 255f;
                var b = outPx[di];
                outPx[di] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(s.r * a + b.r * (1f - a)), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(s.g * a + b.g * (1f - a)), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(s.b * a + b.b * (1f - a)), 0, 255),
                    255);
            }
        // Copy green_db vs green_toe diffs (bind toggle + caption Toe plate→Double boot), skip hero + left list.
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (x < 580) continue;
                bool inHero = x >= hx && x < hx + tw && y >= hy && y < hy + th;
                if (inHero) continue;
                int i = y * W + x;
                int dr = Mathf.Abs(gdb[i].r - gtoe[i].r), dg = Mathf.Abs(gdb[i].g - gtoe[i].g), db = Mathf.Abs(gdb[i].b - gtoe[i].b);
                if (Mathf.Max(dr, Mathf.Max(dg, db)) > 18) outPx[i] = gdb[i];
            }
        var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        outTex.SetPixels32(outPx); outTex.Apply();
        File.WriteAllBytes(outPath, outTex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(redToe); UnityEngine.Object.DestroyImmediate(greenDb);
        UnityEngine.Object.DestroyImmediate(greenToe); UnityEngine.Object.DestroyImmediate(skiDb); UnityEngine.Object.DestroyImmediate(outTex);
        Debug.Log("[SkiView] SkiPickerRedDb -> " + outPath);
    }

    /// <summary>b10.15 batch Tricks Phase A listen smoke: call SkiAudio APIs, log Play/clip, write offline EDITOR-BATCH concat note dir.</summary>
    public static void TricksListenSmoke()
    {
        AssetDatabase.Refresh();
        string stage = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/audio/_tools/listen_smoke_b10.15"));
        Directory.CreateDirectory(stage);
        var log = new System.Text.StringBuilder();
        void L(string s) { log.AppendLine(s); Debug.Log("[SkiView][TricksListen] " + s); }

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null) { L("ERROR: no SkiViewGame"); File.WriteAllText(Path.Combine(stage, "editor_batch_log.txt"), log.ToString()); return; }
        game.autopilot = true; game.Init();
        // Ensure() calls DontDestroyOnLoad — illegal in edit-mode batch. Build SkiAudio the same way without DDOL.
        SkiAudio audio = SkiAudio.Instance;
        if (audio == null)
        {
            var bank = Resources.Load<SkiAudioBank>("SkiViewAudio/Bank");
            if (bank == null) { L("ERROR: Resources/SkiViewAudio/Bank missing"); File.WriteAllText(Path.Combine(stage, "editor_batch_log.txt"), log.ToString()); return; }
            var go = new GameObject("SkiAudio_EditorBatch");
            audio = go.AddComponent<SkiAudio>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(SkiAudio).GetField("bank", flags)?.SetValue(audio, bank);
            typeof(SkiAudio).GetMethod("Setup", flags)?.Invoke(audio, null);
        }
        typeof(SkiAudio).GetMethod("Attach", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(audio, new object[] { game });
        if (audio == null) { L("ERROR: SkiAudio editor init failed"); File.WriteAllText(Path.Combine(stage, "editor_batch_log.txt"), log.ToString()); return; }
        L("SkiAudio ready (edit-mode, no DontDestroyOnLoad)");

        // Ensure AudioListener exists for Play()
        if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null && game.cam != null)
            game.cam.gameObject.AddComponent<AudioListener>();

        System.Reflection.FieldInfo PoolField(string n) => typeof(SkiAudio).GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var pool2 = PoolField("pool2")?.GetValue(audio) as System.Collections.IList;
        var pool3 = PoolField("pool3")?.GetValue(audio) as System.Collections.IList;
        var trickSlide = PoolField("trickSlide")?.GetValue(audio) as AudioSource;

        string PlayingClips()
        {
            var names = new System.Collections.Generic.List<string>();
            void Scan(System.Collections.IList pool)
            {
                if (pool == null) return;
                foreach (var o in pool)
                {
                    var s = o as AudioSource;
                    if (s != null && s.isPlaying && s.clip != null) names.Add(s.clip.name + $"@vol={s.volume:0.00}");
                }
            }
            Scan(pool2); Scan(pool3);
            if (trickSlide != null && trickSlide.volume > 0.01f && trickSlide.clip != null)
                names.Add(trickSlide.clip.name + $"(loop vol={trickSlide.volume:0.00} playing={trickSlide.isPlaying})");
            return names.Count == 0 ? "(none)" : string.Join(", ", names);
        }

        double t0 = EditorApplication.timeSinceStartup;
        void Cue(string label, System.Action act)
        {
            act();
            // pump a few editor ticks so AudioSource can report isPlaying
            for (int i = 0; i < 3; i++) { System.Threading.Thread.Sleep(30); }
            L($"{EditorApplication.timeSinceStartup - t0:0.000}s  {label}  playing={PlayingClips()}");
        }

        L("method=batch executeMethod SkiViewCapture.TricksListenSmoke");
        L($"scene={SkiViewBuilder.SCENE} game={game.name}");

        Cue("TrickCarve", () => audio.TrickCarve());
        Cue("TrickSlide(true)", () =>
        {
            audio.TrickSlide(true);
            // Edit-mode Time.deltaTime is ~0 — force loop audible for the log.
            if (trickSlide != null) { trickSlide.volume = 0.75f; if (!trickSlide.isPlaying && trickSlide.clip != null) trickSlide.Play(); }
        });
        Cue("TrickSpin(180)", () => audio.TrickSpin(180));
        Cue("TrickWakePop", () => audio.TrickWakePop());
        Cue("TrickSpin(180) wake path", () => audio.TrickSpin(180));
        Cue("TrickWakeLand", () => audio.TrickWakeLand());
        Cue("TrickTimer reset>5", () => audio.TrickTimer(6f));
        for (int sec = 5; sec >= 0; sec--)
        {
            int s = sec;
            Cue($"TrickTimer({s})", () => audio.TrickTimer(s));
        }
        Cue("TrickCredited(150,false)", () => audio.TrickCredited(150, false));
        // Delayed chime is LateUpdate-driven; fire bank chime directly so batch log confirms clip.
        Cue("ui_trick_score_chime (LateUpdate stand-in)", () => audio.Play("ui_trick_score_chime", null, 0f, -2f));
        Cue("TrickFall", () => audio.TrickFall());
        Cue("TrickRunResult", () => audio.TrickRunResult());
        Cue("TrickSlide(false)", () => { audio.TrickSlide(false); if (trickSlide != null) trickSlide.volume = 0f; });

        File.WriteAllText(Path.Combine(stage, "editor_batch_log.txt"), log.ToString());
        // Marker for offline concat (parent/python may rebuild; we always stamp EDITOR-BATCH).
        File.WriteAllText(Path.Combine(stage, "METHOD.txt"), "EDITOR-BATCH\nexecuteMethod=SkiViewCapture.TricksListenSmoke\n");
        L("wrote " + stage);
    }

    /// <summary>Diag: approach stills matching JumpShots path (no BeginJumpRound).</summary>
    public static void JumpApproachDiag()
    {
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        if (game.jumpRamp == null) { Debug.LogError("no ramp"); return; }
        bool ap0 = game.autopilot; game.autopilot = false;
        SkiViewGame.SaveJumpSetup(3, 1);
        game.Init();
        // Match JumpShots: BeginJumpRun only (no BeginJumpRound)
        game.BeginJumpRun(false); game.jumpCutAt = -4.3f; game.jumpPopAt = 0.86f; game.SimulateFor(1f / 30f);
        Vector3 lip = game.jumpRamp.position; Vector3 fwd = game.jumpRamp.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 Sk(float up = 0.9f) => game.SkierPos + Vector3.up * up;
        StepUntil(game, () => game.JumpRunT >= -6.0f, 8f);
        Debug.Log($"[SkiView] diag A runT={game.JumpRunT:0.00} jphase={game.jphase} skier={game.SkierPos} boat={game.BoatPos} jumpMode={game.jumpMode}");
        Shot(cam, game, "jump/diag_j1_boat", null);
        StepUntil(game, () => game.JumpRunT >= -1.4f, 6f);
        Vector3 side = game.JumpSide;
        Shot(cam, game, "jump/diag_j2_cut", () =>
        {
            Vector3 c = lip - side * 12f + fwd * 10f + Vector3.up * 3.0f;
            Vector3 at = Vector3.Lerp(lip - fwd * 5f, Sk(0.8f), 0.5f);
            cam.transform.position = c; cam.transform.LookAt(at);
            cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan((Vector3.Distance(lip, game.SkierPos) * 0.8f + 5f) * 0.5f / Vector3.Distance(c, at)) * Mathf.Rad2Deg, 8f, 60f);
        });
        game.autopilot = ap0;
        Debug.Log("[SkiView] JumpApproachDiag done");
    }

    /// <summary>b10.15: Scott-cleared lean polish hot-swap (torso normals/weights + L/R tip equalize). Screens/wade_lean/b10.15_*.</summary>

    /// <summary>b10.16: import JumpSki_V1 + Ramp_V1 PBR, rebuild, capture Jump screens Screens/jump/b10.16_*.</summary>
    public static void JumpSkiRampPipeline()
    {
        AssetDatabase.Refresh();
        SkiViewSkierSetup.JumpSkiV1();
        SkiViewSkierSetup.RampV1();
        SkiViewBuilder.BuildAll();
        Directory.CreateDirectory(Path.Combine(OutDir, "jump"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        if (game.jumpRamp == null) { Debug.LogError("[SkiView] JumpSkiRamp: no ramp"); return; }
        Debug.Log($"[SkiView] b10.16 gear jumpSkiL={(game.jumpSkiL != null ? game.jumpSkiL.name : "-")} rampKids={game.jumpRamp.childCount}");
        SkiViewGame.SaveJumpSetup(3, 1); // 54 km/h, 1.65 m
        bool ap0 = game.autopilot; game.autopilot = false;
        game.Init();
        game.rampHeight = 1.65f; game.jumpBoatKmh = 54f;
        var R = game.jumpRamp; Vector3 lip = R.position; Vector3 fwd = R.forward; fwd.y = 0f; fwd.Normalize();
        float H = game.rampHeight;
        Vector3 L(float x, float y, float z) => R.TransformPoint(new Vector3(x, y, z));
        Vector3 Sk(float up = 0.9f) => game.SkierPos + Vector3.up * up;
        game.BeginJumpRun(false);
        game.jumpCutAt = -4.3f; game.jumpPopAt = 0.86f;
        game.SimulateFor(1f / 30f);
        StepUntil(game, () => game.JumpRunT >= -6.0f, 12f);
        Shot(cam, game, "jump/b10.16_approach", null);
        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Ramp && game.RampProgress >= 0.45f, 14f, 1f / 60f);
        Shot(cam, game, "jump/b10.16_ramp", () =>
        {
            Vector3 side = game.JumpSide;
            Vector3 c = lip - side * 9f - fwd * 7f + Vector3.up * 1.6f;
            Vector3 at = Vector3.Lerp(L(0f, Mathf.Max(0.5f, H * 0.45f), -2.5f), Sk(0.9f), 0.55f);
            cam.transform.position = c; cam.transform.LookAt(at); cam.fieldOfView = 40f; cam.nearClipPlane = 0.2f;
        });
        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Flight && game.JumpT >= 0.15f, 4f, 1f / 60f);
        Shot(cam, game, "jump/b10.16_flight", () => { game.camMode = SkiViewGame.CamMode.CloseChase; game.ApplyVisuals(true); });
        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Landing || game.jphase == SkiViewGame.JPhase.RideOut || game.mode == SkiViewGame.Mode.Fallen || game.mode == SkiViewGame.Mode.Turn, 8f, 1f / 60f);
        Shot(cam, game, "jump/b10.16_landing", () => { game.camMode = SkiViewGame.CamMode.SkierChase; game.ApplyVisuals(true); });
        if (!game.JumpResultsOpen)
        {
            typeof(SkiViewGame).GetMethod("RegisterJumpPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(game, new object[] { "SCORED", Mathf.Max(25f, game.JumpDistance) });
            while (game.JumpPassCount < SkiViewGame.JUMP_PASSES)
                typeof(SkiViewGame).GetMethod("RegisterJumpPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(game, new object[] { game.JumpPassCount == 1 ? "BALK" : "SCORED", game.JumpPassCount == 1 ? 0f : 38.6f });
        }
        Shot(cam, game, "jump/b10.16_results", () =>
        {
            Vector3 s = game.SkierPos;
            cam.transform.position = s - game.BoatFwd * 16f + Vector3.up * 6.5f + game.JumpSide * 3f;
            cam.transform.LookAt(s + Vector3.up * 1.2f); cam.fieldOfView = 52f; game.ApplyVisuals(true);
        });
        game.autopilot = ap0;
        Debug.Log($"[SkiView] b10.16 jump ski+ramp pipeline -> Screens/jump/ dist={game.JumpDistance:0.0} best={game.JumpBest:0.0}");
    }


    /// <summary>b10.19 T32 Jump results HUD on live scene. Writes Screens/jump/b10.19_*.png + meta for PIL composite.
    /// Run: -executeMethod SkiViewCapture.JumpResultsHudPipeline</summary>
    public static void JumpResultsHudPipeline()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(Path.Combine(OutDir, "jump"));
        string mock = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/ui/mockups"));
        void CopyRef(string src, string dst)
        {
            string a = Path.Combine(mock, src), b = Path.Combine(OutDir, "jump", dst);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] ref " + dst); }
            else Debug.LogWarning("[SkiView] missing mockup " + a);
        }
        CopyRef("32_jump_results_overlay_desktop.png", "b10.19_32_overlay_desktop.png");
        CopyRef("32_jump_results_overlay_phone.png", "b10.19_32_overlay_phone.png");
        CopyRef("31c_jump_results_desktop.png", "b10.19_results_ref.png");

        foreach (var n in new[] { "FP_Glove19", "FP_GloveAccent19", "FP_Glove" })
        {
            foreach (var g in AssetDatabase.FindAssets(n + " t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m == null || m.name != n) continue;
                if (n.Contains("Accent")) m.SetColor("_BaseColor", new Color(0.102f, 0.122f, 0.141f, 1f));
                else m.SetColor("_BaseColor", new Color(0.541f, 0.565f, 0.596f, 1f));
                EditorUtility.SetDirty(m);
            }
        }
        AssetDatabase.SaveAssets();

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        if (game.jumpRamp == null) { Debug.LogError("[SkiView] JumpResultsHud: no ramp"); return; }
        SkiViewGame.SaveJumpSetup(3, 1); // 54 km/h, 1.65 m
        bool ap0 = game.autopilot; game.autopilot = false;
        game.Init();
        game.rampHeight = 1.65f; game.jumpBoatKmh = 54f;

        game.BeginJumpRun(false);
        game.jumpCutAt = -4.3f; game.jumpPopAt = 0.86f;
        game.SimulateFor(1f / 30f);
        StepUntil(game, () => game.JumpRunT >= -6.0f, 12f);
        Shot(cam, game, "jump/b10.19_approach", null);
        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Flight && game.JumpT >= 0.12f, 14f, 1f / 60f);
        Shot(cam, game, "jump/b10.19_flight", () => { game.camMode = SkiViewGame.CamMode.CloseChase; game.ApplyVisuals(true); });
        StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Landing || game.jphase == SkiViewGame.JPhase.RideOut || game.mode == SkiViewGame.Mode.Fallen || game.mode == SkiViewGame.Mode.Turn, 8f, 1f / 60f);

        float live = game.JumpDistance > 1f ? game.JumpDistance : 38.6f;
        float p1 = 34.2f;
        float p3 = Mathf.Max(live, 38.6f);
        var reg = typeof(SkiViewGame).GetMethod("RegisterJumpPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        game.ResetJumpRound();
        reg?.Invoke(game, new object[] { "SCORED", p1 });
        reg?.Invoke(game, new object[] { "BALK", 0f });
        reg?.Invoke(game, new object[] { "SCORED", p3 });

        Shot(cam, game, "jump/b10.19_results_scene", () =>
        {
            Vector3 s = game.SkierPos;
            Vector3 c = s - game.BoatFwd * 16f + Vector3.up * 6.5f + game.JumpSide * 3f;
            cam.transform.position = c;
            cam.transform.LookAt(s + Vector3.up * 1.2f);
            cam.fieldOfView = 52f;
            game.ApplyVisuals(true);
        });

        if (game.fpGloveL != null)
        {
            game.camMode = SkiViewGame.CamMode.FirstPerson;
            game.ApplyVisuals(true);
            for (int i = 0; i < 8; i++) game.SimulateFor(1f / 60f);
            Shot(cam, game, "jump/b10.19_fp_hands", null);
            game.camMode = SkiViewGame.CamMode.SkierChase;
            game.ApplyVisuals(true);
        }

        int bestFt = Mathf.FloorToInt(game.JumpBest / 0.3048f + 0.5f);
        var passes = new System.Collections.Generic.List<string>();
        foreach (var line in game.JumpResultsSummary().Split('\n'))
            if (!string.IsNullOrEmpty(line)) passes.Add(line);
        float mph = game.jumpBoatKmh / 1.609344f;
        string setup = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0} km/h · {1:0.0} mph · {2:0.00} m ramp · 23 m rope", game.jumpBoatKmh, mph, game.rampHeight);
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "  \"best_m\": {0:0.###},\n", game.JumpBest);
        sb.AppendFormat("  \"best_ft\": {0},\n", bestFt);
        sb.Append("  \"unit\": \"ft\",\n");
        sb.Append("  \"setup\": ").Append(JsonString(setup)).Append(",\n");
        sb.Append("  \"passes\": [");
        for (int i = 0; i < passes.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(JsonString(passes[i]));
        }
        sb.Append("]\n}\n");
        string metaPath = Path.Combine(OutDir, "jump", "b10.19_results_meta.json");
        File.WriteAllText(metaPath, sb.ToString());
        Debug.Log("[SkiView] wrote " + metaPath + " best_m=" + game.JumpBest.ToString("0.0") + " ft=" + bestFt);

        string py = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/composite_jump_results_hud.py"));
        string scene = Path.Combine(OutDir, "jump", "b10.19_results_scene.png");
        string hud = Path.Combine(OutDir, "jump", "b10.19_results_hud.png");
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "\"" + py + "\" \"" + scene + "\" \"" + metaPath + "\" \"" + hud + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var proc = System.Diagnostics.Process.Start(psi);
            string so = proc.StandardOutput.ReadToEnd();
            string se = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60000);
            Debug.Log("[SkiView] composite exit=" + proc.ExitCode + " out=" + so + " err=" + se);
        }
        catch (System.Exception e) { Debug.LogWarning("[SkiView] composite failed: " + e.Message); }

        game.autopilot = ap0;
        Debug.Log("[SkiView] b10.19 Jump results HUD pipeline -> Screens/jump/ resultsOpen=" + game.JumpResultsOpen + " best=" + game.JumpBest.ToString("0.0") + "m/" + bestFt + "ft passes=" + game.JumpPassCount);
    }

    static string JsonString(string s)
    {
        if (s == null) return "\"\"";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }

    /// <summary>b10.17 Clinic Phase A: CLV2 load smoke, stance begin/end, copy T33 mockup refs → Screens/clinic/b10.17_*.</summary>
    public static void ClinicPhaseAPipeline()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(Path.Combine(OutDir, "clinic"));
        string stageMock = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/ui/mockups"));
        void CopyRef(string src, string dst)
        {
            string a = Path.Combine(stageMock, src);
            string b = Path.Combine(OutDir, "clinic", dst);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] clinic ref " + dst); }
            else Debug.LogWarning("[SkiView] clinic mockup missing " + a);
        }
        CopyRef("33a_title_clinic_desktop.png", "b10.17_33a_title_clinic.png");
        CopyRef("33b_clinic_lessons_desktop.png", "b10.17_33b_lessons.png");
        CopyRef("33b2_clinic_plan_desktop.png", "b10.17_33b2_plan.png");
        CopyRef("33c_clinic_results_desktop.png", "b10.17_33c_results.png");
        CopyRef("33c_clinic_results_fail_desktop.png", "b10.17_33c_results_fail.png");

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        bool ap0 = game.autopilot; game.autopilot = true;
        game.Init();
        SkiViewClinic.Ensure();
        var data = SkiViewClinic.Data;
        Debug.Log($"[SkiView] b10.17 CLV2 lessons={data.lessons.Length} modeName={SkiViewClinic.ModeName()} stanceUnlocked={SkiViewClinic.IsUnlocked("stance")} playable={SkiViewClinic.IsPlayable("stance")}");
        if (SkiViewClinic.Get("stance") == null) { Debug.LogError("[SkiView] stance lesson missing"); return; }

        // clear prior clinic prefs for clean capture
        PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + "stance.done");
        PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + "stance.bestBuoys");
        PlayerPrefs.Save();

        SkiViewClinic.BeginLesson(game, "stance", "handle_low");
        Debug.Log($"[SkiView] clinic begin mph={game.RunMph} rope={game.sim.ropeIndex} banner={game.banner}");
        // settle into a pass
        int guard = 0;
        while (game.mode != SkiViewGame.Mode.Pass && guard++ < 8000) game.SimulateFor(1f / 60f);
        for (int i = 0; i < 90; i++) game.SimulateFor(1f / 60f);
        Shot(cam, game, "clinic/b10.17_stance_pass", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
        });
        // simulate clean end
        if (game.mode == SkiViewGame.Mode.Pass)
        {
            typeof(SkiViewGame).GetMethod("EndPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(game, new object[] { true });
        }
        Debug.Log($"[SkiView] clinic end ResultsOpen={SkiViewClinic.ResultsOpen} complete={SkiViewClinic.IsComplete("stance")} best={SkiViewClinic.BestBuoys("stance"):0.#} fell={SkiViewClinic.LastFell}");
        Shot(cam, game, "clinic/b10.17_stance_after", () =>
        {
            game.camMode = SkiViewGame.CamMode.SkierChase;
            game.ApplyVisuals(true);
        });
        // fail path smoke (fresh)
        PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + "stance.done");
        SkiViewClinic.ClearActive();
        SkiViewClinic.BeginLesson(game, "stance", "knees");
        SkiViewClinic.EndPass(game, true, 1.5f);
        Debug.Log($"[SkiView] clinic fail path ResultsOpen={SkiViewClinic.ResultsOpen} complete={SkiViewClinic.IsComplete("stance")} notes={string.Join(" | ", SkiViewClinic.FeedbackLines())}");
        game.autopilot = ap0;
        Debug.Log("[SkiView] b10.17 Clinic Phase A pipeline -> Screens/clinic/");
    }



    /// <summary>b10.18 Clinic lessons 2–7 playable: unlock ladder, DA complete criteria, tip bank, T33 plates, TrickSki PBR soft.
    /// Writes Screens/clinic/b10.18_*.png. Run: -executeMethod SkiViewCapture.ClinicLessons27Pipeline</summary>
    public static void ClinicLessons27Pipeline()
    {
        AssetDatabase.Refresh();
        try { SkiViewSkierSetup.TrickSkiV1Pbr(); }
        catch (System.Exception e) { Debug.LogWarning("[SkiView] TrickSkiV1Pbr soft-skip: " + e.Message); }

        Directory.CreateDirectory(Path.Combine(OutDir, "clinic"));
        string stageMock = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/ui/mockups"));
        void CopyRef(string src, string dst)
        {
            string a = Path.Combine(stageMock, src);
            string b = Path.Combine(OutDir, "clinic", dst);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] clinic ref " + dst); }
            else Debug.LogWarning("[SkiView] clinic mockup missing " + a);
        }
        CopyRef("33a_title_clinic_desktop.png", "b10.18_33a_title_clinic.png");
        CopyRef("33b_clinic_lessons_desktop.png", "b10.18_33b_lessons.png");
        CopyRef("33b2_clinic_plan_desktop.png", "b10.18_33b2_plan.png");
        CopyRef("33c_clinic_results_desktop.png", "b10.18_33c_results.png");
        CopyRef("33c_clinic_results_fail_desktop.png", "b10.18_33c_results_fail.png");

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        bool ap0 = game.autopilot; game.autopilot = true;
        game.Init();
        SkiViewClinic.SkipLakeLoad = true;
        SkiViewClinic.Ensure();
        Debug.Log($"[SkiView] b10.18 CLV2 lessons={SkiViewClinic.Data.lessons.Length} tips={SkiViewClinic.GetTip("stance") != null} mode={SkiViewClinic.ModeName()}");

        // wipe ladder prefs for deterministic unlock smoke
        foreach (var id in SkiViewClinic.Ladder)
        {
            PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + id + ".done");
            PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + id + ".bestBuoys");
        }
        PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + "deep_water_start.done");
        PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + "rope_progression.maxUnlockedIndex");
        PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + "pending.lesson");
        PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + "pending.cue");
        PlayerPrefs.Save();

        void AssertPlay(string id, bool expect)
        {
            bool u = SkiViewClinic.IsUnlocked(id), p = SkiViewClinic.IsPlayable(id);
            Debug.Log($"[SkiView] unlock {id} unlocked={u} playable={p} expectPlayable={expect}");
            if (p != expect) Debug.LogError($"[SkiView] b10.18 playable mismatch {id} got={p} want={expect}");
        }
        AssertPlay("stance", true);
        AssertPlay("wide_pull", false);
        AssertPlay("deep_water_start", true);

        // DA criteria unit smoke (no sim)
        void Crit(string id, bool fell, float buoys, bool gates, bool want)
        {
            bool got = SkiViewClinic.EvaluateComplete(id, fell, buoys, gates, game);
            Debug.Log($"[SkiView] crit {id} fell={fell} buoys={buoys} gates={gates} -> {got} want={want}");
            if (got != want) Debug.LogError($"[SkiView] criteria mismatch {id}");
        }
        Crit("stance", false, 0f, false, true);
        Crit("stance", true, 6f, true, false);
        Crit("wide_pull", false, 4f, false, true);
        Crit("wide_pull", false, 3.5f, true, false);
        Crit("gate_first_ball", false, 1f, true, true);
        Crit("gate_first_ball", false, 1f, false, false);
        Crit("preturn", false, 5f, false, true);
        Crit("wake_crossing", false, 0f, false, true);
        Crit("rope_progression", false, 6f, true, true);
        Crit("rope_progression", false, 5f, true, false);

        string[] cues = { "handle_low", "wide", "pull_out", "early", "edge", "same_basics", "tuck" };
        string[] lessons = { "stance", "wide_pull", "gate_first_ball", "preturn", "wake_crossing", "rope_progression", "deep_water_start" };
        float[] passBuoys = { 3f, 4.5f, 2f, 5.5f, 3f, 6f, 0f };
        bool[] passGates = { false, false, true, false, false, true, false };
        bool[] expectOk = { true, true, true, true, true, true, true };

        void SettlePass()
        {
            int guard = 0;
            while (game.mode != SkiViewGame.Mode.Pass && game.mode != SkiViewGame.Mode.DeepStart && guard++ < 8000)
                game.SimulateFor(1f / 60f);
            for (int i = 0; i < 75; i++) game.SimulateFor(1f / 60f);
        }

        for (int i = 0; i < lessons.Length; i++)
        {
            string id = lessons[i];
            // unlock prior ladder steps so lesson is playable
            for (int k = 0; k < SkiViewClinic.Ladder.Length; k++)
            {
                if (SkiViewClinic.Ladder[k] == id) break;
                SkiViewClinic.MarkComplete(SkiViewClinic.Ladder[k], 6f);
            }
            AssertPlay(id, true);
            SkiViewClinic.ClearActive();
            string tip = SkiViewClinic.PlanTip(id) ?? "";
            Debug.Log($"[SkiView] tip[{id}] len={tip.Length} '{(tip.Length > 60 ? tip.Substring(0, 60) + "…" : tip)}'");

            SkiViewClinic.BeginLesson(game, id, cues[i]);
            if (id == "deep_water_start")
            {
                SettlePass();
                Shot(cam, game, "clinic/b10.18_" + id + "_dws", () =>
                {
                    game.camMode = SkiViewGame.CamMode.CloseChase;
                    game.ApplyVisuals(true);
                });
                SkiViewClinic.NotifyDeepStartOutcome(game, true);
            }
            else
            {
                SettlePass();
                Shot(cam, game, "clinic/b10.18_" + id + "_pass", () =>
                {
                    game.camMode = SkiViewGame.CamMode.CloseChase;
                    game.ApplyVisuals(true);
                });
                SkiViewClinic.EndPass(game, false, passBuoys[i], passGates[i]);
            }
            Debug.Log($"[SkiView] lesson {id} ResultsOpen={SkiViewClinic.ResultsOpen} complete={SkiViewClinic.LastComplete} expect={expectOk[i]} isDone={SkiViewClinic.IsComplete(id)} readout={SkiViewClinic.DataReadout(game).Replace("\n", " | ")} notes={string.Join(" · ", SkiViewClinic.FeedbackLines())}");
            if (SkiViewClinic.LastComplete != expectOk[i])
                Debug.LogError($"[SkiView] complete mismatch {id}");
            Shot(cam, game, "clinic/b10.18_" + id + "_after", () =>
            {
                game.camMode = SkiViewGame.CamMode.SkierChase;
                game.ApplyVisuals(true);
            });
        }

        // fail path on gate_first_ball (gates miss)
        SkiViewClinic.ClearActive();
        SkiViewClinic.MarkComplete("stance", 6f);
        SkiViewClinic.MarkComplete("wide_pull", 6f);
        SkiViewClinic.BeginLesson(game, "gate_first_ball", "pull_out");
        SkiViewClinic.EndPass(game, false, 0.5f, false);
        Debug.Log($"[SkiView] gate fail complete={SkiViewClinic.LastComplete} notes={string.Join(" | ", SkiViewClinic.FeedbackLines())}");
        if (SkiViewClinic.LastComplete) Debug.LogError("[SkiView] gate_first_ball should fail without gates");

        // ladder after stance-only: wide_pull playable, later locked
        foreach (var id in SkiViewClinic.Ladder)
        {
            PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + id + ".done");
            PlayerPrefs.DeleteKey(SkiViewClinic.PrefPrefix + id + ".bestBuoys");
        }
        PlayerPrefs.Save();
        SkiViewClinic.MarkComplete("stance", 3f);
        AssertPlay("wide_pull", true);
        AssertPlay("gate_first_ball", false);
        AssertPlay("rope_progression", false);

        game.autopilot = ap0;
        SkiViewClinic.SkipLakeLoad = false;
        SkiViewClinic.ClearActive();
        Debug.Log("[SkiView] b10.18 Clinic lessons 2–7 pipeline -> Screens/clinic/");
    }

    public static void LeanPolishPipeline()
    {
        AssetDatabase.Refresh();
        foreach (var f in new[] {
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox.fbx",
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox_Phone.fbx",
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance.fbx",
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance_LeanLeft.fbx",
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox@SlalomStance_LeanRight.fbx",
            "INTERNAL_ONLY_Felderpuss_V3_WadeCox@Lean58.fbx" })
            AssetDatabase.ImportAsset("Assets/SkiView/Skier/" + f, ImportAssetOptions.ForceUpdate);
        SkiViewSkierSetup.WadeV3();
        SkiViewBuilder.BuildAll();
        Directory.CreateDirectory(Path.Combine(OutDir, "wade_lean"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        game.SimulateFor(4f);
        int guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2 && game.sim.z < SkierSim.BuoyZ(2) + 12f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        var rig = game.rig;
        Debug.Log($"[SkiView] b10.15 lean polish pull={(rig != null && rig.pull != null ? rig.pull.name : "-")} leanL={(rig != null && rig.leanL != null ? rig.leanL.name : "-")} leanR={(rig != null && rig.leanR != null ? rig.leanR.name : "-")} apex={((rig != null) ? rig.leanTotalApexDeg : -1f):0}");

        void LeanCam()
        {
            Vector3 s = game.SkierPos; Transform sk = game.skier;
            var anim = rig != null ? rig.GetComponentInChildren<Animator>() : null;
            Transform head = anim ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Transform la = anim ? anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null, ra = anim ? anim.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            Vector3 hp = head != null ? head.position : s + sk.up * 1.5f;
            Vector3 chest = (la != null && ra != null) ? Vector3.Cross(ra.position - la.position, Vector3.up) : sk.forward; chest.y = 0; chest = chest.sqrMagnitude > 1e-4f ? chest.normalized : game.BoatFwd;
            cam.fieldOfView = 26f;
            float bestScore = -1f, bestAz = 0f;
            for (float az = 0f; az < 360f; az += 5f)
            {
                Vector3 dir = Quaternion.Euler(0, az, 0) * Vector3.forward;
                float front = Vector3.Dot(dir, chest);
                if (front < 0.35f) continue;
                cam.transform.position = s + dir * 7.2f + Vector3.up * 1.15f;
                cam.transform.LookAt(Vector3.Lerp(s, hp, 0.55f));
                Vector3 a0 = cam.WorldToViewportPoint(s), a1 = cam.WorldToViewportPoint(hp);
                Vector2 d2 = new Vector2((a1.x - a0.x) * cam.aspect, a1.y - a0.y);
                float lean = Mathf.Atan2(Mathf.Abs(d2.x), Mathf.Max(1e-4f, d2.y)) * Mathf.Rad2Deg;
                float score = lean + 5f * (1f - Mathf.Abs(front - 0.65f));
                if (score > bestScore) { bestScore = score; bestAz = az; }
            }
            Vector3 bd = Quaternion.Euler(0, bestAz, 0) * Vector3.forward;
            cam.transform.position = s + bd * 7.2f + Vector3.up * 1.15f;
            cam.transform.LookAt(Vector3.Lerp(s, hp, 0.55f));
        }

        if (rig != null) { rig.forceClip = 7; for (int i = 0; i < 8; i++) game.SimulateFor(1f / 60f); }
        LogBodyTilt(game, "b10.15 leanL");
        Shot(cam, game, "wade_lean/b10.15_lean_left", LeanCam);

        if (rig != null) { rig.forceClip = 8; for (int i = 0; i < 8; i++) game.SimulateFor(1f / 60f); }
        LogBodyTilt(game, "b10.15 leanR");
        Shot(cam, game, "wade_lean/b10.15_lean_right", LeanCam);

        if (rig != null) rig.forceClip = -1;
        guard = 0;
        while (!(game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3 && game.sim.z < SkierSim.BuoyZ(3) + 4f) && guard++ < 6000) game.SimulateFor(1f / 60f);
        LogBodyTilt(game, "b10.15 lean58 apex");
        Shot(cam, game, "wade_lean/b10.15_lean58_apex", LeanCam);
        Shot(cam, game, "wade_lean/b10.15_chase", () => { game.camMode = SkiViewGame.CamMode.SkierChase; game.ApplyVisuals(true); });
        Debug.Log("[SkiView] b10.15 lean polish pipeline -> Screens/wade_lean/");
    }



    /// <summary>b10.20 T29 Tricks Phase A polish: title/setup/results + landed/denied plates.
    /// Writes Screens/tricks/b10.20_*.png + PIL HUD composites (Shot has no UI Toolkit).
    /// Run: -executeMethod SkiViewCapture.TricksPhaseAPolishPipeline</summary>
    public static void TricksPhaseAPolishPipeline()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(Path.Combine(OutDir, "tricks"));
        string mock = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/ui/mockups"));
        void CopyRef(string src, string dst)
        {
            string a = Path.Combine(mock, src), b = Path.Combine(OutDir, "tricks", dst);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] ref " + dst); }
            else Debug.LogWarning("[SkiView] missing mockup " + a);
        }
        CopyRef("29a_title_tricks_focused_desktop.png", "b10.20_29a_title_focused_ref.png");
        CopyRef("29a_title_tricks_selected_desktop.png", "b10.20_29a_title_selected_ref.png");
        CopyRef("29b_tricks_setup_desktop.png", "b10.20_29b_setup_ref.png");
        CopyRef("29c_tricks_results_desktop.png", "b10.20_29c_results_ref.png");
        CopyRef("29d_trick_landed_desktop.png", "b10.20_29d_landed_ref.png");
        CopyRef("29d_trick_denied_desktop.png", "b10.20_29d_denied_ref.png");
        CopyRef("29a_title_tricks_focused_phone.png", "b10.20_29a_title_focused_phone_ref.png");
        CopyRef("29b_tricks_setup_phone.png", "b10.20_29b_setup_phone_ref.png");
        CopyRef("29c_tricks_results_phone.png", "b10.20_29c_results_phone_ref.png");
        CopyRef("29d_trick_landed_phone.png", "b10.20_29d_landed_phone_ref.png");
        CopyRef("29d_trick_denied_phone.png", "b10.20_29d_denied_phone_ref.png");

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        SkiViewGame.SaveTrickSetup(2, 0); // 31 km/h default · first rope step
        bool ap0 = game.autopilot; game.autopilot = false;
        game.Init();
        game.trickMode = true; game.SetTrickGear(true);
        game.BeginTrickRound();
        for (int i = 0; i < 60; i++) game.SimulateFor(1f / 60f);

        // title-ish dock / south scene with SKI VIEW wordmark composite
        Shot(cam, game, "tricks/b10.20_title_scene", () =>
        {
            Vector3 b = game.BoatPos; Vector3 f = game.BoatFwd;
            cam.transform.position = b - f * 22f + Vector3.up * 7f + Vector3.Cross(Vector3.up, f).normalized * 10f;
            cam.transform.LookAt(b + Vector3.up * 1.4f);
            cam.fieldOfView = 48f;
            game.ApplyVisuals(true);
        });

        // setup dock plate context
        Shot(cam, game, "tricks/b10.20_setup_scene", () =>
        {
            Vector3 b = game.BoatPos; Vector3 f = game.BoatFwd;
            cam.transform.position = b - f * 14f + Vector3.up * 5f + Vector3.Cross(Vector3.up, f).normalized * 6f;
            cam.transform.LookAt(b + Vector3.up * 1.0f);
            cam.fieldOfView = 50f;
            game.ApplyVisuals(true);
        });

        // on-water + force spin for landed toast scene
        int guard = 0;
        while (game.tphase != SkiViewGame.TPhase.Armed && game.tphase != SkiViewGame.TPhase.Active && guard++ < 8000)
            game.SimulateFor(1f / 60f);
        typeof(SkiViewGame).GetMethod("BeginSpin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(game, new object[] { 180f, 1f, false, false });
        for (int i = 0; i < 28; i++) game.SimulateFor(1f / 60f);
        Shot(cam, game, "tricks/b10.20_landed_scene", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
        });
        Shot(cam, game, "tricks/b10.20_denied_scene", () =>
        {
            game.camMode = SkiViewGame.CamMode.SkierChase;
            game.ApplyVisuals(true);
        });

        // Seed provisional chart entries (live Chart pts + TBD chip; do not invent)
        var logF = typeof(SkiViewGame).GetField("trickLog", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var scoreP = typeof(SkiViewGame).GetProperty("TrickScore");
        var passF = typeof(SkiViewGame).GetField("trickPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var phaseF = typeof(SkiViewGame).GetField("tphase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var list = logF?.GetValue(game) as System.Collections.IList;
        list?.Clear();
        var te = typeof(SkiViewGame).GetNestedType("TrickEntry");
        object Make(string code, string name, int pts, bool credited, string reason, int pass)
        {
            object e = System.Activator.CreateInstance(te);
            te.GetField("code").SetValue(e, code);
            te.GetField("name").SetValue(e, name);
            te.GetField("pts").SetValue(e, pts);
            te.GetField("credited").SetValue(e, credited);
            te.GetField("reason").SetValue(e, reason);
            te.GetField("reverse").SetValue(e, false);
            te.GetField("pass").SetValue(e, pass);
            return e;
        }
        // Chart bas: B=60, F=60, O=90, S=40 — provisional; TBD chip on HUD
        list?.Add(Make("B", "Surface 180 F→B", 60, true, "", 1));
        list?.Add(Make("F", "Surface 180 B→F", 60, true, "", 1));
        list?.Add(Make("O", "Surface 360 F→F", 0, false, "Rotation incomplete", 1));
        list?.Add(Make("B", "Surface 180 F→B", 60, true, "", 2));
        list?.Add(Make("S", "Side slide", 40, true, "", 2));
        list?.Add(Make("O", "Surface 360 F→F", 0, false, "Repeat", 2));
        int total = 60 + 60 + 60 + 40; // credited only
        // TrickScore may be set-only private — use backing field
        var scoreF = typeof(SkiViewGame).GetField("<TrickScore>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? typeof(SkiViewGame).GetField("trickScore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (scoreF != null) scoreF.SetValue(game, total);
        else scoreP?.SetValue(game, total);
        passF?.SetValue(game, 2);
        phaseF?.SetValue(game, SkiViewGame.TPhase.Results);
        game.mode = SkiViewGame.Mode.Turn;

        Shot(cam, game, "tricks/b10.20_results_scene", () =>
        {
            Vector3 s = game.SkierPos;
            cam.transform.position = s - game.BoatFwd * 16f + Vector3.up * 6.5f + Vector3.Cross(Vector3.up, game.BoatFwd).normalized * 3f;
            cam.transform.LookAt(s + Vector3.up * 1.2f);
            cam.fieldOfView = 52f;
            game.ApplyVisuals(true);
        });

        var lines = new System.Collections.Generic.List<string>();
        foreach (var line in game.TrickResultsSummary().Split('\n'))
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
        string setup = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0} km/h · {1:0.0} m · 2 × 20 s", game.trickBoatKmh, game.trickRopeM);
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        sb.AppendFormat("  \"total\": {0},\n", game.TrickScore);
        sb.Append("  \"setup\": ").Append(JsonString(setup)).Append(",\n");
        sb.Append("  \"status\": ").Append(JsonString(game.TrickRoundOver ? "ROUND OVER · FALL IN PASS 1" : "BOTH PASSES COMPLETE")).Append(",\n");
        sb.Append("  \"lines\": [");
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(JsonString(lines[i]));
        }
        sb.Append("]\n}\n");
        string metaPath = Path.Combine(OutDir, "tricks", "b10.20_results_meta.json");
        File.WriteAllText(metaPath, sb.ToString());

        string py = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/composite_tricks_results_hud.py"));
        void RunPy(string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                string so = proc.StandardOutput.ReadToEnd();
                string se = proc.StandardError.ReadToEnd();
                proc.WaitForExit(60000);
                Debug.Log("[SkiView] composite exit=" + proc.ExitCode + " out=" + so + " err=" + se);
            }
            catch (System.Exception e) { Debug.LogWarning("[SkiView] composite failed: " + e.Message); }
        }
        string dir = Path.Combine(OutDir, "tricks");
        RunPy("\"" + py + "\" wordmark \"" + Path.Combine(dir, "b10.20_title_scene.png") + "\" \"" + Path.Combine(dir, "b10.20_title_hud.png") + "\"");
        RunPy("\"" + py + "\" results \"" + Path.Combine(dir, "b10.20_results_scene.png") + "\" \"" + Path.Combine(dir, "b10.20_results_hud.png") + "\" \"" + metaPath + "\"");
        RunPy("\"" + py + "\" landed \"" + Path.Combine(dir, "b10.20_landed_scene.png") + "\" \"" + Path.Combine(dir, "b10.20_landed_hud.png") + "\"");
        RunPy("\"" + py + "\" denied \"" + Path.Combine(dir, "b10.20_denied_scene.png") + "\" \"" + Path.Combine(dir, "b10.20_denied_hud.png") + "\"");

        game.autopilot = ap0;
        Debug.Log("[SkiView] b10.20 Tricks Phase A polish -> Screens/tricks/ score=" + game.TrickScore + " resultsOpen=" + game.TrickResultsOpen);
    }



    /// <summary>b10.26 T17b/T29b Tricks scoring wire: landed toast +{pts}, results real ints, no TBD.
    /// Writes Screens/tricks/b10.26_*.png + PIL HUD composites. Run: SkiViewCapture.TricksScoringWirePipeline</summary>
    public static void TricksScoringWirePipeline()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(Path.Combine(OutDir, "tricks"));
        string mock = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/ui/mockups"));
        void CopyRef(string src, string dst)
        {
            string a = Path.Combine(mock, src), b = Path.Combine(OutDir, "tricks", dst);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] ref " + dst); }
            else Debug.LogWarning("[SkiView] missing mockup " + a);
        }
        CopyRef("29b_tricks_setup_desktop.png", "b10.26_29b_setup_ref.png");
        CopyRef("29c_tricks_results_desktop.png", "b10.26_29c_results_ref.png");
        CopyRef("29d_trick_landed_desktop.png", "b10.26_29d_landed_ref.png");
        CopyRef("29d_trick_denied_desktop.png", "b10.26_29d_denied_ref.png");
        CopyRef("29b_tricks_setup_phone.png", "b10.26_29b_setup_phone_ref.png");
        CopyRef("29c_tricks_results_phone.png", "b10.26_29c_results_phone_ref.png");
        CopyRef("29d_trick_landed_phone.png", "b10.26_29d_landed_phone_ref.png");
        CopyRef("29d_trick_denied_phone.png", "b10.26_29d_denied_phone_ref.png");

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        SkiViewGame.SaveTrickSetup(2, 0);
        bool ap0 = game.autopilot; game.autopilot = false;
        game.Init();
        game.trickMode = true; game.SetTrickGear(true);
        game.BeginTrickRound();
        for (int i = 0; i < 60; i++) game.SimulateFor(1f / 60f);

        Shot(cam, game, "tricks/b10.26_setup_scene", () =>
        {
            Vector3 b = game.BoatPos; Vector3 f = game.BoatFwd;
            cam.transform.position = b - f * 14f + Vector3.up * 5f + Vector3.Cross(Vector3.up, f).normalized * 6f;
            cam.transform.LookAt(b + Vector3.up * 1.0f);
            cam.fieldOfView = 50f;
            game.ApplyVisuals(true);
        });

        int guard = 0;
        while (game.tphase != SkiViewGame.TPhase.Armed && game.tphase != SkiViewGame.TPhase.Active && guard++ < 8000)
            game.SimulateFor(1f / 60f);
        typeof(SkiViewGame).GetMethod("BeginSpin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(game, new object[] { 180f, 1f, false, false });
        for (int i = 0; i < 28; i++) game.SimulateFor(1f / 60f);
        Shot(cam, game, "tricks/b10.26_landed_scene", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
        });
        Shot(cam, game, "tricks/b10.26_denied_scene", () =>
        {
            game.camMode = SkiViewGame.CamMode.SkierChase;
            game.ApplyVisuals(true);
        });

        // T17b SAMPLE (Julia T29b): Pass1 540 · Pass2 270 · Total 810 — Chart ints only
        var logF = typeof(SkiViewGame).GetField("trickLog", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var passF = typeof(SkiViewGame).GetField("trickPass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var phaseF = typeof(SkiViewGame).GetField("tphase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var list = logF?.GetValue(game) as System.Collections.IList;
        list?.Clear();
        var te = typeof(SkiViewGame).GetNestedType("TrickEntry");
        object Make(string code, string name, int pts, bool credited, string reason, int pass)
        {
            object e = System.Activator.CreateInstance(te);
            te.GetField("code").SetValue(e, code);
            te.GetField("name").SetValue(e, name);
            te.GetField("pts").SetValue(e, pts);
            te.GetField("credited").SetValue(e, credited);
            te.GetField("reason").SetValue(e, reason);
            te.GetField("reverse").SetValue(e, code.StartsWith("R") && code.Length > 1);
            te.GetField("pass").SetValue(e, pass);
            return e;
        }
        list?.Add(Make("S", "Side slide", 40, true, "", 1));
        list?.Add(Make("RS", "Side slide", 40, true, "", 1));
        list?.Add(Make("B", "Surface 180 F→B", 60, true, "", 1));
        list?.Add(Make("RB", "Surface 180 F→B", 60, true, "", 1));
        list?.Add(Make("O", "Surface 360 F→F", 90, true, "", 1));
        list?.Add(Make("RO", "Surface 360 F→F", 90, true, "", 1));
        list?.Add(Make("WB", "Wake 180 F→B", 80, true, "", 1));
        list?.Add(Make("WF", "Wake 180 B→F", 80, true, "", 1));
        list?.Add(Make("F", "Surface 180 B→F", 60, true, "", 2));
        list?.Add(Make("RF", "Surface 180 B→F", 60, true, "", 2));
        list?.Add(Make("BB", "Surface 360 B→B", 90, true, "", 2));
        list?.Add(Make("RBB", "Surface 360 B→B", 0, false, "Rotation incomplete", 2));
        list?.Add(Make("S", "Side slide", 0, false, "Repeat", 2));
        list?.Add(Make("B", "Surface 180 F→B", 60, true, "", 2));
        list?.Add(Make("WB", "Wake 180 F→B", 0, false, "Repeat", 2));
        list?.Add(Make("WF", "Wake 180 B→F", 0, false, "Repeat", 2));
        int total = 810;
        var scoreF = typeof(SkiViewGame).GetField("<TrickScore>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? typeof(SkiViewGame).GetField("trickScore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (scoreF != null) scoreF.SetValue(game, total);
        passF?.SetValue(game, 2);
        phaseF?.SetValue(game, SkiViewGame.TPhase.Results);
        game.mode = SkiViewGame.Mode.Turn;

        Shot(cam, game, "tricks/b10.26_results_scene", () =>
        {
            Vector3 s = game.SkierPos;
            cam.transform.position = s - game.BoatFwd * 16f + Vector3.up * 6.5f + Vector3.Cross(Vector3.up, game.BoatFwd).normalized * 3f;
            cam.transform.LookAt(s + Vector3.up * 1.2f);
            cam.fieldOfView = 52f;
            game.ApplyVisuals(true);
        });

        var lines = new System.Collections.Generic.List<string>();
        foreach (var line in game.TrickResultsSummary().Split('\n'))
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
        string setup = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0} km/h · {1:0.0} m · 2 × 20 s", game.trickBoatKmh, game.trickRopeM);
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        sb.AppendFormat("  \"total\": {0},\n", game.TrickScore);
        sb.Append("  \"passLine\": ").Append(JsonString("Pass 1: 540 · Pass 2: 270 · Total 810")).Append(",\n");
        sb.Append("  \"setup\": ").Append(JsonString(setup)).Append(",\n");
        sb.Append("  \"status\": ").Append(JsonString(game.TrickRoundOver ? "ROUND OVER · FALL IN PASS 1" : "BOTH PASSES COMPLETE")).Append(",\n");
        sb.Append("  \"lines\": [");
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(JsonString(lines[i]));
        }
        sb.Append("]\n}\n");
        string metaPath = Path.Combine(OutDir, "tricks", "b10.26_results_meta.json");
        File.WriteAllText(metaPath, sb.ToString());

        string py = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/composite_tricks_results_hud.py"));
        void RunPy(string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                string so = proc.StandardOutput.ReadToEnd();
                string se = proc.StandardError.ReadToEnd();
                proc.WaitForExit(60000);
                Debug.Log("[SkiView] composite exit=" + proc.ExitCode + " out=" + so + " err=" + se);
            }
            catch (System.Exception e) { Debug.LogWarning("[SkiView] composite failed: " + e.Message); }
        }
        string dir = Path.Combine(OutDir, "tricks");
        RunPy("\"" + py + "\" results \"" + Path.Combine(dir, "b10.26_results_scene.png") + "\" \"" + Path.Combine(dir, "b10.26_results_hud.png") + "\" \"" + metaPath + "\"");
        RunPy("\"" + py + "\" landed \"" + Path.Combine(dir, "b10.26_landed_scene.png") + "\" \"" + Path.Combine(dir, "b10.26_landed_hud.png") + "\" \"✓ Landed +80\"");
        RunPy("\"" + py + "\" denied \"" + Path.Combine(dir, "b10.26_denied_scene.png") + "\" \"" + Path.Combine(dir, "b10.26_denied_hud.png") + "\" \"✕ Denied · Rotation incomplete\"");

        // also write notes
        File.WriteAllText(Path.Combine(dir, "b10.26_notes.md"),
            "# b10.26 Tricks scoring wire (T17b + T29b)\n"
            + "- Toast: `✓ Landed +{pts}` (SAMPLE WB +80)\n"
            + "- Results: Chart ints S40 B/F60 O/BB90 WB/WF80; SAMPLE 540+270=810\n"
            + "- Denied/Repeat show 0 + reason; no TBD chips; skip _big chime\n");

        game.autopilot = ap0;
        Debug.Log("[SkiView] b10.26 Tricks scoring wire -> Screens/tricks/ score=" + game.TrickScore + " resultsOpen=" + game.TrickResultsOpen + " toast=✓ Landed +pts");
    }



    /// <summary>b10.23 far tree-line fleck soften: before = b10.22 after; tint bark/fir/cedar/deciduous/riprap (PBR tint only).
    /// Run: -executeMethod SkiViewCapture.ShorePolishPipeline</summary>
    public static void ShorePolishPipeline()
    {
        AssetDatabase.Refresh();
        string polish = Path.Combine(OutDir, "polish");
        string ground = Path.Combine(OutDir, "diag", "ground");
        Directory.CreateDirectory(polish);

        void CopyBefore(string srcName, string dstName)
        {
            string a = Path.Combine(polish, srcName);
            if (!File.Exists(a)) a = Path.Combine(ground, srcName);
            string b = Path.Combine(polish, dstName);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] before " + dstName); }
            else Debug.LogWarning("[SkiView] missing before src " + a);
        }
        CopyBefore("b10.22_after_island1_low.png", "b10.23_before_island1_low.png");
        CopyBefore("b10.22_after_island1_aerial.png", "b10.23_before_island1_aerial.png");
        CopyBefore("b10.22_after_island2_low.png", "b10.23_before_island2_low.png");
        CopyBefore("b10.22_after_island2_aerial.png", "b10.23_before_island2_aerial.png");
        CopyBefore("b10.22_after_phone_island1_low.png", "b10.23_before_phone_island1_low.png");
        CopyBefore("b10.22_after_close.png", "b10.23_before_close.png");

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        SkiViewTerrainFix.Apply();
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.42f, 0.40f);

        void Tint(string name, Color c, float smooth)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m == null || m.name != name) continue;
                m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                m.SetFloat("_Smoothness", smooth);
                m.DisableKeyword("_ENVIRONMENTREFLECTIONS");
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                EditorUtility.SetDirty(m);
            }
        }
        // b10.23: far tree-line pale rock/trunk flecks — darken bark + impostors + riprap (no baked lighting)
        Tint("Bark", new Color(0.52f, 0.46f, 0.40f), 0.06f);
        Tint("FirSpray", new Color(0.52f, 0.60f, 0.50f), 0.10f);
        Tint("FirImpostor", new Color(0.55f, 0.62f, 0.52f), 0.06f);
        Tint("CedarSpray", new Color(0.52f, 0.58f, 0.48f), 0.10f);
        Tint("CedarImpostor", new Color(0.54f, 0.60f, 0.50f), 0.06f);
        Tint("AlderLeaves", new Color(0.72f, 0.78f, 0.70f), 0.12f);
        Tint("MapleLeaves", new Color(0.74f, 0.78f, 0.68f), 0.12f);
        Tint("AlderImpostor", new Color(0.72f, 0.78f, 0.70f), 0.06f);
        Tint("MapleImpostor", new Color(0.74f, 0.78f, 0.68f), 0.06f);
        Tint("CanopyImpostor", new Color(0.70f, 0.76f, 0.70f), 0.06f);
        Tint("RipRap", new Color(0.32f, 0.28f, 0.25f), 0.05f);
        Tint("ShoreRock", new Color(0.34f, 0.26f, 0.18f), 0.05f);
        AssetDatabase.SaveAssets();

        System.Environment.SetEnvironmentVariable("SV_TAG", "b10.23_after");
        DiagGround();

        void CopyAfter(string srcName, string dstName)
        {
            string a = Path.Combine(ground, srcName), b = Path.Combine(polish, dstName);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] after " + dstName); }
        }
        CopyAfter("b10.23_after_desktop_island1_low.png", "b10.23_after_island1_low.png");
        CopyAfter("b10.23_after_desktop_island1_aerial.png", "b10.23_after_island1_aerial.png");
        CopyAfter("b10.23_after_desktop_island2_low.png", "b10.23_after_island2_low.png");
        CopyAfter("b10.23_after_desktop_island2_aerial.png", "b10.23_after_island2_aerial.png");
        CopyAfter("b10.23_after_phone_island1_low.png", "b10.23_after_phone_island1_low.png");
        CopyAfter("b10.23_after_desktop_close.png", "b10.23_after_close.png");

        string doc = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/polish/b10.23-treeline-flecks.md"));
        if (File.Exists(doc)) File.Copy(doc, Path.Combine(polish, "b10.23-treeline-flecks.md"), true);

        Debug.Log("[SkiView] b10.23 treeline flecks -> Screens/polish/");
    }



    /// <summary>b10.27 T24 Finish Page leftover UI: slalom TITLE, rope-choice Esc, T28 picker hits, HUD 144 hits.
    /// Writes Screens/ui/b10.27_*.png (+ mock refs). Run: SkiViewCapture.T24LeftoverUiPipeline</summary>
    public static void T24LeftoverUiPipeline()
    {
        AssetDatabase.Refresh();
        string dir = Path.Combine(OutDir, "ui");
        Directory.CreateDirectory(dir);
        string mock = Path.GetFullPath(Path.Combine(Application.dataPath, "../../skiview-assets-staging/ui/mockups"));
        void CopyRef(string src, string dst)
        {
            string a = Path.Combine(mock, src), b = Path.Combine(dir, dst);
            if (File.Exists(a)) { File.Copy(a, b, true); Debug.Log("[SkiView] ref " + dst); }
            else Debug.LogWarning("[SkiView] missing mockup " + a);
        }
        CopyRef("28_ski_picker_desktop.png", "b10.27_28_picker_desk_ref.png");
        CopyRef("28_ski_picker_phone.png", "b10.27_28_picker_phone_ref.png");
        CopyRef("28b_ski_picker_doubleboot_desktop.png", "b10.27_28b_db_desk_ref.png");
        CopyRef("28b_ski_picker_doubleboot_phone.png", "b10.27_28b_db_phone_ref.png");

        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        bool ap0 = game.autopilot; game.autopilot = false;
        game.Init();
        for (int i = 0; i < 90; i++) game.SimulateFor(1f / 60f);

        // HUD / safe-area chase plate (CAM+Pause live under .sv-safe; hits via .sv-hit--*)
        Shot(cam, game, "ui/b10.27_hud_chase", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
        });
        Shot(cam, game, "ui/b10.27_hud_broadcast", () =>
        {
            game.camMode = SkiViewGame.CamMode.Broadcast;
            game.ApplyVisuals(true);
        });

        // Prove ski_*_db Resources exist for all six colourways (D3)
        var missing = new System.Collections.Generic.List<string>();
        for (int i = 0; i < 6; i++)
        {
            if (Resources.Load<Texture2D>("SkiViewUI/Skis/ski_" + i + "_db") == null) missing.Add("ski_" + i + "_db");
            if (Resources.Load<Texture2D>("SkiViewUI/Skis/ski_" + i) == null) missing.Add("ski_" + i);
            if (Resources.Load<Texture2D>("SkiViewUI/Skis/thumb_" + i) == null) missing.Add("thumb_" + i);
        }

        string py = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/composite_t24_ui.py"));
        void RunPy(string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                string so = proc.StandardOutput.ReadToEnd();
                string se = proc.StandardError.ReadToEnd();
                proc.WaitForExit(60000);
                Debug.Log("[SkiView] t24 composite exit=" + proc.ExitCode + " out=" + so + " err=" + se);
            }
            catch (System.Exception e) { Debug.LogWarning("[SkiView] t24 composite failed: " + e.Message); }
        }
        RunPy("\"" + py + "\" hud \"" + Path.Combine(dir, "b10.27_hud_chase.png") + "\" \"" + Path.Combine(dir, "b10.27_hud_safe_overlay.png") + "\"");
        RunPy("\"" + py + "\" results \"" + Path.Combine(dir, "b10.27_hud_chase.png") + "\" \"" + Path.Combine(dir, "b10.27_slalom_results_title.png") + "\"");
        RunPy("\"" + py + "\" picker \"" + Path.Combine(dir, "b10.27_28_picker_phone_ref.png") + "\" \"" + Path.Combine(dir, "b10.27_picker_phone_annotated.png") + "\"");

        File.WriteAllText(Path.Combine(dir, "b10.27_notes.md"),
            "# b10.27 T24 Finish Page leftover UI\n"
            + "- Gap A: slalom results **TITLE** → ToTitle (Jump/Tricks hide duplicate; rsAlt stays RETRY on slalom)\n"
            + "- Gap B: RopeChoice Esc → CancelRopeChoice → results plate; NEXT PASS ChooseRope(SAME) if pending\n"
            + "- D1: skipick rows 96 / phone 100; CHOOSE LAKE+START sv-btn--start-tall (phone 144)\n"
            + "- D2: focus ring on .sv-skipick__it:focus; selected = --on blue+Equipped\n"
            + "- D3: ski_0..5 + _db + thumbs " + (missing.Count == 0 ? "ALL PRESENT" : ("MISSING " + string.Join(",", missing))) + "\n"
            + "- D4: Jump/Tricks Run Setup already skip SkiPicker (locked cards)\n"
            + "- D5: Equipped badge follows pending suSki\n"
            + "- HUD: SafeAreaRoot 88/24 phone · 48 desktop; .sv-hit--cam / --pause 144; .sv-phone class toggled\n"
            + "- Hold: lakes, store/trailer, Wade invent, Lot 17 import\n");

        game.autopilot = ap0;
        Debug.Log("[SkiView] b10.27 T24 leftover UI -> Screens/ui/ missingDb=" + missing.Count);
    }


    /// <summary>b10.28 reshoot lake-facing plates (no import/BuildAll). Scene must already have Lot 17.
    /// Run: unity_run.sh SkiViewCapture.Lot17CaptureOnly</summary>
    public static void Lot17CaptureOnly()
    {
        Directory.CreateDirectory(Path.Combine(OutDir, "houses"));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        game.SimulateFor(2f);

        Transform lot = null;
        foreach (var xf in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (xf.name.StartsWith("Lot_8419")) { lot = xf; break; }
        Vector3 intoW = lot != null ? lot.forward : new Vector3(0.992f, 0f, -0.126f).normalized;
        intoW.y = 0f; intoW.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, intoW).normalized;
        Vector3 pivot = lot != null ? lot.position : new Vector3(49.8f, 0.45f, -279.1f);
        Vector3 shore = pivot + intoW * 37.74f; shore.y = 0f;
        Bounds b = new Bounds(pivot + intoW * 18f + Vector3.up * 4f, new Vector3(30f, 12f, 30f));
        if (lot != null)
        {
            bool any = false;
            foreach (var rnd in lot.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = rnd.bounds; any = true; }
                else b.Encapsulate(rnd.bounds);
            }
        }
        Vector3 look = b.center;
        Debug.Log($"[SkiView] b10.28 CaptureOnly Lot_8419 found={(lot != null)} name={(lot != null ? lot.name : "null")} pivot={pivot:F2} shore={shore:F2} intoW={intoW:F3} boundsC={b.center:F2}");

        Shot(cam, game, "houses/b10.28_lake_face", () =>
        {
            cam.fieldOfView = 40f; cam.farClipPlane = 2000f;
            cam.transform.position = look + intoW * 38f + Vector3.up * 6f;
            cam.transform.LookAt(look + Vector3.up * 1.2f);
        });
        Shot(cam, game, "houses/b10.28_lake_oblique", () =>
        {
            cam.fieldOfView = 38f;
            cam.transform.position = look + intoW * 32f + right * 24f + Vector3.up * 11f;
            cam.transform.LookAt(look + Vector3.up * 0.5f);
        });
        Shot(cam, game, "houses/b10.28_dock", () =>
        {
            cam.fieldOfView = 42f;
            Vector3 dockTip = shore + intoW * 8f;
            cam.transform.position = dockTip + intoW * 10f + right * -5f + Vector3.up * 3.5f;
            cam.transform.LookAt(Vector3.Lerp(look, dockTip, 0.65f) + Vector3.up * 2f);
        });
        Shot(cam, game, "houses/b10.28_chase_pass", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
            cam.fieldOfView = 45f;
            cam.transform.position = look + intoW * 48f + right * 10f + Vector3.up * 4f;
            cam.transform.LookAt(look + Vector3.up * 1f);
        });
        Shot(cam, game, "houses/b10.28_aerial_south", () =>
        {
            cam.fieldOfView = 50f; cam.farClipPlane = 3000f;
            cam.transform.position = look + Vector3.up * 140f - intoW * 10f;
            cam.transform.rotation = Quaternion.Euler(89.5f, 0f, 0f);
        });
        Debug.Log("[SkiView] b10.28 Lot17CaptureOnly -> Screens/houses/b10.28_*");
    }

    /// <summary>b10.28: Scott-cleared Lot 17 (lot-8419) polish hot-swap — ForceUpdate FBX+textures,
    /// bind URP MetallicSmoothness on body, BuildAll, lake-facing Screens/houses/b10.28_*.
    /// Run: unity_run.sh SkiViewCapture.Lot17PolishPipeline</summary>
    public static void Lot17PolishPipeline()
    {
        const string F = "Assets/SkiView/Houses/lot-8419.fbx";
        const string T = "Assets/SkiView/Houses/textures/";
        AssetDatabase.Refresh();
        foreach (var texName in new[] {
            "hv2_lot17_albedo.jpg", "hv2_lot17_normal.jpg", "hv2_lot17_glass.jpg",
            "hv2_lot17_impostor.jpg", "hv2_lot17_metallicSmoothness.jpg" })
        {
            var ti = AssetImporter.GetAtPath(T + texName) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[SkiView] b10.28 missing tex " + texName); continue; }
            bool isN = texName.Contains("normal"), isLin = texName.Contains("metallic") || texName.Contains("Smoothness") || texName.Contains("roughness");
            bool want = (isN && ti.textureType != TextureImporterType.NormalMap) || (isLin && ti.sRGBTexture) || ti.maxTextureSize < 2048;
            if (want)
            {
                if (isN) ti.textureType = TextureImporterType.NormalMap;
                if (isLin) ti.sRGBTexture = false;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            else AssetDatabase.ImportAsset(T + texName, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.ImportAsset(F, ImportAssetOptions.ForceUpdate);

        // Confirm mesh + bind body MS before BuildAll (HouseMat also wires MS when present)
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(F);
        int lod0Tris = 0;
        if (go != null)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.name.EndsWith("_LOD0") || mf.sharedMesh.name.Contains("LOD0"))
                    lod0Tris = mf.sharedMesh.triangles.Length / 3;
            }
        }
        var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/SkiView/Houses/Materials/hv2_lot17_body.mat");
        var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(T + "hv2_lot17_metallicSmoothness.jpg");
        var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(T + "hv2_lot17_albedo.jpg");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(T + "hv2_lot17_normal.jpg");
        if (body != null)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null) body.shader = lit;
            if (alb != null) { body.SetTexture("_BaseMap", alb); body.SetColor("_BaseColor", Color.white); }
            if (nrm != null) { body.SetTexture("_BumpMap", nrm); body.EnableKeyword("_NORMALMAP"); body.SetFloat("_BumpScale", 1f); }
            if (ms != null)
            {
                body.SetTexture("_MetallicGlossMap", ms);
                body.EnableKeyword("_METALLICSPECGLOSSMAP");
                body.SetFloat("_Smoothness", 1f); body.SetFloat("_Metallic", 1f);
            }
            EditorUtility.SetDirty(body);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SkiView] b10.28 Lot17 assets ready lod0Tris={lod0Tris} ms={(ms != null)} body={(body != null)}");

        SkiViewBuilder.BuildAll();

        string dir = Path.Combine(OutDir, "houses");
        Directory.CreateDirectory(dir);
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        game.SimulateFor(2f);

        Transform lot = null;
        foreach (var xf in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (xf.name.StartsWith("Lot_8419")) { lot = xf; break; }
        }
        // Placement: wrap.rotation = LookRotation(into-water shore normal); shoreline ≈ pivot + forward * shoreZ
        Vector3 intoW = lot != null ? lot.forward : new Vector3(0.992f, 0f, -0.126f).normalized;
        intoW.y = 0f; intoW.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, intoW).normalized;
        Vector3 pivot = lot != null ? lot.position : new Vector3(49.8f, 0.45f, -279.1f);
        float shoreZ = 37.74f;
        Vector3 shore = pivot + intoW * shoreZ; shore.y = 0f;
        Bounds b = new Bounds(pivot + intoW * 18f + Vector3.up * 4f, new Vector3(30f, 12f, 30f));
        if (lot != null)
        {
            bool any = false;
            foreach (var rnd in lot.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = rnd.bounds; any = true; }
                else b.Encapsulate(rnd.bounds);
            }
        }
        Vector3 look = b.center;
        Debug.Log($"[SkiView] b10.28 Lot_8419 found={(lot != null)} name={(lot != null ? lot.name : "null")} pivot={pivot:F2} shore={shore:F2} intoW={intoW:F3} boundsC={b.center:F2} ext={b.extents:F2}");

        Shot(cam, game, "houses/b10.28_lake_face", () =>
        {
            // From open water looking back at the lake face (slate-blue + dock)
            cam.fieldOfView = 40f; cam.farClipPlane = 2000f;
            cam.transform.position = look + intoW * 38f + Vector3.up * 6f;
            cam.transform.LookAt(look + Vector3.up * 1.2f);
        });
        Shot(cam, game, "houses/b10.28_lake_oblique", () =>
        {
            cam.fieldOfView = 38f;
            cam.transform.position = look + intoW * 32f + right * 24f + Vector3.up * 11f;
            cam.transform.LookAt(look + Vector3.up * 0.5f);
        });
        Shot(cam, game, "houses/b10.28_dock", () =>
        {
            // Dock tip ≈ shore + intoW * small; stay on water side
            cam.fieldOfView = 42f;
            Vector3 dockTip = shore + intoW * 8f;
            cam.transform.position = dockTip + intoW * 10f + right * -5f + Vector3.up * 3.5f;
            cam.transform.LookAt(Vector3.Lerp(look, dockTip, 0.65f) + Vector3.up * 2f);
        });
        Shot(cam, game, "houses/b10.28_chase_pass", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
            cam.fieldOfView = 45f;
            cam.transform.position = look + intoW * 48f + right * 10f + Vector3.up * 4f;
            cam.transform.LookAt(look + Vector3.up * 1f);
        });
        Shot(cam, game, "houses/b10.28_aerial_south", () =>
        {
            cam.fieldOfView = 50f; cam.farClipPlane = 3000f;
            cam.transform.position = look + Vector3.up * 140f - intoW * 10f;
            cam.transform.rotation = Quaternion.Euler(89.5f, 0f, 0f);
        });

        File.WriteAllText(Path.Combine(dir, "b10.28_notes.md"),
            "# b10.28 Lot 17 (lot-8419) polish hot-swap\n"
            + $"- FBX md5 c40573117e9d3f5615780d2f9351b27c (Scott-cleared t464u)\n"
            + $"- LOD0 tris≈{lod0Tris} (budget ≤22k); shoreZ 37.74 drop-in; pivot/axes preserved\n"
            + $"- URP Lit body: albedo+normal+metallicSmoothness (Metallic/Roughness Unreal archive only in staging)\n"
            + $"- Lot found={(lot != null)} name={(lot != null ? lot.name : "?")}\n"
            + "- Hold: other lakes, store/trailer, Wade invent, Lot 18\n");

        Debug.Log("[SkiView] b10.28 Lot17 polish pipeline -> Screens/houses/b10.28_* lod0Tris=" + lod0Tris);
    }

    /// <summary>Generic house polish hot-swap (ForceUpdate FBX+textures, bind URP MS, BuildAll, lake Screens).</summary>
    static void HousePolishPipeline(string buildTag, string fbxRel, string texPrefix, string bodyMatRel,
        string lotNamePrefix, float shoreZ, string fbxMd5Note, string holdNote)
    {
        const string T = "Assets/SkiView/Houses/textures/";
        string F = fbxRel;
        AssetDatabase.Refresh();
        foreach (var texName in new[] {
            texPrefix + "albedo.jpg", texPrefix + "normal.jpg", texPrefix + "glass.jpg",
            texPrefix + "impostor.jpg", texPrefix + "metallicSmoothness.jpg" })
        {
            var ti = AssetImporter.GetAtPath(T + texName) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[SkiView] " + buildTag + " missing tex " + texName); continue; }
            bool isN = texName.Contains("normal"), isLin = texName.Contains("metallic") || texName.Contains("Smoothness") || texName.Contains("roughness");
            bool want = (isN && ti.textureType != TextureImporterType.NormalMap) || (isLin && ti.sRGBTexture) || ti.maxTextureSize < 2048;
            if (want)
            {
                if (isN) ti.textureType = TextureImporterType.NormalMap;
                if (isLin) ti.sRGBTexture = false;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            else AssetDatabase.ImportAsset(T + texName, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.ImportAsset(F, ImportAssetOptions.ForceUpdate);

        var go = AssetDatabase.LoadAssetAtPath<GameObject>(F);
        int lod0Tris = 0;
        if (go != null)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.name.EndsWith("_LOD0") || mf.sharedMesh.name.Contains("LOD0"))
                    lod0Tris = mf.sharedMesh.triangles.Length / 3;
            }
        }
        var body = AssetDatabase.LoadAssetAtPath<Material>(bodyMatRel);
        var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(T + texPrefix + "metallicSmoothness.jpg");
        var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(T + texPrefix + "albedo.jpg");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(T + texPrefix + "normal.jpg");
        if (body != null)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null) body.shader = lit;
            if (alb != null) { body.SetTexture("_BaseMap", alb); body.SetColor("_BaseColor", Color.white); }
            if (nrm != null) { body.SetTexture("_BumpMap", nrm); body.EnableKeyword("_NORMALMAP"); body.SetFloat("_BumpScale", 1f); }
            if (ms != null)
            {
                body.SetTexture("_MetallicGlossMap", ms);
                body.EnableKeyword("_METALLICSPECGLOSSMAP");
                body.SetFloat("_Smoothness", 1f); body.SetFloat("_Metallic", 1f);
            }
            EditorUtility.SetDirty(body);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SkiView] {buildTag} assets ready lod0Tris={lod0Tris} ms={(ms != null)} body={(body != null)}");

        SkiViewBuilder.BuildAll();

        string dir = Path.Combine(OutDir, "houses");
        Directory.CreateDirectory(dir);
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        var cam = game.cam;
        game.autopilot = true; game.Init();
        game.SimulateFor(2f);

        Transform lot = null;
        foreach (var xf in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (xf.name.StartsWith(lotNamePrefix)) { lot = xf; break; }
        }
        Vector3 intoW = lot != null ? lot.forward : new Vector3(0.992f, 0f, -0.126f).normalized;
        intoW.y = 0f; intoW.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, intoW).normalized;
        Vector3 pivot = lot != null ? lot.position : Vector3.zero;
        Vector3 shore = pivot + intoW * shoreZ; shore.y = 0f;
        Bounds b = new Bounds(pivot + intoW * 18f + Vector3.up * 4f, new Vector3(30f, 12f, 30f));
        if (lot != null)
        {
            bool any = false;
            foreach (var rnd in lot.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = rnd.bounds; any = true; }
                else b.Encapsulate(rnd.bounds);
            }
        }
        Vector3 look = b.center;
        Debug.Log($"[SkiView] {buildTag} {lotNamePrefix} found={(lot != null)} name={(lot != null ? lot.name : "null")} pivot={pivot:F2} shore={shore:F2} intoW={intoW:F3} boundsC={b.center:F2} ext={b.extents:F2}");

        Shot(cam, game, $"houses/{buildTag}_lake_face", () =>
        {
            cam.fieldOfView = 40f; cam.farClipPlane = 2000f;
            cam.transform.position = look + intoW * 38f + Vector3.up * 6f;
            cam.transform.LookAt(look + Vector3.up * 1.2f);
        });
        Shot(cam, game, $"houses/{buildTag}_lake_oblique", () =>
        {
            cam.fieldOfView = 38f;
            cam.transform.position = look + intoW * 32f + right * 24f + Vector3.up * 11f;
            cam.transform.LookAt(look + Vector3.up * 0.5f);
        });
        Shot(cam, game, $"houses/{buildTag}_dock", () =>
        {
            cam.fieldOfView = 42f;
            Vector3 dockTip = shore + intoW * 8f;
            cam.transform.position = dockTip + intoW * 10f + right * -5f + Vector3.up * 3.5f;
            cam.transform.LookAt(Vector3.Lerp(look, dockTip, 0.65f) + Vector3.up * 2f);
        });
        Shot(cam, game, $"houses/{buildTag}_chase_pass", () =>
        {
            game.camMode = SkiViewGame.CamMode.CloseChase;
            game.ApplyVisuals(true);
            cam.fieldOfView = 45f;
            cam.transform.position = look + intoW * 48f + right * 10f + Vector3.up * 4f;
            cam.transform.LookAt(look + Vector3.up * 1f);
        });
        Shot(cam, game, $"houses/{buildTag}_aerial_south", () =>
        {
            cam.fieldOfView = 50f; cam.farClipPlane = 3000f;
            cam.transform.position = look + Vector3.up * 140f - intoW * 10f;
            cam.transform.rotation = Quaternion.Euler(89.5f, 0f, 0f);
        });

        File.WriteAllText(Path.Combine(dir, buildTag + "_notes.md"),
            $"# {buildTag} polish hot-swap\n"
            + $"- FBX md5 {fbxMd5Note} (Scott-cleared t476u)\n"
            + $"- LOD0 tris≈{lod0Tris}; shoreZ {shoreZ:F2} drop-in; pivot/axes preserved\n"
            + $"- URP Lit body: albedo+normal+metallicSmoothness (Metallic/Roughness Unreal archive only in staging)\n"
            + $"- Lot found={(lot != null)} name={(lot != null ? lot.name : "?")}\n"
            + $"- Hold: {holdNote}\n");

        Debug.Log($"[SkiView] {buildTag} polish pipeline -> Screens/houses/{buildTag}_* lod0Tris=" + lod0Tris);
    }

    /// <summary>b10.29: Scott-cleared Lot 18 (lot-8505) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot18PolishPipeline</summary>
    public static void Lot18PolishPipeline()
    {
        HousePolishPipeline("b10.29", "Assets/SkiView/Houses/lot-8505.fbx", "hv2_lot18_",
            "Assets/SkiView/Houses/Materials/hv2_lot18_body.mat", "Lot_8505", 36.13f,
            "f5b59a43b141b391f1f11fe77faacddb", "other lakes, store/trailer, Wade invent, Lots 9/10/12/14/16");
    }

    /// <summary>b10.30: Scott-cleared Lot 16 (lot-8415) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot16PolishPipeline</summary>
    public static void Lot16PolishPipeline()
    {
        HousePolishPipeline("b10.30", "Assets/SkiView/Houses/lot-8415.fbx", "hv2_lot16_",
            "Assets/SkiView/Houses/Materials/hv2_lot16_body.mat", "Lot_8415", 43.78f,
            "16a83e9d865aa6711383271280166f49", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.31: Scott-cleared Lot 10 (lot-8229) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot10PolishPipeline</summary>
    public static void Lot10PolishPipeline()
    {
        HousePolishPipeline("b10.31", "Assets/SkiView/Houses/lot-8229.fbx", "hv2_lot10_",
            "Assets/SkiView/Houses/Materials/hv2_lot10_body.mat", "Lot_8229", 36.36f,
            "e60069454dfe18f6601b1c8cf62cff53", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.32: Scott-cleared Lot 12 (lot-8311) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot12PolishPipeline</summary>
    public static void Lot12PolishPipeline()
    {
        HousePolishPipeline("b10.32", "Assets/SkiView/Houses/lot-8311.fbx", "hv2_lot12_",
            "Assets/SkiView/Houses/Materials/hv2_lot12_body.mat", "Lot_8311", 36.20f,
            "b22355b5410ad8199efa2eb8b13c83e3", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.33: Scott-cleared Lot 9 (lot-8221) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot09PolishPipeline</summary>
    public static void Lot09PolishPipeline()
    {
        HousePolishPipeline("b10.33", "Assets/SkiView/Houses/lot-8221.fbx", "hv2_lot09_",
            "Assets/SkiView/Houses/Materials/hv2_lot09_body.mat", "Lot_8221", 38.76f,
            "69e4b1cd1f0cf882e5c344a1c95c4c06", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.34: Scott-cleared Lot 14 (lot-8405) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot14PolishPipeline</summary>
    public static void Lot14PolishPipeline()
    {
        HousePolishPipeline("b10.34", "Assets/SkiView/Houses/lot-8405.fbx", "hv2_lot14_",
            "Assets/SkiView/Houses/Materials/hv2_lot14_body.mat", "Lot_8405", 40.96f,
            "160861fedbfc78ee3f5b87c482da18cf", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.35: Scott-cleared Lot 11 (lot-8235) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot11PolishPipeline</summary>
    public static void Lot11PolishPipeline()
    {
        HousePolishPipeline("b10.35", "Assets/SkiView/Houses/lot-8235.fbx", "hv2_lot11_",
            "Assets/SkiView/Houses/Materials/hv2_lot11_body.mat", "Lot_8235", 38.86f,
            "66c983e0bb5f04b4194d731a394276a5", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.36: Scott-cleared Lot 13 (lot-8317) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot13PolishPipeline</summary>
    public static void Lot13PolishPipeline()
    {
        HousePolishPipeline("b10.36", "Assets/SkiView/Houses/lot-8317.fbx", "hv2_lot13_",
            "Assets/SkiView/Houses/Materials/hv2_lot13_body.mat", "Lot_8317", 37.75f,
            "98c9a150600d609109bbc00ece33118c", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.37: Scott-cleared Lot 01 (lot-8017) hero polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot01PolishPipeline</summary>
    public static void Lot01PolishPipeline()
    {
        HousePolishPipeline("b10.37", "Assets/SkiView/Houses/lot-8017.fbx", "hv2_lot01_",
            "Assets/SkiView/Houses/Materials/hv2_lot01_body.mat", "Lot_8017", 44.40f,
            "78447894fd6b0cd5b7b952dd3e32c0e2", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.38: Scott-cleared Lot 02 (lot-8035) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot02PolishPipeline</summary>
    public static void Lot02PolishPipeline()
    {
        HousePolishPipeline("b10.38", "Assets/SkiView/Houses/lot-8035.fbx", "hv2_lot02_",
            "Assets/SkiView/Houses/Materials/hv2_lot02_body.mat", "Lot_8035", 38.95f,
            "c2934fca6aa8e878a3e2fe4d461fb652", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.39: Scott-cleared Lot 03 (lot-8049) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot03PolishPipeline</summary>
    public static void Lot03PolishPipeline()
    {
        HousePolishPipeline("b10.39", "Assets/SkiView/Houses/lot-8049.fbx", "hv2_lot03_",
            "Assets/SkiView/Houses/Materials/hv2_lot03_body.mat", "Lot_8049", 37.55f,
            "099291ab43c3da719b4c461297d1d5fa", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.40: Scott-cleared Lot 04 (lot-8107) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot04PolishPipeline</summary>
    public static void Lot04PolishPipeline()
    {
        HousePolishPipeline("b10.40", "Assets/SkiView/Houses/lot-8107.fbx", "hv2_lot04_",
            "Assets/SkiView/Houses/Materials/hv2_lot04_body.mat", "Lot_8107", 50.11f,
            "ba264b53aaaaee49b4c880e051df1b1d", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.41: Scott-cleared Lot 05 (lot-8121) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot05PolishPipeline</summary>
    public static void Lot05PolishPipeline()
    {
        HousePolishPipeline("b10.41", "Assets/SkiView/Houses/lot-8121.fbx", "hv2_lot05_",
            "Assets/SkiView/Houses/Materials/hv2_lot05_body.mat", "Lot_8121", 36.36f,
            "ace993ef9a31ef52c9cd27270cb5e055", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.42: Scott-cleared Lot 06 (lot-8143) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot06PolishPipeline</summary>
    public static void Lot06PolishPipeline()
    {
        HousePolishPipeline("b10.42", "Assets/SkiView/Houses/lot-8143.fbx", "hv2_lot06_",
            "Assets/SkiView/Houses/Materials/hv2_lot06_body.mat", "Lot_8143", 38.56f,
            "aad19b7112576f414d53b982c0dcb72c", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.43: Scott-cleared Lot 07 (lot-8201) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot07PolishPipeline</summary>
    public static void Lot07PolishPipeline()
    {
        HousePolishPipeline("b10.43", "Assets/SkiView/Houses/lot-8201.fbx", "hv2_lot07_",
            "Assets/SkiView/Houses/Materials/hv2_lot07_body.mat", "Lot_8201", 37.96f,
            "723ebaf161ba437ed85af3680c88937a", "other lakes, store/trailer, Wade invent");
    }

    /// <summary>b10.44: Scott-cleared Lot 08 (lot-8215) polish hot-swap. Run: unity_run.sh SkiViewCapture.Lot08PolishPipeline</summary>
    public static void Lot08PolishPipeline()
    {
        HousePolishPipeline("b10.44", "Assets/SkiView/Houses/lot-8215.fbx", "hv2_lot08_",
            "Assets/SkiView/Houses/Materials/hv2_lot08_body.mat", "Lot_8215", 39.96f,
            "5f07471e4b8383cb3fb19017bf163272", "other lakes, store/trailer, Wade invent");
    }



}
