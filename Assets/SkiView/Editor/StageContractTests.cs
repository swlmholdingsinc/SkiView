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
