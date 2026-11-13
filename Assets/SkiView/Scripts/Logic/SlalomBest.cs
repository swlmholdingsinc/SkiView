namespace SkiView.Logic
{
    /// <summary>
    /// Slalom personal-best text. A packed score is <c>ropeIndex * 6 + buoys</c>.
    /// Six buoys at a rope and zero buoys at the next rope are the same number, so a packed 48
    /// is both 6 @ 41 OFF and 0 @ 43 OFF. Callers that still have the pass use <see cref="FormatPass"/>.
    /// Labels match <c>SkierSim.ROPE_LABEL</c>.
    /// </summary>
    public static class SlalomBest
    {
        public static readonly string[] RopeLabel =
        {
            "15 OFF", "22 OFF", "28 OFF", "32 OFF", "35 OFF", "38 OFF", "39.5 OFF", "41 OFF", "43 OFF"
        };

        public static float Pack(int rope, float buoys)
        {
            if (rope < 0) rope = 0;
            if (rope > 8) rope = 8;
            if (float.IsNaN(buoys) || buoys < 0f) buoys = 0f;
            if (buoys > 6f) buoys = 6f;
            return rope * 6f + buoys;
        }

        public static bool IsBetter(float previous, int rope, float buoys)
        {
            return Pack(rope, buoys) > previous + 0.01f;
        }

        /// <summary>The pass that was actually skied. Zero buoys at 43 OFF stays "0 @ 43 OFF".</summary>
        public static string FormatPass(int rope, float buoys)
        {
            if (rope < 0) rope = 0;
            if (rope > 8) rope = 8;
            if (float.IsNaN(buoys) || buoys < 0f) buoys = 0f;
            if (buoys > 6f) buoys = 6f;
            return Balls(buoys) + " @ " + RopeLabel[rope];
        }

        /// <summary>
        /// Older saves stored only the packed float. An exact multiple of 6 is shown as a full pass
        /// at the previous rope, which is what a clean pass wrote. Packed 48 therefore stays "6 @ 41 OFF".
        /// </summary>
        public static string FormatPacked(float score)
        {
            if (score < 0f) return "—";
            float balls = (score % 6f < 0.01f && score > 0f) ? 6f : score % 6f;
            int rope = (int)System.Math.Floor((score - 0.01f) / 6f);
            if (rope < 0) rope = 0;
            if (rope > 8) rope = 8;
            return Balls(balls) + " @ " + RopeLabel[rope];
        }

        public static string Display(float packed, bool hasPass, int rope, float buoys)
        {
            if (hasPass) return FormatPass(rope, buoys);
            return FormatPacked(packed);
        }

        public static string Balls(float b)
        {
            int w = (int)System.Math.Floor(b + 1e-3f);
            return (b - w >= 0.49f) ? w + ".5" : w.ToString();
        }
    }
}
