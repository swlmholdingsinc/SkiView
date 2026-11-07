namespace SkiView.Logic
{
    /// <summary>What RESTART PASS / the skip pill should do. NotJump = run the existing (slalom) code unchanged.</summary>
    public enum JumpRetryAction { NotJump, ReplayPass, NewRound, Ignore }

    /// <summary>
    /// Pure decision rules for jump-mode retry / skip (no UnityEngine, so EditMode tests can call them directly).
    /// Before this, RESTART PASS and the skip pill always ran SkipToSetup(), which flips the boat to -passDir on the
    /// slalom run-in; the jump trigger needs dir == jumpDir, so the round sat on "SET UP · JUMP" until an island loop.
    /// </summary>
    public static class JumpRetryRules
    {
        static bool RoundDone(bool resultsOpen, int passCount, int passesPerRound) => resultsOpen || passCount >= passesPerRound;

        /// <summary>RESTART PASS: replay the current jump pass, or start a new round once the round is over.</summary>
        public static JumpRetryAction OnRetry(bool jumpMode, bool hasRamp, bool resultsOpen, int passCount, int passesPerRound)
        {
            if (!jumpMode || !hasRamp) return JumpRetryAction.NotJump;
            return RoundDone(resultsOpen, passCount, passesPerRound) ? JumpRetryAction.NewRound : JumpRetryAction.ReplayPass;
        }

        /// <summary>Skip pill: go straight to the next jump approach; never a slalom set-up. A finished round is left to the results panel.</summary>
        public static JumpRetryAction OnSkip(bool jumpMode, bool hasRamp, bool resultsOpen, int passCount, int passesPerRound)
        {
            if (!jumpMode || !hasRamp) return JumpRetryAction.NotJump;
            return RoundDone(resultsOpen, passCount, passesPerRound) ? JumpRetryAction.Ignore : JumpRetryAction.ReplayPass;
        }
    }
}
