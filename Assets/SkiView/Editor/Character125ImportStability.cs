using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Face/Gear/Hair 125 texture policy. WebGL must stay DXT5 across repeated imports.
/// A manual ASTC or Automatic override is rewritten, then a second reimport does not change it again.
/// Run: unity_run.sh Character125ImportStability.Run
/// </summary>
public static class Character125ImportStability
{
    const string Root = "Assets/SkiView/Editor/Character125ReimportTemp";

    // 1x1 opaque PNG. The postprocessor decides the platform format before the pixels are compressed.
    static readonly byte[] Png =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x03, 0x01, 0x01, 0x00, 0xC9, 0xFE, 0x92, 0xEF, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82
    };

    public static void Run()
    {
        int fails = 0;
        void Check(bool ok, string what)
        {
            Debug.Log($"[Char125Import] {(ok ? "PASS" : "FAIL")} {what}");
            if (!ok) fails++;
        }

        CheckPolicy(Check);

        try
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();

            CheckSet(Check, "Face125", 2048, "Head_BaseColor.png", expectAlpha: false, normal: false, mask: false, gearAniso: false);
            CheckSet(Check, "Face125", 2048, "Head_Alpha.png", expectAlpha: true, normal: false, mask: false, gearAniso: false);
            CheckSet(Check, "Face125", 2048, "Head_Normal.png", expectAlpha: false, normal: true, mask: false, gearAniso: false);
            CheckSet(Check, "Face125", 2048, "Head_MaskMS.png", expectAlpha: false, normal: false, mask: true, gearAniso: false);
            CheckSet(Check, "Gear125", 2048, "Vest_Normal.png", expectAlpha: false, normal: true, mask: false, gearAniso: true);
            CheckSet(Check, "Gear125", 2048, "Vest_MaskMS.png", expectAlpha: false, normal: false, mask: true, gearAniso: true);
            CheckSet(Check, "Hair125", 1024, "Hair_Alpha.png", expectAlpha: true, normal: false, mask: false, gearAniso: false);
            CheckSet(Check, "Hair125", 1024, "Hair_Normal.png", expectAlpha: false, normal: true, mask: false, gearAniso: false);
            CheckSet(Check, "Hair125", 1024, "Hair_MaskMS.png", expectAlpha: false, normal: false, mask: true, gearAniso: false);
        }
        finally
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            AssetDatabase.Refresh();
        }

        Debug.Log($"[Char125Import] {fails} failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }

    static void CheckPolicy(System.Action<bool, string> Check)
    {
        foreach (var pair in new (string name, System.Func<string, TextureImporterFormat> fmt)[]
        {
            ("Face125", Face125Import.PlatformFormat),
            ("Gear125", Gear125Import.PlatformFormat),
            ("Hair125", Hair125Import.PlatformFormat),
        })
        {
            var a = pair.fmt("WebGL");
            var b = pair.fmt("WebGL");
            Check(a == b && a == TextureImporterFormat.DXT5, $"{pair.name} WebGL format is DXT5 and stable ({a})");
            Check(a != TextureImporterFormat.ASTC_6x6 && a != TextureImporterFormat.Automatic, $"{pair.name} WebGL is not ASTC or Automatic");
            Check(pair.fmt("Android") == TextureImporterFormat.ASTC_6x6, $"{pair.name} Android stays ASTC_6x6");
            Check(pair.fmt("iPhone") == TextureImporterFormat.ASTC_6x6, $"{pair.name} iPhone stays ASTC_6x6");
            Check(pair.fmt("Android") == pair.fmt("Android"), $"{pair.name} Android format is stable across calls");
        }
        Check(Face125Import.PlatformFormat("Standalone") == TextureImporterFormat.DXT5, "Face125 Standalone stays DXT5");
        Check(Face125Import.PolicyVersion == 2 && Gear125Import.PolicyVersion == 2 && Hair125Import.PolicyVersion == 2
            && Face125Import.PolicyVersion == Gear125Import.PolicyVersion, "importer policy version is 2 on all three and does not drift");
    }

    static void CheckSet(System.Action<bool, string> Check, string folder, int maxSize, string file, bool expectAlpha, bool normal, bool mask, bool gearAniso)
    {
        string dir = $"{Root}/Resources/{folder}";
        Directory.CreateDirectory(dir);
        string path = $"{dir}/{file}";
        File.WriteAllBytes(path, Png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var first = Snapshot(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var second = Snapshot(path);
        Check(first == second, $"{folder}/{file} settings identical after a second reimport ({second})");

        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        // The failure mode: a hand edit to ASTC or Automatic is overwritten on the next import, then stays put.
        foreach (var poisoned in new[] { TextureImporterFormat.ASTC_6x6, TextureImporterFormat.Automatic })
        {
            var web = ti.GetPlatformTextureSettings("WebGL");
            web.overridden = true;
            web.format = poisoned;
            web.maxTextureSize = 32;
            ti.SetPlatformTextureSettings(web);
            ti.SaveAndReimport();
            var after = Snapshot(path);
            Check(after == first, $"{folder}/{file} reimport restores policy after WebGL was set to {poisoned} ({after})");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var third = Snapshot(path);
            Check(third == after, $"{folder}/{file} stays stable on the reimport after {poisoned} ({third})");
        }

        Check(first.web == TextureImporterFormat.DXT5 && first.android == TextureImporterFormat.ASTC_6x6 && first.iphone == TextureImporterFormat.ASTC_6x6,
            $"{folder}/{file} WebGL DXT5, Android/iPhone ASTC");
        Check(first.max == maxSize && first.webMax == maxSize, $"{folder}/{file} max size {maxSize}");
        Check(first.mips, $"{folder}/{file} mipmaps stay on");
        if (normal) Check(first.normal && !first.srgb, $"{folder}/{file} normal map, linear");
        if (mask) Check(!first.normal && !first.srgb, $"{folder}/{file} mask stays linear and is not a normal map");
        if (file.Contains("Alpha") || file.Contains("BaseColor")) Check(first.alpha == expectAlpha, $"{folder}/{file} alphaIsTransparency {expectAlpha}");
        if (gearAniso) Check(first.aniso == 4, $"{folder}/{file} gear aniso stays 4");
    }

    struct Shot
    {
        public TextureImporterFormat web, android, iphone;
        public int max, webMax, aniso;
        public bool srgb, normal, alpha, mips;
        public override string ToString() => $"web={web}@{webMax} android={android} iphone={iphone} max={max} srgb={srgb} normal={normal} alpha={alpha} mips={mips} aniso={aniso}";
        public static bool operator ==(Shot a, Shot b) => a.web == b.web && a.android == b.android && a.iphone == b.iphone && a.max == b.max && a.webMax == b.webMax && a.aniso == b.aniso && a.srgb == b.srgb && a.normal == b.normal && a.alpha == b.alpha && a.mips == b.mips;
        public static bool operator !=(Shot a, Shot b) => !(a == b);
        public override bool Equals(object obj) => obj is Shot s && this == s;
        public override int GetHashCode() => web.GetHashCode() ^ android.GetHashCode() ^ max;
    }

    static Shot Snapshot(string path)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        var web = ti.GetPlatformTextureSettings("WebGL");
        var android = ti.GetPlatformTextureSettings("Android");
        var iphone = ti.GetPlatformTextureSettings("iPhone");
        return new Shot
        {
            web = web.format, android = android.format, iphone = iphone.format,
            max = ti.maxTextureSize, webMax = web.maxTextureSize, aniso = ti.anisoLevel,
            srgb = ti.sRGBTexture, normal = ti.textureType == TextureImporterType.NormalMap,
            alpha = ti.alphaIsTransparency, mips = ti.mipmapEnabled
        };
    }
}
