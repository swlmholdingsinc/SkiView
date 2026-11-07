using UnityEditor;
using UnityEngine;

/// <summary>Icon-module rules. No Play mode. Run: unity_run.sh SkiViewBuildIconModuleTest.Run</summary>
public static class SkiViewBuildIconModuleTest
{
    static int fails;

    static void Check(bool ok, string what)
    {
        Debug.Log("[BuildIconTest] " + (ok ? "PASS" : "FAIL") + " " + what);
        if (!ok) fails++;
    }

    class FieldKind { public static readonly string Legacy = "legacy"; public static string Adaptive { get { return "adaptive"; } } }

    public static void Run()
    {
        fails = 0;
        Check(SkiViewBuildIconModule.Decide(false, false) == SkiViewBuildIconModule.Availability.ModuleAbsent, "editor-only skips icons");
        Check(SkiViewBuildIconModule.Decide(false, true) == SkiViewBuildIconModule.Availability.ModuleAbsent, "unsupported target does not assign icons");
        Check(SkiViewBuildIconModule.Decide(true, false) == SkiViewBuildIconModule.Availability.TypeMissing, "installed module with no kind type is a hard failure");
        Check(SkiViewBuildIconModule.Decide(true, true) == SkiViewBuildIconModule.Availability.Ready, "installed module with the kind type assigns icons");
        Check(SkiViewBuildIconModule.TwoLayers("Adaptive") && !SkiViewBuildIconModule.TwoLayers("Legacy") && !SkiViewBuildIconModule.TwoLayers("Marketing"), "only adaptive icons take two layers");
        Check(SkiViewBuildIconModule.AndroidKinds.Length == 3 && SkiViewBuildIconModule.AndroidKinds[0] == "Legacy" && SkiViewBuildIconModule.AndroidKinds[2] == "Adaptive", "android kind names");
        Check(SkiViewBuildIconModule.IosKinds.Length == 5 && SkiViewBuildIconModule.IosKinds[0] == "Application" && SkiViewBuildIconModule.IosKinds[4] == "Marketing", "ios kind names");
        Check((string)SkiViewBuildIconModule.StaticMember(typeof(FieldKind), "Legacy") == "legacy", "static field");
        Check((string)SkiViewBuildIconModule.StaticMember(typeof(FieldKind), "Adaptive") == "adaptive", "static property");
        Check(SkiViewBuildIconModule.StaticMember(typeof(FieldKind), "Missing") == null, "missing member");
        Check(SkiViewBuildIconModule.FindLoaded("System.String") == typeof(string), "loaded type");
        Check(SkiViewBuildIconModule.FindLoaded(SkiViewBuildIconModule.AndroidKindType) == null, "android extension type is not in this process");
        Check(SkiViewBuildIconModule.AndroidDll.EndsWith("UnityEditor.Android.Extensions.dll") && SkiViewBuildIconModule.IosDll.EndsWith("UnityEditor.iOS.Extensions.dll"), "module dll paths");
        Debug.Log("[BuildIconTest] " + fails + " failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
