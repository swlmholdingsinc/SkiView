using System.Collections.Generic;
using UnityEngine;

namespace SkiView
{
    /// <summary>
    /// Aligns Body_LOD skin with the live Face125 material. The head clip stays.
    /// Does not change meshes, UVs, bind poses, the rig, or vest materials.
    /// FaceMat in EnsureFace125 sets metallic 0 and smoothness 0.25. SetA leaves metallic at 1
    /// when its mask map is assigned, and that keyword keeps the neck on the metal path.
    /// </summary>
    public static class SkiViewNeckSkinMatch
    {
        const string MetallicKeyword = "_METALLICSPECGLOSSMAP";

        struct Slot
        {
            public Material mat;
            public Shader shader;
            public bool metallicKeyword;
            public float metallic, smoothness, wetness, sss, pore, bump;
            public bool hasMetallic, hasSmoothness, hasWetness, hasSss, hasPore, hasBump;
        }

        static readonly Dictionary<int, Slot> baseline = new Dictionary<int, Slot>();

        public static bool IsBodySkin(string rendererName, string materialName)
        {
            if (string.IsNullOrEmpty(rendererName) || rendererName.IndexOf("Body_LOD", System.StringComparison.Ordinal) < 0) return false;
            if (string.IsNullOrEmpty(materialName)) return false;
            string n = materialName.ToLowerInvariant();
            if (n.Contains("vest") || n.Contains("short") || n.Contains("glove") || n.Contains("fabric") || n.Contains("hair") || n.Contains("eye")) return false;
            return true;
        }

        public static void Remember(Transform root)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !IsBodySkin(r.name, m.name) || baseline.ContainsKey(m.GetInstanceID())) continue;
                    baseline[m.GetInstanceID()] = Read(m);
                }
            }
        }

        public static int Apply(Transform root)
        {
            if (root == null) return 0;
            Remember(root);
            var face = ActiveFace(root);
            int changed = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool edit = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !IsBodySkin(r.name, m.name)) continue;
                    Match(m, face);
                    edit = true;
                    changed++;
                }
                if (edit) r.sharedMaterials = mats;
            }
            return changed;
        }

        public static int Restore()
        {
            int n = 0;
            foreach (var pair in baseline)
            {
                Write(pair.Value);
                n++;
            }
            return n;
        }

        static Slot Read(Material m)
        {
            var s = new Slot { mat = m, shader = m.shader, metallicKeyword = m.IsKeywordEnabled(MetallicKeyword) };
            s.hasMetallic = m.HasProperty("_Metallic");
            s.hasSmoothness = m.HasProperty("_Smoothness");
            s.hasWetness = m.HasProperty("_Wetness");
            s.hasSss = m.HasProperty("_SSSStrength");
            s.hasPore = m.HasProperty("_PoreStrength");
            s.hasBump = m.HasProperty("_BumpScale");
            if (s.hasMetallic) s.metallic = m.GetFloat("_Metallic");
            if (s.hasSmoothness) s.smoothness = m.GetFloat("_Smoothness");
            if (s.hasWetness) s.wetness = m.GetFloat("_Wetness");
            if (s.hasSss) s.sss = m.GetFloat("_SSSStrength");
            if (s.hasPore) s.pore = m.GetFloat("_PoreStrength");
            if (s.hasBump) s.bump = m.GetFloat("_BumpScale");
            return s;
        }

        static void Write(Slot s)
        {
            if (s.mat == null) return;
            var baseMap = s.mat.HasProperty("_BaseMap") ? s.mat.GetTexture("_BaseMap") : null;
            var bump = s.mat.HasProperty("_BumpMap") ? s.mat.GetTexture("_BumpMap") : null;
            var tint = s.mat.HasProperty("_BaseColor") ? s.mat.GetColor("_BaseColor") : Color.white;
            if (s.shader != null) s.mat.shader = s.shader;
            if (baseMap != null && s.mat.HasProperty("_BaseMap")) s.mat.SetTexture("_BaseMap", baseMap);
            if (bump != null && s.mat.HasProperty("_BumpMap")) s.mat.SetTexture("_BumpMap", bump);
            if (s.mat.HasProperty("_BaseColor")) s.mat.SetColor("_BaseColor", tint);
            if (s.metallicKeyword) s.mat.EnableKeyword(MetallicKeyword); else s.mat.DisableKeyword(MetallicKeyword);
            if (s.hasMetallic && s.mat.HasProperty("_Metallic")) s.mat.SetFloat("_Metallic", s.metallic);
            if (s.hasSmoothness && s.mat.HasProperty("_Smoothness")) s.mat.SetFloat("_Smoothness", s.smoothness);
            if (s.hasWetness && s.mat.HasProperty("_Wetness")) s.mat.SetFloat("_Wetness", s.wetness);
            if (s.hasSss && s.mat.HasProperty("_SSSStrength")) s.mat.SetFloat("_SSSStrength", s.sss);
            if (s.hasPore && s.mat.HasProperty("_PoreStrength")) s.mat.SetFloat("_PoreStrength", s.pore);
            if (s.hasBump && s.mat.HasProperty("_BumpScale")) s.mat.SetFloat("_BumpScale", s.bump);
        }

        static void Match(Material body, Material face)
        {
            var baseMap = body.HasProperty("_BaseMap") ? body.GetTexture("_BaseMap") : null;
            var bump = body.HasProperty("_BumpMap") ? body.GetTexture("_BumpMap") : null;
            var tint = body.HasProperty("_BaseColor") ? body.GetColor("_BaseColor") : Color.white;
            if (face != null && face.shader != null) body.shader = face.shader;
            if (baseMap != null && body.HasProperty("_BaseMap")) body.SetTexture("_BaseMap", baseMap);
            if (bump != null && body.HasProperty("_BumpMap")) body.SetTexture("_BumpMap", bump);
            if (body.HasProperty("_BaseColor")) body.SetColor("_BaseColor", tint);
            body.DisableKeyword(MetallicKeyword);
            float metallic = face != null && face.HasProperty("_Metallic") ? face.GetFloat("_Metallic") : 0f;
            float smoothness = face != null && face.HasProperty("_Smoothness") ? face.GetFloat("_Smoothness") : 0.25f;
            if (body.HasProperty("_Metallic")) body.SetFloat("_Metallic", metallic);
            if (body.HasProperty("_Smoothness")) body.SetFloat("_Smoothness", smoothness);
            Copy(face, body, "_Wetness");
            Copy(face, body, "_SSSStrength");
            Copy(face, body, "_PoreStrength");
            Copy(face, body, "_BumpScale");
        }

        static void Copy(Material from, Material to, string prop)
        {
            if (from == null || !from.HasProperty(prop) || !to.HasProperty(prop)) return;
            to.SetFloat(prop, from.GetFloat(prop));
        }

        static Material ActiveFace(Transform root)
        {
            Material found = null;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (r.name.IndexOf("Eye", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !m.name.StartsWith("M_Face125_", System.StringComparison.Ordinal)) continue;
                    if (m.name.IndexOf("Eye", System.StringComparison.Ordinal) >= 0 || m.name.IndexOf("Pony", System.StringComparison.Ordinal) >= 0) continue;
                    found = m;
                }
            }
            return found;
        }
    }
}
