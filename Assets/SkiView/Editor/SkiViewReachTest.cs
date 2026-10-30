using UnityEditor;
using UnityEngine;
using SkiView;

/// <summary>
/// #125 (Ray's per-buoy pose table): the boat is always inside the turn, so EVERY buoy reaches inward toward the boat.
/// Odd/even only mirror the course side (SkierSim.BuoyX), which comes in through toBoat; the outside hand releases.
/// The hookup after a ball stays on that buoy until the skier crosses the centreline.
/// Run: unity_run.sh SkiViewReachTest.Run
/// </summary>
public static class SkiViewReachTest
{
    public static void Run()
    {
        int fails = 0;
        void Check(bool ok, string what)
        {
            Debug.Log($"[ReachTest] {(ok ? "PASS" : "FAIL")} {what}");
            if (!ok) fails++;
        }

        Vector3 toBoat = new Vector3(4f, 0.2f, 8f);
        Vector3 right = Vector3.right;
        for (int b = 1; b <= 6; b++)
        {
            Vector3 dir = SkierPoses.FreeArmDir(toBoat, b);
            float toward = Vector3.Dot(dir, new Vector3(toBoat.x, 0f, toBoat.z).normalized);
            Check(SkierPoses.ReachTowardBoat(b) == 1, $"buoy {b} reaches inward toward the boat");
            Check(toward > 0.9f, $"buoy {b} reach direction toward the boat (dot {toward:0.00})");
        }
        foreach (var tb in new[] { new Vector3(4f, 0.2f, 8f), new Vector3(-4f, 0.2f, 8f), new Vector3(6f, 0f, 2f), new Vector3(-6f, 0f, 2f) })
            for (int b = 1; b <= 6; b++)
                Check(Vector3.Dot(SkierPoses.FreeArmDir(tb, b), new Vector3(tb.x, 0f, tb.z).normalized) > 0.9f, $"boat at {tb}: buoy {b} reaches toward it");
        // mirror by course side: the boat flips from the skier's right to left between odd and even buoys
        Vector3 toBoatOther = Vector3.Reflect(toBoat, Vector3.right);
        Check(Mathf.Abs(SkierPoses.HookupHipSign(1, toBoat, right) + SkierPoses.HookupHipSign(2, toBoatOther, right)) < 0.01f, "buoy 2 hookup hip mirrors buoy 1 across the course");
        for (int b = 3; b <= 6; b++)
            Check(Mathf.Abs(SkierPoses.HookupHipSign(b, toBoat, right) - SkierPoses.HookupHipSign(1, toBoat, right)) < 0.01f, $"buoy {b} hookup hip follows the boat side only");

        // nextBuoy is 0-based. pendingHalf != 0 means the ball just taken is still the active turn (hookup).
        Check(SkierPoses.ActiveTurnBuoy(0, 0f) == 1, "approaching buoy 1");
        Check(SkierPoses.ActiveTurnBuoy(1, 1f) == 1, "hookup after buoy 1 (pending half, next is 2)");
        Check(SkierPoses.ActiveTurnBuoy(1, 0f) == 2, "after the centreline, buoy 2");
        Check(SkierPoses.ActiveTurnBuoy(2, -1f) == 2, "hookup after buoy 2");
        Check(SkierPoses.ActiveTurnBuoy(2, 0f) == 3, "buoy 3");
        Check(SkierPoses.ActiveTurnBuoy(3, 1f) == 3, "buoy 3 hookup");
        Check(SkierPoses.ActiveTurnBuoy(5, 0f) == 6, "buoy 6");
        Check(SkierPoses.ActiveTurnBuoy(6, -1f) == 6, "buoy 6 hookup");

        Debug.Log($"[ReachTest] {fails} failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
