using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SkiView;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

/// <summary>Pre-release polish (pass 20): per-task frame-time probe and helpers.</summary>
public static partial class SkiViewCapture
{
    /// <summary>Renders the camera into a small target and returns (mean luma, fraction of pixels brighter than 0.08) over the centre half.</summary>
    static RenderTexture Resolved(RenderTexture rt)
    {
        if (rt.antiAliasing <= 1) return rt;
        if (resolveRT == null || resolveRT.width != rt.width || resolveRT.height != rt.height) { if (resolveRT != null) resolveRT.Release(); resolveRT = new RenderTexture(rt.width, rt.height, 0, RenderTextureFormat.ARGB32); resolveRT.Create(); }
        rt.ResolveAntiAliasedSurface(resolveRT); return resolveRT;
    }
    static RenderTexture resolveRT;

    static void SyncA2C(int tier = 0) { var d = UnityEngine.Object.FindFirstObjectByType<LakeDetail>(); var ov = Environment.GetEnvironmentVariable("SV_A2C"); Shader.SetGlobalFloat("_SV_A2C", ov != null ? float.Parse(ov) : (d != null && d.foliageA2C && tier < 2 ? 1f : 0f)); }   // edit mode: LakeDetail.OnEnable does not run

    static (float mean, float lit) ViewLuma(Camera cam)
    {
        const int w = 192, h = 108;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.Create();
        RenderInto(cam, rt);
        var prev = RenderTexture.active; RenderTexture.active = Resolved(rt);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = prev;
        float sum = 0f; int n = 0, lit = 0;
        for (int y = h / 4; y < h * 3 / 4; y++) for (int x = w / 4; x < w * 3 / 4; x++) { var c = tex.GetPixel(x, y); float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; sum += l; n++; if (l > 0.08f) lit++; }
        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        return (sum / n, (float)lit / n);
    }

    static void RenderInto(Camera cam, RenderTexture rt)
    {
        bool ok = false;
        if (Environment.GetEnvironmentVariable("SV_CAMRENDER") == "1") { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; return; }
        try { var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt }; if (RenderPipeline.SupportsRenderRequest(cam, req)) { RenderPipeline.SubmitRenderRequest(cam, req); ok = true; } }
        catch (Exception) { }
        if (!ok) { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
    }

    /// <summary>T1: a building camera that still renders mostly black (inside vegetation-pack foliage, which ClearOfTrees cannot see) is moved: ring of raised positions, first that sees the building.</summary>
    static Vector3 LitBuildingView(Camera cam, Vector3 p, Vector3 aim, Vector3 c, float back)
    {
        cam.transform.position = p; cam.transform.LookAt(aim);
        var v = ViewLuma(cam); if (v.lit > 0.85f) return p;
        foreach (float hgt in new[] { 6f, 12f, 20f, 30f })
            foreach (float r in new[] { back, back * 1.5f })
                for (int k = 0; k < 12; k++)
                {
                    float a = k * Mathf.PI / 6f; Vector3 q = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r; var tr_ = Terrain.activeTerrain; q.y = Mathf.Max(1.6f, tr_ != null ? tr_.SampleHeight(q) + tr_.transform.position.y : 0f) + hgt;
                    cam.transform.position = q; cam.transform.LookAt(aim);
                    var u = ViewLuma(cam);
                    if (u.lit > 0.85f) { Debug.Log($"[SkiView] T1 building view moved: lit {v.lit:0.00} -> {u.lit:0.00} at h {hgt} r {r:0} a {k * 30}"); return q; }
                }
        Debug.Log("[SkiView] T1 building view: no clear position found"); return p;
    }

    /// <summary>Mobile-tier frame-time probe: Mobile quality + LakeDetail tier 2, 1280x720 (+ planar reflection), median of 30 GPU-synced renders per view. Software GL on the box: a relative A/B number, not a phone number.</summary>
    public static void P20Perf()
    {
        string tag = Environment.GetEnvironmentVariable("SV_TAG") ?? "probe";
        var ids = (Environment.GetEnvironmentVariable("SV_LAKES") ?? "tallpine").Split(',');
        var lines = new List<string>();
        foreach (var id in ids) lines.AddRange(FrameTimes(id.Trim(), 2, 1280, 720));
        File.WriteAllText(Path.Combine(OutDir, $"perf20_{tag}.txt"), string.Join("\n", lines) + "\n");
        Debug.Log("[SkiView] P20Perf " + tag + ":\n" + string.Join("\n", lines));
    }

    static List<string> FrameTimes(string id, int tier, int w, int h)
    {
        var res = new List<string>();
        string sp = id == "skiview" ? SkiViewBuilder.SCENE : SkiViewBuilder.LakeScenePath(id);
        if (!File.Exists(sp)) return res;
        EditorSceneManager.OpenScene(sp);
        var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.camMode = SkiViewGame.CamMode.Broadcast; game.Init(); SyncA2C(tier);
        var cam = game.cam;
        string qn = tier == 2 ? "Mobile" : "PC"; int qi = Array.FindIndex(QualitySettings.names, n => n.Contains(qn)); if (qi >= 0) QualitySettings.SetQualityLevel(qi, true);
        var urp0 = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; float sd0 = urp0 != null ? urp0.shadowDistance : 0f; int sc0 = urp0 != null ? urp0.shadowCascadeCount : 1;
        var det = UnityEngine.Object.FindFirstObjectByType<LakeDetail>(); if (det != null && Environment.GetEnvironmentVariable("SV_PHONEXF") == "1") det.phonePrototypes = new GameObject[0];
        if (det != null && Environment.GetEnvironmentVariable("SV_MOBILETREES") == "0" && det.phonePrototypes != null) det.phonePrototypes = det.phonePrototypes.Select(p => p != null && p.name.EndsWith("_mobile") ? (SkiViewBuilder.Veg(p.name.Substring(0, p.name.Length - 7)) ?? p) : p).ToArray();   // same-build A/B: back to the full phone prefabs
        // A/B: phone keeps the cross-fade prefabs
        if (det != null && tier > 0) det.Apply(tier);
        { var bm = Environment.GetEnvironmentVariable("SV_BDMESH"); var bgo = GameObject.Find("Backdrop"); if (bm != null && bgo != null) { var mm = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/SkiView/Lakes/{id}/Meshes/{bm}.asset"); if (mm != null) bgo.GetComponent<MeshFilter>().sharedMesh = mm; Debug.Log($"[SkiView] A/B backdrop mesh {bm}: {(mm != null ? mm.vertexCount : -1)} verts"); } }   // T3 A/B: same build, old backdrop mesh
        StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3, 400f);
        int aa_ = urp0 != null ? Mathf.Max(1, urp0.msaaSampleCount) : 1;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; rt.Create();   // URP renders with the asset MSAA (aa_) internally and resolves into this target
        var px = new Texture2D(1, 1, TextureFormat.RGB24, false);
        var pr = cam.GetComponent<PlanarReflection>();
        float total = 0f;
        foreach (var view in new[] { SkiViewGame.CamMode.Broadcast, SkiViewGame.CamMode.SkierChase, SkiViewGame.CamMode.CloseChase })
        {
            game.camMode = view; cam.aspect = (float)w / h; game.ApplyVisuals(true);
            var ms = new List<double>();
            for (int i = 0; i < 36; i++)
            {
                var sw = Stopwatch.StartNew();
                if (pr != null && pr.enabled) { cam.targetTexture = rt; pr.RenderNow(); cam.targetTexture = null; }
                RenderInto(cam, rt);
                var prev = RenderTexture.active; RenderTexture.active = Resolved(rt); px.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); px.Apply(); RenderTexture.active = prev;   // GPU sync
                sw.Stop(); if (i >= 6) ms.Add(sw.Elapsed.TotalMilliseconds);
            }
            { var full = new Texture2D(w, h, TextureFormat.RGB24, false); var pv = RenderTexture.active; RenderTexture.active = Resolved(rt); full.ReadPixels(new Rect(0, 0, w, h), 0, 0); full.Apply(); RenderTexture.active = pv;
              Directory.CreateDirectory(Path.Combine(OutDir, "p20")); File.WriteAllBytes(Path.Combine(OutDir, "p20", $"perfframe_{id}_{view}.png"), full.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(full); }   // proof the probe rendered the scene
            ms.Sort(); double med = ms[ms.Count / 2]; total += (float)med;
            res.Add($"{id,-11} tier {tier} {w}x{h} {view,-10}: median {med,7:0.0} ms  (p10 {ms[ms.Count / 10],7:0.0}, p90 {ms[ms.Count * 9 / 10],7:0.0})");
        }
        res.Add($"{id,-11} tier {tier} msaa {aa_}x mean of 3 views: {total / 3f:0.0} ms");
        UnityEngine.Object.DestroyImmediate(px); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        if (urp0 != null) { urp0.shadowDistance = sd0; urp0.shadowCascadeCount = sc0; }
        cam.ResetAspect(); game.camMode = SkiViewGame.CamMode.Broadcast;
        return res;
    }

    /// <summary>Lake P1: per-camera frame time at a shot's framing. Desktop = PC quality, LakeDetail tier 0, 1920x1080; phone = Mobile
    /// quality, tier 2, 1280x720 (+ planar reflection). Median of 24 GPU-synced renders. Software GL: relative A/B numbers only.</summary>
    public static readonly List<string> shotPerf = new List<string>();
    static void ShotPerf(Camera cam, string name)
    {
        var det = UnityEngine.Object.FindFirstObjectByType<LakeDetail>(); var pr = cam.GetComponent<PlanarReflection>();
        int q0 = QualitySettings.GetQualityLevel(); var px = new Texture2D(1, 1, TextureFormat.RGB24, false); string line = $"{name,-34}";
        foreach (var (lab, qn, tier, w, h) in new[] { ("desktop", "PC", 0, 1920, 1080), ("phone", "Mobile", 2, 1280, 720) })
        {
            int qi = Array.FindIndex(QualitySettings.names, n => n.Contains(qn)); if (qi >= 0) QualitySettings.SetQualityLevel(qi, true);
            if (det != null) det.Apply(tier); SyncA2C(tier); cam.aspect = (float)w / h;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; rt.Create(); var ms = new List<double>();
            for (int i = 0; i < 30; i++)
            {
                var sw = Stopwatch.StartNew();
                if (pr != null && pr.enabled) { cam.targetTexture = rt; pr.RenderNow(); cam.targetTexture = null; }
                RenderInto(cam, rt);
                var prev = RenderTexture.active; RenderTexture.active = Resolved(rt); px.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); px.Apply(); RenderTexture.active = prev;
                sw.Stop(); if (i >= 6) ms.Add(sw.Elapsed.TotalMilliseconds);
            }
            ms.Sort(); line += $"  {lab} {ms[ms.Count / 2],7:0.0} ms"; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
        QualitySettings.SetQualityLevel(q0, true); if (det != null) det.Apply(0); SyncA2C(); cam.aspect = (float)W / H;
        UnityEngine.Object.DestroyImmediate(px); shotPerf.Add(line);
    }

    /// <summary>Pass 20 per-task run: SV_LAKES rebuilt, lake shots, then the frame-time probe (SV_TAG).</summary>
    public static void P20Lakes()
    {
        var ids = (Environment.GetEnvironmentVariable("SV_LAKES") ?? "tallpine").Split(',').Select(s => s.Trim()).ToArray();
        if (Environment.GetEnvironmentVariable("SV_NOBUILD") != "1") foreach (var id in ids) SkiViewBuilder.BuildLake(id);
        shotPerf.Clear();
        foreach (var id in ids) LakeShots(id);
        if (shotPerf.Count > 0) File.WriteAllText(Path.Combine(OutDir, $"perfcams_{Environment.GetEnvironmentVariable("SV_TAG") ?? "probe"}.txt"), string.Join("\n", shotPerf) + "\n");
        if (Environment.GetEnvironmentVariable("SV_NOPROBE") != "1") P20Perf();
        Debug.Log("[SkiView] P20Lakes done");
    }
    /// <summary>T2: 1920x1080 foliage-edge shots with the quality level's MSAA (PC = desktop, Mobile = phone tier): boat cam, eye-height shore, and a 3x crop.</summary>
    public static void P20Foliage()
    {
        string tag = Environment.GetEnvironmentVariable("SV_TAG") ?? "probe";
        foreach (var id in (Environment.GetEnvironmentVariable("SV_LAKES") ?? "tallpine,heronmarsh").Split(','))
            foreach (var qn in new[] { "PC", "Mobile" })
            {
                EditorSceneManager.OpenScene(SkiViewBuilder.LakeScenePath(id));
                var game = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>(); game.autopilot = true; game.camMode = SkiViewGame.CamMode.Broadcast; game.Init(); SyncA2C(qn == "Mobile" ? 2 : 0);
                var cam = game.cam; int qi = Array.FindIndex(QualitySettings.names, n => n.Contains(qn)); if (qi >= 0) QualitySettings.SetQualityLevel(qi, true);
                if (qn == "Mobile") { var dt = UnityEngine.Object.FindFirstObjectByType<LakeDetail>(); if (dt != null) dt.Apply(2); }
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; int aa = urp != null ? Mathf.Max(1, urp.msaaSampleCount) : 1;
                StepUntil(game, () => game.mode == SkiViewGame.Mode.Pass && game.sim.nextBuoy >= 3, 400f);
                foreach (var (vn, mode) in new[] { ("boatcam", SkiViewGame.CamMode.Broadcast), ("eye", SkiViewGame.CamMode.CloseChase) })
                {
                    game.camMode = mode; cam.aspect = 16f / 9f; game.cameraFollow = true; game.ApplyVisuals(true);
                    if (vn == "eye") { game.cameraFollow = false; Vector3 sp = game.SkierPos; Vector3 side = Vector3.Cross(Vector3.up, (game.BoatPos - sp).normalized); var p = sp + Vector3.up * 1.7f; cam.transform.position = p; cam.transform.rotation = Quaternion.LookRotation(side); cam.fieldOfView = 50f; }
                    var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; rt.Create();   // URP applies the asset MSAA internally
                    var pr = cam.GetComponent<PlanarReflection>(); if (pr != null && pr.enabled) { cam.targetTexture = rt; pr.RenderNow(); cam.targetTexture = null; }
                    RenderInto(cam, rt);
                    var prev = RenderTexture.active; RenderTexture.active = Resolved(rt); var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply(); RenderTexture.active = prev;
                    Directory.CreateDirectory(Path.Combine(OutDir, "p20"));
                    File.WriteAllBytes(Path.Combine(OutDir, "p20", $"foliage_{id}_{qn.ToLower()}_{vn}_{tag}.png"), tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); game.cameraFollow = true;
                }
                cam.ResetAspect();
            }
        Debug.Log("[SkiView] P20Foliage done " + tag);
    }

    public static void P20FoliagePerf() { if (Environment.GetEnvironmentVariable("SV_VEGIMPORT") == "1") SkiViewBuilder.VegTextureImport(); if (Environment.GetEnvironmentVariable("SV_NOBUILD") != "1") foreach (var id in (Environment.GetEnvironmentVariable("SV_LAKES") ?? "tallpine,heronmarsh").Split(',')) SkiViewBuilder.BuildLake(id); P20Foliage(); P20Perf(); }
}
