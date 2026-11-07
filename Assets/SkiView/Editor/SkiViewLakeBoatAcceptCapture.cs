using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SkiView;

/// <summary>
/// One stepped batch for home plus the first regional scene named by SkiViewLake's lakeScenes list.
/// Uses that scene's SkiViewGame.cam (Main Camera). Does not enter Play Mode, save the scene, or measure player performance.
///
/// Unity.exe -batchmode -projectPath &lt;staged&gt; -executeMethod SkiViewLakeBoatAcceptCapture.Run -logFile lake-boat-accept.log -quit
/// </summary>
public static class SkiViewLakeBoatAcceptCapture
{
    const string HomeScene = "Assets/SkiView/Scenes/SkiViewLake.unity";
    const string BoatFbx = "Assets/SkiView/Boat/SkiBoat.fbx";
    const int Width = 1280, Height = 720;
    const int SequenceFrames = 8;
    const float WakeSampleSeconds = 0.25f;
    const float WarmupSeconds = 2f;

    public static void Run()
    {
        int code = 0;
        try { code = Capture(); }
        catch (Exception ex)
        {
            Debug.LogError("[LakeBoat] " + ex);
            code = 5;
        }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static int Capture()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[LakeBoat] no graphics device. Do not pass -nographics.");
            return 2;
        }
        string allow = Environment.GetEnvironmentVariable("SV_CAPTURE_ALLOW_NON_D3D11");
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11 && allow != "1")
        {
            Debug.LogError("[LakeBoat] graphicsDeviceType=" + SystemInfo.graphicsDeviceType + ". This batch is for the Booster D3D11 machine.");
            return 2;
        }
        var pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline == null || pipeline.GetType().Name.IndexOf("Universal", StringComparison.Ordinal) < 0)
        {
            Debug.LogError("[LakeBoat] URP is not active.");
            return 2;
        }
        if (!File.Exists(HomeScene))
        {
            Debug.LogError("[LakeBoat] missing scene file " + HomeScene);
            return 3;
        }

        string dir = Environment.GetEnvironmentVariable("SV_LAKEBOAT_OUT");
        if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/lake-boat-accept"));
        Directory.CreateDirectory(dir);

        EditorSceneManager.OpenScene(HomeScene);
        var home = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (home == null)
        {
            Debug.LogError("[LakeBoat] SkiViewGame is missing from " + HomeScene);
            return 3;
        }
        string regionName = FirstRegionName(home);
        if (regionName == null)
        {
            Debug.LogError("[LakeBoat] SkiViewGame.lakeScenes on " + HomeScene + " has no regional scene name.");
            return 3;
        }
        string regionPath = "Assets/SkiView/Scenes/" + regionName + ".unity";
        if (!File.Exists(regionPath))
        {
            Debug.LogError("[LakeBoat] missing region scene file " + regionPath);
            return 3;
        }

        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"command\": \"SkiViewLakeBoatAcceptCapture.Run\",\n");
        sb.Append("  \"execution\": \"stepped\",\n");
        sb.Append("  \"playMode\": false,\n");
        sb.Append("  \"playerPerformance\": \"not measured\",\n");
        sb.Append("  \"sceneSaved\": false,\n");
        sb.Append("  \"assetsModified\": false,\n");
        sb.Append("  \"trailer\": \"not captured\",\n");
        sb.Append("  \"destination\": \"").Append(Esc(dir)).Append("\",\n");
        sb.Append("  \"destinationNote\": \"justin/lakes-bar/captures is not in this source tree. This folder does not overwrite Screens/env_h_hero_8017_lake.png.\",\n");
        sb.Append("  \"rubric\": \"CATALOG.md is not in this source tree. No numeric tolerance is applied. Frames are for Colin and Raymond.\",\n");
        sb.Append("  \"shoreCloseUp\": \"not_tested\",\n");
        sb.Append("  \"shoreReason\": \"Neither scene has a shore camera. SkiViewGame.UpdateJumpCamera is the jump shore path and this command does not enter jump.\",\n");
        sb.Append("  \"unityVersion\": \"").Append(Esc(Application.unityVersion)).Append("\",\n");
        sb.Append("  \"device\": \"").Append(Esc(SystemInfo.deviceName)).Append("\",\n");
        sb.Append("  \"machine\": \"").Append(Esc(Environment.MachineName)).Append("\",\n");
        sb.Append("  \"os\": \"").Append(Esc(SystemInfo.operatingSystem)).Append("\",\n");
        sb.Append("  \"gpu\": \"").Append(Esc(SystemInfo.graphicsDeviceName)).Append("\",\n");
        sb.Append("  \"graphicsApi\": \"").Append(SystemInfo.graphicsDeviceType).Append("\",\n");
        sb.Append("  \"quality\": \"").Append(Esc(QualitySettings.names[QualitySettings.GetQualityLevel()])).Append("\",\n");
        sb.Append("  \"preset\": \"").Append(SkiViewQuality.Current).Append("\",\n");
        sb.Append("  \"reflections\": ").Append(SkiViewQuality.Reflections ? "true" : "false").Append(",\n");
        sb.Append("  \"boatFbx\": \"").Append(BoatFbx).Append("\",\n");
        sb.Append("  \"boatFbxSha256\": \"").Append(ShaFile(BoatFbx)).Append("\",\n");
        sb.Append("  \"boatRevisionClaim\": \"none. Hash and scene object names only. SkiBoat_r5 is not assumed.\",\n");
        sb.Append("  \"captureScriptSha256\": \"").Append(ShaFull(Path.Combine(Application.dataPath, "SkiView/Editor/SkiViewLakeBoatAcceptCapture.cs"))).Append("\",\n");
        sb.Append("  \"utc\": \"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("\",\n");
        sb.Append("  \"input\": \"no keyboard or touch. steerInput is not read. autopilot is left at the scene value.\",\n");
        sb.Append("  \"particles\": \"ParticleSystem.Simulate on the scene boatSpray and boatSheet for the same seconds as SimulateFor. PlayMode Update did not run.\",\n");
        sb.Append("  \"wakeSampleSeconds\": ").Append(G(WakeSampleSeconds)).Append(",\n");
        sb.Append("  \"wakeSampleSource\": \"SkiViewGame.FixedStep resets wakeT to 0.25 seconds.\",\n");
        sb.Append("  \"warmupSeconds\": ").Append(G(WarmupSeconds)).Append(",\n");
        sb.Append("  \"sequenceFrames\": ").Append(SequenceFrames).Append(",\n");
        sb.Append("  \"coverage\": \"After Init, 2.00 s warmup, then 8 broadcast frames at sim time 2.00 through 3.75. Not a pass threshold.\",\n");
        sb.Append("  \"bounds\": {\"width\": ").Append(Width).Append(", \"height\": ").Append(Height).Append(", \"lakes\": 2, \"framesPerLake\": ").Append(SequenceFrames + 2).Append(", \"framesTotal\": ").Append((SequenceFrames + 2) * 2).Append("},\n");
        sb.Append("  \"lakes\": [\n");

        int frames = 0;
        string err = OneLake(sb, HomeScene, ref frames);
        if (err != null) { Debug.LogError("[LakeBoat] " + err); return 3; }
        sb.Append(",\n");
        err = OneLake(sb, regionPath, ref frames);
        if (err != null) { Debug.LogError("[LakeBoat] " + err); return 3; }
        sb.Append("\n  ]\n}\n");
        if (frames != (SequenceFrames + 2) * 2)
        {
            Debug.LogError("[LakeBoat] frame count " + frames);
            return 4;
        }
        File.WriteAllText(Path.Combine(dir, "manifest.json"), sb.ToString());
        Debug.Log("[LakeBoat] stepped frames=" + frames + " dir=" + dir + " playMode=false playerPerformance=not measured sceneSaved=false shore=not_tested");
        return 0;
    }

    static string FirstRegionName(SkiViewGame home)
    {
        if (home.lakeScenes == null) return null;
        for (int i = 0; i < home.lakeScenes.Length; i++)
        {
            string n = home.lakeScenes[i];
            if (string.IsNullOrEmpty(n) || n == "SkiViewLake") continue;
            return n;
        }
        return null;
    }

    static string OneLake(StringBuilder sb, string scenePath, ref int frames)
    {
        EditorSceneManager.OpenScene(scenePath);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        if (game == null || game.cam == null || game.path == null || game.path.Count == 0)
            return scenePath + " has no SkiViewGame with cam and a BoatPath.";
        var detail = UnityEngine.Object.FindFirstObjectByType<LakeDetail>();
        if (detail != null)
        {
            Shader.SetGlobalVector("_SVWind", detail.wind);
            Shader.DisableKeyword("SV_WIND_OFF");
            Shader.SetGlobalFloat("_SV_A2C", detail.foliageA2C && detail.Tier < 2 ? 1f : 0f);
            Shader.SetGlobalVector("_SV_FoliageRim", detail.foliageRim);
        }
        bool urpAdded = false;
        if (game.cam.GetComponent<UniversalAdditionalCameraData>() == null)
        {
            game.cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urpAdded = true;
        }
        game.cam.aspect = Width / (float)Height;
        game.Init();
        Step(game, WarmupSeconds, true);

        string root = Environment.GetEnvironmentVariable("SV_LAKEBOAT_OUT");
        if (string.IsNullOrEmpty(root)) root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screens/lake-boat-accept"));
        string lakeDir = Path.Combine(root, Safe(game.lakeId));
        Directory.CreateDirectory(lakeDir);

        sb.Append("    {\"scene\": \"").Append(Esc(scenePath)).Append("\"");
        sb.Append(", \"sceneSha256\": \"").Append(ShaFile(scenePath)).Append("\"");
        sb.Append(", \"lakeId\": \"").Append(Esc(game.lakeId)).Append("\"");
        sb.Append(", \"lakeName\": \"").Append(Esc(game.lakeName)).Append("\"");
        sb.Append(", \"camera\": \"").Append(Esc(game.cam.name)).Append("\"");
        sb.Append(", \"cameraGlobalId\": \"").Append(Esc(GlobalObjectId.GetGlobalObjectIdSlow(game.cam).ToString())).Append("\"");
        sb.Append(", \"urpCameraDataAdded\": ").Append(urpAdded ? "true" : "false");
        sb.Append(", \"sceneSaved\": false");
        AppendSun(sb);
        AppendBoat(sb, game);
        AppendDetail(sb, detail);
        sb.Append(", \"autopilot\": ").Append(game.autopilot ? "true" : "false");
        sb.Append(", \"jumpMode\": ").Append(game.jumpMode ? "true" : "false");
        sb.Append(", \"frames\": [\n");

        bool any = false;
        float prevHeading = float.NaN;
        for (int i = 0; i < SequenceFrames; i++)
        {
            if (i > 0) Step(game, WakeSampleSeconds, false);
            float heading = HeadingDeg(game.BoatFwd);
            float yawRate = float.NaN;
            if (!float.IsNaN(prevHeading)) yawRate = Mathf.DeltaAngle(prevHeading, heading) / WakeSampleSeconds;
            any = Shot(sb, game, lakeDir, "broadcast_" + i.ToString("00", CultureInfo.InvariantCulture), "broadcast", i, yawRate, any, ref frames);
            if (i == 0)
            {
                any = Shot(sb, game, lakeDir, "chase_00", "skierChase", i, yawRate, any, ref frames);
                any = Shot(sb, game, lakeDir, "close_00", "closeChase", i, yawRate, any, ref frames);
                game.camMode = SkiViewGame.CamMode.Broadcast;
                game.ApplyVisuals(true);
            }
            prevHeading = heading;
        }
        sb.Append("\n    ]}");
        return null;
    }

    static void Step(SkiViewGame game, float seconds, bool restartParticles)
    {
        game.SimulateFor(seconds);
        Simulate(game.boatSpray, seconds, restartParticles);
        Simulate(game.boatSheet, seconds, restartParticles);
    }

    static void Simulate(ParticleSystem ps, float seconds, bool restart)
    {
        if (ps == null) return;
        ps.Simulate(seconds, true, restart);
    }

    static bool Shot(StringBuilder sb, SkiViewGame game, string lakeDir, string shot, string role, int index, float yawRate, bool any, ref int frames)
    {
        if (role == "skierChase") game.camMode = SkiViewGame.CamMode.SkierChase;
        else if (role == "closeChase") game.camMode = SkiViewGame.CamMode.CloseChase;
        else game.camMode = SkiViewGame.CamMode.Broadcast;
        game.ApplyVisuals(true);
        string path = Path.Combine(lakeDir, shot + ".png");
        string renderErr = Render(game.cam, path);
        if (renderErr != null) throw new InvalidOperationException(shot + " " + renderErr);
        if (any) sb.Append(",\n");
        var t = game.cam.transform;
        sb.Append("      {\"shot\": \"").Append(shot).Append("\"");
        sb.Append(", \"role\": \"").Append(role).Append("\"");
        sb.Append(", \"camMode\": \"").Append(game.camMode).Append("\"");
        sb.Append(", \"camLabel\": \"").Append(Esc(game.CamLabel)).Append("\"");
        sb.Append(", \"jumpCamOn\": ").Append(game.JumpCamOn ? "true" : "false");
        sb.Append(", \"simTime\": ").Append(G(game.simTime));
        sb.Append(", \"frameIndex\": ").Append(index);
        sb.Append(", \"sampleSeconds\": ").Append(G(index == 0 && role != "broadcast" ? 0f : (index == 0 ? WarmupSeconds : WakeSampleSeconds)));
        sb.Append(", \"mode\": \"").Append(game.mode).Append("\"");
        sb.Append(", \"boatSpeedMps\": ").Append(G(game.BoatSpeed));
        sb.Append(", \"headingDeg\": ").Append(G(HeadingDeg(game.BoatFwd)));
        if (float.IsNaN(yawRate)) sb.Append(", \"yawRateDegPerSec\": null");
        else sb.Append(", \"yawRateDegPerSec\": ").Append(G(yawRate));
        sb.Append(", \"pos\": "); Vec(sb, t.position);
        sb.Append(", \"euler\": "); Vec(sb, t.eulerAngles);
        sb.Append(", \"fov\": ").Append(G(game.cam.fieldOfView));
        sb.Append(", \"near\": ").Append(G(game.cam.nearClipPlane));
        sb.Append(", \"far\": ").Append(G(game.cam.farClipPlane));
        sb.Append(", \"wakeCount\": ").Append(WakeCount(game));
        sb.Append(", \"sha256\": \"").Append(ShaFull(path)).Append("\"");
        sb.Append(", \"bytes\": ").Append(new FileInfo(path).Length).Append("}");
        frames++;
        return true;
    }

    static int WakeCount(SkiViewGame game)
    {
        var f = game.GetType().GetField("wakeN", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) return -1;
        object v = f.GetValue(game);
        return v is int n ? n : -1;
    }

    static void AppendSun(StringBuilder sb)
    {
        Light sun = null;
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light != null && light.name == "Sun") { sun = light; break; }
        }
        if (sun == null)
        {
            sb.Append(", \"sun\": null, \"timeOfDay\": \"absent. No clock hour is stored. No object named Sun.\"");
            return;
        }
        sb.Append(", \"sun\": {\"name\": \"Sun\", \"globalId\": \"").Append(Esc(GlobalObjectId.GetGlobalObjectIdSlow(sun).ToString())).Append("\"");
        sb.Append(", \"euler\": "); Vec(sb, sun.transform.eulerAngles);
        sb.Append(", \"color\": \"").Append(ColorUtility.ToHtmlStringRGB(sun.color)).Append("\"");
        sb.Append(", \"intensity\": ").Append(G(sun.intensity)).Append("}");
        sb.Append(", \"timeOfDay\": \"Sun transform only. No clock hour is stored.\"");
    }

    static void AppendBoat(StringBuilder sb, SkiViewGame game)
    {
        sb.Append(", \"boat\": "); Ident(sb, game.boat);
        sb.Append(", \"tow\": "); Ident(sb, game.boatTow);
        sb.Append(", \"wakeEmitter\": "); Ident(sb, game.boatWake);
        sb.Append(", \"boatSpray\": ").Append(game.boatSpray != null ? "true" : "false");
        sb.Append(", \"boatSheet\": ").Append(game.boatSheet != null ? "true" : "false");
        if (game.rope == null) sb.Append(", \"rope\": null");
        else
        {
            sb.Append(", \"rope\": {\"positions\": ").Append(game.rope.positionCount);
            if (game.rope.positionCount > 0)
            {
                sb.Append(", \"first\": "); Vec(sb, game.rope.GetPosition(0));
                sb.Append(", \"last\": "); Vec(sb, game.rope.GetPosition(game.rope.positionCount - 1));
            }
            sb.Append("}");
        }
        sb.Append(", \"meshes\": [");
        if (game.boat != null)
        {
            bool any = false;
            foreach (var r in game.boat.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                string n = r.name;
                if (n != "SkiBoat_LOD0" && n != "SkiBoat_LOD1" && n != "SkiBoat_LOD2" && n != "TowPoint" && n != "WakeEmitter") continue;
                if (any) sb.Append(", ");
                any = true;
                var mf = r.GetComponent<MeshFilter>();
                var smr = r as SkinnedMeshRenderer;
                Mesh mesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);
                sb.Append("{\"name\": \"").Append(Esc(n)).Append("\"");
                sb.Append(", \"globalId\": \"").Append(Esc(GlobalObjectId.GetGlobalObjectIdSlow(r).ToString())).Append("\"");
                sb.Append(", \"mesh\": \"").Append(mesh != null ? Esc(mesh.name) : "").Append("\"");
                sb.Append(", \"verts\": ").Append(mesh != null ? mesh.vertexCount : 0).Append("}");
            }
        }
        sb.Append("]");
    }

    static void AppendDetail(StringBuilder sb, LakeDetail detail)
    {
        if (detail == null)
        {
            sb.Append(", \"lakeDetail\": null");
            return;
        }
        sb.Append(", \"lakeDetail\": {\"tier\": ").Append(detail.Tier);
        sb.Append(", \"forceTier\": ").Append(detail.forceTier);
        sb.Append(", \"autoTier\": ").Append(LakeDetail.AutoTier);
        sb.Append(", \"foliageA2C\": ").Append(detail.foliageA2C ? "true" : "false");
        sb.Append(", \"applyCalled\": false");
        sb.Append(", \"note\": \"Awake applies a tier only in Play Mode. This stepped run set the wind and foliage shader globals from the serialized LakeDetail and did not copy TerrainData.\"}");
    }

    static void Ident(StringBuilder sb, Transform t)
    {
        if (t == null) { sb.Append("null"); return; }
        sb.Append("{\"name\": \"").Append(Esc(t.name)).Append("\"");
        sb.Append(", \"globalId\": \"").Append(Esc(GlobalObjectId.GetGlobalObjectIdSlow(t).ToString())).Append("\"");
        sb.Append(", \"pos\": "); Vec(sb, t.position);
        sb.Append(", \"local\": "); Vec(sb, t.localPosition);
        sb.Append("}");
    }

    static string Render(Camera cam, string path)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        try
        {
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(cam, req))
                return "URP SingleCameraRequest unsupported";
            RenderPipeline.SubmitRenderRequest(cam, req);
        }
        catch (Exception ex)
        {
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return ex.Message;
        }
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        return null;
    }

    static float HeadingDeg(Vector3 f) { return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }

    static string ShaFile(string assetPath)
    {
        string full = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + assetPath));
        return File.Exists(full) ? ShaFull(full) : "";
    }

    static string ShaFull(string full)
    {
        using (var sha = SHA256.Create())
        using (var fs = File.OpenRead(full))
        {
            var hash = sha.ComputeHash(fs);
            var b = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) b.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }

    static void Vec(StringBuilder sb, Vector3 v)
    {
        sb.Append("[").Append(G(v.x)).Append(",").Append(G(v.y)).Append(",").Append(G(v.z)).Append("]");
    }

    static string G(float v) { return v.ToString("G9", CultureInfo.InvariantCulture); }

    static string Safe(string s)
    {
        if (string.IsNullOrEmpty(s)) return "lake";
        var b = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            b.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        }
        return b.ToString();
    }

    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "'").Replace("\n", " ").Replace("\r", " ");
    }
}
