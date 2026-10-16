using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Measures every prefab under the given folders (root-local bounds, instantiated at the origin) and writes Build/kit.txt.
public static class Measure
{
    public static string Run()
    {
        var sb = new StringBuilder();
        string[] folders = { "Assets/Synty/PolygonDungeon/Prefabs", "Assets/Synty/PolygonDungeonRealms/Prefabs" };
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) continue;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            inst.transform.localScale = Vector3.one;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            int cols = inst.GetComponentsInChildren<Collider>(true).Length;
            int lights = inst.GetComponentsInChildren<Light>(true).Length;
            if (any)
                sb.AppendLine($"{path.Substring(path.LastIndexOf('/') + 1).Replace(".prefab", "")}\t{b.size.x:F2}\t{b.size.y:F2}\t{b.size.z:F2}\tmin {b.min.x:F2},{b.min.y:F2},{b.min.z:F2}\tmax {b.max.x:F2},{b.max.y:F2},{b.max.z:F2}\tcol {cols}\tlight {lights}\t{path.Substring(0, path.LastIndexOf('/'))}");
            else
                sb.AppendLine($"{path.Substring(path.LastIndexOf('/') + 1).Replace(".prefab", "")}\tNO_RENDERER\tcol {cols}\tlight {lights}\t{path.Substring(0, path.LastIndexOf('/'))}");
            Object.DestroyImmediate(inst);
        }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/kit.txt", sb.ToString());
        return "wrote Build/kit.txt lines=" + sb.ToString().Split('\n').Length;
    }
}
