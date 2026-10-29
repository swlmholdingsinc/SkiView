using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SkiView
{
    /// <summary>Player-chosen display name. Saved trimmed, in the typed case, in PlayerPrefs <c>sv.player.name</c>.
    /// <see cref="Name"/> is null when nothing valid is stored, so callers do not invent a fallback.</summary>
    public static class SkiViewPlayer
    {
        public const string PrefKey = "sv.player.name";
        public const int MinLen = 1;
        public const int MaxLen = 12;

        /// <summary>Development builds and store-screenshot captures may run without a saved name.</summary>
        public static bool CaptureExempt =>
            Debug.isDebugBuild
            || SkiViewArgs.Has("shot")
            || SkiViewArgs.Has("capture")
            || UrlHasCapture();

        /// <summary>A player build with no valid name must show the name screen before the run, URL args included.</summary>
        public static bool BlocksRun => Application.isPlaying && !HasName && !CaptureExempt;

        static bool UrlHasCapture()
        {
            string url = Application.absoluteURL ?? "";
            return url.IndexOf("/capture", StringComparison.OrdinalIgnoreCase) >= 0
                || url.IndexOf("shot=", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Saved display name, or null if missing or corrupt.</summary>
        public static string Name
        {
            get
            {
                string stored = PlayerPrefs.GetString(PrefKey, "");
                return TryNormalize(stored, out var name) ? name : null;
            }
        }

        public static bool HasName => Name != null;

        /// <summary>NFC, trim, and accept 1–12 letters, digits, spaces, hyphens, or apostrophes. Keeps typed case. Rejects emoji, surrogates, Wade, and Felderpuss.</summary>
        public static bool TryNormalize(string raw, out string name)
        {
            name = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string s = raw.Normalize(NormalizationForm.FormC).Trim();
            if (s.Length < MinLen || s.Length > MaxLen) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsSurrogate(c)) return false;
                bool ok = char.IsLetter(c) || (c >= '0' && c <= '9') || c == ' ' || c == '-' || c == '\'' || c == '\u2019' || c == '\u2018';
                if (!ok) return false;
            }
            if (Blocked(s)) return false;
            name = s;
            return true;
        }

        public static bool TrySave(string raw)
        {
            if (!TryNormalize(raw, out var name)) return false;
            PlayerPrefs.SetString(PrefKey, name);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Leetspeak fold of a copy: lower case, accents stripped, 4/@ a, 3 e, 0 o, 1/!/| i or l, 5/$ s, 7 t, and spaces, dots, dashes and underscores removed.</summary>
        static bool Blocked(string typed) => FoldContainsBan(Fold(typed), 0, new StringBuilder());

        static string Fold(string typed)
        {
            string lower = typed.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(lower.Length);
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                switch (c)
                {
                    case '4': case '@': sb.Append('a'); break;
                    case '3': sb.Append('e'); break;
                    case '0': sb.Append('o'); break;
                    case '1': case '!': case '|': sb.Append('\u0001'); break; // i or l
                    case '5': case '$': sb.Append('s'); break;
                    case '7': sb.Append('t'); break;
                    case ' ': case '.': case '-': case '_':
                    case '\u2010': case '\u2011': case '\u2012': case '\u2013': case '\u2014': case '\u2212':
                        break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        static bool FoldContainsBan(string src, int i, StringBuilder acc)
        {
            if (i == src.Length)
            {
                string s = acc.ToString();
                return s.Contains("wade") || s.Contains("felderpuss");
            }
            char c = src[i];
            if (c == '\u0001')
            {
                acc.Append('i');
                if (FoldContainsBan(src, i + 1, acc)) return true;
                acc.Length--;
                acc.Append('l');
                bool hit = FoldContainsBan(src, i + 1, acc);
                acc.Length--;
                return hit;
            }
            acc.Append(c);
            bool ok = FoldContainsBan(src, i + 1, acc);
            acc.Length--;
            return ok;
        }
    }
}
