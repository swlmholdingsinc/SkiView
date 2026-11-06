using System;
using System.Collections.Generic;
using System.Globalization;

namespace SkiView
{
    /// <summary>
    /// Pure rules for the opt-in collar QA view. No scene changes live here.
    /// Gameplay stays on the normal materials unless a vest QA argument is present.
    /// </summary>
    public static class SkiViewVestQaLogic
    {
        public const float PoseDegrees = 35f;

        public struct Entry
        {
            public int id;
            public string role;
            public string path;
            public string mesh;
            public int submeshes;
            public string materials;
            public int triangles;
            public bool enabled;
            public bool readable;
        }

        public static bool IsOn(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            value = value.Trim().ToLowerInvariant();
            return value == "1" || value == "true" || value == "on" || value == "yes" || value == "all";
        }

        /// <summary>True only when the page or command line asked for the collar view.</summary>
        public static bool Requested(string vestqa, string vestshade, string pose, string vestid, string vestsub)
        {
            if (IsOn(vestqa)) return true;
            if (!string.IsNullOrEmpty(vestshade)) return true;
            if (!float.IsNaN(PoseRollDegrees(pose))) return true;
            if (!string.IsNullOrEmpty(vestid)) return true;
            if (!string.IsNullOrEmpty(vestsub)) return true;
            return false;
        }

        /// <summary>NaN when pose is absent. left is +35 degrees about the skier forward axis; right is -35.</summary>
        public static float PoseRollDegrees(string pose)
        {
            if (string.IsNullOrEmpty(pose)) return float.NaN;
            pose = pose.Trim().ToLowerInvariant();
            if (pose == "left" || pose == "l") return PoseDegrees;
            if (pose == "right" || pose == "r") return -PoseDegrees;
            if (pose == "center" || pose == "mid" || pose == "0") return 0f;
            return float.NaN;
        }

        /// <summary>lit is the gameplay material. Anything else is a debug replacement.</summary>
        public static string Shade(string vestshade)
        {
            if (string.IsNullOrEmpty(vestshade)) return "lit";
            vestshade = vestshade.Trim().ToLowerInvariant();
            if (vestshade == "normal" || vestshade == "normals") return "normals";
            if (vestshade == "tangent" || vestshade == "tangents") return "tangents";
            if (vestshade == "unlit" || vestshade == "flat") return "unlit";
            if (vestshade == "id" || vestshade == "ids" || vestshade == "color") return "ids";
            if (vestshade == "lit" || vestshade == "pbr" || vestshade == "off") return "lit";
            return "lit";
        }

        public static int ShadeMode(string shade)
        {
            if (shade == "normals") return 1;
            if (shade == "tangents") return 2;
            return 0;
        }

        /// <summary>vest if the name, mesh, or material says so. otherwise other.</summary>
        public static string Role(string rendererName, string meshName, string materialNames)
        {
            string s = ((rendererName ?? "") + " " + (meshName ?? "") + " " + (materialNames ?? "")).ToLowerInvariant();
            if (s.Contains("vest")) return "vest";
            return "other";
        }

        public static bool ParseSub(string vestsub, out int rendererId, out int submesh)
        {
            rendererId = -1;
            submesh = -1;
            if (string.IsNullOrEmpty(vestsub)) return false;
            int dot = vestsub.IndexOf('.');
            if (dot <= 0) return false;
            if (!int.TryParse(vestsub.Substring(0, dot), NumberStyles.Integer, CultureInfo.InvariantCulture, out rendererId)) return false;
            if (!int.TryParse(vestsub.Substring(dot + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out submesh)) return false;
            return rendererId >= 0 && submesh >= 0;
        }

        public static int ParseId(string vestid)
        {
            if (string.IsNullOrEmpty(vestid)) return -1;
            return int.TryParse(vestid.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : -1;
        }

        /// <summary>Reads vestqa, vestshade, pose, vestid, vestsub from a query or from a path that already uses lake=a&amp;cam=face.</summary>
        public static Dictionary<string, string> ArgsFromUrl(string url)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(url)) return map;
            int q = url.IndexOf('?');
            string qs = q >= 0 ? url.Substring(q + 1) : url;
            int slash = qs.LastIndexOf('/');
            if (q < 0 && slash >= 0) qs = qs.Substring(slash + 1);
            int hash = qs.IndexOf('#');
            if (hash >= 0) qs = qs.Substring(0, hash);
            foreach (var kv in qs.Split('&'))
            {
                if (kv.Length == 0 || kv.IndexOf('=') < 0) continue;
                int e = kv.IndexOf('=');
                string k = Uri.UnescapeDataString(kv.Substring(0, e).Replace('+', ' ')).ToLowerInvariant();
                string v = Uri.UnescapeDataString(kv.Substring(e + 1).Replace('+', ' '));
                if (k == "vestqa" || k == "vestshade" || k == "pose" || k == "vestid" || k == "vestsub" || k == "vestscope" || k == "cam" || k == "skier")
                    map[k] = v;
            }
            return map;
        }

        public static string LegendLine(Entry e)
        {
            return "id=" + e.id.ToString(CultureInfo.InvariantCulture)
                + " role=" + e.role
                + " sub=" + e.submeshes.ToString(CultureInfo.InvariantCulture)
                + " tris=" + e.triangles.ToString(CultureInfo.InvariantCulture)
                + " on=" + (e.enabled ? "1" : "0")
                + " read=" + (e.readable ? "1" : "0")
                + " mesh=" + e.mesh
                + " mats=" + e.materials
                + " path=" + e.path;
        }

        /// <summary>Same paths always get the same ids, sorted by path.</summary>
        public static List<Entry> AssignIds(List<Entry> raw)
        {
            var list = new List<Entry>(raw);
            list.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                e.id = i;
                list[i] = e;
            }
            return list;
        }
    }
}
