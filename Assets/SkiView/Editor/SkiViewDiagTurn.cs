using System.IO;
using System.Reflection;
using System.Text;
using SkiView;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Headless check of the home lake turnaround and jump run: autopilot slalom, autopilot jump, player jump with no press and
/// player jump with an early press. Logs boat pose, skier position and the ground under the skier every 0.1 s to
/// Screens/diag/run_*.csv and grabs boat-camera frames through the south island turn and the jump approach (Screens/diag/f_*.png).</summary>
public static partial class SkiViewCapture
{
    static object DF(SkiViewGame g, string n) => typeof(SkiViewGame).GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(g);
    public static void DiagTurnJump()
    {
        Directory.CreateDirectory(Path.Combine(OutDir, "diag"));
        foreach (var run in new[] { "slalom", "slalom_wide", "jump", "jump_player", "jump_early" })
        {
            EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
            var g = Object.FindFirstObjectByType<SkiViewGame>(); g.autopilot = run.StartsWith("slalom") || run == "jump"; g.Init();
            if (run == "slalom_wide") ((SkierSim)DF(g, "sim")).testExitX = 9f;
            if (!run.StartsWith("slalom")) g.ToggleJumpMode();
            var cam = g.cam; var ter = Object.FindFirstObjectByType<Terrain>();
            var sb = new StringBuilder("t,mode,jphase,boatS,bx,bz,by,pitch,roll,sx,sz,sy,ground,runT\n");
            int shots = 0; float lastShot = -9f; bool early = false;
            for (int i = 0; i < (run.StartsWith("slalom") ? 1600 : 2000); i++)
            {
                g.SimulateFor(0.1f); float t = i * 0.1f;
                string mode = DF(g, "mode").ToString(), jp = DF(g, "jphase").ToString(); float bs = (float)DF(g, "boatS");
                if (run == "jump_early" && !early && mode == "Turn" && t > 1f) { g.JumpArm(); early = true; }
                if (run == "jump_early" && mode == "Jump" && jp == "Approach" && (float)DF(g, "runT") < -7f) g.JumpPress();   // too early: kept
                var b = g.boat; var e = b.rotation.eulerAngles; var s = g.SkierPos;
                float gy = ter.SampleHeight(s) + ter.transform.position.y;
                sb.Append($"{t:0.0},{mode},{jp},{bs:0.0},{b.position.x:0.00},{b.position.z:0.00},{b.position.y:0.000},{Mathf.DeltaAngle(0, e.x):0.0},{Mathf.DeltaAngle(0, e.z):0.0},{s.x:0.00},{s.z:0.00},{s.y:0.00},{gy:0.00},{DF(g, "runT"):0.00}\n");
                bool turn = run == "slalom" && bs > 560f && bs < 790f && t < 120f;
                bool appr = run == "jump" && mode == "Jump" && t < 40f;
                if ((turn || appr) && t - lastShot >= (turn ? 2.4f : 0.8f) && shots < 12)
                { Shot(cam, g, $"diag/f_{run}_{shots:00}", null); shots++; lastShot = t; }
            }
            File.WriteAllText(Path.Combine(OutDir, $"diag/run_{run}.csv"), sb.ToString());
        }
        Debug.Log("[SkiView] diag done");
    }
}
