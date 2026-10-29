namespace SkiView
{
    public partial class SkiViewGame
    {
        /// <summary>Last world-record submission this session (slalom pass, jump round, or trick round).</summary>
        public SkiViewRecords.Place LastRecord { get; private set; }

        void NoteSlalomRecord(SkierSim.PassResult res)
        {
            if (jumpMode || trickMode || SkiViewClinic.Active || TutorialBlocksRecords()) return;
            // A pass with no buoys is not a slalom score. rope*6 + 0 would collide with a full pass at the previous rope.
            if (res.buoys <= 0.01f)
            {
                LastRecord = SkiViewRecords.Miss(SkiViewRecords.Discipline.Slalom);
                return;
            }
            float score = res.rope * 6f + res.buoys;
            string detail = lakeName + " · " + UI.SkiViewUI.SpeedMph(RunMph);
            LastRecord = SkiViewRecords.Submit(SkiViewRecords.Discipline.Slalom, score, detail, res.rope, res.buoys);
        }

        void NoteJumpRecord()
        {
            if (TutorialBlocksRecords())
            {
                LastRecord = SkiViewRecords.Miss(SkiViewRecords.Discipline.Jump);
                return;
            }
            float mph = jumpBoatKmh / 1.609344f;
            string detail = $"{jumpBoatKmh:0} km/h · {mph:0.0} mph · {rampHeight:0.00} m ramp · 23 m rope";
            LastRecord = SkiViewRecords.Submit(SkiViewRecords.Discipline.Jump, JumpBest, detail);
        }

        void NoteTrickRecord()
        {
            if (TutorialBlocksRecords())
            {
                LastRecord = SkiViewRecords.Miss(SkiViewRecords.Discipline.Trick);
                return;
            }
            string detail = $"{trickBoatKmh:0} km/h · {trickRopeM:0.0} m" + (TrickRoundOver ? " · round over" : " · 2 passes");
            LastRecord = SkiViewRecords.Submit(SkiViewRecords.Discipline.Trick, TrickScore, detail);
        }

        static bool TutorialBlocksRecords()
        {
            var ui = UI.SkiViewUI.Instance;
            return ui != null && ui.TutorialRunning;
        }
    }
}
