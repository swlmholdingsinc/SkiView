using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SkiView
{
    /// <summary>
    /// Local world-record boards for Ski View. One top-10 per discipline (slalom, jump, tricks).
    /// This project has no online leaderboard; boards persist in PlayerPrefs.
    ///
    /// Ranking: a higher score ranks first. The same score keeps the earlier run ahead
    /// (lower sequence). Ranks are unique — a tie does not share a place.
    /// A run qualifies when the board has fewer than 10 scores, or its score is strictly
    /// higher than 10th place. A tie with 10th place stays off the list.
    /// Scores at or below zero are not recorded. The same player can hold more than one row.
    /// Slalom records one score per set: the furthest pass that scored buoys, not one row per pass.
    /// A fall counts when that pass scored buoys. A fall with no buoys keeps the earlier pass and does not add a zero row.
    /// The holder is always <see cref="HolderName"/>: PlayerPrefs sv.player.name, or PLAYER when that is empty.
    /// Branding is not a name source. Old rows with a blank holder, or the old branding name, display as <see cref="HolderName"/>.
    /// The first launch copies each lake's saved personal best (<c>sv.pb.&lt;lake&gt;</c>) onto the slalom board once.
    /// </summary>
    public static class SkiViewRecords
    {
        public const int Capacity = 10;
        public const string PrefPrefix = "sv.records.v1.";
        /// <summary>Saved on first launch by the player-name prompt. Records read it at write time.</summary>
        public const string PlayerNamePref = "sv.player.name";
        public const string FallbackHolder = "PLAYER";
        /// <summary>Per-lake personal best written by the results plate. Imported once onto the slalom board.</summary>
        public const string PbPrefPrefix = "sv.pb.";
        /// <summary>"1" after <see cref="ImportLegacyPersonalBests"/> has run. A later lake best is not imported again.</summary>
        public const string PbImportedKey = PrefPrefix + "pbImported";

        public struct LegacyPbLake { public string id; public string name; }

        /// <summary>Lakes that may already have <c>sv.pb.&lt;id&gt;</c>. Names match the in-run lake name, not the menu's all-caps title.</summary>
        public static readonly LegacyPbLake[] LegacyPbLakes =
        {
            new LegacyPbLake { id = "skiview", name = "Ski View" },
            new LegacyPbLake { id = "cedarbend", name = "Cedar Bend" },
            new LegacyPbLake { id = "saltwhite", name = "Saltwhite" },
            new LegacyPbLake { id = "villaserena", name = "Villa Serena" },
            new LegacyPbLake { id = "tallpine", name = "Tall Pine" },
            new LegacyPbLake { id = "heronmarsh", name = "Heron Marsh" },
            new LegacyPbLake { id = "harrowpond", name = "Harrow Pond" },
            new LegacyPbLake { id = "gulfwind", name = "Gulfwind" },
            new LegacyPbLake { id = "saguaro", name = "Saguaro" },
            new LegacyPbLake { id = "marrowind", name = "Marrowind" },
        };

        public enum Discipline { Slalom, Jump, Trick }

        [Serializable]
        public class Entry
        {
            public string holder;
            public float score;
            public string detail;
            /// <summary>Monotonic per board. Lower was achieved earlier and ranks ahead on a tie.</summary>
            public int seq;
            /// <summary>UTC DateTime ticks, stored as text because JsonUtility does not serialize long.</summary>
            public string achieved;
            public int rope;
            public float buoys;
        }

        [Serializable]
        public class SaveBag { public Entry[] entries; }

        public struct Place
        {
            public bool attempted;
            public bool qualified;
            public int rank;
            public int count;
            public int seq;
            public float score;
            public Discipline discipline;
        }

        static Dictionary<string, string> memory;

        /// <summary>Test hook. When on, boards stay in memory and PlayerPrefs is left alone. Pass false to restore PlayerPrefs.</summary>
        public static void UseMemoryStore(bool on) { memory = on ? new Dictionary<string, string>() : null; }

        /// <summary>Test hook. Replaces one saved board. Respects the memory store.</summary>
        public static void SetRawForTests(Discipline d, string json) { Write(Key(d), json ?? ""); }

        public static Place Miss(Discipline d) => new Place { attempted = true, discipline = d };

        /// <summary>Clinic and the tutorial never write a record. Same rule for slalom, jump, and tricks.</summary>
        public static bool AllowRecord(bool clinicActive, bool tutorialRunning) => !clinicActive && !tutorialRunning;

        /// <summary>The furthest scored pass of one slalom set. A fall or miss ends the set; quit and title take it as it stands.</summary>
        public struct SlalomSet
        {
            public bool has;
            public float score;
            public int rope;
            public float buoys;
            public string detail;
        }

        /// <summary>
        /// Fold one pass into the open set. A clean pass does not submit.
        /// A fall or miss (not clean) ends the set: <paramref name="submit"/> is the furthest pass that scored buoys, and the set is cleared.
        /// A fall with buoys counts. A pass with no buoys is not that pass. Returns true when the set ended.
        /// </summary>
        public static bool NoteSlalomSetPass(ref SlalomSet set, int rope, float buoys, bool clean, string detail, out SlalomSet submit)
        {
            ConsiderSlalomPass(ref set, rope, buoys, detail);
            submit = default;
            if (clean) return false;
            submit = set;
            set = default;
            return true;
        }

        /// <summary>Quit or return to title. Returns the furthest scored pass once, and clears the set.</summary>
        public static bool TakeSlalomSet(ref SlalomSet set, out SlalomSet submit)
        {
            submit = set;
            set = default;
            return submit.has;
        }

        /// <summary>Keep the pass that reached furthest: a shorter rope, or more buoys on that same rope. An easier rope never replaces it.</summary>
        static void ConsiderSlalomPass(ref SlalomSet set, int rope, float buoys, string detail)
        {
            if (buoys <= 0.01f) return;
            if (set.has && (rope < set.rope || (rope == set.rope && buoys <= set.buoys + 0.01f))) return;
            set.has = true;
            set.score = rope * 6f + buoys;
            set.rope = rope;
            set.buoys = buoys;
            set.detail = detail ?? "";
        }

        /// <summary>
        /// Saved player name. This is the only lookup: a later change can return SkiViewPlayer.Name from here.
        /// PlayerPrefs <see cref="PlayerNamePref"/>, trimmed. Empty or missing becomes <see cref="FallbackHolder"/>.
        /// </summary>
        public static string HolderName()
        {
            string n = PlayerPrefs.GetString(PlayerNamePref, "");
            if (string.IsNullOrWhiteSpace(n)) return FallbackHolder;
            return n.Trim();
        }

        /// <summary>
        /// Holder to write or display. Blank uses <see cref="HolderName"/>.
        /// Older boards stored the branding skier name when no player name existed; that value is not a typed name
        /// unless it is the saved player name now.
        /// </summary>
        public static string ResolveHolder(string stored)
        {
            string current = HolderName();
            if (string.IsNullOrWhiteSpace(stored)) return current;
            stored = stored.Trim();
            if (stored.Equals("SCOTT", StringComparison.OrdinalIgnoreCase) && !current.Equals(stored, StringComparison.OrdinalIgnoreCase))
                return current;
            return stored;
        }

        /// <summary>
        /// Copy each saved lake personal best onto the slalom board, then set <see cref="PbImportedKey"/>.
        /// A best that does not make the top 10 is still consumed: the key is set either way, so a later run is not imported twice.
        /// Returns how many imported scores took a place.
        /// </summary>
        public static int ImportLegacyPersonalBests()
        {
            if (Read(PbImportedKey) == "1") return 0;
            int added = 0;
            for (int i = 0; i < LegacyPbLakes.Length; i++)
            {
                var lake = LegacyPbLakes[i];
                float sc = ReadFloat(PbPrefPrefix + lake.id);
                DecodeSlalomScore(sc, out int rope, out float buoys);
                if (buoys <= 0.01f) continue;
                var place = Submit(Discipline.Slalom, sc, lake.name, rope, buoys);
                if (place.qualified) added++;
            }
            Write(PbImportedKey, "1");
            return added;
        }

        /// <summary>
        /// Split a lake personal-best score (rope index × 6 + buoys) the same way the pause plate prints it.
        /// An exact multiple of 6 is a full pass: 6 buoys at that rope, not 0 buoys at the next rope.
        /// </summary>
        public static void DecodeSlalomScore(float score, out int rope, out float buoys)
        {
            rope = 0;
            buoys = 0f;
            if (float.IsNaN(score) || float.IsInfinity(score) || score <= 0f) return;
            int max = SkierSim.ROPE_LABEL.Length - 1;
            rope = Mathf.Clamp(Mathf.FloorToInt((score - 0.01f) / 6f), 0, max);
            float rem = score - rope * 6f;
            if (rem < 0.01f) rem = 6f;
            buoys = rem;
        }

        /// <summary>Test hook. Stores a float where <see cref="ImportLegacyPersonalBests"/> will read it.</summary>
        public static void SetFloatForTests(string key, float value)
        {
            if (memory != null) { memory[key] = value.ToString(CultureInfo.InvariantCulture); return; }
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        public static Place Submit(Discipline d, float score, string detail, int rope = -1, float buoys = 0f, int seq = 0, long ticks = 0)
        {
            var board = Load(d);
            if (seq <= 0) seq = NextSeq(board);
            if (ticks <= 0) ticks = DateTime.UtcNow.Ticks;
            var entry = new Entry
            {
                holder = HolderName(),
                score = score,
                detail = detail ?? "",
                seq = seq,
                achieved = ticks.ToString(CultureInfo.InvariantCulture),
                rope = rope,
                buoys = buoys
            };
            var place = TryInsert(board, entry);
            place.discipline = d;
            if (place.qualified) Save(d, board);
            return place;
        }

        /// <summary>Insert into an already-ranked board. Does not touch PlayerPrefs. The board must be best-first.</summary>
        public static Place TryInsert(List<Entry> board, Entry entry)
        {
            if (board == null) board = new List<Entry>();
            if (entry == null || float.IsNaN(entry.score) || float.IsInfinity(entry.score) || entry.score <= 0f)
                return new Place { attempted = true, qualified = false, count = board.Count, score = entry != null && !float.IsNaN(entry.score) && !float.IsInfinity(entry.score) ? entry.score : 0f };

            entry.holder = ResolveHolder(entry.holder);
            if (entry.detail == null) entry.detail = "";
            if (entry.seq <= 0) entry.seq = NextSeq(board);
            if (string.IsNullOrEmpty(entry.achieved)) entry.achieved = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);

            if (board.Count >= Capacity && !(entry.score > board[Capacity - 1].score))
                return new Place { attempted = true, qualified = false, rank = 0, count = board.Count, score = entry.score, seq = entry.seq };

            int at = 0;
            int limit = Math.Min(board.Count, Capacity);
            for (; at < limit; at++)
                if (RanksAhead(entry, board[at])) break;
            board.Insert(at, entry);
            if (board.Count > Capacity) board.RemoveRange(Capacity, board.Count - Capacity);
            int rank = board.IndexOf(entry) + 1;
            return new Place
            {
                attempted = true,
                qualified = rank > 0,
                rank = rank,
                count = board.Count,
                seq = entry.seq,
                score = entry.score
            };
        }

        public static List<Entry> Load(Discipline d)
        {
            var board = new List<Entry>();
            string json = Read(Key(d));
            if (string.IsNullOrEmpty(json)) return board;
            SaveBag bag = null;
            try { bag = JsonUtility.FromJson<SaveBag>(json); }
            catch (Exception e) { Debug.LogWarning("[SkiView] records: " + e.Message); }
            if (bag?.entries == null) return board;
            for (int i = 0; i < bag.entries.Length; i++)
            {
                var e = bag.entries[i];
                if (e == null || float.IsNaN(e.score) || float.IsInfinity(e.score) || e.score <= 0f) continue;
                e.holder = ResolveHolder(e.holder);
                if (e.detail == null) e.detail = "";
                board.Add(e);
            }
            board.Sort(Compare);
            if (board.Count > Capacity) board.RemoveRange(Capacity, board.Count - Capacity);
            return board;
        }

        public static string Format(Discipline d, Entry e)
        {
            if (e == null) return "";
            if (d == Discipline.Slalom)
            {
                int rope = Mathf.Clamp(e.rope, 0, SkierSim.ROPE_LABEL.Length - 1);
                return SkierSim.Fmt(e.buoys) + " @ " + SkierSim.ROPE_LABEL[rope];
            }
            if (d == Discipline.Jump)
            {
                if (UI.SkiViewUI.Kmh) return e.score.ToString("0.0", CultureInfo.InvariantCulture) + " m";
                int ft = Mathf.FloorToInt(e.score / 0.3048f + 0.5f);
                return ft + " ft";
            }
            return Mathf.RoundToInt(e.score) + " PTS";
        }

        public static string AchievedLabel(Entry e)
        {
            if (e == null || string.IsNullOrEmpty(e.achieved)) return "";
            if (!long.TryParse(e.achieved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) || ticks <= 0) return "";
            try { return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture); }
            catch (ArgumentOutOfRangeException) { return ""; }
        }

        public static string PlacementHeading(Place p, int boardCount)
        {
            if (!p.attempted) return "";
            if (p.score > 0f && p.qualified && p.rank == 1) return "NEW WORLD RECORD · RANK 1";
            if (p.score > 0f && p.qualified) return "WORLD RECORDS · RANK " + p.rank;
            if (p.score > 0f) return "OUTSIDE THE TOP 10";
            return boardCount == 0 ? "WORLD RECORDS · NONE YET" : "WORLD RECORDS";
        }

        /// <summary>Results-plate banner. Rank 1 is the record. Every other top-10 rank names its place. Outside the list, no banner.</summary>
        public static string ResultBanner(Place p)
        {
            if (!p.attempted || p.score <= 0f || !p.qualified || p.rank <= 0) return "";
            if (p.rank == 1) return "NEW WORLD RECORD";
            return "WORLD RECORDS · RANK " + p.rank;
        }

        /// <summary>Ranks 1–3 use the gold treatment. Ranks 4–10 still get a banner, without gold.</summary>
        public static bool MedalRank(int rank) => rank >= 1 && rank <= 3;

        public static string DisciplineName(Discipline d) => d == Discipline.Jump ? "JUMP" : d == Discipline.Trick ? "TRICKS" : "SLALOM";

        /// <summary>True when a should be listed above b. Equal scores: lower seq (earlier) wins.</summary>
        public static bool RanksAhead(Entry a, Entry b)
        {
            if (a == null) return false;
            if (b == null) return true;
            if (a.score > b.score) return true;
            if (a.score < b.score) return false;
            return a.seq < b.seq;
        }

        static int Compare(Entry a, Entry b)
        {
            if (RanksAhead(a, b)) return -1;
            if (RanksAhead(b, a)) return 1;
            return 0;
        }

        static int NextSeq(List<Entry> board)
        {
            int m = 0;
            if (board != null) for (int i = 0; i < board.Count; i++) if (board[i] != null && board[i].seq > m) m = board[i].seq;
            return m + 1;
        }

        static void Save(Discipline d, List<Entry> board)
        {
            var bag = new SaveBag { entries = board.ToArray() };
            Write(Key(d), JsonUtility.ToJson(bag));
        }

        static string Key(Discipline d) => PrefPrefix + (d == Discipline.Jump ? "jump" : d == Discipline.Trick ? "trick" : "slalom");

        static string Read(string key)
        {
            if (memory != null) return memory.TryGetValue(key, out var v) ? v : "";
            return PlayerPrefs.GetString(key, "");
        }

        static float ReadFloat(string key)
        {
            if (memory != null)
            {
                if (memory.TryGetValue(key, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return v;
                return -1f;
            }
            return PlayerPrefs.GetFloat(key, -1f);
        }

        static void Write(string key, string value)
        {
            if (memory != null) { memory[key] = value ?? ""; return; }
            PlayerPrefs.SetString(key, value ?? "");
            PlayerPrefs.Save();
        }
    }
}
