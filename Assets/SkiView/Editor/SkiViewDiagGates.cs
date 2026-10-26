using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using SkiView;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Gate / scoring check with human-like input (binary left/right keys, reaction delay, early/late cuts, wide and narrow
/// lines, angled gate crossings). Logs every gate and buoy decision with the skier, boat, ski and drawn-ball positions in the
/// scoring frame to Screens/diag/gates_human.txt, plus a visual-vs-logic check of every drawn course ball.</summary>
public static partial class SkiViewCapture
{
    class Human
    {
        public string name; public float delay, gateCut, width, turnLead, exitAim, jitter; public int entrySide; public System.Random rnd;
        readonly Queue<Vector3> hist = new Queue<Vector3>();
        float jit;
        public float Steer(SkierSim s, int nTick)
        {
            hist.Enqueue(new Vector3(s.x, s.z, s.Lateral)); int lag = Mathf.Max(1, Mathf.RoundToInt(delay * 60f));
            while (hist.Count > lag) hist.Dequeue();
            var o = hist.Peek(); float x = o.x, z = o.y;   // what the player "sees", delayed
            if (nTick % 30 == 0) jit = (float)(rnd.NextDouble() * 2 - 1) * jitter;
            float target;
            if (!s.gatesCleared) target = z > gateCut + jit ? entrySide * 7f : -entrySide * width;   // hang out on one side, then cut across the gates
            else if (s.nextBuoy < 6)
            {
                int k = s.nextBuoy; float side = SkierSim.BuoyX(k) > 0 ? 1 : -1;
                float bz = SkierSim.BuoyZ(k);
                target = side * width;
                if (k > 0 && z > SkierSim.BuoyZ(k - 1) - turnLead - jit) target = -side * width;   // still finishing the previous turn
            }
            else target = exitAim;
            float err = target - x;
            if (Mathf.Abs(err) < 0.8f) return 0f;
            return err > 0 ? -1f : 1f;   // +1 = left; binary keys like a player
        }
    }

    public static void DiagGatesHuman()
    {
        var outp = Path.Combine(OutDir, "diag"); Directory.CreateDirectory(outp);
        var sb = new StringBuilder();
        var players = new List<Human>();
        var rr = new System.Random(17);
        string[] names = { "steady", "late_cut", "early_cut", "wide_line", "narrow_line", "angled_exit", "lazy_exit", "slow_reflex", "jittery" };
        float[,] P = { // delay, gateCut(z m), width, turnLead, exitAim, jitter, entrySide
            {0.20f, 10f, 13.0f, 4f, 0f, 0.5f, 1}, {0.25f, 3f, 13f, 2f, 0f, 1f, -1}, {0.20f, 22f, 12.5f, 7f, 0f, 1f, 1}, {0.20f, 10f, 15f, 4f, 0f, 1f, -1},
            {0.20f, 10f, 10.5f, 4f, 0f, 1f, 1}, {0.20f, 10f, 13f, 4f, 6f, 1f, -1}, {0.20f, 10f, 13f, 4f, 999f, 1f, 1}, {0.45f, 10f, 13f, 3f, 0f, 1.5f, -1}, {0.15f, 10f, 13f, 4f, 0f, 3f, 1} };
        for (int i = 0; i < names.Length; i++) players.Add(new Human { name = names[i], delay = P[i, 0], gateCut = P[i, 1], width = P[i, 2], turnLead = P[i, 3], exitAim = P[i, 4], jitter = P[i, 5], entrySide = (int)P[i, 6], rnd = new System.Random(rr.Next()) });
        var fSteer = typeof(SkiViewGame).GetField("steerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        bool first = true;
        foreach (var h in players)
        {
            EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
            var g = Object.FindFirstObjectByType<SkiViewGame>(); g.autopilot = false; g.Init();
            var log = new List<string>();
            SkierSim.GateLog = m => log.Add(m);
            if (first)
            {   // drawn balls vs the scoring frame, both directions
                first = false;
                foreach (int dir in new[] { 1, -1 })
                {
                    Vector3 cf = g.path.CourseFwd(dir), right = Vector3.Cross(Vector3.up, cf);
                    sb.Append($"== drawn course balls in the scoring frame, {(dir > 0 ? "southbound" : "northbound")} ==\n");
                    foreach (Transform t in GameObject.Find("SlalomCourse").GetComponentsInChildren<Transform>(true))
                    {
                        if (t.childCount > 0 || !(t.name.StartsWith("Gate") || t.name.StartsWith("Turn"))) continue;
                        Vector3 p = t.GetComponent<Renderer>() != null ? t.GetComponent<Renderer>().bounds.center : t.position;
                        sb.Append($"  {t.name,-9} world ({p.x:0.00},{p.z:0.00}) -> x {Vector3.Dot(p, right):0.00} z {-(Vector3.Dot(p, cf) + SkierSim.COURSE_LENGTH * 0.5f):0.00}\n");
                    }
                }
                var mf = GameObject.Find("SlalomCourse").GetComponentsInChildren<MeshFilter>(true);
                sb.Append($"  (course objects: {mf.Length} mesh filters; merged meshes report their bounds centre)\n");
            }
            sb.Append($"\n== player '{h.name}' delay {h.delay}s cut@{h.gateCut}m width {h.width} lead {h.turnLead} exitAim {h.exitAim} jitter {h.jitter} entry side {h.entrySide} ==\n");
            int passes = 0, tick = 0; string lastMode = "";
            for (int s = 0; s < 60 * 240 && passes < 2; s++, tick++)
            {
                bool inPass = g.mode == SkiViewGame.Mode.Pass;
                fSteer.SetValue(g, inPass ? h.Steer(g.sim, tick) : 0f);
                int before = log.Count;
                g.SimulateFor(1f / 60f);
                for (int k = before; k < log.Count; k++)
                {
                    Vector3 cf = g.path.CourseFwd(g.passDir), right = Vector3.Cross(Vector3.up, cf);
                    Vector3 sk = g.SkierPos, b = g.BoatPos, ski = (GameObject.Find("Skier") is GameObject sg ? sg.transform.position : sk);
                    sb.Append($"  [{g.passes + 1}] {log[k]} | boat x {Vector3.Dot(b, right):0.00} | ski-vs-sim lateral {Vector3.Dot(ski - sk, right):+0.00;-0.00} m | HUD buoys {g.sim.passBuoys:0.#} msg '{g.sim.message}'\n");
                }
                string m = g.mode.ToString();
                if (lastMode == "Pass" && m != "Pass") { passes++; sb.Append($"  [{g.passes}] END -> {m}: banner '{g.banner}' score {g.sim.Score()}\n"); }
                lastMode = m;
            }
        }
        SkierSim.GateLog = null;
        File.WriteAllText(Path.Combine(outp, "gates_human.txt"), sb.ToString());
        Debug.Log("[SkiView] gates human done");
    }

    public static void DiagPassResult()
    {
        var sb = new StringBuilder();
        var fSteer = typeof(SkiViewGame).GetField("steerInput", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (int caseN in new[] { 0, 1, 2 })
        {
            EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
            var g = Object.FindFirstObjectByType<SkiViewGame>(); g.autopilot = true; g.Init();
            typeof(SkiViewGame).GetMethod("BuildGateDebug", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(g, null);
            var log = new List<string>(); SkierSim.GateLog = m => log.Add(m);
            int rope0 = -1; string prev = ""; bool done = false;
            for (int s = 0; s < 60 * 300 && !done; s++)
            {
                if (g.mode == SkiViewGame.Mode.Pass && rope0 < 0) rope0 = g.sim.ropeIndex;
                if (caseN == 1 && g.sim.awaitingExit) { g.autopilot = false; fSteer.SetValue(g, 1f); }   // never cuts back: wide out the LEFT side
                if (caseN == 2 && g.sim.awaitingExit) { g.autopilot = false; fSteer.SetValue(g, -1f); }   // cuts back across, then out the RIGHT side   // hold out wide past the exit gates
                g.SimulateFor(1f / 60f);
                if (prev == "Pass" && g.mode.ToString() != "Pass")
                {
                    var r = g.sim.last;
                    sb.Append($"{(caseN == 0 ? "CLEAN" : caseN == 1 ? "MISS-EXIT-LEFT" : "MISS-EXIT-RIGHT")}: last.buoys {r.buoys} rounded {r.rounded} exitGate {r.exitGate} clean {r.clean} | results text '{SkierSim.Fmt(r.buoys)}' breakdown '{g.sim.Breakdown(r)}' | rope {rope0} -> {g.sim.ropeIndex} | banner '{g.banner}'\n");
                    bool ok = caseN > 0 ? (Mathf.Abs(r.buoys - (caseN == 1 ? 5.5f : 6f)) < 0.01f && !r.clean && g.sim.ropeIndex == rope0) : (r.buoys == 6f && g.sim.ropeIndex == rope0 + 1);
                    sb.Append(ok ? "  OK\n" : "  FAIL\n"); done = true;
                }
                prev = g.mode.ToString();
            }
            foreach (var l in log) sb.Append("  gate: " + l + "\n");
        }
        SkierSim.GateLog = null;
        File.WriteAllText(Path.Combine(OutDir, "diag", "pass_result.txt"), sb.ToString());
        Debug.Log("[SkiView] pass result done");
    }

    public static void DiagBuoysPhone()
    {
        DiagPassResult();
        var dir = Path.Combine(OutDir, "diag", "buoys"); Directory.CreateDirectory(dir);
        foreach (bool after in new[] { false, true })
        {
            SkiViewGame.BuoyVisOff = !after; SkiViewGame.BuoyScreenH = 540f;
            EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
            var g = Object.FindFirstObjectByType<SkiViewGame>(); g.autopilot = true; g.Init();
            var det = Object.FindFirstObjectByType<LakeDetail>(); if (det != null) { det.forceTier = 2; det.Apply(2); }
            int qi = System.Array.FindIndex(QualitySettings.names, n => n.Contains("Mobile")); if (qi >= 0) QualitySettings.SetQualityLevel(qi, true);
            int guard = 0; while (!(g.mode == SkiViewGame.Mode.Pass && g.sim.nextBuoy >= 1) && guard++ < 30000) g.SimulateFor(1f / 30f);
            string tag = after ? "after" : "before"; var cf = g.path.CourseFwd(g.passDir);
            SkiViewGame.BuoyScreenH = H;
            Shot(g.cam, g, $"diag/buoys/phone_{tag}_chase", null);
            Debug.Log($"[SkiView] buoyvis {tag}: turn1 scale {GameObject.Find("Turn1_vis")?.transform.localScale.x:0.00} vis {(GameObject.Find("Turn1_vis") != null)} lossy {GameObject.Find("Turn1")?.transform.lossyScale.x:0.00} ");
            Shot(g.cam, g, $"diag/buoys/phone_{tag}_course", () => { g.cam.transform.position = -cf * 150f + Vector3.up * 5f; g.cam.transform.LookAt(-cf * 70f); });
        }
        SkiViewGame.BuoyVisOff = false; SkiViewGame.BuoyScreenH = -1f;
        Debug.Log("[SkiView] buoys phone done");
    }

    /// <summary>Buoy look before/after (Scott 5:54): phone tier, mid-pass, CLOSE and HIGH chase. SV_TAG names the set.</summary>
    public static void DiagBuoysCams()
    {
        string tag = System.Environment.GetEnvironmentVariable("SV_TAG") ?? "now";
        Directory.CreateDirectory(Path.Combine(OutDir, "diag", "buoys2"));
        foreach (var cm in new[] { SkiViewGame.CamMode.CloseChase, SkiViewGame.CamMode.SkierChase })
        {
            SkiViewGame.BuoyVisOff = false; SkiViewGame.BuoyScreenH = 540f;
            EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
            var g = Object.FindFirstObjectByType<SkiViewGame>(); g.autopilot = true; g.Init();
            var det = Object.FindFirstObjectByType<LakeDetail>(); if (det != null) { det.forceTier = 2; det.Apply(2); }
            int qi = System.Array.FindIndex(QualitySettings.names, n => n.Contains("Mobile")); if (qi >= 0) QualitySettings.SetQualityLevel(qi, true);
            g.SetCamera(cm);
            int guard = 0; while (!(g.mode == SkiViewGame.Mode.Pass && g.sim.nextBuoy >= 2) && guard++ < 30000) g.SimulateFor(1f / 30f);
            SkiViewGame.BuoyScreenH = H;
            Shot(g.cam, g, $"diag/buoys2/{tag}_{(cm == SkiViewGame.CamMode.CloseChase ? "close" : "high")}", null);
        }
        SkiViewGame.BuoyScreenH = -1f;
        Debug.Log("[SkiView] buoys cams done " + tag);
    }

    /// <summary>Ground / turn-island audit (Scott 6:06): desktop (tier 0) and phone (tier 2), CLOSE / HIGH / 1ST / BOAT mid-pass,
    /// plus island close-ups (low from the water and aerial) for both islands. SV_TAG names the set (before / after).</summary>
    public static void DiagGround()
    {
        string tag = System.Environment.GetEnvironmentVariable("SV_TAG") ?? "now";
        Directory.CreateDirectory(Path.Combine(OutDir, "diag", "ground"));
        var vl = JsonUtility.FromJson<VenueLite>(File.ReadAllText(Path.Combine(Application.dataPath, "SkiView/Generated/venue.json")));
        foreach (int tier in new[] { 0, 2 })
        {
            string tn = tier == 0 ? "desktop" : "phone";
            foreach (var cm in new[] { SkiViewGame.CamMode.CloseChase, SkiViewGame.CamMode.SkierChase, SkiViewGame.CamMode.FirstPerson, SkiViewGame.CamMode.Broadcast })
            {
                var g = OpenTier(tier); g.SetCamera(cm);
                int guard = 0; while (!(g.mode == SkiViewGame.Mode.Pass && g.sim.nextBuoy >= 2) && guard++ < 30000) g.SimulateFor(1f / 30f);
                Shot(g.cam, g, $"diag/ground/{tag}_{tn}_{SkiViewUI_Name(cm)}", null);
            }
            for (int k = 0; k < 2; k++)
            {
                var c = new Vector3(vl.islandCentres[k * 2], 0, vl.islandCentres[k * 2 + 1]);
                var g = OpenTier(tier); g.SimulateFor(0.1f);
                Vector3 toLake = new Vector3(-c.x, 0, -c.z).normalized;   // look at the island from the lake side (course is at the origin)
                Shot(g.cam, g, $"diag/ground/{tag}_{tn}_island{k + 1}_low", () => { g.cam.fieldOfView = 50f; g.cam.transform.position = c + toLake * 75f + Vector3.up * 2.5f; g.cam.transform.LookAt(c + Vector3.up * 4f); });
                Shot(g.cam, g, $"diag/ground/{tag}_{tn}_island{k + 1}_aerial", () => { g.cam.fieldOfView = 50f; g.cam.transform.position = c + toLake * 70f + Vector3.up * 55f; g.cam.transform.LookAt(c); });
            }
        }
        Debug.Log("[SkiView] ground diag done " + tag);
    }
    static string SkiViewUI_Name(SkiViewGame.CamMode m) => m == SkiViewGame.CamMode.CloseChase ? "close" : m == SkiViewGame.CamMode.SkierChase ? "high" : m == SkiViewGame.CamMode.FirstPerson ? "1st" : "boat";
    static SkiViewGame OpenTier(int tier)
    {
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var g = Object.FindFirstObjectByType<SkiViewGame>(); g.autopilot = true; g.Init();
        var det = Object.FindFirstObjectByType<LakeDetail>(); if (det != null) { det.forceTier = tier; det.Apply(tier); }
        int qi = System.Array.FindIndex(QualitySettings.names, n => n.Contains(tier >= 2 ? "Mobile" : "PC")); if (qi >= 0) QualitySettings.SetQualityLevel(qi, true);
        return g;
    }
}
