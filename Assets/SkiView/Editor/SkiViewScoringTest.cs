using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SkiView;

/// <summary>b9 scoring regression + b10.21 short-line ladder.
/// Autopilot must still clear 15–35; 38+ may fail (short-line cliff). Run: unity_run.sh SkiViewScoringTest.Run</summary>
public static class SkiViewScoringTest
{
    enum Force { None, MissBall3, MissEntry, MissExit }

    struct Out { public float buoys; public bool done; public string why; public float[] ballX; public float maxReach; }

    static Out RunPass(int rope, Force f)
    {
        var s = new SkierSim { ropeIndex = rope, ropeM = SkierSim.ROPE_M[rope], theta = 0.05f };
        const float dt = 1f / 120f, v = 15.2f;
        float boatZ = 70f; var bx = new List<float>(); float maxR = 0f;
        s.BeginPass();
        if (f == Force.MissExit) s.testExitX = 6f;
        for (int k = 0; k < 120 * 40 && !s.fallen && !s.passDone; k++)
        {
            float steer = s.AutoSteer(true);
            float zs = boatZ + s.Behind;
            if (f == Force.MissEntry && zs > -2f) steer = Mathf.Clamp(-0.5f * (4f - s.Lateral), -1f, 1f);
            if (f == Force.MissBall3 && s.nextBuoy == 2) steer = Mathf.Clamp(-0.5f * (0f - s.Lateral), -1f, 1f);
            s.Step(dt, steer, 1f);
            boatZ -= v * dt;
            int nb = s.nextBuoy; float pz = s.z;
            s.CheckCourse(s.Lateral, boatZ + s.Behind);
            if (nb < 6 && pz >= SkierSim.BuoyZ(nb) && s.z < SkierSim.BuoyZ(nb) && s.gatesCleared) bx.Add(s.x * Mathf.Sign(SkierSim.BuoyX(nb)));
            maxR = Mathf.Max(maxR, Mathf.Abs(s.Lateral));
            if (boatZ + s.Behind < SkierSim.EXIT_Z - 20f) break;
        }
        return new Out { buoys = s.passBuoys, done = s.passDone, why = s.message, ballX = bx.ToArray(), maxReach = maxR };
    }

    public static void Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { Debug.Log($"[ScoreTest] {(ok ? "PASS" : "FAIL")} {what}"); if (!ok) fails++; }
        var sb = new StringBuilder();
        sb.AppendLine("# b10.21 short-line ladder (autopilot @ 34 mph)");
        sb.AppendLine("| Rope | PhysMul | GateExtra | BuoyGrace | Need | Autopilot | Buoys | MaxReach |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (int rope in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var probe = new SkierSim { ropeIndex = rope };
            float mul = probe.RopePhysicsMul(), gate = probe.GateExtraM(), need = probe.BuoyClearNeed();
            float grace = SkierSim.BUOY_OFFSET - need;
            var o = RunPass(rope, Force.None);
            int rounded = 0; foreach (var x in o.ballX) { if (x >= need) rounded++; else break; }
            // b10.21: require full pass only through 35 off; 38+ cliff may fail autopilot
            if (rope <= 4)
                Check(o.done, $"{SkierSim.ROPE_LABEL[rope]} autopilot full pass (required ≤35)");
            else
                Debug.Log($"[ScoreTest] INFO {SkierSim.ROPE_LABEL[rope]} short-line: full={o.done} buoys={o.buoys:0.0} (cliff OK if fail)");
            Check(Mathf.Floor(o.buoys) <= rounded + 1e-3f || !o.done,
                $"{SkierSim.ROPE_LABEL[rope]} judge: {o.buoys:0.0} buoys, full={o.done}, ball x [{string.Join(", ", System.Array.ConvertAll(o.ballX, x => x.ToString("0.00")))}], max |x| {o.maxReach:0.00} · {o.why}");
            sb.AppendLine($"| {SkierSim.ROPE_LABEL[rope]} | {mul:0.00} | {gate:0.00} | {grace:0.00} | {need:0.00} | {(o.done ? "FULL" : "fail")} | {o.buoys:0.0} | {o.maxReach:0.00} |");
        }
        foreach (int rope in new[] { 6, 7 })
        {
            var m = RunPass(rope, Force.MissBall3); Check(!m.done && m.buoys <= 2.0f + 1e-3f, $"{SkierSim.ROPE_LABEL[rope]} forced miss of ball 3: {m.buoys:0.0} (want <= 2.0, no full pass) · {m.why}");
            var g = RunPass(rope, Force.MissEntry); Check(!g.done && g.buoys == 0f, $"{SkierSim.ROPE_LABEL[rope]} entry gate missed (4 m wide): {g.buoys:0.0} (want 0) · {g.why}");
            var e = RunPass(rope, Force.MissExit); Check(!e.done, $"{SkierSim.ROPE_LABEL[rope]} exit gate missed (6 m wide): {e.buoys:0.0}, full={e.done} (want no full pass) · {e.why}");
        }
        var c = RunPass(2, Force.None); Check(c.done && c.buoys >= 5.99f, $"28 OFF autopilot runs a full pass: {c.buoys:0.0} full={c.done}");
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/polish"));
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "b10.21_shortline_ladder.md"), sb.ToString());
        Debug.Log($"[ScoreTest] wrote Screens/polish/b10.21_shortline_ladder.md · {fails} failure(s)");
        if (Application.isBatchMode) EditorApplicationExit(fails);
    }
    static void EditorApplicationExit(int fails) => UnityEditor.EditorApplication.Exit(fails == 0 ? 0 : 3);
}
