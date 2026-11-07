using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Deterministic D3D11 close-ups of the staged URP skier. Does not save assets or run the lake sim.
/// KarthikCapture125.cs is not in this repo and is not called.
/// SkiViewCapture.Run is not used: it simulates the boat and Shot() calls ApplyVisuals.
///
/// Unity 6000.3.26f1, project that already contains the staged art. No -nographics.
/// Unity.exe -batchmode -projectPath &lt;staged&gt; -executeMethod SkiViewCharacterCapture125.Run -logFile character-capture.log -quit
/// </summary>
public static class SkiViewCharacterCapture125
{
    const int W = 1280, H = 720;
    const string ScenePath = "Assets/SkiView/Scenes/SkiViewLake.unity";
    const string VestFbx = "Assets/SkiView/Resources/Gear125/SkiView_Gear_Slalom_125.fbx";

    public static void Run()
    {
        int code = 0;
        try { code = Capture(); }
        catch (Exception ex)
        {
            Debug.LogError("[CharCap] " + ex);
            code = 5;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static int Capture()
    {
        var missing = SkiViewCharacterCapture125Logic.Missing(File.Exists);
        if (missing.Count > 0)
        {
            Debug.LogError("[CharCap] missing\n" + string.Join("\n", missing.ToArray()));
            return 3;
        }
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[CharCap] no graphics device. Do not pass -nographics.");
            return 2;
        }
        bool allowOther = Environment.GetEnvironmentVariable("SV_CAPTURE_ALLOW_NON_D3D11") == "1";
        if (!allowOther && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
        {
            Debug.LogError("[CharCap] graphicsDeviceType=" + SystemInfo.graphicsDeviceType + " name=" + SystemInfo.graphicsDeviceName + ". This harness is the D3D11 capture.");
            return 2;
        }
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null || pipeline.GetType().Name.IndexOf("Universal", StringComparison.Ordinal) < 0)
        {
            Debug.LogError("[CharCap] active pipeline is " + (pipeline == null ? "null" : pipeline.GetType().FullName) + ". PC_RPAsset must be the URP asset. No substitute pipeline is created.");
            return 2;
        }

        EditorSceneManager.OpenScene(ScenePath);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null || game.skier == null || game.rig == null || game.rig.animator == null || !game.rig.animator.isHuman)
        {
            Debug.LogError("[CharCap] SkiViewLake has no humanoid SkiViewGame rig.");
            return 3;
        }
        var setAthlete = game.GetType().GetMethod("SetAthlete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
        if (setAthlete == null)
        {
            Debug.LogError("[CharCap] SkiViewGame.SetAthlete(int) is missing. The staged athlete partial is required. KarthikCapture125 is not called.");
            return 3;
        }
        Light sun = null;
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light != null && light.type == LightType.Directional && light.enabled) { sun = light; break; }
        if (sun == null)
        {
            Debug.LogError("[CharCap] SkiViewLake has no enabled directional light. No studio light is added.");
            return 3;
        }

        string dir = Environment.GetEnvironmentVariable("SV_CAPTURE_OUT");
        if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/character125"));
        Directory.CreateDirectory(dir);
        var shader = Shader.Find("Hidden/SkiView/CharacterCaptureDiag");
        if (shader == null)
        {
            Debug.LogError("[CharCap] Hidden/SkiView/CharacterCaptureDiag was not imported.");
            return 3;
        }
        string normalsMode = ImporterMode(VestFbx);
        var camGo = new GameObject("CharacterCaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.aspect = (float)W / H;
        if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
            camGo.AddComponent<UniversalAdditionalCameraData>();

        var metrics = new StringBuilder();
        metrics.Append("{\n");
        metrics.Append("  \"device\": \"").Append(Esc(SystemInfo.graphicsDeviceName)).Append("\",\n");
        metrics.Append("  \"deviceType\": \"").Append(SystemInfo.graphicsDeviceType).Append("\",\n");
        metrics.Append("  \"pipeline\": \"").Append(Esc(pipeline.GetType().FullName)).Append("\",\n");
        metrics.Append("  \"sun\": \"").Append(Esc(sun.name)).Append("\",\n");
        metrics.Append("  \"sunIntensity\": ").Append(sun.intensity.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
        metrics.Append("  \"vestImporter\": \"").Append(Esc(normalsMode)).Append("\",\n");
        metrics.Append("  \"shots\": [\n");

        int fails = 0;
        bool first = true;
        var hashes = new Dictionary<string, uint>();
        Quaternion skierRot = game.skier.localRotation;
        try
        {
            for (int athlete = 0; athlete < 2; athlete++)
            {
                string sex = athlete == 0 ? "male" : "female";
                setAthlete.Invoke(game, new object[] { athlete });
                var anim = game.rig.animator;
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                Transform neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                if (head == null || neck == null) { Debug.LogError("[CharCap] missing head or neck"); return 4; }
                float headY = head.localPosition.y, neckY = neck.localPosition.y;
                bool headOk = SkiViewCharacterCapture125Logic.BoneOk(headY, SkiViewCharacterCapture125Logic.HeadLocalY);
                bool neckOk = SkiViewCharacterCapture125Logic.BoneOk(neckY, SkiViewCharacterCapture125Logic.NeckLocalY);
                if (!headOk || !neckOk) fails++;
                Debug.Log("[CharCap] " + sex + " headY=" + headY.ToString("0.#####", CultureInfo.InvariantCulture) + " neckY=" + neckY.ToString("0.#####", CultureInfo.InvariantCulture) + " headOk=" + headOk + " neckOk=" + neckOk);
                var vests = Vests(game.skier, game.rig.animator.transform);
                if (vests.Count < 1) { Debug.LogError("[CharCap] no original vest renderer after SetAthlete " + sex); fails++; }

                foreach (var pose in new[] { "front", "left", "right" })
                {
                    float roll = SkiViewCharacterCapture125Logic.PoseRoll(pose);
                    game.skier.localRotation = skierRot * Quaternion.AngleAxis(roll, Vector3.forward);
                    Frame(cam, head);
                    foreach (var mode in new[] { "lit", "normals", "tangents", "backface" })
                    {
                        Material[][] saved = null;
                        if (mode != "lit") saved = SwapDiag(vests, shader, mode);
                        string name = sex + "_" + pose + "_" + mode;
                        uint hash = 0; float mean = 0, stdev = 0; int bytes = 0;
                        string err = Render(cam, Path.Combine(dir, name + ".png"), out hash, out mean, out stdev, out bytes);
                        if (saved != null) Restore(vests, saved);
                        bool luma = err == null && SkiViewCharacterCapture125Logic.LumaOk(mode, mean, stdev);
                        if (!luma) fails++;
                        hashes[name] = hash;
                        if (!first) metrics.Append(",\n");
                        first = false;
                        metrics.Append("    {\"name\": \"").Append(name)
                            .Append("\", \"roll\": ").Append(roll.ToString("0", CultureInfo.InvariantCulture))
                            .Append(", \"headLocalY\": ").Append(headY.ToString("0.#####", CultureInfo.InvariantCulture))
                            .Append(", \"neckLocalY\": ").Append(neckY.ToString("0.#####", CultureInfo.InvariantCulture))
                            .Append(", \"vests\": ").Append(vests.Count)
                            .Append(", \"bytes\": ").Append(bytes)
                            .Append(", \"mean\": ").Append(mean.ToString("0.##", CultureInfo.InvariantCulture))
                            .Append(", \"stdev\": ").Append(stdev.ToString("0.##", CultureInfo.InvariantCulture))
                            .Append(", \"lumaOk\": ").Append(luma ? "true" : "false")
                            .Append(", \"error\": \"").Append(Esc(err)).Append("\"}");
                    }
                }
            }
        }
        finally { game.skier.localRotation = skierRot; UnityEngine.Object.DestroyImmediate(camGo); }

        if (hashes.ContainsKey("male_left_lit") && hashes.ContainsKey("male_right_lit") && !SkiViewCharacterCapture125Logic.ShotsDiffer(hashes["male_left_lit"], hashes["male_right_lit"]))
            fails++;
        if (hashes.ContainsKey("male_front_lit") && hashes.ContainsKey("male_front_normals") && !SkiViewCharacterCapture125Logic.ShotsDiffer(hashes["male_front_lit"], hashes["male_front_normals"]))
            fails++;
        metrics.Append("\n  ],\n  \"fails\": ").Append(fails).Append("\n}\n");
        File.WriteAllText(Path.Combine(dir, "metrics.json"), metrics.ToString());
        Debug.Log("[CharCap] wrote " + dir + " fails=" + fails);
        return fails == 0 ? 0 : 4;
    }

    static string ImporterMode(string path)
    {
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) return "missing";
        return "normals=" + mi.importNormals + " tangents=" + mi.importTangents;
    }

    static List<SkinnedMeshRenderer> Vests(Transform skier, Transform anim)
    {
        var list = new List<SkinnedMeshRenderer>();
        var seen = new HashSet<int>();
        AddVests(skier, list, seen);
        AddVests(anim, list, seen);
        return list;
    }

    static void AddVests(Transform root, List<SkinnedMeshRenderer> list, HashSet<int> seen)
    {
        if (root == null) return;
        foreach (var sm in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (sm == null || !seen.Add(sm.GetInstanceID())) continue;
            string mesh = sm.sharedMesh != null ? sm.sharedMesh.name : "";
            if (SkiViewCharacterCapture125Logic.OriginalVestName(sm.name, mesh)) list.Add(sm);
        }
    }

    static void Frame(Camera cam, Transform head)
    {
        Vector3 aim = head.position;
        Vector3 fwd = head.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 pos = aim + fwd * 0.94f + Vector3.up * 0.01f - Vector3.up * 0.055f;
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(aim - Vector3.up * 0.055f - pos, Vector3.up));
    }

    static Material[][] SwapDiag(List<SkinnedMeshRenderer> vests, Shader shader, string mode)
    {
        float m = mode == "tangents" ? 1f : mode == "backface" ? 2f : 0f;
        var saved = new Material[vests.Count][];
        for (int i = 0; i < vests.Count; i++)
        {
            saved[i] = vests[i].sharedMaterials;
            int n = Mathf.Max(1, saved[i] == null ? 1 : saved[i].Length);
            var repl = new Material[n];
            for (int s = 0; s < n; s++)
            {
                var mat = new Material(shader) { name = "CharCapDiag" };
                if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", m);
                repl[s] = mat;
            }
            vests[i].sharedMaterials = repl;
        }
        return saved;
    }

    static void Restore(List<SkinnedMeshRenderer> vests, Material[][] saved)
    {
        for (int i = 0; i < vests.Count; i++)
        {
            var cur = vests[i].sharedMaterials;
            vests[i].sharedMaterials = saved[i];
            if (cur == null) continue;
            for (int s = 0; s < cur.Length; s++)
                if (cur[s] != null && cur[s].name == "CharCapDiag") UnityEngine.Object.DestroyImmediate(cur[s]);
        }
    }

    static string Render(Camera cam, string path, out uint hash, out float mean, out float stdev, out int bytes)
    {
        hash = 0; mean = 0; stdev = 0; bytes = 0;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        bool urp = false;
        try
        {
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(cam, req))
                return "URP SingleCameraRequest unsupported";
            RenderPipeline.SubmitRenderRequest(cam, req);
            urp = true;
        }
        catch (Exception ex) { return "URP render failed: " + ex.Message; }
        if (!urp) return "URP render was not submitted";
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        var pix = tex.GetPixels32();
        double sum = 0, sum2 = 0;
        unchecked
        {
            for (int i = 0; i < pix.Length; i++)
            {
                float y = (pix[i].r * 0.2126f + pix[i].g * 0.7152f + pix[i].b * 0.0722f);
                sum += y; sum2 += y * y;
                hash = hash * 16777619u + pix[i].r;
                hash = hash * 16777619u + pix[i].g;
                hash = hash * 16777619u + pix[i].b;
            }
        }
        double n = pix.Length;
        mean = (float)(sum / n);
        stdev = (float)Math.Sqrt(Math.Max(0, sum2 / n - mean * mean));
        byte[] png = tex.EncodeToPNG();
        bytes = png.Length;
        File.WriteAllBytes(path, png);
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        return null;
    }

    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "'").Replace("\n", " ").Replace("\r", " ");
    }
}
