namespace SkiView.Logic
{
    /// <summary>Runs without Unity. Returns the number of failed checks.</summary>
    public static class PassRulesHarness
    {
        public static int Run(System.Action<string> fail)
        {
            int n = 0;
            void F(bool ok, string m) { if (!ok) { n++; if (fail != null) fail(m); } }

            F(PassRestart.Choose(false, true) == PauseRestart.TrickPass, "TR-07 trick pause restart is the trick pass");
            F(PassRestart.Choose(false, false) == PauseRestart.RetryPass, "slalom pause restart stays on RetryPass");
            F(PassRestart.Choose(true, false) == PauseRestart.RetryPass, "jump pause restart stays on RetryPass");
            F(PassRestart.Choose(true, true) == PauseRestart.RetryPass, "jump wins when both flags are set, so the jump patch still runs");

            int pass1 = PassRestart.TrickPassDirection(1, -1);
            int pass2 = PassRestart.TrickPassDirection(2, -1);
            F(pass1 == -1, "trick pass 1 keeps the Ski View start direction");
            F(pass2 == 1, "trick pass 2 is the opposite of the start direction");
            F(pass1 != PassRestart.SlalomSetupDirection(pass1), "restarting pass 1 does not use the slalom flip");
            F(PassRestart.SlalomSetupDirection(pass2) == -1, "once pass 2 is already heading its own way, the slalom flip would reverse it");
            F(PassRestart.SlalomSetupDirection(-1) == 1 && PassRestart.SlalomSetupDirection(1) == -1 && PassRestart.SlalomSetupDirection(0) == 1, "slalom flip itself is unchanged");
            F(PassRestart.TrickPassDirection(1, 1) == 1 && PassRestart.TrickPassDirection(2, 1) == -1, "a southbound start still swaps only on pass 2");

            F(PassRestart.TrickPassProgress(1, -232f, 115f) == -232f, "pass 1 restarts from the round start");
            float pass2At = PassRestart.TrickPassProgress(2, -232f, -115f);
            F(pass2At == -195f, "pass 2 restarts 80 m short of its entry");
            F(pass2At < -115f - 5f, "pass 2 starts before the arm window");

            F(SlalomBest.FormatPass(8, 0f) == "0 @ 43 OFF", "0 buoys at 43 OFF displays as 0 @ 43 OFF");
            F(SlalomBest.FormatPass(7, 6f) == "6 @ 41 OFF", "6 buoys at 41 OFF stays 6 @ 41 OFF");
            F(SlalomBest.FormatPass(8, 3.5f) == "3.5 @ 43 OFF", "3.5 at 43 OFF");
            F(SlalomBest.FormatPass(2, 6f) == "6 @ 28 OFF", "6 at 28 OFF");
            F(SlalomBest.Pack(8, 0f) == 48f && SlalomBest.Pack(7, 6f) == 48f, "packed 0 @ 43 and 6 @ 41 are the same number");
            F(SlalomBest.FormatPacked(48f) == "6 @ 41 OFF", "a legacy packed 48 still reads as the full pass at 41 OFF");
            F(SlalomBest.FormatPacked(18f) == "6 @ 28 OFF", "packed 18 is 6 @ 28 OFF");
            F(SlalomBest.FormatPacked(51.5f) == "3.5 @ 43 OFF", "packed 51.5 is 3.5 @ 43 OFF");
            F(SlalomBest.FormatPacked(-1f) == "—", "no saved best stays the em dash");
            F(SlalomBest.FormatPacked(54f) == "6 @ 43 OFF", "a full pass at 43 OFF still fits the last rope");
            F(SlalomBest.FormatPacked(0f) == "0 @ 15 OFF", "zero at the longest rope is 0 @ 15 OFF");
            F(SlalomBest.Display(48f, true, 8, 0f) == "0 @ 43 OFF", "a saved pass of 0 at 43 OFF is not shown as 6 @ 41 OFF");
            F(SlalomBest.Display(48f, true, 7, 6f) == "6 @ 41 OFF", "a saved full pass at 41 OFF keeps its own rope");
            F(SlalomBest.Display(48f, false, 0, 0f) == "6 @ 41 OFF", "without the pass fields, packed 48 keeps the old reading");
            F(SlalomBest.IsBetter(-1f, 8, 0f), "a first pass of 0 at 43 OFF is a best");
            F(!SlalomBest.IsBetter(48f, 8, 0f), "0 at 43 OFF does not replace a packed 48");
            F(SlalomBest.IsBetter(47.5f, 8, 0.5f) && SlalomBest.FormatPass(8, 0.5f) == "0.5 @ 43 OFF", "half a buoy at 43 OFF");

            return n;
        }
    }
}
