using UnityEngine;

namespace SkiView
{
    /// <summary>T6: Low / Med / High graphics presets on top of the platform tier (phone, browser, desktop).
    /// High is the current look on every platform. Med and Low scale the reflection resolution, the far-tree density
    /// and the spray particle rates down from there. Water reflections are a separate on/off switch. Both persist (PlayerPrefs).</summary>
    public static class SkiViewQuality
    {
        public enum Preset { Low = 0, Med = 1, High = 2 }
        const string PRESET_PREF = "sv.gfx.preset", REFL_PREF = "sv.gfx.reflections";
        static bool loaded; static Preset preset = Preset.High; static bool reflections = true;

        //                                         Low    Med    High
        static readonly float[] REFL_SCALE  = { 0.5f,  0.75f, 1f };   // x the tier's reflection resolution scale
        static readonly float[] TREE_KEEP   = { 0.5f,  0.75f, 1f };   // x the tier's far-tree keep fraction (trees within nearBand always stay)
        static readonly float[] NEAR_BAND   = { 0.45f, 0.75f, 1f };   // x the band from the water's edge where every tree is kept
        static readonly float[] SPRAY_RATE  = { 0.4f,  0.7f,  1f };   // x spray emission rates and particle caps

        static void Load()
        {
            if (loaded) return; loaded = true;
            preset = (Preset)Mathf.Clamp(PlayerPrefs.GetInt(PRESET_PREF, (int)Preset.High), 0, 2);
            reflections = PlayerPrefs.GetInt(REFL_PREF, 1) == 1;
            // captures / perf runs: ?preset=low|med|high, ?refl=0 (URL or -preset on the command line), SV_PRESET / SV_REFL in the editor
            string a = SkiViewArgs.Get("preset") ?? System.Environment.GetEnvironmentVariable("SV_PRESET");
            if (!string.IsNullOrEmpty(a)) preset = a.StartsWith("l") ? Preset.Low : a.StartsWith("m") ? Preset.Med : Preset.High;
            string r = SkiViewArgs.Get("refl") ?? System.Environment.GetEnvironmentVariable("SV_REFL");
            if (!string.IsNullOrEmpty(r)) reflections = r != "0";
        }
        public static Preset Current { get { Load(); return preset; } }
        public static bool Reflections { get { Load(); return reflections; } }
        public static float ReflectionScale => REFL_SCALE[(int)Current];
        public static float TreeKeep => TREE_KEEP[(int)Current];
        public static float NearBand => NEAR_BAND[(int)Current];
        public static float Spray => SPRAY_RATE[(int)Current];

        public static void SetPreset(Preset p) { Load(); preset = p; PlayerPrefs.SetInt(PRESET_PREF, (int)p); PlayerPrefs.Save(); Changed?.Invoke(); }
        public static void SetReflections(bool on) { Load(); reflections = on; PlayerPrefs.SetInt(REFL_PREF, on ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        public static event System.Action Changed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { loaded = false; Changed = null; }
    }
}
