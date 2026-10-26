using UnityEngine;

namespace SkiView
{
    /// <summary>Per-lake gelcoat tint (Christopher's boat README snippet): one MaterialPropertyBlock per gelcoat/livery slot
    /// on every LOD renderer. MPBs aren't serialized, so this re-applies on enable (edit mode too).</summary>
    [ExecuteAlways]
    public class BoatPalette : MonoBehaviour
    {
        public string lake = "skiview";
        public Color deck = new Color32(0xF4, 0xF5, 0xF2, 0xFF), band = new Color32(0x1C, 0x3F, 0x9E, 0xFF),
                     accent = new Color32(0xB9, 0xC0, 0xC9, 0xFF), livery = Color.white;
        public float accentMetallic = 0.35f;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name;
                    Color? c = n.StartsWith("M_Gelcoat_Deck") ? deck : n.StartsWith("M_Gelcoat_Band") ? band
                             : n.StartsWith("M_Gelcoat_Accent") ? accent : n.StartsWith("M_Livery") ? livery : (Color?)null;
                    if (c == null) continue;
                    r.GetPropertyBlock(mpb, i); mpb.SetColor("_BaseColor", c.Value);
                    if (n.StartsWith("M_Gelcoat_Accent")) mpb.SetFloat("_Metallic", accentMetallic);
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }
    }
}
