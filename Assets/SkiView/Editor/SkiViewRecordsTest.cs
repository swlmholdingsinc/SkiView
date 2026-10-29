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
        try
        {
            var slalom = SkiViewRecords.Discipline.Slalom;
            Check(SkiViewRecords.Load(slalom).Count == 0, "empty slalom board");

            var first = SkiViewRecords.Submit(slalom, 6f, "Ski View · 34 mph", 0, 6f, "SCOTT", 1, 638000000000000000L);
            Check(first.qualified && first.rank == 1 && first.count == 1, "first score takes rank 1");
            var loaded = SkiViewRecords.Load(slalom);
            Check(loaded.Count == 1 && loaded[0].holder == "SCOTT" && loaded[0].rope == 0 && Mathf.Approximately(loaded[0].buoys, 6f) && loaded[0].seq == 1, "round-trip holder, rope, buoys, seq");
            Check(loaded[0].achieved == 638000000000000000L.ToString(CultureInfo.InvariantCulture), "achieved ticks survive json");
            Check(SkiViewRecords.Format(slalom, loaded[0]) == "6 @ 15 OFF", "slalom format uses rope and buoys");

            var blank = SkiViewRecords.Submit(slalom, 8f, "Cedar Bend", 1, 2f, "   ", 2, 638000000000000100L);
            Check(blank.qualified && blank.rank == 1, "higher rope score ranks first");
            Check(SkiViewRecords.Load(slalom)[0].holder == "SCOTT", "blank holder falls back to SCOTT");
            Check(SkiViewRecords.Format(slalom, SkiViewRecords.Load(slalom)[0]) == "2 @ 22 OFF", "2 @ 22 OFF");

            var tie = SkiViewRecords.Submit(slalom, 8f, "later tie", 1, 2f, "SCOTT", 3, 638000000000000200L);
            Check(tie.qualified && tie.rank == 2, "equal score on a short list ranks after the earlier run");
            loaded = SkiViewRecords.Load(slalom);
            Check(loaded[0].seq == 2 && loaded[1].seq == 3, "earlier sequence stays ahead of the tie");

            var assigned = SkiViewRecords.Submit(slalom, 8f, "assigned seq", 1, 2f, "SCOTT", 0, 638000000000000300L);
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

            var jump = SkiViewRecords.Submit(SkiViewRecords.Discipline.Jump, 32.5f, "54 km/h", holder: "SCOTT", seq: 1, ticks: 638000000000000000L);
            Check(jump.qualified && jump.rank == 1, "jump board is separate");
            Check(SkiViewRecords.Load(slalom).Count >= 1 && SkiViewRecords.Load(SkiViewRecords.Discipline.Jump).Count == 1, "jump submit does not clear slalom");
            var trick = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, 0f, "no tricks", holder: "SCOTT", seq: 1);
            Check(!trick.qualified && SkiViewRecords.Load(SkiViewRecords.Discipline.Trick).Count == 0, "zero trick points are not stored");
            var trickOk = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, 420f, "31 km/h · 12.5 m · 2 passes", holder: "SCOTT", seq: 2);
            Check(trickOk.qualified && SkiViewRecords.Format(SkiViewRecords.Discipline.Trick, SkiViewRecords.Load(SkiViewRecords.Discipline.Trick)[0]) == "420 PTS", "trick points format");

            var miss = SkiViewRecords.Miss(slalom);
            Check(SkiViewRecords.PlacementHeading(miss, 0) == "WORLD RECORDS · NONE YET", "empty miss heading");
            Check(SkiViewRecords.PlacementHeading(miss, 3) == "WORLD RECORDS", "miss with a board still shows the list heading");
            Check(SkiViewRecords.PlacementHeading(topTie, 10) == "WORLD RECORDS · RANK 2", "qualifying heading uses rank");
            var wr = topTie; wr.rank = 1;
            Check(SkiViewRecords.PlacementHeading(wr, 10) == "NEW WORLD RECORD · RANK 1", "rank 1 heading");
            Check(SkiViewRecords.PlacementHeading(cut, 10) == "OUTSIDE THE TOP 10", "cutoff heading");

            for (int n = 0; n < 15; n++)
                SkiViewRecords.Submit(SkiViewRecords.Discipline.Jump, 40f + n, "ramp", holder: "SCOTT", seq: 10 + n);
            var jumpBoard = SkiViewRecords.Load(SkiViewRecords.Discipline.Jump);
            Check(jumpBoard.Count == SkiViewRecords.Capacity && jumpBoard[0].score > jumpBoard[9].score, "persisted jump board keeps 10 and stays ranked");
            Check(!ContainsScore(jumpBoard, 32.5f), "an older lower jump drops off a full board");
        }
        finally { SkiViewRecords.UseMemoryStore(false); }

        Debug.Log($"[RecordsTest] {fails} failure(s)");
        if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(fails == 0 ? 0 : 3);
    }

    static bool ContainsScore(List<SkiViewRecords.Entry> board, float score)
    {
        for (int i = 0; i < board.Count; i++) if (Mathf.Approximately(board[i].score, score)) return true;
        return false;
    }
}
