using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Reads kit meshes directly: tread levels (up-facing surfaces) of stairs. Writes Build/probe.txt.
public static class Probe
{
    static GameObject Load(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/Synty/PolygonDungeon/Prefabs" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }

    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var name in new[] { "SM_Env_Stairs_01", "SM_Env_Stairs_02", "SM_Env_Stairs_Large_01", "SM_Env_Steps_01" })
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Load(name));
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var ups = new List<Vector3>();
            var all = new List<Vector3>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.sharedMesh;
                var n = m.normals;
                for (int i = 0; i < m.vertexCount; i++)
                {
                    Vector3 w = mf.transform.TransformPoint(m.vertices[i]);
                    Vector3 wn = mf.transform.TransformDirection(n.Length > i ? n[i] : Vector3.up);
                    all.Add(w);
                    if (wn.y > 0.9f) ups.Add(w);
                }
            }
            Object.DestroyImmediate(go);
            var b = new Bounds(all[0], Vector3.zero);
            foreach (var p in all) b.Encapsulate(p);
            // tread levels: up-facing vertices grouped by height (1 cm)
            var groups = ups.GroupBy(p => Mathf.Round(p.y * 100f) / 100f).Where(g => g.Count() >= 6).OrderBy(g => g.Key).ToList();
            sb.AppendLine($"{name}: bounds {b.min} .. {b.max}");
            sb.AppendLine("  tread levels (y: count, z-range): " + string.Join("  ", groups.Select(g =>
                $"{g.Key:F2}:{g.Count()}[z {g.Min(p => p.z):F1}..{g.Max(p => p.z):F1} x {g.Min(p => p.x):F1}..{g.Max(p => p.x):F1}]")));
        }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/probe.txt", sb.ToString());
        return sb.ToString();
    }
}
