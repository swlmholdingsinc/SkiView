using System.Text;
using UnityEngine;

namespace SkiView
{
    /// <summary>Player-chosen display name. Saved upper-case in PlayerPrefs <c>sv.player.name</c>.
    /// <see cref="Name"/> is null when nothing valid is stored, so callers (welcome line, skier banner, world records) do not invent a fallback.</summary>
    public static class SkiViewPlayer
    {
        public const string PrefKey = "sv.player.name";
        public const int MinLen = 1;
        public const int MaxLen = 12;

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

        /// <summary>Trim, upper-case, and accept 1–12 letters, digits, spaces, hyphens, or apostrophes. Rejects Wade and Felderpuss.</summary>
        public static bool TryNormalize(string raw, out string name)
        {
            name = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string s = raw.Replace('\u2019', '\'').Replace('\u2018', '\'').Trim().ToUpperInvariant();
            if (s.Length < MinLen || s.Length > MaxLen) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool ok = char.IsLetter(c) || (c >= '0' && c <= '9') || c == ' ' || c == '-' || c == '\'';
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

        static bool Blocked(string upper)
        {
            if (ContainsBlocked(Compact(upper))) return true;
            var parts = upper.Split(' ');
            for (int i = 0; i < parts.Length; i++)
                if (ContainsBlocked(Compact(parts[i]))) return true;
            return false;
        }

        static string Compact(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetter(c) || (c >= '0' && c <= '9')) sb.Append(c);
            }
            return sb.ToString();
        }

        static bool ContainsBlocked(string compact) =>
            compact.Contains("WADE") || compact.Contains("FELDERPUSS");
    }
}
