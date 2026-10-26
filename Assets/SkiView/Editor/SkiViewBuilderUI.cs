using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

/// <summary>
/// Pass 18: imports Julia's UI kit (skiview-assets-staging/ui, copied into Assets/SkiView/UI): SVG icons and scrims as UI Toolkit
/// Vector Images, SDF FontAssets for the faces the USS binds ("X SDF.asset", sampling 90, padding 9, 1024 atlas, dynamic, ASCII
/// pre-baked), the default runtime theme and the PanelSettings asset (Scale With Screen Size, 1920x1080, Expand) in
/// Resources/SkiViewUI, then reimports the USS so its url() references resolve. Runs from BuildAll.
/// </summary>
public static class SkiViewBuilderUI
{
    const string UI = "Assets/SkiView/UI", RES = UI + "/Resources/SkiViewUI";
    static readonly string[] FONTS = { "BarlowCondensed-SemiBold", "BarlowCondensed-Bold", "BarlowCondensed-ExtraBold", "Barlow-Medium", "SVTabCondensed-Bold" };

    [MenuItem("SkiView/Setup UI kit")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(RES)) { Debug.LogWarning("[SkiView] UI kit: " + RES + " missing"); return; }
        int svg = 0;
        foreach (var dir in new[] { RES + "/Icons", UI + "/Textures" })
            foreach (var guid in AssetDatabase.FindAssets("", new[] { dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid); if (!p.EndsWith(".svg")) continue;
                var imp = AssetImporter.GetAtPath(p); if (imp == null) continue;
                var so = new SerializedObject(imp); var t = so.FindProperty("m_SvgType");
                var prop = imp.GetType().GetProperty("SvgType");
                bool changed = false;
                if (prop != null)
                {
                    var want = System.Enum.Parse(prop.PropertyType, "VectorImage");
                    if (!Equals(prop.GetValue(imp), want)) { prop.SetValue(imp, want); changed = true; }
                    var tm = imp.GetType().GetProperty("TargetResolution"); if (tm != null && tm.PropertyType == typeof(int)) tm.SetValue(imp, 96);
                }
                else if (t != null) { t.intValue = 3; so.ApplyModifiedPropertiesWithoutUndo(); changed = true; }
                if (changed) { imp.SaveAndReimport(); }
                svg++;
            }
        const string ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u00a9\u00b7\u2190\u2192\u2193";
        int made = 0;
        foreach (var f in FONTS)
        {
            string fa = UI + "/Fonts/" + f + " SDF.asset";
            if (File.Exists(fa)) continue;
            var font = AssetDatabase.LoadAssetAtPath<Font>(UI + "/Fonts/" + f + ".ttf");
            if (font == null) { Debug.LogWarning("[SkiView] UI kit: font missing " + f); continue; }
            var asset = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) { Debug.LogWarning("[SkiView] UI kit: CreateFontAsset failed for " + f); continue; }
            asset.name = f + " SDF";
            AssetDatabase.CreateAsset(asset, fa);
            asset.TryAddCharacters(ascii, out _);
            if (asset.atlasTextures != null) foreach (var tx in asset.atlasTextures) if (tx != null && !AssetDatabase.Contains(tx)) { tx.name = f + " Atlas"; AssetDatabase.AddObjectToAsset(tx, asset); }
            if (asset.material != null && !AssetDatabase.Contains(asset.material)) { asset.material.name = f + " Material"; AssetDatabase.AddObjectToAsset(asset.material, asset); }
            EditorUtility.SetDirty(asset); made++;
        }
        AssetDatabase.SaveAssets();
        string tss = RES + "/SV_Theme.tss";
        if (!File.Exists(tss)) { File.WriteAllText(tss, "@import url(\"unity-theme://default\");\n"); AssetDatabase.ImportAsset(tss); }
        var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tss);
        string pp = RES + "/SV_Panel.asset";
        var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(pp);
        if (ps == null) { ps = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(ps, pp); }
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize; ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.screenMatchMode = PanelScreenMatchMode.Expand; ps.referenceDpi = 96; ps.fallbackDpi = 96; ps.sortingOrder = 10;
        ps.themeStyleSheet = theme; ps.clearColor = false;
        EditorUtility.SetDirty(ps); AssetDatabase.SaveAssets();
        foreach (var guid in AssetDatabase.FindAssets("t:StyleSheet", new[] { RES })) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        var cam = AssetDatabase.LoadAssetAtPath<VectorImage>(RES + "/Icons/camera.svg");
        Debug.Log($"[SkiView] UI kit: {svg} SVGs (camera.svg as VectorImage: {cam != null}), {made} new FontAssets, theme {(theme != null)}, PanelSettings 1920x1080 Expand -> {pp}");
    }
}
