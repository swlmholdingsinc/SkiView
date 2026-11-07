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
        Check(SkiViewCharacterCapture125Logic.BoneOk(0.11152f, SkiViewCharacterCapture125Logic.HeadParentLocalY), "baseline head parent-local matches the FBX");
        Check(SkiViewCharacterCapture125Logic.BoneOk(0.1505f, SkiViewCharacterCapture125Logic.NeckParentLocalY), "baseline neck parent-local matches the FBX");
        Check(!SkiViewCharacterCapture125Logic.BoneOk(0.11152f, SkiViewCharacterCapture125Logic.LegacyCumulativeHeadY), "parent-local head is not the old cumulative constant");
        Check(!SkiViewCharacterCapture125Logic.BoneOk(0.1505f, SkiViewCharacterCapture125Logic.LegacyCumulativeNeckY), "parent-local neck is not the old cumulative constant");
        float headRoot = SkiViewCharacterCapture125Logic.BindRootY(SkiViewCharacterCapture125Logic.HeadJoint);
        float neckRoot = SkiViewCharacterCapture125Logic.BindRootY(SkiViewCharacterCapture125Logic.NeckJoint);
        Check(!SkiViewCharacterCapture125Logic.BoneOk(headRoot, SkiViewCharacterCapture125Logic.LegacyCumulativeHeadY), "FBX root-space head is not 1.55578");
        Check(!SkiViewCharacterCapture125Logic.BoneOk(neckRoot, SkiViewCharacterCapture125Logic.LegacyCumulativeNeckY), "FBX root-space neck is not 1.44886");
        Check(Mathf.Abs((headRoot - neckRoot) - (SkiViewCharacterCapture125Logic.LegacyCumulativeHeadY - SkiViewCharacterCapture125Logic.LegacyCumulativeNeckY)) <= SkiViewCharacterCapture125Logic.BoneEpsilon, "the old constants keep the bind separation and the wrong origin");
        Check(SkiViewCharacterCapture125Logic.RootDistinctFromParentLocal(headRoot, SkiViewCharacterCapture125Logic.HeadParentLocalY), "root height is not parent-local");
        Check(headRoot > 1.60f && headRoot < 1.61f && neckRoot > 1.49f && neckRoot < 1.50f, "composed bind root Y");
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
