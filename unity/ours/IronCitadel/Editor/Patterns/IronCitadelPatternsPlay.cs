using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IronCitadel.Conformance;
using IronCitadel.Play;
using IronCitadel.Rooms.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronCitadel.Patterns.Editor
{
    /// <summary>
    /// Makes the PATTERN-TILE level (IronCitadel_Patterns.unity) walkable like the rooms level, and walks it.
    ///
    /// <para><b>Play</b> (<see cref="BuildPlay"/>): the scene root "Play" holds the shared walker
    /// (Walker/IronCitadelPlayer.prefab, Starter Assets' PlayerCapsule) at PlayerStart facing north, a Goal at the
    /// stronghold's post, and the same play features as the rooms level: the HUD (room name, timer, the goal
    /// announcement) and the M overview. The room names come from the solver's pattern regions, written to
    /// <see cref="RegionsPath"/> from the stitched layout. A rebuild replaces only "Play" (and removes the new scene's
    /// default Main Camera, so the walker's camera is the only MainCamera).</para>
    ///
    /// <para><b>Walk</b> (<see cref="WalkAndCapture"/>): the validator's nine must_be_true checks all name
    /// level.json's rooms on its 45 m cells, so none applies here. What does carry over is check 10's method: the
    /// kit's own bake settings (NavBake.Settings) and the kit's Walker (a CharacterController r 0.5, h 2, step 0.25
    /// driven along NavMesh paths). It walks entry to the stronghold and out, through each flank, to the gallery
    /// (floor and walkway), the hidden cache and the sniper's perch; it also checks that every stand cell of the
    /// solver's layout is NavMesh reachable from the entry. The gate tests (<see cref="GateTests"/>) then close the
    /// named cells with tall boxes and rebake, to see whether each measured pattern (two ways round each neck, the
    /// cache behind one door, the keep behind its two) still holds on the built tiles. Writes
    /// <see cref="ScoresDir"/>/walk.md, walk.json, walk-map.png and gates-map.png, and eye captures to
    /// <see cref="CapturesDir"/>. The scene is never saved by the walk.</para>
    ///
    /// <para>Batch: <c>unity run &lt;Builder&gt; -- -executeMethod IronCitadel.Patterns.Editor.IronCitadelPatternsPlay.BuildAndWalk
    /// -logFile &lt;log&gt;</c> (or <c>.BuildPlayBatch</c>, <c>.WalkBatch</c>). Each writes <see cref="ResultPath"/>.</para>
    /// </summary>
    public static class IronCitadelPatternsPlay
    {
        public const string RegionsPath = "Assets/IronCitadel/Patterns/iron-citadel.regions.json";
        public const string PlayerPrefabPath = "Assets/IronCitadel/Walker/IronCitadelPlayer.prefab";
        public const string SolveDir = "C:/Repos/IronCitadel/patterns/iron-citadel";
        public const string ScoresDir = "C:/Repos/IronCitadel/scores/patterns";
        public const string CapturesDir = "C:/Repos/IronCitadel/captures/patterns";
        public const string ResultPath = "C:/Repos/IronCitadel/logs/patterns-play-result.txt";
        const string PlayRoot = "Play";
        const string StartName = "PlayerStart";

        // The solver's grid (iron-citadel-request.json): 3 x 4 slots of 20 x 20 cells of 5 m, the assembled prefab
        // centred on the origin, +z north, so the south-west corner is (-150, -200). Cell (x, z) counts from there.
        const float CellM = 5f;
        const int SlotCells = 20, SlotsX = 3, SlotsZ = 4;
        static readonly Vector2 Origin = new Vector2(-SlotsX * SlotCells * CellM / 2f, -SlotsZ * SlotCells * CellM / 2f);

        const float GoalReach = 5f;        // the post stands between the keep's two pillars; 5 m reads as "at the post"
        const float EyeHeight = 1.5f;      // capture eye over the floor (the PlayerCameraRoot is about this high)

        // The brief's room each slot on the walk stands for, from the request's note (iron-citadel-request.json):
        // in at the entry, the choke at the gate, round it by the west wing, the vault (hidden cache), the great hall
        // (gallery), the throne (stronghold).
        static readonly Dictionary<string, string> BriefRole = new Dictionary<string, string>
        {
            ["1,0"] = "entry", ["1,1"] = "gate", ["0,1"] = "west wing", ["0,2"] = "vault", ["1,2"] = "great hall", ["1,3"] = "throne"
        };

        // The stitched layout's area labels, as the HUD says them.
        static readonly Dictionary<string, string> AreaWord = new Dictionary<string, string>
        {
            ["flank"] = "flank", ["rejoin"] = "flank rejoins", ["neck"] = "the neck", ["perch"] = "sniper's perch",
            ["watched-ground"] = "watched ground", ["way-up"] = "way up", ["pocket"] = "the cache", ["trunk"] = "trunk passage",
            ["door"] = "hidden door", ["stairs"] = "stairs", ["floor"] = "floor", ["walkway"] = "walkway", ["keep"] = "the keep",
            ["post"] = "the post", ["door-1"] = "keep door", ["door-2"] = "keep door"
        };

        // ------------------------------------------------------------------ entry points

        [MenuItem("Iron Citadel/Patterns/Build play (walker, HUD, M map)")]
        public static void BuildPlayMenu()
        {
            var log = new StringBuilder();
            BuildPlay(log);
            Debug.Log(log.ToString());
        }

        [MenuItem("Iron Citadel/Patterns/Walk and capture")]
        public static void WalkMenu() => WalkBatch();

        public static void BuildPlayBatch() => Run(BuildPlay);
        public static void WalkBatch() => Run(WalkAndCapture);
        public static void BuildAndWalk() => Run(log => { BuildPlay(log); WalkAndCapture(log); });

        static void Run(Action<StringBuilder> body)
        {
            var log = new StringBuilder();
            log.AppendLine($"IronCitadelPatternsPlay {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            try
            {
                body(log);
                log.AppendLine("RESULT ok");
            }
            catch (Exception e)
            {
                log.AppendLine("RESULT failed: " + e);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, log.ToString());
            Debug.Log(log.ToString());
        }

        // ------------------------------------------------------------------ the solver's layout

        sealed class Area
        {
            public string Name, Word, Display;
            public int SX, SZ;
            public readonly List<Vector2Int> Cells = new List<Vector2Int>();
            public string Slot => SX + "," + SZ;

            public Vector2Int Middle
            {
                get
                {
                    var c = new Vector2((float)Cells.Average(p => p.x), (float)Cells.Average(p => p.y));
                    return Cells.OrderBy(p => (new Vector2(p.x, p.y) - c).sqrMagnitude).First();
                }
            }
        }

        sealed class Layout
        {
            public string[] Ground;        // row 0 is the NORTH edge (z = H - 1)
            public int W, H;
            public readonly List<Area> Areas = new List<Area>();
            public readonly Dictionary<string, string> SlotPattern = new Dictionary<string, string>();
            public readonly Dictionary<string, string> SlotTile = new Dictionary<string, string>();

            public char At(int x, int z) => x < 0 || z < 0 || x >= W || z >= H ? '#' : Ground[H - 1 - z][x];
            public bool Stand(int x, int z) => At(x, z) != '#';
            public bool High(int x, int z) => At(x, z) == '1';
            public Area Find(string slot, string word) => Areas.FirstOrDefault(a => a.Slot == slot && a.Word == word);

            public string SlotDisplay(string slot)
            {
                if (!SlotPattern.TryGetValue(slot, out var pattern) || pattern == "rock") return "Solid rock";
                string name = Capitalise(pattern.Replace('-', ' '));
                return BriefRole.TryGetValue(slot, out var role) ? name + " (" + role + ")" : name;
            }

            public static Layout Load(StringBuilder log)
            {
                string stitched = SolveDir + "/stitched-layout.json", plan = SolveDir + "/place-plan.json";
                if (!File.Exists(stitched)) throw new FileNotFoundException("the solver's stitched layout is missing", stitched);
                var j = JObject.Parse(File.ReadAllText(stitched));
                var L = new Layout { Ground = j["ground"].Select(t => (string)t).ToArray() };
                L.H = L.Ground.Length;
                L.W = L.Ground[0].Length;

                foreach (var a in j["areas"])
                {
                    string full = (string)a["name"];
                    int colon = full.IndexOf(':');
                    var slot = full.Substring(0, colon).Split(',');
                    var area = new Area
                    {
                        Name = full, Word = full.Substring(colon + 1),
                        SX = int.Parse(slot[0], CultureInfo.InvariantCulture), SZ = int.Parse(slot[1], CultureInfo.InvariantCulture)
                    };
                    if (a["cells"] is JArray cells)
                        foreach (var c in cells) area.Cells.Add(new Vector2Int((int)c[0], (int)c[1]));
                    else
                    {
                        Range((string)a["x"], out int x0, out int x1);
                        Range((string)a["z"], out int z0, out int z1);
                        for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                            area.Cells.Add(new Vector2Int(x, z));
                    }
                    area.Display = AreaWord.TryGetValue(area.Word, out var w) ? w : area.Word.Replace('-', ' ');
                    L.Areas.Add(area);
                }

                // place-plan: one demand per slot, cell index = z * 3 + x; the pattern placed there, or the tile's own
                // pattern for a slot the walk passes with no want in it.
                foreach (var d in JObject.Parse(File.ReadAllText(plan))["demands"])
                {
                    int cell = (int)d["cell"];
                    string slot = (cell % SlotsX) + "," + (cell / SlotsX);
                    string label = (string)d["label"];
                    string place = d["place"]?.Type == JTokenType.String ? (string)d["place"] : null;
                    L.SlotTile[slot] = label;
                    L.SlotPattern[slot] = place ?? TilePattern(label);
                }
                log.AppendLine($"layout: {L.W} x {L.H} cells, {L.Areas.Count} areas, slots {string.Join("; ", L.SlotPattern.Where(kv => kv.Value != "rock").Select(kv => kv.Key + " " + kv.Value))}");
                return L;
            }

            static void Range(string s, out int lo, out int hi)
            {
                var parts = s.Split(new[] { ".." }, StringSplitOptions.None);
                lo = int.Parse(parts[0], CultureInfo.InvariantCulture);
                hi = int.Parse(parts[parts.Length - 1], CultureInfo.InvariantCulture);
            }

            // "d-20-sniper-location-013@3" -> "sniper-location"; "d-20-rock@0" -> "rock"
            static string TilePattern(string label)
            {
                var m = Regex.Match(label ?? "", @"^d-\d+-(.+?)(-\d+)?@\d+$");
                return m.Success ? m.Groups[1].Value : label;
            }
        }

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static Vector3 CellCentre(int x, int z) => new Vector3(Origin.x + (x + 0.5f) * CellM, 0f, Origin.y + (z + 0.5f) * CellM);
        static Vector3 CellCentre(Vector2Int c) => CellCentre(c.x, c.y);

        /// <summary>The regions file the HUD reads (PatternRegionMap): every slot named after its pattern and the
        /// brief's room, and every area of the stitched layout, smallest first (so a cell takes its most specific name).</summary>
        static void WriteRegions(Layout L, StringBuilder log)
        {
            string N(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);
            string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"format\": \"iron-citadel-pattern-regions/1\",");
            sb.AppendLine("  \"id\": \"iron-citadel\",");
            sb.AppendLine("  \"note\": " + Q("Written by IronCitadel.Patterns.Editor.IronCitadelPatternsPlay from " + SolveDir + "/stitched-layout.json and place-plan.json. Cell (x, z) spans world x origin.x + 5x .. +5, z origin.z + 5z .. +5; slot = cell / 20.") + ",");
            sb.AppendLine($"  \"origin_xz\": [{N(Origin.x)}, {N(Origin.y)}],");
            sb.AppendLine($"  \"cell_m\": {N(CellM)},");
            sb.AppendLine($"  \"cells\": [{L.W}, {L.H}],");
            sb.AppendLine($"  \"slot_cells\": {SlotCells},");
            sb.AppendLine("  \"slots\": [");
            var slots = new List<string>();
            for (int z = 0; z < SlotsZ; z++)
            for (int x = 0; x < SlotsX; x++)
            {
                string id = x + "," + z;
                slots.Add($"    {{ \"slot\": [{x}, {z}], \"id\": {Q(id)}, \"name\": {Q(L.SlotDisplay(id))} }}");
            }
            sb.AppendLine(string.Join("," + Environment.NewLine, slots));
            sb.AppendLine("  ],");
            sb.AppendLine("  \"areas\": [");
            var areas = L.Areas.OrderBy(a => a.Cells.Count).Select(a =>
                $"    {{ \"name\": {Q(a.Display)}, \"slot\": [{a.SX}, {a.SZ}], \"from\": {Q(a.Name)}, \"cells\": [{string.Join(", ", a.Cells.Select(c => c.x + ", " + c.y))}] }}");
            sb.AppendLine(string.Join("," + Environment.NewLine, areas));
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            Directory.CreateDirectory(Path.GetDirectoryName(RegionsPath));
            string text = sb.ToString();
            bool same = File.Exists(RegionsPath) && File.ReadAllText(RegionsPath) == text;
            if (!same) File.WriteAllText(RegionsPath, text);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine($"regions: {RegionsPath} ({(same ? "unchanged" : "written")}, {slots.Count} slots, {L.Areas.Count} areas)");
        }

        // ------------------------------------------------------------------ play: walker, goal, HUD, overview

        public static void BuildPlay(StringBuilder log)
        {
            var L = Layout.Load(log);
            WriteRegions(L, log);
            var regionsAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(RegionsPath);
            if (regionsAsset == null) throw new InvalidOperationException("the regions file did not import as a TextAsset: " + RegionsPath);

            Scene scene = EditorSceneManager.OpenScene(IronCitadelPatternsScene.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var level = roots.FirstOrDefault(r => r.name == IronCitadelPatternsScene.LevelName)
                        ?? throw new InvalidOperationException("no root '" + IronCitadelPatternsScene.LevelName + "' in " + scene.path + "; run Iron Citadel/Patterns/Build scene");
            var ps = roots.FirstOrDefault(r => r.name == StartName) ?? throw new InvalidOperationException("no PlayerStart in " + scene.path);
            foreach (var r in roots)
            {
                if (r.name == PlayRoot) { log.AppendLine("replacing root: Play"); Object.DestroyImmediate(r); continue; }
                var cam = r.GetComponent<Camera>();
                if (cam != null && r.CompareTag("MainCamera"))
                {
                    log.AppendLine("removing the default camera '" + r.name + "' (the walker brings the MainCamera)");
                    Object.DestroyImmediate(r);
                }
            }
            var play = new GameObject(PlayRoot);
            SceneManager.MoveGameObjectToScene(play, scene);

            // the walker, its capsule's feet on PlayerStart, turned to its yaw (north)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath)
                         ?? throw new InvalidOperationException("missing " + PlayerPrefabPath + " (the walker kit makes it)");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = "IronCitadelPlayer";
            player.transform.SetParent(play.transform, true);
            var capsule = player.GetComponentsInChildren<Transform>(true).First(t => t.name == "PlayerCapsule");
            float dyaw = Mathf.DeltaAngle(capsule.eulerAngles.y, ps.transform.eulerAngles.y);
            player.transform.RotateAround(capsule.position, Vector3.up, dyaw);
            player.transform.position += ps.transform.position + Vector3.up * 0.05f - capsule.position;
            var camRoot = capsule.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PlayerCameraRoot");
            var mainCam = player.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (mainCam != null && camRoot != null) mainCam.transform.SetPositionAndRotation(camRoot.position, camRoot.rotation);
            log.AppendLine($"walker {PlayerPrefabPath} at {capsule.position} yaw {capsule.eulerAngles.y:0}; camera root {(camRoot != null ? camRoot.position.ToString() : "none")}");

            // the goal: the stronghold's post (between the keep's pillars), else the keep's middle
            var post = L.Find("1,3", "post") ?? L.Find("1,3", "keep");
            Vector3 goalPos = post != null ? PostCentre(post) : Vector3.zero;
            goalPos.y = FloorUnder(goalPos);
            var goal = new GameObject("Goal");
            goal.transform.SetParent(play.transform, false);
            goal.transform.position = goalPos;
            try { goal.tag = "Finish"; } catch { }
            var trig = goal.AddComponent<SphereCollider>();
            trig.isTrigger = true; trig.radius = 2f; trig.center = Vector3.up;
            log.AppendLine($"Goal (the stronghold's {(post != null ? post.Word : "?")}) at {goalPos}, reach {GoalReach} m");

            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(play.transform, false);
            var hud = hudGo.AddComponent<IronCitadelHud>();
            hud.level = null;
            hud.regions = regionsAsset;
            hud.player = capsule;
            hud.goal = goal.transform;
            hud.goalReach = GoalReach;
            hud.goalWord = "stronghold";

            float w = SlotsX * SlotCells * CellM, d = SlotsZ * SlotCells * CellM;
            var camGo = new GameObject("OverviewCamera");
            camGo.transform.SetParent(play.transform, false);
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 400f, 0f), Quaternion.Euler(90f, 0f, 0f));
            var ocam = camGo.AddComponent<Camera>();
            ocam.orthographic = true;
            ocam.orthographicSize = d / 2f + 4f;
            ocam.nearClipPlane = 1f;
            ocam.farClipPlane = 500f;
            ocam.clearFlags = CameraClearFlags.SolidColor;
            ocam.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            ocam.enabled = false;

            // the level is open to the sky and lit by the scene's sun, so the overview's own light is a soft fill
            var lightGo = new GameObject("OverviewLight");
            lightGo.transform.SetParent(play.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(65f, 30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 0.5f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var ov = hudGo.AddComponent<IronCitadelOverview>();
            ov.overviewCamera = ocam;
            ov.overviewLight = light;
            ov.levelRoot = level.transform;
            ov.player = capsule;
            ov.centreXZ = Vector2.zero;
            ov.sizeXZ = new Vector2(w, d);
            log.AppendLine("play: HUD (pattern-region names, time, stronghold) and M overview added under Play");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("SaveScene failed: " + scene.path);
            AssetDatabase.SaveAssets();
            log.AppendLine("scene saved: " + scene.path);
        }

        static Vector3 PostCentre(Area a)
        {
            var c = Vector3.zero;
            foreach (var p in a.Cells) c += CellCentre(p);
            return c / a.Cells.Count;
        }

        static float FloorUnder(Vector3 p)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out var hit, 10f) ? hit.point.y : 0f;
        }

        // ------------------------------------------------------------------ walk

        sealed class Ctx
        {
            public Layout L;
            public PatternRegionMap Regions;
            public NavModel M;
            public NavMeshQueryFilter Filter;
            public Vector3 Start;
            public int StartComp = -1;

            public string Where(Vector3 p) => Regions.RoomNameAt(p, out _);

            int CompOf(Vector3 p)
            {
                int t = M.FindTri(p, 0.6f, false);
                return t < 0 ? -1 : M.Comp[t];
            }

            /// <summary>A NavMesh point in cell (x, z), on the deck for a band-1 cell and on the floor otherwise;
            /// a point joined to the entry is preferred (that drops wall tops and table tops).</summary>
            public Vector3? Point(int x, int z, bool? high = null, bool joinedOnly = false)
            {
                bool up = high ?? L.High(x, z);
                var c = CellCentre(x, z);
                var found = new List<Vector3>();
                for (float y = -1.5f; y <= 12f; y += 0.75f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(c.x, y, c.z), out var h, 1f, Filter)) continue;
                    if (Mathf.Abs(h.position.x - c.x) > CellM / 2f || Mathf.Abs(h.position.z - c.z) > CellM / 2f) continue;
                    if (!found.Any(f => (f - h.position).sqrMagnitude < 0.01f)) found.Add(h.position);
                }
                if (found.Count == 0) return null;
                var joined = found.Where(f => StartComp >= 0 && CompOf(f) == StartComp).ToList();
                if (joined.Count == 0 && joinedOnly) return null;
                var pool = joined.Count > 0 ? joined : found;
                return up ? pool.OrderByDescending(f => f.y).First() : pool.OrderBy(f => Mathf.Abs(f.y)).First();
            }

            public Vector3? Point(Vector2Int c, bool? high = null) => Point(c.x, c.y, high);

            public bool Joined(Vector3 p) => StartComp >= 0 && CompOf(p) == StartComp;
        }

        sealed class RouteDef
        {
            public string Name, Why;
            public readonly List<(string label, Vector3? p)> Stops = new List<(string, Vector3?)>();
        }

        sealed class RegionRow
        {
            public string Region, Slot, Status;
            public Vector3? At;
            public float PathM;
        }

        public static void WalkAndCapture(StringBuilder log)
        {
            var L = Layout.Load(log);
            if (!File.Exists(RegionsPath)) throw new FileNotFoundException("run BuildPlay first", RegionsPath);
            var ctx = new Ctx { L = L, Regions = PatternRegionMap.Parse(File.ReadAllText(RegionsPath)) };

            Scene scene = EditorSceneManager.OpenScene(IronCitadelPatternsScene.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var level = roots.FirstOrDefault(r => r.name == IronCitadelPatternsScene.LevelName) ?? throw new InvalidOperationException("no level root");
            var ps = roots.FirstOrDefault(r => r.name == StartName) ?? throw new InvalidOperationException("no PlayerStart");
            var play = roots.FirstOrDefault(r => r.name == PlayRoot);
            var goalT = play != null ? play.transform.Find("Goal") : null;
            Directory.CreateDirectory(ScoresDir);
            Directory.CreateDirectory(CapturesDir);

            bool playWasActive = play != null && play.activeSelf;
            if (play != null) play.SetActive(false);
            var guard = new SceneGuard(scene);
            NavMeshDataInstance inst = default;
            NavMeshData data = null;
            var walks = new List<(RouteDef def, WalkResult res)>();
            var regionRows = new List<RegionRow>();
            var gates = new List<GateTest>();
            var unreachable = new List<Vector2Int>();
            var noMesh = new List<Vector2Int>();
            int standCells = 0, standJoined = 0, rockJoined = 0, rockMesh = 0;
            float meshArea = 0f, joinedArea = 0f;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // ---- bake: the kit's agent and voxel settings, over the level (centred on the origin) plus a margin
                var settings = NavBake.Settings();
                data = BakeData(scene, settings, out int sourceCount);
                inst = NavMesh.AddNavMeshData(data);
                ctx.Filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
                // the kit's opening graph needs level.json's cells; with an empty spec it still welds the triangulation
                // and finds its connected pieces, which is all this walk uses (plus the Filter, for Walker.Walk)
                ctx.M = NavModel.FromTriangulation(new LevelSpec { Name = "pattern-tile level (no 45 m grid)" }, ctx.Filter, guard.Links);
                log.AppendLine($"bake: {sourceCount} sources, {ctx.M.T} triangles, {ctx.M.CompCount} pieces, {sw.Elapsed.TotalSeconds:0.0} s (agent r {settings.agentRadius} h {settings.agentHeight} step {settings.agentClimb} slope {settings.agentSlope}, voxel {settings.voxelSize})");

                // ---- the entry
                if (!NavMesh.SamplePosition(ps.transform.position, out var sh, 2f, ctx.Filter))
                    throw new InvalidOperationException("no NavMesh within 2 m of PlayerStart " + ps.transform.position);
                ctx.Start = sh.position;
                int st = ctx.M.FindTri(ctx.Start, 0.6f, false);
                ctx.StartComp = st >= 0 ? ctx.M.Comp[st] : -1;
                for (int t = 0; t < ctx.M.T; t++)
                {
                    meshArea += ctx.M.Area[t];
                    if (ctx.M.Comp[t] == ctx.StartComp) joinedArea += ctx.M.Area[t];
                }
                log.AppendLine($"start {ctx.Start}; NavMesh {meshArea:0} m2, {joinedArea:0} m2 of it joined to the entry");

                // ---- the layout against the build: every stand cell should be NavMesh joined to the entry
                for (int z = 0; z < L.H; z++)
                for (int x = 0; x < L.W; x++)
                {
                    var any = ctx.Point(x, z);    // prefers a point joined to the entry when the cell has one
                    bool joined = any.HasValue && ctx.Joined(any.Value);
                    if (L.Stand(x, z))
                    {
                        standCells++;
                        if (joined) standJoined++;
                        else if (any.HasValue) unreachable.Add(new Vector2Int(x, z));
                        else noMesh.Add(new Vector2Int(x, z));
                    }
                    else
                    {
                        if (any.HasValue) rockMesh++;
                        if (joined) rockJoined++;
                    }
                }
                log.AppendLine($"layout: {standJoined}/{standCells} stand cells joined to the entry; {unreachable.Count} with NavMesh not joined, {noMesh.Count} with no NavMesh; {rockJoined} rock cells joined ({rockMesh} with any NavMesh)");

                // ---- the walks
                var routes = Routes(ctx, goalT != null ? goalT.position : (Vector3?)null, log);
                foreach (var r in routes)
                {
                    var res = r.Stops.Any(s => s.p == null)
                        ? WalkResult.Missing(r.Name, "no NavMesh at " + string.Join(", ", r.Stops.Where(s => s.p == null).Select(s => s.label)))
                        : Walker.Walk(ctx.M, r.Name, r.Stops.Select(s => s.p).ToList());
                    foreach (var s in res.Snags) s.Node = ctx.Where(s.At);
                    if (!res.Pass && res.Snags.Count > 0) res.Note = res.Snags[0].What + " at " + V(res.Snags[0].At) + " in " + res.Snags[0].Node;
                    walks.Add((r, res));
                    log.AppendLine($"walk '{r.Name}': {(res.Pass ? "PASS" : "FAIL")} planned {res.Planned:0} m walked {res.Walked:0} m; {res.Note}");
                }

                // ---- every pattern region: a complete NavMesh path from the entry?
                foreach (var a in L.Areas.OrderBy(a => a.SZ).ThenBy(a => a.SX).ThenBy(a => a.Name))
                {
                    var p = ctx.Point(a.Middle);
                    var row = new RegionRow { Region = L.SlotDisplay(a.Slot) + " \u00b7 " + a.Display + "  (" + a.Name + ")", Slot = a.Slot, At = p };
                    if (p == null) row.Status = "no NavMesh at its middle cell";
                    else
                    {
                        var path = new NavMeshPath();
                        NavMesh.CalculatePath(ctx.Start, p.Value, ctx.Filter, path);
                        row.Status = path.status.ToString();
                        for (int i = 1; i < path.corners.Length; i++) row.PathM += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    }
                    regionRows.Add(row);
                }

                // ---- captures (the walker and HUD stay switched off; the scene's own sun lights the eye shots)
                Captures(ctx, walks, unreachable, goalT != null ? goalT.position : (Vector3?)null, log);

                // ---- do the measured patterns hold in the build? close a way, re-bake, look for a path (last: it re-bakes)
                gates = GateTests(ctx, scene, settings, ref data, ref inst, log);
            }
            finally
            {
                if (inst.valid) inst.Remove();
                if (data != null) Object.DestroyImmediate(data);
                guard.Restore();
                if (play != null) play.SetActive(playWasActive);
            }

            // the M overview as Play would show it (16:9), through the scene's own OverviewCamera
            if (play != null) OverviewPlayCapture(play, log);

            WriteReport(ctx, walks, regionRows, gates, unreachable, noMesh, standCells, standJoined, rockJoined, rockMesh, meshArea, joinedArea, log);
            CopySolverReport(log);
            log.AppendLine($"walk done in {sw.Elapsed.TotalSeconds:0} s");
        }

        static NavMeshData BakeData(Scene scene, NavMeshBuildSettings settings, out int sourceCount)
        {
            const float margin = 10f;
            float w = SlotsX * SlotCells * CellM, d = SlotsZ * SlotCells * CellM;
            var bounds = new Bounds(new Vector3(0f, (Tune.BakeTop + Tune.BakeBottom) * 0.5f, 0f),
                new Vector3(w + 2 * margin, Tune.BakeTop - Tune.BakeBottom, d + 2 * margin));
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
#pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.CollectSourcesInStage(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                false, markups, false, scene, sources);
#pragma warning restore CS0618
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("BuildNavMeshData returned null");
            data.hideFlags = HideFlags.HideAndDontSave;
            sourceCount = sources.Count;
            return data;
        }

        // ------------------------------------------------------------------ gate tests: do the measured patterns hold in the build?

        sealed class GateTest
        {
            public string Pattern, Closed, Expect, Detail;
            public bool ExpectThrough, Through, Pass, Ran;
            public readonly List<Vector2Int> Blocked = new List<Vector2Int>();
            public readonly List<Vector3> Path = new List<Vector3>();
        }

        /// <summary>The solver measured its patterns on the stitched layout, not on the built tiles, and the built tiles
        /// carry floor the layout calls rock (side rooms). So each claim that hangs on a way through is re-tested on the
        /// build: tall boxes close the named cells, the NavMesh is re-baked with the same settings, and a path is looked
        /// for between the two sides. A choke with its flank: closing either one still lets you through, closing both does
        /// not. The hidden cache: closing its door's mouth cuts it off. The stronghold: closing both keep doors cuts it off.</summary>
        static List<GateTest> GateTests(Ctx c, Scene scene, NavMeshBuildSettings settings, ref NavMeshData data, ref NavMeshDataInstance inst, StringBuilder log)
        {
            var L = c.L;
            var plans = new List<(GateTest t, Vector3? a, Vector3? b)>();
            GateTest T(string pattern, string closed, bool expectThrough, IEnumerable<Vector2Int> blocked)
            {
                var t = new GateTest
                {
                    Pattern = pattern, Closed = closed, ExpectThrough = expectThrough,
                    Expect = expectThrough ? "still a way through" : "no way through"
                };
                t.Blocked.AddRange(blocked.Distinct());
                return t;
            }

            foreach (var slot in new[] { "1,0", "0,1" })
            {
                var neck = L.Find(slot, "neck");
                var flank = L.Find(slot, "flank");
                if (neck == null || flank == null) continue;
                int x0 = neck.Cells.Min(p => p.x), x1 = neck.Cells.Max(p => p.x), z0 = neck.Cells.Min(p => p.y), z1 = neck.Cells.Max(p => p.y);
                Vector2Int na, nb;
                if (z1 - z0 >= x1 - x0) { int xm = (x0 + x1) / 2; na = new Vector2Int(xm, z0 - 3); nb = new Vector2Int(xm, z1 + 3); }
                else { int zm = (z0 + z1) / 2; na = new Vector2Int(x1 + 3, zm); nb = new Vector2Int(x0 - 3, zm); }
                Vector3? a = c.Point(na, false), b = c.Point(nb, false);
                int m = flank.Cells.Count / 2;
                var flankBlock = flank.Cells.Skip(Math.Max(0, m - 1)).Take(3).ToList();
                string name = L.SlotDisplay(slot) + ", slot " + slot + ": " + Cell(na) + " to " + Cell(nb);
                plans.Add((T(name, "the neck " + Cells(neck.Cells), true, neck.Cells), a, b));
                plans.Add((T(name, "the flank at " + Cells(flankBlock), true, flankBlock), a, b));
                plans.Add((T(name, "the neck and the flank", false, neck.Cells.Concat(flankBlock)), a, b));
            }

            var pocket = L.Find("0,2", "pocket");
            var door = L.Find("0,2", "door");
            if (pocket != null && door != null)
            {
                var inPocket = new HashSet<Vector2Int>(pocket.Cells);
                var mouth = door.Cells.Where(p => !inPocket.Contains(p) && Neighbours(p).Any(inPocket.Contains)).ToList();
                plans.Add((T(L.SlotDisplay("0,2") + ", slot 0,2: the entry to the cache " + Cell(pocket.Middle), "the hidden door's mouth " + Cells(mouth), false, mouth),
                    c.Start, c.Point(pocket.Middle, false)));
            }

            var keep = L.Find("1,3", "keep");
            var d1 = L.Find("1,3", "door-1");
            var d2 = L.Find("1,3", "door-2");
            if (keep != null && d1 != null && d2 != null)
                plans.Add((T(L.SlotDisplay("1,3") + ", slot 1,3: the entry to the keep " + Cell(keep.Middle), "both keep doors " + Cells(d1.Cells.Concat(d2.Cells)), false, d1.Cells.Concat(d2.Cells)),
                    c.Start, c.Point(keep.Middle, false)));

            const float pad = 0.5f;
            foreach (var (t, a, b) in plans)
            {
                if (a == null || b == null) { t.Detail = "not run: no NavMesh at one side"; continue; }
                var boxes = new List<GameObject>();
                try
                {
                    foreach (var cell in t.Blocked)
                    {
                        var go = new GameObject("~gate-block " + Cell(cell));
                        SceneManager.MoveGameObjectToScene(go, scene);
                        var bc = go.AddComponent<BoxCollider>();
                        go.transform.position = CellCentre(cell) + Vector3.up * 9f;
                        bc.size = new Vector3(CellM + 2 * pad, 20f, CellM + 2 * pad);
                        boxes.Add(go);
                    }
                    Physics.SyncTransforms();
                    if (inst.valid) inst.Remove();
                    if (data != null) Object.DestroyImmediate(data);
                    data = BakeData(scene, settings, out _);
                    inst = NavMesh.AddNavMeshData(data);

                    bool okA = NavMesh.SamplePosition(a.Value, out var ha, 2f, c.Filter), okB = NavMesh.SamplePosition(b.Value, out var hb, 2f, c.Filter);
                    if (!okA || !okB) { t.Detail = "not run: a side lost its NavMesh"; continue; }
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(ha.position, hb.position, c.Filter, path);
                    t.Ran = true;
                    t.Through = path.status == NavMeshPathStatus.PathComplete;
                    t.Pass = t.Through == t.ExpectThrough;
                    if (t.Through)
                    {
                        t.Path.AddRange(path.corners);
                        float len = 0f;
                        for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                        // the regions it passes, and the cells it crosses that the layout calls rock
                        var where = new List<string>();
                        var rock = new List<Vector2Int>();
                        for (int i = 1; i < path.corners.Length; i++)
                        {
                            var p0 = path.corners[i - 1]; var p1 = path.corners[i];
                            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p0, p1) / 1f));
                            for (int k = 0; k <= n; k++)
                            {
                                var p = Vector3.Lerp(p0, p1, k / (float)n);
                                string w = c.Where(p);
                                if (where.Count == 0 || where[where.Count - 1] != w) where.Add(w);
                                var cell = new Vector2Int(Mathf.FloorToInt((p.x - Origin.x) / CellM), Mathf.FloorToInt((p.z - Origin.y) / CellM));
                                if (!L.Stand(cell.x, cell.y) && !rock.Contains(cell)) rock.Add(cell);
                            }
                        }
                        t.Detail = $"path {len:0} m via {string.Join(" > ", where)}" + (rock.Count > 0 ? $"; it crosses {rock.Count} cell(s) the layout calls rock: {Cells(rock.Take(16))}{(rock.Count > 16 ? " ..." : "")}" : "; every cell it crosses is a layout stand cell");
                    }
                    else t.Detail = "no complete path (" + path.status + ")";
                }
                finally
                {
                    foreach (var go in boxes) if (go != null) Object.DestroyImmediate(go);
                    Physics.SyncTransforms();
                }
                log.AppendLine($"gate {(t.Pass ? "PASS" : t.Ran ? "FAIL" : "n/a")}: {t.Pattern}, closed {t.Closed}: expect {t.Expect}; {t.Detail}");
            }

            // a map of the gate tests: the closed cells in magenta, any path found in red (green where a way was expected)
            string src = CapturesDir + "/overview.png", map = ScoresDir + "/gates-map.png";
            if (File.Exists(src))
            {
                const float half = 200f;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(src));
                float m2p = tex.width / (2f * half);
                Vector2 P(Vector3 w) => new Vector2((w.x + half) * m2p, (w.z + half) * m2p);
                foreach (var (t, _, _) in plans)
                {
                    foreach (var cell in t.Blocked) Box(tex, P(CellCentre(cell)), CellM * m2p * 0.5f, new Color32(255, 0, 255, 255), 3);
                    var col = t.ExpectThrough ? new Color32(60, 255, 90, 255) : new Color32(255, 30, 30, 255);
                    for (int i = 1; i < t.Path.Count; i++) Line(tex, P(t.Path[i - 1]), P(t.Path[i]), col, t.ExpectThrough ? 2 : 4);
                }
                tex.Apply();
                File.WriteAllBytes(map, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                log.AppendLine("capture: " + map);
            }
            return plans.Select(p => p.t).ToList();
        }

        static IEnumerable<Vector2Int> Neighbours(Vector2Int p)
        {
            yield return p + Vector2Int.right; yield return p + Vector2Int.left; yield return p + Vector2Int.up; yield return p + Vector2Int.down;
        }

        static string Cells(IEnumerable<Vector2Int> cells) => string.Join(" ", cells.Select(Cell));

        static List<RouteDef> Routes(Ctx c, Vector3? goal, StringBuilder log)
        {
            var L = c.L;
            var list = new List<RouteDef>();
            RouteDef R(string name, string why)
            {
                var r = new RouteDef { Name = name, Why = why };
                r.Stops.Add(("the entry (PlayerStart)", c.Start));
                list.Add(r);
                return r;
            }
            (string, Vector3?) Stop(string label, Vector2Int cell, bool? high = null) => (label + " " + Cell(cell), c.Point(cell, high));

            var post = L.Find("1,3", "post") ?? L.Find("1,3", "keep");
            var goalCell = post?.Middle ?? new Vector2Int(29, 70);
            Vector3? goalPoint = goal.HasValue && NavMesh.SamplePosition(goal.Value, out var gh, 3f, c.Filter) ? gh.position : c.Point(goalCell);
            R("entry to the stronghold (the goal)", "the whole walk: choke, round the gate, west wing, cache side, gallery, keep").Stops.Add(("the stronghold's post", goalPoint));

            // the exit: the north rim at the connection the layout declares ("north 1/2" -> the middle of slot (1,3))
            int exitX = L.W / 2 - 1;
            for (int x = 0; x < L.W; x++) if (L.Stand(x, L.H - 1)) { exitX = x; break; }
            R("entry to the exit, out of the north rim", "in by the south door, out by the north one").Stops.Add(Stop("the exit", new Vector2Int(exitX, L.H - 1)));

            foreach (var slot in new[] { "1,0", "0,1" })
            {
                var flank = L.Find(slot, "flank");
                if (flank == null) { log.AppendLine("no flank in slot " + slot); continue; }
                var cells = flank.Cells;
                // walk it from the end nearer the entry, so the flank itself is walked end to end
                var first = c.Point(cells[0]); var last = c.Point(cells[cells.Count - 1]);
                float dFirst = PathLength(c, first), dLast = PathLength(c, last);
                bool forward = dFirst <= dLast;
                var a = forward ? cells[0] : cells[cells.Count - 1];
                var b = forward ? cells[cells.Count - 1] : cells[0];
                var mid = cells[cells.Count / 2];
                var r = R("through the flank in " + L.SlotDisplay(slot), "the second way round the neck at " + (L.Find(slot, "neck") != null ? Cell(L.Find(slot, "neck").Middle) : "?") + ", end to end");
                r.Stops.Add(Stop("flank end", a));
                r.Stops.Add(Stop("flank middle", mid));
                r.Stops.Add(Stop("flank end", b));
            }

            var walkway = L.Find("1,2", "walkway");
            if (walkway != null) R("entry up to the gallery walkway", "the raised gallery, up its stairs").Stops.Add(Stop("the walkway", walkway.Middle, true));
            var floor = L.Find("1,2", "floor");
            if (floor != null) R("entry to the gallery floor", "the floor the walkway overlooks").Stops.Add(Stop("the gallery floor", floor.Middle, false));
            var pocket = L.Find("0,2", "pocket");
            if (pocket != null) R("entry to the hidden cache", "the pocket behind the hidden door").Stops.Add(Stop("the cache", pocket.Middle, false));
            var perch = L.Find("1,1", "perch");
            if (perch != null) R("entry up to the sniper's perch", "the raised perch over the watched ground, by its way up").Stops.Add(Stop("the perch", perch.Middle, true));
            return list;
        }

        static float PathLength(Ctx c, Vector3? p)
        {
            if (p == null) return float.MaxValue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(c.Start, p.Value, c.Filter, path) || path.status != NavMeshPathStatus.PathComplete) return float.MaxValue;
            float s = 0f;
            for (int i = 1; i < path.corners.Length; i++) s += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return s;
        }

        static string Cell(Vector2Int c) => "(" + c.x + "," + c.y + ")";
        static string V(Vector3 p) => "(" + p.x.ToString("0.#", CultureInfo.InvariantCulture) + ", " + p.y.ToString("0.#", CultureInfo.InvariantCulture) + ", " + p.z.ToString("0.#", CultureInfo.InvariantCulture) + ")";
        static string F(float f) => f.ToString("0", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ captures

        static void Captures(Ctx c, List<(RouteDef def, WalkResult res)> walks, List<Vector2Int> unreachable, Vector3? goal, StringBuilder log)
        {
            var L = c.L;
            // the map: top-down, flat-lit, 4 px/m, north up, the 100 m slot grid in yellow; then the walks drawn on it
            const float half = 200f;
            const int px = 1600;
            string map = ScoresDir + "/walk-map.png";
            IronCitadelCapture.TopDown(map, Vector2.zero, half, px, true, 300f, SlotCells * CellM, Origin);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(map));
            float m2p = px / (2f * half);
            Vector2 P(Vector3 w) => new Vector2((w.x + half) * m2p, (w.z + half) * m2p);
            foreach (var u in unreachable) Box(tex, P(CellCentre(u)), CellM * m2p * 0.45f, new Color32(255, 40, 40, 255), 2);
            var colours = new[]
            {
                new Color32(255, 255, 255, 255), new Color32(0, 230, 255, 255), new Color32(255, 120, 0, 255), new Color32(190, 90, 255, 255),
                new Color32(60, 255, 90, 255), new Color32(255, 230, 0, 255), new Color32(255, 80, 180, 255), new Color32(120, 170, 255, 255)
            };
            for (int i = 0; i < walks.Count; i++)
            {
                var res = walks[i].res;
                var col = colours[i % colours.Length];
                for (int k = 1; k < res.Trace.Count; k++) Line(tex, P(res.Trace[k - 1]), P(res.Trace[k]), col, 2);
                foreach (var s in res.Snags) Cross(tex, P(s.At), 10, new Color32(255, 0, 0, 255));
            }
            Box(tex, P(c.Start), 7, new Color32(0, 255, 0, 255), 4);
            if (goal.HasValue) Box(tex, P(goal.Value), 7, new Color32(255, 200, 0, 255), 4);
            tex.Apply();
            File.WriteAllBytes(map, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            log.AppendLine("capture: " + map);

            string top = CapturesDir + "/overview.png";
            IronCitadelCapture.TopDown(top, Vector2.zero, half, px, true, 300f, SlotCells * CellM, Origin);
            log.AppendLine("capture: " + top);

            // eye shots along the walk: (name, stand cell, look-at cell, stand high?, look high?)
            var shots = new List<(string name, Vector3? eye, Vector3? at)>();
            void Shot(string name, Vector3? stand, Vector3? at) => shots.Add((name, stand, at));
            Vector3? Pt(Area a, int index, bool? high = null) => a == null ? null : c.Point(a.Cells[Mathf.Clamp(index, 0, a.Cells.Count - 1)], high);
            Vector3? Mid(Area a, bool? high = null) => a == null ? null : c.Point(a.Middle, high);

            var neck0 = L.Find("1,0", "neck");
            Shot("eye-01-entry", c.Start, neck0 != null ? c.Point(neck0.Middle) : (Vector3?)null);
            if (neck0 != null)
            {
                var n = neck0.Cells.OrderBy(p => p.y).First();
                Shot("eye-02-choke-neck", c.Point(new Vector2Int(n.x, n.y - 3)), c.Point(new Vector2Int(n.x, n.y + 4)));
            }
            var flank0 = L.Find("1,0", "flank");
            if (flank0 != null) Shot("eye-03-choke-flank", Pt(flank0, flank0.Cells.Count / 2 - 3), Pt(flank0, flank0.Cells.Count / 2 + 3));
            var perch = L.Find("1,1", "perch");
            Shot("eye-04-sniper-perch", Mid(perch, true), Mid(L.Find("1,1", "watched-ground"), false));
            var flank1 = L.Find("0,1", "flank");
            if (flank1 != null) Shot("eye-05-flanking-route", Pt(flank1, flank1.Cells.Count - 4), Pt(flank1, flank1.Cells.Count - 14));
            var door = L.Find("0,2", "door");
            var pocket = L.Find("0,2", "pocket");
            if (door != null && pocket != null)
            {
                // stand in the hidden door where it turns into the pocket, looking down into the cache
                var pm = pocket.Middle;
                int x0 = pocket.Cells.Min(p => p.x), x1 = pocket.Cells.Max(p => p.x);
                var inPocketColumns = door.Cells.Where(p => p.x >= x0 && p.x <= x1).ToList();
                var stand = inPocketColumns.Count > 0 ? inPocketColumns.OrderByDescending(p => p.y).First() : door.Middle;
                Shot("eye-06-hidden-cache", c.Point(stand), c.Point(pm));
            }
            Shot("eye-07-gallery-from-walkway", Mid(L.Find("1,2", "walkway"), true), Mid(L.Find("1,2", "floor"), false));
            Shot("eye-08-gallery-floor", Mid(L.Find("1,2", "floor"), false), Mid(L.Find("1,2", "walkway"), true));
            var keepDoor = L.Find("1,3", "door-1");
            Shot("eye-09-stronghold", Mid(keepDoor), goal ?? Mid(L.Find("1,3", "keep")));
            int exitX = Enumerable.Range(0, L.W).FirstOrDefault(x => L.Stand(x, L.H - 1));
            Shot("eye-10-exit", c.Point(new Vector2Int(exitX, L.H - 4)), c.Point(new Vector2Int(exitX, L.H - 1)));

            foreach (var s in shots)
            {
                if (s.eye == null || s.at == null) { log.AppendLine($"capture {s.name}: skipped (no NavMesh at its stand or look point)"); continue; }
                var eye = s.eye.Value + Vector3.up * EyeHeight;
                var to = s.at.Value + Vector3.up * 1.2f - eye;
                float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -35f, 35f);
                string path = CapturesDir + "/" + s.name + ".png";
                IronCitadelCapture.Perspective(path, eye, yaw, pitch, 70f, 1280, 720, false);
                log.AppendLine($"capture {s.name}: eye {V(eye)} yaw {yaw:0} pitch {pitch:0} in {c.Where(eye)}");
            }
        }

        static void OverviewPlayCapture(GameObject play, StringBuilder log)
        {
            var cam = play.GetComponentsInChildren<Camera>(true).FirstOrDefault(k => k.name == "OverviewCamera");
            var light = play.GetComponentsInChildren<Light>(true).FirstOrDefault(k => k.name == "OverviewLight");
            var ov = play.GetComponentInChildren<IronCitadelOverview>(true);
            if (cam == null || ov == null) { log.AppendLine("capture overview-play: no OverviewCamera"); return; }
            const int w = 1600, h = 900;
            // what IronCitadelOverview.Fit does at a 16:9 window
            float aspect = (float)w / h;
            var prevPos = cam.transform.position; var prevRot = cam.transform.rotation; float prevSize = cam.orthographicSize;
            bool camWas = cam.enabled, lightWas = light != null && light.enabled;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            var prevActive = RenderTexture.active;
            try
            {
                cam.orthographicSize = Mathf.Max(ov.sizeXZ.y / 2f, ov.sizeXZ.x / 2f / aspect) + ov.margin;
                cam.transform.SetPositionAndRotation(new Vector3(ov.centreXZ.x, prevPos.y, ov.centreXZ.y), Quaternion.Euler(90f, 0f, 0f));
                cam.aspect = aspect;
                cam.enabled = true;
                if (light != null) light.enabled = true;
                bool prevAsync = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
                try
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                        if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                        else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
                    }
                }
                finally { ShaderUtil.allowAsyncCompilation = prevAsync; }
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                string path = CapturesDir + "/overview-play.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                log.AppendLine($"capture: {path} (the scene's OverviewCamera, ortho size {cam.orthographicSize:0}, as M shows it at 16:9)");
            }
            finally
            {
                RenderTexture.active = prevActive;
                cam.transform.SetPositionAndRotation(prevPos, prevRot);
                cam.orthographicSize = prevSize;
                cam.ResetAspect();
                cam.enabled = camWas;
                if (light != null) light.enabled = lightWas;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        static void Put(Texture2D t, int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c);
        }

        static void Line(Texture2D t, Vector2 a, Vector2 b, Color32 c, int thick)
        {
            int n = Mathf.CeilToInt((b - a).magnitude) + 1;
            for (int i = 0; i <= n; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)n);
                for (int dx = -thick / 2; dx <= thick / 2; dx++)
                for (int dy = -thick / 2; dy <= thick / 2; dy++)
                    Put(t, Mathf.RoundToInt(p.x) + dx, Mathf.RoundToInt(p.y) + dy, c);
            }
        }

        static void Box(Texture2D t, Vector2 p, float r, Color32 c, int thick)
        {
            var a = p + new Vector2(-r, -r); var b = p + new Vector2(r, -r); var cc = p + new Vector2(r, r); var d = p + new Vector2(-r, r);
            Line(t, a, b, c, thick); Line(t, b, cc, c, thick); Line(t, cc, d, c, thick); Line(t, d, a, c, thick);
        }

        static void Cross(Texture2D t, Vector2 p, float r, Color32 c)
        {
            Line(t, p + new Vector2(-r, -r), p + new Vector2(r, r), c, 3);
            Line(t, p + new Vector2(-r, r), p + new Vector2(r, -r), c, 3);
        }

        // ------------------------------------------------------------------ report

        static void WriteReport(Ctx c, List<(RouteDef def, WalkResult res)> walks, List<RegionRow> regions, List<GateTest> gates, List<Vector2Int> unreachable,
            List<Vector2Int> noMesh, int standCells, int standJoined, int rockJoined, int rockMesh, float meshArea, float joinedArea, StringBuilder log)
        {
            int pass = walks.Count(w => w.res.Pass);
            var md = new StringBuilder();
            md.AppendLine("# Iron Citadel, pattern-tile level: walk report");
            md.AppendLine();
            md.AppendLine($"Scene `{IronCitadelPatternsScene.ScenePath}`, level `{IronCitadelPatternsScene.LevelFolder}/{IronCitadelPatternsScene.LevelName}.prefab`, {DateTime.Now:yyyy-MM-dd HH:mm}.");
            md.AppendLine("Written by `IronCitadel.Patterns.Editor.IronCitadelPatternsPlay.WalkAndCapture`. The solver's own promised-against-measured report is `solve-report.md` beside this file.");
            md.AppendLine();
            md.AppendLine("## What ran");
            md.AppendLine();
            md.AppendLine("- **Not the validator's nine checks.** Every `must_be_true` line in level.json names its rooms on 45 m cells (rooms on cells, doors, the armory gate, the wings, the vault, the throne room's door, the gallery, the stage, the heights). This level is 3 x 4 pattern slots of 100 m, so none of them has anything to measure; the conformance CLI would fail its check 1 on the grid alone.");
            md.AppendLine($"- **Check 10's method, with the kit's own code.** The NavMesh is baked with the kit's `NavBake.Settings()` (bake r {Tune.BakeRadius}, h {Tune.BakeHeight}, step {Tune.AgentClimb}, slope {Tune.AgentSlope}, voxel {Tune.VoxelSize}) from physics colliders; each route is planned on it and then walked by the kit's `Walker.Walk`: a CharacterController r {Tune.AgentRadius}, h {Tune.AgentHeight}, step {Tune.AgentClimb}, skin {Tune.WalkSkin}, the Starter Assets PlayerCapsule, at {Tune.WalkSpeed} m/s. A route passes when the capsule reaches its end without sticking {Tune.WalkStuckSeconds} s or falling.");
            md.AppendLine("- **The layout against the build.** Every stand cell of the solver's stitched layout (60 x 80 cells of 5 m) is looked up on the NavMesh and checked to be joined to the entry.");
            md.AppendLine();
            md.AppendLine("## Walks");
            md.AppendLine();
            md.AppendLine($"**{pass} of {walks.Count} routes walked.**");
            md.AppendLine();
            md.AppendLine("| route | what it tests | result | planned m | walked m | note |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var (def, res) in walks)
                md.AppendLine($"| {def.Name} | {def.Why} | {(res.Pass ? "PASS" : "FAIL")} | {F(res.Planned)} | {F(res.Walked)} | {res.Note} |");
            md.AppendLine();
            md.AppendLine("Stops, in order:");
            md.AppendLine();
            foreach (var (def, _) in walks)
                md.AppendLine($"- {def.Name}: {string.Join(" -> ", def.Stops.Select(s => s.label + (s.p.HasValue ? " " + V(s.p.Value) : " (no NavMesh)")))}");
            md.AppendLine();
            md.AppendLine("## The layout against the build");
            md.AppendLine();
            md.AppendLine($"- Stand cells in the stitched layout: {standCells}. Joined to the entry on the NavMesh: **{standJoined}**. With NavMesh that is not joined: {unreachable.Count}. With no NavMesh: {noMesh.Count}.");
            md.AppendLine($"- Rock (`#`) cells with NavMesh joined to the entry: {rockJoined} ({rockMesh} with any NavMesh). These are floor the realized tiles carry beyond the layout's stand cells, such as side rooms and wide passages.");
            md.AppendLine($"- NavMesh: {meshArea:0} m2 in all, {joinedArea:0} m2 of it in the piece the entry stands on ({(meshArea > 0 ? joinedArea / meshArea : 0):P0}; the rest is wall tops, prop tops and closed-off pockets).");
            if (unreachable.Count + noMesh.Count > 0)
            {
                md.AppendLine();
                md.AppendLine("Stand cells not joined to the entry, by region:");
                md.AppendLine();
                foreach (var g in unreachable.Select(u => (u, why: "NavMesh not joined")).Concat(noMesh.Select(u => (u, why: "no NavMesh")))
                             .GroupBy(t => c.Where(CellCentre(t.u)) + " (" + t.why + ")"))
                    md.AppendLine($"- {g.Key}: {g.Count()} cell(s): {string.Join(" ", g.Take(24).Select(t => Cell(t.u)))}{(g.Count() > 24 ? " ..." : "")}");
            }
            md.AppendLine();
            md.AppendLine("## Do the measured patterns hold in the build?");
            md.AppendLine();
            md.AppendLine("The solver measured its patterns on the stitched layout (stand cells only), not on the built tiles, and the built tiles carry floor the layout calls rock. So each claim that depends on the ways through is tested again on the build: tall boxes close the named cells, the NavMesh is re-baked with the same settings, and a path is looked for between the two sides.");
            md.AppendLine();
            int gPass = gates.Count(g => g.Pass), gRan = gates.Count(g => g.Ran);
            md.AppendLine($"**{gPass} of {gates.Count} hold** ({gRan} ran).");
            md.AppendLine();
            md.AppendLine("| pattern, between | closed | expected | found | result | detail |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var g in gates)
                md.AppendLine($"| {g.Pattern} | {g.Closed} | {g.Expect} | {(g.Ran ? (g.Through ? "a way through" : "no way through") : "-")} | {(g.Pass ? "HOLDS" : g.Ran ? "BROKEN" : "n/a")} | {g.Detail} |");
            md.AppendLine();
            md.AppendLine("`gates-map.png`: the closed cells in magenta; a path found where none was expected in thick red, a path found where one was expected in thin green.");
            md.AppendLine();
            md.AppendLine("## Every pattern region from the entry");
            md.AppendLine();
            md.AppendLine("A NavMesh path from PlayerStart to the middle cell of each area the stitched layout names.");
            md.AppendLine();
            md.AppendLine("| region | at | NavMesh path | path m |");
            md.AppendLine("|---|---|---|---|");
            foreach (var r in regions)
                md.AppendLine($"| {r.Region} | {(r.At.HasValue ? V(r.At.Value) : "-")} | {r.Status} | {(r.Status == "PathComplete" ? F(r.PathM) : "-")} |");
            md.AppendLine();
            md.AppendLine("## Files");
            md.AppendLine();
            md.AppendLine("- `walk-map.png`: top view at 4 px/m, north up, the 100 m slots in yellow. Each walk's trace is a coloured line in the order of the table (white, cyan, orange, violet, green, yellow, pink, light blue); a red X is a snag; the green box is the entry, the gold box the goal; red boxes are stand cells not joined to the entry.");
            md.AppendLine("- `walk.json`: the same, as data.");
            md.AppendLine($"- Eye captures: `{CapturesDir}/`.");
            File.WriteAllText(ScoresDir + "/walk.md", md.ToString());

            var json = new JObj()
                .Add("scene", IronCitadelPatternsScene.ScenePath)
                .Add("written", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                .Add("agent", new JObj().Add("radius", Tune.AgentRadius).Add("height", Tune.AgentHeight).Add("step", Tune.AgentClimb).Add("skin", Tune.WalkSkin)
                    .Add("bake_radius", Tune.BakeRadius).Add("bake_height", Tune.BakeHeight))
                .Add("walks_passed", pass).Add("walks", walks.Count)
                .Add("routes", walks.Select(w => (object)w.res.ToJson().Add("why", w.def.Why)
                    .Add("stops", w.def.Stops.Select(s => (object)new JObj().Add("label", s.label).Add("at", s.p.HasValue ? (object)s.p.Value : null)).ToList())).ToList())
                .Add("layout", new JObj().Add("stand_cells", standCells).Add("stand_joined", standJoined).Add("stand_not_joined", unreachable.Count)
                    .Add("stand_no_navmesh", noMesh.Count).Add("rock_joined", rockJoined).Add("rock_any_navmesh", rockMesh)
                    .Add("navmesh_m2", meshArea).Add("joined_m2", joinedArea)
                    .Add("not_joined", unreachable.Concat(noMesh).Select(u => (object)new List<object> { u.x, u.y }).ToList()))
                .Add("gates", gates.Select(g => (object)new JObj().Add("pattern", g.Pattern).Add("closed", g.Closed).Add("expect_through", g.ExpectThrough)
                    .Add("ran", g.Ran).Add("through", g.Through).Add("holds", g.Pass).Add("detail", g.Detail)
                    .Add("blocked", g.Blocked.Select(u => (object)new List<object> { u.x, u.y }).ToList())).ToList())
                .Add("regions", regions.Select(r => (object)new JObj().Add("region", r.Region).Add("slot", r.Slot).Add("at", r.At.HasValue ? (object)r.At.Value : null)
                    .Add("path", r.Status).Add("path_m", r.PathM)).ToList());
            File.WriteAllText(ScoresDir + "/walk.json", MiniJson.Write(json));
            log.AppendLine($"report: {ScoresDir}/walk.md ({pass}/{walks.Count} walks), walk.json");
        }

        static void CopySolverReport(StringBuilder log)
        {
            string src = SolveDir + "/solve-report.md", dst = ScoresDir + "/solve-report.md";
            if (!File.Exists(src)) { log.AppendLine("no solver report at " + src); return; }
            File.Copy(src, dst, true);
            log.AppendLine("copied the solver's report to " + dst);
        }
    }
}
