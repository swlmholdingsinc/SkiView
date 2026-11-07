using System;
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
/// Same pose as the body-head coverage views. Before is the live body skin. After matches that skin to Face125.
/// The head mesh clip is left in place. Vest, UVs, binds, and the rig are not edited.
///
/// Unity.exe -batchmode -projectPath &lt;staged&gt; -executeMethod SkiViewNeckSkinMatchCapture.Run -logFile neck-skin-match.log -quit
/// </summary>
public static class SkiViewNeckSkinMatchCapture
{
    const string ScenePath = "Assets/SkiView/Scenes/SkiViewLake.unity";

    public static void Run()
    {
        int code = 0;
        try { code = Capture(); }
        catch (Exception ex)
        {
            Debug.LogError("[NeckSkin] " + ex);
            code = 5;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static int Capture()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[NeckSkin] no graphics device. Do not pass -nographics.");
            return 2;
        }
        bool allowOther = Environment.GetEnvironmentVariable("SV_CAPTURE_ALLOW_NON_D3D11") == "1";
        if (!allowOther && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
        {
            Debug.LogError("[NeckSkin] graphicsDeviceType=" + SystemInfo.graphicsDeviceType);
            return 2;
        }
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null || pipeline.GetType().Name.IndexOf("Universal", StringComparison.Ordinal) < 0)
        {
            Debug.LogError("[NeckSkin] URP is not active.");
            return 2;
        }
        if (!File.Exists(ScenePath)) { Debug.LogError("[NeckSkin] missing scene"); return 3; }
        EditorSceneManager.OpenScene(ScenePath);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null || game.skier == null || game.rig == null || game.rig.animator == null)
        {
            Debug.LogError("[NeckSkin] no skier rig");
            return 3;
        }
        var setAthlete = game.GetType().GetMethod("SetAthlete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
        if (setAthlete == null) { Debug.LogError("[NeckSkin] SetAthlete missing"); return 3; }

        string dir = Environment.GetEnvironmentVariable("SV_NECK_OUT");
        if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/neck-skin-match"));
        Directory.CreateDirectory(dir);

        var camGo = new GameObject("NeckSkinMatchCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.aspect = 1280f / 720f;
        if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
            camGo.AddComponent<UniversalAdditionalCameraData>();

        var shots = new StringBuilder();
        var root = game.rig.animator.transform;
        int changed = 0;
        try
        {
            Pair(game, setAthlete, cam, dir, root, 0, "male", shots, ref changed);
            Pair(game, setAthlete, cam, dir, root, 1, "female", shots, ref changed);
        }
        finally { UnityEngine.Object.DestroyImmediate(camGo); }

        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"playMode\": false,\n");
        sb.Append("  \"playerFixed\": false,\n");
        sb.Append("  \"geometryEdited\": false,\n");
        sb.Append("  \"uvEdited\": false,\n");
        sb.Append("  \"rigEdited\": false,\n");
        sb.Append("  \"vestEdited\": false,\n");
        sb.Append("  \"assetSaved\": false,\n");
        sb.Append("  \"materialsMatched\": ").Append(changed).Append(",\n");
        sb.Append("  \"cause\": \"Body_LOD SetA keeps _METALLICSPECGLOSSMAP and metallic 1. Face125 sets metallic 0, smoothness 0.25, and HumanSkin125 when that material exists. The match copies the face shader and those scalars onto Body_LOD skin and leaves the body albedo, tint, mesh, and vest.\",\n");
        sb.Append("  \"shots\": [\n").Append(shots).Append("\n  ]\n}\n");
        File.WriteAllText(Path.Combine(dir, "neck-skin.json"), sb.ToString());
        Debug.Log("[NeckSkin] " + dir + " materialsMatched=" + changed + " playerFixed=false sceneSaved=false");
        return changed > 0 ? 0 : 4;
    }

    static void Pair(SkiViewGame game, MethodInfo setAthlete, Camera cam, string dir, Transform root, int athlete, string label, StringBuilder shots, ref int changed)
    {
        setAthlete.Invoke(game, new object[] { athlete });
        SkiViewNeckSkinMatch.Remember(root);
        SkiViewNeckSkinMatch.Restore();
        Shoot(game, cam, dir, label + "_before", shots);
        changed += SkiViewNeckSkinMatch.Apply(root);
        Shoot(game, cam, dir, label + "_after", shots);
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
