using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProbePieces
{
    static GameObject Spawn(string path, Vector3 pos, float yaw = 0)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        return go;
    }
    public static string Run()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string W = "Assets/Synty/PolygonDungeon/Prefabs/Environments/Walls/";
        string F = "Assets/Synty/PolygonDungeon/Prefabs/Environments/Floors/";
        // door frames: measure clear opening by raycasting along x at several heights, and up from the middle
        string[] frames = { "SM_Env_Wall_DoorFrame_01", "SM_Env_Wall_DoorFrame_Round_01", "SM_Env_Wall_DoorFrame_Double_01", "SM_Env_Wall_DoorFrame_Double_Round_01", "SM_Env_Wall_Archway_01", "SM_Env_Wall_Archway_02", "SM_Env_Door_Large_Frame_01", "SM_Env_Door_Large_Frame_02", "SM_Env_Wall_Fireplace_Gap_01", "SM_Env_Wall_Tunnel_Entrance_01" };
        int k = 0;
        foreach (var f in frames)
        {
            var go = Spawn(W + f + ".prefab", new Vector3(0, 0, k * 20));
            Physics.SyncTransforms();
            var b = new Bounds(); bool has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            // find the opening: scan x across the piece at y=1.0, find gap where a ray along z passes through
            float zc = b.center.z; float gapMin = float.NaN, gapMax = float.NaN;
            for (float x = b.min.x - 0.1f; x <= b.max.x + 0.1f; x += 0.02f)
            {
                bool blocked = Physics.Raycast(new Vector3(x, 1.0f, zc - 3f), Vector3.forward, 6f) || Physics.Raycast(new Vector3(x, 1.0f, zc + 3f), Vector3.back, 6f);
                if (!blocked) { if (float.IsNaN(gapMin)) gapMin = x; gapMax = x; }
            }
            float midx = (gapMin + gapMax) / 2f;
            // clear height: cast up from the floor at the gap middle
            float h = Physics.Raycast(new Vector3(midx, 0.05f, zc), Vector3.up, out var hit, 20f) ? hit.point.y : -1;
            // also clear height near the gap edges (arches)
            float hEdge = Physics.Raycast(new Vector3(gapMin + 0.35f, 0.05f, zc), Vector3.up, out var hit2, 20f) ? hit2.point.y : -1;
            sb.AppendFormat("{0}: bounds x[{1:F2},{2:F2}] z[{3:F2},{4:F2}] y[{5:F2},{6:F2}]  opening x[{7:F2},{8:F2}] width {9:F2} mid {10:F2}  clearH mid {11:F2} at 0.35 from edge {12:F2}\n",
                f, b.min.x, b.max.x, b.min.z, b.max.z, b.min.y, b.max.y, gapMin, gapMax, gapMax - gapMin, midx, h, hEdge);
            k++;
        }
        // stairs: sample surface height along the run
        string[] stairs = { F + "SM_Env_Stairs_01.prefab", F + "SM_Env_Stairs_02.prefab", F + "SM_Env_Steps_01.prefab", "Assets/Synty/PolygonDungeonRealms/Prefabs/Environments/SM_Env_Dwarf_Stairs_01.prefab", "Assets/Synty/PolygonDungeonRealms/Prefabs/Environments/SM_Env_Dwarf_Stairs_Half_01.prefab", "Assets/Synty/PolygonDungeonRealms/Prefabs/Environments/SM_Env_Dwarf_Stairs_Small_01.prefab", W + "SM_Env_Ceiling_Stone_Stairs_01.prefab" };
        foreach (var s in stairs)
        {
            var go = Spawn(s, new Vector3(100, 0, k * 20));
            Physics.SyncTransforms();
            var b = new Bounds(); bool has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            sb.AppendFormat("{0}: bounds x[{1:F2},{2:F2}] y[{3:F2},{4:F2}] z[{5:F2},{6:F2}]\n", System.IO.Path.GetFileNameWithoutExtension(s), b.min.x - 100, b.max.x - 100, b.min.y, b.max.y, b.min.z - k * 20, b.max.z - k * 20);
            float xc = b.center.x;
            var line = new StringBuilder("  z-profile(x=centre): ");
            float prev = float.NaN; float maxStep = 0;
            for (float z = b.min.z; z <= b.max.z; z += 0.1f)
            {
                float y = Physics.Raycast(new Vector3(xc, b.max.y + 1f, z), Vector3.down, out var hit, 30f) ? hit.point.y : float.NaN;
                if (!float.IsNaN(prev) && !float.IsNaN(y)) maxStep = Mathf.Max(maxStep, Mathf.Abs(y - prev));
                prev = y;
                if (Mathf.Abs(z * 10 - Mathf.Round(z * 10)) < 0.01f && Mathf.RoundToInt(z * 10) % 5 == 0) line.AppendFormat("z{0:F1}:{1:F2} ", z - k * 20, y);
            }
            line.AppendFormat(" maxStep(0.1m samples)={0:F2}", maxStep);
            sb.AppendLine(line.ToString());
            var line2 = new StringBuilder("  x-profile(z=centre): ");
            float zc = b.center.z; prev = float.NaN; maxStep = 0;
            for (float x = b.min.x; x <= b.max.x; x += 0.1f)
            {
                float y = Physics.Raycast(new Vector3(x, b.max.y + 1f, zc), Vector3.down, out var hit, 30f) ? hit.point.y : float.NaN;
                if (!float.IsNaN(prev) && !float.IsNaN(y)) maxStep = Mathf.Max(maxStep, Mathf.Abs(y - prev));
                prev = y;
                if (Mathf.Abs(x * 10 - Mathf.Round(x * 10)) < 0.01f && Mathf.RoundToInt(x * 10) % 5 == 0) line2.AppendFormat("x{0:F1}:{1:F2} ", x - 100, y);
            }
            line2.AppendFormat(" maxStep={0:F2}", maxStep);
            sb.AppendLine(line2.ToString());
            k++;
        }
        // wall face: which z side has more vertices (detail)?
        foreach (var w in new[] { "SM_Env_Wall_01", "SM_Env_Wall_03", "SM_Env_Wall_DoorFrame_01", "SM_Env_Wall_Archway_01" })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(W + w + ".prefab");
            int front = 0, back = 0;
            foreach (var mf in p.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; if (m == null) continue;
                var verts = m.vertices; var norms = m.normals;
                for (int i = 0; i < verts.Length; i++) { var n = mf.transform.TransformDirection(norms[i]); if (n.z > 0.5f) front++; else if (n.z < -0.5f) back++; }
            }
            sb.AppendFormat("{0}: normals +z {1}, -z {2}\n", w, front, back);
        }
        System.IO.File.WriteAllText("Tools/probe_pieces.txt", sb.ToString());
        return sb.ToString();
    }
}
