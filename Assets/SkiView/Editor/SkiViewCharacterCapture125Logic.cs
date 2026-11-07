using System;
using System.Collections.Generic;
using System.Globalization;

namespace SkiView
{
    /// <summary>Rules for the D3D11 character capture. No scene changes.</summary>
    public static class SkiViewCharacterCapture125Logic
    {
        public const float BoneEpsilon = 0.001f;
        /// <summary>Parent-local translation Y of mixamorig:Head in INTERNAL_ONLY_Felderpuss_V3_WadeCox.fbx. This is what Transform.localPosition.y is.</summary>
        public const float HeadParentLocalY = 0.11152041f;
        /// <summary>Parent-local translation Y of mixamorig:Neck in the same file.</summary>
        public const float NeckParentLocalY = 0.15049955f;
        /// <summary>Blender armature matrix_world translation Z, identical on body, gear, and both faces. Skeleton-space height plus this offset is the canonical world height.</summary>
        public const float ArmatureObjectOffsetZ = -0.048361f;
        /// <summary>Canonical head world height. Not parent-local Y and not skeleton-space Y.</summary>
        public const float HeadCanonicalY = 1.555778f;
        /// <summary>Canonical neck world height.</summary>
        public const float NeckCanonicalY = 1.448865f;
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

        /// <summary>One imported bind joint. Rotation is the Unity quaternion from the FBX importer skeleton.</summary>
        public struct BindJoint
        {
            public readonly float px, py, pz, qx, qy, qz, qw;
            public BindJoint(float px, float py, float pz, float qx, float qy, float qz, float qw)
            {
                this.px = px; this.py = py; this.pz = pz;
                this.qx = qx; this.qy = qy; this.qz = qz; this.qw = qw;
            }
        }

        /// <summary>
        /// Model root to head, in importer order. Armature carries the file's -90° X.
        /// Composing this is the root-space bind position. It is not parent-local Y.
        /// </summary>
        public static readonly BindJoint[] BindChain = new[]
        {
            new BindJoint(0f, 0f, 0f, -0.7071068f, 0f, 0f, 0.7071067f),
            new BindJoint(0.00020160222f, 0.006811854f, 1.0058476f, 0.7071068f, 0f, 0f, 0.7071068f),
            new BindJoint(0.00093684334f, 0.09524512f, -0.018706575f, -0.050025433f, 0.00028784084f, -0.0057465974f, 0.9987314f),
            new BindJoint(-5.3862836e-10f, 0.11359373f, -1.1621523e-8f, 0.028563015f, -0.00027098024f, 0.0037820362f, 0.9995848f),
            new BindJoint(6.8219527e-12f, 0.13385658f, 3.1542413e-10f, 0.08163163f, 0.00043349888f, 0.011176379f, 0.9965998f),
            new BindJoint(-6.4335963e-9f, 0.15049955f, 1.4243142e-8f, 0.082010515f, 0.0028557447f, 0.013901659f, 0.9965304f),
            new BindJoint(-6.538248e-9f, 0.11152041f, -6.456685e-8f, -1.2665986e-7f, -1.3969838e-9f, -2.2773744e-8f, 1f)
        };

        public const int NeckJoint = 5;
        public const int HeadJoint = 6;

        public static void Compose(int count, out float x, out float y, out float z)
        {
            if (count < 1) count = 1;
            if (count > BindChain.Length) count = BindChain.Length;
            float px = 0f, py = 0f, pz = 0f;
            float qx = 0f, qy = 0f, qz = 0f, qw = 1f;
            for (int i = 0; i < count; i++)
            {
                BindJoint j = BindChain[i];
                float tx, ty, tz;
                Rotate(qx, qy, qz, qw, j.px, j.py, j.pz, out tx, out ty, out tz);
                px += tx; py += ty; pz += tz;
                float nx, ny, nz, nw;
                Multiply(qx, qy, qz, qw, j.qx, j.qy, j.qz, j.qw, out nx, out ny, out nz, out nw);
                qx = nx; qy = ny; qz = nz; qw = nw;
            }
            x = px; y = py; z = pz;
        }

        public static float BindRootY(int jointInclusive)
        {
            float x, y, z;
            Compose(jointInclusive + 1, out x, out y, out z);
            return y;
        }

        /// <summary>Skeleton-space root Y plus the armature object offset. This is the canonical world height.</summary>
        public static float CanonicalY(float skeletonRootY)
        {
            return skeletonRootY + ArmatureObjectOffsetZ;
        }

        /// <summary>A root-space height is not the parent-local translation. The logged baseline mixed these.</summary>
        public static bool RootDistinctFromParentLocal(float rootY, float parentLocalY)
        {
            return Math.Abs(rootY - parentLocalY) > 0.05f;
        }

        static void Multiply(float ax, float ay, float az, float aw, float bx, float by, float bz, float bw, out float x, out float y, out float z, out float w)
        {
            x = aw * bx + ax * bw + ay * bz - az * by;
            y = aw * by - ax * bz + ay * bw + az * bx;
            z = aw * bz + ax * by - ay * bx + az * bw;
            w = aw * bw - ax * bx - ay * by - az * bz;
        }

        static void Rotate(float qx, float qy, float qz, float qw, float vx, float vy, float vz, out float x, out float y, out float z)
        {
            float tx = 2f * (qy * vz - qz * vy);
            float ty = 2f * (qz * vx - qx * vz);
            float tz = 2f * (qx * vy - qy * vx);
            x = vx + qw * tx + (qy * tz - qz * ty);
            y = vy + qw * ty + (qz * tx - qx * tz);
            z = vz + qw * tz + (qx * ty - qy * tx);
        }

        /// <summary>The 39 representative core frames. Same indices as the pose-selection core tier. Not the extended pre/post or first/last frames.</summary>
        public static readonly int[] CoreFrames =
        {
            109, 110, 578, 613, 616, 621, 777, 782, 784, 902, 903, 916, 923, 932, 942,
            1101, 1105, 1108, 1226, 1227, 1240, 1256, 1265, 1266, 1424, 1432, 1470,
            1565, 1566, 1902, 1903, 1904, 1915, 1916, 1931, 1932, 1966, 1967, 2322
        };

        public static bool IsCoreFrame(int index)
        {
            int lo = 0, hi = CoreFrames.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int v = CoreFrames[mid];
                if (v == index) return true;
                if (v < index) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        public static int LastCoreFrame()
        {
            return CoreFrames[CoreFrames.Length - 1];
        }

        /// <summary>A weight returned by GetBlendShapeWeight is written even when it is zero. Absence is not zero.</summary>
        public static bool IncludeReadWeight(bool wasRead)
        {
            return wasRead;
        }

        public const bool BakeUseScale = false;
        public const string BakeSpace = "SkinnedMeshRenderer local. BakeMesh useScale=false, so lossyScale is not in the vertices. localToWorldMatrix.MultiplyPoint3x4 applies that scale once. Do not multiply by lossyScale again.";

        public static string MeshRole(string rendererName, string meshName)
        {
            string s = ((rendererName ?? "") + " " + (meshName ?? "")).ToLowerInvariant();
            if (s.Contains("r6")) return "r6";
            if (s.Contains("body")) return "body";
            if (OriginalVestName(rendererName, meshName)) return "originalVest";
            if (s.Contains("vest")) return "otherVest";
            return "other";
        }

        public static bool BakeRole(string role, string rendererName, string meshName)
        {
            if (role == "r6" || role == "originalVest") return true;
            if (role != "body") return false;
            string s = ((rendererName ?? "") + " " + (meshName ?? "")).ToLowerInvariant();
            return s.IndexOf("lod1") < 0 && s.IndexOf("lod2") < 0;
        }

        public static List<int> EnabledDuplicateIds(int[] ids, string[] meshes, bool[] enabled)
        {
            var list = new List<int>();
            if (ids == null || meshes == null || enabled == null) return list;
            int n = ids.Length;
            if (meshes.Length < n) n = meshes.Length;
            if (enabled.Length < n) n = enabled.Length;
            var count = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                if (!enabled[i] || string.IsNullOrEmpty(meshes[i])) continue;
                int c;
                count.TryGetValue(meshes[i], out c);
                count[meshes[i]] = c + 1;
            }
            for (int i = 0; i < n; i++)
            {
                int c;
                if (enabled[i] && !string.IsNullOrEmpty(meshes[i]) && count.TryGetValue(meshes[i], out c) && c > 1)
                    list.Add(ids[i]);
            }
            return list;
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
