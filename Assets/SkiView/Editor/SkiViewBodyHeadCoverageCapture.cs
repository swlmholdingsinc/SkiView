using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SkiView;

/// <summary>
/// Same static pose as the isolation run. Renders before and after the body head-island clip.
/// Male and female, front, side, rear, and the collar boundary. Does not save assets.
///
/// Unity.exe -batchmode -projectPath &lt;staged&gt; -executeMethod SkiViewBodyHeadCoverageCapture.Run -logFile body-head-coverage.log -quit
/// </summary>
public static class SkiViewBodyHeadCoverageCapture
{
    const string ScenePath = "Assets/SkiView/Scenes/SkiViewLake.unity";

    public static void Run()
    {
        int code = 0;
        try { code = Capture(); }
        catch (System.Exception ex)
        {
            Debug.LogError("[HeadCoverage] " + ex);
            code = 5;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static int Capture()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[HeadCoverage] no graphics device. Do not pass -nographics.");
            return 2;
        }
        bool allowOther = Environment.GetEnvironmentVariable("SV_CAPTURE_ALLOW_NON_D3D11") == "1";
        if (!allowOther && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
        {
            Debug.LogError("[HeadCoverage] graphicsDeviceType=" + SystemInfo.graphicsDeviceType);
            return 2;
        }
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null || pipeline.GetType().Name.IndexOf("Universal", System.StringComparison.Ordinal) < 0)
        {
            Debug.LogError("[HeadCoverage] URP is not active.");
            return 2;
        }
        if (!File.Exists(ScenePath)) { Debug.LogError("[HeadCoverage] missing scene"); return 3; }
        EditorSceneManager.OpenScene(ScenePath);
        var game = Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null || game.skier == null || game.rig == null || game.rig.animator == null)
        {
            Debug.LogError("[HeadCoverage] no skier rig");
            return 3;
        }
        var setAthlete = game.GetType().GetMethod("SetAthlete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
        if (setAthlete == null) { Debug.LogError("[HeadCoverage] SetAthlete missing"); return 3; }

        string dir = Environment.GetEnvironmentVariable("SV_HEAD_OUT");
        if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/body-head-coverage"));
        Directory.CreateDirectory(dir);

        var camGo = new GameObject("BodyHeadCoverageCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.aspect = 1280f / 720f;
        if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
            camGo.AddComponent<UniversalAdditionalCameraData>();

        var shots = new StringBuilder();
        int removed = 0;
        var bodyMeshes = new List<KeyValuePair<SkinnedMeshRenderer, Mesh>>();
        var scanRoot = game.rig.animator != null ? game.rig.animator.transform : game.skier;
        foreach (var smr in scanRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null || smr.sharedMesh == null) continue;
            if (smr.name.IndexOf("Body_LOD", System.StringComparison.Ordinal) < 0) continue;
            bodyMeshes.Add(new KeyValuePair<SkinnedMeshRenderer, Mesh>(smr, smr.sharedMesh));
        }
        try
        {
            setAthlete.Invoke(game, new object[] { 0 });
            RestoreBody(bodyMeshes);
            Shoot(game, cam, dir, "male_before", shots);
            setAthlete.Invoke(game, new object[] { 1 });
            RestoreBody(bodyMeshes);
            Shoot(game, cam, dir, "female_before", shots);
            removed = SkiViewBodyHeadCoverage.Apply(scanRoot);
            setAthlete.Invoke(game, new object[] { 0 });
            Shoot(game, cam, dir, "male_after", shots);
            setAthlete.Invoke(game, new object[] { 1 });
            Shoot(game, cam, dir, "female_after", shots);
        }
        finally { Object.DestroyImmediate(camGo); }

        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"playMode\": false,\n");
        sb.Append("  \"playerFixed\": false,\n");
        sb.Append("  \"trianglesRemoved\": ").Append(removed).Append(",\n");
        sb.Append("  \"uv\": \"u in [0.505, 0.892] and v > 0.505, the island EnsureFace125 already names\",\n");
        sb.Append("  \"assetSaved\": false,\n");
        sb.Append("  \"vestEdited\": false,\n");
        sb.Append("  \"shots\": [\n").Append(shots).Append("\n  ]\n}\n");
        File.WriteAllText(Path.Combine(dir, "coverage.json"), sb.ToString());
        Debug.Log("[HeadCoverage] " + dir + " trianglesRemoved=" + removed);
        return removed > 0 ? 0 : 4;
    }

    static void RestoreBody(List<KeyValuePair<SkinnedMeshRenderer, Mesh>> saved)
    {
        for (int i = 0; i < saved.Count; i++)
            if (saved[i].Key != null) saved[i].Key.sharedMesh = saved[i].Value;
    }

    static void Shoot(SkiViewGame game, Camera cam, string dir, string prefix, StringBuilder shots)
    {
        var anim = game.rig.animator;
        var head = anim.GetBoneTransform(HumanBodyBones.Head);
        var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
        if (head == null) return;
        Vector3 flatFwd = head.forward; flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 1e-4f) flatFwd = Vector3.forward;
        flatFwd.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, flatFwd);
        SkiViewCharacterCapture125.Frame(cam, head);
        One(cam, dir, prefix + "_front", shots);
        Aim(cam, head.position, side, 0.94f);
        One(cam, dir, prefix + "_side", shots);
        Aim(cam, head.position, -flatFwd, 0.94f);
        One(cam, dir, prefix + "_rear", shots);
        Vector3 collar = neck != null ? Vector3.Lerp(neck.position, head.position, 0.35f) : head.position + Vector3.down * 0.12f;
        Aim(cam, collar, flatFwd + Vector3.up * 0.15f, 1.55f);
        One(cam, dir, prefix + "_collar", shots);
    }

    static void Aim(Camera cam, Vector3 aim, Vector3 dir, float dist)
    {
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        dir.Normalize();
        Vector3 pos = aim + dir * dist;
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(aim - pos, Vector3.up));
    }

    static void One(Camera cam, string dir, string name, StringBuilder shots)
    {
        uint hash; float mean, stdev; int bytes;
        string err = SkiViewCharacterCapture125.Render(cam, Path.Combine(dir, name + ".png"), out hash, out mean, out stdev, out bytes);
        if (shots.Length > 0) shots.Append(",\n");
        shots.Append("    {\"name\": \"").Append(name).Append("\", \"bytes\": ").Append(bytes).Append(", \"error\": \"").Append(err ?? "").Append("\"}");
    }
}
