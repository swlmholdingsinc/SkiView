using System;
using System.Reflection;

/// <summary>
/// Decides whether store icon kinds can be read. Those kinds are static fields on
/// UnityEditor.Android.Extensions and UnityEditor.iOS.Extensions. An editor-only install
/// does not contain those assemblies. UNITY_ANDROID is not used: it follows the active
/// build target, so it would drop icons while the editor is still on Standalone.
/// </summary>
public static class SkiViewBuildIconModule
{
    public const string AndroidKindType = "UnityEditor.Android.AndroidPlatformIconKind";
    public const string IosKindType = "UnityEditor.iOS.iOSPlatformIconKind";
    public const string AndroidDll = "PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll";
    public const string IosDll = "PlaybackEngines/iOSSupport/UnityEditor.iOS.Extensions.dll";

    public static readonly string[] AndroidKinds = { "Legacy", "Round", "Adaptive" };
    public static readonly string[] IosKinds = { "Application", "Spotlight", "Settings", "Notification", "Marketing" };

    public enum Availability { ModuleAbsent, TypeMissing, Ready }

    public static Availability Decide(bool buildTargetSupported, bool kindTypeLoaded)
    {
        if (!buildTargetSupported) return Availability.ModuleAbsent;
        if (!kindTypeLoaded) return Availability.TypeMissing;
        return Availability.Ready;
    }

    public static bool TwoLayers(string member)
    {
        return member == "Adaptive";
    }

    public static object StaticMember(Type type, string name)
    {
        if (type == null || string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        var field = type.GetField(name, flags);
        if (field != null) return field.GetValue(null);
        var prop = type.GetProperty(name, flags);
        if (prop != null) return prop.GetValue(null, null);
        return null;
    }

    public static Type FindLoaded(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return null;
        var asms = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < asms.Length; i++)
        {
            Type found = null;
            try { found = asms[i].GetType(fullName, false); }
            catch (Exception) { found = null; }
            if (found != null) return found;
        }
        return null;
    }
}
