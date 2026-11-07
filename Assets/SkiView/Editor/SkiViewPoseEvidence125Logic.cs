using System;

namespace SkiView
{
    /// <summary>
    /// Sampling rules for the gameplay pose export.
    /// The ±35° root rolls belong to SkiViewCharacterCapture125 and are not decided here.
    /// This file does not apply blendshape weights.
    /// </summary>
    public static class SkiViewPoseEvidence125Logic
    {
        public const string BendFwd = "SK_bendFwd_fold_Vest";
        public const string ArmholeR = "SK_armhole_ease_Vest_R";
        public const string ArmholeL = "SK_armhole_ease_Vest_L";
        public const string HipTuck = "SK_hipFlex_tuck_Shorts";

        public static bool BothAtLeast(float bendFwd, float armholeR, float threshold)
        {
            return bendFwd >= threshold && armholeR >= threshold;
        }

        public static float Pair(float bendFwd, float armholeR)
        {
            return bendFwd < armholeR ? bendFwd : armholeR;
        }

        /// <summary>Stride sample, plus any frame that raises the pair or either key.</summary>
        public static bool Keep(int index, int stride, float pair, float bestPair, float bend, float bestBend, float arm, float bestArm, float absLean, float bestAbsLean)
        {
            if (stride < 1) stride = 1;
            if (index % stride == 0) return true;
            if (pair > bestPair + 0.5f) return true;
            if (bend > bestBend + 0.5f) return true;
            if (arm > bestArm + 0.5f) return true;
            return absLean > bestAbsLean + 0.5f;
        }

        public static float HipFlexDeg(float spineToThighDeg)
        {
            return 180f - spineToThighDeg;
        }

        public static float Larger(float a, float b)
        {
            return a > b ? a : b;
        }

        public static float ClampDot(float d)
        {
            if (d > 1f) return 1f;
            if (d < -1f) return -1f;
            return d;
        }

        /// <summary>Same asin the vest driver uses for an arm direction dotted with chest forward. Not a blendshape weight.</summary>
        public static float ArmForwardDeg(float armDotChestForward)
        {
            return (float)(Math.Asin(ClampDot(armDotChestForward)) * (180.0 / Math.PI));
        }
    }
}
