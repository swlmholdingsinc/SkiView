using System;
using System.IO;
using System.Text;
using SkiView;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>b10.7: autopilot one clean pass each direction on Ski View; capture chase+top at let-go (dropPh 1→2)
/// and prove drop distance from each island centre stays in open water (mouth ~90–110 m, never channel ~26–33 m).
/// Also grabs island ground low shots. Run: unity_run.sh SkiViewCapture.DropMouth</summary>
public static partial class SkiViewCapture
{
    public static void DropMouth()
    {
        Directory.CreateDirectory(Path.Combine(OutDir, "turnaround"));
        Directory.CreateDirectory(Path.Combine(OutDir, "diag", "ground"));
        var sb = new StringBuilder("end,dropIslandM,boatX,boatZ,skierX,skierZ,dropR,circleLandHits,phaseLog\n");
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        SkiViewGame.SaveSetup(0, 0, 34);
        var g = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        g.autopilot = true; g.Init();
        SkiViewTerrainFix.Apply();
        int ends = 0; int lastPh = 0; var phase = new StringBuilder();
        int shots = 0;
        var prevMode = g.mode;
        // First completed pass on home start is southbound (toward EXIT_Z island); second is northbound.
        for (int i = 0; i < 6000 && ends < 2; i++)
        {
            g.SimulateFor(0.1f);
            if (g.DropPhase != lastPh)
            {
                phase.Append($"{i * 0.1f:0.0}:{lastPh}->{g.DropPhase};");
                if (lastPh == 1 && g.DropPhase == 2 && ends < 2)
                {
                    Vector3 dropBoat = g.BoatPos, dropSkier = g.SkierPos;
                    float dropIslandAtLetGo = DistNearestIsland(g, dropBoat);
                    float dropR = ReadFloat(g, "dropR");
                    int land = CircleLandHits(g);
                    string end = NearerEndName(g, dropBoat);
                    sb.AppendLine($"{end},{dropIslandAtLetGo:0.0},{dropBoat.x:0.00},{dropBoat.z:0.00},{dropSkier.x:0.00},{dropSkier.z:0.00},{dropR:0.0},{land},{phase}");
                    Debug.Log($"[SkiView] dropmouth {end}: islandDist={dropIslandAtLetGo:0.0}m R={dropR:0.0} landHits={land} boat={dropBoat}");
                    Shot(g.cam, g, $"turnaround/b10.7_{end}_drop_chase", null);
                    Vector3 look = dropBoat;
                    Shot(g.cam, g, $"turnaround/b10.7_{end}_drop_top", () =>
                    {
                        g.cam.fieldOfView = 50f;
                        g.cam.transform.position = look + Vector3.up * 90f - g.BoatFwd * 10f;
                        g.cam.transform.LookAt(look);
                    });
                    shots += 2;
                    ends++;
                    phase.Clear();
                }
                lastPh = g.DropPhase;
            }
            prevMode = g.mode;
        }
        // ground island lows (after drop sim so TerrainFix applied + mats loaded)
        var vl = JsonUtility.FromJson<VenueLite>(File.ReadAllText(Path.Combine(Application.dataPath, "SkiView/Generated/venue.json")));
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        g = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        g.autopilot = true; g.Init(); SkiViewTerrainFix.Apply();
        g.SimulateFor(0.1f);
        for (int k = 0; k < 2 && vl.islandCentres != null && vl.islandCentres.Length >= 4; k++)
        {
            var c = new Vector3(vl.islandCentres[k * 2], 0, vl.islandCentres[k * 2 + 1]);
            Vector3 toLake = new Vector3(-c.x, 0, -c.z).normalized;
            if (toLake.sqrMagnitude < 0.01f) toLake = Vector3.forward;
            string name = k == 0 ? "south" : "north";
            Action place = () =>
            {
                g.cam.fieldOfView = 50f;
                g.cam.transform.position = c + toLake * 75f + Vector3.up * 2.5f;
                g.cam.transform.LookAt(c + Vector3.up * 4f);
            };
            Shot(g.cam, g, $"turnaround/b10.7_ground_{name}_island", place);
            Shot(g.cam, g, $"diag/ground/after4_desktop_island{k + 1}_low", place);
        }
        File.WriteAllText(Path.Combine(OutDir, "turnaround/b10.7_dropmouth.txt"), sb.ToString());
        Debug.Log("[SkiView] dropmouth done shots=" + shots + "\n" + sb);
    }

    static float ReadFloat(SkiViewGame g, string field)
    {
        var f = typeof(SkiViewGame).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return f == null ? -1f : (float)f.GetValue(g);
    }

    static string NearerEndName(SkiViewGame g, Vector3 p)
    {
        if (g.islandCentres == null || g.islandCentres.Length < 4) return "unk";
        var s = new Vector3(g.islandCentres[0], 0, g.islandCentres[1]);
        var n = new Vector3(g.islandCentres[2], 0, g.islandCentres[3]);
        var q = new Vector3(p.x, 0, p.z);
        return Vector3.Distance(q, s) <= Vector3.Distance(q, n) ? "south" : "north";
    }

    static float DistNearestIsland(SkiViewGame g, Vector3 p)
    {
        float best = 999f;
        if (g.islandCentres == null) return best;
        for (int i = 0; i + 1 < g.islandCentres.Length; i += 2)
        {
            var ic = new Vector3(g.islandCentres[i], 0f, g.islandCentres[i + 1]);
            best = Mathf.Min(best, Vector3.Distance(new Vector3(p.x, 0, p.z), ic));
        }
        return best;
    }

    static int CircleLandHits(SkiViewGame g)
    {
        float R = ReadFloat(g, "dropR");
        var fC = typeof(SkiViewGame).GetField("dropC", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        var fU = typeof(SkiViewGame).GetField("dropU", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        var fF = typeof(SkiViewGame).GetField("dropF", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Vector3 C = (Vector3)fC.GetValue(g), U = (Vector3)fU.GetValue(g), F = (Vector3)fF.GetValue(g);
        var ter = UnityEngine.Object.FindFirstObjectByType<Terrain>(); if (ter == null) return -1;
        int hits = 0;
        for (int k = 0; k < 16; k++)
        {
            float a = k * Mathf.PI * 2f / 16f;
            Vector3 p = C + R * (U * Mathf.Cos(a) + F * Mathf.Sin(a));
            float gy = ter.SampleHeight(p) + ter.transform.position.y;
            if (gy > -1.0f) hits++;
        }
        return hits;
    }

    /// <summary>b10.25: full post-pass pull-out QA on Ski View home lake (Scott mouth rule + James checklist).
    /// Autopilot completes clean slalom passes; logs boat↔island, boatS, dropPh, island/beach intrusion;
    /// plates under Screens/polish/b10.25_* (+ turnaround mirrors). Run: unity_run.sh SkiViewCapture.DropPullOutQA
    /// Holly T17b G1–G6 Tricks gaps: HOLD this slice (slalom Ski View only).</summary>
    public static void DropPullOutQA()
    {
        Directory.CreateDirectory(Path.Combine(OutDir, "turnaround"));
        Directory.CreateDirectory(Path.Combine(OutDir, "polish"));
        var log = new StringBuilder("t,end,dropPh,boatS,islandM,boatX,boatZ,skierX,skierZ,groundY,onLand,inIslandR35,dropR,circleLandHits\n");
        var summary = new StringBuilder("b10.25 DropPullOutQA — Ski View home lake post-pass pull-out\n");
        summary.AppendLine("Scott: mouth drop → pull-out wide → float → open-water circle (never island/beach).");
        summary.AppendLine("Holly T17b G1–G6 Tricks gaps: HOLD (slalom only this build).");
        EditorSceneManager.OpenScene(SkiViewBuilder.SCENE);
        SkiViewGame.SaveSetup(0, 0, 34);
        var g = UnityEngine.Object.FindFirstObjectByType<SkiViewGame>();
        g.autopilot = true; g.Init();
        SkiViewTerrainFix.Apply();
        var ter = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        int ends = 0; int lastPh = 0; int shots = 0;
        bool bugIsland = false, bugBeach = false, bugMouth = false;
        bool tookMouth = false, tookPull = false, tookFloat = false, tookCircle = false, tookNext = false;
        bool midShot = false;
        var prevMode = g.mode;
        float letgoIsland = -1f;
        // Two completed-pass drops (N + S). Count ends on let-go (1→2).
        for (int i = 0; i < 9000 && ends < 2; i++)
        {
            g.SimulateFor(0.1f);
            float t = i * 0.1f;
            int ph = g.DropPhase;
            Vector3 bp = g.BoatPos, sp = g.SkierPos;
            float islandM = DistNearestIsland(g, bp);
            float gy = ter != null ? ter.SampleHeight(bp) + ter.transform.position.y : -99f;
            bool onLand = gy > -1.0f;
            bool inIsle = islandM < 35f;
            string end = NearerEndName(g, bp);

            if (ph >= 1)
            {
                if (inIsle) bugIsland = true;
                if (onLand) bugBeach = true;
                if (ph != lastPh || i % 5 == 0)
                {
                    float dropR = ph >= 2 ? ReadFloat(g, "dropR") : -1f;
                    int land = ph >= 2 ? CircleLandHits(g) : -1;
                    log.AppendLine($"{t:0.0},{end},{ph},{g.BoatS:0.00},{islandM:0.0},{bp.x:0.00},{bp.z:0.00},{sp.x:0.00},{sp.z:0.00},{gy:0.00},{(onLand ? 1 : 0)},{(inIsle ? 1 : 0)},{dropR:0.0},{land}");
                }
            }

            if (ph != lastPh)
            {
                // Mouth freeze: wait ended, pull-out starts (still ph 1) — captured at 0→1 then again at freeze via dropWait field.
                if (lastPh == 0 && ph == 1)
                {
                    summary.AppendLine($"approach {end}: islandM={islandM:0.0} boatS={g.BoatS:0.0} boat={bp}");
                }
                if (lastPh == 1 && ph == 2)
                {
                    float dropR = ReadFloat(g, "dropR");
                    int land = CircleLandHits(g);
                    letgoIsland = islandM;
                    // Mouth band = (50, 62]; allow small seek tolerance 48–70.
                    if (islandM < 48f || islandM > 70f) bugMouth = true;
                    if (land > 0) bugBeach = true;
                    if (islandM < 50f) bugIsland = true;

                    // Per-end chase/top (turnaround/)
                    Shot(g.cam, g, $"turnaround/b10.25_{end}_drop_chase", null);
                    Vector3 look = bp;
                    Shot(g.cam, g, $"turnaround/b10.25_{end}_drop_top", () =>
                    {
                        g.cam.fieldOfView = 50f;
                        g.cam.transform.position = look + Vector3.up * 90f - g.BoatFwd * 10f;
                        g.cam.transform.LookAt(look);
                    });
                    shots += 2;

                    // James checklist plates (once, first clean let-go)
                    if (!tookMouth)
                    {
                        Shot(g.cam, g, "polish/b10.25_mouth_drop", null);
                        Shot(g.cam, g, "polish/b10.25_mouth_drop_top", () =>
                        {
                            g.cam.fieldOfView = 50f;
                            g.cam.transform.position = look + Vector3.up * 90f - g.BoatFwd * 10f;
                            g.cam.transform.LookAt(look);
                        });
                        tookMouth = true; shots += 2;
                    }
                    summary.AppendLine($"letgo {end}: islandM={islandM:0.0} R={dropR:0.0} circleLandHits={land} boatS={g.BoatS:0.0} boat={bp} skier={sp}");
                    ends++;
                    midShot = false;
                }
                if (ph == 2 && lastPh < 2 && !tookFloat)
                {
                    // float beat just after let-go (same tick as 1→2; take after a brief coast below)
                }
                if ((ph == 3 || ph == 4) && lastPh == 2)
                    summary.AppendLine($"circleDone {end}: islandM={islandM:0.0} boat={bp} → ph{ph}");
                lastPh = ph;
            }

            // Pull-out wide: during ph 1 after freeze (boatS stable), skier off to tree side
            if (ph == 1 && !tookPull && Mathf.Abs(g.sim.Lateral) > 6f)
            {
                Shot(g.cam, g, "polish/b10.25_pullout_wide", null);
                tookPull = true; shots++;
                summary.AppendLine($"pullout_wide: lat={g.sim.Lateral:0.0} islandM={islandM:0.0} boat={bp}");
            }
            // Float plate ~1.2 s into sink
            if (ph == 2 && !tookFloat && g.mode == SkiViewGame.Mode.Sink && g.ActionT > 1.2f)
            {
                Shot(g.cam, g, "polish/b10.25_float", null);
                tookFloat = true; shots++;
                summary.AppendLine($"float: islandM={islandM:0.0} skier={sp} boat={bp}");
            }
            // Mid-circle open-water plate (James island_circle)
            if (ph == 2 && !midShot)
            {
                float phi = ReadFloat(g, "dropPhi");
                if (phi > 2.8f && phi < 3.6f)
                {
                    Vector3 look = bp;
                    Shot(g.cam, g, $"turnaround/b10.25_{end}_midcircle_chase", null);
                    Shot(g.cam, g, $"polish/b10.25_{end}_midcircle_top", () =>
                    {
                        g.cam.fieldOfView = 55f;
                        g.cam.transform.position = look + Vector3.up * 110f;
                        g.cam.transform.LookAt(look);
                    });
                    shots += 2;
                    if (!tookCircle)
                    {
                        Shot(g.cam, g, "polish/b10.25_island_circle", () =>
                        {
                            g.cam.fieldOfView = 55f;
                            g.cam.transform.position = look + Vector3.up * 110f;
                            g.cam.transform.LookAt(look);
                        });
                        tookCircle = true; shots++;
                    }
                    midShot = true;
                    summary.AppendLine($"midcircle {end}: islandM={islandM:0.0} phi={phi:0.00} boat={bp}");
                }
            }
            // Next pass after a completed drop
            if (!tookNext && ends >= 1 && g.mode == SkiViewGame.Mode.Pass && prevMode != SkiViewGame.Mode.Pass && ph == 0)
            {
                Shot(g.cam, g, "polish/b10.25_next_pass", null);
                tookNext = true; shots++;
                summary.AppendLine($"next_pass: boat={bp} rope={g.sim.ropeIndex}");
            }
            prevMode = g.mode;
            if (ph == 0) lastPh = 0;
        }

        bool pass = !bugIsland && !bugBeach && !bugMouth && tookMouth && letgoIsland >= 48f && letgoIsland <= 70f;
        summary.AppendLine($"shots={shots} ends={ends} letgoIsland={letgoIsland:0.0} bugIsland={bugIsland} bugBeach={bugBeach} bugMouth={bugMouth}");
        summary.AppendLine($"plates: mouth={tookMouth} pullout={tookPull} float={tookFloat} circle={tookCircle} next={tookNext}");
        summary.AppendLine(pass
            ? "RESULT: PASS — mouth band (~48–70 m) + open-water circle (no island R35 / beach)"
            : "RESULT: FAIL — see bugMouth/bugIsland/bugBeach or missing James plates");
        File.WriteAllText(Path.Combine(OutDir, "polish/b10.25_pullout.txt"), log.ToString());
        File.WriteAllText(Path.Combine(OutDir, "turnaround/b10.25_dropmouth.txt"), log.ToString());
        File.WriteAllText(Path.Combine(OutDir, "polish/b10.25_summary.txt"), summary.ToString());
        Debug.Log("[SkiView] DropPullOutQA done\n" + summary);
    }
}
