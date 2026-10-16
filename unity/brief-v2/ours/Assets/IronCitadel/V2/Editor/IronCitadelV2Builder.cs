using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using IronCitadel.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace IronCitadelV2.Editor
{
    /// <summary>Stage 3 of Iron Citadel v2 (C:/Repos/IronCitadel/v2/CONTRACT.md): turns a run folder's level.json,
    /// picks.json and (when present) placements.json into a playable scene, and writes floor.json (the walkable floor,
    /// for the tactics program) and built.json (the room graph as built, checked against the design).
    ///
    /// <para>Re-runnable: every build starts from a new empty scene and saves it over
    /// <see cref="ScenePath"/>, so the same inputs give the same scene. The steps, in order: rooms (one prefab per
    /// pick, on its cell, turned by its rotation; a wall or doorway two rooms both stand on their shared edge drawn once;
    /// missing floor and wall colliders patched as v1 did), doors (the leaves a room stands in a doorway lifted out
    /// before it is measured; open: the doorway as built, the rooms' leaves standing open; shut: the rooms' own leaves,
    /// else a Synty leaf sized to the measured opening, swinging open with E; hidden: v1's bookcase on room a's side),
    /// the NavMesh (baked with every door open, saved beside the scene and loaded by
    /// <see cref="IronCitadelV2NavMesh"/>), floor.json (written at once: the tactics program waits for it), the room
    /// graph, then enemies, pickups, the player, the HUD and the M map, the lights and the grade, built.json, the
    /// saved scene and the captures. Everything it does is logged to &lt;run&gt;/build-log.txt.</para>
    ///
    /// <para>Run it: <c>IronCitadelV2.Editor.IronCitadelV2Builder.Build("C:/Repos/IronCitadel/v2")</c>, or the menu
    /// Iron Citadel/V2.</para></summary>
    public static partial class IronCitadelV2Builder
    {
        public const string LevelsDir = "Assets/CrawfisSoftware/AllSyntyDungeons/Levels";
        public const string SceneName = "IronCitadelV2";
        /// <summary>The scene a build saves: IronCitadelV2.unity, or IronCitadelV2_&lt;suffix&gt;.unity under <see cref="BuildAs"/>.</summary>
        public static string ScenePath => LevelsDir + "/" + s_scene + ".unity";
        public const string DataDir = LevelsDir + "/IronCitadelV2";
        /// <summary>That scene's NavMesh; every build's lies in <see cref="DataDir"/>.</summary>
        public static string NavMeshPath => DataDir + "/" + s_scene + ".NavMesh.asset";
        public const string AmbienceDir = DataDir + "/Ambience";
        static string s_scene = SceneName;
        public const string RootName = "IronCitadel";
        public const string PlayerPrefabPath = "Assets/IronCitadel/Walker/IronCitadelPlayer.prefab";
        public const string SampleRun = "C:/Repos/IronCitadel/v2/sample";
        public const string RealRun = "C:/Repos/IronCitadel/v2";
        const string Tag = "[IronCitadelV2]";
        const int AllLayers = ~0;

        const string PD = "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/";
        const string PDM = "Assets/Synty/PolygonDungeonMap/Prefabs/";
        const string HM = "Assets/_THIRD_PARTY/Synty/PolygonHorrorMansion/Prefabs/";
        const string HMC = HM + "Characters/Characters/";
        const string HMK = "Assets/CrawfisSoftware/PolygonHorrorMansion/Kit/";

        /// <summary>The Horror Mansion profile (Hollowmere Manor, 2026-10-14): the same level built from the mansion's
        /// rooms and art -- the Count for the warlord, the mansion's monsters for the goblins and the dead, a safe for the
        /// hoard, the kit's bookcase for the secret door, a panelled leaf for a shut door, the master's armchair for the
        /// throne. Set by <see cref="BuildMansion"/> only.</summary>
        static bool s_mansion;
        static string BookcasePrefab => s_mansion ? HMK + "HK_Bookcase_A.prefab" : PDM + "SM_Prop_Bookcase_Small_01.prefab";
        static float BookcaseW => s_mansion ? 2.5f : 2.13f;
        static float BookcaseH => s_mansion ? 3.01f : 3.351f;
        static string ChestHoard => s_mansion ? HM + "Props/SM_Prop_Safe_01.prefab" : PD + "Props/SM_Prop_Chest_01.prefab";
        static string ChestGear => s_mansion ? HM + "Props/SM_Prop_Lockbox_02.prefab" : PD + "Props/SM_Prop_Chest_03.prefab";
        static string Coins => s_mansion ? HM + "Props/SM_Prop_Goblet_01.prefab" : PD + "Items/SM_Item_Coins_02.prefab";
        static string Shaman => s_mansion ? HMC + "SM_Chr_Witch_01.prefab" : PD + "Characters/SM_Chr_Goblin_Shaman_01.prefab";
        static string WarChief => s_mansion ? HMC + "SM_Chr_Count_01.prefab" : PD + "Characters/SM_Chr_Goblin_WarChief_01.prefab";
        static string[] Warriors => s_mansion ? new[] { HMC + "SM_Chr_Butcher_01.prefab", HMC + "SM_Chr_Werewolf_01.prefab" }
            : new[] { PD + "Characters/SM_Chr_Goblin_Warrior_Male_01.prefab", PD + "Characters/SM_Chr_Goblin_Warrior_Female_01.prefab" };
        static string[] Skeletons => s_mansion ? new[] { HMC + "SM_Chr_Ghoul_01.prefab", HMC + "SM_Chr_Yokai_01.prefab" }
            : new[] { PD + "Characters/SM_Chr_Skeleton_Soldier_01.prefab", PD + "Characters/SM_Chr_Skeleton_Soldier_02.prefab" };
        static string[] Guards => s_mansion ? new[] { HMC + "SM_Chr_Gimp_01.prefab", HMC + "SM_Chr_Demon_01.prefab" } : Warriors;
        static readonly string[] Staffs = { PD + "Weapons/SM_Wep_Goblin_Staff_01.prefab", PD + "Weapons/SM_Wep_Staff_Gem_01.prefab" };
        /// <summary>The goal's piece: the throne, or in the mansion the master's armchair.</summary>
        static string ThroneWord => s_mansion ? "ArmChair_05" : "Throne";
        /// <summary>floor.json's largest step between linked samples in neighbouring columns. The mansion's stairs climb
        /// 0.6 m a metre (the grand flight 3 m over 5 m, the stage steps 1.5 m over 2.5 m), so at 0.6 m the samples'
        /// noise cut the gallery deck and the throne stage off as link pieces of their own (2026-10-14).</summary>
        static float LinkStep => s_mansion ? 0.75f : 0.6f;
        /// <summary>The kinds whose melee enemies are the dead, not goblins: prison, crypt, torture chamber.</summary>
        static readonly HashSet<string> UndeadKinds = new HashSet<string> { "P", "C", "X" };
        // v1's warlord stands twice a goblin's size (Roger, 2026-10-07); v2 keeps it
        const float BossScale = 2f;

        /// <summary>One build's state.</summary>
        sealed class Ctx
        {
            public string runDir;
            public V2Level level;
            public Dictionary<string, V2Pick> picks;
            public V2Placements placements;
            public Scene scene;
            public GameObject root;
            public Transform roomsT, featT, markT, lightT, playT;
            public readonly Dictionary<string, GameObject> rooms = new Dictionary<string, GameObject>();
            public readonly Dictionary<string, Opening> openings = new Dictionary<string, Opening>();
            /// <summary>Per design door, the leaves its rooms stood in its doorway (<see cref="LiftRoomLeaves"/>).</summary>
            public readonly Dictionary<string, List<RoomLeaf>> leaves = new Dictionary<string, List<RoomLeaf>>();
            public readonly List<BuiltDoor> doors = new List<BuiltDoor>();
            public NavMeshQueryFilter filter;
            public NavMeshBuildSettings settings;
            public NavMeshData navData;
            public Vector3 start;
            public float startYaw;
            public string startHow;
            public FloorData floor;
            public Newtonsoft.Json.Linq.JObject built;
            public Transform goal;
            public readonly StringBuilder log = new StringBuilder();
            public readonly List<string> problems = new List<string>();
            public readonly Stopwatch clock = Stopwatch.StartNew();
            public void Log(string s) => log.AppendLine($"[{clock.Elapsed.TotalSeconds,6:0.0}s] {s}");
            public void Problem(string s) { problems.Add(s); Log("PROBLEM " + s); }
            public float Lid(string roomId) => picks.TryGetValue(roomId, out var p) && p.lid > 0f ? p.lid : 10f;
            public V2Room RoomAt(Vector3 p)
            {
                int c = Mathf.FloorToInt(p.x / level.tile), r = Mathf.FloorToInt(-p.z / level.tile);
                return level.At(r, c);
            }
        }

        /// <summary>The clear doorway measured across a design door's edge, before any leaf is hung in it.</summary>
        sealed class Opening
        {
            public V2Edge e;
            public bool found;
            public float tc, width, height, floorY;
            public Vector3 Centre => e.centre + e.along * tc + Vector3.up * floorY;
        }

        /// <summary>A shut or hidden door as built: its component and each hinge's closed pose.</summary>
        sealed class BuiltDoor
        {
            public V2Door design;
            public IronCitadelV2Door door;
            public Quaternion[] closed;
        }

        // ------------------------------------------------------------------ entry points

        [MenuItem("Iron Citadel/V2/Build sample run (v2/sample)")]
        public static void BuildSampleMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(SampleRun);
        }

        [MenuItem("Iron Citadel/V2/Build real run (v2)")]
        public static void BuildRealMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(RealRun);
        }

        [MenuItem("Iron Citadel/V2/Build from a run folder...")]
        public static void BuildPickMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var dir = EditorUtility.OpenFolderPanel("Iron Citadel v2 run folder (level.json, picks.json)", RealRun, "");
            if (!string.IsNullOrEmpty(dir)) Build(dir);
        }

        /// <summary>Build the scene for the run folder <paramref name="runDir"/>, write its floor.json and built.json,
        /// save the scene and take the captures (&lt;run&gt;/captures). Throws, after logging why, when level.json or
        /// picks.json is missing or does not fit.</summary>
        public static void Build(string runDir)
        {
            BuildCore(runDir, true);
        }

        /// <summary><see cref="Build"/> without the captures.</summary>
        public static void BuildNoCaptures(string runDir)
        {
            BuildCore(runDir, false);
        }

        /// <summary><see cref="Build"/> into a scene of its own, IronCitadelV2_<paramref name="suffix"/>.unity, with its
        /// NavMesh beside the main scene's, so another seed's level leaves IronCitadelV2.unity alone. The room looks
        /// under <see cref="AmbienceDir"/> are shared: a look is rebuilt only when its values change.</summary>
        public static void BuildAs(string runDir, string suffix, bool captures = true)
        {
            if (string.IsNullOrWhiteSpace(suffix)) throw new ArgumentException("suffix is empty");
            s_scene = SceneName + "_" + suffix;
            try { BuildCore(runDir, captures); }
            finally { s_scene = SceneName; }
        }

        /// <summary><see cref="BuildAs"/> with the Horror Mansion profile: a run whose picks are the mansion's rooms
        /// (Assets/CrawfisSoftware/PolygonHorrorMansion/Rooms), into IronCitadelV2_<paramref name="suffix"/>.unity.</summary>
        public static void BuildMansion(string runDir, string suffix = "HM", bool captures = true)
        {
            s_mansion = true;
            try { BuildAs(runDir, suffix, captures); }
            finally { s_mansion = false; }
        }

        static void BuildCore(string runDir, bool captures)
        {
            if (string.IsNullOrWhiteSpace(runDir)) throw new ArgumentException("runDir is empty");
            var c = new Ctx { runDir = Path.GetFullPath(runDir).Replace('\\', '/').TrimEnd('/') };
            c.Log($"build {c.runDir} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            try
            {
                try
                {
                    c.level = V2Model.LoadLevel(c.runDir + "/level.json");
                    c.picks = V2Model.LoadPicks(c.runDir + "/picks.json", c.level);
                    c.placements = V2Model.LoadPlacements(c.runDir + "/placements.json");
                }
                catch (Exception e)
                {
                    c.Log("STOPPED: " + e.Message);
                    Debug.LogError($"{Tag} cannot build {c.runDir}: {e.Message}");
                    throw;
                }
                c.Log($"level '{c.level.name}': {c.level.rows}x{c.level.cols} cells of {c.level.tile} m, {c.level.rooms.Count} rooms, {c.level.doors.Count} doors " +
                      $"({string.Join(", ", c.level.doors.GroupBy(d => d.kind).Select(g => $"{g.Count()} {g.Key}"))}); placements.json {(c.placements != null ? "present" : "absent")}");

                NewScene(c);
                PlaceRooms(c);
                HideDoubledEdgePieces(c);
                PatchMissingColliders(c);
                LiftRoomLeaves(c);
                MeasureOpenings(c);
                BuildDoors(c);
                Physics.SyncTransforms();

                // NavMesh, floor.json and the room graph, all with every door open
                SetDoorsOpen(c, true);
                try
                {
                    Bake(c);
                    ChooseStart(c);
                    SampleFloor(c);
                    WriteFloor(c);
                    CheckRoomGraph(c);
                }
                finally { SetDoorsOpen(c, false); }

                BuildMarkers(c);
                BuildPlay(c);
                LightRooms(c);
                Ambience(c);
                CheckEnemies(c);
                WriteBuilt(c);
                SaveScene(c);
                if (captures) Captures(c);
                c.Log($"done in {c.clock.Elapsed.TotalSeconds:0.0} s; {c.problems.Count} problem(s)");
                Debug.Log($"{Tag} built {c.runDir} in {c.clock.Elapsed.TotalSeconds:0} s: scene {ScenePath}, floor.json {c.floor?.samples.Count} samples, " +
                          $"{c.problems.Count} problem(s){(c.problems.Count > 0 ? ": " + string.Join("; ", c.problems.Take(5)) : "")}");
            }
            catch (Exception e)
            {
                c.Log("FAILED: " + e);
                throw;
            }
            finally
            {
                try { File.WriteAllText(c.runDir + "/build-log.txt", c.log.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false)); }
                catch (Exception e) { Debug.LogWarning($"{Tag} could not write the build log: {e.Message}"); }
            }
        }

        // ------------------------------------------------------------------ scene and rooms

        static void NewScene(Ctx c)
        {
            EnsureFolder(LevelsDir);
            EnsureFolder(DataDir);
            NavMesh.RemoveAllNavMeshData();
            c.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            IronCitadel.Rooms.Editor.IronCitadelRoomsAssembler.ApplyNightLighting();
            c.root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(c.root, c.scene);
            c.roomsT = Child(c.root.transform, "Rooms");
            c.featT = Child(c.root.transform, "Features");
            c.markT = Child(c.root.transform, "Markers");
            c.lightT = Child(c.root.transform, "Lighting");
            AddNightFill(c.lightT);
            c.Log("new scene; night ambient and fill as v1 (ambient (0.2, 0.19, 0.24), three 0.13 directional fills)");
        }

        static void PlaceRooms(Ctx c)
        {
            foreach (var room in c.level.rooms)
            {
                var pick = c.picks[room.id];
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(pick.prefab);
                if (asset == null) throw new FileNotFoundException($"{room.id}: the picked prefab is not in the project: {pick.prefab}");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, c.scene);
                go.name = room.id;
                go.transform.SetParent(c.roomsT, false);
                go.transform.SetPositionAndRotation(V2Model.CellCenter(c.level.tile, room.r, room.c), Quaternion.Euler(0f, pick.rotation, 0f));
                c.rooms[room.id] = go;
                c.Log($"room {room.id} [{room.r},{room.c}]: {Path.GetFileNameWithoutExtension(pick.prefab)} turned {pick.rotation} ({pick.theme}/{pick.family}, {pick.state} = {pick.stateKind}, lid {pick.lid} m)");
            }
            Physics.SyncTransforms();
        }

        /// <summary>A room stands its outer walls and doorways centred on its tile's edge, so where two rooms meet, both
        /// rooms' pieces stand in the same place, turned half round, and z-fight (Hollowmere's doorways, 2026-10-14). A
        /// piece standing across a tile edge (under 1 m thick across it) whose mesh and bounds, within 1 cm, an earlier
        /// room's piece already has is not drawn; its collider stays, so nothing walked or measured changes. A taller
        /// room's upper walls have no twin and stay.</summary>
        static void HideDoubledEdgePieces(Ctx c)
        {
            float tile = c.level.tile;
            bool Across(float lo, float hi)
            {
                float k = Mathf.Round((lo + hi) / 2f / tile) * tile;
                return hi - lo < 1f && lo < k && k < hi;
            }
            bool Same(Bounds a, Bounds b) => (a.center - b.center).sqrMagnitude < 1e-4f && (a.size - b.size).sqrMagnitude < 1e-4f;

            var drawn = new Dictionary<Mesh, List<(string room, Bounds b)>>();
            var hidden = new SortedDictionary<string, int>();
            foreach (var kv in c.rooms)
                foreach (var mr in kv.Value.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || !mr.enabled) continue;
                    var mesh = mf.sharedMesh;
                    var b = mr.bounds;
                    if (!Across(b.min.x, b.max.x) && !Across(b.min.z, b.max.z)) continue;
                    if (!drawn.TryGetValue(mesh, out var seen)) drawn[mesh] = seen = new List<(string, Bounds)>();
                    if (seen.Any(s => s.room != kv.Key && Same(s.b, b)))
                    {
                        mr.enabled = false;
                        hidden[mesh.name] = hidden.TryGetValue(mesh.name, out var n) ? n + 1 : 1;
                    }
                    else seen.Add((kv.Key, b));
                }
            c.Log(hidden.Count == 0 ? "edge pieces: no room's piece doubles a neighbour's" :
                $"edge pieces drawn once where two rooms meet ({hidden.Values.Sum()} copies hidden, colliders kept): " +
                string.Join(", ", hidden.Select(kv => $"{kv.Key} x{kv.Value}")));
        }

        /// <summary>v1's patch: some room pieces ship without a collider, so a walker would drop through. Every Floor
        /// piece without one gets a box (at least 0.2 m thick), every Walls piece a mesh collider; scene copy only.</summary>
        static void PatchMissingColliders(Ctx c)
        {
            var found = new SortedDictionary<string, int>();
            foreach (var kv in c.rooms)
                foreach (var group in kv.Value.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Floor" || t.name == "Walls"))
                    foreach (var mf in group.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                        var pc = mf.GetComponentInParent<Collider>();
                        if (pc != null && pc.transform != mf.transform) continue;
                        if (group.name == "Floor")
                        {
                            var bc = mf.gameObject.AddComponent<BoxCollider>();
                            if (bc.size.y < 0.1f) { bc.center -= new Vector3(0f, (0.2f - bc.size.y) / 2f, 0f); bc.size = new Vector3(bc.size.x, 0.2f, bc.size.z); }
                        }
                        else mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                        string key = $"{kv.Key}/{group.parent.name}/{group.name}";
                        found[key] = found.TryGetValue(key, out var n) ? n + 1 : 1;
                    }
            Physics.SyncTransforms();
            c.Log(found.Count == 0 ? "colliders: every floor and wall piece has one" :
                $"colliders added where the prefab had none ({found.Values.Sum()}): " + string.Join(", ", found.Select(kv => $"{kv.Key} x{kv.Value}")));
        }

        // ------------------------------------------------------------------ markers: enemies, pickups, start, goal

        static void BuildMarkers(Ctx c)
        {
            var defT = Child(c.markT, "Defenders");
            var pickT = Child(c.markT, "Pickups");

            // the start
            var ps = new GameObject("PlayerStart");
            ps.transform.SetParent(c.markT, false);
            ps.transform.SetPositionAndRotation(c.start, Quaternion.Euler(0f, c.startYaw, 0f));
            TrySetTag(ps, "Respawn");
            var pm = ps.AddComponent<IronCitadelMarker>();
            pm.kind = IronCitadelMarker.Kind.PlayerStart; pm.room = c.level.startRoom; pm.note = c.startHow;

            // the goal: the throne's foot, on its stage
            var goalRoom = c.rooms[c.level.goalRoom];
            var throne = goalRoom.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("SM_", StringComparison.Ordinal) && t.name.IndexOf(ThroneWord, StringComparison.OrdinalIgnoreCase) >= 0 && Drawn(t).Length > 0)
                // the mansion's ballroom has armchairs all over its floor: its throne is the one standing highest (on the stage)
                .OrderByDescending(t => s_mansion ? BoundsOf(t).min.y : 0f)
                .ThenByDescending(t => BoundsOf(t).size.y).ThenBy(t => t.position.x).ThenBy(t => t.position.z)
                .FirstOrDefault();
            Vector3 gp;
            string gwhy;
            if (throne != null) { var tb = BoundsOf(throne); gp = new Vector3(tb.center.x, tb.min.y, tb.center.z); gwhy = "the foot of " + throne.name; }
            else
            {
                var dais = goalRoom.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "dais");
                gp = dais != null ? BoundsOf(dais).center : goalRoom.transform.position;
                gwhy = dais != null ? "the dais (no throne piece found)" : "the room centre (no throne or dais found)";
                c.Problem("goal: no throne piece in " + c.level.goalRoom + "; " + gwhy);
            }
            var goal = new GameObject("Goal");
            goal.transform.SetParent(c.markT, false);
            goal.transform.position = gp;
            TrySetTag(goal, "Finish");
            var gm = goal.AddComponent<IronCitadelMarker>(); gm.kind = IronCitadelMarker.Kind.Goal; gm.room = c.level.goalRoom; gm.note = "the throne: " + gwhy;
            var trig = goal.AddComponent<SphereCollider>(); trig.isTrigger = true; trig.radius = 2f; trig.center = Vector3.up;
            c.goal = goal.transform;
            c.Log($"goal at {V(gp)} ({gwhy})");

            if (c.placements == null)
            {
                c.Log("enemies and pickups: no placements.json in the run folder, so none placed (the tactics program writes it from floor.json)");
                return;
            }

            // enemies
            var perRoomKind = new Dictionary<string, int>();
            foreach (var e in c.placements.enemies)
            {
                var room = c.level.Room(e.room);
                string key = e.room + "/" + e.kind;
                int idx = perRoomKind.TryGetValue(key, out var n) ? n : 0;
                perRoomKind[key] = idx + 1;
                string prefab, what;
                float scale = 1f;
                switch (e.kind)
                {
                    case "ranged": prefab = Shaman; what = s_mansion ? "witch" : "goblin shaman with a staff"; break;
                    case "boss": prefab = WarChief; what = (s_mansion ? "the Count (the master)" : "goblin war chief (the warlord)") + ", x" + BossScale; scale = BossScale; break;
                    case "melee":
                        if (room != null && UndeadKinds.Contains(room.kind)) { prefab = Skeletons[idx % 2]; what = s_mansion ? "ghoul or yokai" : "skeleton soldier"; }
                        else { prefab = Warriors[idx % 2]; what = s_mansion ? "butcher or werewolf" : "goblin warrior"; }
                        break;
                    case "guard": prefab = Guards[idx % 2]; what = s_mansion ? "gimp or demon (the master's guard)" : "goblin warrior (the warlord's guard)"; break;
                    default: prefab = Warriors[idx % 2]; what = "goblin warrior (unknown kind '" + e.kind + "')"; c.Problem($"enemy {e.id}: unknown kind '{e.kind}', placed as a goblin warrior"); break;
                }
                var m = new GameObject(string.IsNullOrEmpty(e.id) ? $"enemy {e.room} {e.kind} {idx + 1}" : e.id);
                m.transform.SetParent(defT, false);
                m.transform.SetPositionAndRotation(e.pos, Quaternion.Euler(0f, e.yaw, 0f));
                var mk = m.AddComponent<IronCitadelMarker>();
                mk.kind = IronCitadelMarker.Kind.Defender; mk.room = e.room;
                mk.note = $"{e.kind}, post {e.post}, watches {e.watches}: {what}";
                var cap = m.AddComponent<CapsuleCollider>();
                cap.radius = 0.35f * scale; cap.height = 1.8f * scale; cap.center = new Vector3(0f, 0.9f * scale, 0f);
                var ch = Spawn(prefab, m.transform);
                ch.transform.localPosition = Vector3.zero;
                ch.transform.localRotation = Quaternion.identity;
                ch.transform.localScale = Vector3.one * scale;
                PoseArmsDown(ch);
                if (e.kind == "ranged" && !s_mansion) GiveStaff(c, ch, Staffs[idx % 2], e.pos.y);
            }
            c.Log($"enemies: {c.placements.enemies.Count} placed from placements.json: " +
                  string.Join(", ", c.placements.enemies.GroupBy(x => x.room).Select(g => $"{g.Key} {string.Join("+", g.GroupBy(x => x.kind).Select(k => $"{k.Count()} {k.Key}"))}")));

            // pickups (v1's chests)
            foreach (var p in c.placements.pickups)
            {
                bool hoard = p.id == "hoard" || (c.level.Room(p.room)?.kind == "V");
                var m = new GameObject(string.IsNullOrEmpty(p.id) ? "pickup " + p.room : p.id);
                m.transform.SetParent(pickT, false);
                var rc = c.rooms.TryGetValue(p.room ?? "", out var rgo) ? rgo.transform.position : p.pos;
                var toCentre = V2Model.Flat(rc - p.pos);
                m.transform.SetPositionAndRotation(p.pos, Quaternion.Euler(0f, toCentre.sqrMagnitude > 0.01f ? V2Model.Yaw(toCentre) : 0f, 0f));
                var mk = m.AddComponent<IronCitadelMarker>();
                mk.kind = IronCitadelMarker.Kind.Pickup; mk.room = p.room; mk.note = p.id;
                var chest = Spawn(hoard ? ChestHoard : ChestGear, m.transform, "Chest");
                chest.transform.localPosition = Vector3.zero; chest.transform.localRotation = Quaternion.identity;
                if (hoard)
                    for (int k = 0; k < 3; k++)
                    {
                        var coin = Spawn(Coins, m.transform, "Coins");
                        coin.transform.localPosition = new Vector3(-0.6f + 0.6f * k, 0f, 0.65f);
                        coin.transform.localRotation = Quaternion.Euler(0f, 37f * k, 0f);
                    }
                var lg = new GameObject("PickupGlow");
                lg.transform.SetParent(m.transform, false);
                lg.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.45f); l.intensity = 1.5f; l.range = 3.5f; l.shadows = LightShadows.None;
                c.Log($"pickup {p.id} in {p.room} at {V(p.pos)} ({(hoard ? (s_mansion ? "the safe and goblets" : "hoard chest and coins") : (s_mansion ? "lockbox" : "gear chest"))})");
            }
            Physics.SyncTransforms();
        }

        /// <summary>A staff held upright in the right hand: its foot just over the floor, its shaft through the hand's
        /// x, z, parented to Hand_R so it stays with the pose. Its colliders go (the marker's capsule is the body).</summary>
        static void GiveStaff(Ctx c, GameObject ch, string staffPath, float floorY)
        {
            var hand = ch.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Hand_R");
            if (hand == null) { c.Problem($"{ch.transform.parent.name}: no Hand_R bone; staff not given"); return; }
            var staff = Spawn(staffPath, ch.transform, "Staff");
            foreach (var col in staff.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            staff.transform.rotation = ch.transform.rotation;
            staff.transform.localScale = Vector3.one;
            var b = RendererBounds(staff);
            var hp = hand.position;
            staff.transform.position += new Vector3(hp.x - b.center.x, floorY + 0.02f - b.min.y, hp.z - b.center.z);
            staff.transform.SetParent(hand, true);
        }

        // ------------------------------------------------------------------ play: walker, HUD, map

        static void BuildPlay(Ctx c)
        {
            c.playT = Child(c.root.transform, "Play");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null) throw new FileNotFoundException("the walker prefab is missing: " + PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, c.scene);
            player.name = "IronCitadelPlayer";
            player.transform.SetParent(c.playT, true);
            var capsule = player.GetComponentsInChildren<Transform>(true).First(t => t.name == "PlayerCapsule");
            float dyaw = Mathf.DeltaAngle(capsule.eulerAngles.y, c.startYaw);
            player.transform.RotateAround(capsule.position, Vector3.up, dyaw);
            player.transform.position += c.start + Vector3.up * 0.05f - capsule.position;
            var camRoot = capsule.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PlayerCameraRoot");
            var mainCam = player.GetComponentsInChildren<Camera>(true).FirstOrDefault(cm => cm.CompareTag("MainCamera"));
            if (mainCam != null && camRoot != null) mainCam.transform.SetPositionAndRotation(camRoot.position, camRoot.rotation);
            if (mainCam != null)
            {
                // the room grade and moods are post-processing: the player's camera must render it
                var acd = mainCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (acd != null && !acd.renderPostProcessing) { acd.renderPostProcessing = true; c.Log("player camera: post-processing switched on (the grade and the room moods need it)"); }
            }
            c.Log($"walker at {V(capsule.position)} yaw {capsule.eulerAngles.y:0} ({c.startHow})");

            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(c.playT, false);
            var hud = hudGo.AddComponent<IronCitadelV2Hud>();
            hud.tile = c.level.tile; hud.rows = c.level.rows; hud.cols = c.level.cols;
            hud.rooms = c.level.rooms.Select(r => new IronCitadelV2Hud.RoomCell { id = r.id, name = r.name, row = r.r, col = r.c }).ToArray();
            hud.player = capsule;
            hud.goal = c.goal;

            float W = c.level.tile * c.level.cols, D = c.level.tile * c.level.rows;
            var camGo = new GameObject("OverviewCamera");
            camGo.transform.SetParent(c.playT, false);
            camGo.transform.SetPositionAndRotation(new Vector3(W / 2f, 400f, -D / 2f), Quaternion.Euler(90f, 0f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = D / 2f + 4f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            cam.enabled = false;

            var lightGo = new GameObject("OverviewLight");
            lightGo.transform.SetParent(c.playT, false);
            lightGo.transform.rotation = Quaternion.Euler(65f, 30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var ov = hudGo.AddComponent<IronCitadel.Play.IronCitadelOverview>();
            ov.overviewCamera = cam;
            ov.overviewLight = light;
            ov.levelRoot = c.root.transform;
            ov.player = capsule;
            ov.centreXZ = new Vector2(W / 2f, -D / 2f);
            ov.sizeXZ = new Vector2(W, D);
            c.Log("play: walker, HUD (v2 fork: room, time, E prompt, throne) and v1's M overview (ceilings hidden) under IronCitadel/Play");
        }

        // ------------------------------------------------------------------ save

        static void SaveScene(Ctx c)
        {
            EditorSceneManager.MarkSceneDirty(c.scene);
            if (!EditorSceneManager.SaveScene(c.scene, ScenePath)) throw new IOException("could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            c.Log("saved " + ScenePath);
        }

        // ------------------------------------------------------------------ helpers

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>v1's night fill: three dim, cool directional lights without shadows, 120 degrees apart.</summary>
        static void AddNightFill(Transform parent)
        {
            for (int k = 0; k < 3; k++)
            {
                var go = new GameObject($"Night fill {k + 1}");
                go.transform.SetParent(parent, false);
                go.transform.rotation = Quaternion.Euler(40f, 30f + 120f * k, 0f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = IronCitadel.Rooms.Editor.IronCitadelRoomsAssembler.NightFillColor;
                l.intensity = IronCitadel.Rooms.Editor.IronCitadelRoomsAssembler.NightFillIntensity;
                l.shadows = LightShadows.None;
            }
        }

        static GameObject Spawn(string path, Transform parent, string name = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new FileNotFoundException("missing prefab " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            if (name != null) go.name = name;
            return go;
        }

        static Renderer[] Drawn(Transform t) => t.GetComponentsInChildren<Renderer>(false)
            .Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();

        static Bounds BoundsOf(Transform t)
        {
            var rs = Drawn(t);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Bounds RendererBounds(GameObject go) => BoundsOf(go.transform);

        /// <summary>The box of <paramref name="go"/>'s meshes in <paramref name="frame"/>'s space.</summary>
        static Bounds LocalBounds(GameObject go, Transform frame)
        {
            Bounds b = default; bool first = true;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var lp = frame.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
                }
            }
            return b;
        }

        /// <summary>v1's pose: the Synty characters ship in a T-pose with no idle clip; drop the arms to the sides.</summary>
        static void PoseArmsDown(GameObject ch)
        {
            foreach (var an in ch.GetComponentsInChildren<Animator>(true)) an.enabled = false;
            var bones = ch.GetComponentsInChildren<Transform>(true);
            foreach (var side in new[] { "L", "R" })
            {
                var sh = bones.FirstOrDefault(b => b.name == "Shoulder_" + side);
                var el = bones.FirstOrDefault(b => b.name == "Elbow_" + side);
                if (sh == null || el == null) continue;
                var cur = el.position - sh.position;
                var outward = V2Model.Flat(cur).normalized;
                var target = (Vector3.down * Mathf.Cos(12f * Mathf.Deg2Rad) + outward * Mathf.Sin(12f * Mathf.Deg2Rad)).normalized;
                sh.rotation = Quaternion.FromToRotation(cur.normalized, target) * sh.rotation;
            }
        }

        static void TrySetTag(GameObject go, string tag)
        {
            try { go.tag = tag; } catch { }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        static bool IsCeiling(Transform t)
        {
            for (var x = t; x != null; x = x.parent)
                if (IronCitadel.Rooms.Editor.IronCitadelCapture.IsCeiling(x)) return true;
            return false;
        }

        static string PathOf(Transform t)
        {
            var s = t.name;
            for (var x = t.parent; x != null && x.parent != null; x = x.parent) s = x.name + "/" + s;
            return s;
        }

        static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
        static string F(float f, string fmt = "0.##") => f.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    }
}
