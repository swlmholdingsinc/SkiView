using UnityEditor;
using UnityEngine;
using SkiView;

/// <summary>Pose-export rules. No Play mode. Run: unity_run.sh SkiViewPoseEvidence125Test.Run</summary>
public static class SkiViewPoseEvidence125Test
{
    static int fails;

    static void Check(bool ok, string what)
    {
        Debug.Log("[PoseEvTest] " + (ok ? "PASS" : "FAIL") + " " + what);
        if (!ok) fails++;
    }

    public static void Run()
    {
        fails = 0;
        Check(SkiViewPoseEvidence125Logic.BothAtLeast(100f, 100f, 99f), "full bend and armhole together");
        Check(!SkiViewPoseEvidence125Logic.BothAtLeast(100f, 40f, 99f), "armhole below the pair");
        Check(!SkiViewPoseEvidence125Logic.BothAtLeast(10f, 100f, 99f), "bend below the pair");
        Check(SkiViewPoseEvidence125Logic.Pair(80f, 30f) == 30f, "pair is the smaller weight");
        Check(SkiViewPoseEvidence125Logic.Keep(0, 6, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f), "stride keeps frame 0");
        Check(!SkiViewPoseEvidence125Logic.Keep(1, 6, 10f, 10f, 10f, 10f, 10f, 10f, 1f, 1f), "non-stride without a new peak is dropped");
        Check(SkiViewPoseEvidence125Logic.Keep(1, 6, 40f, 10f, 40f, 40f, 40f, 40f, 0f, 0f), "a higher simultaneous pair is kept");
        Check(SkiViewPoseEvidence125Logic.Keep(2, 6, 0f, 0f, 80f, 10f, 0f, 0f, 0f, 0f), "a new bend peak is kept");
        Check(SkiViewPoseEvidence125Logic.Keep(3, 6, 0f, 0f, 0f, 0f, 0f, 0f, 30f, 5f), "a new lean peak is kept");
        Check(SkiViewPoseEvidence125Logic.BendFwd == "SK_bendFwd_fold_Vest" && SkiViewPoseEvidence125Logic.ArmholeR == "SK_armhole_ease_Vest_R", "key names match the vest driver");
        Check(Mathf.Abs(SkiViewPoseEvidence125Logic.HipFlexDeg(140f) - 40f) < 0.001f, "hip flexion is 180 minus the spine-thigh angle");
        Check(Mathf.Abs(SkiViewPoseEvidence125Logic.ArmForwardDeg(0f)) < 0.001f, "arm across the chest is zero forward");
        Check(Mathf.Abs(SkiViewPoseEvidence125Logic.ArmForwardDeg(1f) - 90f) < 0.001f, "arm along chest forward is 90 degrees");
        Check(SkiViewPoseEvidence125Logic.ArmForwardDeg(2f) == SkiViewPoseEvidence125Logic.ArmForwardDeg(1f), "the arm dot is clamped");
        Debug.Log("[PoseEvTest] " + fails + " failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
