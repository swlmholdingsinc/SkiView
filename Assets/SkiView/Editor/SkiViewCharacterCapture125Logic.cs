using System;
using System.Collections.Generic;
using System.Globalization;

namespace SkiView
{
    /// <summary>Rules for the D3D11 character capture. No scene changes.</summary>
    public static class SkiViewCharacterCapture125Logic
    {
        public const float HeadLocalY = 1.55578f;
        public const float NeckLocalY = 1.44886f;
        public const float BoneEpsilon = 0.001f;
        public const float PoseLeft = 35f;
        public const float PoseRight = -35f;

        public static readonly string[] RequiredPaths =
        {
            "Assets/SkiView/Scenes/SkiViewLake.unity",
            "Assets/SkiView/Skier/INTERNAL_ONLY_Felderpuss_V3_WadeCox.fbx",
            "Assets/SkiView/Resources/Gear125/SkiView_Gear_Slalom_125.fbx",
            "Assets/SkiView/Resources/Face125/SkiView_Face_Male_V1.fbx",
            "Assets/SkiView/Resources/Face125/SkiView_Face_Female_V1.fbx",
            "Assets/SkiView/Resources/Hair125/SkiView_HeadHair_V1_InGameOnly.fbx",
            "Assets/SkiView/Resources/Materials125/RuntimeLit125.mat",
            "Assets/Settings/PC_RPAsset.asset"
        };

        public static List<string> Missing(Func<string, bool> exists)
        {
            var missing = new List<string>();
            if (exists == null) return missing;
            for (int i = 0; i < RequiredPaths.Length; i++)
                if (!exists(RequiredPaths[i])) missing.Add(RequiredPaths[i]);
            return missing;
        }

        public static float PoseRoll(string pose)
        {
            if (pose == "left") return PoseLeft;
            if (pose == "right") return PoseRight;
            return 0f;
        }

        public static bool BoneOk(float measured, float canonical)
        {
            return Math.Abs(measured - canonical) <= BoneEpsilon;
        }

        /// <summary>Original slalom vest mesh. Jump vests and rejected candidates are not accepted.</summary>
        public static bool OriginalVestName(string rendererName, string meshName)
        {
            string s = ((rendererName ?? "") + " " + (meshName ?? "")).ToLowerInvariant();
            if (!s.Contains("gear_vest") && !s.Contains("vest_lod")) return false;
            if (s.Contains("jump")) return false;
            if (s.Contains("candidate")) return false;
            return true;
        }

        public static bool LumaOk(string mode, float mean, float stdev)
        {
            if (mean < 8f || mean > 245f) return false;
            if (stdev < 4f) return false;
            if (mode == "lit" && stdev < 6f) return false;
            return true;
        }

        public static bool ShotsDiffer(uint hashA, uint hashB)
        {
            return hashA != hashB;
        }
    }
}
