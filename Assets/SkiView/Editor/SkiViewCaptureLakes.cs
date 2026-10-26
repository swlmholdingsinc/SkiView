using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SkiView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Pass 15: stills of the other lakes (aerial, course, shoreline, buildings, boat) and the per-lake boat colours.</summary>
public static partial class SkiViewCapture
{
    public static void P15Test() { SkiViewBuilder.BuildAll(); LakeShotsAll(); BoatShots(); }
    public static void P15TestRamp() { SkiViewBuilder.BuildAll(); LakeShotsAll(); BoatShots(); RampShots(); JumpShots(); }
    public static void LakeShotsAll() { foreach (var id in SkiViewBuilder.LAKES) if (File.Exists(SkiViewBuilder.LakeScenePath(id))) LakeShots(id); }
    /// <summary>Pass 17 test: scenes + lake shots + cameras + per-tier perf stats (no player builds).</summary>
    public static void P17Test() { SkiViewBuilder.BuildAll(); LakeShotsAll(); CameraShots(); LakePerfAll(); }
    public static void P19Home() { SkiViewBuilder.ConfigureProject(); SkiViewBuilderUI.Setup(); SkiViewBuilderAudio.Setup(); SkiViewBuilder.BuildScene(); CameraShots(); }
    public static void P19Turns() { foreach (var id in new[] { "saltwhite", "tallpine", "cedarbend" }) SkiViewBuilder.BuildLake(id); TurnaroundShots("saltwhite"); TurnaroundShots("tallpine"); TurnaroundShots("cedarbend"); SkierClearance("tallpine"); }
    /// <summary>Pass 19: every lake (new four + kit swaps + Tall Pine look), Addressables groups, lake stills for the new lakes and Tall
    /// Pine, turnaround shots on the new lakes, Gulfwind / Saguaro skier clearance, per-tier perf.</summary>
    public static void P19Lakes()
    {
        SkiViewBuilder.BuildLakes();
        foreach (var id in new[] { "saguaro", "harrowpond", "gulfwind", "marrowind", "tallpine", "saltwhite", "heronmarsh" }) LakeShots(id);
        foreach (var id in new[] { "saguaro", "harrowpond", "gulfwind" }) TurnaroundShots(id);
        SkierClearance("gulfwind"); SkierClearance("saguaro");
        LakePerfAll();
    }
    /// <summary>Pass 19 (punch list "course and buoy accuracy"): straight-down orthographic shot of every course with each ball's
    /// world and viewport position in Screens/course_<id>_v19.json; Tools/make_v19_sheets.py overlays the rule positions on it.</summary>
    public static void CourseTopAll() { foreach (var id in new[] { "skiview" }.Concat(SkiViewBuilder.LAKES)) CourseTop(id); }
    public static void CourseTop(string id)
    {
        string sp = id == "skiview" ? SkiViewBuilder.SCENE : SkiViewBuilder.LakeScenePath(id); if (!File.Exists(sp)) return;
        EditorSceneManager.OpenScene(sp);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.camMode = SkiViewGame.CamMode.Broadcast; game.Init();
        var cam = game.cam; var course = GameObject.Find("SlalomCourse"); if (course == null) { Debug.LogWarning("[SkiView] course top " + id + ": no SlalomCourse"); return; }
        Vector3 o = course.transform.position; Transform t1 = course.transform.Find("Turn1"), t6 = course.transform.Find("Turn6");
        // centreline axis: midpoint(Turn1,Turn2) -> midpoint(Turn5,Turn6) (Turn1->Turn6 is skewed by the +-11.5 m offsets)
        Transform t2 = course.transform.Find("Turn2"), t5 = course.transform.Find("Turn5");
        Vector3 ax = t1 != null && t6 != null && t2 != null && t5 != null ? Vector3.ProjectOnPlane((t5.position + t6.position) - (t1.position + t2.position), Vector3.up).normalized : Vector3.forward;
        bool ortho0 = cam.orthographic; float os0 = cam.orthographicSize, far0 = cam.farClipPlane, fov0 = cam.fieldOfView, near0 = cam.nearClipPlane; bool fog0 = RenderSettings.fog;
        Shot(cam, game, "course_" + id + "_top", () =>
        {
            cam.orthographic = false; cam.fieldOfView = 2f * Mathf.Atan(112f / 900f) * Mathf.Rad2Deg; cam.nearClipPlane = 600f; cam.farClipPlane = 1000f; RenderSettings.fog = false;
            cam.transform.position = o + Vector3.up * 900f; cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.Cross(ax, Vector3.up));
        });
        // same placement again for the projection (Shot resets aspect afterwards)
        cam.orthographic = false; cam.fieldOfView = 2f * Mathf.Atan(112f / 900f) * Mathf.Rad2Deg; cam.nearClipPlane = 600f; cam.farClipPlane = 1000f; RenderSettings.fog = false; cam.aspect = (float)W / H;
        cam.transform.position = o + Vector3.up * 900f; cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.Cross(ax, Vector3.up));
        var sb = new System.Text.StringBuilder(); sb.Append("{\"lake\":\"" + id + "\",\"w\":" + W + ",\"h\":" + H + ",\"orthoSize\":112,");
        sb.Append($"\"origin\":[{o.x:F3},{o.z:F3}],\"axis\":[{ax.x:F5},{ax.z:F5}],\"balls\":[");
        bool first = true;
        foreach (Transform b in course.transform)
        {
            var vp = cam.WorldToViewportPoint(b.position); var lp = b.position - o;
            float along = Vector3.Dot(lp, ax), lat = Vector3.Dot(lp, Vector3.Cross(Vector3.up, ax));
            sb.Append((first ? "" : ",") + $"{{\"n\":\"{b.name}\",\"along\":{along:F3},\"lat\":{lat:F3},\"vx\":" + vp.x.ToString("F5", System.Globalization.CultureInfo.InvariantCulture) + ",\"vy\":" + vp.y.ToString("F5", System.Globalization.CultureInfo.InvariantCulture) + "}"); first = false;
        }
        sb.Append("]}"); File.WriteAllText(Path.Combine(OutDir, "course_" + id + "_v19.json"), sb.ToString());
        cam.orthographic = ortho0; cam.orthographicSize = os0; cam.farClipPlane = far0; cam.fieldOfView = fov0; cam.nearClipPlane = near0; RenderSettings.fog = fog0; cam.ResetAspect();
        Debug.Log("[SkiView] course top " + id + " done");
    }
    /// <summary>Pass 19 final: full rebuild (all scenes), then every capture for the v19 sheets in one Unity run.</summary>
    public static void P19CourseTop() { CourseTopAll(); Debug.Log("[SkiView] P19CourseTop done"); }
    public static void P19Final()
    {
        SkiViewBuilder.BuildAll();
        CameraShots(); LakeShotsAll(); TurnaroundShotsAll(); CourseTopAll(); LakePerfAll();
        Debug.Log("[SkiView] P19Final done");
    }
    public static void P19LakeOne() { var id = Environment.GetEnvironmentVariable("SV_LAKE") ?? "saguaro"; SkiViewBuilder.BuildLake(id); LakeShots(id); }
    public static void P18Quick() { SkiViewBuilder.BuildAll(); CameraShots(); TurnaroundShotsAll(); LakePerfAll(); }
    public static void P18Turn() { TurnaroundShotsAll(); }
    public static void P18Clear() { SkierClearance("saltwhite"); }
    public static void P18Tall() { SkiViewBuilder.BuildLake("tallpine"); LakeShots("tallpine"); SkierClearance("tallpine"); TurnaroundShots("tallpine"); }
    public static void P18NewLakes2()
    {
        SkiViewBuilder.BuildLake("tallpine");
        foreach (var id in new[] { "villaserena", "tallpine", "heronmarsh" }) LakeShots(id);
        SkierClearance("tallpine"); TurnaroundShots("villaserena"); TurnaroundShots("tallpine");
    }
    public static void P18NewLakes()
    {
        SkiViewBuilder.BuildLakes();   // every lake + scene registration + Addressables groups
        foreach (var id in new[] { "villaserena", "tallpine", "heronmarsh" }) { LakeShots(id); if (id != "heronmarsh") SkierClearance(id); }
    }
    public static void P18Salt() { SkiViewBuilder.BuildLake("saltwhite"); LakeShots("saltwhite"); SkierClearance("saltwhite"); }

    /// <summary>Pass 18: closest the skier comes to the shore (terrain above the water) over three full passes with their turnarounds,
    /// split by course zone (along-course thirds and the two turn ends), from a 10-degree ring march every 0.2 s.</summary>
    public static void SkierClearance(string id)
    {
        string sp = id == "skiview" ? SkiViewBuilder.SCENE : SkiViewBuilder.LakeScenePath(id);
        EditorSceneManager.OpenScene(sp);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.Init();
        var terrain = Terrain.activeTerrain; if (terrain == null) return;
        float wl = 0f; var water = GameObject.Find("Water"); if (water != null) wl = water.transform.position.y;
        Vector3 fS = new Vector3(game.path.courseFwdSouth.x, 0, game.path.courseFwdSouth.y), pE = new Vector3(-fS.z, 0, fS.x);
        var zoneMin = new Dictionary<string, (float, Vector3)>();
        int passes = 0; float t = 0f; var track = new List<string>();
        while (t < 420f && passes < 3)
        {
            var m0 = game.mode; game.SimulateFor(0.2f); t += 0.2f; if (m0 == SkiViewGame.Mode.Pass && game.mode != SkiViewGame.Mode.Pass) passes++;
            if (game.mode == SkiViewGame.Mode.Fallen) continue;
            Vector3 s = game.SkierPos; float best = 99f;
            for (int k = 0; k < 36 && best > 0.5f; k++)
            {
                Vector3 d = Quaternion.Euler(0, k * 10f, 0) * Vector3.forward;
                for (float r = 0.5f; r < Mathf.Min(best, 30f); r += 0.5f) { var q = s + d * r; if (terrain.SampleHeight(q) + terrain.transform.position.y > wl - 0.05f) { best = r; break; } }
            }
            float a = Vector3.Dot(s, fS), pp = Vector3.Dot(s, pE);
            string zone = a < -190f ? "turn N/NW end" : a > 190f ? "turn S/SE end" : a < -60f ? "reach N/NW" : a > 60f ? "reach S/SE" : "middle";
            if (!zoneMin.TryGetValue(zone, out var cur) || best < cur.Item1) zoneMin[zone] = (best, new Vector3(a, 0, pp));
            track.Add($"[{s.x:0.0},{s.z:0.0}]");
        }
        // skier track (world x, z every 0.2 s) for the shore generator's clearance push (Tools/gen_venue_lake.py)
        File.WriteAllText($"Tools/lake_data/{id}_skier_track.json", "{\"xz\": [" + string.Join(",", track) + "]}\n");
        foreach (var kv in zoneMin.OrderBy(k => k.Key)) Debug.Log($"[SkiView] clearance {id} {kv.Key}: skier min {kv.Value.Item1:0.0} m to shore at along {kv.Value.Item2.x:0} m, lateral {kv.Value.Item2.z:0} m ({passes} passes)");
    }

    public static void TurnaroundShotsAll() { foreach (var id in new[] { "skiview", "saltwhite", "tallpine", "cedarbend", "saguaro", "harrowpond", "gulfwind", "marrowind" }) TurnaroundShots(id); }

    /// <summary>Pass 18: the post-pass turnaround (sink holding the handle, DWS spray, skiing round the turn island behind the boat,
    /// set-up into the gates), high chase camera, plus the boat camera round the island and the first-person DWS.</summary>
    public static void TurnaroundShots(string id)
    {
        Directory.CreateDirectory(OutDir);
        string sp = id == "skiview" ? SkiViewBuilder.SCENE : SkiViewBuilder.LakeScenePath(id);
        if (!File.Exists(sp)) { Debug.LogWarning("[SkiView] turnaround: no scene " + sp); return; }
        EditorSceneManager.OpenScene(sp);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.camMode = SkiViewGame.CamMode.SkierChase; game.Init();
        var cam = game.cam;
        int pcQ = Array.FindIndex(QualitySettings.names, n => n.Contains("PC")); if (pcQ >= 0) QualitySettings.SetQualityLevel(pcQ, true);
        string pre = "turn19_" + id + "_";
        // switch camera, then let two frames run so camera-velocity-stretched particles don't smear from the cut
        void Snap(string nm, SkiViewGame.CamMode m) { var keep = game.camMode; game.camMode = m; game.ApplyVisuals(true); game.SimulateFor(1f / 30f); game.SimulateFor(1f / 30f); Shot(cam, game, pre + nm, null); game.camMode = keep; }
        bool ok = StepUntil(game, () => game.mode == SkiViewGame.Mode.Sink, 400f);
        if (!ok) { Debug.LogWarning($"[SkiView] turnaround {id}: no full pass (mode {game.mode})"); return; }
        Vector3 b0 = game.BoatPos;
        StepUntil(game, () => game.mode != SkiViewGame.Mode.Sink || game.ActionT > 2.9f, 10f, 1f / 60f);
        Debug.Log($"[SkiView] turnaround {id}: sink, boat {Vector3.Distance(b0, game.BoatPos):0.0} m past the exit, skier y {game.skier.position.y:0.00}");
        Snap("1_sink", SkiViewGame.CamMode.CloseChase);
        if (game.SpinTurn)
        {
            // pass 19 spin turn: the boat pivots round the floating skier at idle, then the line comes tight for the DWS
            StepUntil(game, () => !game.SpinTurn || game.path.InSpin(game.BoatS), 40f, 1f / 30f);
            float sp0 = game.ActionT; Vector3 f0 = game.BoatFwd;
            StepUntil(game, () => !game.SpinTurn || Mathf.Abs(Vector3.SignedAngle(f0, game.BoatFwd, Vector3.up)) > 80f, 40f, 1f / 30f);
            Debug.Log($"[SkiView] turnaround {id}: spin turn, boat turned 80 deg in {game.ActionT - sp0:0.0} s at {game.BoatSpeed:0.0} m/s, skier {Vector3.Distance(game.SkierPos, game.BoatPos):0.0} m from the boat");
            Snap("1b_spin", SkiViewGame.CamMode.SkierChase); Snap("1c_spin_boat", SkiViewGame.CamMode.Broadcast);
        }
        StepUntil(game, () => game.mode == SkiViewGame.Mode.DeepStart && game.ActionT > 1.15f, 90f, 1f / 60f);
        Snap("2_dws", SkiViewGame.CamMode.CloseChase);
        // round the island: wait for the boat to be broadside to the course (apex of the turn); log which way it turned
        Vector3 cf = new Vector3(game.path.courseFwdSouth.x, 0, game.path.courseFwdSouth.y);
        float turned = 0f; Vector3 lastF = game.BoatFwd;
        StepUntil(game, () => { float a = Vector3.SignedAngle(lastF, game.BoatFwd, Vector3.up); turned += a; lastF = game.BoatFwd; return game.mode == SkiViewGame.Mode.Pass || (game.mode == SkiViewGame.Mode.Turn && Mathf.Abs(turned) >= 90f); }, 90f, 1f / 30f);
        Debug.Log($"[SkiView] turnaround {id}: island apex, boat turned {turned:0} deg so far ({(turned > 0 ? "clockwise" : "counterclockwise")} seen from above), skier {Vector3.Distance(game.SkierPos, game.BoatPos):0.0} m behind, mode {game.mode}");
        Snap("3_island", SkiViewGame.CamMode.SkierChase); Snap("3b_island_boat", SkiViewGame.CamMode.Broadcast);
        StepUntil(game, () => { float a = Vector3.SignedAngle(lastF, game.BoatFwd, Vector3.up); turned += a; lastF = game.BoatFwd; float pg = game.path.Progress(game.BoatS); return game.mode == SkiViewGame.Mode.Pass || (Mathf.Abs(turned) > 160f && Mathf.Abs(Vector3.Dot(game.BoatFwd, cf)) > 0.97f && pg < 0f && pg > -game.passWindowLate - 12f); }, 90f, 1f / 30f);
        Debug.Log($"[SkiView] turnaround {id}: set-up, turned {turned:0} deg in all, progress {game.path.Progress(game.BoatS):0} m, mode {game.mode}, rope {game.sim.ropeM:0.00} m");
        Snap("4_setup", SkiViewGame.CamMode.SkierChase);
        // second pass in first person from its exit (the FP spray thinning applies to what's emitted while FP is on), DWS frame,
        // then skip from its set-up check
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Sink, 120f);
        if (game.mode == SkiViewGame.Mode.Sink)
        {
            game.camMode = SkiViewGame.CamMode.FirstPerson;
            StepUntil(game, () => game.mode == SkiViewGame.Mode.DeepStart && game.ActionT > 1.15f, 10f, 1f / 60f);
            Shot(cam, game, pre + "2b_dws_first", null); game.camMode = SkiViewGame.CamMode.SkierChase;
        }
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Sink, 120f);
        if (game.mode == SkiViewGame.Mode.Sink) { game.SkipToSetup(); game.SimulateFor(1f); Debug.Log($"[SkiView] turnaround {id}: skip -> mode {game.mode}, progress {game.path.Progress(game.BoatS):0} m, aligned {Vector3.Dot(game.BoatFwd, cf):0.00}"); StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass, 15f); Debug.Log($"[SkiView] turnaround {id}: after skip the pass starts: {game.mode == SkiViewGame.Mode.Pass}"); }
        game.camMode = SkiViewGame.CamMode.Broadcast;
    }

    public static void LakeShots(string id)
    {
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(SkiViewBuilder.LakeScenePath(id));
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.camMode = SkiViewGame.CamMode.Broadcast; game.Init(); SyncA2C();
        var cam = game.cam; var terrain = Terrain.activeTerrain;
        int pcQ = Array.FindIndex(QualitySettings.names, n => n.Contains("PC")); if (pcQ >= 0) QualitySettings.SetQualityLevel(pcQ, true);
        float Ground(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : 0f;
        Vector3 fS = new Vector3(game.path.courseFwdSouth.x, 0, game.path.courseFwdSouth.y), pE = new Vector3(-fS.z, 0, fS.x);   // pE = layout +p side
        Vector3 Wp(float a, float p) => fS * a + pE * p;
        string pre = "lake_" + id + "_";
        if (id == "saltwhite") { SaltwhiteShots(game, cam, terrain, pre, Wp); return; }
        if (id == "villaserena" || id == "tallpine" || id == "heronmarsh" || id == "saguaro" || id == "harrowpond" || id == "gulfwind" || id == "marrowind") { NewLakeShots(id, game, cam, terrain, pre, Wp); return; }
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2, 400f);
        Shot(cam, game, pre + "course_boatcam", null);
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 4, 60f);
        // drone over the south pre-gate looking up the course (NNE)
        Shot(cam, game, pre + "course_drone", () =>
        {
            cam.fieldOfView = 55f; cam.transform.position = Wp(250f, -14f) + Vector3.up * 34f; cam.transform.LookAt(Wp(-40f, 4f));
        });
        // aerial, straight down, north up: lake centre, ~960 m up (fog off for the study comparison)
        bool fog = RenderSettings.fog; float far = cam.farClipPlane;
        Shot(cam, game, pre + "aerial", () =>
        {
            RenderSettings.fog = false; cam.farClipPlane = 4000f; cam.fieldOfView = 40f;
            cam.transform.position = new Vector3(10f, 980f, 85f); cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        });
        RenderSettings.fog = fog; cam.farClipPlane = far;
        // oblique aerial from the SSW, like the listing drone photos
        Shot(cam, game, pre + "oblique", () =>
        {
            cam.fieldOfView = 50f; Vector3 p = Wp(430f, -60f); p.y = 120f; cam.transform.position = p; cam.transform.LookAt(Wp(-60f, 0f));
        });
        // shoreline: from the ESE fairway across to the WNW lawn strip, tree row and fir wall
        Shot(cam, game, pre + "shore_east_lawn", () =>
        {
            cam.fieldOfView = 55f; Vector3 p = Wp(-30f, 50f);   // v2 shore: ESE edge ~30 m off the line here
            p.y = Ground(p) + 1.7f; cam.transform.position = p;
            Vector3 t = Wp(10f, -60f); t.y = 7f; cam.transform.LookAt(t);
        });
        // from the WNW strip down the lake toward the fairway and the moorage
        Shot(cam, game, pre + "shore_west_strip", () =>
        {
            cam.fieldOfView = 58f; Vector3 p = Wp(20f, -47f);   // v2 shore: WNW edge ~38 m off the line
            p.y = Ground(p) + 1.7f; cam.transform.position = p;
            Vector3 t = Wp(150f, 40f); t.y = 3f; cam.transform.LookAt(t);
        });
        var moor = GameObject.Find("Bldg_moorage"); var house = GameObject.Find("Bldg_farmhouse"); var barn = GameObject.Find("Bldg_barn");
        if (moor != null) Shot(cam, game, pre + "moorage", () =>
        {
            Transform m = moor.transform; cam.fieldOfView = 50f;
            Vector3 p = m.position + m.forward * 22f - m.right * 9f; p.y = 2.0f; cam.transform.position = p; cam.transform.LookAt(m.position + Vector3.up * 1.6f);
        });
        if (house != null) Shot(cam, game, pre + "farmhouse", () =>
        {
            Transform h = house.transform; cam.fieldOfView = 45f;
            Vector3 p = h.position + h.forward * 34f + h.right * 10f; p.y = Ground(p) + 1.7f; cam.transform.position = p; cam.transform.LookAt(h.position + Vector3.up * 4.5f);
        });
        if (house != null) Shot(cam, game, pre + "farmhouse_close", () =>
        {
            Transform h = house.transform; cam.fieldOfView = 40f;   // pass 16: detail check (trim, porch, windows, gutters, chimney)
            Vector3 p = h.position + h.forward * 15f - h.right * 7f; p.y = Ground(p) + 1.7f; cam.transform.position = p; cam.transform.LookAt(h.position + h.forward * 2f + Vector3.up * 3.6f);
        });
        if (barn != null) Shot(cam, game, pre + "barn", () =>
        {
            Transform b = barn.transform; cam.fieldOfView = 45f;
            // stand in the farmyard (toward the farmhouse) so the shot isn't inside the tree wall
            Vector3 d = house != null ? house.transform.position - b.position : b.forward; d.y = 0f; d.Normalize();
            Vector3 p = b.position + d * 24f + Vector3.Cross(Vector3.up, d) * 6f; p.y = Ground(p) + 1.7f; cam.transform.position = p; cam.transform.LookAt(b.position + Vector3.up * 3f);
        });
        Shot(cam, game, pre + "backdrop", () =>
        {
            cam.fieldOfView = 50f; Vector3 p = Wp(-200f, 0f); p.y = 2.0f; cam.transform.position = p;   // pass 16: foothills / Cascade layers
            Vector3 t = Wp(260f, 0f); t.y = 60f; cam.transform.LookAt(t);
        });
        if (barn != null) Shot(cam, game, pre + "north_end", () =>
        {
            Transform b = barn.transform; cam.fieldOfView = 55f;
            Vector3 p = Wp(-250f, 4f); p.y = 1.3f; cam.transform.position = p; cam.transform.LookAt(b.position + Vector3.up * 3f);
        });
        // boat, low side view on the water (same framing as env_a)
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2, 400f);
        Shot(cam, game, pre + "boat", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 50f; cam.transform.position = b - r * 10.5f + f * 1.0f + Vector3.up * 1.25f;
            cam.transform.LookAt(b + Vector3.up * 0.9f - r * 2f); cam.transform.rotation = Quaternion.Euler(-3f, cam.transform.eulerAngles.y, 0);
        });
        Debug.Log($"[SkiView] lake shots {id} done");
    }

    /// <summary>boats_v15: the boat in every lake palette (boat_palettes.json order), same framing, overcast Cedar Bend light;
    /// the Ski View home boat also rendered in its own lake.</summary>
    public static void BoatShots()
    {
        string scenePath = File.Exists(SkiViewBuilder.LakeScenePath("cedarbend")) ? SkiViewBuilder.LakeScenePath("cedarbend") : SkiViewBuilder.SCENE;
        EditorSceneManager.OpenScene(scenePath);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.Init();
        var cam = game.cam;
        int pcQ = Array.FindIndex(QualitySettings.names, n => n.Contains("PC")); if (pcQ >= 0) QualitySettings.SetQualityLevel(pcQ, true);
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3, 400f);
        var pal = game.boat.GetComponentInChildren<BoatPalette>();
        string txt = File.ReadAllText("Assets/SkiView/Boat/boat_palettes.json");
        var order = Regex.Match(txt, "\"order\"\\s*:\\s*\\[([^\\]]*)\\]").Groups[1].Value.Replace("\"", "").Split(',');
        var log = new List<string>();
        foreach (var raw in order)
        {
            string id = raw.Trim(); if (id.Length == 0) continue;
            var blk = Regex.Match(txt, "\"" + id + "\"\\s*:\\s*\\{([^{}]*)\\}").Groups[1].Value;
            string G(string k) { var m = Regex.Match(blk, "\"" + k + "\"\\s*:\\s*\"?([^\",}]*)\"?"); return m.Success ? m.Groups[1].Value.Trim() : null; }
            ColorUtility.TryParseHtmlString(G("deck"), out pal.deck); ColorUtility.TryParseHtmlString(G("band"), out pal.band);
            ColorUtility.TryParseHtmlString(G("accent"), out pal.accent); ColorUtility.TryParseHtmlString(G("livery") ?? "#FFFFFF", out pal.livery);
            float.TryParse(G("accent_metallic") ?? "0", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pal.accentMetallic);
            pal.lake = id; pal.Apply();
            Shot(cam, game, "boat15_" + id, () =>
            {
                Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
                cam.fieldOfView = 21f; cam.transform.position = b - r * 11f + f * 5.5f + Vector3.up * 2.6f;
                cam.transform.LookAt(b + Vector3.up * 0.7f + f * 0.4f);
            });
            log.Add($"{id}: band {G("band")} accent {G("accent")} deck {G("deck")}");
        }
        Debug.Log("[SkiView] boat palette shots: " + string.Join(" | ", log));
        // the home boat in its own lake
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.Init(); cam = game.cam;
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3, 400f);
        Shot(cam, game, "boat15_home_skiview", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 21f; cam.transform.position = b - r * 11f + f * 5.5f + Vector3.up * 2.6f; cam.transform.LookAt(b + Vector3.up * 0.7f + f * 0.4f);
        });
    }

    /// <summary>Pass 18: stills for the v2-file lakes (Villa Serena, Tall Pine, Heron Marsh): course, aerial, oblique, both banks from
    /// the water, the mood shot down the lane toward the sun, the buildings, the jump (Tall Pine / Heron Marsh) and the boat.</summary>
    static void NewLakeShots(string id, SkiViewGame game, Camera cam, Terrain terrain, string pre, Func<float, float, Vector3> Wp)
    {
        float Ground(Vector3 p) => terrain != null ? Mathf.Max(0f, terrain.SampleHeight(p) + terrain.transform.position.y) : 0f;
        bool jumpOnly = game.jumpOnly;
        if (jumpOnly) game.SimulateFor(1.5f);   // warm the planar reflection / water before the first still
        if (!jumpOnly)
        {
            StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2, 400f);
            Shot(cam, game, pre + "course_boatcam", null);
            StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 4, 60f);
            Shot(cam, game, pre + "course_chase", () => { game.camMode = SkiViewGame.CamMode.SkierChase; game.ApplyVisuals(true); });
            if (Environment.GetEnvironmentVariable("SV_SHOTPERF") == "1") Shot(cam, game, pre + "close_chase", () => { game.camMode = SkiViewGame.CamMode.CloseChase; game.ApplyVisuals(true); });   // lake P1: CLOSE camera pair
            game.camMode = SkiViewGame.CamMode.Broadcast;
        }
        Shot(cam, game, pre + "course_drone", () => { cam.fieldOfView = 55f; cam.transform.position = Wp(250f, -10f) + Vector3.up * 34f; cam.transform.LookAt(Wp(-40f, 6f)); });
        var water = GameObject.Find("Water"); var wb = water != null ? water.GetComponent<Renderer>().bounds : new Bounds(Vector3.zero, Vector3.one * 600f);
        bool fog = RenderSettings.fog; float far = cam.farClipPlane;
        Shot(cam, game, pre + "aerial", () => { RenderSettings.fog = false; cam.farClipPlane = 4000f; cam.fieldOfView = 40f; cam.transform.position = new Vector3(wb.center.x, Mathf.Max(700f, wb.size.magnitude * 1.25f), wb.center.z); cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); });
        RenderSettings.fog = fog; cam.farClipPlane = far;
        Shot(cam, game, pre + "oblique", () => { cam.fieldOfView = 50f; Vector3 p = Wp(420f, -70f); p.y = 120f; cam.transform.position = p; cam.transform.LookAt(Wp(-60f, 0f)); });
        // both banks from mid-lane at eye height on the water (+p bank, -p bank)
        Shot(cam, game, pre + "bank_plus", () => { cam.fieldOfView = 58f; cam.transform.position = Wp(-60f, -6f) + Vector3.up * 1.6f; Vector3 t = Wp(10f, 60f); t.y = 8f; cam.transform.LookAt(t); });
        Shot(cam, game, pre + "bank_minus", () => { cam.fieldOfView = 58f; cam.transform.position = Wp(60f, 6f) + Vector3.up * 1.6f; Vector3 t = Wp(-10f, -60f); t.y = 8f; cam.transform.LookAt(t); });
        // mood: low down the lane toward the sun (golden-hour lakes) or away from it (noon)
        var sun = RenderSettings.sun; Vector3 toSun = sun != null ? -sun.transform.forward : Vector3.up; Vector3 fS = Wp(1f, 0f) - Wp(0f, 0f);
        float sgn = Vector3.Dot(new Vector3(toSun.x, 0, toSun.z), fS) >= 0f ? 1f : -1f;
        Shot(cam, game, pre + "mood", () => { cam.fieldOfView = 55f; cam.transform.position = Wp(-sgn * 160f, 0f) + Vector3.up * 1.4f; Vector3 t = Wp(sgn * 200f, 0f); t.y = 9f; cam.transform.LookAt(t); });
        GameObject First(string n) => GameObject.Find(n);
        // pass 19: plus two kit pieces (the first house and a dock / boathouse) from KitBuildings
        var picks = new List<(string, GameObject)>();
        foreach (var bn in new[] { "Bldg_estate", "Bldg_boathouse", "Bldg_farmhouse", "Bldg_pier", "Bldg_grandstand", "Bldg_bridge" }) { var g_ = First(bn); if (g_ != null) picks.Add((bn, g_)); }
        var kr = GameObject.Find("KitBuildings");
        if (kr != null)
        {
            Transform house = null, dock = null;
            foreach (Transform k in kr.transform) { bool d_ = k.name.Contains("dock") || k.name.Contains("boathouse") || k.name.Contains("seawall"); if (d_ && dock == null && !k.name.Contains("seawall")) dock = k; if (!d_ && house == null) house = k; }
            if (house != null) picks.Add(("kit_" + house.name, house.gameObject));
            if (house != null && id == "tallpine") picks.Add(("Bldg_house_inland", house.gameObject));   // T1: the old farmhouse view (now the clubhouse), from the shaded land side
            if (dock != null) picks.Add(("Bldg_pier_kit_" + dock.name, dock.gameObject));
        }
        foreach (var (bn, bgo) in picks)
        {
            // aim off the renderer bounds (some building roots sit at the origin with world-placed meshes), camera on the lake side
            var rs = bgo.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
            Bounds bb = rs[0].bounds; foreach (var r_ in rs) bb.Encapsulate(r_.bounds);
            Vector3 c = bb.center, o = Wp(0f, 0f), ax = (Wp(1f, 0f) - o).normalized;
            Vector3 onLine = o + ax * Vector3.Dot(c - o, ax); Vector3 toLake = onLine - c; toLake.y = 0f; float dl = toLake.magnitude; toLake = dl > 1f ? toLake / dl : Vector3.forward;
            bool onWater = bn.StartsWith("Bldg_pier") || bn == "Bldg_boathouse" || bn == "Bldg_bridge"; float back = Mathf.Max(24f, bb.extents.magnitude * 2.2f);
            Shot(cam, game, pre + bn.Replace("Bldg_", "bldg_"), () =>
            {
                cam.fieldOfView = 48f;
                Vector3 p = c + toLake * Mathf.Min(back, Mathf.Max(8f, dl - 2f)) + Vector3.Cross(Vector3.up, toLake) * 8f; p.y = onWater ? 1.6f : Mathf.Max(1.6f, Ground(p) + (dl > 30f ? 12f : 1.7f));   // inland (behind tree rows): from 12 m up
                if (bn.StartsWith("kit_"))
                {
                    // pass 19: kit houses from the water, 3 m up (the lake-side fringe trees hide them from an inland camera)
                    float s_ = 30f; while (s_ < 200f && Ground(c + toLake * s_) > -0.1f) s_ += 4f;
                    p = c + toLake * (s_ + 22f) + Vector3.Cross(Vector3.up, toLake) * 10f; p.y = 3f;
                }
                Vector3 aim = new Vector3(c.x, bb.min.y + Mathf.Min(3f, bb.extents.y), c.z);
                if (!onWater && !bn.StartsWith("kit_")) p = ClearOfTrees(p, aim, c, back);   // pass 19 punch list #1 (Tall Pine): the farmhouse camera stood inside a trunk
                if (!onWater && !bn.StartsWith("kit_")) p = LitBuildingView(cam, p, aim, c, back);   // T1: vegetation-pack foliage is not in the terrain tree list
                cam.transform.position = p; cam.transform.LookAt(aim);
            });
        }
        if (game.jumpRamp != null)
        {
            Transform R = game.jumpRamp; Vector3 f = R.forward; f.y = 0; f.Normalize();
            game.autopilot = false;
            game.BeginJumpRun(false); game.jumpCutAt = -4.3f; game.jumpPopAt = 0.86f; game.SimulateFor(1f / 30f);
            Vector3 side = game.JumpSide;
            Shot(cam, game, pre + "ramp", () => { cam.fieldOfView = 45f; Vector3 p = R.position - side * 16f + f * 9f + Vector3.up * 1.6f; cam.transform.position = p; cam.transform.LookAt(R.position - f * 3f + Vector3.up * 0.6f); });
            StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Flight && game.JumpT >= 0.35f, 20f, 1f / 60f);
            Shot(cam, game, pre + "jump_flight", () => { Vector3 sk = game.SkierPos; Vector3 p = sk + side * 18f - f * 4f; p.y = 1.8f; cam.transform.position = p; cam.fieldOfView = 40f; cam.transform.LookAt(sk + Vector3.up * 0.5f); });
            StepUntil(game, () => game.mode != SkiViewGame.Mode.Jump, 20f, 1f / 60f);
            Debug.Log($"[SkiView] {id} jump: result '{game.JumpResult}' distance {game.JumpDistance:0.0} m, jumpDir {game.jumpDir}, ramp {Vector3.Distance(Vector3.ProjectOnPlane(R.position, Vector3.up), f * Vector3.Dot(R.position, f)):0.0} m off the line, jumpOnly {jumpOnly}");
            game.autopilot = true; if (!jumpOnly) { game.jumpMode = false; game.SetJumpGear(false); }
        }
        if (!jumpOnly) StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2, 400f);
        Shot(cam, game, pre + "boat", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 50f; cam.transform.position = b - r * 10.5f + f * 1.0f + Vector3.up * 1.25f;
            cam.transform.LookAt(b + Vector3.up * 0.9f - r * 2f); cam.transform.rotation = Quaternion.Euler(-3f, cam.transform.eulerAngles.y, 0);
        });
        Debug.Log($"[SkiView] lake shots {id} done");
    }

    /// <summary>Pass 17: Saltwhite stills. Lp(along, p) = Holly's layout frame (along +dir_unit NW -> SE, +p = SW bank).</summary>
    static void SaltwhiteShots(SkiViewGame game, Camera cam, Terrain terrain, string pre, Func<float, float, Vector3> Wp)
    {
        float Ground(Vector3 p) => terrain != null ? Mathf.Max(0f, terrain.SampleHeight(p) + terrain.transform.position.y) : 0f;
        Vector3 Lp(float al, float p) => Wp(-al, p);
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2, 400f);
        Shot(cam, game, pre + "course_boatcam", null);
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 4, 60f);
        Shot(cam, game, pre + "course_drone", () => { cam.fieldOfView = 55f; cam.transform.position = Lp(250f, -10f) + Vector3.up * 34f; cam.transform.LookAt(Lp(-40f, 6f)); });
        var water = GameObject.Find("Water"); Vector3 c = water != null ? water.GetComponent<Renderer>().bounds.center : Vector3.zero;
        bool fog = RenderSettings.fog; float far = cam.farClipPlane;
        Shot(cam, game, pre + "aerial", () => { RenderSettings.fog = false; cam.farClipPlane = 4000f; cam.fieldOfView = 40f; cam.transform.position = new Vector3(c.x, 980f, c.z); cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); });
        Debug.Log($"[SkiView] saltwhite aerial camera centre {c.x:0.0} {c.z:0.0}");
        RenderSettings.fog = fog; cam.farClipPlane = far;
        Shot(cam, game, pre + "oblique", () => { cam.fieldOfView = 50f; Vector3 p = Lp(430f, -70f); p.y = 120f; cam.transform.position = p; cam.transform.LookAt(Lp(-60f, 0f)); });
        Shot(cam, game, pre + "shore_sw_sand", () => { cam.fieldOfView = 58f; Vector3 p = Lp(-20f, 56f); p.y = Ground(p) + 1.7f; cam.transform.position = p; Vector3 t = Lp(30f, -90f); t.y = 5f; cam.transform.LookAt(t); });
        Shot(cam, game, pre + "shore_ne", () => { cam.fieldOfView = 58f; Vector3 p = Lp(70f, -30f); p.y = Ground(p) + 1.7f; cam.transform.position = p; Vector3 t = Lp(-30f, 60f); t.y = 3f; cam.transform.LookAt(t); });
        GameObject Nearest(string n, Vector3 to) { GameObject best = null; float bd = 1e9f; foreach (var g in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (g.name == n) { float d = (g.position - to).sqrMagnitude; if (d < bd) { bd = d; best = g.gameObject; } } return best; }
        var house = Nearest("Bldg_estate", Lp(-14f, 100f));
        if (house != null) Shot(cam, game, pre + "estate", () => { Transform h = house.transform; cam.fieldOfView = 48f; Vector3 p = h.position + h.forward * 48f + h.right * 14f; p.y = Ground(p) + 1.7f; cam.transform.position = p; cam.transform.LookAt(h.position + Vector3.up * 4f); });
        if (house != null) Shot(cam, game, pre + "estate_close", () => { Transform h = house.transform; cam.fieldOfView = 50f; Vector3 p = h.position + h.forward * 32f - h.right * 10f; p.y = Ground(p) + 1.7f; cam.transform.position = p; cam.transform.LookAt(h.position + h.forward * 14f + Vector3.up * 2.6f); });
        var dock = GameObject.Find("Bldg_commdock");
        if (dock != null) Shot(cam, game, pre + "commdock", () => { Transform m = dock.transform; cam.fieldOfView = 50f; Vector3 p = m.position + m.forward * 26f + m.right * 12f; p.y = 2.0f; cam.transform.position = p; cam.transform.LookAt(m.position + m.forward * 9f + Vector3.up * 1.5f); });
        var tower = GameObject.Find("Bldg_tower");
        // pass 18: the judges' tower framed left of centre with the tents beyond it (v17 aimed 22 m past it and lost the tower)
        if (tower != null) Shot(cam, game, pre + "dressing", () =>
        {
            Transform t = tower.transform; cam.fieldOfView = 58f; Vector3 p = t.position + t.forward * 30f - t.right * 6f; p.y = Mathf.Max(Ground(p), 0f) + 1.8f; cam.transform.position = p;
            cam.transform.LookAt(t.position - t.right * 9f + Vector3.up * 4f);
            var tb = tower.GetComponentsInChildren<Renderer>(); var bb = tb.Length > 0 ? tb[0].bounds : new Bounds(t.position, Vector3.one); foreach (var rr in tb) bb.Encapsulate(rr.bounds);
            Vector3 v0 = cam.WorldToViewportPoint(bb.center - Vector3.up * bb.extents.y), v1 = cam.WorldToViewportPoint(bb.center + Vector3.up * bb.extents.y);
            if (v0.z <= 0 || v0.x < 0.08f || v0.x > 0.92f || v1.y > 0.95f || v0.y < 0.03f) cam.transform.LookAt(bb.center);
            Debug.Log($"[SkiView] saltwhite dressing: tower viewport base ({v0.x:0.00},{v0.y:0.00}) top ({v1.x:0.00},{v1.y:0.00})");
        });
        Shot(cam, game, pre + "island", () => { cam.fieldOfView = 45f; Vector3 p = Lp(190f, 14f); p.y = 2.2f; cam.transform.position = p; Vector3 t = Lp(222f, -16f); t.y = 2.5f; cam.transform.LookAt(t); });
        Shot(cam, game, pre + "backdrop", () => { cam.fieldOfView = 50f; Vector3 p = Lp(120f, 0f); p.y = 2.0f; cam.transform.position = p; Vector3 t = Lp(-700f, 0f); t.y = 22f; cam.transform.LookAt(t); });
        if (game.jumpRamp != null)
        {
            Transform R = game.jumpRamp; Vector3 f = R.forward; f.y = 0; f.Normalize(); Vector3 side = game.JumpSide;
            Shot(cam, game, pre + "ramp", () => { cam.fieldOfView = 45f; Vector3 p = R.position - side * 16f + f * 9f + Vector3.up * 1.6f; cam.transform.position = p; cam.transform.LookAt(R.position - f * 3f + Vector3.up * 0.6f); });
            // jump run on the Saltwhite ramp (NW -> SE, jumpDir -1): ideal timing, still at the top of the flight from the shore side
            game.autopilot = false;
            game.BeginJumpRun(false); game.jumpCutAt = -4.3f; game.jumpPopAt = 0.86f; game.SimulateFor(1f / 30f);
            side = game.JumpSide;
            StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Flight && game.JumpT >= 0.35f, 20f, 1f / 60f);
            Shot(cam, game, pre + "jump_flight", () => { Vector3 sk = game.SkierPos; Vector3 p = sk + side * 18f - f * 4f; p.y = 1.8f; cam.transform.position = p; cam.fieldOfView = 40f; cam.transform.LookAt(sk + Vector3.up * 0.5f); });
            StepUntil(game, () => game.mode != SkiViewGame.Mode.Jump, 20f, 1f / 60f);
            Debug.Log($"[SkiView] saltwhite jump: result '{game.JumpResult}' distance {game.JumpDistance:0.0} m, jumpDir {game.jumpDir}, ramp {R.position} ({Vector3.Distance(Vector3.ProjectOnPlane(R.position, Vector3.up), f * Vector3.Dot(R.position, f)):0.0} m off the line)");
            game.autopilot = true; game.jumpMode = false; game.SetJumpGear(false);
        }
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2, 400f);
        Shot(cam, game, pre + "boat", () =>
        {
            Vector3 b = game.BoatPos, f = game.BoatFwd, r = Vector3.Cross(Vector3.up, f);
            cam.fieldOfView = 50f; cam.transform.position = b - r * 10.5f + f * 1.0f + Vector3.up * 1.25f;
            cam.transform.LookAt(b + Vector3.up * 0.9f - r * 2f); cam.transform.rotation = Quaternion.Euler(-3f, cam.transform.eulerAngles.y, 0);
        });
        Debug.Log("[SkiView] lake shots saltwhite done");
    }

    /// <summary>Pass 17: cameras_v17 - each player camera mid-pass (at the wake crossing) and at a buoy, same instants.</summary>
    public static void CameraShots()
    {
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.Init();
        var cam = game.cam;
        int pcQ = Array.FindIndex(QualitySettings.names, n => n.Contains("PC")); if (pcQ >= 0) QualitySettings.SetQualityLevel(pcQ, true);
        var modes = new[] { SkiViewGame.CamMode.FirstPerson, SkiViewGame.CamMode.CloseChase, SkiViewGame.CamMode.SkierChase, SkiViewGame.CamMode.Broadcast };
        string Nm(SkiViewGame.CamMode m) => m == SkiViewGame.CamMode.FirstPerson ? "first" : m == SkiViewGame.CamMode.CloseChase ? "close" : m == SkiViewGame.CamMode.SkierChase ? "high" : "boat";
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 2, 400f);
        StepUntil(game, () => Mathf.Abs(game.sim.Lateral) < 1.0f, 10f, 1f / 60f);
        void FpLog(string tag)
        {
            if (game.camMode != SkiViewGame.CamMode.FirstPerson) return;
            game.ApplyVisuals(true);
            Vector3 h = game.fpHandle != null ? cam.WorldToViewportPoint(game.fpHandle.position) : Vector3.zero;
            Vector3 tip = Vector3.zero; float best = -1e9f;
            foreach (var r in game.ski.GetComponentsInChildren<MeshRenderer>()) { var b = r.bounds; for (int i = 0; i < 8; i++) { var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z); float d = Vector3.Dot(c - game.ski.position, game.ski.forward); if (d > best) { best = d; tip = c; } } }
            Vector3 tv = cam.WorldToViewportPoint(game.ski.position + game.ski.forward * best);
            Vector3 boat = cam.WorldToViewportPoint(game.BoatPos + Vector3.up * 1f);
            bool In(Vector3 v) => v.z > 0 && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1;
            Debug.Log($"[SkiView] cam19 first {tag}: handle viewport ({h.x:0.00},{h.y:0.00}) {(In(h) ? "IN" : "OUT")}, ski tip ({tv.x:0.00},{tv.y:0.00}) {(In(tv) ? "IN" : "OUT")}, boat ({boat.x:0.00},{boat.y:0.00}) {(In(boat) ? "IN" : "OUT")}, rig {(game.fpRig != null && game.fpRig.gameObject.activeSelf ? "on" : "off")}");
        }
        foreach (var m in modes) { game.camMode = m; FpLog("mid"); Shot(cam, game, "cam19_" + Nm(m) + "_mid", null); }
        int nb = game.sim.nextBuoy;
        StepUntil(game, () => game.sim.nextBuoy > nb || game.mode != SkiViewGame.Mode.Pass, 10f, 1f / 60f);
        foreach (var m in modes) { game.camMode = m; FpLog("buoy"); Shot(cam, game, "cam19_" + Nm(m) + "_buoy", null); }
        // first person: the hard cut after the ball (edge change, max lean) and the next wake crossing
        game.camMode = SkiViewGame.CamMode.FirstPerson;
        StepUntil(game, () => Mathf.Abs(game.LeanDeg) > 40f || game.mode != SkiViewGame.Mode.Pass, 3f, 1f / 60f);
        FpLog("cut"); Shot(cam, game, "cam19_first_cut", null);
        StepUntil(game, () => Mathf.Abs(game.sim.Lateral) < 1.0f || game.mode != SkiViewGame.Mode.Pass, 3f, 1f / 60f);
        FpLog("wake"); Shot(cam, game, "cam19_first_wake", null);
        // turn + fall sanity (positions only): no camera under the water or inside the skier
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Turn, 60f);
        foreach (var m in modes) { game.camMode = m; game.ApplyVisuals(true); Vector3 p = cam.transform.position; Debug.Log($"[SkiView] cam19 {Nm(m)} in turn: y {p.y:0.00}, {Vector3.Distance(p, game.SkierPos + Vector3.up):0.0} m from the skier"); }
        // pass 19: first person in the air (jump flight), the shore jump camera off so the rider's own view renders
        if (game.jumpRamp != null)
        {
            bool jc0 = game.jumpCam; game.jumpCam = false; game.autopilot = false;
            game.BeginJumpRun(false); game.jumpCutAt = -4.3f; game.jumpPopAt = 0.86f; game.SimulateFor(1f / 30f);
            game.camMode = SkiViewGame.CamMode.FirstPerson;
            StepUntil(game, () => game.jphase == SkiViewGame.JPhase.Flight && game.JumpT >= 0.3f, 25f, 1f / 60f);
            FpLog("jump"); Shot(cam, game, "cam19_first_jump", null);
            StepUntil(game, () => game.mode != SkiViewGame.Mode.Jump, 20f, 1f / 60f);
            game.jumpCam = jc0; game.autopilot = true; game.jumpMode = false; game.SetJumpGear(false);
        }
        game.camMode = SkiViewGame.CamMode.Broadcast;
        Debug.Log("[SkiView] camera shots done");
    }

    /// <summary>Pass 17: draw-call / triangle estimate per LakeDetail tier (0 desktop, 1 WebGL, 2 phone) from the boat camera and the
    /// high chase camera mid-pass. Terrain trees are counted per prototype / LOD / submesh as instanced batches (511 per draw),
    /// the shadow pass and the planar reflection pass are added; scene meshes are counted per visible submesh.</summary>
    public static void LakePerfAll()
    {
        var lines = new List<string>();
        foreach (var id in new[] { "skiview" }.Concat(SkiViewBuilder.LAKES))
            for (int tier = 0; tier <= 2; tier++) lines.AddRange(LakePerf(id, tier));
        File.WriteAllText(Path.Combine(OutDir, "perf_tiers_v19.txt"), string.Join("\n", lines) + "\n");
        Debug.Log("[SkiView] perf tiers:\n" + string.Join("\n", lines));
    }

    static List<string> LakePerf(string id, int tier)
    {
        var res = new List<string>();
        string sp = id == "skiview" ? SkiViewBuilder.SCENE : SkiViewBuilder.LakeScenePath(id);
        if (!File.Exists(sp)) return res;
        EditorSceneManager.OpenScene(sp);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.camMode = SkiViewGame.CamMode.Broadcast; game.Init();
        var cam = game.cam; var terrain = Terrain.activeTerrain;
        string qn = tier == 2 ? "Mobile" : "PC"; int qi = Array.FindIndex(QualitySettings.names, n => n.Contains(qn)); if (qi >= 0) QualitySettings.SetQualityLevel(qi, true);
        var det = UnityEngine.Object.FindFirstObjectByType<LakeDetail>();
        var urp0 = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        float sd0 = urp0 != null ? urp0.shadowDistance : 0f; int sc0 = urp0 != null ? urp0.shadowCascadeCount : 1;
        if (det != null && tier > 0) det.Apply(tier);
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3, 400f);
        foreach (var view in new[] { SkiViewGame.CamMode.Broadcast, SkiViewGame.CamMode.SkierChase, SkiViewGame.CamMode.CloseChase, SkiViewGame.CamMode.FirstPerson })
        {
            game.camMode = view; cam.aspect = 16f / 9f; game.ApplyVisuals(true);
            perfGroups.Clear();
            var st = PerfCount(cam, terrain, QualitySettings.lodBias);
            string top = string.Join(", ", perfGroups.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Key + " " + kv.Value));
            float reflLod = tier == 2 && det != null ? det.mobileReflLodBias : QualitySettings.lodBias;
            var rf = PerfCount(cam, terrain, reflLod, true);
            int draws = st.draws + st.shadowDraws + rf.draws + 40;   // + terrain patches / sky / water / UI (approx)
            long tris = st.tris + st.shadowTris + rf.tris;
            res.Add($"{id,-10} tier {tier} ({qn,-6}) {(view == SkiViewGame.CamMode.Broadcast ? "boat cam  " : view == SkiViewGame.CamMode.SkierChase ? "high chase" : view == SkiViewGame.CamMode.CloseChase ? "close chas" : "1st person")}: trees in view {st.trees,6} (LOD0 {st.lod0,5}), draws ~{draws,4} (main {st.draws}, shadow {st.shadowDraws}, reflection {rf.draws}), tris ~{tris / 1000,5}k (main {st.tris / 1000}k, shadow {st.shadowTris / 1000}k, refl {rf.tris / 1000}k), scene meshes {st.meshes}, plant instances {(det != null && tier > 0 ? LakeDetail.LastKept : (terrain != null ? terrain.terrainData.treeInstanceCount : 0))}; mesh draws by group: {top}");
        }
        game.camMode = SkiViewGame.CamMode.Broadcast;
        if (urp0 != null) { urp0.shadowDistance = sd0; urp0.shadowCascadeCount = sc0; }   // tier 2 changes the URP asset: put it back
        return res;
    }

    static Dictionary<string, int> perfGroups = new Dictionary<string, int>();
    struct PerfStat { public int trees, lod0, draws, shadowDraws, meshes; public long tris, shadowTris; }
    static PerfStat PerfCount(Camera cam, Terrain terrain, float lodBias, bool reflection = false)
    {
        var st = new PerfStat();
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        Vector3 cp = cam.transform.position; float tanH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var urpA = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        float shadowDist = urpA != null ? urpA.shadowDistance : QualitySettings.shadowDistance; int cascades = Mathf.Max(1, urpA != null ? urpA.shadowCascadeCount : QualitySettings.shadowCascades);
        var batches = new Dictionary<long, int>(); var shadowBatches = new Dictionary<long, int>();
        if (terrain != null && terrain.drawTreesAndFoliage)
        {
            var td = terrain.terrainData; var protos = td.treePrototypes; Vector3 tp = terrain.transform.position, size = td.size;
            var info = new (float h, float[] thr, int[][] tris, bool[] shadow)[protos.Length];
            for (int i = 0; i < protos.Length; i++)
            {
                var pf = protos[i].prefab; var lg = pf != null ? pf.GetComponent<LODGroup>() : null;
                if (lg == null) { var mf = pf != null ? pf.GetComponentInChildren<MeshFilter>() : null; info[i] = (mf != null ? mf.sharedMesh.bounds.size.y : 2f, new[] { 0.01f }, new[] { SubTris(mf) }, new[] { true }); continue; }
                var lods = lg.GetLODs(); var thr = new float[lods.Length]; var tr = new int[lods.Length][]; var sh = new bool[lods.Length];
                for (int l = 0; l < lods.Length; l++) { thr[l] = lods[l].screenRelativeTransitionHeight; var r = lods[l].renderers.FirstOrDefault(x => x != null); tr[l] = SubTris(r != null ? r.GetComponent<MeshFilter>() : null); sh[l] = r != null && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off; }
                info[i] = (lg.size, thr, tr, sh);
            }
            float bias = lodBias * terrain.treeLODBiasMultiplier;
            foreach (var t in td.treeInstances)
            {
                Vector3 w = new Vector3(tp.x + t.position.x * size.x, tp.y + t.position.y * size.y, tp.z + t.position.z * size.z);
                if (reflection) w.y = -w.y;   // mirrored about the water plane (approximate planar reflection view)
                float d = Vector3.Distance(w, cp); if (d > terrain.treeDistance) continue;
                var inf = info[t.prototypeIndex]; float hgt = inf.h * t.heightScale;
                if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(w + Vector3.up * (reflection ? -hgt / 2 : hgt / 2), new Vector3(hgt * 0.6f, hgt, hgt * 0.6f)))) continue;
                float rel = hgt / (2f * Mathf.Max(0.1f, d) * tanH) * bias;
                int lod = -1; for (int l = 0; l < inf.thr.Length; l++) if (rel >= inf.thr[l]) { lod = l; break; }
                if (lod < 0) continue;
                st.trees++; if (lod == 0) st.lod0++;
                var subs = inf.tris[lod];
                for (int s = 0; s < subs.Length; s++)
                {
                    long key = ((long)t.prototypeIndex << 16) | ((long)lod << 8) | (long)s;
                    batches[key] = batches.TryGetValue(key, out var n) ? n + 1 : 1; st.tris += subs[s];
                    if (!reflection && inf.shadow[lod] && d < shadowDist) { shadowBatches[key] = shadowBatches.TryGetValue(key, out var m) ? m + 1 : 1; st.shadowTris += subs[s] * cascades; }
                }
            }
        }
        foreach (var kv in batches) st.draws += (kv.Value + 510) / 511;
        foreach (var kv in shadowBatches) st.shadowDraws += (kv.Value + 510) / 511 * cascades;
        int noRefl = (1 << PlanarReflection.NO_REFLECT_LAYER) | (1 << PlanarReflection.FP_LAYER) | (1 << 4);
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (reflection && ((1 << r.gameObject.layer) & noRefl) != 0) continue;
            if (r.gameObject.name == "PlanarReflectionCamera") continue;
            if (!reflection && (cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
            Mesh sm = r is SkinnedMeshRenderer smr ? smr.sharedMesh : (r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null);
            if (sm == null) continue;
            if (r.GetComponentInParent<LODGroup>() != null) continue;
            var b = r.bounds; if (reflection) { var c = b.center; c.y = -c.y; b.center = c; }
            if (Vector3.Distance(b.ClosestPoint(cp), cp) > cam.farClipPlane || !GeometryUtility.TestPlanesAABB(planes, b)) continue;
            var subs = SubTris(sm); st.meshes++; st.draws += subs.Length; foreach (var x in subs) st.tris += x;
            if (!reflection) { var t = r.transform; while (t.parent != null && t.parent.parent != null) t = t.parent; string k = t.name; perfGroups[k] = perfGroups.TryGetValue(k, out var q) ? q + subs.Length : subs.Length; }
            if (!reflection && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off && Vector3.Distance(b.ClosestPoint(cp), cp) < shadowDist) { st.shadowDraws += subs.Length * cascades; foreach (var x in subs) st.shadowTris += x * cascades; }
        }
        // scene LODGroups (home lot houses etc.): the LOD chosen by screen height, its visible renderers
        foreach (var lg in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
        {
            if (!lg.enabled || !lg.gameObject.activeInHierarchy) continue;
            var lods = lg.GetLODs(); if (lods.Length == 0) continue;
            Vector3 wc = lg.transform.TransformPoint(lg.localReferencePoint); if (reflection) wc.y = -wc.y;
            float sc = Mathf.Max(Mathf.Abs(lg.transform.lossyScale.x), Mathf.Abs(lg.transform.lossyScale.y), Mathf.Abs(lg.transform.lossyScale.z));
            float d = Mathf.Max(0.1f, Vector3.Distance(wc, cp)); float rel = lg.size * sc / (2f * d * tanH) * lodBias;
            int li = -1; for (int l = 0; l < lods.Length; l++) if (rel >= lods[l].screenRelativeTransitionHeight) { li = l; break; }
            if (li < 0) continue;
            foreach (var r in lods[li].renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (reflection && ((1 << r.gameObject.layer) & noRefl) != 0) continue;
                if (!reflection && (cam.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                var b = r.bounds; if (reflection) { var c = b.center; c.y = -c.y; b.center = c; }
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                var mf = r.GetComponent<MeshFilter>(); var subs = SubTris(mf); st.meshes++; st.draws += subs.Length; foreach (var x in subs) st.tris += x;
                if (!reflection && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off && Vector3.Distance(b.ClosestPoint(cp), cp) < shadowDist) { st.shadowDraws += subs.Length * cascades; foreach (var x in subs) st.shadowTris += x * cascades; }
                if (!reflection) { var t = r.transform; while (t.parent != null && t.parent.parent != null) t = t.parent; perfGroups[t.name] = perfGroups.TryGetValue(t.name, out var q) ? q + subs.Length : subs.Length; }
            }
        }
        return st;
    }
    static int[] SubTris(MeshFilter mf) => SubTris(mf != null ? mf.sharedMesh : null);
    static int[] SubTris(Mesh m)
    {
        if (m == null) return new int[0]; var o = new int[m.subMeshCount]; for (int i = 0; i < o.Length; i++) o[i] = (int)(m.GetIndexCount(i) / 3); return o;
    }

    /// <summary>Pass 19: pick a ground camera spot round a building whose sight line to it crosses the fewest terrain trees.</summary>
    static Vector3 ClearOfTrees(Vector3 p0, Vector3 aim, Vector3 c, float back)
    {
        var t = Terrain.activeTerrain; if (t == null) return p0;
        var td = t.terrainData; var tp = t.transform.position; var trees = td.treeInstances;
        var pts = new List<Vector2>(trees.Length); foreach (var ti in trees) { var w = Vector3.Scale(ti.position, td.size) + tp; pts.Add(new Vector2(w.x, w.z)); }
        int Hits(Vector3 a, Vector3 b)
        {
            int n = 0; Vector2 A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), AB = B - A; float L2 = Mathf.Max(AB.sqrMagnitude, 1e-3f);
            foreach (var Q in pts)
            {
                float u = Mathf.Clamp01(Vector2.Dot(Q - A, AB) / L2);
                if (u > 0.9f && (Q - B).sqrMagnitude < 36f) continue;   // the building's own garden trees
                if ((A + AB * u - Q).sqrMagnitude < (u < 0.08f ? 25f : 12.25f)) n++;
            }
            return n;
        }
        Vector3 best = p0; int bh = Hits(p0, aim); if (bh == 0) return p0;
        for (int k = 0; k < 24 && bh > 0; k++)
            foreach (float r in new[] { back, back * 0.7f, back * 1.4f })
            {
                float a = k * Mathf.PI / 12f; Vector3 q = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                q.y = Mathf.Max(1.6f, t.SampleHeight(q) + tp.y + 1.7f); int h = Hits(q, aim); if (h < bh) { bh = h; best = q; }
            }
        Debug.Log($"[SkiView] building cam moved {Vector3.Distance(p0, best):0} m to clear trees ({bh} trees left on the sight line)");
        return best;
    }
}
