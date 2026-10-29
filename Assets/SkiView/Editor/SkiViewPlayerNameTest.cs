using UnityEditor;
using UnityEngine;
using SkiView;

/// <summary>Name validation and PlayerPrefs round-trip. Run: unity_run.sh SkiViewPlayerNameTest.Run</summary>
public static class SkiViewPlayerNameTest
{
    public static void Run()
    {
        int fails = 0;
        void Check(bool ok, string what)
        {
            Debug.Log($"[NameTest] {(ok ? "PASS" : "FAIL")} {what}");
            if (!ok) fails++;
        }

        Check(SkiViewPlayer.TryNormalize("scott", out var n) && n == "SCOTT", "upper-case display");
        Check(SkiViewPlayer.TryNormalize("  Jo ", out n) && n == "JO", "trim");
        Check(SkiViewPlayer.TryNormalize("A", out n) && n == "A", "one character");
        Check(SkiViewPlayer.TryNormalize("ABCDEFGHIJKL", out n) && n == "ABCDEFGHIJKL", "twelve characters");
        Check(!SkiViewPlayer.TryNormalize("ABCDEFGHIJKLM", out _), "thirteen rejected");
        Check(!SkiViewPlayer.TryNormalize("   ", out _), "blank rejected");
        Check(!SkiViewPlayer.TryNormalize("", out _), "empty rejected");
        Check(!SkiViewPlayer.TryNormalize(null, out _), "null rejected");
        Check(SkiViewPlayer.TryNormalize("Ann-Marie", out n) && n == "ANN-MARIE", "hyphen");
        Check(SkiViewPlayer.TryNormalize("O'Neil", out n) && n == "O'NEIL", "apostrophe");
        Check(SkiViewPlayer.TryNormalize("O\u2019Neil", out n) && n == "O'NEIL", "curly apostrophe");
        Check(SkiViewPlayer.TryNormalize("Sam 2", out n) && n == "SAM 2", "digit and space");
        Check(!SkiViewPlayer.TryNormalize("Sam!", out _), "other punctuation rejected");
        Check(!SkiViewPlayer.TryNormalize("Wade", out _), "Wade rejected");
        Check(!SkiViewPlayer.TryNormalize("wade cox", out _), "Wade Cox rejected");
        Check(!SkiViewPlayer.TryNormalize("Felderpuss", out _), "Felderpuss rejected");
        Check(!SkiViewPlayer.TryNormalize("fel derpuss", out _), "split Felderpuss rejected");
        Check(SkiViewPlayer.TryNormalize("SCOTT", out n) && n == "SCOTT", "Scott allowed when typed");

        bool had = PlayerPrefs.HasKey(SkiViewPlayer.PrefKey);
        string prev = had ? PlayerPrefs.GetString(SkiViewPlayer.PrefKey, "") : null;
        try
        {
            PlayerPrefs.DeleteKey(SkiViewPlayer.PrefKey);
            PlayerPrefs.Save();
            Check(!SkiViewPlayer.HasName && SkiViewPlayer.Name == null, "missing name is not invented");
            Check(!SkiViewPlayer.TrySave("!!!"), "invalid save rejected");
            Check(!SkiViewPlayer.HasName, "invalid save does not persist");
            Check(SkiViewPlayer.TrySave("  ada "), "valid save");
            Check(PlayerPrefs.GetString(SkiViewPlayer.PrefKey, "") == "ADA" && SkiViewPlayer.Name == "ADA", "persisted upper-case");
            PlayerPrefs.SetString(SkiViewPlayer.PrefKey, "Wade");
            PlayerPrefs.Save();
            Check(!SkiViewPlayer.HasName && SkiViewPlayer.Name == null, "corrupt Wade is not shown");
            PlayerPrefs.SetString(SkiViewPlayer.PrefKey, "THIS NAME IS WAY TOO LONG");
            PlayerPrefs.Save();
            Check(!SkiViewPlayer.HasName, "corrupt length is not shown");
        }
        finally
        {
            if (had) PlayerPrefs.SetString(SkiViewPlayer.PrefKey, prev);
            else PlayerPrefs.DeleteKey(SkiViewPlayer.PrefKey);
            PlayerPrefs.Save();
        }

        Debug.Log($"[NameTest] {fails} failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
