using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronCitadel.EditorTools
{
    public static class Measure
    {
        /// <summary>Writes renderer bounds (prefab at origin, identity) for every prefab whose path contains one of the filters.</summary>
        public static string Run(string filtersCsv, string outFile)
        {
            var filters = filtersCsv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            var sb = new StringBuilder();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Synty" });
            int n = 0;
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var file = Path.GetFileNameWithoutExtension(p);
                if (!filters.Any(f => file.Contains(f))) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) continue;
                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { sb.AppendLine($"{p}\tNO RENDERERS"); continue; }
                Bounds b = new Bounds();
                bool first = true;
                foreach (var r in rs)
                {
                    if (r is ParticleSystemRenderer) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    Bounds rb;
                    if (mf != null && mf.sharedMesh != null)
                    {
                        // transform mesh bounds into prefab-root space
                        var mb = mf.sharedMesh.bounds;
                        var m = go.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                        rb = TransformBounds(m, mb);
                    }
                    else if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                    {
                        var m = go.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                        rb = TransformBounds(m, smr.sharedMesh.bounds);
                    }
                    else continue;
                    if (first) { b = rb; first = false; } else b.Encapsulate(rb);
                }
                var cols = go.GetComponentsInChildren<Collider>(true);
                string colInfo = string.Join(",", cols.Select(c => c.GetType().Name + (c is MeshCollider mc ? (mc.convex ? "(cvx)" : "") : "")).Distinct());
                var lights = go.GetComponentsInChildren<Light>(true).Length;
                var ps = go.GetComponentsInChildren<ParticleSystem>(true).Length;
                sb.AppendLine($"{file}\tsize=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2})\tmin=({b.min.x:F2},{b.min.y:F2},{b.min.z:F2})\tmax=({b.max.x:F2},{b.max.y:F2},{b.max.z:F2})\tcol={colInfo}\tlights={lights}\tps={ps}\tscale={go.transform.localScale}\t{p}");
                n++;
            }
            File.WriteAllText(outFile, sb.ToString());
            return $"measured {n}";
        }

        static Bounds TransformBounds(Matrix4x4 m, Bounds b)
        {
            var c = b.center; var e = b.extents;
            var corners = new Vector3[8];
            int i = 0;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                corners[i++] = m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(x, y, z)));
            var r = new Bounds(corners[0], Vector3.zero);
            for (int k = 1; k < 8; k++) r.Encapsulate(corners[k]);
            return r;
        }
    }
}

namespace IronCitadel.EditorTools
{
    public static class MeasureOpening
    {
        /// <summary>For a frame/archway prefab, estimates the clear opening: inner left/right at y=1 m and the soffit height at the centre.</summary>
        public static string Run(string namesCsv)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in namesCsv.Split(','))
            {
                var path = Lineup.FindPrefab(n.Trim());
                if (path == null) { sb.AppendLine(n + " missing"); continue; }
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
                var verts = new System.Collections.Generic.List<UnityEngine.Vector3>();
                foreach (var mf in go.GetComponentsInChildren<UnityEngine.MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var m = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    foreach (var v in mf.sharedMesh.vertices) verts.Add(m.MultiplyPoint3x4(v));
                }
                float minX = float.MaxValue, maxX = float.MinValue;
                foreach (var v in verts) { minX = System.Math.Min(minX, v.x); maxX = System.Math.Max(maxX, v.x); }
                float cx = (minX + maxX) / 2f;
                // scan x in 2 cm steps at y in [0.3,1.6]: occupied if any triangle... approximate with vertices within band
                float innerL = minX, innerR = maxX;
                foreach (var v in verts) if (v.y > 0.3f && v.y < 1.6f) { if (v.x < cx) innerL = System.Math.Max(innerL, v.x); else innerR = System.Math.Min(innerR, v.x); }
                float soffit = float.MaxValue;
                foreach (var v in verts) if (System.Math.Abs(v.x - cx) < 0.25f && v.y > 1.0f) soffit = System.Math.Min(soffit, v.y);
                sb.AppendLine($"{n}: x[{minX:F2},{maxX:F2}] centre {cx:F2} inner [{innerL:F2},{innerR:F2}] w={innerR - innerL:F2} soffit={soffit:F2}");
            }
            return sb.ToString();
        }
    }
}
