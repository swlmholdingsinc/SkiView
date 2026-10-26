using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Headless builds. Outputs go to /workspace/skiview-unity/Builds/.</summary>
public static class SkiViewBuild
{
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));


    /// <summary>Play upload key for com.swlm.cutline. Reads /workspace/waterski-game/signing/keystore.properties (chmod 600). Never logs passwords.</summary>
    static bool ApplyCutlineUploadSigning()
    {
        string propsPath = Environment.GetEnvironmentVariable("SV_KEYSTORE_PROPS")
            ?? "/workspace/waterski-game/signing/keystore.properties";
        if (!File.Exists(propsPath)) { Debug.LogError("[SkiView] missing keystore.properties at " + propsPath); return false; }
        var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(propsPath))
        {
            var line = raw.Trim(); if (line.Length == 0 || line.StartsWith("#") || !line.Contains("=")) continue;
            int eq = line.IndexOf('='); map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        if (!map.ContainsKey("storeFile") || !map.ContainsKey("storePassword") || !map.ContainsKey("keyAlias") || !map.ContainsKey("keyPassword"))
        { Debug.LogError("[SkiView] keystore.properties missing required keys"); return false; }
        string store = map["storeFile"];
        if (!Path.IsPathRooted(store)) store = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(propsPath), store));
        if (!File.Exists(store)) { Debug.LogError("[SkiView] keystore file missing: " + store); return false; }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = store;
        PlayerSettings.Android.keystorePass = map["storePassword"];
        PlayerSettings.Android.keyaliasName = map["keyAlias"];
        PlayerSettings.Android.keyaliasPass = map["keyPassword"];
        Debug.Log("[SkiView] Android signing: cutline upload keystore (" + Path.GetFileName(store) + ", alias set)");
        return true;
    }

    static void AppIcons()
    {
        const string P = "Assets/SkiView/Branding/";
        var imp = AssetImporter.GetAtPath(P + "icon-512.png") as TextureImporter; if (imp == null) return;
        if (imp.textureType != TextureImporterType.Default || imp.mipmapEnabled || imp.npotScale != TextureImporterNPOTScale.None) { imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = false; imp.npotScale = TextureImporterNPOTScale.None; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.SaveAndReimport(); }
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(P + "icon-512.png"); var clear = AssetDatabase.LoadAssetAtPath<Texture2D>(P + "icon-fg-clear.png");
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        var tgt = UnityEditor.Build.NamedBuildTarget.Android;
        foreach (var kind in new[] { UnityEditor.Android.AndroidPlatformIconKind.Legacy, UnityEditor.Android.AndroidPlatformIconKind.Round, UnityEditor.Android.AndroidPlatformIconKind.Adaptive })
        {
            var icons = PlayerSettings.GetPlatformIcons(tgt, kind);
            foreach (var ic in icons) { if (kind == UnityEditor.Android.AndroidPlatformIconKind.Adaptive) ic.SetTextures(icon, clear); else ic.SetTexture(icon); }
            PlayerSettings.SetPlatformIcons(tgt, kind, icons);
        }
        Debug.Log("[SkiView] app icons set from Branding/icon-512.png (legacy / round / adaptive)");
    }

    static void Report(string what, BuildReport r, string outPath)
    {
        var s = r.summary;
        long size = File.Exists(outPath) ? new FileInfo(outPath).Length :
            (Directory.Exists(outPath) ? Directory.GetFiles(outPath, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : 0);
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm} {what}: {s.result} | {s.totalTime.TotalSeconds:0}s | {size / 1048576.0:0.0} MB | errors {s.totalErrors} warnings {s.totalWarnings} | {outPath}";
        Debug.Log("[SkiView] " + line);
        File.AppendAllText(Path.Combine(Root, "Builds", "build-log.txt"), line + "\n");
        if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(2);
    }

    public static void Android()
    {
        AppIcons();   // pass 19: Julia's Play icon (google-play/icon-512.png) as legacy, round and adaptive (background layer; the art sits in the 66% safe circle)
        Directory.CreateDirectory(Path.Combine(Root, "Builds/Android"));
        SkiViewBuilder.ConfigureProject();
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.development = false;
        bool release = Environment.GetEnvironmentVariable("SV_RELEASE") == "1";
        if (release && !ApplyCutlineUploadSigning()) { if (Application.isBatchMode) EditorApplication.Exit(3); return; }
        SkiViewContent.Setup();
        // 1) sideload APK: Addressables content (all groups) in StreamingAssets inside the APK
        SkiViewContent.SelectBuilder(false);
        EditorUserBuildSettings.buildAppBundle = false; PlayerSettings.Android.splitApplicationBinary = false;
        string outp = Path.Combine(Root, "Builds/Android", release ? "SkiView-release.apk" : "SkiView-N1-greybox-debug.apk");
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = SkiViewBuilder.BuildScenes(), locationPathName = outp, target = BuildTarget.Android, options = BuildOptions.None });
        Report(release ? "Android APK (IL2CPP arm64, cutline upload key, all content inside)" : "Android APK (IL2CPP arm64, debug keystore, all content inside)", r, outp);
        SkiViewContent.BundleReport();
        // 2) Play Store AAB: split binary + Play Asset Delivery (core install-time, each other lake an on-demand pack)
        EditorUserBuildSettings.buildAppBundle = true; PlayerSettings.Android.splitApplicationBinary = true;
        string aab = Path.Combine(Root, "Builds/Android", release ? "SkiView-release.aab" : "SkiView-N1-greybox.aab");
        try
        {
            var r2 = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = SkiViewBuilder.BuildScenes(), locationPathName = aab, target = BuildTarget.Android, options = BuildOptions.None });
            Report(release ? "Android AAB release (PAD + cutline upload key, com.swlm.cutline)" : "Android AAB (PAD: core install-time, lakes on-demand)", r2, aab);
        }
        finally { EditorUserBuildSettings.buildAppBundle = false; PlayerSettings.Android.splitApplicationBinary = false; SkiViewContent.SelectBuilder(false); }
    }

    public static void WebGL()
    {
        Directory.CreateDirectory(Path.Combine(Root, "Builds/WebGL"));
        SkiViewBuilder.ConfigureProject();
        // favour build time over wasm size on this CPU-only box
        try
        {
            var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.WebGL.UserBuildSettings")).FirstOrDefault(x => x != null);
            var p = t?.GetProperty("codeOptimization");
            if (p != null) { var e = Enum.Parse(p.PropertyType, "BuildTimes"); p.SetValue(null, e); Debug.Log("[SkiView] WebGL codeOptimization=BuildTimes"); }
        }
        catch (Exception ex) { Debug.LogWarning("[SkiView] codeOptimization not set: " + ex.Message); }
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        PlayerSettings.WebGL.template = "PROJECT:SkiViewMinimal";   // Phase 1 review #2: canvas-only page, no Unity footer/logo
        SkiViewContent.Setup(); SkiViewContent.SelectBuilder(false);   // pass 16: lakes as Addressables bundles
        string outp = Path.Combine(Root, "Builds/WebGL");
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = SkiViewBuilder.BuildScenes(),
            locationPathName = outp,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        Report("WebGL (gzip + fallback)", r, outp);
        SkiViewContent.BundleReport();
    }
}
