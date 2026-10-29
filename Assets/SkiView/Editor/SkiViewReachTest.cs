using UnityEditor;
using UnityEngine;
using SkiView;

/// <summary>
/// Scott: buoys 1, 3, 5 reach toward the boat; 2, 4, 6 reach away.
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
            int want = (b & 1) == 1 ? 1 : -1;
            int got = SkierPoses.ReachTowardBoat(b);
            Vector3 dir = SkierPoses.FreeArmDir(toBoat, b);
            float toward = Vector3.Dot(dir, new Vector3(toBoat.x, 0f, toBoat.z).normalized);
            Check(got == want, $"buoy {b} reach sign {got} (want {want})");
            Check(want > 0 ? toward > 0.9f : toward < -0.9f, $"buoy {b} free arm {(toward > 0 ? "toward" : "away")} the boat (dot {toward:0.00})");
        }

        Check(Vector3.Dot(SkierPoses.FreeArmDir(toBoat, 1), SkierPoses.FreeArmDir(toBoat, 2)) < -0.9f, "buoy 2 free arm mirrors buoy 1");
        Check(Mathf.Abs(SkierPoses.HookupHipSign(1, toBoat, right) + SkierPoses.HookupHipSign(2, toBoat, right)) < 0.01f, "hookup hip for buoy 2 mirrors buoy 1");
        Check(Mathf.Abs(SkierPoses.HookupHipSign(3, toBoat, right) - SkierPoses.HookupHipSign(1, toBoat, right)) < 0.01f, "hookup hip for buoy 3 matches buoy 1");
        Check(Mathf.Abs(SkierPoses.HookupHipSign(4, toBoat, right) - SkierPoses.HookupHipSign(2, toBoat, right)) < 0.01f, "hookup hip for buoy 4 matches buoy 2");
        Check(Mathf.Abs(SkierPoses.HookupHipSign(5, toBoat, right) - SkierPoses.HookupHipSign(1, toBoat, right)) < 0.01f, "hookup hip for buoy 5 matches buoy 1");
        Check(Mathf.Abs(SkierPoses.HookupHipSign(6, toBoat, right) - SkierPoses.HookupHipSign(2, toBoat, right)) < 0.01f, "hookup hip for buoy 6 matches buoy 2");

        // nextBuoy is 0-based. pendingHalf != 0 means the ball just taken is still the active turn (hookup).
        Check(SkierPoses.ActiveTurnBuoy(0, 0f) == 1, "approaching buoy 1");
        Check(SkierPoses.ReachTowardBoat(SkierPoses.ActiveTurnBuoy(0, 0f)) == 1, "buoy 1 approach reaches toward the boat");
        Check(SkierPoses.ActiveTurnBuoy(1, 1f) == 1, "hookup after buoy 1 (pending half, next is 2)");
        Check(SkierPoses.ReachTowardBoat(SkierPoses.ActiveTurnBuoy(1, 1f)) == 1, "buoy 1 hookup still reaches toward the boat");
        Check(SkierPoses.ActiveTurnBuoy(1, 0f) == 2, "after the centreline, buoy 2");
        Check(SkierPoses.ReachTowardBoat(SkierPoses.ActiveTurnBuoy(1, 0f)) == -1, "buoy 2 reaches away from the boat");
        Check(SkierPoses.ActiveTurnBuoy(2, -1f) == 2, "hookup after buoy 2");
        Check(SkierPoses.ReachTowardBoat(SkierPoses.ActiveTurnBuoy(2, -1f)) == -1, "buoy 2 hookup still reaches away");
        Check(SkierPoses.ActiveTurnBuoy(2, 0f) == 3 && SkierPoses.ReachTowardBoat(3) == 1, "buoy 3 toward the boat");
        Check(SkierPoses.ActiveTurnBuoy(3, 1f) == 3 && SkierPoses.ReachTowardBoat(SkierPoses.ActiveTurnBuoy(3, 1f)) == 1, "buoy 3 hookup toward the boat");
        Check(SkierPoses.ActiveTurnBuoy(3, 0f) == 4 && SkierPoses.ReachTowardBoat(4) == -1, "buoy 4 away from the boat");
        Check(SkierPoses.ActiveTurnBuoy(4, -1f) == 4 && SkierPoses.ReachTowardBoat(SkierPoses.ActiveTurnBuoy(4, -1f)) == -1, "buoy 4 hookup away");
        Check(SkierPoses.ActiveTurnBuoy(4, 0f) == 5 && SkierPoses.ReachTowardBoat(5) == 1, "buoy 5 toward the boat");
        Check(SkierPoses.ActiveTurnBuoy(5, 1f) == 5 && SkierPoses.ReachTowardBoat(SkierPoses.ActiveTurnBuoy(5, 1f)) == 1, "buoy 5 hookup toward the boat");
        Check(SkierPoses.ActiveTurnBuoy(5, 0f) == 6 && SkierPoses.ReachTowardBoat(6) == -1, "buoy 6 away from the boat");
        Check(SkierPoses.ActiveTurnBuoy(6, -1f) == 6 && SkierPoses.ReachTowardBoat(SkierPoses.ActiveTurnBuoy(6, -1f)) == -1, "buoy 6 hookup away");

        Debug.Log($"[ReachTest] {fails} failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
