namespace SkiView
{
    public partial class SkiViewGame
    {
        /// <summary>Last world-record submission this session (slalom set, jump round, or trick round).</summary>
        public SkiViewRecords.Place LastRecord { get; private set; }

        SkiViewRecords.SlalomSet slalomSet;

        void NoteSlalomRecord(SkierSim.PassResult res)
        {
            if (jumpMode || trickMode || !RecordsAllowed()) return;
            string detail = lakeName + " · " + UI.SkiViewUI.SpeedMph(RunMph);
            if (!SkiViewRecords.NoteSlalomSetPass(ref slalomSet, res.rope, res.buoys, res.clean, detail, out var final))
            {
                LastRecord = default; // full pass: the set is still open, so this pass is not its own record
                return;
            }
            CommitSlalomSet(final);
        }

        /// <summary>Quit, retry, or return to title. One score: the furthest pass that scored.</summary>
        public void FlushSlalomSet()
        {
            if (!SkiViewRecords.TakeSlalomSet(ref slalomSet, out var final)) return;
            if (!RecordsAllowed()) return;
            CommitSlalomSet(final);
        }

        void OnApplicationQuit() { FlushSlalomSet(); }

        void CommitSlalomSet(SkiViewRecords.SlalomSet final)
        {
            if (!final.has || final.score <= 0f)
            {
                LastRecord = SkiViewRecords.Miss(SkiViewRecords.Discipline.Slalom);
                return;
            }
            LastRecord = SkiViewRecords.Submit(SkiViewRecords.Discipline.Slalom, final.score, final.detail, final.rope, final.buoys);
        }

        void NoteJumpRecord()
        {
            if (!RecordsAllowed()) return;
            float mph = jumpBoatKmh / 1.609344f;
            string detail = $"{jumpBoatKmh:0} km/h · {mph:0.0} mph · {rampHeight:0.00} m ramp · 23 m rope";
            LastRecord = SkiViewRecords.Submit(SkiViewRecords.Discipline.Jump, JumpBest, detail);
        }

        void NoteTrickRecord()
        {
            if (!RecordsAllowed()) return;
            string detail = $"{trickBoatKmh:0} km/h · {trickRopeM:0.0} m" + (TrickRoundOver ? " · round over" : " · 2 passes");
            LastRecord = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, TrickScore, detail);
        }

        static bool RecordsAllowed() => SkiViewRecords.AllowRecord(SkiViewClinic.Active, TutorialBlocksRecords());

        static bool TutorialBlocksRecords()
        {
            var ui = UI.SkiViewUI.Instance;
            return ui != null && ui.TutorialRunning;
        }
    }
}
