using System.IO;
using System.Linq;
using System.Text;
using SkiView;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>b9.1 turnaround check (Scott Oct 23): autopilot slalom for 6 min on the home lake and every built lake scene. After each
/// completed pass the boat must stay on the baked loop round the end island (no teleport: boat step &lt;= 4 m per 0.1 s, no skip), the
/// skier towed through (no sink), and the next pass must start in the other direction. Run: unity_run.sh SkiViewCapture.TurnCheck</summary>
public static partial class SkiViewCapture
{
    public static void TurnCheck()
    {
        var sum = new StringBuilder();
        string only = System.Environment.GetEnvironmentVariable("SV_LAKES");
        foreach (var sc in SkiViewBuilder.LakeScenePaths())
        {
            if (!string.IsNullOrEmpty(only) && !only.Split(',').Any(sc.Contains)) continue;
            EditorSceneManager.OpenScene(sc);
            int.TryParse(System.Environment.GetEnvironmentVariable("SV_ROPE") ?? "0", out int rope0);   // reach v2: SV_ROPE=7 runs the check at 41 off
            SkiViewGame.SaveSetup(0, rope0, 34);
            var g = Object.FindFirstObjectByType<SkiViewGame>(); if (g == null || g.jumpOnly) { sum.AppendLine($"{sc}: skipped (jump only / no game)"); continue; }
            g.autopilot = true; g.Init();
            Vector3 prev = g.BoatPos; int teleports = 0, sinks = 0, starts = 0, ends = 0, dirFlips = 0, turnsDone = 0; float maxStep = 0f;
            var prevMode = g.mode; Vector3 passFwd = Vector3.zero; float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f; int skip0 = g.SkipCount; int lastPh = 0; Vector3 prevSk = g.SkierPos; float maxSkJump = 0f; int nShots = 0;
            for (int i = 0; i < 3600; i++)
            {
                g.SimulateFor(0.1f);
                float st = (g.BoatPos - prev).magnitude; maxStep = Mathf.Max(maxStep, st); if (st > 4f) teleports++; prev = g.BoatPos;
                if (g.mode == SkiViewGame.Mode.Sink && g.DropPhase == 0) sinks++;
                if (g.DropPhase != lastPh) { Debug.Log($"[SkiView] turncheck {Path.GetFileName(sc)} t={i * 0.1f:0.0} dropPhase {lastPh}->{g.DropPhase} mode={g.mode} boat={g.BoatPos} skier={g.SkierPos} rope={g.sim.ropeIndex}"); lastPh = g.DropPhase; }
                { float sj = (g.SkierPos - prevSk).magnitude; if (g.mode == SkiViewGame.Mode.DeepStart) maxSkJump = Mathf.Max(maxSkJump, sj); if (sj > 5f) Debug.Log($"[SkiView] turncheck skjump {sj:0.0}m t={i * 0.1f:0.0} mode {prevMode}->{g.mode} drop={g.DropPhase} from={prevSk} to={g.SkierPos} boat={g.BoatPos}"); }
                prevSk = g.SkierPos;
                if (System.Environment.GetEnvironmentVariable("SV_SHOTS") == "1" && ends == 1 && (g.DropPhase > 0 || g.mode == SkiViewGame.Mode.DeepStart) && i % 10 == 0 && nShots < 20) { Shot(g.cam, g, $"rigcheck/drop_{Path.GetFileNameWithoutExtension(sc)}_{nShots++:00}", null); }
                if (g.InTurnaround) { minX = Mathf.Min(minX, g.BoatPos.x); maxX = Mathf.Max(maxX, g.BoatPos.x); minZ = Mathf.Min(minZ, g.BoatPos.z); maxZ = Mathf.Max(maxZ, g.BoatPos.z); }
                if (g.mode == SkiViewGame.Mode.Pass && prevMode != SkiViewGame.Mode.Pass)
                {
                    starts++; Vector3 f = g.boat.forward; f.y = 0; f.Normalize();
                    if (passFwd != Vector3.zero && Vector3.Dot(f, passFwd) < -0.8f) dirFlips++;
                    if (prevMode == SkiViewGame.Mode.Turn && ends > 0) turnsDone++;
                    passFwd = f;
                }
                if (prevMode == SkiViewGame.Mode.Pass && g.mode != SkiViewGame.Mode.Pass) { ends++; Debug.Log($"[SkiView] turncheck {Path.GetFileName(sc)} pass end t={i * 0.1f:0.0} mode={g.mode} turnaround={g.turnaround} msg={g.sim.message} rope={SkierSim.ROPE_LABEL[g.sim.last.rope]} score={SkierSim.Fmt(g.sim.last.buoys)}"); }
                prevMode = g.mode;
            }
            string line = $"{Path.GetFileNameWithoutExtension(sc)}: passStarts={starts} passEnds={ends} oppositeDirStarts={dirFlips} turnsThenPass={turnsDone} teleports={teleports} maxSkierStepInDWS={maxSkJump:0.00}m/0.1s maxBoatStep={maxStep:0.00}m sinkTicks={sinks} skips={g.SkipCount - skip0} turnBox=x[{minX:0},{maxX:0}] z[{minZ:0},{maxZ:0}]";
            sum.AppendLine(line); Debug.Log("[SkiView] turncheck " + line);
        }
        Directory.CreateDirectory(Path.Combine(OutDir, "rigcheck"));
        File.WriteAllText(Path.Combine(OutDir, "rigcheck/turncheck.txt"), sum.ToString());
        Debug.Log("[SkiView] turncheck done");
    }
}
