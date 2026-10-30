using UnityEngine;
using UnityEngine.UIElements;

namespace SkiView.UI
{
    /// <summary>World Records menu: one local top-10 per discipline, same plate and tab language as Settings.</summary>
    public partial class SkiViewUI
    {
        VisualElement recordsMenu, recordsBody, rsRecordBox, rsRecordLines;
        Label recordsCrumb, recordsRule, rsRecordHead;
        Button[] recordsTabs;
        int recordsTab;
        int recordListSeq = int.MinValue;
        bool recordListShown;
        SkiViewRecords.Discipline recordListDisc;

        void BuildRecords()
        {
            recordsMenu = Menu(out var safe, "TITLE", "WORLD RECORDS", NavBack);
            var header = safe.Q(className: "sv-header");
            if (header != null) recordsCrumb = header.Q<Label>(className: "t-micro");
            var body = El("sv-body", safe);
            var tabs = El("sv-tabs", body);
            recordsTabs = new Button[3];
            string[] names = { "SLALOM", "JUMP", "TRICKS" };
            for (int i = 0; i < names.Length; i++)
            {
                int k = i;
                recordsTabs[i] = Btn(names[i], "sv-tab", tabs, () => { recordsTab = k; FillRecords(); });
            }
            var plate = El("sv-plate sv-plate--card sv-panel", body);
            El("sv-strip", plate);
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; plate.Add(sv);
            recordsBody = sv.contentContainer;
            recordsRule = Lb("UP TO 10 SCORES. A HIGHER SCORE RANKS FIRST. THE SAME SCORE KEEPS THE EARLIER RUN AHEAD. A TIE WITH 10TH PLACE STAYS OFF THE LIST.", "t-caption", plate);
            recordsRule.style.whiteSpace = WhiteSpace.Normal;
            recordsRule.style.marginTop = 12;
            FillRecords();
        }

        public void OpenRecords(SkiViewRecords.Discipline d, string crumb = null)
        {
            if (recordsMenu == null) return;
            recordsTab = (int)d;
            if (recordsCrumb != null && !string.IsNullOrEmpty(crumb)) recordsCrumb.text = crumb;
            Open(recordsMenu);
        }

        void FillRecords()
        {
            if (recordsBody == null || recordsTabs == null) return;
            var d = (SkiViewRecords.Discipline)Mathf.Clamp(recordsTab, 0, 2);
            for (int i = 0; i < recordsTabs.Length; i++) recordsTabs[i].EnableInClassList("sv-tab--on", i == recordsTab);
            recordsBody.Clear();
            var board = SkiViewRecords.Load(d);
            if (board.Count == 0)
            {
                Lb("NO RECORDS YET", "t-h2", recordsBody);
                var empty = Lb("FINISH A SCORED " + SkiViewRecords.DisciplineName(d) + " RUN TO CLAIM A PLACE.", "t-caption", recordsBody);
                empty.style.whiteSpace = WhiteSpace.Normal;
                return;
            }
            for (int i = 0; i < board.Count; i++)
            {
                var e = board[i];
                var row = El("sv-rec-row", recordsBody);
                if (SkiViewRecords.IsGoldRank(i + 1)) row.AddToClassList("sv-rec-row--top");
                bool fresh = game != null && game.LastRecord.qualified && game.LastRecord.discipline == d && game.LastRecord.seq == e.seq;
                if (fresh) row.AddToClassList("sv-row--selected");
                Lb((i + 1).ToString(), "sv-rec-rank", row);
                var mid = El("sv-rec-mid", row);
                Lb(SkiViewRecords.ResolveHolder(e.holder), "t-label", mid);
                string sub = e.detail ?? "";
                string when = SkiViewRecords.AchievedLabel(e);
                if (!string.IsNullOrEmpty(when)) sub = string.IsNullOrEmpty(sub) ? when : sub + " · " + when;
                if (!string.IsNullOrEmpty(sub))
                {
                    var cap = Lb(sub, "t-caption", mid);
                    cap.style.whiteSpace = WhiteSpace.Normal;
                }
                Lb(SkiViewRecords.Format(d, e), "sv-rec-score", row);
            }
        }

        void OpenRecordsFromResults()
        {
            var d = SkiViewRecords.Discipline.Slalom;
            if (game != null && game.LastRecord.attempted) d = game.LastRecord.discipline;
            else if (game != null && game.trickMode) d = SkiViewRecords.Discipline.Trick;
            else if (game != null && game.jumpMode) d = SkiViewRecords.Discipline.Jump;
            if (results != null) results.style.display = DisplayStyle.None;
            titleReturn = false;
            pauseReturn = false;
            OpenRecords(d, "RESULTS");
        }

        void ShowRecordList(SkiViewRecords.Discipline d)
        {
            if (rsRecordBox == null || game == null) return;
            var p = game.LastRecord;
            if (!p.attempted || p.discipline != d)
            {
                rsRecordBox.style.display = DisplayStyle.None;
                recordListSeq = int.MinValue;
                recordListShown = false;
                return;
            }
            bool rebuild = !recordListShown || recordListSeq != p.seq || recordListDisc != d;
            if (rebuild)
            {
                var board = SkiViewRecords.Load(d);
                if (rsRecordHead != null) rsRecordHead.text = SkiViewRecords.PlacementHeading(p, board.Count);
                if (rsRecordLines != null)
                {
                    rsRecordLines.Clear();
                    if (board.Count == 0)
                    {
                        if (p.score > 0f) Lb("NO RECORDS YET", "t-caption sv-results19__recline", rsRecordLines);
                    }
                    else
                    {
                        for (int i = 0; i < board.Count; i++)
                        {
                            var e = board[i];
                            var line = Lb($"{i + 1}   {SkiViewRecords.ResolveHolder(e.holder)}   {SkiViewRecords.Format(d, e)}", "t-caption sv-results19__recline", rsRecordLines);
                            if (SkiViewRecords.IsGoldRank(i + 1)) line.AddToClassList("t-gold");
                        }
                    }
                }
                recordListSeq = p.seq;
                recordListDisc = d;
                recordListShown = true;
                rsRecordBox.style.display = DisplayStyle.Flex;
            }
            if (p.qualified && p.rank >= 1 && rsBanner != null && results != null)
            {
                bool gold = SkiViewRecords.IsGoldRank(p.rank);
                rsBanner.text = SkiViewRecords.BannerText(p);
                rsBanner.EnableInClassList("sv-results19__pb--plain", !gold);
                rsBanner.style.display = DisplayStyle.Flex;
                results.EnableInClassList("sv-results19--pb", gold);
            }
        }
    }
}
