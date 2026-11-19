using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SkiView;

/// <summary>
/// Pass 18: brings in James's sound pack from skiview-assets-staging/audio using manifest.json only (_raw/ and _tools/ are never
/// read). Core sounds (boat, skier, UI, the result stings) go to Assets/SkiView/Audio/<folder>/ and into
/// Resources/SkiViewAudio/Bank.asset; each built lake's baked ambient bed (ambient/amb_lake_<id>_loop) is referenced only by that
/// lake's SkiViewGame.ambientClip, so it ships in the lake's own pack. Import per UNITY-AUDIO-SPEC §0.3: loops Compressed In Memory,
/// short SFX Decompress On Load, mono forced where the file is mono, Vorbis q 0.7. Loop points from loop_start/loop_end_samples.
/// </summary>
public static class SkiViewBuilderAudio
{
    const string SRC = "/workspace/skiview-assets-staging/audio", DST = "Assets/SkiView/Audio", RES = "Assets/SkiView/Audio/Resources/SkiViewAudio";
    static readonly HashSet<string> CORE = new HashSet<string> {
        "boat_idle_loop", "boat_idle_onboard_loop", "boat_throttle_up", "boat_throttle_up_onboard", "boat_cruise_v8_loop", "boat_hull_slap_loop", "boat_wake_loop",
        "boat_wake_shore_wash", "boat_engine_decel_v8", "boat_gear_clunk",
        "skier_carve_spray", "skier_edge_change", "skier_buoy_pass", "skier_splash_fall", "skier_ski_landing", "skier_jump_ramp_hit", "skier_big_crash",
        "skier_rope_tug", "skier_plow_rush", "skier_float_lap_loop", "skier_jump_runup_hiss", "skier_jump_lip_pop", "skier_flight_wind_loop", "skier_jump_whoosh",
        "ui_click_01", "ui_click_02", "ui_click_03", "ui_click_04", "ui_confirm", "ui_back", "ui_results_sting", "ui_results_sting_fail", "ui_gate_tick",
        // pass 19 (audio pass 2 §1.5 / §1.6): scoreboard, tournament PA + applause, shore birds, menu music
        "ui_scoreboard_beep", "amb_pa_chime", "amb_crowd_applause_cheer", "amb_bird_sandhill_crane", "amb_bird_osprey", "amb_bird_anhinga", "music_menu_loop", "boat_passby_fast", "amb_show_pa_announce",
        // audio passes 4-8 (T13 §6-§10)
        "boat_engine_rpm_low_loop", "boat_engine_rpm_mid_loop", "boat_engine_rpm_high_loop", "boat_passby", "boat_engine_idle_driver_loop",
        "skier_dws_splash", "skier_dws_rope_tension", "skier_dws_planing",
        "skier_rope_slap_01", "skier_rope_slap_02", "skier_edge_chatter_01", "skier_edge_chatter_02", "skier_wake_thump_light", "skier_wake_thump_heavy", "skier_jump_landing_heavy",
        "skier_trick_carve_01", "skier_trick_carve_02", "skier_trick_carve_03", "skier_trick_slide_loop", "skier_trick_spin_180_01", "skier_trick_spin_180_02",
        "skier_trick_spin_360_01", "skier_trick_spin_360_02", "skier_trick_wake_pop_01", "skier_trick_wake_pop_02", "skier_trick_wake_land_01", "skier_trick_wake_land_02",
        "skier_trick_toestrap_on", "skier_trick_toehold_rope_01", "skier_trick_toehold_rope_02", "skier_trick_flip_whoosh_01", "skier_trick_flip_whoosh_02",
        "skier_trick_flip_land_splash", "skier_trick_fall_splash_01", "skier_trick_fall_splash_02",
        "ui_clinic_lesson_start", "ui_clinic_pass", "ui_clinic_fail", "ui_clinic_tip_chime", "ui_beatscott_rung_unlock", "ui_beatscott_near_miss", "ui_beatscott_win",
        "ui_ski_unlock", "ui_personal_best", "ui_results_sting_alt",
        "ui_trick_tally_tick", "ui_trick_timer_beep", "ui_trick_timer_final", "ui_trick_score_chime", "ui_trick_score_chime_big", "ui_trick_result_sting",
        "music_title_loop",
        "amb_reed_rustle_01", "amb_reed_rustle_02", "amb_reed_rustle_03", "amb_bank_lap_01", "amb_bank_lap_02", "amb_bank_lap_03",
        "amb_dock_creak_01", "amb_dock_creak_02", "amb_dock_creak_03",
        // pass 10: presence layer, tournament jump bed (bank clip, swapped in by SkiAudio.TournamentJumpAmbience), spectator reactions
        "boat_engine_rpm_high_presence_loop", "amb_bed_tournament_jump_loop", "amb_crowd_react_big_01", "amb_crowd_react_big_02" };   // pass 19 audio pass 3: v8 inboard cruise / decel, show PA


    static List<Dictionary<string, string>> Manifest()
    {
        // manifest.json is a flat array of objects with mixed-type fields; pull the handful we need with a tiny scanner
        var list = new List<Dictionary<string, string>>();
        string txt = File.ReadAllText(Path.Combine(SRC, "manifest.json"));
        int i = 0;
        while ((i = txt.IndexOf("\"file\":", i)) >= 0)
        {
            int end = txt.IndexOf("\"sources\"", i); if (end < 0) end = txt.Length;
            string blk = txt.Substring(i, end - i); var d = new Dictionary<string, string>();
            foreach (var k in new[] { "file", "loop", "loop_start_samples", "loop_end_samples", "channels", "integrated_lufs" })
            {
                int p = blk.IndexOf("\"" + k + "\":"); if (p < 0) continue; p += k.Length + 3;
                int q = blk.IndexOfAny(new[] { ',', '\n' }, p); string v = blk.Substring(p, (q < 0 ? blk.Length : q) - p).Trim().Trim('"');
                d[k] = v;
            }
            list.Add(d); i = end;
        }
        return list;
    }

    public static AudioClip Ambient(string lakeId)
    {
        // pass 5/8: prefer the new per-lake bed amb_bed_<id>_loop (Src -6 in SkiAudio), fall back to the old amb_lake_<id>_loop
        var bed = AssetDatabase.LoadAssetAtPath<AudioClip>(DST + "/ambient/amb_bed_" + lakeId + "_loop.ogg");
        return bed != null ? bed : AssetDatabase.LoadAssetAtPath<AudioClip>(DST + "/ambient/amb_lake_" + lakeId + "_loop.ogg");
    }

    [MenuItem("SkiView/Setup audio")]
    public static void Setup()
    {
        if (!File.Exists(Path.Combine(SRC, "manifest.json"))) { Debug.LogWarning("[SkiView] audio: no manifest at " + SRC); return; }
        var lakes = new HashSet<string>(SkiViewBuilder.LAKES) { "skiview" };
        var man = Manifest(); var entries = new List<SkiAudioBank.Entry>(); int copied = 0, beds = 0; long bytes = 0;
        foreach (var e in man)
        {
            string f = e["file"]; if (f.StartsWith("_raw") || f.StartsWith("_tools")) continue;
            string id = Path.GetFileNameWithoutExtension(f);
            bool bed = (id.StartsWith("amb_lake_") || id.StartsWith("amb_bed_")) && id.EndsWith("_loop") && lakes.Contains(id.Substring(id.IndexOf('_', 4) + 1, id.Length - (id.IndexOf('_', 4) + 1) - 5));
            if (id == "amb_bed_tournament_jump_loop") bed = false;   // pass 10: event bed, lives in the Bank, not a lake bed
            if (!CORE.Contains(id) && !bed) continue;
            string dst = DST + "/" + f; Directory.CreateDirectory(Path.GetDirectoryName(dst));
            if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(Path.Combine(SRC, f)).Length || File.GetLastWriteTimeUtc(Path.Combine(SRC, f)) > File.GetLastWriteTimeUtc(dst)) { File.Copy(Path.Combine(SRC, f), dst, true); copied++; }
            bytes += new FileInfo(dst).Length;
            AssetDatabase.ImportAsset(dst);
            bool loop = e.TryGetValue("loop", out var lp) && lp == "yes";
            var imp = AssetImporter.GetAtPath(dst) as AudioImporter;
            if (imp != null)
            {
                imp.forceToMono = e.TryGetValue("channels", out var ch) && ch == "1";
                imp.loadInBackground = bed;
                var st = imp.defaultSampleSettings;
                st.loadType = loop || bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = 0.7f; st.preloadAudioData = !bed;
                imp.defaultSampleSettings = st; imp.SaveAndReimport();
            }
            if (bed) { beds++; continue; }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(dst);
            int.TryParse(e.TryGetValue("loop_start_samples", out var ls) ? ls : "0", out int l0);
            int.TryParse(e.TryGetValue("loop_end_samples", out var le) ? le : "0", out int l1);
            float.TryParse(e.TryGetValue("integrated_lufs", out var lu) ? lu : "0", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lufs);
            entries.Add(new SkiAudioBank.Entry { id = id, clip = clip, lufs = lufs, loop = loop, loopStart = l0, loopEnd = l1 });
        }
        Directory.CreateDirectory(RES);
        string bp = RES + "/Bank.asset";
        var bank = AssetDatabase.LoadAssetAtPath<SkiAudioBank>(bp);
        if (bank == null) { bank = ScriptableObject.CreateInstance<SkiAudioBank>(); AssetDatabase.CreateAsset(bank, bp); }
        bank.entries = entries.ToArray(); EditorUtility.SetDirty(bank); AssetDatabase.SaveAssets();
        Debug.Log($"[SkiView] audio: {entries.Count} core sounds in the bank, {beds} lake beds, {copied} files copied, {bytes / 1024} KB source OGG (skipped _raw/_tools)");
    }
}
