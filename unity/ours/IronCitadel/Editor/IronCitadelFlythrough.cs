using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>A cinematic flythrough of the rooms level, rendered offline frame by frame.
    ///
    /// The shot list is a JSON file (default C:/Repos/IronCitadel/flythrough/shots.json). A "path" shot names
    /// via points; the camera follows the NavMesh path between them (baked at the walker's size, with the
    /// vault's bookcase swung open), pushed toward the middle of narrow passages, smoothed, and timed so that it
    /// speeds through halls and slows in rooms and on turns. A "keys" shot is a Catmull-Rom crane through
    /// explicit keys. The tool writes JPG frames, poses.json (every frame's pose and each shot's path, for the
    /// compositor's minimap) and a top-down map. Captions, the minimap and the encode happen in Python.
    ///
    /// -executeMethod IronCitadel.Rooms.Editor.IronCitadelFlythrough.Run [-shots file] [-out dir] [-preview]
    ///     [-only id,id] [-every n] [-noMap] [-resume] [-maxFrames n]
    /// -preview renders every nth frame (default 45) at 960x540 to out/preview/ instead of every frame.
    /// A full-size film runs the editor out of memory: render it in chunks with -resume -maxFrames
    /// (C:/Repos/IronCitadel/flythrough/render-chunks.sh rooms). Do not change the shot file between chunks: a
    /// resumed shot keeps the frames already on disk.</summary>
    public static class IronCitadelFlythrough
    {
        const string DefaultOut = "C:/Repos/IronCitadel/flythrough";
        const int AllLayers = ~0;
        const float Step = 0.25f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ data

        sealed class Pose
        {
            public Vector3 pos; public float yaw, pitch, fov, book; public bool ceilings = true, moon;
        }

        sealed class Sample
        {
            public Vector3 p; public float width = 99f, h, wait; public string evt;
            public Vector3? look; public bool stop;
        }

        sealed class Shot
        {
            public string id, type; public JObject j;
            public List<Pose> poses = new List<Pose>();
            public List<Vector3> path = new List<Vector3>();
            public float length, seconds; public int bumps; public string note = "";
        }

        static IronCitadelRoomsAssembler.Level s_level;
        static GameObject s_root;
        static NavMeshQueryFilter s_filter;
        static readonly Dictionary<string, Vector3> s_eyes = new Dictionary<string, Vector3>();
        static readonly StringBuilder s_log = new StringBuilder();

        // ------------------------------------------------------------------ entry

        public static void Run()
        {
            int code = 0;
            try { RunCore(); }
            catch (Exception e) { Debug.LogException(e); s_log.AppendLine("EXCEPTION " + e); code = 1; }
            Debug.Log("[Flythrough] done, exit " + code + "\n" + s_log);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string Arg(string name, string def = null)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }

        static bool Flag(string name) => Environment.GetCommandLineArgs().Contains(name);

        static void RunCore()
        {
            string outDir = Arg("-out", DefaultOut);
            string shotsPath = Arg("-shots", outDir + "/shots.json");
            bool preview = Flag("-preview");
            int every = int.Parse(Arg("-every", "45"), Inv);
            var only = Arg("-only")?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToHashSet();
            var spec = JObject.Parse(File.ReadAllText(shotsPath));

            var scene = EditorSceneManager.OpenScene(IronCitadelRoomsAssembler.ScenePath, OpenSceneMode.Single);
            s_root = scene.GetRootGameObjects().First(g => g.name == IronCitadelRoomsAssembler.RootName);
            var play = s_root.transform.Find("Play");
            if (play != null) play.gameObject.SetActive(false);
            s_level = IronCitadelRoomsAssembler.LoadLevel();
            ReadEyes();

            // the bookcase swung open (colliders off) while the paths are planned, closed again to render
            var sdoor = s_root.GetComponentInChildren<SecretBookcaseDoor>(true);
            Quaternion bookClosed = sdoor != null ? sdoor.hinge.localRotation : Quaternion.identity;
            var bookCols = sdoor != null ? sdoor.hinge.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToList() : new List<Collider>();
            if (sdoor != null)
            {
                sdoor.hinge.localRotation = bookClosed * Quaternion.Euler(0f, sdoor.openAngle, 0f);
                foreach (var c in bookCols) c.enabled = false;
            }
            Physics.SyncTransforms();
            Bake();
            if (Flag("-dump")) Dump(outDir);

            var shots = new List<Shot>();
            foreach (JObject sj in spec["shots"])
            {
                var shot = new Shot { id = (string)sj["id"], type = (string)sj["type"] ?? "path", j = sj };
                if (shot.type == "interlude") continue;
                if (only != null && !only.Contains(shot.id) && shot.type != "pathonly") continue;
                try
                {
                    if (shot.type == "keys") PlanKeys(shot, spec);
                    else PlanPath(shot, spec);
                }
                catch (Exception e) { shot.note = "PLAN FAILED: " + e.Message; Debug.LogException(e); }
                s_log.AppendLine($"shot {shot.id} ({shot.type}): {shot.length:0} m, {shot.seconds:0.0} s, {shot.poses.Count} frames, {shot.bumps} frame(s) with the camera inside a collider {shot.note}");
                shots.Add(shot);
            }

            if (sdoor != null)
            {
                sdoor.hinge.localRotation = bookClosed;
                foreach (var c in bookCols) c.enabled = true;
            }
            Physics.SyncTransforms();

            Directory.CreateDirectory(outDir);
            WritePoses(outDir + (preview ? "/poses-preview.json" : "/poses.json"), shots, spec);
            if (!Flag("-noMap")) MapImage(outDir + "/map.png", outDir + "/map.json");

            int w = preview ? 960 : (int?)spec["width"] ?? 1920;
            int h = preview ? 540 : (int?)spec["height"] ?? 1080;
            using (var r = new FrameRenderer(w, h, spec, sdoor, bookClosed))
            {
                if (Flag("-probe"))
                {
                    foreach (var ps in shots.Where(s => s.type != "pathonly" && s.poses.Count > 0))
                        r.Probe(ps, outDir + "/probe");
                    File.WriteAllText(outDir + "/flythrough-probe.log", s_log.ToString());
                    return;
                }
                // the editor's own memory grows with every frame rendered in batch mode (no player loop runs to free
                // what each render request leaves behind), so a full film exhausts the machine: -maxFrames ends the
                // process after that many frames, and -resume carries on from the frames already on disk
                int cap = int.Parse(Arg("-maxFrames", "0"), Inv), total = 0;
                bool capped = false, resume = !preview && Flag("-resume");
                foreach (var shot in shots)
                {
                    if (capped) break;
                    if (shot.type == "pathonly" || shot.poses.Count == 0) continue;
                    string dir = outDir + (preview ? "/preview/" : "/frames/") + shot.id;
                    var have = new HashSet<string>();
                    if (resume && Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, "*.jpg").Select(Path.GetFileName).OrderBy(s => s, StringComparer.Ordinal).ToList();
                        if (files.Count == shot.poses.Count)
                        {
                            s_log.AppendLine($"kept {shot.id}: {shot.poses.Count} frame(s) already rendered");
                            continue;
                        }
                        // a stopped process may have left its last frame half-written: render that one again
                        if (files.Count > 0) { File.Delete(Path.Combine(dir, files[files.Count - 1])); files.RemoveAt(files.Count - 1); }
                        have = files.ToHashSet();
                    }
                    else if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    Directory.CreateDirectory(dir);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    int n = 0;
                    r.BeginShot(shot);
                    for (int f = 0; f < shot.poses.Count; f++)
                    {
                        // every frame advances the particles, rendered or not, so a resumed shot matches an unbroken one
                        r.Advance();
                        if (preview && f % every != 0 && f != shot.poses.Count - 1) continue;
                        string name = $"{f:00000}.jpg";
                        if (have.Contains(name)) continue;
                        if (cap > 0 && total >= cap) { capped = true; break; }
                        r.Frame(shot.poses[f], dir + "/" + name);
                        n++; total++;
                    }
                    s_log.AppendLine($"rendered {shot.id}: {n} frame(s) in {sw.Elapsed.TotalSeconds:0.0} s" + (have.Count > 0 ? $", {have.Count} kept" : ""));
                }
                s_log.AppendLine(capped ? $"status: stopped at the {cap}-frame cap; run again with -resume" : "status: complete");
            }
            File.WriteAllText(outDir + (preview ? "/flythrough-preview.log" : "/flythrough.log"), s_log.ToString());
        }

        static void Dump(string outDir)
        {
            var sb = new StringBuilder();
            foreach (var m in s_root.GetComponentsInChildren<IronCitadelMarker>(true))
                sb.AppendLine($"marker {m.kind} {m.room} '{m.note}' at {m.transform.position.ToString("F1")} yaw {m.transform.eulerAngles.y:0} path {m.transform.parent?.name}/{m.name}");
            var mi = typeof(IronCitadelRoomsAssembler).GetMethod("MainGroupBounds", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var r in s_level.rooms)
            {
                var t = s_root.transform.Find("Rooms/" + r.id);
                if (t == null) continue;
                var args = new object[] { t.gameObject, null };
                var b = (Bounds)mi.Invoke(null, args);
                sb.AppendLine($"main {r.id} ({((Transform)args[1]).name}): x {b.min.x:0.0}..{b.max.x:0.0} z {b.min.z:0.0}..{b.max.z:0.0}");
            }
            foreach (var d in s_level.doors)
            {
                try { sb.AppendLine($"door {d.id}: {Anchor("door:" + d.id).ToString("F1")}"); } catch (Exception e) { sb.AppendLine($"door {d.id}: {e.Message}"); }
            }
            // reachability over a cell, 1 m grid, north up: '.' reached, 'p' partial, '#' off the mesh
            void NavMap(string room, string from)
            {
                var rm = s_level.ById(room);
                var c = IronCitadelRoomsAssembler.CellCenter(rm.r, rm.c);
                var src = OnMesh(Anchor(from));
                sb.AppendLine($"navmap {room} from {from} {src.ToString("F1")}");
                var path = new NavMeshPath();
                for (float z = c.z + 22f; z >= c.z - 22f; z -= 1f)
                {
                    var line = new StringBuilder($"{z,7:0} ");
                    for (float x = c.x - 22f; x <= c.x + 22f; x += 1f)
                    {
                        if (!NavMesh.SamplePosition(new Vector3(x, 0.15f, z), out var hit, 0.4f, s_filter)) { line.Append('#'); continue; }
                        NavMesh.CalculatePath(src, hit.position, s_filter, path);
                        line.Append(path.status == NavMeshPathStatus.PathComplete ? '.' : 'p');
                    }
                    sb.AppendLine(line.ToString());
                }
            }
            try { NavMap("armory", "door:under_gallery:armory:3"); } catch (Exception e) { sb.AppendLine("navmap armory: " + e.Message); }
            // the same with the defender stand-ins' colliders off: is it the NPCs or the props that seal it
            var npc = s_root.GetComponentsInChildren<IronCitadelMarker>(true).Where(m => m.kind.ToString() == "Defender")
                .SelectMany(m => m.GetComponentsInChildren<Collider>(true)).Where(k => k.enabled).ToList();
            foreach (var k in npc) k.enabled = false;
            Physics.SyncTransforms(); Bake();
            sb.AppendLine($"without {npc.Count} defender collider(s):");
            try { NavMap("armory", "door:under_gallery:armory:3"); } catch (Exception e) { sb.AppendLine("navmap armory: " + e.Message); }
            foreach (var k in npc) k.enabled = true;
            Physics.SyncTransforms(); Bake();
            // what seals the reachable west half from the archers' strip
            foreach (var col in Physics.OverlapBox(new Vector3(115f, 0.9f, -120f), new Vector3(10f, 0.8f, 7f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore)
                         .OrderBy(k => k.bounds.center.z))
            {
                var bb = col.bounds;
                string pth = col.transform.name;
                for (var tp = col.transform.parent; tp != null && tp != s_root.transform; tp = tp.parent) pth = tp.name + "/" + pth;
                sb.AppendLine($"collider x {bb.min.x:0.0}..{bb.max.x:0.0} z {bb.min.z:0.0}..{bb.max.z:0.0} y {bb.min.y:0.0}..{bb.max.y:0.0} {col.GetType().Name} {pth}");
            }
            File.WriteAllText(outDir + "/dump.txt", sb.ToString());
        }

        // ------------------------------------------------------------------ NavMesh

        static void Bake()
        {
            NavMesh.RemoveAllNavMeshData();
            var set = IronCitadel.Conformance.NavBake.Settings();
            s_filter = new NavMeshQueryFilter { agentTypeID = set.agentTypeID, areaMask = NavMesh.AllAreas };
            float W = IronCitadelRoomsAssembler.Tile * s_level.cols, D = IronCitadelRoomsAssembler.Tile * s_level.rows;
            var bounds = new Bounds(new Vector3(W / 2f, 10f, -D / 2f), new Vector3(W + 6f, 40f, D + 6f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, AllLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var data = NavMeshBuilder.BuildNavMeshData(set, sources, bounds, Vector3.zero, Quaternion.identity);
            NavMesh.AddNavMeshData(data);
            s_log.AppendLine($"NavMesh: {sources.Count} sources, baked in {sw.Elapsed.TotalSeconds:0.0} s");
        }

        static Vector3 OnMesh(Vector3 p, float drop = 0f)
        {
            var q = p - Vector3.up * drop;
            if (NavMesh.SamplePosition(q, out var hit, 2f, s_filter)) return hit.position;
            if (NavMesh.SamplePosition(q, out hit, 6f, s_filter)) return hit.position;
            throw new Exception("no NavMesh near " + p.ToString("F2"));
        }

        // ------------------------------------------------------------------ anchors

        static void ReadEyes()
        {
            string f = IronCitadelRoomsAssembler.OutRoot + "/captures/rooms/captures.txt";
            if (!File.Exists(f)) return;
            foreach (var line in File.ReadAllLines(f))
            {
                int c = line.IndexOf(".png: eye (", StringComparison.Ordinal);
                if (c < 0) continue;
                string name = line.Substring(0, c);
                if (name.StartsWith("eye-")) name = name.Substring(4);
                int a = line.IndexOf('(', c) + 1, b = line.IndexOf(')', a);
                var v = line.Substring(a, b - a).Split(',').Select(x => float.Parse(x.Trim(), Inv)).ToArray();
                s_eyes[name] = new Vector3(v[0], v[1], v[2]);
            }
        }

        /// <summary>An anchor: xyz:x,y,z | door:id[:room:metres] | eye:name | main:room | cell:room | obj:path,
        /// with an optional @dx,dy,dz offset.</summary>
        static Vector3 Anchor(string a)
        {
            Vector3 off = Vector3.zero;
            int at = a.IndexOf('@');
            if (at >= 0) { off = V3(a.Substring(at + 1)); a = a.Substring(0, at); }
            int k = a.IndexOf(':');
            string kind = a.Substring(0, k), rest = a.Substring(k + 1);
            Vector3 p;
            switch (kind)
            {
                case "xyz": p = V3(rest); break;
                case "eye":
                    if (!s_eyes.TryGetValue(rest, out p)) throw new Exception("no eye " + rest + " in captures.txt");
                    break;
                case "cell":
                    { var rm = s_level.ById(rest); p = IronCitadelRoomsAssembler.CellCenter(rm.r, rm.c); }
                    break;
                case "main":
                    {
                        var room = s_root.transform.Find("Rooms/" + rest).gameObject;
                        var mi = typeof(IronCitadelRoomsAssembler).GetMethod("MainGroupBounds", BindingFlags.NonPublic | BindingFlags.Static);
                        var args = new object[] { room, null };
                        var b = (Bounds)mi.Invoke(null, args);
                        p = new Vector3(b.center.x, 0f, b.center.z);
                    }
                    break;
                case "obj":
                    {
                        var t = s_root.transform.Find(rest);
                        if (t == null) throw new Exception("no object " + rest);
                        p = t.position;
                    }
                    break;
                case "door":
                    {
                        var parts = rest.Split(':');
                        var d = s_level.doors.First(x => x.id == parts[0]);
                        var e = IronCitadelRoomsAssembler.EdgeOfDoor(d);
                        float tc = 0f;
                        if (IronCitadelRoomsAssembler.MeasureOpening(e, out var t0, out _, out _) && Mathf.Abs(t0) < 4f) tc = t0;
                        p = e.centre + e.along * tc;
                        if (parts.Length >= 3)
                        {
                            var rm = s_level.ById(parts[1]);
                            float m = float.Parse(parts[2], Inv);
                            bool far = rm.r == e.r2 && rm.c == e.c2;
                            p += e.n * (far ? m : -m);
                        }
                    }
                    break;
                default: throw new Exception("unknown anchor " + a);
            }
            return p + off;
        }

        static Vector3 V3(string s)
        {
            var v = s.Split(',').Select(x => float.Parse(x.Trim(), Inv)).ToArray();
            return new Vector3(v[0], v[1], v[2]);
        }

        static float F(JObject o, string k, float def) => o[k] != null ? (float)o[k] : def;

        // ------------------------------------------------------------------ path shots

        static void PlanPath(Shot shot, JObject spec)
        {
            var sj = shot.j;
            float height = F(sj, "camHeight", F(spec, "camHeight", 1.7f));
            var vias = new List<JObject>();
            foreach (var v in (JArray)sj["via"]) vias.Add(v.Type == JTokenType.String ? new JObject { ["at"] = v } : (JObject)v);

            // via points on the NavMesh; an eye anchor stands 1.6-1.8 m up, so look for its floor below it
            var pts = vias.Select(v =>
            {
                string a = (string)v["at"];
                var p = Anchor(a);
                return OnMesh(p, a.StartsWith("eye:") ? 1.6f : 0f);
            }).ToList();

            var S = new List<Sample> { new Sample { p = pts[0], h = height, wait = F(vias[0], "wait", 0f) } };
            S[0].stop = S[0].wait > 0f;
            var hKeys = new List<(int i, float h)> { (0, height) };
            for (int i = 1; i < pts.Count; i++)
            {
                var np = new NavMeshPath();
                NavMesh.CalculatePath(pts[i - 1], pts[i], s_filter, np);
                if (np.status != NavMeshPathStatus.PathComplete)
                    shot.note += $" [leg {i} {np.status}: {pts[i - 1].ToString("F1")} -> {pts[i].ToString("F1")}]";
                var corners = np.corners.Length >= 2 ? np.corners : new[] { pts[i - 1], pts[i] };
                int legStart = S.Count;
                for (int c = 1; c < corners.Length; c++)
                {
                    var a = corners[c - 1]; var b = corners[c];
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / Step));
                    for (int s = 1; s <= n; s++) S.Add(new Sample { p = Vector3.Lerp(a, b, s / (float)n) });
                }
                var v = vias[i];
                var end = S[S.Count - 1];
                end.wait = F(v, "wait", 0f); end.evt = (string)v["event"]; end.stop = end.wait > 0f;
                if (v["h"] != null) hKeys.Add((S.Count - 1, (float)v["h"]));
                if (v["look"] != null)
                {
                    var target = Anchor((string)v["look"]);
                    float from = F(v, "lookFrom", 1e9f);
                    // from the end of the leg back "from" metres
                    float acc = 0f;
                    for (int s = S.Count - 1; s >= legStart; s--)
                    {
                        if (acc > from) break;
                        S[s].look = target;
                        if (s > legStart) acc += Vector3.Distance(S[s].p, S[s - 1].p);
                    }
                }
            }
            if (hKeys[hKeys.Count - 1].i != S.Count - 1) hKeys.Add((S.Count - 1, hKeys[hKeys.Count - 1].h));
            // camera height: linear between keys in arc length
            var cum = Cumulative(S);
            for (int k = 1; k < hKeys.Count; k++)
            {
                var (i0, h0) = hKeys[k - 1]; var (i1, h1) = hKeys[k];
                for (int i = i0; i <= i1; i++)
                {
                    float u = cum[i1] - cum[i0] > 1e-3f ? (cum[i] - cum[i0]) / (cum[i1] - cum[i0]) : 1f;
                    S[i].h = Mathf.Lerp(h0, h1, Mathf.SmoothStep(0f, 1f, u));
                }
            }

            Centre(S, F(sj, "centreMax", 2.5f));
            Smooth(S, F(sj, "smooth", 1.5f));
            if (sj["keep"] is JArray keep)
            {
                var c0 = Cumulative(S);
                float L = c0[c0.Count - 1], ka = (float)keep[0], kb = (float)keep[1];
                if (ka < 0f) ka = L + ka;
                if (kb <= 0f) kb = L + kb;
                int i0 = c0.FindIndex(x => x >= ka), i1 = c0.FindLastIndex(x => x <= kb);
                if (i0 < 0 || i1 <= i0) throw new Exception($"keep [{ka}, {kb}] is outside the path (0..{L:0.0} m)");
                S = S.GetRange(i0, i1 - i0 + 1);
            }
            for (int i = 0; i < S.Count; i++) S[i].width = Width(S[i], S, i);
            shot.path = S.Select(s => s.p).ToList();
            cum = Cumulative(S);
            shot.length = cum[cum.Count - 1];

            // speed: fast in narrow halls, slow in rooms, on turns and while looking at something
            float vmin = F(sj, "vmin", F(spec, "vmin", 2.2f)), vmax = F(sj, "vmax", F(spec, "vmax", 5.5f));
            float vlook = F(sj, "vlook", F(spec, "vlook", 1.6f)), acc2 = 2f * F(spec, "accel", 1.4f);
            float omega = F(spec, "turnRate", 40f) * Mathf.Deg2Rad;
            int N = S.Count;
            var vA = new float[N];
            for (int i = 0; i < N; i++)
            {
                float v = Mathf.Lerp(vmax, vmin, Mathf.InverseLerp(4f, 10f, S[i].width));
                float kappa = Curvature(S, i, 6);
                if (kappa > 1e-4f) v = Mathf.Min(v, omega / kappa);
                if (S[i].look.HasValue) v = Mathf.Min(v, vlook);
                vA[i] = Mathf.Max(0.4f, v);
            }
            vA = MovingMin(vA, 6);
            vA = MovingAvg(vA, 8);
            var V = new float[N];
            V[0] = Mathf.Min(vA[0], F(sj, "vstart", 1.2f));
            for (int i = 1; i < N; i++)
            {
                float ds = Vector3.Distance(S[i].p, S[i - 1].p);
                float prev = S[i - 1].stop ? 0f : V[i - 1];
                V[i] = Mathf.Min(vA[i], Mathf.Sqrt(prev * prev + acc2 * ds));
                if (S[i].stop) V[i] = 0f;
            }
            V[N - 1] = Mathf.Min(V[N - 1], S[N - 1].stop ? 0f : F(sj, "vend", 1.2f));
            for (int i = N - 2; i >= 0; i--)
            {
                float ds = Vector3.Distance(S[i].p, S[i + 1].p);
                V[i] = Mathf.Min(V[i], Mathf.Sqrt(V[i + 1] * V[i + 1] + acc2 * ds));
            }
            // arrival and leave times per sample
            var tA = new double[N]; var tL = new double[N];
            tA[0] = F(sj, "holdStart", 0f); tL[0] = tA[0] + S[0].wait;
            for (int i = 1; i < N; i++)
            {
                float ds = Vector3.Distance(S[i].p, S[i - 1].p);
                tA[i] = tL[i - 1] + ds / Mathf.Max(0.08f, (V[i] + V[i - 1]) / 2f);
                tL[i] = tA[i] + S[i].wait;
            }
            double total = tL[N - 1] + F(sj, "holdEnd", 0f);
            shot.seconds = (float)total;

            int fps = (int?)spec["fps"] ?? 30;
            float fov = F(sj, "fov", F(spec, "fov", 55f));
            float tau = F(sj, "lookLag", F(spec, "lookLag", 0.55f));
            float lookAhead = F(sj, "lookAhead", F(spec, "lookAhead", 5f));
            float pitchDown = F(sj, "pitchDown", F(spec, "pitchDown", 3f));
            bool ceilings = sj["ceilings"] == null || (bool)sj["ceilings"];
            // the bookcase opens at the stop that carries the event
            double bookT = -1;
            for (int i = 0; i < N; i++) if (S[i].evt == "bookcase") bookT = tA[i] + F(sj, "bookDelay", 0.8f);
            float bookDur = F(sj, "bookDuration", 1.8f);
            float bookStart = sj["bookOpen"] != null && (bool)sj["bookOpen"] ? 1f : 0f;

            int frames = Mathf.CeilToInt((float)(total * fps));
            Vector3 dir = Vector3.zero;
            int idx = 0;
            for (int f = 0; f <= frames; f++)
            {
                double t = f / (double)fps;
                while (idx < N - 1 && tA[idx + 1] <= t) idx++;
                float u = 0f;
                if (idx < N - 1 && t > tL[idx]) u = (float)((t - tL[idx]) / Math.Max(1e-6, tA[idx + 1] - tL[idx]));
                int j = Mathf.Min(idx + 1, N - 1);
                var floor = Vector3.Lerp(S[idx].p, S[j].p, u);
                float h = Mathf.Lerp(S[idx].h, S[j].h, u);
                var cam = floor + Vector3.up * h;
                var look = u < 0.5f ? S[idx].look : S[j].look;
                Vector3 want;
                if (look.HasValue) want = (look.Value - cam).normalized;
                else
                {
                    float speed = Mathf.Lerp(V[idx], V[j], u);
                    int ahead = Mathf.Min(N - 1, idx + Mathf.RoundToInt((lookAhead + 0.8f * speed) / Step));
                    var tgt = S[ahead].p + Vector3.up * S[ahead].h;
                    want = tgt - cam;
                    if (want.sqrMagnitude < 0.01f) want = dir.sqrMagnitude > 0 ? dir : Vector3.forward;
                    want = Quaternion.AngleAxis(pitchDown, Vector3.Cross(Vector3.up, want).normalized) * want.normalized;
                }
                if (f == 0) dir = sj["startLook"] != null ? (Anchor((string)sj["startLook"]) - cam).normalized : want;
                else dir = Vector3.Slerp(dir, want, 1f - Mathf.Exp(-1f / (fps * tau))).normalized;
                float book = bookStart;
                if (bookT >= 0) book = Mathf.Max(book, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)((t - bookT) / bookDur))));
                if (Physics.CheckSphere(cam, 0.12f, AllLayers, QueryTriggerInteraction.Ignore)) shot.bumps++;
                shot.poses.Add(new Pose
                {
                    pos = cam, fov = fov, ceilings = ceilings, book = book, moon = sj["moon"] != null && (bool)sj["moon"],
                    yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg,
                    pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg,
                });
            }
        }

        static List<float> Cumulative(List<Sample> S)
        {
            var c = new List<float> { 0f };
            for (int i = 1; i < S.Count; i++) c.Add(c[i - 1] + Vector3.Distance(S[i].p, S[i - 1].p));
            return c;
        }

        static Vector3 Tangent(List<Sample> S, int i, int k = 4)
        {
            var a = S[Mathf.Max(0, i - k)].p; var b = S[Mathf.Min(S.Count - 1, i + k)].p;
            var d = b - a; d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        /// <summary>Free width across the path at 1.3 m: the sum of the clear runs left and right (8 m cap each).</summary>
        static float Width(Sample s, List<Sample> S, int i)
        {
            var t = Tangent(S, i);
            var n = new Vector3(-t.z, 0f, t.x);
            var o = s.p + Vector3.up * 1.3f;
            float L = Physics.Raycast(o, n, out var hl, 8f, AllLayers, QueryTriggerInteraction.Ignore) ? hl.distance : 8f;
            float R = Physics.Raycast(o, -n, out var hr, 8f, AllLayers, QueryTriggerInteraction.Ignore) ? hr.distance : 8f;
            return L + R;
        }

        /// <summary>Pushes each sample toward the middle of a narrow passage, at most maxShift m, then smooths the shift.</summary>
        static void Centre(List<Sample> S, float maxShift)
        {
            int N = S.Count;
            var shift = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                if (S[i].stop || i == 0 || i == N - 1) continue;
                var t = Tangent(S, i);
                var n = new Vector3(-t.z, 0f, t.x);
                var o = S[i].p + Vector3.up * 1.3f;
                float L = Physics.Raycast(o, n, out var hl, 6f, AllLayers, QueryTriggerInteraction.Ignore) ? hl.distance : 6f;
                float R = Physics.Raycast(o, -n, out var hr, 6f, AllLayers, QueryTriggerInteraction.Ignore) ? hr.distance : 6f;
                if (L >= 6f && R >= 6f) continue;
                float m = Mathf.Clamp((L - R) / 2f, -maxShift, maxShift);
                shift[i] = n * m;
            }
            // smooth the shift over about 2 m so a doorway jamb does not jerk the camera
            var sm = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                Vector3 acc = Vector3.zero; int c = 0;
                for (int k = -8; k <= 8; k++) { int j = i + k; if (j < 0 || j >= N) continue; acc += shift[j]; c++; }
                sm[i] = acc / c;
            }
            for (int i = 0; i < N; i++)
            {
                if (S[i].stop || i == 0 || i == N - 1) continue;
                var cand = S[i].p + sm[i];
                for (int tries = 0; tries < 4; tries++)
                {
                    if (Clear(cand, S[i].h)) { S[i].p = cand; break; }
                    cand = Vector3.Lerp(S[i].p, cand, 0.5f);
                }
            }
        }

        /// <summary>Moving-average smoothing over +-radius metres, three passes; a point that would hit something stays put.</summary>
        static void Smooth(List<Sample> S, float radius)
        {
            int N = S.Count, k = Mathf.Max(1, Mathf.RoundToInt(radius / Step));
            for (int pass = 0; pass < 3; pass++)
            {
                var np = new Vector3[N];
                for (int i = 0; i < N; i++)
                {
                    if (S[i].stop || i == 0 || i == N - 1) { np[i] = S[i].p; continue; }
                    Vector3 acc = Vector3.zero; int c = 0;
                    for (int d = -k; d <= k; d++) { int j = i + d; if (j < 0 || j >= N) continue; acc += S[j].p; c++; }
                    np[i] = acc / c;
                }
                for (int i = 0; i < N; i++)
                {
                    var cand = np[i];
                    for (int tries = 0; tries < 4; tries++)
                    {
                        if (Clear(cand, S[i].h)) { S[i].p = cand; break; }
                        cand = Vector3.Lerp(S[i].p, cand, 0.5f);
                    }
                }
            }
        }

        static bool Clear(Vector3 floor, float h)
        {
            return !Physics.CheckSphere(floor + Vector3.up * Mathf.Max(0.6f, h - 0.6f), 0.28f, AllLayers, QueryTriggerInteraction.Ignore)
                && !Physics.CheckSphere(floor + Vector3.up * h, 0.28f, AllLayers, QueryTriggerInteraction.Ignore);
        }

        static float Curvature(List<Sample> S, int i, int k)
        {
            int a = Mathf.Max(0, i - k), b = Mathf.Min(S.Count - 1, i + k);
            if (b - a < 2) return 0f;
            var t0 = S[i].p - S[a].p; var t1 = S[b].p - S[i].p; t0.y = 0; t1.y = 0;
            if (t0.sqrMagnitude < 1e-4f || t1.sqrMagnitude < 1e-4f) return 0f;
            float ang = Vector3.Angle(t0, t1) * Mathf.Deg2Rad;
            return ang / Mathf.Max(0.1f, (t0.magnitude + t1.magnitude) / 2f);
        }

        static float[] MovingMin(float[] v, int k)
        {
            var r = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float m = float.MaxValue;
                for (int d = -k; d <= k; d++) { int j = i + d; if (j >= 0 && j < v.Length) m = Mathf.Min(m, v[j]); }
                r[i] = m;
            }
            return r;
        }

        static float[] MovingAvg(float[] v, int k)
        {
            var r = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float a = 0f; int c = 0;
                for (int d = -k; d <= k; d++) { int j = i + d; if (j >= 0 && j < v.Length) { a += v[j]; c++; } }
                r[i] = a / c;
            }
            return r;
        }

        // ------------------------------------------------------------------ key shots

        static void PlanKeys(Shot shot, JObject spec)
        {
            var sj = shot.j;
            var keys = ((JArray)sj["keys"]).Cast<JObject>().ToList();
            var T = keys.Select(k => (float)k["t"]).ToArray();
            var P = keys.Select(k => Anchor((string)k["at"])).ToArray();
            var L = keys.Select(k => Anchor((string)k["look"])).ToArray();
            int fps = (int?)spec["fps"] ?? 30;
            float total = T[T.Length - 1];
            shot.seconds = total;
            float fov = F(sj, "fov", F(spec, "fov", 55f));
            bool ceilings = sj["ceilings"] == null || (bool)sj["ceilings"];
            bool ease = sj["ease"] == null || (bool)sj["ease"];
            int frames = Mathf.CeilToInt(total * fps);
            for (int f = 0; f <= frames; f++)
            {
                float t = Mathf.Min(total, f / (float)fps);
                if (ease) t = total * Mathf.SmoothStep(0f, 1f, t / total);
                int i = 0;
                while (i < T.Length - 2 && T[i + 1] <= t) i++;
                float u = Mathf.Clamp01((t - T[i]) / Mathf.Max(1e-4f, T[i + 1] - T[i]));
                var pos = CatmullRom(P, i, u);
                var look = CatmullRom(L, i, u);
                var d = (look - pos).normalized;
                shot.poses.Add(new Pose
                {
                    pos = pos, fov = fov, ceilings = ceilings, moon = sj["moon"] != null && (bool)sj["moon"],
                    book = sj["bookOpen"] != null && (bool)sj["bookOpen"] ? 1f : 0f,
                    yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = -Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg,
                });
                shot.path.Add(pos);
            }
            for (int k = 1; k < P.Length; k++) shot.length += Vector3.Distance(P[k], P[k - 1]);
        }

        static Vector3 CatmullRom(Vector3[] P, int i, float u)
        {
            var p0 = P[Mathf.Max(0, i - 1)]; var p1 = P[i]; var p2 = P[Mathf.Min(P.Length - 1, i + 1)]; var p3 = P[Mathf.Min(P.Length - 1, i + 2)];
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        // ------------------------------------------------------------------ outputs

        static void WritePoses(string path, List<Shot> shots, JObject spec)
        {
            var root = new JObject { ["fps"] = (int?)spec["fps"] ?? 30 };
            var arr = new JArray();
            foreach (var s in shots)
            {
                var o = new JObject
                {
                    ["id"] = s.id, ["type"] = s.type, ["length_m"] = Math.Round(s.length, 1), ["seconds"] = Math.Round(s.seconds, 2),
                    ["frames"] = s.poses.Count, ["bumps"] = s.bumps, ["note"] = s.note,
                };
                var pa = new JArray();
                for (int i = 0; i < s.path.Count; i += 4) pa.Add(new JArray(R(s.path[i].x), R(s.path[i].y), R(s.path[i].z)));
                if (s.path.Count > 0) { var l = s.path[s.path.Count - 1]; pa.Add(new JArray(R(l.x), R(l.y), R(l.z))); }
                o["path"] = pa;
                var po = new JArray();
                foreach (var p in s.poses) po.Add(new JArray(R(p.pos.x), R(p.pos.y), R(p.pos.z), R(p.yaw), R(p.pitch), R(p.book)));
                o["poses"] = po;
                arr.Add(o);
            }
            root["shots"] = arr;
            File.WriteAllText(path, root.ToString(Newtonsoft.Json.Formatting.None));
        }

        static double R(float v) => Math.Round(v, 2);

        static void MapImage(string png, string json)
        {
            var c = new Vector2(IronCitadelRoomsAssembler.Tile * s_level.cols / 2f, -IronCitadelRoomsAssembler.Tile * s_level.rows / 2f);
            float half = IronCitadelRoomsAssembler.Tile * s_level.cols / 2f + 2f;
            int px = 1440;
            var hidden = IronCitadelCapture.HideCeilings(s_root);
            try { IronCitadelCapture.TopDown(png, c, half, px, true, 300f); }
            finally { IronCitadelCapture.Restore(hidden); }
            File.WriteAllText(json, new JObject { ["centre"] = new JArray(c.x, c.y), ["half"] = half, ["px"] = px }.ToString());
        }

        // ------------------------------------------------------------------ rendering

        sealed class FrameRenderer : IDisposable
        {
            readonly int w, h;
            readonly Camera cam;
            readonly RenderTexture rt;
            readonly Texture2D tex;
            readonly GameObject volumeGo, moonGo;
            readonly JObject basePost;
            Tonemapping tm; Bloom bloom; ColorAdjustments ca; Vignette vig;
            readonly Light moon;
            readonly SecretBookcaseDoor sdoor;
            readonly Quaternion bookClosed;
            readonly List<GameObject> ceilings;
            readonly List<ParticleSystem> particles;
            readonly float dt;
            readonly bool prevAsync;
            bool ceilingsHidden;
            int warm;

            public FrameRenderer(int w, int h, JObject spec, SecretBookcaseDoor sdoor, Quaternion bookClosed)
            {
                this.w = w; this.h = h; this.sdoor = sdoor; this.bookClosed = bookClosed;
                dt = 1f / ((int?)spec["fps"] ?? 30);
                var go = new GameObject("~Flythrough");
                cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
                cam.allowHDR = true;
                var data = cam.GetUniversalAdditionalCameraData();
                var post = spec["post"] as JObject ?? new JObject();
                basePost = post;
                bool usePost = post["enabled"] == null || (bool)post["enabled"];
                data.renderPostProcessing = usePost;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { useMipMap = false, antiAliasing = 1 };
                rt.Create();
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

                if (usePost)
                {
                    volumeGo = new GameObject("~FlythroughVolume");
                    var vol = volumeGo.AddComponent<Volume>();
                    vol.isGlobal = true; vol.priority = 1000f;
                    var prof = ScriptableObject.CreateInstance<VolumeProfile>();
                    tm = prof.Add<Tonemapping>(true);
                    bloom = prof.Add<Bloom>(true);
                    ca = prof.Add<ColorAdjustments>(true);
                    vig = prof.Add<Vignette>(true);
                    vol.sharedProfile = prof;
                    ApplyPost(post);
                    // every other post component overridden at its neutral default, so the project's default
                    // volume profile (whatever state it is in) cannot tint the film; one render initializes the manager
                    RenderOnce();
                    var neutral = new List<string>();
                    foreach (var t in VolumeManager.instance.baseComponentTypeArray)
                        if (!prof.Has(t)) { prof.Add(t, true); neutral.Add(t.Name); }
                    s_log.AppendLine("neutralized: " + string.Join(", ", neutral));
                }
                moonGo = new GameObject("~FlythroughMoon");
                moon = moonGo.AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.color = new Color(0.78f, 0.84f, 1f);
                moon.intensity = F(spec, "moonIntensity", 0.9f);
                moon.shadows = LightShadows.Soft;
                moonGo.transform.rotation = Quaternion.Euler(58f, -35f, 0f);
                moon.enabled = false;

                ceilings = new List<GameObject>();
                foreach (var t in s_root.GetComponentsInChildren<Transform>(false))
                    if (t != null && t.gameObject.activeSelf && IronCitadelCapture.IsCeiling(t)) ceilings.Add(t.gameObject);
                particles = s_root.GetComponentsInChildren<ParticleSystem>(false)
                    .Where(p => p.transform.parent == null || p.transform.parent.GetComponentInParent<ParticleSystem>() == null).ToList();
                s_log.AppendLine($"renderer {w}x{h}, post {usePost}, {particles.Count} particle system(s), {ceilings.Count} ceiling group(s)");
                prevAsync = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
            }

            void ApplyPost(JObject post)
            {
                if (tm == null) return;
                tm.mode.Override((string)post["tonemap"] == "neutral" ? TonemappingMode.Neutral : (string)post["tonemap"] == "none" ? TonemappingMode.None : TonemappingMode.ACES);
                bloom.intensity.Override(F(post, "bloom", 0.6f));
                bloom.threshold.Override(F(post, "bloomThreshold", 0.9f));
                bloom.scatter.Override(F(post, "bloomScatter", 0.7f));
                ca.postExposure.Override(F(post, "exposure", 0.6f));
                ca.contrast.Override(F(post, "contrast", 10f));
                ca.saturation.Override(F(post, "saturation", 8f));
                vig.intensity.Override(F(post, "vignette", 0.3f));
                vig.smoothness.Override(0.45f);
            }

            public void BeginShot(Shot shot)
            {
                var merged = (JObject)basePost.DeepClone();
                if (shot.j["post"] is JObject over) merged.Merge(over);
                ApplyPost(merged);
                foreach (var p in particles) p.Simulate(3f, true, true, false);
                warm = 0;
            }

            public void Advance()
            {
                foreach (var p in particles) p.Simulate(dt, true, false, false);
            }

            public void Frame(Pose p, string file)
            {
                bool hide = !p.ceilings;
                if (hide != ceilingsHidden)
                {
                    foreach (var g in ceilings) if (g != null) g.SetActive(!hide);
                    ceilingsHidden = hide;
                }
                moon.enabled = p.moon;
                if (sdoor != null) sdoor.hinge.localRotation = bookClosed * Quaternion.Euler(0f, sdoor.openAngle * p.book, 0f);
                cam.transform.SetPositionAndRotation(p.pos, Quaternion.Euler(p.pitch, p.yaw, 0f));
                cam.fieldOfView = p.fov;
                cam.aspect = w / (float)h;
                // the first frame of a shot renders three times: shader variants, shadow maps, auto exposure
                int n = warm == 0 ? 3 : 1;
                warm++;
                for (int i = 0; i < n; i++) RenderOnce();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                // no Apply: the JPEG encodes the CPU copy, and a GPU upload per frame exhausts D3D12's upload
                // memory, which a batch-mode loop never recycles (crashed after ~2000 frames at 2560x1440)
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                RenderTexture.active = prev;
                GL.Flush();
                File.WriteAllBytes(file, tex.EncodeToJPG(94));
            }

            public void Probe(Shot shot, string dir)
            {
                Directory.CreateDirectory(dir);
                var p = shot.poses[shot.poses.Count * 2 / 3];
                var vm = VolumeManager.instance;
                var data = cam.GetUniversalAdditionalCameraData();
                var vol = volumeGo.GetComponent<Volume>();
                string Vols() => string.Join(", ", vm.GetVolumes(data.volumeLayerMask).Select(v => $"{v.name}(prio {v.priority}, global {v.isGlobal}, on {v.isActiveAndEnabled}, layer {v.gameObject.layer})"));
                s_log.AppendLine($"probe vm init {vm.isInitialized}, cam mask {data.volumeLayerMask.value}, vol on {vol.isActiveAndEnabled} layer {volumeGo.layer} comps {vol.sharedProfile.components.Count}, volumes [{Vols()}]");
                RenderOnce();
                s_log.AppendLine($"probe after a render: vm init {vm.isInitialized}, volumes [{Vols()}], stack exposure {vm.stack.GetComponent<ColorAdjustments>().postExposure.value:0.00}");
                vol.enabled = false; vol.enabled = true;
                RenderOnce();
                s_log.AppendLine($"probe after re-enable: volumes [{Vols()}], stack exposure {vm.stack.GetComponent<ColorAdjustments>().postExposure.value:0.00}");
                s_log.AppendLine($"probe rendered-by-camera {UnityEditor.SceneManagement.StageUtility.IsGameObjectRenderedByCamera(volumeGo, cam)}, vol scene {volumeGo.scene.name}, cam scene {cam.gameObject.scene.name}");
                vm.Update(null, data.volumeLayerMask);
                s_log.AppendLine($"probe manual global-only update: stack exposure {vm.stack.GetComponent<ColorAdjustments>().postExposure.value:0.00}, tonemap {vm.stack.GetComponent<Tonemapping>().mode.value}");
                var cases = new[] { ("none0", "{\"tonemap\":\"none\",\"exposure\":0,\"contrast\":0,\"saturation\":0,\"vignette\":0,\"bloom\":0}"),
                                    ("aces1.25", "{\"exposure\":1.25}"), ("aces1.75", "{\"exposure\":1.75}"), ("aces2.25", "{\"exposure\":2.25}"),
                                    ("neutral0.75", "{\"tonemap\":\"neutral\",\"exposure\":0.75}"), ("neutral1.25", "{\"tonemap\":\"neutral\",\"exposure\":1.25}") };
                foreach (var (name, js) in cases)
                {
                    var merged = (JObject)basePost.DeepClone();
                    merged.Merge(JObject.Parse(js));
                    ApplyPost(merged);
                    warm = 0;
                    Frame(p, dir + "/" + shot.id + "-" + name + ".jpg");
                    var stack = VolumeManager.instance.stack;
                    var sca = stack.GetComponent<ColorAdjustments>();
                    var stm = stack.GetComponent<Tonemapping>();
                    s_log.AppendLine($"probe {shot.id} {name}: luma {Luma():0.000}, stack exposure {sca.postExposure.value:0.00} active {sca.active}, tonemap {stm.mode.value}, " +
                        $"supports request {RenderPipeline.SupportsRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt })}, " +
                        $"pipeline {GraphicsSettings.currentRenderPipeline?.name}, cam post {cam.GetUniversalAdditionalCameraData().renderPostProcessing}");
                }
                // the same frame through Camera.Render instead of a render request
                var merged2 = (JObject)basePost.DeepClone(); merged2.Merge(JObject.Parse("{\"exposure\":2}")); ApplyPost(merged2);
                cam.targetTexture = rt; cam.Render(); cam.Render(); cam.targetTexture = null;
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); RenderTexture.active = prev;
                File.WriteAllBytes(dir + "/" + shot.id + "-camrender-exp2.jpg", tex.EncodeToJPG(94));
                s_log.AppendLine($"probe camrender exp2: luma {Luma():0.000}");
            }

            float Luma()
            {
                var px = tex.GetPixels32();
                double s = 0;
                for (int i = 0; i < px.Length; i += 7) s += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
                return (float)(s / (px.Length / 7.0) / 255.0);
            }

            void RenderOnce()
            {
                // a render request does not update the volume stack in the editor: blend the global volumes here
                VolumeManager.instance.Update(null, cam.GetUniversalAdditionalCameraData().volumeLayerMask);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }

            public void Dispose()
            {
                ShaderUtil.allowAsyncCompilation = prevAsync;
                if (ceilingsHidden) foreach (var g in ceilings) if (g != null) g.SetActive(true);
                if (sdoor != null) sdoor.hinge.localRotation = bookClosed;
                UnityEngine.Object.DestroyImmediate(cam.gameObject);
                if (volumeGo != null) UnityEngine.Object.DestroyImmediate(volumeGo);
                UnityEngine.Object.DestroyImmediate(moonGo);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
