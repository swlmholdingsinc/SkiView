using UnityEngine;

/// <summary>WebGL jump stall: a scripted cut mark of 0 (what a serialized NaN becomes in some player loads)
/// must still assist-cut and reach the ramp, and a 1 Hz frame clock must not drop the approach.
/// Run: unity_run.sh SkiViewJumpWebGLTest.Run</summary>
public static class SkiViewJumpWebGLTest
{
    const float DT = 1f / 60f;

    struct End { public bool cut, ramp, missed; public float runT; }

    static End Sim(float mark, float frameDt, float cap, int frames)
    {
        float runT = -10.6f, acc = 0f;
        bool armed = false, cut = false, ramp = false, missed = false;
        for (int f = 0; f < frames && !ramp; f++)
        {
            int n = SkiView.SkiViewGame.CountFixedSteps(ref acc, frameDt, 1f, cap, DT);
            for (int i = 0; i < n && !ramp; i++)
            {
                runT += DT;
                if (!cut && SkiView.SkiViewGame.WantJumpCut(ref armed, runT, mark, false, false, false)) cut = true;
                if (!cut && runT >= SkiView.SkiViewGame.CUT_LATE) missed = true;
                if (cut && !missed && runT >= 0f) ramp = true;
            }
        }
        return new End { cut = cut, ramp = ramp, missed = missed, runT = runT };
    }

    public static void Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { Debug.Log($"[JumpWebGL] {(ok ? "PASS" : "FAIL")} {what}"); if (!ok) fails++; }

        Check(!SkiView.SkiViewGame.JumpMarkOverrides(float.NaN), "NaN mark is not scripted");
        Check(!SkiView.SkiViewGame.JumpMarkOverrides(float.PositiveInfinity), "+Inf mark is not scripted");
        Check(!SkiView.SkiViewGame.JumpMarkOverrides(float.NegativeInfinity), "-Inf mark is not scripted");
        Check(!SkiView.SkiViewGame.JumpMarkOverrides(0f), "exact 0 mark is not scripted");
        Check(SkiView.SkiViewGame.JumpMarkOverrides(-4.3f), "capture cut -4.3 is scripted");
        Check(SkiView.SkiViewGame.JumpMarkOverrides(0.86f), "capture pop 0.86 is scripted");
        Check(SkiView.SkiViewGame.JumpMarkOverrides(99f), "no-cut capture 99 is scripted");

        bool armed = false;
        bool early = SkiView.SkiViewGame.WantJumpCut(ref armed, -8f, float.NaN, false, true, false);
        Check(!early && armed, "press before the window arms the cut");
        bool atAuto = SkiView.SkiViewGame.WantJumpCut(ref armed, -4.3f, float.NaN, false, false, false);
        Check(atAuto, "armed cut fires at -4.3");

        armed = false;
        bool assist = SkiView.SkiViewGame.WantJumpCut(ref armed, -3.9f, 0f, false, false, false);
        Check(assist && !armed, "mark 0 still assist-cuts at -3.9");
        bool before = SkiView.SkiViewGame.WantJumpCut(ref armed, -3.95f, 0f, false, false, false);
        Check(!before, "assist has not fired at -3.95");

        armed = true;
        bool held = SkiView.SkiViewGame.WantJumpCut(ref armed, -5f, -4.3f, false, false, false);
        Check(!held, "a real scripted cut time still waits for its own instant");

        var steady = Sim(0f, DT, SkiView.SkiViewGame.SimFrameCap, 800);
        Check(steady.cut && steady.ramp && !steady.missed, $"60 Hz, mark 0: cut and ramp (runT {steady.runT:0.00})");

        var nanMark = Sim(float.NaN, DT, SkiView.SkiViewGame.SimFrameCap, 800);
        Check(nanMark.cut && nanMark.ramp && !nanMark.missed, $"60 Hz, mark NaN: cut and ramp (runT {nanMark.runT:0.00})");

        var noCut = Sim(99f, DT, SkiView.SkiViewGame.SimFrameCap, 900);
        Check(!noCut.cut && noCut.missed && !noCut.ramp, $"scripted 99: miss, no ramp (runT {noCut.runT:0.00})");

        var throttled = Sim(0f, 1f, SkiView.SkiViewGame.SimFrameCap, 30);
        Check(throttled.cut && throttled.ramp && !throttled.missed, $"1 Hz frames, cap {SkiView.SkiViewGame.SimFrameCap}: ramp within 30 s (runT {throttled.runT:0.00})");

        var oldCap = Sim(0f, 1f, 0.1f, 40);
        Check(!oldCap.cut && !oldCap.ramp && !oldCap.missed, $"1 Hz frames, old 0.1 cap: still short of the cut after 40 s (runT {oldCap.runT:0.00})");

        float acc = 0.02f;
        int nanSteps = SkiView.SkiViewGame.CountFixedSteps(ref acc, float.NaN, 1f, SkiView.SkiViewGame.SimFrameCap, DT);
        Check(nanSteps >= 1 && !float.IsNaN(acc), $"NaN frame dt does not poison the accumulator (steps {nanSteps}, acc {acc})");
        int negSteps = SkiView.SkiViewGame.CountFixedSteps(ref acc, -1f, 1f, SkiView.SkiViewGame.SimFrameCap, DT);
        Check(negSteps == 0 && !float.IsNaN(acc), "negative frame dt adds no steps");
        float acc2 = 0f;
        int nanScale = SkiView.SkiViewGame.CountFixedSteps(ref acc2, 0.5f, float.NaN, SkiView.SkiViewGame.SimFrameCap, DT);
        Check(nanScale > 10 && !float.IsNaN(acc2), $"NaN time scale falls back to 1 (steps {nanScale})");

        Debug.Log($"[JumpWebGL] {fails} failure(s)");
        if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
