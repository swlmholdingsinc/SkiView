using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiView;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Android;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets.Android;

/// <summary>Pass 16: per-lake content split (Addressables + Addressables for Android / Play Asset Delivery).
/// Player build = SkiViewBoot.unity + code. Groups:
///   Core_Home   - the Ski View home lake scene (local; Android install-time)
///   Core_Shared - every asset used by two or more lake scenes: boat, skier, FX, tree kit, water, shaders... (local; install-time)
///   Lake_&lt;id&gt;  - one per non-home lake: its scene + everything only it uses (terrain, sky, buildings) (local bundle on WebGL,
///                 fetched only when that lake is picked; Android AAB: its own on-demand asset pack)</summary>
public static class SkiViewContent
{
    public const string BOOT = "Assets/SkiView/Scenes/SkiViewBoot.unity";
    static readonly string[] SKIP_EXT = { ".cs", ".dll", ".asmdef", ".asmref", ".unity" };

    public static void EnsureBootScene()
    {
        var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("BootCamera"); var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.07f, 0.09f); cam.depth = -100;
        var boot = new GameObject("Boot").AddComponent<SkiViewBoot>(); boot.homeScene = Path.GetFileNameWithoutExtension(SkiViewBuilder.SCENE);
        EditorSceneManager.SaveScene(sc, BOOT);
    }

    static AddressableAssetGroup Group(AddressableAssetSettings s, string name)
    {
        var g = s.FindGroup(name) ?? s.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        var b = g.GetSchema<BundledAssetGroupSchema>() ?? g.AddSchema<BundledAssetGroupSchema>();
        b.BuildPath.SetVariableByName(s, AddressableAssetSettings.kLocalBuildPath);
        b.LoadPath.SetVariableByName(s, AddressableAssetSettings.kLocalLoadPath);
        b.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        b.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        b.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
        b.IncludeInBuild = true;
        return g;
    }

    /// <summary>Creates / refreshes the Addressables settings, groups and entries. Safe to run repeatedly.</summary>
    public static AddressableAssetSettings Setup()
    {
        var s = AddressableAssetSettingsDefaultObject.GetSettings(true);
        s.BuildRemoteCatalog = false;
        s.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
        // Play Asset Delivery init (the package's own menu action; its dialog is answered "Add" via its test hook)
        var setup = typeof(PlayAssetDeliverySchema).Assembly.GetType("UnityEditor.AddressableAssets.Android.PlayAssetDeliverySetup");
        var notInit = setup?.GetMethod("PlayAssetDeliveryNotInitialized", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (setup != null && notInit != null && (bool)notInit.Invoke(null, null))
        {
            setup.GetProperty("ForcePADToExistingAddressablesGroup", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, (bool?)true);
            setup.GetMethod("InitPlayAssetDelivery", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        }
        var home = Group(s, "Core_Home"); var shared = Group(s, "Core_Shared");
        var scenes = new List<(string path, string address, AddressableAssetGroup g, DeliveryType dt)> { (SkiViewBuilder.SCENE, Path.GetFileNameWithoutExtension(SkiViewBuilder.SCENE), home, DeliveryType.InstallTime) };
        foreach (var id in SkiViewBuilder.LAKES)
            if (File.Exists(SkiViewBuilder.LakeScenePath(id))) scenes.Add((SkiViewBuilder.LakeScenePath(id), Path.GetFileNameWithoutExtension(SkiViewBuilder.LakeScenePath(id)), Group(s, "Lake_" + id), DeliveryType.OnDemand));
        foreach (var g in new[] { home, shared }.Concat(scenes.Select(x => x.g)).Distinct())
        {
            var pad = g.GetSchema<PlayAssetDeliverySchema>() ?? g.AddSchema<PlayAssetDeliverySchema>();
            pad.IncludeInCustomAssetPack = false;
            pad.AssetPackDeliveryType = g.Name.StartsWith("Lake_") ? DeliveryType.OnDemand : DeliveryType.InstallTime;
            EditorUtility.SetDirty(g);
        }
        foreach (var x in scenes) { var e = s.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(x.path), x.g, false, false); e.address = x.address; }
        // shared kit = assets referenced by >= 2 lake scenes (so nothing is duplicated between the home and lake bundles)
        var count = new Dictionary<string, int>();
        foreach (var x in scenes)
            foreach (var d in AssetDatabase.GetDependencies(x.path, true))
            {
                string ext = Path.GetExtension(d).ToLowerInvariant();
                if (SKIP_EXT.Contains(ext) || d.Contains("/Editor/") || d.Contains("/Resources/") || d.StartsWith("ProjectSettings")) continue;
                count[d] = count.TryGetValue(d, out var c) ? c + 1 : 1;
            }
        var want = new HashSet<string>(count.Where(kv => kv.Value >= 2).Select(kv => AssetDatabase.AssetPathToGUID(kv.Key)).Where(gd => !string.IsNullOrEmpty(gd)));
        foreach (var e in shared.entries.ToList()) if (!want.Contains(e.guid)) s.RemoveAssetEntry(e.guid, false);
        int added = 0;
        foreach (var gd in want)
        {
            try { var e = s.CreateOrMoveEntry(gd, shared, false, false); if (e != null) { e.address = AssetDatabase.GUIDToAssetPath(gd); added++; } }
            catch (Exception ex) { Debug.LogWarning("[SkiView] shared entry skipped " + AssetDatabase.GUIDToAssetPath(gd) + ": " + ex.Message); }
        }
        s.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SkiView] content split: {scenes.Count} lake scenes, shared kit {added} assets; groups {string.Join(", ", s.groups.Select(g => g.Name + "(" + g.entries.Count + ")"))}");
        return s;
    }

    /// <summary>Selects the player data builder: Play Asset Delivery on Android, packed mode elsewhere.</summary>
    public static void SelectBuilder(bool android)
    {
        var s = AddressableAssetSettingsDefaultObject.Settings;
        int i = android ? s.DataBuilders.FindIndex(b => b is BuildScriptPlayAssetDelivery) : s.DataBuilders.FindIndex(b => b != null && b.GetType() == typeof(BuildScriptPackedMode));
        if (i >= 0) s.ActivePlayerDataBuilderIndex = i;
        EditorUtility.SetDirty(s); AssetDatabase.SaveAssets();
        Debug.Log($"[SkiView] addressables data builder: {s.ActivePlayerDataBuilder?.Name}");
    }

    /// <summary>Report the built bundle sizes per group (Library/com.unity.addressables/aa/&lt;target&gt;).</summary>
    public static string BundleReport()
    {
        string dir = UnityEngine.AddressableAssets.Addressables.BuildPath;
        if (!Directory.Exists(dir)) return "(no addressables build at " + dir + ")";
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
        var lines = files.Select(f => (f, new FileInfo(f).Length)).OrderByDescending(x => x.Length).Select(x => $"{Path.GetRelativePath(dir, x.f)} {x.Length / 1048576.0:0.00} MB");
        string r = string.Join("\n  ", lines);
        Debug.Log("[SkiView] addressables output (" + dir + "):\n  " + r);
        return r;
    }
}
