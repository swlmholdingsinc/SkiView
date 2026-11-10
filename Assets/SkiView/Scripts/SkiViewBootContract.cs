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

        /// <summary>Chrome exposes the Cache API only in a secure context. http://127.0.0.1 is one. A LAN IP on plain HTTP is not.</summary>
        public static bool BrowserCacheSupported(bool secureContext, bool cacheApiPresent)
        {
            return secureContext && cacheApiPresent;
        }

        /// <summary>must-revalidate only where the Cache API exists. settings.json and catalogs stay no-store.</summary>
        public static string CacheControl(bool secureContext, bool cacheApiPresent, string url)
        {
            if (!BrowserCacheSupported(secureContext, cacheApiPresent)) return "no-store";
            if (string.IsNullOrEmpty(url)) return "no-store";
            if (url.IndexOf(".data", StringComparison.Ordinal) >= 0 || url.IndexOf(".bundle", StringComparison.Ordinal) >= 0)
                return "must-revalidate";
            return "no-store";
        }

        public static string SettingsUrl(string streamingAssetsPath)
        {
            if (string.IsNullOrEmpty(streamingAssetsPath)) return "StreamingAssets/aa/settings.json";
            string root = streamingAssetsPath.TrimEnd('/');
            if (root.EndsWith("/aa", StringComparison.Ordinal)) return root + "/settings.json";
            return root + "/aa/settings.json";
        }

        public static string DescribeInitFailure(string addressablesMessage, bool cacheSupported, string settingsUrl)
        {
            string msg = addressablesMessage ?? "";
            bool runtimeData = msg.IndexOf("Unable to load runtime data", StringComparison.Ordinal) >= 0
                || msg.IndexOf("ResourceManagerRuntimeData", StringComparison.Ordinal) >= 0
                || msg.IndexOf("Invalid path in TextDataProvider", StringComparison.Ordinal) >= 0
                || msg.IndexOf("unable to load from url", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!runtimeData) return msg;
            string where = string.IsNullOrEmpty(settingsUrl) ? "(no settings url)" : settingsUrl;
            if (!cacheSupported)
                return "runtime data was not loaded from " + where
                    + ". This origin is plain HTTP, so the browser does not provide the Cache API. UnityCache stopped the request before settings.json was fetched. Direct fetch is required. Addressables said: "
                    + msg;
            return "runtime data was not loaded from " + where + ". Addressables said: " + msg;
        }
    }
}
