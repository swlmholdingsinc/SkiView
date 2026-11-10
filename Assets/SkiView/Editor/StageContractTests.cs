using System;
using SkiView;

/// <summary>Boot and catalog checks. Run() returns the failure count. No Unity player is started.</summary>
public static class StageContractTests
{
    public static int Run(Action<string> fail)
    {
        int n = 0;
        Action<bool, string> F = (ok, m) => { if (!ok) { n++; if (fail != null) fail(m); } };

        F(!SkiViewBootContract.Stalled("addressables-init", 7f), "init under 8s is not yet a stall");
        F(SkiViewBootContract.Stalled("addressables-init", 8f), "init with no later step is a stall");
        F(!SkiViewBootContract.Stalled("lake-ready SkiViewLake", 30f), "a ready lake is not a stall");
        F(SkiViewBootContract.StallFault("addressables-init").IndexOf("addressables-init", StringComparison.Ordinal) >= 0, "stall names the phase");
        F(SkiViewBootContract.StallFault("addressables-init").IndexOf("No red browser message", StringComparison.Ordinal) >= 0, "stall does not require a red console line");
        string stall = SkiViewBootContract.StallFault("load-scene SkiViewLake");
        F(SkiViewBootContract.NextFault(true, stall, null) == null, "a later step clears a stall");
        F(SkiViewBootContract.NextFault(false, stall, null) == stall, "the same step keeps the stall");
        F(SkiViewBootContract.NextFault(true, "invalid handle", null) == "invalid handle", "a real fault stays when the phase advances");
        F(SkiViewBootContract.NextFault(true, stall, "load scene failed") == "load scene failed", "an explicit failure replaces a stall");
        F(SkiViewBootContract.HideBootOverlay("lake-ready SkiViewLake", null), "lake-ready with no fault hides the overlay");
        F(SkiViewBootContract.HideBootOverlay("lake-ready SkiViewLake", stall), "lake-ready hides a leftover stall");
        F(!SkiViewBootContract.HideBootOverlay("lake-ready SkiViewLake", "load scene failed"), "a real load error stays visible");
        F(!SkiViewBootContract.HideBootOverlay("load-scene SkiViewLake", null), "load-scene stays on screen");
        F(106805050L >= CatalogCohesion.MaxBundleBytes, "the shared bundle is over 100 MiB");
        F(104857599L < CatalogCohesion.MaxBundleBytes, "one byte under 100 MiB is inside the cap");

        F(!SkiViewBootContract.BrowserCacheSupported(false, false), "plain HTTP on a LAN IP has no Cache API");
        F(!SkiViewBootContract.BrowserCacheSupported(false, true), "an insecure origin does not keep UnityCache");
        F(SkiViewBootContract.BrowserCacheSupported(true, true), "127.0.0.1 and https keep the Cache API");
        string settings = SkiViewBootContract.SettingsUrl("http://192.168.1.155:8095/StreamingAssets");
        F(settings == "http://192.168.1.155:8095/StreamingAssets/aa/settings.json", "runtime data is settings.json under StreamingAssets/aa");
        F(SkiViewBootContract.CacheControl(false, false, settings) == "no-store", "a LAN origin fetches settings.json directly");
        F(SkiViewBootContract.CacheControl(true, true, "http://127.0.0.1:8095/Build/player.data") == "must-revalidate", "a secure origin still caches the player data file");
        F(SkiViewBootContract.CacheControl(true, true, "http://127.0.0.1:8095/aa/core_shared.bundle") == "must-revalidate", "a secure origin still caches bundles");
        F(SkiViewBootContract.CacheControl(true, true, settings) == "no-store", "settings.json is not put in UnityCache");
        string named = SkiViewBootContract.DescribeInitFailure("Addressables - Unable to load runtime data at location ResourceManagerRuntimeData.", false, settings);
        F(named.IndexOf("Cache API", StringComparison.Ordinal) >= 0 && named.IndexOf(settings, StringComparison.Ordinal) >= 0, "init failure names the cache step and the settings url");
        F(SkiViewBootContract.DescribeInitFailure("load scene failed", false, settings) == "load scene failed", "a later load error stays a load error");

        F(SkiViewLakeChoice.BuiltLakeIds.Length == 10, "the menu lists ten built lakes");
        F(System.Array.IndexOf(SkiViewLakeChoice.BuiltLakeIds, "skiview") == 0, "Ski View Lake is in the list");
        F(System.Array.IndexOf(SkiViewLakeChoice.BuiltLakeIds, "heronmarsh") > 0, "Heron Marsh is in the list");
        F(SkiViewLakeChoice.Selectable(true, "heronmarsh", false, false, 14800000, false), "a WebGL lake bundle stays selectable");
        F(!SkiViewLakeChoice.Selectable(false, "heronmarsh", false, false, 14800000, false), "a phone pack that is not on the device is not yet selectable");
        F(SkiViewLakeChoice.Selectable(false, "skiview", false, false, 0, true), "the home lake stays selectable");
        F(SkiViewLakeChoice.Selectable(false, "cedarbend", false, false, 0, true), "an installed phone pack stays selectable offline");

        var hashed = CatalogCohesion.Check(
            new[] { "\"core_home_scenes_all_0123456789abcdef0123456789abcdef.bundle\"" },
            new[] { "core_home_scenes_all.bundle" });
        F(!hashed.Proven && hashed.Missing.Count == 1, "a hashed catalog id is not the unhashed file");
        var exact = CatalogCohesion.Check(
            new[] { "core_home_scenes_all.bundle" },
            new[] { "aa/core_home_scenes_all.bundle" });
        F(exact.Proven, "the same file name is a match");
        var none = CatalogCohesion.Check(new[] { "no ids here" }, new[] { "core_home_scenes_all.bundle" });
        F(!none.Proven, "no catalog ids is not a proven reuse");
        return n;
    }
}
