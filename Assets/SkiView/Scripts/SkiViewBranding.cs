using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiView
{
    /// <summary>Reads Resources/Branding/branding.json once. Title/badge/product strings come from here.
    /// Scott Oct 24 2026 ~11:49 PM CT: Unity player-facing brand is Ski View (Wade logos/title off). Empty fields fall back to
    /// the built-in Ski View strings below. Wade logo paths only load when branding+likeness flags are on.</summary>
    public static class SkiViewBranding
    {
        [Serializable] public class Flags { public bool wadeBrandingEnabled, wadeLikenessEnabled, wadeContentApproved, sponsorSlotsEnabled, wadeLicenseSigned; }
        [Serializable] public class Logos { public string primary, icon, wordmark, titleHero; }
        [Serializable] public class Colors { public string accent; }
        [Serializable] public class Links { public string home, about, clinics, podcast; }
        [Serializable] public class Block { public string title, skierDisplayName, subtitle, badge, productName, storeName, greyboxTag; public Logos logos; public Colors colors; public Links links; }
        [Serializable] public class ClinicBlock { public string modeName; }
        [Serializable] class File { public Flags flags; public Block wade; public ClinicBlock clinic; }

        static File f;
        static File F
        {
            get
            {
                if (f != null) return f;
                var ta = Resources.Load<TextAsset>("Branding/branding");
                try { f = ta != null ? JsonUtility.FromJson<File>(ta.text) : null; } catch (Exception e) { Debug.LogWarning("[branding] " + e.Message); }
                if (f == null) f = new File();
                if (f.flags == null) f.flags = new Flags();
                if (f.wade == null) f.wade = new Block();
                return f;
            }
        }

        public static Flags Flag => F.flags;
        /// <summary>Likeness mesh/path gate (Wade look). Logos/title branding use Flag.wadeBrandingEnabled separately.</summary>
        public static bool Wade => Flag.wadeLikenessEnabled;
        static readonly Block DEF = new Block { title = "SKI VIEW", skierDisplayName = "SCOTT", subtitle = "SLALOM / JUMP / FREE RIDE", badge = "SV", productName = "SKI VIEW", storeName = "Ski View", greyboxTag = "", colors = new Colors { accent = "#2F7DF6" } };
        static string Pick(Func<Block, string> g) { string s = g(F.wade); return string.IsNullOrEmpty(s) ? (g(DEF) ?? "") : s; }

        /// <summary>This Unity product always shows SKI VIEW. Branding flags and branding.json cannot rename it.</summary>
        public const string LockedProductName = "SKI VIEW";
        public static string Title => LockedProductName;
        /// <summary>On-screen skier name is the saved player name. Empty until they enter one. Never a Wade or Felderpuss fallback.</summary>
        public static string SkierName => SkiViewPlayer.Name ?? "";
        public static string Subtitle => Pick(b => b.subtitle);
        public static string Badge => Pick(b => b.badge);
        public static string ProductName => LockedProductName;
        public static string StoreName => "Ski View";
        public static string GreyboxTag => Pick(b => b.greyboxTag);
        public static string Accent => Pick(b => b.colors?.accent);
        public static string LogoPath(string slot)
        {
            // Wade-gated logos (hero etc.) — off when Wade branding disabled
            if (!Flag.wadeBrandingEnabled) return null;
            Func<Block, string> g = b => b.logos == null ? null : slot == "primary" ? b.logos.primary : slot == "icon" ? b.logos.icon : slot == "wordmark" ? b.logos.wordmark : slot == "titleHero" ? b.logos.titleHero : null;
            string s = Pick(g);
            return string.IsNullOrEmpty(s) ? null : s;
        }
        /// <summary>SWLM / company logo paths from branding.json — available even when Wade branding is off.</summary>
        public static string CompanyLogoPath(string slot = "primary")
        {
            Func<Block, string> g = b => b.logos == null ? null : slot == "primary" ? b.logos.primary : slot == "icon" ? b.logos.icon : slot == "wordmark" ? b.logos.wordmark : null;
            string s = Pick(g);
            return string.IsNullOrEmpty(s) ? null : s;
        }
        public static Links Link => F.wade.links ?? new Links();
        /// <summary>T22: the "title.hero" slot (ship:false, test builds only). JsonUtility can't read a dotted key, so pull the few fields
        /// by pattern. Returns the Resources path for the aspect (20x9 / 4x3 / default), or null when the slot or its flags are off.</summary>
        public static string TitleHeroPath(float aspect)
        {
            var ta = Resources.Load<TextAsset>("Branding/branding"); if (ta == null) return null;
            var m = System.Text.RegularExpressions.Regex.Match(ta.text, "\"title\\.hero\"\\s*:\\s*\\{(?<b>[\\s\\S]*?)\"fallback\"");
            if (!m.Success || !(Flag.wadeBrandingEnabled && Flag.wadeLikenessEnabled)) return null;
            string b = m.Groups["b"].Value;
            string G(string k) { var r = System.Text.RegularExpressions.Regex.Match(b, "\"" + k + "\"\\s*:\\s*\"([^\"]+)\""); return r.Success ? r.Groups[1].Value : null; }
            return (aspect >= 2f ? G("20x9") : aspect <= 1.45f ? G("4x3") : null) ?? G("asset");
        }
        public static string ClinicModeName => string.IsNullOrEmpty(F.clinic?.modeName) ? "Clinic" : F.clinic.modeName;
        public static void Reload() { f = null; }
    }
}
