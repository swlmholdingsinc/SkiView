using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using SkiView;

/// <summary>World-record top-10 rules. Run: unity_run.sh SkiViewRecordsTest.Run</summary>
public static class SkiViewRecordsTest
{
    public static void Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { Debug.Log($"[RecordsTest] {(ok ? "PASS" : "FAIL")} {what}"); if (!ok) fails++; }
        SkiViewRecords.UseMemoryStore(true);
        bool hadPlayer = PlayerPrefs.HasKey(SkiViewRecords.PlayerNamePref);
        string prevPlayer = PlayerPrefs.GetString(SkiViewRecords.PlayerNamePref, "");
        PlayerPrefs.DeleteKey(SkiViewRecords.PlayerNamePref);
        try
        {
            var slalom = SkiViewRecords.Discipline.Slalom;
            Check(SkiViewRecords.Load(slalom).Count == 0, "empty slalom board");

            var first = SkiViewRecords.Submit(slalom, 6f, "Ski View · 34 mph", 0, 6f, 1, 638000000000000000L);
            Check(first.qualified && first.rank == 1 && first.count == 1, "first score takes rank 1");
            var loaded = SkiViewRecords.Load(slalom);
            Check(loaded.Count == 1 && loaded[0].holder == "PLAYER" && loaded[0].rope == 0 && Mathf.Approximately(loaded[0].buoys, 6f) && loaded[0].seq == 1, "round-trip holder, rope, buoys, seq");
            Check(loaded[0].achieved == 638000000000000000L.ToString(CultureInfo.InvariantCulture), "achieved ticks survive json");
            Check(SkiViewRecords.Format(slalom, loaded[0]) == "6 @ 15 OFF", "slalom format uses rope and buoys");

            var blank = SkiViewRecords.Submit(slalom, 8f, "Cedar Bend", 1, 2f, 2, 638000000000000100L);
            Check(blank.qualified && blank.rank == 1, "higher rope score ranks first");
            Check(SkiViewRecords.Load(slalom)[0].holder == "PLAYER", "blank holder falls back to PLAYER, not a branding name");
            Check(SkiViewRecords.Format(slalom, SkiViewRecords.Load(slalom)[0]) == "2 @ 22 OFF", "2 @ 22 OFF");

            var tie = SkiViewRecords.Submit(slalom, 8f, "later tie", 1, 2f, 3, 638000000000000200L);
            Check(tie.qualified && tie.rank == 2, "equal score on a short list ranks after the earlier run");
            loaded = SkiViewRecords.Load(slalom);
            Check(loaded[0].seq == 2 && loaded[1].seq == 3, "earlier sequence stays ahead of the tie");

            var assigned = SkiViewRecords.Submit(slalom, 8f, "assigned seq", 1, 2f, 0, 638000000000000300L);
            Check(assigned.seq > 3 && assigned.rank == 3, "seq 0 is assigned after the current max and ranks after earlier ties");

            var board = SkiViewRecords.Load(slalom);
            var forced = new SkiViewRecords.Entry { holder = "SCOTT", score = 8f, detail = "forced earlier", seq = 1, achieved = "638000000000000010", rope = 1, buoys = 2f };
            var forcedPlace = SkiViewRecords.TryInsert(board, forced);
            Check(forcedPlace.qualified && forcedPlace.rank == 1 && board[0].seq == 1 && board[0].detail == "forced earlier", "lower seq inserts ahead of an equal score");

            var full = new List<SkiViewRecords.Entry>();
            for (int i = 1; i <= 10; i++)
                full.Add(new SkiViewRecords.Entry { holder = "SCOTT", score = i * 10f, seq = i, rope = 0, buoys = 1f, detail = "n" + i, achieved = "1" });
            full.Sort((a, b) => SkiViewRecords.RanksAhead(a, b) ? -1 : SkiViewRecords.RanksAhead(b, a) ? 1 : 0);
            Check(Mathf.Approximately(full[0].score, 100f) && Mathf.Approximately(full[9].score, 10f), "fixture ranked 100 down to 10");

            var cut = SkiViewRecords.TryInsert(full, new SkiViewRecords.Entry { holder = "SCOTT", score = 10f, seq = 11, rope = 0, buoys = 1f, detail = "tie-10th" });
            Check(!cut.qualified && full.Count == 10 && full[9].seq == 1, "tie with 10th does not enter and does not drop the earlier row");

            var made = SkiViewRecords.TryInsert(full, new SkiViewRecords.Entry { holder = "SCOTT", score = 15f, seq = 12, rope = 0, buoys = 1f, detail = "fifteen" });
            Check(made.qualified && made.rank == 10 && full.Count == 10 && Mathf.Approximately(full[9].score, 15f) && !ContainsScore(full, 10f), "score above 10th takes last place and drops the previous 10th");

            var topTie = SkiViewRecords.TryInsert(full, new SkiViewRecords.Entry { holder = "SCOTT", score = 100f, seq = 50, rope = 0, buoys = 1f, detail = "later hundred" });
            Check(topTie.qualified && topTie.rank == 2 && full[0].seq != 50 && full[1].detail == "later hundred" && full.Count == 10, "later tie with 1st ranks 2nd and the board stays at 10");

            int before = full.Count;
            var zero = SkiViewRecords.TryInsert(full, new SkiViewRecords.Entry { holder = "SCOTT", score = 0f, seq = 60 });
            var neg = SkiViewRecords.TryInsert(full, new SkiViewRecords.Entry { holder = "SCOTT", score = -4f, seq = 61 });
            var nan = SkiViewRecords.TryInsert(full, new SkiViewRecords.Entry { holder = "SCOTT", score = float.NaN, seq = 62 });
            Check(!zero.qualified && !neg.qualified && !nan.qualified && full.Count == before, "zero, negative, and NaN do not enter");

            var jump = SkiViewRecords.Submit(SkiViewRecords.Discipline.Jump, 32.5f, "54 km/h", seq: 1, ticks: 638000000000000000L);
            Check(jump.qualified && jump.rank == 1, "jump board is separate");
            Check(SkiViewRecords.Load(slalom).Count >= 1 && SkiViewRecords.Load(SkiViewRecords.Discipline.Jump).Count == 1, "jump submit does not clear slalom");
            var trick = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, 0f, "no tricks", seq: 1);
            Check(!trick.qualified && SkiViewRecords.Load(SkiViewRecords.Discipline.Trick).Count == 0, "zero trick points are not stored");
            var trickOk = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, 420f, "31 km/h · 12.5 m · 2 passes", seq: 2);
            Check(trickOk.qualified && SkiViewRecords.Format(SkiViewRecords.Discipline.Trick, SkiViewRecords.Load(SkiViewRecords.Discipline.Trick)[0]) == "420 PTS", "trick points format");

            var miss = SkiViewRecords.Miss(slalom);
            Check(SkiViewRecords.PlacementHeading(miss, 0) == "WORLD RECORDS · NONE YET", "empty miss heading");
            Check(SkiViewRecords.PlacementHeading(miss, 3) == "WORLD RECORDS", "miss with a board still shows the list heading");
            Check(SkiViewRecords.PlacementHeading(topTie, 10) == "WORLD RECORDS · RANK 2", "qualifying heading uses rank");
            var wr = topTie; wr.rank = 1;
            Check(SkiViewRecords.PlacementHeading(wr, 10) == "NEW WORLD RECORD · RANK 1", "rank 1 heading");
            Check(SkiViewRecords.PlacementHeading(cut, 10) == "OUTSIDE THE TOP 10", "cutoff heading");

            for (int n = 0; n < 15; n++)
                SkiViewRecords.Submit(SkiViewRecords.Discipline.Jump, 40f + n, "ramp", seq: 10 + n);
            var jumpBoard = SkiViewRecords.Load(SkiViewRecords.Discipline.Jump);
            Check(jumpBoard.Count == SkiViewRecords.Capacity && jumpBoard[0].score > jumpBoard[9].score, "persisted jump board keeps 10 and stays ranked");
            Check(!ContainsScore(jumpBoard, 32.5f), "an older lower jump drops off a full board");

            var open = new SkiViewRecords.SlalomSet();
            SkiViewRecords.SlalomSet done;
            int submits = 0;
            void Pass(int rope, float buoys, bool clean)
            {
                if (!SkiViewRecords.NoteSlalomSetPass(ref open, rope, buoys, clean, "Ski View", out done)) return;
                submits++;
                Check(submits == 1, "a set submits on the pass that ends it and not before");
            }
            Pass(0, 6f, true);
            Pass(1, 6f, true);
            Check(submits == 0 && open.has && open.rope == 1 && Mathf.Approximately(open.buoys, 6f), "full passes stay in the set until it ends");
            Pass(2, 2f, false);
            Check(submits == 1 && done.has && done.rope == 2 && Mathf.Approximately(done.buoys, 2f) && !open.has, "a miss ends the set once, on the furthest pass that scored");

            open = default;
            Check(!SkiViewRecords.NoteSlalomSetPass(ref open, 0, 6f, true, "lake", out done), "a lone full pass does not submit yet");
            Check(SkiViewRecords.NoteSlalomSetPass(ref open, 1, 0f, false, "lake", out done) && done.has && done.rope == 0 && Mathf.Approximately(done.buoys, 6f), "a zero-buoy fall keeps the furthest pass that scored");
            Check(!open.has, "the set is clear after a fall or miss");

            open = default;
            Check(!SkiViewRecords.NoteSlalomSetPass(ref open, 0, 6f, true, "easy", out done), "easier full pass is only a candidate");
            Check(!SkiViewRecords.NoteSlalomSetPass(ref open, 4, 1f, true, "short", out done) && open.rope == 4 && Mathf.Approximately(open.buoys, 1f), "one buoy on a shorter rope is further than six on a long rope");
            Check(!SkiViewRecords.NoteSlalomSetPass(ref open, 1, 6f, true, "back", out done) && open.rope == 4, "a later pass on an easier rope does not replace the furthest pass");
            Check(!SkiViewRecords.NoteSlalomSetPass(ref open, 4, 2f, true, "same", out done) && Mathf.Approximately(open.buoys, 2f), "more buoys on that same rope reached further");
            Check(SkiViewRecords.NoteSlalomSetPass(ref open, 4, 1f, false, "less", out done) && done.rope == 4 && Mathf.Approximately(done.buoys, 2f), "a fall with fewer buoys at the same rope keeps the furthest pass");

            open = default;
            Check(SkiViewRecords.NoteSlalomSetPass(ref open, 0, 0f, false, "lake", out done) && !done.has, "a set with no scored pass submits nothing");
            open = default;
            Check(!SkiViewRecords.NoteSlalomSetPass(ref open, 0, 6f, true, "a", out done), "clean pass opens the set");
            Check(!SkiViewRecords.NoteSlalomSetPass(ref open, 3, 4f, true, "b", out done) && open.rope == 3, "a further full pass replaces the running pass without submitting");
            SkiViewRecords.SlalomSet flushed;
            Check(SkiViewRecords.TakeSlalomSet(ref open, out flushed) && flushed.rope == 3 && Mathf.Approximately(flushed.buoys, 4f) && !open.has, "quit or title records the furthest scored pass once");
            Check(!SkiViewRecords.TakeSlalomSet(ref open, out flushed), "a second quit or title does not record the set again");

            Check(!SkiViewRecords.AllowRecord(true, false), "clinic blocks records");
            Check(!SkiViewRecords.AllowRecord(false, true), "tutorial blocks records");
            Check(!SkiViewRecords.AllowRecord(true, true), "clinic and tutorial together block records");
            Check(SkiViewRecords.AllowRecord(false, false), "a normal run may record");

            Check(SkiViewRecords.HolderName() == SkiViewRecords.FallbackHolder, "missing sv.player.name falls back to PLAYER");
            Check(SkiViewRecords.HolderName() != SkiViewBranding.SkierName, "the fallback is not the branding.json skier name");
            var unnamed = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, 15f, "no name", seq: 50);
            Check(unnamed.qualified && HolderOf(SkiViewRecords.Load(SkiViewRecords.Discipline.Trick), 50) == "PLAYER", "a record written with no saved name stores PLAYER");
            Check(!BoardHasHolder(SkiViewRecords.Discipline.Slalom, "SCOTT") && !BoardHasHolder(SkiViewRecords.Discipline.Jump, "SCOTT") && !BoardHasHolder(SkiViewRecords.Discipline.Trick, "SCOTT"), "boards do not contain SCOTT when the player did not type it");

            var legacy = new SkiViewRecords.SaveBag
            {
                entries = new[]
                {
                    new SkiViewRecords.Entry { holder = "", score = 3f, seq = 1, rope = 0, buoys = 3f, detail = "blank", achieved = "1" },
                    new SkiViewRecords.Entry { holder = "SCOTT", score = 4f, seq = 2, rope = 0, buoys = 4f, detail = "legacy", achieved = "1" },
                    new SkiViewRecords.Entry { holder = "Scott", score = 4.5f, seq = 4, rope = 0, buoys = 4.5f, detail = "legacy-case", achieved = "1" },
                    new SkiViewRecords.Entry { holder = "CASEY", score = 5f, seq = 3, rope = 0, buoys = 5f, detail = "kept", achieved = "1" },
                }
            };
            SkiViewRecords.SetRawForTests(SkiViewRecords.Discipline.Slalom, JsonUtility.ToJson(legacy));
            var migrated = SkiViewRecords.Load(SkiViewRecords.Discipline.Slalom);
            Check(HolderOf(migrated, 1) == "PLAYER", "a blank old row displays as PLAYER");
            Check(HolderOf(migrated, 2) == "PLAYER" && HolderOf(migrated, 4) == "PLAYER", "an old branding holder displays as PLAYER");
            Check(HolderOf(migrated, 3) == "CASEY", "a real saved holder is left as written");
            Check(!BoardHasHolder(migrated, "SCOTT"), "loading old rows does not surface SCOTT");

            PlayerPrefs.SetString(SkiViewRecords.PlayerNamePref, "SCOTT");
            PlayerPrefs.Save();
            var typed = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, 17f, "typed", seq: 60);
            Check(SkiViewRecords.HolderName() == "SCOTT" && typed.qualified && HolderOf(SkiViewRecords.Load(SkiViewRecords.Discipline.Trick), 60) == "SCOTT", "SCOTT is stored only when the player saved that name");
            SkiViewRecords.SetRawForTests(SkiViewRecords.Discipline.Slalom, JsonUtility.ToJson(legacy));
            Check(HolderOf(SkiViewRecords.Load(SkiViewRecords.Discipline.Slalom), 2) == "SCOTT", "a SCOTT row stays when that is the saved player name");

            PlayerPrefs.SetString(SkiViewRecords.PlayerNamePref, "  CASEY  ");
            PlayerPrefs.Save();
            Check(SkiViewRecords.HolderName() == "CASEY", "sv.player.name is trimmed");
            var named = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, 16f, "named", seq: 51);
            Check(named.qualified && HolderOf(SkiViewRecords.Load(SkiViewRecords.Discipline.Trick), 51) == "CASEY", "a new record stamps the trimmed player name");
            SkiViewRecords.SetRawForTests(SkiViewRecords.Discipline.Slalom, JsonUtility.ToJson(legacy));
            var renamed = SkiViewRecords.Load(SkiViewRecords.Discipline.Slalom);
            Check(HolderOf(renamed, 2) == "CASEY" && HolderOf(renamed, 4) == "CASEY" && HolderOf(renamed, 3) == "CASEY", "old SCOTT rows follow the saved player name");
            Check(!BoardHasHolder(renamed, "SCOTT"), "after the name changes, loaded rows do not contain an untyped SCOTT");
            Check(HolderOf(SkiViewRecords.Load(SkiViewRecords.Discipline.Trick), 60) == "CASEY", "a stored SCOTT row displays as the saved player name");
        }
        finally
        {
            SkiViewRecords.UseMemoryStore(false);
            if (hadPlayer) PlayerPrefs.SetString(SkiViewRecords.PlayerNamePref, prevPlayer);
            else PlayerPrefs.DeleteKey(SkiViewRecords.PlayerNamePref);
            PlayerPrefs.Save();
        }

        Debug.Log($"[RecordsTest] {fails} failure(s)");
        if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(fails == 0 ? 0 : 3);
    }

    static bool ContainsScore(List<SkiViewRecords.Entry> board, float score)
    {
        for (int i = 0; i < board.Count; i++) if (Mathf.Approximately(board[i].score, score)) return true;
        return false;
    }

    static string HolderOf(List<SkiViewRecords.Entry> board, int seq)
    {
        for (int i = 0; i < board.Count; i++) if (board[i].seq == seq) return board[i].holder;
        return "";
    }

    static bool BoardHasHolder(List<SkiViewRecords.Entry> board, string holder)
    {
        for (int i = 0; i < board.Count; i++)
            if (string.Equals(board[i].holder, holder, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static bool BoardHasHolder(SkiViewRecords.Discipline d, string holder) => BoardHasHolder(SkiViewRecords.Load(d), holder);
}
