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
    /// Slalom records one score per set: the furthest pass that scored, not one row per pass.
    /// The holder is always <see cref="HolderName"/>: the saved player name, or PLAYER when that is empty.
    /// Branding is not a name source. Old rows with a blank holder, or the old branding name, display as <see cref="HolderName"/>.
    /// A submit with no valid name stays pending until the name screen saves one, so imported rows are not rewritten to PLAYER first.
    /// </summary>
    public static class SkiViewRecords
    {
        public const int Capacity = 10;
        public const string PrefPrefix = "sv.records.v1.";
        /// <summary>Saved on first launch by the player-name prompt. Records read it at write time.</summary>
        public const string PlayerNamePref = "sv.player.name";
        public const string FallbackHolder = "PLAYER";
        const string PendingPref = "sv.records.v1.pending";

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

        [Serializable]
        class PendingBag { public int[] discipline; public Entry[] entries; }

        public struct Place
        {
            public bool attempted;
            public bool qualified;
            /// <summary>True when the row is waiting for a saved player name and is not on the board yet.</summary>
            public bool pending;
            public int rank;
            public int count;
            public int seq;
            public float score;
            public Discipline discipline;
        }

        struct PendingRow
        {
            public Discipline discipline;
            public Entry entry;
        }

        static Dictionary<string, string> memory;
        static readonly List<PendingRow> pending = new List<PendingRow>();
        static bool holdUnnamed;

        /// <summary>Test hook. When on, boards stay in memory and PlayerPrefs is left alone. Pass false to restore PlayerPrefs.</summary>
        public static void UseMemoryStore(bool on)
        {
            memory = on ? new Dictionary<string, string>() : null;
            pending.Clear();
            holdUnnamed = false;
        }

        /// <summary>Test hook. A submit with no valid name stays pending instead of storing PLAYER. Pass false to restore the capture/editor path.</summary>
        public static void HoldUnnamedForTests(bool on) { holdUnnamed = on; if (!on) pending.Clear(); }

        public static bool HasPending { get { LoadPending(); return pending.Count > 0; } }

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
        /// A fall or miss (not clean) ends the set: <paramref name="submit"/> is the furthest pass that scored, and the set is cleared.
        /// A pass with no buoys is not that pass. Returns true when the set ended.
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
        /// Saved player name from <see cref="SkiViewPlayer.Name"/> (PlayerPrefs <see cref="PlayerNamePref"/>, validated).
        /// Empty, missing, or rejected names become <see cref="FallbackHolder"/>.
        /// </summary>
        public static string HolderName()
        {
            string n = SkiViewPlayer.Name;
            return string.IsNullOrEmpty(n) ? FallbackHolder : n;
        }

        /// <summary>A playing release build with no valid name must not stamp PLAYER or rewrite imported rows. Captures and editor tests still store PLAYER.</summary>
        static bool HoldUnnamedSubmit =>
            string.IsNullOrEmpty(SkiViewPlayer.Name)
            && (holdUnnamed || (Application.isPlaying && !SkiViewPlayer.CaptureExempt));

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

        public static Place Submit(Discipline d, float score, string detail, int rope = -1, float buoys = 0f, int seq = 0, long ticks = 0)
        {
            if (float.IsNaN(score) || float.IsInfinity(score) || score <= 0f)
                return new Place { attempted = true, qualified = false, discipline = d, score = !float.IsNaN(score) && !float.IsInfinity(score) ? score : 0f };

            if (HoldUnnamedSubmit)
            {
                var existing = Load(d);
                if (seq <= 0) seq = NextSeq(existing);
                if (ticks <= 0) ticks = DateTime.UtcNow.Ticks;
                pending.Add(new PendingRow
                {
                    discipline = d,
                    entry = new Entry
                    {
                        holder = "",
                        score = score,
                        detail = detail ?? "",
                        seq = seq,
                        achieved = ticks.ToString(CultureInfo.InvariantCulture),
                        rope = rope,
                        buoys = buoys
                    }
                });
                PersistPending();
                return new Place { attempted = true, pending = true, qualified = false, count = existing.Count, seq = seq, score = score, discipline = d };
            }

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

        /// <summary>Write rows that were held for the name screen. Does nothing until <see cref="SkiViewPlayer.Name"/> is valid. The last written place is returned.</summary>
        public static Place CommitPending()
        {
            LoadPending();
            if (pending.Count == 0 || string.IsNullOrEmpty(SkiViewPlayer.Name)) return default;
            var batch = pending.ToArray();
            pending.Clear();
            PersistPending();
            Place last = default;
            for (int i = 0; i < batch.Length; i++)
            {
                var item = batch[i];
                if (item.entry == null) continue;
                item.entry.holder = HolderName();
                var board = Load(item.discipline);
                var place = TryInsert(board, item.entry);
                place.discipline = item.discipline;
                if (place.qualified) Save(item.discipline, board);
                last = place;
            }
            return last;
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

        static void LoadPending()
        {
            if (memory != null || pending.Count > 0) return;
            string json = PlayerPrefs.GetString(PendingPref, "");
            if (string.IsNullOrEmpty(json)) return;
            PendingBag bag = null;
            try { bag = JsonUtility.FromJson<PendingBag>(json); }
            catch (Exception e) { Debug.LogWarning("[SkiView] records pending: " + e.Message); }
            if (bag?.entries == null || bag.discipline == null) return;
            int n = Math.Min(bag.entries.Length, bag.discipline.Length);
            for (int i = 0; i < n; i++)
            {
                var entry = bag.entries[i];
                if (entry == null || float.IsNaN(entry.score) || float.IsInfinity(entry.score) || entry.score <= 0f) continue;
                if (bag.discipline[i] < 0 || bag.discipline[i] > (int)Discipline.Trick) continue;
                pending.Add(new PendingRow { discipline = (Discipline)bag.discipline[i], entry = entry });
            }
        }

        static void PersistPending()
        {
            if (memory != null) return;
            if (pending.Count == 0)
            {
                if (!PlayerPrefs.HasKey(PendingPref)) return;
                PlayerPrefs.DeleteKey(PendingPref);
                PlayerPrefs.Save();
                return;
            }
            var bag = new PendingBag { discipline = new int[pending.Count], entries = new Entry[pending.Count] };
            for (int i = 0; i < pending.Count; i++)
            {
                bag.discipline[i] = (int)pending[i].discipline;
                bag.entries[i] = pending[i].entry;
            }
            PlayerPrefs.SetString(PendingPref, JsonUtility.ToJson(bag));
            PlayerPrefs.Save();
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

        static void Write(string key, string value)
        {
            if (memory != null) { memory[key] = value ?? ""; return; }
            PlayerPrefs.SetString(key, value ?? "");
            PlayerPrefs.Save();
        }
    }
}
