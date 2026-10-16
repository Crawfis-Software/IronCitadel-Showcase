using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Measure
{
    // args: a folder or a list of prefab paths separated by ';'. Writes Notes/measure.txt
    public static string Run(string args)
    {
        var sb = new StringBuilder();
        var paths = new System.Collections.Generic.List<string>();
        foreach (var a in args.Split(';'))
        {
            var p = a.Trim();
            if (p.Length == 0) continue;
            if (AssetDatabase.IsValidFolder(p))
                paths.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { p }).Select(AssetDatabase.GUIDToAssetPath).Where(x => Path.GetDirectoryName(x).Replace('\\', '/') == p));
            else paths.Add(p);
        }
        foreach (var path in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { sb.AppendLine("MISSING " + path); continue; }
            Bounds b = new Bounds(); bool has = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var w = m.MultiplyPoint3x4(c);
                    if (!has) { b = new Bounds(w, Vector3.zero); has = true; } else b.Encapsulate(w);
                }
            }
            var cols = go.GetComponentsInChildren<Collider>(true);
            string colInfo = string.Join(",", cols.Select(c => c.GetType().Name.Replace("Collider", "") + (c.isTrigger ? "(T)" : "")).Distinct());
            var lights = go.GetComponentsInChildren<Light>(true).Length;
            var ps = go.GetComponentsInChildren<ParticleSystem>(true).Length;
            sb.AppendLine($"{Path.GetFileNameWithoutExtension(path)}\tmin({b.min.x:F2},{b.min.y:F2},{b.min.z:F2}) max({b.max.x:F2},{b.max.y:F2},{b.max.z:F2}) size({b.size.x:F2},{b.size.y:F2},{b.size.z:F2})\tcol[{colInfo}] L{lights} PS{ps} rootpos{go.transform.position}");
        }
        Directory.CreateDirectory("Notes");
        File.AppendAllText("Notes/measure.txt", sb.ToString());
        return "ok " + paths.Count;
    }
}
