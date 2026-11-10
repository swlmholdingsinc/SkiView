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

        public static bool IsStall(string fault)
        {
            return !string.IsNullOrEmpty(fault) && fault.StartsWith("stalled at ", StringComparison.Ordinal);
        }

        /// <summary>A later boot step drops a stall. An explicit failure string stays.</summary>
        public static string NextFault(bool advanced, string existing, string incoming)
        {
            if (incoming != null) return incoming;
            if (advanced && IsStall(existing)) return null;
            return existing;
        }

        public static bool HideBootOverlay(string phase, string fault)
        {
            if (string.IsNullOrEmpty(phase) || phase.IndexOf("lake-ready", StringComparison.Ordinal) < 0) return false;
            return string.IsNullOrEmpty(fault) || IsStall(fault);
        }
    }
}
