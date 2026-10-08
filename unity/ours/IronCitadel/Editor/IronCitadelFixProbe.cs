using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>Diagnostic (-executeMethod IronCitadel.Rooms.Editor.IronCitadelFixProbe.Run): on the saved rooms scene,
    /// (a) the kitchen's ceiling as check 9 sees it, a 1.5 m map and the pieces above 6.5 m; (b) every piece of the
    /// great hall near the west and east landing edges (z -62 .. -58). Writes C:/Repos/IronCitadel/logs/rooms-fix-probe.txt.</summary>
    public static class IronCitadelFixProbe
    {
        public static void Run()
        {
            int code = 0;
            try { Probe(); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string PathOf(Transform t, Transform root)
        {
            var s = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        static string B(Bounds b) => $"x {b.min.x:0.00}..{b.max.x:0.00}, y {b.min.y:0.00}..{b.max.y:0.00}, z {b.min.z:0.00}..{b.max.z:0.00}";

        static void Probe()
        {
            EditorSceneManager.OpenScene(IronCitadelRoomsAssembler.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            var kitchen = GameObject.Find("IronCitadel/Rooms/kitchen").transform;
            var hall = GameObject.Find("IronCitadel/Rooms/great_hall").transform;

            // (a) the kitchen: the cell is x 45..90, z -90..-45
            sb.AppendLine($"kitchen at {kitchen.position} yaw {kitchen.eulerAngles.y}");
            sb.AppendLine("ceiling map, 1.5 m: '.' no floor, '5' lid <= 5.5, '6' <= 6.5, '8' <= 8, 'A' <= 10.5, 'H' higher, '-' no hit");
            int low = 0, all = 0;
            for (float z = -45.75f; z > -90f; z -= 1.5f)
            {
                var row = new StringBuilder($"{z,8:0.00} ");
                for (float x = 45.75f; x < 90f; x += 1.5f)
                {
                    if (!Physics.Raycast(new Vector3(x, 1.5f, z), Vector3.down, out var f, 2.5f, ~0, QueryTriggerInteraction.Ignore) || f.point.y > 1f)
                    { row.Append('.'); continue; }
                    all++;
                    var hits = Physics.RaycastAll(f.point + Vector3.up * 2.1f, Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore);
                    if (hits.Length == 0) { row.Append('-'); continue; }
                    float h = hits.Min(hh => hh.point.y) - f.point.y;
                    if (h <= 6.5f) low++;
                    row.Append(h <= 5.5f ? '5' : h <= 6.5f ? '6' : h <= 8f ? '8' : h <= 10.5f ? 'A' : 'H');
                }
                sb.AppendLine(row.ToString());
            }
            sb.AppendLine($"kitchen floor samples {all}, under 6.5 m {low} ({(all > 0 ? 100f * low / all : 0):0}%)");
            sb.AppendLine("kitchen pieces with a collider whose bottom is above 5.5 m (the lids and what stands on them):");
            foreach (var c in kitchen.GetComponentsInChildren<Collider>(true).Where(c => c.bounds.min.y > 5.5f).OrderBy(c => c.bounds.min.y).ThenBy(c => c.bounds.min.x))
                sb.AppendLine($"  {PathOf(c.transform, kitchen)} [{c.GetType().Name}; {B(c.bounds)}]");
            sb.AppendLine("kitchen renderers whose top is above 5.5 m, with no collider of their own:");
            foreach (var r in kitchen.GetComponentsInChildren<Renderer>(true).Where(r => r.bounds.max.y > 5.5f && r.GetComponent<Collider>() == null).OrderBy(r => r.bounds.min.y))
                sb.AppendLine($"  {PathOf(r.transform, kitchen)} [{B(r.bounds)}]");
            sb.AppendLine("kitchen pieces switched off, and every beam:");
            foreach (var t in kitchen.GetComponentsInChildren<Transform>(true).Where(t => (!t.gameObject.activeSelf && t.parent.gameObject.activeInHierarchy) || t.name.Contains("Beams")))
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                var rb = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }) : new Bounds(t.position, Vector3.zero);
                sb.AppendLine($"  {(t.gameObject.activeSelf ? "on " : "off")} {PathOf(t, kitchen)} [{rs.Length} renderer(s); {B(rb)}]");
            }
            sb.AppendLine("kitchen lights:");
            foreach (var l in kitchen.GetComponentsInChildren<Light>(true))
                sb.AppendLine($"  {PathOf(l.transform, kitchen)} at {l.transform.position.ToString("F2")} range {l.range:0.0}");

            // (b) the hall's landing edges
            sb.AppendLine();
            sb.AppendLine($"great_hall at {hall.position} yaw {hall.eulerAngles.y}");
            foreach (var (name, x0, x1) in new[] { ("west", 90f, 101f), ("east", 124f, 135f) })
            {
                sb.AppendLine($"{name} landing edge, x {x0}..{x1}, z -62 .. -58, y < 3:");
                foreach (var r in hall.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds;
                    if (b.max.x < x0 || b.min.x > x1 || b.max.z < -62f || b.min.z > -58f || b.min.y > 3f) continue;
                    var col = r.GetComponent<Collider>();
                    var t = r.transform;
                    sb.AppendLine($"  {PathOf(t, hall)} [{(col != null ? col.GetType().Name : "no collider")}; {B(b)}; pos {t.position.ToString("F2")} yaw {t.eulerAngles.y:0} scale {t.lossyScale.ToString("F2")}]");
                }
            }
            // the two landing edges, from the side hall, at eye height
            var cap = IronCitadelRoomsAssembler.OutRoot + "/captures/rooms/";
            IronCitadelCapture.Perspective(cap + "landing-edge-west.png", new Vector3(92.5f, 1.6f, -55.5f), 140f, 0f, 70f, 1280, 720, false);
            IronCitadelCapture.Perspective(cap + "landing-edge-east.png", new Vector3(132.5f, 1.6f, -55.5f), 220f, 0f, 70f, 1280, 720, false);
            Directory.CreateDirectory(IronCitadelRoomsAssembler.OutRoot + "/logs");
            File.WriteAllText(IronCitadelRoomsAssembler.OutRoot + "/logs/rooms-fix-probe.txt", sb.ToString());
        }
    }
}
