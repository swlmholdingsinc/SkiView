namespace SkiView.Logic
{
    public enum PauseRestart { RetryPass, TrickPass }

    /// <summary>
    /// Pause → RESTART PASS. Jump stays on <c>RetryPass</c> so fix/jump-retry can own that path.
    /// Trick does not use the slalom set-up. Slalom, including its direction flip, stays on <c>RetryPass</c>.
    /// </summary>
    public static class PassRestart
    {
        public static PauseRestart Choose(bool jumpMode, bool trickMode)
        {
            if (!jumpMode && trickMode) return PauseRestart.TrickPass;
            return PauseRestart.RetryPass;
        }

        /// <summary>The direction <c>SkipToSetup</c> already uses. Not applied to a trick restart.</summary>
        public static int SlalomSetupDirection(int passDir)
        {
            int nd = -passDir;
            if (nd == 0) nd = 1;
            return nd;
        }

        /// <summary>Pass 1 keeps the round's start direction. Pass 2 is the opposite of that start, not of the boat's current direction.</summary>
        public static int TrickPassDirection(int trickPass, int startDir)
        {
            int d = startDir < 0 ? -1 : 1;
            return trickPass <= 1 ? d : -d;
        }

        /// <summary>
        /// Where to place the boat. Pass 1 uses the round start. Pass 2 sits 80 m short of its entry buoy:
        /// progress increases as the boat travels, the arm window opens a few metres before the buoy,
        /// and the deep-water start itself covers about 30 m.
        /// </summary>
        public static float TrickPassProgress(int trickPass, float startProgress, float entryAlong)
        {
            if (trickPass <= 1) return startProgress;
            return entryAlong - 80f;
        }
    }
}
