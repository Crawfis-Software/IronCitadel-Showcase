using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronCitadel.Editor
{
    /// <summary>Dumps the local-space bounds of every prefab under Assets/Synty to Tools/prefab_bounds.tsv.</summary>
    public static class PrefabCatalog
    {
        [MenuItem("Tools/Iron Citadel/Dump prefab bounds")]
        public static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("path\tminx\tminy\tminz\tmaxx\tmaxy\tmaxz\tcolliders\trenderers\tlights\tparticles");
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Synty" });
            int n = 0;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    inst.transform.position = Vector3.zero;
                    inst.transform.rotation = Quaternion.identity;
                    var rends = inst.GetComponentsInChildren<Renderer>(true);
                    bool has = false;
                    var b = new Bounds(Vector3.zero, Vector3.zero);
                    foreach (var r in rends)
                    {
                        if (r is ParticleSystemRenderer) continue;
                        if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                    }
                    int cols = inst.GetComponentsInChildren<Collider>(true).Length;
                    int lights = inst.GetComponentsInChildren<Light>(true).Length;
                    int ps = inst.GetComponentsInChildren<ParticleSystem>(true).Length;
                    sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0}\t{1:F3}\t{2:F3}\t{3:F3}\t{4:F3}\t{5:F3}\t{6:F3}\t{7}\t{8}\t{9}\t{10}",
                        path, b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z, cols, rends.Length, lights, ps));
                    n++;
                }
                finally { Object.DestroyImmediate(inst); }
            }
            Directory.CreateDirectory("Tools");
            File.WriteAllText("Tools/prefab_bounds.tsv", sb.ToString());
            Debug.Log("[PrefabCatalog] wrote " + n + " prefabs to Tools/prefab_bounds.tsv");
        }
    }
}
