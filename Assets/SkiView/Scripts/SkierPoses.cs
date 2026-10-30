using System;
using UnityEngine;

namespace SkiView
{
    /// <summary>
    /// One tuner for skier poses. Footage estimates (±5–10°), not locked constants in the IK:
    /// slalom Will Asher (Hb3kTRenFAA, 154–192 s); jumps Freddy Krueger, Ryan Dodd, Jacinta Carroll.
    /// Knee angles are included degrees (180 = straight). Lean is degrees from vertical.
    /// #125 rule: at EVERY buoy the reach goes toward the boat and down the course. Odd and even buoys alternate only the
    /// course side and the handle hand (outside hand releases). Hookup uses the buoy just taken.
    /// </summary>
    [Serializable]
    public class SkierPoses
    {
        [Header("Slalom lean from vertical")]
        public float turnFinishLean = 22f;   // 20–25, back toward the boat
        public float pullLean = 50f;         // 45–55, one straight board
        public float wakeLean = 32f;         // 30–35
        public float edgeLean = 8f;          // 0–15
        public float glideLean = 8f;         // 5–10
        public float reachLean = 15f;        // 10–20 inboard
        public float peakSkiRoll = 58f;      // pull / max edge, about 50–60
        public float bodyExtraDeg = 4f;      // extra hinge past the ski roll; keep this small so the board stays straight

        [Header("Slalom knees (180 = straight)")]
        public float turnFinishKnee = 115f;  // 110–120
        public float pullKnee = 148f;        // 140–155
        public float wakeKnee = 130f;
        public float edgeKnee = 140f;
        public float glideKnee = 162f;
        public float reachKnee = 122f;       // 115–130

        [Header("Slalom arms (1 = locked straight) and handle")]
        public float turnFinishArm = 0.78f;  // elbows bent
        public float pullArm = 0.84f;        // elbows tucked, hips into the handle
        public float wakeArm = 0.95f;
        public float edgeArm = 0.86f;        // handle at the waist
        public float glideArm = 0.94f;
        public float reachArm = 0.96f;       // hand on the handle, extended
        public float handleFwd = 0.20f;
        public float handleDrop = 0.16f;     // low, at the hip / below the navel
        public float edgeHandleDrop = 0.12f;
        public float freeArmFwd = 0.50f;     // free arm out and forward, not behind or overhead
        public float freeArmOut = 0.70f;
        public float shoulderYawRate = 80f;  // shoulders finish the turn last
        public float headTiltWithBody = 0.06f;
        public float headPitchDownDeg = 2f;
        public float gazeToBoat = 0.12f;

        [Header("Jump approach and takeoff")]
        public float approachLean = 30f;
        public float approachKnee = 140f;
        public float approachArm = 0.95f;
        public float approachHandleIn = 0.12f;
        public float approachTip = 4f;
        public float takeoffKneeIn = 152f;   // 150–165, no squat
        public float takeoffKneeOut = 164f;
        public float takeoffHip = 110f;      // study hip; chest pitch below is what the rig applies so the chest stays up
        public float takeoffChestPitch = 10f;
        public float takeoffArm = 0.94f;
        public float takeoffHandleIn = 0.15f;

        [Header("Jump flight (body lean from vertical, ski tip from horizontal)")]
        public float earlyFlightLean = 18f;  // 15–20
        public float peakFlightLean = 32f;   // 30–35
        public float preLandLean = 20f;      // 15–25
        public float earlyFlightTip = 40f;   // to horizontal, skis parallel
        public float peakFlightTip = 22f;    // 20–25, tips up on the body line
        public float preLandTip = 5f;        // V closed, skis near level
        public float flightKnee = 170f;      // legs straight
        public float preLandKnee = 156f;
        public float peakSkiV = 35f;         // 30–40 apart, total
        public float flightArm = 0.94f;      // long to the handle, not overhead, not locked
        public float flightHandleIn = 0.2f;

        [Header("Jump landing")]
        public float landKnee = 115f;        // 110–120, hips stay above the knees
        public float landTorso = 8f;         // upright to slightly forward
        public float landTip = 3f;           // bottoms flat, tips not dropping
        public float landArm = 0.9f;
        public float landHandleIn = 0.25f;
        public float landRecoverSec = 1f;    // tall ride-away
        public float landMaxRoll = 12f;
        public float jumpHandleFwd = 0.26f;
        public float jumpHandleDrop = 0.04f;
        public float waterTip = 4f;

        /// <summary>#125 (Ray's per-buoy table): the boat is always inside the turn, so the reach goes inward toward the boat path
        /// at EVERY buoy. Odd/even only mirror the course side (BuoyX), which the toBoat vector already carries.</summary>
        public static int ReachTowardBoat(int buoy1to6) => 1;

        /// <summary>Buoy being turned. After the ball is taken, pendingHalf stays set until the centreline, so the hookup still belongs to that buoy.</summary>
        public static int ActiveTurnBuoy(int nextBuoy, float pendingHalf)
        {
            if (Mathf.Abs(pendingHalf) > 0.01f && nextBuoy >= 1 && nextBuoy <= 6) return nextBuoy;
            if (nextBuoy >= 0 && nextBuoy < 6) return nextBuoy + 1;
            return Mathf.Clamp(nextBuoy < 1 ? 1 : nextBuoy, 1, 6);
        }

        /// <summary>Horizontal unit direction of the reach. Dot with toBoat is positive at every buoy (#125).</summary>
        public static Vector3 FreeArmDir(Vector3 toBoatHorizontal, int buoy1to6)
        {
            Vector3 t = toBoatHorizontal; t.y = 0f;
            if (t.sqrMagnitude < 1e-6f) t = Vector3.forward;
            else t.Normalize();
            return t * ReachTowardBoat(buoy1to6);
        }

        /// <summary>+1 = hookup elbow tucks to the skier's right hip. Odd and even buoys are mirrors for the same boat direction.</summary>
        public static float HookupHipSign(int buoy1to6, Vector3 toBoatHorizontal, Vector3 skierRight)
        {
            Vector3 t = toBoatHorizontal; t.y = 0f;
            float boatOnRight = t.sqrMagnitude < 1e-6f ? 1f : Mathf.Sign(Vector3.Dot(t.normalized, skierRight));
            if (Mathf.Abs(boatOnRight) < 0.01f) boatOnRight = 1f;
            return boatOnRight * ReachTowardBoat(buoy1to6);
        }

        public static float CrouchForKnee(float includedDeg) => Mathf.Sin(Mathf.Clamp(includedDeg, 70f, 175f) * 0.5f * Mathf.Deg2Rad);

        public struct Phase
        {
            public float lean, knee, arm, handleDrop, free, straight, shoulderToBoat;
        }

        /// <summary>Blend of the six slalom shapes. lat01 0 = centreline, 1 = at the buoy. inbound 1 = cutting back toward the boat.</summary>
        public Phase EvaluateSlalom(float lat01, float inbound, float carve, float edge01, int reachTowardBoat)
        {
            lat01 = Mathf.Clamp01(lat01); inbound = Mathf.Clamp01(inbound); carve = Mathf.Clamp01(carve); edge01 = Mathf.Clamp01(edge01);
            float reach = Mathf.Clamp01(Mathf.InverseLerp(0.62f, 0.92f, lat01)) * (1f - inbound);
            float hook = Mathf.Clamp01(Mathf.InverseLerp(0.70f, 0.96f, lat01)) * inbound;
            float pull = Mathf.Clamp01(Mathf.InverseLerp(0.22f, 0.5f, lat01)) * Mathf.Clamp01(Mathf.InverseLerp(0.92f, 0.62f, lat01)) * inbound;
            float wake = Mathf.Clamp01(Mathf.InverseLerp(0.42f, 0.08f, lat01)) * carve * (1f - hook);
            float edge = (1f - edge01) * Mathf.Clamp01(Mathf.InverseLerp(0.06f, 0.28f, lat01)) * Mathf.Clamp01(Mathf.InverseLerp(0.55f, 0.28f, lat01)) * carve;
            float glide = Mathf.Clamp01((1f - inbound) * (1f - reach) * (1f - wake) * (1f - edge));
            float sum = reach + hook + pull + wake + edge + glide;
            if (sum < 1e-3f) { glide = 1f; sum = 1f; }
            float N(float x) => x / sum;
            float buoyOpen = reachTowardBoat < 0 ? -1f : 1f;
            var p = new Phase
            {
                lean = N(hook) * turnFinishLean + N(pull) * pullLean + N(wake) * wakeLean + N(edge) * edgeLean + N(glide) * glideLean + N(reach) * reachLean,
                knee = N(hook) * turnFinishKnee + N(pull) * pullKnee + N(wake) * wakeKnee + N(edge) * edgeKnee + N(glide) * glideKnee + N(reach) * reachKnee,
                arm = N(hook) * turnFinishArm + N(pull) * pullArm + N(wake) * wakeArm + N(edge) * edgeArm + N(glide) * glideArm + N(reach) * reachArm,
                handleDrop = Mathf.Lerp(handleDrop, edgeHandleDrop, N(edge)),
                free = N(reach),
                straight = N(pull),
                shoulderToBoat = N(pull) * 1f + N(hook) * buoyOpen + N(wake) * 0.85f + N(glide) * 0.35f + N(edge) * 0.15f + N(reach) * buoyOpen
            };
            p.lean = Mathf.Min(p.lean, peakSkiRoll);
            return p;
        }

        public void FlightPose(float post, float prep, out float bodyFwd, out float tip, out float knee, out float vHalf)
        {
            post = Mathf.Clamp01(post); prep = Mathf.Clamp01(prep);
            bodyFwd = Mathf.Lerp(Mathf.Lerp(earlyFlightLean, peakFlightLean, post), preLandLean, prep);
            tip = Mathf.Lerp(Mathf.Lerp(earlyFlightTip, peakFlightTip, post), preLandTip, prep);
            knee = Mathf.Lerp(flightKnee, preLandKnee, prep);
            vHalf = Mathf.Lerp(0f, peakSkiV * 0.5f, post) * (1f - prep);
        }
    }
}
