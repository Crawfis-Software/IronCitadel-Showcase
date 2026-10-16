using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Measure
{
    public static string Run(string folderFilter)
    {
        var sb = new StringBuilder();
        string[] roots = {
            "Assets/Synty/PolygonHorrorMansion/Prefabs",
            "Assets/Synty/PolygonGeneric/Prefabs"
        };
        foreach (var root in roots)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(folderFilter) && !path.Contains(folderFilter)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var rends = go.GetComponentsInChildren<MeshRenderer>(true);
                if (rends.Length == 0) { sb.AppendLine(Path.GetFileNameWithoutExtension(path) + " (no renderer)"); continue; }
                // compute bounds in prefab local space using mesh bounds transformed
                Bounds b = new Bounds();
                bool init = false;
                foreach (var r in rends)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mb = mf.sharedMesh.bounds;
                    var m = go.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var p = m.MultiplyPoint3x4(c);
                        if (!init) { b = new Bounds(p, Vector3.zero); init = true; } else b.Encapsulate(p);
                    }
                }
                var cols = go.GetComponentsInChildren<Collider>(true);
                string colStr = string.Join(",", cols.Select(c => c.GetType().Name.Replace("Collider", "")).Distinct());
                var mats = rends.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct();
                sb.AppendLine(string.Format("{0} | min {1:F2},{2:F2},{3:F2} max {4:F2},{5:F2},{6:F2} | size {7:F2},{8:F2},{9:F2} | col:{10} | mats:{11}",
                    Path.GetFileNameWithoutExtension(path), b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z, b.size.x, b.size.y, b.size.z, colStr, string.Join(",", mats)));
            }
        }
        Directory.CreateDirectory("Notes");
        File.WriteAllText("Notes/measure_" + (string.IsNullOrEmpty(folderFilter) ? "all" : folderFilter.Replace("/", "_")) + ".txt", sb.ToString());
        return "ok " + sb.Length;
    }
}
