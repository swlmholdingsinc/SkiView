using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SkiView;

/// <summary>
/// Collar QA rules. No play mode and no mesh edits.
/// Run: unity_run.sh SkiViewVestQaTest.Run
/// </summary>
public static class SkiViewVestQaTest
{
    public static void Run()
    {
        int fails = 0;
        void Check(bool ok, string what)
        {
            Debug.Log("[VestQaTest] " + (ok ? "PASS" : "FAIL") + " " + what);
            if (!ok) fails++;
        }

        Check(!SkiViewVestQaLogic.Requested(null, null, null, null, null), "absent args stay off");
        Check(!SkiViewVestQaLogic.Requested("0", "", "", "", ""), "vestqa=0 stays off");
        Check(SkiViewVestQaLogic.Requested("1", null, null, null, null), "vestqa=1 opts in");
        Check(SkiViewVestQaLogic.Requested(null, "normals", null, null, null), "vestshade opts in");
        Check(SkiViewVestQaLogic.Requested(null, null, "left", null, null), "pose opts in");
        Check(!SkiViewVestQaLogic.Requested(null, null, "nope", null, null), "unknown pose does not opt in");

        Check(float.IsNaN(SkiViewVestQaLogic.PoseRollDegrees(null)), "no pose");
        Check(Mathf.Approximately(SkiViewVestQaLogic.PoseRollDegrees("left"), 35f), "left is +35");
        Check(Mathf.Approximately(SkiViewVestQaLogic.PoseRollDegrees("right"), -35f), "right is -35");
        Check(Mathf.Approximately(SkiViewVestQaLogic.PoseRollDegrees("center"), 0f), "center is 0");
        Check(SkiViewVestQaLogic.PoseRollDegrees("left") != SkiViewVestQaLogic.PoseRollDegrees("right"), "left and right differ");

        Check(SkiViewVestQaLogic.Shade(null) == "lit", "default shade is lit");
        Check(SkiViewVestQaLogic.Shade("unlit") == "unlit", "unlit");
        Check(SkiViewVestQaLogic.Shade("normals") == "normals", "normals");
        Check(SkiViewVestQaLogic.Shade("tangents") == "tangents", "tangents");
        Check(SkiViewVestQaLogic.Shade("ids") == "ids", "ids");
        Check(SkiViewVestQaLogic.ShadeMode("normals") == 1 && SkiViewVestQaLogic.ShadeMode("tangents") == 2 && SkiViewVestQaLogic.ShadeMode("unlit") == 0, "shader mode ids");

        Check(SkiViewVestQaLogic.Role("Vest_LOD0", "SkiView_Gear_Slalom_125", "M_125_Vest") == "vest", "gear vest name");
        Check(SkiViewVestQaLogic.Role("Body_LOD0", "Body", "SetA") == "other", "body is not a vest");
        Check(SkiViewVestQaLogic.Role("Panel", "Cube", "VestWet_C125") == "vest", "gameplay vest material still counts");
        Check(SkiViewVestQaLogic.Role("Body_LOD0", "Body", "VestQa_22_0") == "other", "old QA material name is not a vest");
        Check(SkiViewVestQaLogic.Role("Body_LOD0", "Body", "QaShade_-200_0/Hidden") == "other", "shade material name is not a vest");
        Check(SkiViewVestQaLogic.Role("INTERNAL_ONLY_125_Gear_Vest_LOD0", "INTERNAL_ONLY_125_Gear_Vest_LOD0", "QaShade_1_0") == "vest", "object name stays vest after a shade swap");

        Check(SkiViewVestQaLogic.ParseSub("2.0", out int rid, out int sub) && rid == 2 && sub == 0, "vestsub parser");
        Check(!SkiViewVestQaLogic.ParseSub("2", out _, out _), "vestsub needs a dot");
        Check(SkiViewVestQaLogic.ParseId("4") == 4 && SkiViewVestQaLogic.ParseId(null) == -1, "vestid parser");

        var url = SkiViewVestQaLogic.ArgsFromUrl("http://127.0.0.1:8099/lake=skiview&skier=male&at=gate&cam=face&vestqa=1&pose=left&vestshade=normals");
        Check(url["vestqa"] == "1" && url["pose"] == "left" && url["vestshade"] == "normals" && url["cam"] == "face" && url["skier"] == "male", "path-style capture url");
        var q = SkiViewVestQaLogic.ArgsFromUrl("http://127.0.0.1:8099/index.html?lake=skiview&vestqa=1&vestid=3");
        Check(q["vestqa"] == "1" && q["vestid"] == "3", "query-style url");

        var rows = SkiViewVestQaLogic.OrderForDisplay(new List<SkiViewVestQaLogic.Entry>
        {
            new SkiViewVestQaLogic.Entry { id = -100, path = "Skier/Body", mesh = "Body", role = "overlap", submeshes = 1, materials = "SetA", enabled = true, active = true },
            new SkiViewVestQaLogic.Entry { id = -200, path = "Skier/Gear_Vest/SkinnedMeshRenderer", mesh = "INTERNAL_ONLY_125_Gear_Vest_LOD0", role = "vest", submeshes = 1, materials = "M_125_Vest", triangles = -1, enabled = true, active = true, readable = false },
            new SkiViewVestQaLogic.Entry { id = -300, path = "Skier/Gear_Vest/SkinnedMeshRenderer", mesh = "INTERNAL_ONLY_125_Gear_Vest_LOD0", role = "vest", submeshes = 1, materials = "M_125_Vest", enabled = true, active = true, readable = false }
        });
        Check(rows.Count == 3 && rows[0].role == "vest" && rows[1].role == "vest" && rows[2].id == -100, "enabled vests are listed before overlap");
        Check((rows[0].id == -200 && rows[1].id == -300) || (rows[0].id == -300 && rows[1].id == -200), "same path keeps both instance ids");
        Check(rows[0].pathShared == 2 && rows[1].pathShared == 2 && rows[2].pathShared == 1, "shared path is counted and not merged");
        var more = new List<SkiViewVestQaLogic.Entry>(rows);
        more.Add(new SkiViewVestQaLogic.Entry { id = -400, path = "Skier/Eye", mesh = "Eye", role = "overlap", enabled = true });
        var again = SkiViewVestQaLogic.OrderForDisplay(more);
        bool vestKept = false;
        for (int i = 0; i < again.Count; i++) if (again[i].id == -200 && again[i].role == "vest") vestKept = true;
        Check(vestKept && again[0].id != 0, "adding an overlap row does not renumber the vest");
        string line = "";
        for (int i = 0; i < again.Count; i++) if (again[i].id == -200) line = SkiViewVestQaLogic.CompactLine(again[i]);
        Check(line.Contains("inst=-200") && line.Contains("tris=unread") && line.Contains("pathShared=2") && line.Contains("INTERNAL_ONLY_125_Gear_Vest_LOD0"), "compact line uses the instance id");

        string beat = SkiViewVestQaLogic.Heartbeat(0, "skiview", "waiting for skier", null);
        Check(beat.Length > 0 && beat.Contains("renderers=0") && beat.Contains("scene=skiview") && beat.Contains("waiting for skier"), "zero renderers still produce a heartbeat");
        string err = SkiViewVestQaLogic.Heartbeat(0, "", "scan error", "NullReferenceException: skier");
        Check(err.Contains("renderers=0") && err.Contains("scene=(none)") && err.Contains("error=NullReferenceException"), "init and error stay visible");

        Debug.Log("[VestQaTest] " + fails + " failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
