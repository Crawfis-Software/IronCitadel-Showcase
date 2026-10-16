using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RailDebug
{
    public static string Run(string args)
    {
        Physics.SyncTransforms();
        var tally = new Dictionary<string, int>();
        int total = 0, vis = 0;
        var eyes = new List<Vector3>();
        for (float x = 1.4f; x <= 28.6f; x += 1f) eyes.Add(new Vector3(x, 6.88f, 23.75f - 0.2f - 0.5f));
        for (float x = 0.68f; x < 29.5f; x += 0.5f)
            for (float z = 24.0f; z < 39.4f; z += 0.5f)
            {
                var p = new Vector3(x, 0.05f, z); total++;
                bool seen = false; string first = null;
                foreach (var e in eyes)
                {
                    var d = p - e; float len = d.magnitude;
                    var hits = Physics.RaycastAll(e, d / len, len).OrderBy(h => h.distance).ToList();
                    if (hits.Count == 0) { seen = true; break; }
                    if (first == null) first = hits[0].collider.name + (hits[0].point.z < 24f ? "@rail" : "");
                }
                if (seen) vis++; else tally[first] = tally.TryGetValue(first, out var c) ? c + 1 : 1;
            }
        return $"{vis}/{total} " + string.Join(", ", tally.OrderByDescending(k => k.Value).Take(12).Select(k => k.Key + " " + k.Value));
    }
}
