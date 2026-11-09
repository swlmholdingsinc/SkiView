using System;

namespace SkiView
{
    /// <summary>Boot decisions that do not need Addressables. A blank canvas is not a successful start.</summary>
    public static class SkiViewBootContract
    {
        public const float StallSeconds = 8f;

        public static bool Stalled(string phase, float secondsInPhase)
        {
            if (secondsInPhase < StallSeconds) return false;
            if (string.IsNullOrEmpty(phase)) return true;
            return phase.IndexOf("lake-ready", StringComparison.Ordinal) < 0;
        }

        public static string StallFault(string phase)
        {
            return "stalled at " + (string.IsNullOrEmpty(phase) ? "(none)" : phase)
                + ". No later boot step ran for " + StallSeconds.ToString("0")
                + "s. No red browser message is required for this to be a stop.";
        }
    }
}
