using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gate regression: the r6b16 AAB shape (base + UnityDataAssetPack + catalog.hash only) fails,
/// and the r6b10 pack paths pass. Also round-trips those names through a zip central directory.
/// Run: unity_run.sh SkiViewAndroidPadGateTest.Run
/// </summary>
public static class SkiViewAndroidPadGateTest
{
    public static void Run()
    {
        int fails = SkiViewAndroidPadGate.CheckFixtures();
        Debug.Log($"[PadGate] fixture failures {fails}");
        string zip = Path.Combine(Path.GetTempPath(), "skiview-pad-gate.zip");
        try
        {
            WriteStoreZip(zip, new[]
            {
                "AddressablesAssetPack/assets/aa/catalog.bin",
                "AddressablesAssetPack/assets/aa/settings.json",
                "AddressablesAssetPack/assets/aa/CustomAssetPacksData.json",
                "AddressablesAssetPack/assets/aa/Android/core_home_scenes_all.bundle",
                "AddressablesAssetPack/assets/aa/Android/core_shared_assets_all.bundle",
                "UnityDataAssetPack/assets/aa/catalog.hash"
            });
            var names = SkiViewAndroidPadGate.ReadZipEntryNames(zip);
            var missing = SkiViewAndroidPadGate.MissingEntries(names);
            int lakes = 0;
            for (int i = 0; i < missing.Count; i++) if (missing[i].StartsWith("Lake_")) lakes++;
            if (lakes != SkiViewAndroidPadGate.LakeIds.Length || missing.Count != lakes)
            {
                Debug.LogError("[PadGate] FAIL zip round-trip missing=" + string.Join(", ", missing.ToArray()));
                fails++;
            }
            else Debug.Log("[PadGate] PASS zip round-trip kept catalog.bin and still requires the nine lake packs");

            WriteStoreZip(zip, new[] { "UnityDataAssetPack/assets/aa/catalog.hash", "base/AndroidManifest.xml" });
            names = SkiViewAndroidPadGate.ReadZipEntryNames(zip);
            missing = SkiViewAndroidPadGate.MissingEntries(names);
            if (missing.Count < 5)
            {
                Debug.LogError("[PadGate] FAIL r6b16-shaped zip was accepted");
                fails++;
            }
            else Debug.Log("[PadGate] PASS r6b16-shaped zip rejected (" + missing.Count + " missing)");
        }
        finally
        {
            if (File.Exists(zip)) File.Delete(zip);
        }
        Debug.Log($"[PadGate] {fails} failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }

    static void WriteStoreZip(string path, string[] names)
    {
        var ms = new MemoryStream();
        var locals = new System.Collections.Generic.List<byte[]>();
        var cds = new System.Collections.Generic.List<byte[]>();
        int offset = 0;
        foreach (var name in names)
        {
            byte[] nb = Encoding.UTF8.GetBytes(name);
            using (var w = new BinaryWriter(new MemoryStream()))
            {
                w.Write(0x04034b50);
                w.Write((ushort)20); w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write(0); w.Write(0); w.Write(0);
                w.Write((ushort)nb.Length); w.Write((ushort)0);
                w.Write(nb);
                w.Flush();
                locals.Add(((MemoryStream)w.BaseStream).ToArray());
            }
            using (var w = new BinaryWriter(new MemoryStream()))
            {
                w.Write(0x02014b50);
                w.Write((ushort)20); w.Write((ushort)20);
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write(0); w.Write(0); w.Write(0);
                w.Write((ushort)nb.Length); w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)0); w.Write((ushort)0); w.Write(0); w.Write(offset);
                w.Write(nb);
                w.Flush();
                cds.Add(((MemoryStream)w.BaseStream).ToArray());
            }
            offset += locals[locals.Count - 1].Length;
        }
        using (var fs = File.Create(path))
        {
            foreach (var b in locals) fs.Write(b, 0, b.Length);
            int cdStart = (int)fs.Position;
            int cdSize = 0;
            foreach (var b in cds) { fs.Write(b, 0, b.Length); cdSize += b.Length; }
            using (var w = new BinaryWriter(fs, Encoding.UTF8, true))
            {
                w.Write(0x06054b50);
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)names.Length); w.Write((ushort)names.Length);
                w.Write(cdSize); w.Write(cdStart);
                w.Write((ushort)0);
            }
        }
    }
}
