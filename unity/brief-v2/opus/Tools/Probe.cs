using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Probe
{
    // Instantiates prefabs far away, probes openings (walls) and step profiles (stairs) with raycasts.
    public static string Run(string args)
    {
        var sb = new StringBuilder();
        Physics.simulationMode = SimulationMode.Script;
        Vector3 basePos = new Vector3(1000, 0, 1000);
        foreach (var raw in args.Split(';'))
        {
            var spec = raw.Trim(); if (spec.Length == 0) continue;
            var parts = spec.Split('|');
            string path = parts[0]; string mode = parts.Length > 1 ? parts[1] : "wall";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { sb.AppendLine("MISSING " + path); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = basePos;
            Physics.SyncTransforms();
            sb.AppendLine("== " + Path.GetFileNameWithoutExtension(path) + " " + mode);
            if (mode == "wall")
            {
                // wall runs along x from -5..0 at z=0; probe rays along z at various x,y
                for (float y = 0.2f; y <= 5.0f; y += 0.4f)
                {
                    var line = new StringBuilder();
                    for (float x = -5.0f; x <= 0.01f; x += 0.1f)
                    {
                        var o = basePos + new Vector3(x, y, -2f);
                        bool hit = Physics.Raycast(o, Vector3.forward, 4f);
                        line.Append(hit ? '#' : '.');
                    }
                    sb.AppendLine($"y{y:F1} {line}");
                }
            }
            else if (mode == "wallc") // centred wall/frame (-2.5..2.5)
            {
                for (float y = 0.2f; y <= 5.0f; y += 0.2f)
                {
                    var line = new StringBuilder();
                    for (float x = -3.0f; x <= 3.01f; x += 0.1f)
                    {
                        var o = basePos + new Vector3(x, y, -2f);
                        bool hit = Physics.Raycast(o, Vector3.forward, 4f);
                        line.Append(hit ? '#' : '.');
                    }
                    sb.AppendLine($"y{y:F1} {line}");
                }
            }
            else if (mode.StartsWith("stairs"))
            {
                // cast down along an axis, report top surface heights
                // stairsX: run along x; stairsZ: run along z (negative); at lateral middle
                var b = new Bounds(); bool has = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
                sb.AppendLine("bounds " + b.min + " " + b.max);
                bool alongX = mode == "stairsX";
                float lat = alongX ? b.center.z : b.center.x;
                var line = new StringBuilder();
                float prev = float.NaN;
                for (float t = 0; t <= 1.0001f; t += 0.01f)
                {
                    float a = alongX ? Mathf.Lerp(b.min.x, b.max.x, t) : Mathf.Lerp(b.min.z, b.max.z, t);
                    var o = alongX ? new Vector3(a, b.max.y + 1, lat) : new Vector3(lat, b.max.y + 1, a);
                    if (Physics.Raycast(o, Vector3.down, out var h, 20f))
                    {
                        float yy = h.point.y - basePos.y;
                        if (float.IsNaN(prev) || Mathf.Abs(yy - prev) > 0.02f) line.Append($"[{(a - (alongX ? basePos.x : basePos.z)):F2}:{yy:F2}]");
                        prev = yy;
                    }
                    else { line.Append($"[{a:F2}:none]"); prev = float.NaN; }
                }
                sb.AppendLine(line.ToString());
            }
            Object.DestroyImmediate(go);
        }
        Physics.simulationMode = SimulationMode.FixedUpdate;
        File.AppendAllText("Notes/probe.txt", sb.ToString());
        return "ok";
    }
}
