namespace SkiView.Logic
{
    public enum ModeRestartKind { SlalomSetup, JumpReplay, JumpNewRound, JumpIgnore, TrickPass }

    /// <summary>
    /// RESTART PASS for the modes that exist in this tree. Jump stays on JumpRetryRules.
    /// Slalom stays on SkipToSetup, including its direction flip. Trick does not.
    /// </summary>
    public static class ModeRestartRules
    {
        public static ModeRestartKind Decide(bool jumpMode, bool hasRamp, bool jumpResultsOpen, int jumpPassCount, int jumpPassesPerRound, bool trickMode)
        {
            var jump = JumpRetryRules.OnRetry(jumpMode, hasRamp, jumpResultsOpen, jumpPassCount, jumpPassesPerRound);
            if (jump == JumpRetryAction.ReplayPass) return ModeRestartKind.JumpReplay;
            if (jump == JumpRetryAction.NewRound) return ModeRestartKind.JumpNewRound;
            if (jump == JumpRetryAction.Ignore) return ModeRestartKind.JumpIgnore;
            if (trickMode) return ModeRestartKind.TrickPass;
            return ModeRestartKind.SlalomSetup;
        }

        /// <summary>The direction SkipToSetup already uses. Slalom restart keeps this.</summary>
        public static int SlalomSetupDirection(int passDir)
        {
            int nd = -passDir;
            if (nd == 0) nd = 1;
            return nd;
        }

        /// <summary>Pass 1 uses the round's start direction. Pass 2 uses the opposite. Not a flip of the boat's current direction.</summary>
        public static int TrickPassDirection(int trickPass, int startDir)
        {
            int d = startDir < 0 ? -1 : 1;
            if (startDir == 0) d = -1;
            return trickPass <= 1 ? d : -d;
        }
    }

    /// <summary>Runs without Unity. Returns the number of failed checks.</summary>
    public static class ModeRestartHarness
    {
        public static int Run(System.Action<string> fail)
        {
            int n = 0;
            void F(bool ok, string m) { if (!ok) { n++; if (fail != null) fail(m); } }

            F(ModeRestartRules.Decide(false, true, false, 0, 3, true) == ModeRestartKind.TrickPass, "TR-07 trick restart is not the slalom setup");
            F(ModeRestartRules.Decide(false, true, false, 0, 3, false) == ModeRestartKind.SlalomSetup, "slalom restart stays the setup path");
            F(ModeRestartRules.Decide(true, true, false, 1, 3, false) == ModeRestartKind.JumpReplay, "jump mid-round still replays");
            F(ModeRestartRules.Decide(true, true, false, 3, 3, false) == ModeRestartKind.JumpNewRound, "jump round done still starts a new round");
            F(ModeRestartRules.Decide(true, true, true, 1, 3, true) == ModeRestartKind.JumpNewRound, "jump wins when both flags are set");
            F(ModeRestartRules.Decide(true, false, false, 1, 3, true) == ModeRestartKind.TrickPass, "jump without a ramp falls through to trick");

            int pass1 = ModeRestartRules.TrickPassDirection(1, -1);
            int pass2 = ModeRestartRules.TrickPassDirection(2, -1);
            F(pass1 == -1, "trick pass 1 keeps Ski View start direction");
            F(pass2 == 1, "trick pass 2 is the opposite of the start direction");
            F(pass1 != ModeRestartRules.SlalomSetupDirection(pass1), "restarting pass 1 does not use the slalom flip");
            F(pass2 != ModeRestartRules.SlalomSetupDirection(pass2), "restarting pass 2 does not use the slalom flip");
            F(ModeRestartRules.SlalomSetupDirection(-1) == 1 && ModeRestartRules.SlalomSetupDirection(1) == -1, "slalom flip itself is unchanged");

            // Recorded, not changed: P19 PbFmt. 0 balls at rope index 8 is score 48 and displays as 6 @ 41 OFF.
            float score = 8 * 6f + 0f;
            float shownBalls = (score % 6f < 0.01f && score > 0f) ? 6f : score % 6f;
            int shownRope = (int)System.Math.Floor((score - 0.01f) / 6f);
            if (shownRope < 0) shownRope = 0;
            if (shownRope > 8) shownRope = 8;
            F(score == 48f && shownBalls == 6f && shownRope == 7, "recorded display: 0 at 43 encodes as 6 at rope index 7");
            return n;
        }
    }
}
