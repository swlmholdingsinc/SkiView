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
        Check(!SkiViewCharacterCapture125Logic.BoneOk(0.11152f, SkiViewCharacterCapture125Logic.HeadCanonicalY), "parent-local head is not the canonical world height");
        Check(!SkiViewCharacterCapture125Logic.BoneOk(0.1505f, SkiViewCharacterCapture125Logic.NeckCanonicalY), "parent-local neck is not the canonical world height");
        float headSkeleton = SkiViewCharacterCapture125Logic.BindRootY(SkiViewCharacterCapture125Logic.HeadJoint);
        float neckSkeleton = SkiViewCharacterCapture125Logic.BindRootY(SkiViewCharacterCapture125Logic.NeckJoint);
        float headCanonical = SkiViewCharacterCapture125Logic.CanonicalY(headSkeleton);
        float neckCanonical = SkiViewCharacterCapture125Logic.CanonicalY(neckSkeleton);
        Check(SkiViewCharacterCapture125Logic.BoneOk(headCanonical, SkiViewCharacterCapture125Logic.HeadCanonicalY), "armature offset converts skeleton head to canonical");
        Check(SkiViewCharacterCapture125Logic.BoneOk(neckCanonical, SkiViewCharacterCapture125Logic.NeckCanonicalY), "armature offset converts skeleton neck to canonical");
        Check(SkiViewCharacterCapture125Logic.RootDistinctFromParentLocal(headSkeleton, SkiViewCharacterCapture125Logic.HeadParentLocalY), "skeleton height is not parent-local");
        Check(SkiViewCharacterCapture125Logic.CoreFrames.Length == 39, "39 core frames");
        Check(SkiViewCharacterCapture125Logic.IsCoreFrame(2322) && SkiViewCharacterCapture125Logic.IsCoreFrame(1903) && !SkiViewCharacterCapture125Logic.IsCoreFrame(0) && !SkiViewCharacterCapture125Logic.IsCoreFrame(2399), "core tier excludes the extended ends");
        Check(SkiViewCharacterCapture125Logic.IncludeReadWeight(true) && !SkiViewCharacterCapture125Logic.IncludeReadWeight(false), "a read zero is kept and an unread weight is not written");
        Check(SkiViewCharacterCapture125Logic.BakeUseScale == false && SkiViewCharacterCapture125Logic.BakeSpace.Contains("useScale=false") && SkiViewCharacterCapture125Logic.BakeSpace.Contains("Do not multiply by lossyScale again"), "bake space is documented once");
        Check(SkiViewCharacterCapture125Logic.BakeRole("body", "Body_LOD0", "Body_LOD0") && !SkiViewCharacterCapture125Logic.BakeRole("body", "Body_LOD1", "Body_LOD1"), "body LOD0 is baked and body LOD1 is not");
        Check(SkiViewCharacterCapture125Logic.EnabledDuplicateIds(new[] { 1, 2, 3 }, new[] { "Body_LOD0", "Body_LOD1", "Body_LOD2" }, new[] { true, true, true }).Count == 0, "distinct LOD names are not same-mesh duplicates");
        Check(SkiViewCharacterCapture125Logic.IsBodyLod("INTERNAL_ONLY_125_Body_LOD1", "Body_LOD1", 1) && SkiViewCharacterCapture125Logic.IsBodyLod("INTERNAL_ONLY_125_Body_LOD2", "Body_LOD2", 2) && !SkiViewCharacterCapture125Logic.IsBodyLod("Body_LOD0", "Body_LOD0", 1), "body LOD slots are named apart");
        Check(SkiViewCharacterCapture125Logic.IsFace("SkiView_Face_Male_V1", "Face") && !SkiViewCharacterCapture125Logic.IsFace("Body_LOD0", "Body_LOD0"), "face is not the body");
        Check(SkiViewCharacterCapture125Logic.VestContentLabel(SkiViewCharacterCapture125Logic.ExpectedOriginalVestSha256) == "original", "original vest hash");
        Check(SkiViewCharacterCapture125Logic.VestContentLabel("8d8dd716" + new string('a', 52) + "6283") == "r6", "r6 hash span");
        Check(SkiViewCharacterCapture125Logic.VestContentLabel(SkiViewCharacterCapture125Logic.ExpectedOriginalVestSha256) != "r6", "a vest file name is not the label");
        Check(SkiViewBodyHeadCoverage.InHeadIsland(0.7f, 0.8f) && !SkiViewBodyHeadCoverage.InHeadIsland(0.2f, 0.8f) && !SkiViewBodyHeadCoverage.InHeadIsland(0.7f, 0.2f), "body head island is the authored UV window");
        Check(SkiViewBodyHeadCoverage.DropTriangle(true, false, false) && !SkiViewBodyHeadCoverage.DropTriangle(false, false, false), "an island corner drops the triangle");
        var dups = SkiViewCharacterCapture125Logic.EnabledDuplicateIds(new[] { -13886, 44948, 7 }, new[] { "Vest_LOD0", "Vest_LOD0", "Body_LOD0" }, new[] { true, true, true });
        Check(dups.Count == 2 && dups[0] == -13886 && dups[1] == 44948, "two enabled vests are the duplicate pair");
        Check(SkiViewCharacterCapture125Logic.EnabledDuplicateIds(new[] { 1, 2 }, new[] { "Vest_LOD0", "Vest_LOD0" }, new[] { true, false }).Count == 0, "a disabled twin is not a duplicate surface");
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
