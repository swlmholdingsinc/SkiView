using System.Linq; using System.Text; using System.IO;
using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement;
using SkiView;

/// <summary>b9.1b: runtime check of Ski View jump mode: which buoy renderers are visible (default 9 / tournament 13 / slalom 0), plus shots.</summary>
public static partial class SkiViewCapture
{
    public static void JumpBuoyCheck()
    {
        var sb = new StringBuilder();
        EditorSceneManager.OpenScene("Assets/SkiView/Scenes/SkiViewLake.unity");
        var g = Object.FindFirstObjectByType<SkiViewGame>(); g.Init();
        foreach (int tourn in new[] { 0, 1 })
        {
            PlayerPrefs.SetInt(SkiViewGame.TOURN_JUMP_PREF, tourn);
            if (!g.jumpMode) g.ToggleJumpMode();
            g.SimulateFor(1f); g.ApplyVisuals(true);
            var vis = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var jb = vis.Where(r => r.name.StartsWith("JB_")).Select(r => r.name).OrderBy(n => n).ToArray();
            var sl = vis.Where(r => r.transform.GetComponentsInParent<Transform>(true).Any(p => p.name == "SlalomCourse")).ToArray();
            var oldJc = vis.Where(r => r.transform.GetComponentsInParent<Transform>(true).Any(p => p.name == "JumpCourse")).ToArray();
            sb.AppendLine($"tournament={tourn}: jumpBuoys={jb.Length} [{string.Join(",", jb)}] slalomRenderers={sl.Length} oldJumpCourseRenderers={oldJc.Length} jumpMode={g.jumpMode} gear={g.JumpGearOn}");
            var cam = g.cam; string tag = tourn == 1 ? "tourn" : "default";
            Shot(cam, g, $"rigcheck/jump_{tag}_top", () => { cam.transform.position = new Vector3(5f, 230f, 40f); cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); cam.fieldOfView = 60f; cam.farClipPlane = 1000f; });
            Shot(cam, g, $"rigcheck/jump_{tag}_approach", () => { cam.transform.position = new Vector3(30f, 9f, 175f); cam.transform.rotation = Quaternion.LookRotation(new Vector3(-0.16f, -0.08f, -1f)); cam.fieldOfView = 50f; });
            g.ToggleJumpMode();
        }
        PlayerPrefs.SetInt(SkiViewGame.TOURN_JUMP_PREF, 0);
        Directory.CreateDirectory("Screens/rigcheck"); File.WriteAllText("Screens/rigcheck/jumpbuoys.txt", sb.ToString()); Debug.Log("[SkiView] jumpbuoys\n" + sb);
    }
}
