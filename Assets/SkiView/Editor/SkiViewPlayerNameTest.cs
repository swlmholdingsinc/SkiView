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

        Check(SkiViewPlayer.TryNormalize("scott", out var n) && n == "scott", "keeps typed case");
        Check(SkiViewPlayer.TryNormalize("  Jo ", out n) && n == "Jo", "trim keeps case");
        Check(SkiViewPlayer.TryNormalize("A", out n) && n == "A", "one character");
        Check(SkiViewPlayer.TryNormalize("ABCDEFGHIJKL", out n) && n == "ABCDEFGHIJKL", "twelve characters");
        Check(!SkiViewPlayer.TryNormalize("ABCDEFGHIJKLM", out _), "thirteen rejected");
        Check(!SkiViewPlayer.TryNormalize("   ", out _), "blank rejected");
        Check(!SkiViewPlayer.TryNormalize("", out _), "empty rejected");
        Check(!SkiViewPlayer.TryNormalize(null, out _), "null rejected");
        Check(SkiViewPlayer.TryNormalize("Ann-Marie", out n) && n == "Ann-Marie", "hyphen");
        Check(SkiViewPlayer.TryNormalize("O'Neil", out n) && n == "O'Neil", "apostrophe");
        Check(SkiViewPlayer.TryNormalize("O\u2019Neil", out n) && n == "O\u2019Neil", "curly apostrophe kept");
        Check(SkiViewPlayer.TryNormalize("Sam 2", out n) && n == "Sam 2", "digit and space");
        Check(SkiViewPlayer.TryNormalize("e\u0301lodie", out n) && n == "\u00e9lodie", "nfc combining acute");
        Check(!SkiViewPlayer.TryNormalize("Sam!", out _), "other punctuation rejected");
        Check(!SkiViewPlayer.TryNormalize("A\U0001F600", out _), "emoji rejected");
        Check(!SkiViewPlayer.TryNormalize("A\uD83D", out _), "surrogate rejected");
        Check(!SkiViewPlayer.TryNormalize("Wade", out _), "Wade rejected");
        Check(!SkiViewPlayer.TryNormalize("wade cox", out _), "Wade Cox rejected");
        Check(!SkiViewPlayer.TryNormalize("W4DE", out _), "leetspeak W4DE rejected");
        Check(!SkiViewPlayer.TryNormalize("W4D3", out _), "leetspeak W4D3 rejected");
        Check(!SkiViewPlayer.TryNormalize("wa de", out _), "spaced wade rejected");
        Check(!SkiViewPlayer.TryNormalize("W\u00e1de", out _), "accented wade rejected");
        Check(!SkiViewPlayer.TryNormalize("F3LD3RPU55", out _), "leetspeak Felderpuss rejected");
        Check(!SkiViewPlayer.TryNormalize("Felderpuss", out _), "Felderpuss rejected");
        Check(!SkiViewPlayer.TryNormalize("fel derpuss", out _), "split Felderpuss rejected");
        Check(SkiViewPlayer.TryNormalize("Scott", out n) && n == "Scott", "Scott allowed when typed");

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
            Check(PlayerPrefs.GetString(SkiViewPlayer.PrefKey, "") == "ada" && SkiViewPlayer.Name == "ada", "persisted trimmed case");
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
