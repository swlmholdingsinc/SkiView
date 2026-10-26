using UnityEngine;

namespace SkiView
{
    /// <summary>Pre-run setup: V3 ski colourway + binding (T28); slalom rope/speed; Jump Phase A speed/ramp (T15/T31).</summary>
    public partial class SkiViewGame
    {
        public const string SKI_PREF = "sv.setup.ski", BIND_PREF = "sv.setup.binding", ROPE_PREF = "sv.setup.rope", MPH_PREF = "sv.setup.mph";
        public const string JUMP_KMH_PREF = "sv.setup.jump.kmh", JUMP_RAMP_PREF = "sv.setup.jump.ramp";
        /// <summary>T15/T31 Jump Phase A boat speeds (km/h).</summary>
        public static readonly float[] JUMP_KMH = { 45f, 48f, 51f, 54f, 57f };
        public static readonly string[] JUMP_KMH_LABEL = {
            "45 km/h\n28.0 mph", "48 km/h\n29.8 mph", "51 km/h\n31.7 mph", "54 km/h\n33.6 mph", "57 km/h\n35.4 mph"
        };
        /// <summary>T15/T31 ramp heights (m). Default 1.65. Women grey 1.80 deferred (T31 out of scope).</summary>
        public static readonly float[] JUMP_RAMP_M = { 1.50f, 1.65f, 1.80f };
        public static readonly string[] JUMP_RAMP_LABEL = { "1.50 m", "1.65 m", "1.80 m" };
        /// <summary>b10.13 V3 colourways (Green default). Plain Title Case per T28. Preview: Resources/SkiViewUI/Skis/ski_N.png (+ ski_N_db.png for double boot).</summary>
        public static readonly string[] SKI_NAMES = { "Green", "Red", "Blue", "Black", "Carbon", "White" };
        public static readonly string[] SKI_IDS = { "green", "red", "blue", "black", "carbon", "white" };
        public static readonly string[] SKI_BLURB = {
            "Fresh green deck with a dark centre stripe.",
            "Bold red, easy to pick out on the water.",
            "Cool blue with the same unbranded stripe.",
            "Solid black, low glare.",
            "Carbon twill weave — charcoal with cool highlights.",
            "Clean white with a dark pinstripe."
        };
        public static readonly string[] BIND_NAMES = { "Toe plate", "Double boot" };
        public const int SETUP_ROPES = 8;   // 15 off .. 41 off
        public static int SetupSki => Mathf.Clamp(PlayerPrefs.GetInt(SKI_PREF, 0), 0, SKI_NAMES.Length - 1);
        public static int SetupBinding => Mathf.Clamp(PlayerPrefs.GetInt(BIND_PREF, 0), 0, BIND_NAMES.Length - 1);
        public static int SetupRope => Mathf.Clamp(PlayerPrefs.GetInt(ROPE_PREF, 0), 0, SETUP_ROPES - 1);
        public static int SetupMph => PlayerPrefs.GetInt(MPH_PREF, 34) == 36 ? 36 : 34;
        public static int SetupJumpKmhIdx
        {
            get
            {
                float v = PlayerPrefs.GetFloat(JUMP_KMH_PREF, 54f);
                for (int i = 0; i < JUMP_KMH.Length; i++) if (Mathf.Abs(JUMP_KMH[i] - v) < 0.01f) return i;
                return 3; // 54 default
            }
        }
        public static int SetupJumpRampIdx
        {
            get
            {
                float v = PlayerPrefs.GetFloat(JUMP_RAMP_PREF, 1.65f);
                for (int i = 0; i < JUMP_RAMP_M.Length; i++) if (Mathf.Abs(JUMP_RAMP_M[i] - v) < 0.01f) return i;
                return 1; // 1.65 default
            }
        }
        public static void SaveJumpSetup(int kmhIdx, int rampIdx)
        {
            kmhIdx = Mathf.Clamp(kmhIdx, 0, JUMP_KMH.Length - 1);
            rampIdx = Mathf.Clamp(rampIdx, 0, JUMP_RAMP_M.Length - 1);
            PlayerPrefs.SetFloat(JUMP_KMH_PREF, JUMP_KMH[kmhIdx]);
            PlayerPrefs.SetFloat(JUMP_RAMP_PREF, JUMP_RAMP_M[rampIdx]);
            PlayerPrefs.Save();
        }
        public static void SaveSetup(int ski, int rope, int mph) => SaveSetup(ski, SetupBinding, rope, mph);
        public static void SaveSetup(int ski, int binding, int rope, int mph)
        {
            PlayerPrefs.SetInt(SKI_PREF, ski);
            PlayerPrefs.SetInt(BIND_PREF, binding);
            PlayerPrefs.SetInt(ROPE_PREF, rope);
            PlayerPrefs.SetInt(MPH_PREF, mph == 36 ? 36 : 34);
            PlayerPrefs.Save();
        }

        int runMph = 34;
        /// <summary>Slalom boat speed for this run (34 or 36 mph); jump runs use jumpBoatKmh.</summary>
        public float RunMph => runMph;
        float SpeedMul => jumpMode || trickMode ? 1f : runMph / 34f;
        string MphTag => runMph + " MPH";

        public void ApplySetup()
        {
            runMph = SetupMph;
            if (trickMode)
            {
                trickBoatKmh = TRICK_KMH[SetupTrickKmhIdx];
                trickRopeM = TRICK_ROPE_M[SetupTrickRopeIdx];
                if (sim != null) { sim.ropeM = trickRopeM; ropeTarget = trickRopeM; }
                ApplyTrickSki(true);
                return;
            }
            if (jumpMode)
            {
                jumpBoatKmh = JUMP_KMH[SetupJumpKmhIdx];
                rampHeight = JUMP_RAMP_M[SetupJumpRampIdx];
                jumpRopeM = 23f;
                if (sim != null) { sim.ropeM = jumpRopeM; ropeTarget = jumpRopeM; }
                ApplySki(SetupSki);
                JumpGeometry();
                ResetJumpRound();
                return;
            }
            if (sim != null) { sim.ropeIndex = SetupRope; sim.ropeM = SkierSim.ROPE_M[sim.ropeIndex]; ropeTarget = sim.ropeM; }
            ApplySki(SetupSki);
        }

        static readonly string[] KIT_SKI = { "SkiBody", "Fin", "FinWing", "FinBlock" };
        public void ApplySki(int k) { if (ski != null) ApplySkiTo(ski, k, SetupBinding); }

        /// <summary>Show V3 colourway k + binding b on a kit ski root. Prefabs from Resources/Skis/SlalomSki_V3_*.</summary>
        public static void ApplySkiTo(Transform skiT, int k) => ApplySkiTo(skiT, k, SetupBinding);
        public static void ApplySkiTo(Transform skiT, int k, int binding)
        {
            k = Mathf.Clamp(k, 0, SKI_NAMES.Length - 1);
            binding = Mathf.Clamp(binding, 0, BIND_NAMES.Length - 1);
            string want = binding == 1 ? "SlalomSki_V3_DoubleBoot" : "SlalomSki_V3_ToePlate";
            // Hide kit + any V2 leftover
            foreach (var n in KIT_SKI) { var t = skiT.Find(n); if (t != null) t.gameObject.SetActive(false); }
            var oldV2 = skiT.Find("SlalomSki_V2"); if (oldV2 != null) oldV2.gameObject.SetActive(false);
            Transform v3 = null;
            foreach (var nm in new[] { "SlalomSki_V3_ToePlate", "SlalomSki_V3_DoubleBoot" })
            {
                var t = skiT.Find(nm);
                if (t == null) continue;
                bool on = nm == want;
                t.gameObject.SetActive(on);
                if (on) v3 = t;
            }
            if (v3 == null)
            {
                var pf = Resources.Load<GameObject>("Skis/" + want);
                if (pf == null) { Debug.LogWarning("[SkiView] Resources/Skis/" + want + " missing"); return; }
                v3 = Object.Instantiate(pf, skiT, false).transform; v3.name = want;
                v3.localPosition = new Vector3(0, 0, 0.671f); v3.localRotation = Quaternion.identity;
            }
            string colour = SKI_IDS[k];
            var alb = Resources.Load<Texture2D>("Skis/V3/" + colour + "_albedo");
            var nrm = Resources.Load<Texture2D>("Skis/V3/" + colour + "_normal");
            var ms = Resources.Load<Texture2D>("Skis/V3/" + colour + "_metallic_smoothness");
            foreach (var r in v3.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (m == null) continue;
                    string bn = m.name.Replace(" (Instance)", "");
                    if (!(bn.Contains("top") || bn.Contains("base") || bn.Contains("skin"))) continue;
                    if (!skinOrig.TryGetValue(r, out var orig) || !orig.name.Contains(bn.Split('_')[0])) { /* keep per-slot */ }
                    // Clone once per renderer+slot so we do not dirty the shared green mat across colourways
                    var key = r.GetInstanceID() + ":" + i;
                    if (!skinByKey.TryGetValue(key, out var sm) || sm == null)
                    {
                        sm = new Material(m) { name = bn + "_rt" };
                        skinByKey[key] = sm;
                    }
                    if (alb != null) sm.SetTexture("_BaseMap", alb);
                    sm.SetColor("_BaseColor", Color.white);
                    if (nrm != null) { sm.SetTexture("_BumpMap", nrm); sm.EnableKeyword("_NORMALMAP"); sm.SetFloat("_BumpScale", 1f); }
                    if (ms != null)
                    {
                        sm.SetTexture("_MetallicGlossMap", ms);
                        sm.EnableKeyword("_METALLICSPECGLOSSMAP");
                        sm.SetFloat("_Smoothness", 1f); sm.SetFloat("_Metallic", 1f);
                    }
                    mats[i] = sm; ch = true;
                }
                if (ch) r.sharedMaterials = mats;
            }
            // Kit boots stay hidden — V3 carries its own bindings
            foreach (var bn in new[] { "FrontBoot", "RearBoot" })
            {
                var b = skiT.Find(bn); if (b == null) continue;
                foreach (var r in b.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            // Spray from V3 emitter if present
            var spray = skiT.GetComponentInChildren<SkiSpray>(true);
            var em = FindDeep(v3, "SprayEmitter");
            if (em != null && spray != null) spray.tailZ = skiT.InverseTransformPoint(em.position).z;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { var f = FindDeep(t.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        static readonly System.Collections.Generic.Dictionary<Renderer, Material> skinOrig = new System.Collections.Generic.Dictionary<Renderer, Material>();
        static readonly System.Collections.Generic.Dictionary<string, Material> skinByKey = new System.Collections.Generic.Dictionary<string, Material>();
    }
}
