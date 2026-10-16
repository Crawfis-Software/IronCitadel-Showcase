using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProbeBounds
{
    public static string Run()
    {
        var sb = new StringBuilder();
        string[] roots = { "Assets/Synty/PolygonDungeon/Prefabs", "Assets/Synty/PolygonDungeonRealms/Prefabs", "Assets/Synty/PolygonDungeonMap/Prefabs", "Assets/Synty/PolygonGeneric/Prefabs" };
        var guids = AssetDatabase.FindAssets("t:Prefab", roots);
        int n = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool has = false; Bounds b = new Bounds();
            foreach (var r in rends)
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            var cols = go.GetComponentsInChildren<Collider>(true);
            int mesh = 0, box = 0, other = 0;
            foreach (var c in cols) { if (c is MeshCollider) mesh++; else if (c is BoxCollider) box++; else other++; }
            var p = go.transform.position; // prefab root pos (usually zero)
            string rel = path.Replace("Assets/Synty/", "");
            if (has)
                sb.AppendFormat("{0}\tsize=({1:F2},{2:F2},{3:F2})\tmin=({4:F2},{5:F2},{6:F2})\tmax=({7:F2},{8:F2},{9:F2})\tcol=m{10}b{11}o{12}\trend={13}\n",
                    rel, b.size.x, b.size.y, b.size.z, b.min.x - p.x, b.min.y - p.y, b.min.z - p.z, b.max.x - p.x, b.max.y - p.y, b.max.z - p.z, mesh, box, other, rends.Length);
            else
                sb.AppendFormat("{0}\tNO_RENDERER\tcol=m{1}b{2}o{3}\n", rel, mesh, box, other);
            n++;
        }
        Directory.CreateDirectory("Tools");
        File.WriteAllText("Tools/prefab_bounds.txt", sb.ToString());
        return "wrote " + n + " entries";
    }
}
