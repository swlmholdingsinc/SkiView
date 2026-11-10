using System;

namespace SkiView
{
    /// <summary>Which lakes the menus may offer, and when Select is the action. WebGL ships the lake bundles in the build.</summary>
    public static class SkiViewLakeChoice
    {
        public static readonly string[] BuiltLakeIds =
        {
            "skiview", "cedarbend", "saltwhite", "villaserena", "tallpine",
            "heronmarsh", "harrowpond", "gulfwind", "saguaro", "marrowind"
        };

        public static bool Selectable(bool webgl, string id, bool downloading, bool failed, long sizeBytes, bool offline)
        {
            if (downloading || failed) return false;
            if (id == "skiview" || webgl) return true;
            if (sizeBytes <= 0) return true;
            return !offline && sizeBytes < 0;
        }
    }
}
