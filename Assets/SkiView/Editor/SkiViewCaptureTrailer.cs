using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SkiView;

/// <summary>Trailer v1 clips: HUD-free 1920x1080 JPG frame sequences (deterministic sim at SV_FPS, default 30) written to
/// /tmp/t19/frames/&lt;clip&gt;/ for ffmpeg to encode as Captures/trailer_v1/tv1_&lt;type&gt;_&lt;camera&gt;_&lt;lake&gt;_vNN.mp4.
/// One slalom run renders all four player cameras on the same frames (so a 4-camera split lines up), then a jump run with the
/// shore jump camera, then an aerial beauty orbit of the home lake.</summary>
public static class SkiViewCaptureTrailer
{
    const int W = 1920, H = 1080;
    static string Root => Environment.GetEnvironmentVariable("SV_TFRAMES_DIR") ?? "/tmp/t19/frames";
    static int Fps => int.TryParse(Environment.GetEnvironmentVariable("SV_FPS"), out var f) ? f : 30;
    static RenderTexture rt; static Texture2D tex;

    static void Render(Camera cam, string clip, int i)
    {
        cam.aspect = (float)W / H;
        if (rt == null || !rt.IsCreated()) { rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, hideFlags = HideFlags.DontSave }; rt.Create(); }
        if (tex == null) tex = new Texture2D(W, H, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave };
        var pr = cam.GetComponent<PlanarReflection>();
        if (pr != null && pr.enabled) { cam.targetTexture = rt; pr.RenderNow(); cam.targetTexture = null; }
        bool ok = false;
        try { var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt }; if (RenderPipeline.SupportsRenderRequest(cam, req)) { RenderPipeline.SubmitRenderRequest(cam, req); ok = true; } }
        catch (Exception e) { Debug.LogWarning("[SkiView] trailer render request failed: " + e.Message); }
        if (!ok) { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
        var prev = RenderTexture.active; RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
        string d = Path.Combine(Root, clip); Directory.CreateDirectory(d);
        File.WriteAllBytes(Path.Combine(d, $"f{i:0000}.jpg"), tex.EncodeToJPG(94));
    }

    static SkiViewGame Open()
    {
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.Init();
        int pcQ = Array.FindIndex(QualitySettings.names, n => n.Contains("PC")); if (pcQ >= 0) QualitySettings.SetQualityLevel(pcQ, true);
        return game;
    }
    static void Step(SkiViewGame g, Func<bool> done, float maxT) { float dt = 1f / 30f; for (float t = 0; t < maxT && !done(); t += dt) g.SimulateFor(dt); }

    public static void Clips()
    {
        string only = Environment.GetEnvironmentVariable("SV_TCLIPS") ?? "slalom,jump,beauty";
        float dt = 1f / Fps; var t0 = DateTime.Now;
        if (only.Contains("slalom"))
        {
            var game = Open(); var cam = game.cam;
            var modes = new[] { (SkiViewGame.CamMode.CloseChase, "close"), (SkiViewGame.CamMode.FirstPerson, "1st"), (SkiViewGame.CamMode.SkierChase, "high"), (SkiViewGame.CamMode.Broadcast, "boat") };
            Step(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 1, 400f);
            int n = Mathf.RoundToInt(float.Parse(Environment.GetEnvironmentVariable("SV_TSLALOM") ?? "13") * Fps);
            for (int i = 0; i < n; i++)
            {
                foreach (var (m, nm) in modes) { game.camMode = m; game.cameraFollow = true; game.ApplyVisuals(true); Render(cam, "slalom_" + nm, i); }
                game.SimulateFor(dt);
                if (i % 30 == 0) Debug.Log($"[SkiView] trailer slalom frame {i}/{n} mode={game.mode} ball={game.sim.nextBuoy} {(DateTime.Now - t0).TotalSeconds:0}s");
            }
            Debug.Log($"[SkiView] trailer slalom done mode={game.mode} pass={game.passes} msg={game.sim.message}");
        }
        if (only.Contains("jump"))
        {
            var game = Open(); var cam = game.cam;
            if (game.jumpRamp == null) Debug.LogWarning("[SkiView] trailer: no ramp");
            else
            {
                game.autopilot = false; game.jumpCam = true; game.camMode = SkiViewGame.CamMode.SkierChase;
                game.BeginJumpRun(false); game.jumpCutAt = -4.3f; game.jumpPopAt = 0.86f; game.SimulateFor(1f / 30f);
                int i = 0, ramp = -1, land = -1;
                for (; i < 40 * Fps; i++)
                {
                    if (ramp < 0 && game.jphase == SkiViewGame.JPhase.Ramp) ramp = i;
                    if (land < 0 && game.jphase == SkiViewGame.JPhase.Landing) land = i;
                    if (land >= 0 && i > land + 3 * Fps) break;
                    if (ramp < 0 && game.jphase == SkiViewGame.JPhase.Approach && game.JumpT < -7f) { game.SimulateFor(dt); i--; continue; }   // skip the long tow-in, keep ~7 s before the ramp
                    game.cameraFollow = true; game.ApplyVisuals(i == 0 || ramp < 0 && i < 2); Render(cam, "jump_any", i); game.SimulateFor(dt);
                }
                Debug.Log($"[SkiView] trailer jump frames={i} ramp={ramp} land={land} phase={game.jphase} {(DateTime.Now - t0).TotalSeconds:0}s");
            }
        }
        if (only.Contains("beauty"))
        {
            var game = Open(); var cam = game.cam;
            Step(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 1, 400f);
            var course = GameObject.Find("SlalomCourse"); Vector3 c = course != null ? course.transform.position : game.SkierPos;
            int n = 9 * Fps; game.cameraFollow = false;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n; float a = Mathf.Lerp(-35f, 10f, u) * Mathf.Deg2Rad;
                Vector3 s = game.SkierPos; Vector3 f = game.BoatFwd.normalized; Vector3 r = Vector3.Cross(Vector3.up, f);
                game.ApplyVisuals(false);
                cam.fieldOfView = 50f; cam.nearClipPlane = 0.5f; cam.farClipPlane = 3000f;
                cam.transform.position = s + (r * Mathf.Cos(a) + f * Mathf.Sin(a)) * 95f + Vector3.up * Mathf.Lerp(48f, 34f, u);
                cam.transform.LookAt(Vector3.Lerp(s, c, 0.35f) + f * 20f);
                Render(cam, "beauty_any", i); game.SimulateFor(dt);
                if (i % 30 == 0) Debug.Log($"[SkiView] trailer beauty frame {i}/{n}");
            }
            game.cameraFollow = true;
        }
        if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); } if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log($"[SkiView] trailer clips done {(DateTime.Now - t0).TotalSeconds:0}s");
    }
}
