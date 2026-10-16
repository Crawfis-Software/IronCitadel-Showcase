using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Showcase
{
    const string W = "Assets/Synty/PolygonDungeon/Prefabs/Environments/Walls/";
    const string F = "Assets/Synty/PolygonDungeon/Prefabs/Environments/Floors/";
    const string M = "Assets/Synty/PolygonDungeon/Prefabs/Environments/Misc/";
    const string P = "Assets/Synty/PolygonDungeon/Prefabs/Environments/Pillars/";
    static GameObject Spawn(string path, Vector3 pos, float yaw = 0, string name = null)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (p == null) { Debug.LogError("missing " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        if (name != null) go.name = name;
        return go;
    }
    public static string Run()
    {
        var sb = new StringBuilder();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        // floor strip
        for (int i = 0; i < 12; i++) for (int j = 0; j < 3; j++) Spawn(F + "SM_Env_Tiles_01.prefab", new Vector3(i * 5 + 5, 0, j * 5 - 5));
        // row of walls facing +z (camera looks from +z toward -z)
        Spawn(W + "SM_Env_Wall_01.prefab", new Vector3(5, 0, 0), 0, "Wall_01");
        var df = Spawn(W + "SM_Env_Wall_DoorFrame_01.prefab", new Vector3(10, 0, 0), 0, "DoorFrame_01");
        Spawn(W + "SM_Env_Wall_DoorFrame_Double_01.prefab", new Vector3(15, 0, 0), 0, "DoorFrame_Double_01");
        Spawn(W + "SM_Env_Wall_Archway_01.prefab", new Vector3(20, 0, 0), 0, "Archway_01");
        Spawn(W + "SM_Env_Wall_Archway_02.prefab", new Vector3(25, 0, 0), 0, "Archway_02");
        Spawn(W + "SM_Env_Wall_Window_01.prefab", new Vector3(30, 0, 0), 0, "Window_01");
        Spawn(W + "SM_Env_Wall_DoorFrame_Round_01.prefab", new Vector3(35, 0, 0), 0, "DoorFrame_Round_01");
        Spawn(W + "SM_Env_Wall_DoorFrame_Double_Round_01.prefab", new Vector3(40, 0, 0), 0, "DoorFrame_Double_Round_01");
        Spawn(W + "SM_Env_Wall_03.prefab", new Vector3(45, 0, 0), 0, "Wall_03");
        Spawn(W + "SM_Env_Wall_Tunnel_Entrance_01.prefab", new Vector3(50, 0, 0), 0, "Tunnel_Entrance_01");
        Spawn(W + "SM_Env_Wall_Fireplace_Gap_01.prefab", new Vector3(55, 0, 0), 0, "Fireplace_Gap_01");
        Spawn(W + "SM_Env_Wall_Alcove_Small_01.prefab", new Vector3(60, 0, 0), 0, "Alcove_Small_01");
        // second row at z=10: stairs & misc
        Spawn(F + "SM_Env_Stairs_01.prefab", new Vector3(0, 0, 12), 0, "Stairs_01");
        Spawn(F + "SM_Env_Stairs_02.prefab", new Vector3(6, 0, 12), 0, "Stairs_02");
        Spawn(W + "SM_Env_Ceiling_Stone_Stairs_01.prefab", new Vector3(17, 0, 7), 0, "Ceiling_Stone_Stairs_01");
        Spawn(F + "SM_Env_Steps_01.prefab", new Vector3(23, 0, 7), 0, "Steps_01");
        Spawn(M + "SM_Env_Railing_01.prefab", new Vector3(29, 0, 8), 0, "Railing_01");
        Spawn(M + "SM_Env_Railing_02.prefab", new Vector3(34, 0, 8), 0, "Railing_02");
        Spawn(P + "SM_Env_Pillar_Square_06.prefab", new Vector3(36, 0, 9), 0, "Pillar_Square_06");
        Spawn(P + "SM_Env_Pillar_Round_01.prefab", new Vector3(38, 0, 9), 0, "Pillar_Round_01");
        Spawn(P + "SM_Env_Pillar_Square_02.prefab", new Vector3(40, 0, 9), 0, "Pillar_Square_02");
        Spawn(W + "SM_Env_Door_Large_Frame_02.prefab", new Vector3(45, 0, 9), 0, "Door_Large_Frame_02");
        Spawn(W + "SM_Env_Door_Large_Frame_01.prefab", new Vector3(52, 0, 9), 0, "Door_Large_Frame_01");
        Spawn(W + "SM_Env_Door_03.prefab", new Vector3(57, 0, 9), 0, "Door_03");
        Spawn(W + "SM_Env_Door_01.prefab", new Vector3(59, 0, 9), 0, "Door_01");
        Spawn(W + "SM_Env_Door_Bars_01.prefab", new Vector3(61, 0, 9), 0, "Door_Bars_01");
        Spawn(W + "SM_Env_Ceiling_Stone_Flat_01.prefab", new Vector3(12, 5, 2), 0, "Ceiling_Flat_01");
        Spawn(W + "SM_Env_Ceiling_Stone_Flat_02.prefab", new Vector3(17, 5, 2), 0, "Ceiling_Flat_02");
        Spawn(W + "SM_Env_Ceiling_Stone_Flat_03.prefab", new Vector3(22, 5, 2), 0, "Ceiling_Flat_03");
        Spawn(W + "SM_Env_Ceiling_Stone_Flat_04.prefab", new Vector3(27, 5, 2), 0, "Ceiling_Flat_04");
        Spawn(W + "SM_Env_Ceiling_Stone_Flat_05.prefab", new Vector3(32, 5, 2), 0, "Ceiling_Flat_05");
        Spawn(W + "SM_Env_Ceiling_Stone_Curved_01.prefab", new Vector3(42, 5, 2), 0, "Ceiling_Curved_01");
        Physics.SyncTransforms();
        // measure door openings with up-rays
        foreach (var name in new[] { "DoorFrame_01", "DoorFrame_Double_01", "DoorFrame_Round_01", "DoorFrame_Double_Round_01", "Archway_01", "Archway_02", "Tunnel_Entrance_01", "Fireplace_Gap_01", "Alcove_Small_01" })
        {
            var go = GameObject.Find(name); var o = go.transform.position;
            float gmin = float.NaN, gmax = float.NaN, hmin = 99;
            for (float x = -5.2f; x <= 0.2f; x += 0.02f)
            {
                var org = new Vector3(o.x + x, 0.3f, o.z + 0.05f);
                bool blockedLow = Physics.Raycast(new Vector3(o.x + x, 0.3f, o.z - 2f), Vector3.forward, 4f, ~0, QueryTriggerInteraction.Ignore);
                // use overlap: a small sphere at waist height inside the wall plane
                bool solid = Physics.CheckSphere(new Vector3(o.x + x, 1.0f, o.z + 0.05f), 0.05f);
                if (!solid) { if (float.IsNaN(gmin)) gmin = x; gmax = x; float h = Physics.Raycast(org, Vector3.up, out var hit, 10f) ? hit.point.y : 99; if (h < hmin) hmin = h; }
            }
            sb.AppendFormat("{0}: open local x[{1:F2},{2:F2}] width {3:F2} mid {4:F2} min clear height {5:F2}\n", name, gmin, gmax, gmax - gmin, (gmin + gmax) / 2, hmin);
        }
        // stair top surface profiles
        foreach (var name in new[] { "Stairs_01", "Stairs_02", "Ceiling_Stone_Stairs_01", "Steps_01" })
        {
            var go = GameObject.Find(name); var o = go.transform.position;
            var line = new StringBuilder(name + " profile (local z: y): ");
            for (float z = -5.5f; z <= 5.5f; z += 0.25f)
            {
                float y = Physics.Raycast(new Vector3(o.x + 2.5f * (name == "Stairs_01" || name == "Stairs_02" ? 1 : -1), 10, o.z + z), Vector3.down, out var hit, 20f) ? hit.point.y : float.NaN;
                if (!float.IsNaN(y)) line.AppendFormat("{0:F2}:{1:F2} ", z, y);
            }
            sb.AppendLine(line.ToString());
        }
        // camera
        var cam = Camera.main; cam.transform.position = new Vector3(33, 6, 30); cam.transform.rotation = Quaternion.Euler(12, 180, 0); cam.fieldOfView = 70; cam.farClipPlane = 500;
        var light = GameObject.Find("Directional Light"); if (light) light.transform.rotation = Quaternion.Euler(50, 200, 0);
        System.IO.File.WriteAllText("Tools/showcase.txt", sb.ToString());
        return sb.ToString();
    }
    public static string Cam(string args)
    {
        // args: "x,y,z,pitch,yaw"
        var a = args.Split(','); var cam = Camera.main;
        cam.transform.position = new Vector3(float.Parse(a[0]), float.Parse(a[1]), float.Parse(a[2]));
        cam.transform.rotation = Quaternion.Euler(float.Parse(a[3]), float.Parse(a[4]), 0);
        return "ok";
    }
}
