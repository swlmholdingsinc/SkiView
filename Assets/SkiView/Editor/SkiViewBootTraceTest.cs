using UnityEditor;
using UnityEngine;

/// <summary>Boot status line. No play mode. Run: unity_run.sh SkiViewBootTraceTest.Run</summary>
public static class SkiViewBootTraceTest
{
    static int fails;

    static void Check(bool ok, string what)
    {
        Debug.Log("[BootTrace] " + (ok ? "PASS" : "FAIL") + " " + what);
        if (!ok) fails++;
    }

    public static void Run()
    {
        fails = 0;
        string waiting = SkiView.SkiViewBoot.BootLine("addressables-init", "SkiViewBoot", false, 1920, 1080, null);
        Check(waiting.Contains("phase=addressables-init") && waiting.Contains("scene=SkiViewBoot") && waiting.Contains("busy=0") && waiting.Contains("screen=1920x1080") && !waiting.Contains("\n"), waiting);
        string failed = SkiView.SkiViewBoot.BootLine("load-failed SkiViewLake", "SkiViewBoot", false, 800, 600, "InvalidKeyException: SkiViewLake");
        Check(failed.Contains("phase=load-failed SkiViewLake") && failed.Contains("busy=0") && failed.Contains("InvalidKeyException: SkiViewLake"), failed);
        string none = SkiView.SkiViewBoot.BootLine("awake", "", true, 0, 0, null);
        Check(none.Contains("scene=(none)") && none.Contains("busy=1") && none.Contains("screen=0x0"), none);
        string owned;
        Check(SkiView.SkiViewBoot.InitAllowsLoad(true, true, null, out owned) && owned == null, "owned success continues");
        string failedInit;
        Check(!SkiView.SkiViewBoot.InitAllowsLoad(true, false, "catalog missing", out failedInit) && failedInit == "catalog missing", "real init failure stays");
        string blank;
        Check(!SkiView.SkiViewBoot.InitAllowsLoad(true, false, "", out blank) && blank == "init failed", "failed init without an exception stays failed");
        string released;
        Check(!SkiView.SkiViewBoot.InitAllowsLoad(false, false, null, out released) && released == "invalid handle", "a handle we asked to keep that is still invalid stays failed");
        Debug.Log("[BootTrace] " + fails + " failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
