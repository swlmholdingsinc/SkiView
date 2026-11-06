using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Fail-fast check for the Play Asset Delivery payload that r6b10 packed and r6b16 omitted.
/// No signing, no upload. Entry names only.
/// </summary>
public static class SkiViewAndroidPadGate
{
    public static readonly string[] LakeIds =
    {
        "harrowpond", "gulfwind", "saltwhite", "saguaro", "tallpine",
        "heronmarsh", "villaserena", "cedarbend", "marrowind"
    };

    public static readonly string[] RequiredFragments =
    {
        "AddressablesAssetPack/assets/aa/catalog.bin",
        "AddressablesAssetPack/assets/aa/settings.json",
        "AddressablesAssetPack/assets/aa/CustomAssetPacksData.json",
        "AddressablesAssetPack/assets/aa/Android/core_home_scenes_all.bundle",
        "AddressablesAssetPack/assets/aa/Android/core_shared_assets_all.bundle"
    };

    public static List<string> MissingEntries(IEnumerable<string> zipEntries)
    {
        var names = new List<string>();
        if (zipEntries != null)
            foreach (var e in zipEntries)
                if (!string.IsNullOrEmpty(e)) names.Add(e.Replace('\\', '/'));
        var missing = new List<string>();
        foreach (var frag in RequiredFragments)
            if (!Contains(names, frag)) missing.Add(frag);
        foreach (var id in LakeIds)
        {
            string frag = "Lake_" + id + "/assets/aa/Android/lake_" + id + "_scenes_all.bundle";
            if (!Contains(names, frag)) missing.Add(frag);
        }
        return missing;
    }

    public static List<string> MissingGradleIncludes(string settingsGradle)
    {
        var missing = new List<string>();
        string text = settingsGradle ?? "";
        if (text.IndexOf("AddressablesAssetPack", StringComparison.Ordinal) < 0) missing.Add("AddressablesAssetPack");
        foreach (var id in LakeIds)
            if (text.IndexOf("Lake_" + id, StringComparison.Ordinal) < 0) missing.Add("Lake_" + id);
        return missing;
    }

    static bool Contains(List<string> names, string frag)
    {
        for (int i = 0; i < names.Count; i++)
            if (names[i].IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    public static List<string> ReadZipEntryNames(string path)
    {
        using (var fs = File.OpenRead(path))
        {
            if (fs.Length < 22) throw new InvalidDataException("zip too small: " + path);
            int window = (int)Math.Min(fs.Length, 22 + 65535);
            var tail = new byte[window];
            fs.Seek(-window, SeekOrigin.End);
            ReadExact(fs, tail, window);
            int eocd = -1;
            for (int i = tail.Length - 22; i >= 0; i--)
            {
                if (tail[i] == 0x50 && tail[i + 1] == 0x4b && tail[i + 2] == 0x05 && tail[i + 3] == 0x06) { eocd = i; break; }
            }
            if (eocd < 0) throw new InvalidDataException("zip EOCD not found: " + path);
            int cdSize = BitConverter.ToInt32(tail, eocd + 12);
            int cdOff = BitConverter.ToInt32(tail, eocd + 16);
            if (cdSize < 0 || cdOff < 0 || cdOff + cdSize > fs.Length) throw new InvalidDataException("zip central directory out of range: " + path);
            var cd = new byte[cdSize];
            fs.Seek(cdOff, SeekOrigin.Begin);
            ReadExact(fs, cd, cdSize);
            var names = new List<string>();
            int p = 0;
            while (p + 46 <= cd.Length)
            {
                if (cd[p] != 0x50 || cd[p + 1] != 0x4b || cd[p + 2] != 0x01 || cd[p + 3] != 0x02) break;
                int nameLen = BitConverter.ToUInt16(cd, p + 28);
                int extra = BitConverter.ToUInt16(cd, p + 30);
                int comment = BitConverter.ToUInt16(cd, p + 32);
                if (p + 46 + nameLen > cd.Length) break;
                names.Add(Encoding.UTF8.GetString(cd, p + 46, nameLen));
                p += 46 + nameLen + extra + comment;
            }
            return names;
        }
    }

    static void ReadExact(Stream s, byte[] buf, int n)
    {
        int o = 0;
        while (o < n)
        {
            int r = s.Read(buf, o, n - o);
            if (r <= 0) throw new EndOfStreamException();
            o += r;
        }
    }

    /// <summary>r6b16 inventory must fail. r6b10 pack paths must pass. Returns the failure count.</summary>
    public static int CheckFixtures()
    {
        int fails = 0;
        var bad = new[]
        {
            "BUNDLE-METADATA/", "BundleConfig.pb", "META-INF/", "UnityDataAssetPack/", "base/",
            "UnityDataAssetPack/assets/aa/catalog.hash"
        };
        var badMissing = MissingEntries(bad);
        if (badMissing.Count < RequiredFragments.Length + LakeIds.Length) fails++;
        if (MissingEntries(bad).Count != badMissing.Count) fails++;

        var good = new List<string>
        {
            "base/", "UnityDataAssetPack/",
            "AddressablesAssetPack/assets/aa/catalog.bin",
            "AddressablesAssetPack/assets/aa/settings.json",
            "AddressablesAssetPack/assets/aa/CustomAssetPacksData.json",
            "AddressablesAssetPack/assets/aa/Android/core_home_scenes_all.bundle",
            "AddressablesAssetPack/assets/aa/Android/core_shared_assets_all.bundle"
        };
        foreach (var id in LakeIds)
            good.Add("Lake_" + id + "/assets/aa/Android/lake_" + id + "_scenes_all.bundle");
        if (MissingEntries(good).Count != 0) fails++;
        if (MissingEntries(good).Count != MissingEntries(good).Count) fails++;

        string gradleBad = "include ':launcher'\ninclude ':unityLibrary'\ninclude ':UnityDataAssetPack'\n";
        if (MissingGradleIncludes(gradleBad).Count != 1 + LakeIds.Length) fails++;
        var gradleGood = new StringBuilder(gradleBad);
        gradleGood.Append("include ':AddressablesAssetPack'\n");
        foreach (var id in LakeIds) gradleGood.Append("include ':Lake_").Append(id).Append("'\n");
        if (MissingGradleIncludes(gradleGood.ToString()).Count != 0) fails++;
        return fails;
    }
}
