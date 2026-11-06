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
        Check(SkiViewVestQaLogic.Role("Panel", "Cube", "VestWet_C125") == "vest", "material name still counts");

        Check(SkiViewVestQaLogic.ParseSub("2.0", out int rid, out int sub) && rid == 2 && sub == 0, "vestsub parser");
        Check(!SkiViewVestQaLogic.ParseSub("2", out _, out _), "vestsub needs a dot");
        Check(SkiViewVestQaLogic.ParseId("4") == 4 && SkiViewVestQaLogic.ParseId(null) == -1, "vestid parser");

        var url = SkiViewVestQaLogic.ArgsFromUrl("http://127.0.0.1:8099/lake=skiview&skier=male&at=gate&cam=face&vestqa=1&pose=left&vestshade=normals");
        Check(url["vestqa"] == "1" && url["pose"] == "left" && url["vestshade"] == "normals" && url["cam"] == "face" && url["skier"] == "male", "path-style capture url");
        var q = SkiViewVestQaLogic.ArgsFromUrl("http://127.0.0.1:8099/index.html?lake=skiview&vestqa=1&vestid=3");
        Check(q["vestqa"] == "1" && q["vestid"] == "3", "query-style url");

        var rows = SkiViewVestQaLogic.AssignIds(new List<SkiViewVestQaLogic.Entry>
        {
            new SkiViewVestQaLogic.Entry { path = "Skier/Gear/Shorts", mesh = "Shorts", role = "other", submeshes = 1, materials = "M_125_Shorts", triangles = 10, enabled = true },
            new SkiViewVestQaLogic.Entry { path = "Skier/Gear/Vest_LOD0", mesh = "Vest_LOD0", role = "vest", submeshes = 1, materials = "M_125_Vest/Universal Render Pipeline/Lit", triangles = 1791, enabled = true, readable = true }
        });
        Check(rows.Count == 2 && rows[0].path.EndsWith("Shorts") && rows[0].id == 0 && rows[1].id == 1, "ids follow path order");
        string line = SkiViewVestQaLogic.LegendLine(rows[1]);
        Check(line.Contains("id=1") && line.Contains("role=vest") && line.Contains("sub=1") && line.Contains("tris=1791") && line.Contains("M_125_Vest") && line.Contains("Vest_LOD0"), "legend names mesh and material");

        Debug.Log("[VestQaTest] " + fails + " failure(s)");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 3);
    }
}
