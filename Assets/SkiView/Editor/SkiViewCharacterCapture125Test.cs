using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SkiView;

/// <summary>Capture rules only. No GPU. Run: unity_run.sh SkiViewCharacterCapture125Test.Run</summary>
public static class SkiViewCharacterCapture125Test
{
    static int fails;

    static void Check(bool ok, string what)
    {
        Debug.Log("[CharCapTest] " + (ok ? "PASS" : "FAIL") + " " + what);
        if (!ok) fails++;
    }

    public static void Run()
    {
        fails = 0;
        Check(SkiViewCharacterCapture125Logic.BoneOk(1.55578f, SkiViewCharacterCapture125Logic.HeadLocalY), "canonical head");
        Check(SkiViewCharacterCapture125Logic.BoneOk(1.44886f, SkiViewCharacterCapture125Logic.NeckLocalY), "canonical neck");
        Check(!SkiViewCharacterCapture125Logic.BoneOk(1.50f, SkiViewCharacterCapture125Logic.HeadLocalY), "wrong head fails");
        Check(SkiViewCharacterCapture125Logic.PoseRoll("front") == 0f && SkiViewCharacterCapture125Logic.PoseRoll("left") == 35f && SkiViewCharacterCapture125Logic.PoseRoll("right") == -35f, "poses differ");
        Check(SkiViewCharacterCapture125Logic.OriginalVestName("INTERNAL_ONLY_125_Gear_Vest_LOD0", "INTERNAL_ONLY_125_Gear_Vest_LOD0"), "original vest");
        Check(!SkiViewCharacterCapture125Logic.OriginalVestName("JumpVest_LOD0", "JumpVest"), "jump vest rejected");
        Check(!SkiViewCharacterCapture125Logic.OriginalVestName("Vest_candidate_b", "Vest_candidate_b"), "candidate rejected");
        Check(SkiViewCharacterCapture125Logic.LumaOk("lit", 40f, 12f), "lit image with detail passes");
        Check(!SkiViewCharacterCapture125Logic.LumaOk("lit", 0f, 0f), "black frame fails");
        Check(SkiViewCharacterCapture125Logic.ShotsDiffer(1u, 2u) && !SkiViewCharacterCapture125Logic.ShotsDiffer(9u, 9u), "identical shots fail");
        var missing = SkiViewCharacterCapture125Logic.Missing(p => p.EndsWith("PC_RPAsset.asset"));
        Check(missing.Count == SkiViewCharacterCapture125Logic.RequiredPaths.Length - 1 && missing.IndexOf("Assets/Settings/PC_RPAsset.asset") < 0, "manifest reports every missing path");
        Debug.Log("[CharCapTest] " + fails + " failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
