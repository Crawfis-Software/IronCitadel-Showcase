using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>Builds the Iron Citadel "rooms" level: one themed K9 room prefab per level.json room,
    /// placed on its 45 m cell and rotated so its doors land on the doors level.json lists, plus the
    /// portcullis and spikes at armory_gate, the secret bookcase door at vault_door, a closed outer door,
    /// static defender / pickup markers, PlayerStart and Goal.
    ///
    /// Re-runnable: it owns the scene root named "IronCitadel" and nothing else. A rebuild opens the
    /// saved scene, deletes that root, rebuilds it and saves; any other root object (a walker, a HUD)
    /// survives.
    ///
    /// Batch: <c>unity run &lt;Builder&gt; -- -executeMethod IronCitadel.Rooms.Editor.IronCitadelRoomsAssembler.Build</c>
    /// builds, saves, runs the door check (C:/Repos/IronCitadel/logs/rooms-doorcheck.txt) and writes the
    /// captures (C:/Repos/IronCitadel/captures/rooms-*.png).</summary>
    public static class IronCitadelRoomsAssembler
    {
        public const string Dir = "Assets/IronCitadel";
        public const string LevelPath = Dir + "/level.json";
        public const string PicksPath = Dir + "/level.picks.json";
        public const string ScenePath = Dir + "/Scenes/IronCitadel_Rooms.unity";
        public const string RootName = "IronCitadel";
        public const string OutRoot = "C:/Repos/IronCitadel";
        public const float Tile = 45f;

        /// <summary>Night ambient: the rooms carry their own torches; this only keeps the unlit corners
        /// from going black.</summary>
        public static readonly Color NightAmbient = new Color(0.2f, 0.19f, 0.24f, 1f);
        public static readonly Color NightFillColor = new Color(0.62f, 0.68f, 0.9f, 1f);
        public const float NightFillIntensity = 0.13f;

        const string PD = "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/";
        const string PDR = "Assets/_THIRD_PARTY/Synty/PolygonDungeonRealms/Prefabs/";
        const string PDM = "Assets/Synty/PolygonDungeonMap/Prefabs/";
        const string PortcullisPrefab = PD + "Environments/Misc/SM_Env_Portcullis_01.prefab";
        const string SpikesWide = PD + "Props/SM_Prop_Goblin_Spikes_01.prefab";
        const string SpikesNarrow = PD + "Props/SM_Prop_Goblin_Spikes_02.prefab";
        const string BookcasePrefab = PDM + "SM_Prop_Bookcase_Small_01.prefab";
        const string OuterDoorPrefab = PD + "Environments/Walls/SM_Env_Door_01.prefab";
        const string ChestHoard = PD + "Props/SM_Prop_Chest_01.prefab";
        const string ChestGear = PD + "Props/SM_Prop_Chest_03.prefab";
        const string Coins = PD + "Items/SM_Item_Coins_02.prefab";

        static readonly string[] Garrison = { PD + "Characters/SM_Chr_Goblin_Warrior_Male_01.prefab", PD + "Characters/SM_Chr_Goblin_Warrior_Female_01.prefab" };
        static readonly string[] Feasters = { PD + "Characters/SM_Chr_Goblin_Male_01.prefab", PD + "Characters/SM_Chr_Goblin_Warrior_Male_01.prefab", PD + "Characters/SM_Chr_Goblin_Female_01.prefab", PD + "Characters/SM_Chr_Goblin_Warrior_Female_01.prefab" };
        static readonly string[] Cooks = { PD + "Characters/SM_Chr_Goblin_Female_01.prefab", PD + "Characters/SM_Chr_Goblin_Male_01.prefab" };
        static readonly string[] Prisoners = { PDR + "Characters/Chr_Nomad_Male_01.prefab", PDR + "Characters/Chr_Nomad_Female_01.prefab", PDR + "Characters/Chr_Nomad_Male_02.prefab", PDR + "Characters/Chr_Nomad_Female_02.prefab", PDR + "Characters/Chr_Nomad_Male_03.prefab" };
        static readonly string[] Smiths = { PDR + "Characters/Chr_BR_Dwarf_Worker_01.prefab", PDR + "Characters/Chr_BR_Dwarf_Miner_01.prefab" };
        static readonly string[] Alchemist = { PD + "Characters/SM_Chr_Goblin_Shaman_01.prefab" };
        static readonly string Warlord = PD + "Characters/SM_Chr_Goblin_WarChief_01.prefab";
        // the boss stands twice a goblin's size on the stage (Roger, 2026-10-07)
        const float WarlordScale = 2f;
        const string WarlordRole = "the warlord, on the stage";

        // ------------------------------------------------------------------ entry points

        /// <summary>-executeMethod entry: build + save, door check, captures. Exits in batchmode
        /// (0 ok, 2 the door check found a problem, 1 an exception).</summary>
        public static void Build()
        {
            int code = 0;
            try
            {
                EnsurePlayerPrefab();
                BuildScene();
                var ok = CheckDoors(out _);
                Captures();
                code = ok ? 0 : 2;
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log($"[IronCitadelRooms] done, exit {code}");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("Iron Citadel/Rooms/Build scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsurePlayerPrefab();
            BuildScene();
            CheckDoors(out var path);
            Debug.Log("[IronCitadelRooms] door check: " + path);
        }

        [MenuItem("Iron Citadel/Rooms/Check doors")]
        public static void CheckMenu()
        {
            CheckDoors(out var path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Iron Citadel/Rooms/Capture overview")]
        public static void CaptureMenu() => Captures();

        /// <summary>Diagnostic (-executeMethod): opens the saved scene and writes, for a few listed doors, the
        /// floor under lines across the doorway, low horizontal casts across it and every collider near it, to
        /// C:/Repos/IronCitadel/logs/rooms-probe.txt.</summary>
        public static void ProbeDoorways()
        {
            int code = 0;
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var root = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == RootName);
                root.transform.Find("Features").gameObject.SetActive(false);
                Physics.SyncTransforms();
                var level = LoadLevel();
                var sb = new StringBuilder();
                foreach (var id in new[] { "west_door", "cell_block", "prison_kitchen", "kitchen_hall", "forge_hall", "under_gallery", "throne_doors" })
                {
                    var d = level.doors.First(x => x.id == id);
                    var e = EdgeOfDoor(d);
                    sb.AppendLine($"== {id} {e} centre {e.centre} n {e.n}");
                    // low horizontal casts across the seam (sills, steps, thin leaves under the 0.5 m linecast)
                    foreach (var t in new[] { -0.5f, 0f, 0.5f })
                    for (float h = 0.05f; h <= 2.21f; h += h < 0.6f ? 0.05f : 0.4f)
                    {
                        var a = e.centre + e.along * t - e.n * 3f + Vector3.up * h; var b = e.centre + e.along * t + e.n * 3f + Vector3.up * h;
                        var fw = Physics.RaycastAll(a, (b - a).normalized, 6f, AllLayers, QueryTriggerInteraction.Ignore);
                        if (fw.Length > 0) sb.AppendLine($"  across t {t} h {h:0.00}: " + string.Join(" | ", fw.OrderBy(x => x.distance).Select(x => $"s {x.distance - 3f:0.00} {PathOf(x.collider.transform)} ({x.collider.GetType().Name})")));
                    }
                    foreach (var t in new[] { -0.5f, 0f, 0.5f })
                    {
                        sb.AppendLine($"  t {t}");
                        for (float s = -3f; s <= 3.01f; s += 0.1f)
                        {
                            var q = e.centre + e.along * t + e.n * s;
                            string hitTxt = "none";
                            var hits = Physics.RaycastAll(new Vector3(q.x, 3.3f, q.z), Vector3.down, 5.3f, AllLayers, QueryTriggerInteraction.Ignore);
                            if (hits.Length > 0) hitTxt = string.Join(" | ", hits.OrderBy(h => h.distance).Select(h => $"y {h.point.y:0.00} {PathOf(h.collider.transform)} ({h.collider.GetType().Name})"));
                            sb.AppendLine($"    s {s:0.0}: {hitTxt}");
                        }
                    }
                    var box = new Bounds(e.centre + Vector3.up * 1.5f, new Vector3(4f + 2f * Mathf.Abs(e.n.x), 3f, 4f + 2f * Mathf.Abs(e.n.z)));
                    foreach (var c in Physics.OverlapBox(box.center, box.extents, Quaternion.identity, AllLayers, QueryTriggerInteraction.Collide))
                        sb.AppendLine($"  near: {PathOf(c.transform)} ({c.GetType().Name}{(c is MeshCollider mc ? (mc.convex ? " convex" : " concave") + " mesh " + (mc.sharedMesh != null ? mc.sharedMesh.name : "null") : "")}) bounds {c.bounds.min}..{c.bounds.max}");
                    // renderers near the doorway floor, to compare with the colliders
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        var b = r.bounds;
                        if (b.max.y > 0.3f || b.min.y < -0.6f) continue;
                        if (!b.Intersects(new Bounds(e.centre, new Vector3(3f + 3f * Mathf.Abs(e.n.x), 2f, 3f + 3f * Mathf.Abs(e.n.z))))) continue;
                        var col = r.GetComponent<Collider>();
                        sb.AppendLine($"  floor mesh: {PathOf(r.transform)} bounds {b.min}..{b.max} collider {(col != null ? col.GetType().Name + (col.enabled ? "" : " (disabled)") : "NONE")}");
                    }
                }
                File.WriteAllText(OutRoot + "/logs/rooms-probe.txt", sb.ToString());
            }
            catch (Exception ex) { Debug.LogException(ex); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>Diagnostic (-executeMethod): opens the saved scene and walks straight lines through the
        /// places where conformance found the NavMesh broken, every 0.1 m: the floor (following steps up to
        /// 0.6 m), the headroom above it and whether the walker's capsule (IronCitadelPlayer: r 0.35, h 1.8
        /// plus 0.02 skin) fits there. Writes C:/Repos/IronCitadel/logs/rooms-probe-lines.txt.</summary>
        public static void ProbeLines()
        {
            int code = 0;
            try { ProbeLinesCore(); }
            catch (Exception ex) { Debug.LogException(ex); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>Diagnostic (-executeMethod): the four probes BuildAndScore runs, on the saved scene, without a
        /// rebuild: rooms-probe-lines.txt, rooms-vault-probe.txt, rooms-stage-probe.txt and rooms-hall-probe.txt in C:/Repos/IronCitadel/logs.</summary>
        public static void Probes()
        {
            int code = 0;
            try { ProbeLinesCore(); ProbeVaultCore(); ProbeStageCore(); ProbeHallCore(); }
            catch (Exception ex) { Debug.LogException(ex); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void ProbeLinesCore()
        {
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var root = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == RootName);
                var play = root.transform.Find("Play");
                if (play != null) play.gameObject.SetActive(false);
                Physics.SyncTransforms();
                var lines = new (string name, Vector2 a, Vector2 b)[]
                {
                    ("hall west stair, down the shaft onto the deck", new Vector2(97f, -66f), new Vector2(97f, -82f)),
                    ("hall east stair, down the shaft onto the deck", new Vector2(128f, -66f), new Vector2(128f, -82f)),
                    ("guard room, east corridor north into the room x 70.5", new Vector2(70.5f, -178f), new Vector2(70.5f, -164f)),
                    ("guard room, east corridor north into the room x 75", new Vector2(75f, -178f), new Vector2(75f, -164f)),
                    ("guard room, east corridor north into the room x 78", new Vector2(78f, -178f), new Vector2(78f, -164f)),
                    ("library, north corridor south into the room x 160", new Vector2(160f, -137.5f), new Vector2(160f, -150f)),
                    ("library, north corridor south into the room x 168", new Vector2(168f, -137.5f), new Vector2(168f, -150f)),
                    ("library, north corridor south into the room x 176.5", new Vector2(176.5f, -137.5f), new Vector2(176.5f, -150f)),
                    ("vault, top passage south into the treasury x 12", new Vector2(12f, -101.5f), new Vector2(12f, -112f)),
                    ("vault, top passage south into the treasury x 15", new Vector2(15f, -101.5f), new Vector2(15f, -112f)),
                    ("vault, lobby north into the top passage x 37", new Vector2(37f, -110f), new Vector2(37f, -100f)),
                };
                const float r = 0.35f, h = 1.82f;
                var sb = new StringBuilder();
                sb.AppendLine("Rows print where the floor steps, the headroom is under 1.82 m or the capsule (r 0.35, h 1.8 + 0.02 skin) does not fit; 'hit' names what it touches.");
                foreach (var (name, a, b) in lines)
                {
                    sb.AppendLine($"== {name}: ({a.x}, {a.y}) -> ({b.x}, {b.y})");
                    float len = Vector2.Distance(a, b), prevY = float.NaN;
                    bool prevBad = false;
                    for (float s = 0f; s <= len + 1e-3f; s += 0.1f)
                    {
                        var q = Vector2.Lerp(a, b, s / len);
                        // floor: the highest surface no more than 0.6 m above the last one (a step), else the next one down
                        float top = (float.IsNaN(prevY) ? 0f : prevY) + 0.6f;
                        string floorWhat = "none"; float fy = float.NaN;
                        foreach (var hit in Physics.RaycastAll(new Vector3(q.x, top + 0.01f, q.y), Vector3.down, 30f, AllLayers, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
                        { fy = hit.point.y; floorWhat = PathOf(hit.collider.transform); break; }
                        if (float.IsNaN(fy)) { sb.AppendLine($"  s {s:0.0}: no floor"); prevBad = true; continue; }
                        var foot = new Vector3(q.x, fy, q.y);
                        float head = Physics.Raycast(foot + Vector3.up * 0.05f, Vector3.up, out var up, 30f, AllLayers, QueryTriggerInteraction.Ignore) ? up.distance + 0.05f : 99f;
                        string headWhat = head < 99f ? PathOf(up.collider.transform) : "-";
                        var p0 = foot + Vector3.up * (0.02f + r); var p1 = foot + Vector3.up * (h - r);
                        var touching = Physics.OverlapCapsule(p0, p1, r - 0.01f, AllLayers, QueryTriggerInteraction.Ignore)
                            .Where(c => !(c.bounds.max.y <= fy + 0.03f)).Select(c => PathOf(c.transform)).Distinct().Take(3).ToList();
                        bool bad = head < h || touching.Count > 0;
                        bool step = !float.IsNaN(prevY) && Mathf.Abs(fy - prevY) > 0.05f;
                        if (s == 0f || step || bad || prevBad || s + 0.1f > len)
                            sb.AppendLine($"  s {s:0.0} ({q.x:0.0}, {q.y:0.0}): floor {fy:0.00}{(step ? $" (step {fy - prevY:+0.00;-0.00})" : "")} on {floorWhat}; headroom {head:0.00} under {headWhat}" +
                                          (touching.Count > 0 ? "; capsule hits " + string.Join(", ", touching) : ""));
                        prevY = fy; prevBad = bad;
                    }
                }
                File.WriteAllText(OutRoot + "/logs/rooms-probe-lines.txt", sb.ToString());
            }
        }

        /// <summary>Diagnostic, run by BuildAndScore: what stands on the throne stage's front steps. Every 0.5 m along
        /// the front (x 104 .. 121, z -17.5 .. -14) it lists the colliders taller than the stage (1.3 m) in a 0.5 m
        /// wide band from the floor to 2 m. Writes C:/Repos/IronCitadel/logs/rooms-stage-probe.txt.</summary>
        public static void ProbeStageCore()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().First(g => g.name == RootName);
            var play = root.transform.Find("Play");
            if (play != null) play.gameObject.SetActive(false);
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            sb.AppendLine("Throne stage front steps: colliders reaching above 1.3 m in a 0.5 m band, x every 0.5 m, z -17.5 .. -14, floor to 2 m.");
            for (float x = 104f; x <= 121.01f; x += 0.5f)
            {
                var hits = Physics.OverlapBox(new Vector3(x, 1.05f, -15.75f), new Vector3(0.25f, 0.95f, 1.75f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore)
                    .Where(c => c.bounds.max.y > 1.3f && c.bounds.min.y < 2f)
                    .Select(c => $"{PathOf(c.transform)} [x {c.bounds.min.x:0.0}..{c.bounds.max.x:0.0}, y {c.bounds.min.y:0.0}..{c.bounds.max.y:0.0}, z {c.bounds.min.z:0.0}..{c.bounds.max.z:0.0}]")
                    .Distinct().ToList();
                sb.AppendLine($"  x {x:0.0}: {(hits.Count == 0 ? "-" : string.Join("; ", hits))}");
            }
            // every collider in the columns check 8 found unstepped (and one it found stepped, 112.5), with the floor profile
            sb.AppendLine("Every collider in a 0.3 m column, z -17.5 .. -13.5, floor to 1.6 m; then the floor profile (top surface every 0.25 m in z).");
            foreach (float x in new[] { 105.75f, 106.25f, 108.75f, 109.25f, 109.75f, 112.5f, 118.75f, 119.25f })
            {
                var hits = Physics.OverlapBox(new Vector3(x, 0.85f, -15.5f), new Vector3(0.15f, 0.75f, 2f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore)
                    .Select(c => $"{PathOf(c.transform)} [y {c.bounds.min.y:0.00}..{c.bounds.max.y:0.00}, z {c.bounds.min.z:0.0}..{c.bounds.max.z:0.0}]").Distinct().ToList();
                sb.AppendLine($"  x {x:0.00}: {string.Join("; ", hits)}");
                var prof = new List<string>();
                for (float z = -18f; z <= -13.5f; z += 0.25f)
                    prof.Add(Physics.Raycast(new Vector3(x, 3f, z), Vector3.down, out var fh, 4f, AllLayers, QueryTriggerInteraction.Ignore) ? $"{z:0.00}:{fh.point.y:0.00}" : $"{z:0.00}:-");
                sb.AppendLine("     floor " + string.Join(" ", prof));
            }
            // what stands at the foot of the steps (z -20 .. -17.3), any height above the floor
            sb.AppendLine("At the foot of the steps (x 104 .. 121, z -20 .. -17.3), every collider rising above 0.15 m:");
            foreach (var c in Physics.OverlapBox(new Vector3(112.5f, 1.2f, -18.65f), new Vector3(8.5f, 1.05f, 1.35f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore)
                         .Where(c => c.bounds.max.y > 0.15f).OrderBy(c => c.bounds.center.x))
                sb.AppendLine($"  {PathOf(c.transform)} [{c.GetType().Name}; x {c.bounds.min.x:0.00}..{c.bounds.max.x:0.00}, y {c.bounds.min.y:0.00}..{c.bounds.max.y:0.00}, z {c.bounds.min.z:0.00}..{c.bounds.max.z:0.00}]");
            File.WriteAllText(OutRoot + "/logs/rooms-stage-probe.txt", sb.ToString());
        }

        /// <summary>Diagnostic, run by BuildAndScore: opens the saved scene, switches the secret door and its decoys
        /// off, and for every door frame and metal door leaf in the vault cell writes its bounds and a scan of the
        /// walker's capsule (r 0.35, h 1.8) across the frame, at the frame and 0.6 m and 1.2 m either side of it
        /// ('.' free, '#' blocked, with what blocks each run). Then it bakes the vault and prison cells at the
        /// walker's size and writes the NavMesh paths prison -> lobby -> passage -> treasury.
        /// Writes C:/Repos/IronCitadel/logs/rooms-vault-probe.txt.</summary>
        public static void ProbeVaultCore()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().First(g => g.name == RootName);
            var play = root.transform.Find("Play");
            if (play != null) play.gameObject.SetActive(false);
            var vd = root.transform.Find("Features/vault_door");
            if (vd != null) vd.gameObject.SetActive(false);
            Physics.SyncTransforms();
            var level = LoadLevel();
            var vault = level.rooms.First(r => r.kind == "V");
            var room = root.transform.Find("Rooms/" + vault.id);
            float x0 = Tile * vault.c, x1 = x0 + Tile, z1 = -Tile * vault.r, z0 = z1 - Tile;
            var sb = new StringBuilder();
            sb.AppendLine("Vault doorways with the secret door off. Scan rows: offset from the frame plane; '.' the capsule (r 0.35, h 1.8) fits, '#' it does not; one column per 0.05 m along the frame, -1.5 .. +1.5 m from its centre.");
            const float r = 0.35f, h = 1.8f;
            Bounds WB(Transform t)
            {
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
                var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); return b;
            }
            foreach (var t in room.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("SM_Prop_Door_Metal", StringComparison.Ordinal)))
            {
                if (t.parent != null && t.parent.name.StartsWith("SM_Prop_Door_Metal", StringComparison.Ordinal)) continue;
                var b = WB(t);
                sb.AppendLine($"leaf {PathOf(t)}: bounds x {b.min.x:0.00}..{b.max.x:0.00}, y {b.min.y:0.00}..{b.max.y:0.00}, z {b.min.z:0.00}..{b.max.z:0.00}; colliders {t.GetComponentsInChildren<Collider>(true).Length}");
            }
            var frames = room.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("SM_Env_Door_Frame", StringComparison.Ordinal) || t.name.StartsWith("SM_Env_Wall_DoorFrame", StringComparison.Ordinal))
                .Where(t => t.GetComponent<MeshFilter>() != null)
                .ToList();
            foreach (var f in frames)
            {
                var b = WB(f);
                if (b.center.x < x0 || b.center.x > x1 || b.center.z < z0 || b.center.z > z1) continue;
                bool wideX = b.size.x >= b.size.z;
                var along = wideX ? Vector3.right : Vector3.forward;
                var n = wideX ? Vector3.forward : Vector3.right;
                var cols = f.GetComponents<Collider>().Select(c => c.GetType().Name + (c is MeshCollider mc ? (mc.convex ? " convex" : " concave") : "")).ToList();
                sb.AppendLine($"== frame {PathOf(f)}: bounds x {b.min.x:0.00}..{b.max.x:0.00}, y {b.min.y:0.00}..{b.max.y:0.00}, z {b.min.z:0.00}..{b.max.z:0.00}; collider {(cols.Count > 0 ? string.Join(", ", cols) : "none")}");
                float floorY = b.min.y;
                foreach (float off in new[] { -1.2f, -0.6f, 0f, 0.6f, 1.2f })
                {
                    var row = new StringBuilder();
                    var runs = new List<string>();
                    string lastWhat = null; float runStart = -1.5f; bool? lastFree = null;
                    for (int k = 0; k <= 60; k++)
                    {
                        float t = -1.5f + 0.05f * k;
                        var foot = new Vector3(b.center.x, floorY, b.center.z) + along * t + n * off;
                        if (FloorAt(foot + Vector3.up * 1.5f, out var fy, foot.y + 1.5f)) foot.y = fy;
                        var hits = Physics.OverlapCapsule(foot + Vector3.up * (0.04f + r), foot + Vector3.up * (h - r), r - 0.01f, AllLayers, QueryTriggerInteraction.Ignore)
                            .Where(c => c.bounds.max.y > foot.y + 0.05f).ToList();
                        bool free = hits.Count == 0;
                        row.Append(free ? '.' : '#');
                        string what = free ? "free" : string.Join(" + ", hits.Select(c => c.name).Distinct().Take(2));
                        if (lastFree != free || (!free && what != lastWhat))
                        {
                            if (lastFree != null) runs.Add($"{runStart:+0.00;-0.00}..{t - 0.05f:+0.00;-0.00} {lastWhat}");
                            runStart = t; lastFree = free; lastWhat = what;
                        }
                    }
                    runs.Add($"{runStart:+0.00;-0.00}..+1.50 {lastWhat}");
                    sb.AppendLine($"  {off,5:+0.0;-0.0} m |{row}|");
                    if (off == 0f) sb.AppendLine("           " + string.Join("; ", runs));
                }
            }

            // NavMesh over the vault and prison cells
            var set = IronCitadel.Conformance.NavBake.Settings();
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = set.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            UnityEngine.AI.NavMesh.RemoveAllNavMeshData();
            var bounds = new Bounds(new Vector3(x0 + Tile, 10f, (z0 + z1) / 2f), new Vector3(2 * Tile + 4f, 40f, Tile + 4f));
            var sources = new List<UnityEngine.AI.NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, ~0, UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders, 0, new List<UnityEngine.AI.NavMeshBuildMarkup>(), sources);
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(set, sources, bounds, Vector3.zero, Quaternion.identity);
            var inst = UnityEngine.AI.NavMesh.AddNavMeshData(data);
            var stops = new (string name, Vector3 p)[]
            {
                ("prison, west end", new Vector3(50f, 0.5f, -112.5f)),
                ("vault lobby", new Vector3(40f, 0.5f, -112f)),
                ("top passage, east", new Vector3(37f, 0.5f, -102.5f)),
                ("top passage, west", new Vector3(14f, 0.5f, -102.5f)),
                ("treasury", new Vector3(12f, 0.5f, -112f)),
                ("treasury goal", new Vector3(18.3f, 0.5f, -111f)),
            };
            sb.AppendLine($"== NavMesh over the vault and prison cells (secret door off), {sources.Count} sources");
            for (int k = 1; k < stops.Length; k++)
            {
                var a = stops[k - 1]; var c = stops[k];
                bool okA = UnityEngine.AI.NavMesh.SamplePosition(a.p, out var ha, 3f, filter), okC = UnityEngine.AI.NavMesh.SamplePosition(c.p, out var hc, 3f, filter);
                if (!okA || !okC) { sb.AppendLine($"  {a.name} -> {c.name}: no NavMesh within 3 m of {(okA ? c.name : a.name)}"); continue; }
                var np = new UnityEngine.AI.NavMeshPath();
                UnityEngine.AI.NavMesh.CalculatePath(ha.position, hc.position, filter, np);
                sb.AppendLine($"  {a.name} {ha.position.ToString("F1")} -> {c.name} {hc.position.ToString("F1")}: {np.status}, corners {string.Join(" ", np.corners.Select(v => v.ToString("F1")))}");
            }
            inst.Remove();
            Object.DestroyImmediate(data);
            File.WriteAllText(OutRoot + "/logs/rooms-vault-probe.txt", sb.ToString());
        }

        /// <summary>Diagnostic, run by BuildAndScore: the great hall's side paths, as conformance check 7(c) walks them. It
        /// bakes the hall and its east and west neighbours at the walker's size, finds the gallery deck (NavMesh 5 m +- 1 m up
        /// within 15 m of the south wall) and its centre point, and for the west and east doorways prints the NavMesh path
        /// from 1 m outside the doorway to that point: its corners, then every 0.5 m point with u (m from the west wall),
        /// v (m from the south wall), its height, and '*' where check 7(c) counts it as central floor (u 7..38, v 7..42,
        /// under 1 m up). Then the colliders around the first such point, and a 0.5 m map of the hall cell: '.' floor
        /// NavMesh, 's' raised NavMesh under 4.5 m (stairs), 'g' NavMesh 4.5 m up or more (the deck), '#' a solid at 1 m
        /// with no NavMesh, ' ' nothing; 'w'/'e' the west/east path at floor level, 'W'/'E' where it counts as central.
        /// The ruler marks u 7 and 38. Writes C:/Repos/IronCitadel/logs/rooms-hall-probe.txt.</summary>
        public static void ProbeHallCore()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().First(g => g.name == RootName);
            var play = root.transform.Find("Play");
            if (play != null) play.gameObject.SetActive(false);
            Physics.SyncTransforms();
            var level = LoadLevel();
            var hall = level.rooms.First(r => r.kind == "D");
            float x0 = Tile * hall.c, x1 = x0 + Tile, z1 = -Tile * hall.r, z0 = z1 - Tile;
            var sb = new StringBuilder();
            var set = IronCitadel.Conformance.NavBake.Settings();
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = set.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            UnityEngine.AI.NavMesh.RemoveAllNavMeshData();
            var bounds = new Bounds(CellCenter(hall.r, hall.c) + Vector3.up * 10f, new Vector3(3 * Tile + 4f, 40f, Tile + 4f));
            var sources = new List<UnityEngine.AI.NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, ~0, UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders, 0, new List<UnityEngine.AI.NavMeshBuildMarkup>(), sources);
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(set, sources, bounds, Vector3.zero, Quaternion.identity);
            var inst = UnityEngine.AI.NavMesh.AddNavMeshData(data);
            try
            {
                bool Hit(Vector3 q, float tol, out Vector3 p)
                {
                    p = default;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(q, out var h, tol, filter)) return false;
                    p = h.position;
                    return new Vector2(p.x - q.x, p.z - q.z).magnitude <= 0.25f;
                }
                // the deck and its centre point, as check 7(a) picks it
                var deck = new List<Vector3>();
                for (float x = x0 + 0.25f; x < x1; x += 0.5f)
                    for (float z = z0 + 0.25f; z < z0 + 15f; z += 0.5f)
                        if (Hit(new Vector3(x, 5f, z), 1.05f, out var p) && Mathf.Abs(p.y - 5f) <= 1f) deck.Add(p);
                if (deck.Count == 0) { sb.AppendLine("no deck NavMesh"); return; }
                var centroid = new Vector3(deck.Average(p => p.x), deck.Average(p => p.y), deck.Average(p => p.z));
                var gp = deck.OrderBy(p => (p - centroid).sqrMagnitude).First();
                sb.AppendLine($"deck: {deck.Count * 0.25f:0} m2, centre point {gp.ToString("F2")}");
                var marks = new Dictionary<(int, int), char>();
                foreach (var (side, nc) in new[] { ("west", hall.c - 1), ("east", hall.c + 1) })
                {
                    var e = EdgeBetween(hall.r, hall.c, hall.r, nc);
                    MeasureOpening(e, out var tc, out var w, out _);
                    var crossing = e.centre + e.along * tc;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(crossing + e.n * 1f + Vector3.up * 0.3f, out var sh, 1.5f, filter))
                    { sb.AppendLine($"== {side}: no NavMesh 1 m outside the doorway at {crossing.ToString("F2")}"); continue; }
                    var np = new UnityEngine.AI.NavMeshPath();
                    UnityEngine.AI.NavMesh.CalculatePath(sh.position, gp, filter, np);
                    sb.AppendLine($"== {side} doorway (centre {crossing.ToString("F2")}, {w:0.00} m wide): path {np.status}, corners {string.Join(" ", np.corners.Select(v => v.ToString("F2")))}");
                    Vector3? first = null; int central = 0;
                    for (int k = 1; k < np.corners.Length; k++)
                    {
                        var a = np.corners[k - 1]; var b = np.corners[k];
                        int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.5f));
                        for (int i = 0; i <= n; i++)
                        {
                            var p = Vector3.Lerp(a, b, i / (float)n);
                            if (p.x < x0 || p.x > x1 || p.z < z0 || p.z > z1) continue;
                            float u = p.x - x0, v = p.z - z0;
                            bool low = p.y < 1f;
                            bool isCentral = low && u >= 7f && u <= Tile - 7f && v >= 7f && v <= Tile - 3f;
                            if (isCentral) { central++; if (first == null) first = p; }
                            if (low) marks[(Mathf.FloorToInt((p.x - x0) / 0.5f), Mathf.FloorToInt((z1 - p.z) / 0.5f))] = isCentral ? char.ToUpperInvariant(side[0]) : side[0];
                            if (isCentral || (low && (u < 12f || u > Tile - 12f) && i % 2 == 0))
                                sb.AppendLine($"    ({p.x:0.00}, {p.y:0.00}, {p.z:0.00}) u {u:0.00} v {v:0.00}{(isCentral ? "  * central floor" : "")}");
                        }
                    }
                    sb.AppendLine($"  {central} point(s) on the central floor");
                    if (first != null)
                        foreach (var c in Physics.OverlapBox(first.Value + Vector3.up * 1f, new Vector3(1.5f, 0.9f, 1.5f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore).OrderBy(c => c.bounds.center.x))
                            sb.AppendLine($"  near the first: {PathOf(c.transform)} [{c.GetType().Name}; x {c.bounds.min.x:0.00}..{c.bounds.max.x:0.00}, y {c.bounds.min.y:0.00}..{c.bounds.max.y:0.00}, z {c.bounds.min.z:0.00}..{c.bounds.max.z:0.00}]");
                }
                // the map: rows north (z1) to south (z0), 0.5 m cells
                int nx = Mathf.RoundToInt(Tile / 0.5f);
                var ruler = new StringBuilder(new string(' ', 9));
                for (int i = 0; i < nx; i++) ruler.Append(i == 14 || i == nx - 14 ? '|' : i % 10 == 0 ? '+' : '-');
                sb.AppendLine($"map of the hall cell, x {x0}..{x1} left to right, z {z1}..{z0} top to bottom, 0.5 m cells; '|' at u 7 and 38");
                sb.AppendLine(ruler.ToString());
                for (int j = 0; j < nx; j++)
                {
                    float z = z1 - (j + 0.5f) * 0.5f;
                    var row = new StringBuilder($"{z,8:0.00} ");
                    for (int i = 0; i < nx; i++)
                    {
                        float x = x0 + (i + 0.5f) * 0.5f;
                        char ch = ' ';
                        if (marks.TryGetValue((i, j), out var m)) ch = m;
                        else if (Hit(new Vector3(x, 0.3f, z), 0.5f, out var p0) && p0.y < 0.6f) ch = '.';
                        else if (Hit(new Vector3(x, 5.1f, z), 1.2f, out var p2) && p2.y >= 4.5f) ch = 'g';
                        else if (Hit(new Vector3(x, 1.5f, z), 1.0f, out var p1) && p1.y >= 0.6f && p1.y < 4.5f) ch = 's';
                        else if (Hit(new Vector3(x, 3.2f, z), 1.0f, out var p3) && p3.y >= 0.6f && p3.y < 4.5f) ch = 's';
                        else if (Physics.CheckBox(new Vector3(x, 1f, z), new Vector3(0.2f, 0.6f, 0.2f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore)) ch = '#';
                        row.Append(ch);
                    }
                    sb.AppendLine(row.ToString());
                }
            }
            finally
            {
                inst.Remove();
                Object.DestroyImmediate(data);
                File.WriteAllText(OutRoot + "/logs/rooms-hall-probe.txt", sb.ToString());
            }
        }

        /// <summary>Diagnostic (-executeMethod, -rooms guard_room,library): for each named room, every K9 prefab of its
        /// theme with the picked prefab's door set is placed alone on the room's cell with the picked rotation, its
        /// missing colliders patched as in a build, and baked with the conformance kit's settings (the walker's
        /// r 0.35, h 1.8, step 0.3). The trial reports whether a NavMesh path joins the room's two listed doors.
        /// Writes C:/Repos/IronCitadel/logs/rooms-picks-trial.txt. The open scene is replaced by an empty one.</summary>
        public static void TryRoomPicks()
        {
            int code = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-rooms");
                TryRoomPicksCore((i >= 0 && i + 1 < args.Length ? args[i + 1] : "guard_room,library").Split(','));
            }
            catch (Exception ex) { Debug.LogException(ex); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static void TryRoomPicksCore(string[] roomIds, bool allDoorSets = false)
        {
            var level = LoadLevel();
            var picks = LoadPicks();
            var sb = new StringBuilder();
            sb.AppendLine("Each K9 prefab with the pick's door set, alone on the room's cell, baked at r 0.35 / h 1.8 / step 0.3; JOINED = a complete NavMesh path between the room's two doors. Heights as check 9 samples them (every 3 m).");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var set = IronCitadel.Conformance.NavBake.Settings();
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = set.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            foreach (var id in roomIds)
            {
                var room = level.ById(id.Trim());
                var pick = picks[room.id];
                string dir = Path.GetDirectoryName(pick.prefab).Replace('\\', '/');
                var token = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(pick.prefab), @"K9[NESW]+_").Value;
                var cands = Directory.GetFiles(dir, "*" + (allDoorSets ? "K9" : token) + "*.prefab").Select(f => f.Replace('\\', '/'))
                    .OrderBy(f => f == pick.prefab ? 0 : 1).ThenBy(f => f).ToList();
                var doors = level.doors.Where(d => !d.outside && ((d.r1 == room.r && d.c1 == room.c) || (d.r2 == room.r && d.c2 == room.c))).ToList();
                sb.AppendLine($"== {room.id} [{room.r},{room.c}] ({room.state}), rotation {pick.rotation}, doors {string.Join(", ", doors.Select(d => d.id))}");
                foreach (var path in cands)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                    go.transform.position = CellCenter(room.r, room.c);
                    go.transform.rotation = Quaternion.Euler(0f, pick.rotation, 0f);
                    Physics.SyncTransforms();
                    PatchMissingColliders(new Dictionary<string, GameObject> { { room.id, go } }, new StringBuilder());
                    Physics.SyncTransforms();
                    var bounds = new Bounds(CellCenter(room.r, room.c) + Vector3.up * 10f, new Vector3(Tile + 4f, 40f, Tile + 4f));
                    var sources = new List<UnityEngine.AI.NavMeshBuildSource>();
                    UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, ~0, UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders, 0, new List<UnityEngine.AI.NavMeshBuildMarkup>(), sources);
                    var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(set, sources, bounds, Vector3.zero, Quaternion.identity);
                    var inst = UnityEngine.AI.NavMesh.AddNavMeshData(data);
                    var pts = new List<Vector3?>();
                    foreach (var d in doors)
                    {
                        bool mine = d.r1 == room.r && d.c1 == room.c;
                        var e = EdgeBetween(room.r, room.c, mine ? d.r2 : d.r1, mine ? d.c2 : d.c1);
                        var q = e.centre - e.n * 1.0f + Vector3.up * 0.5f;
                        pts.Add(UnityEngine.AI.NavMesh.SamplePosition(q, out var hit, 2.5f, filter) ? hit.position : (Vector3?)null);
                    }
                    string verdict;
                    var candToken = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(path), @"K9[NESW]+_").Value;
                    if (candToken != token) verdict = $"doors {candToken.Trim('_')}, not the pick's {token.Trim('_')}: heights only";
                    else if (pts.Count != 2) verdict = $"{pts.Count} doors listed, not 2";
                    else if (pts.Any(p => p == null)) verdict = "a door has no NavMesh within 2.5 m of its doorway";
                    else
                    {
                        var np = new UnityEngine.AI.NavMeshPath();
                        UnityEngine.AI.NavMesh.CalculatePath(pts[0].Value, pts[1].Value, filter, np);
                        float len = 0f; for (int k = 1; k < np.corners.Length; k++) len += Vector3.Distance(np.corners[k - 1], np.corners[k]);
                        verdict = np.status == UnityEngine.AI.NavMeshPathStatus.PathComplete ? $"JOINED, {len:0} m door to door"
                            : $"CUT ({np.status}), the path from {doors[0].id} ends at {(np.corners.Length > 0 ? np.corners[np.corners.Length - 1].ToString("F1") : "its start")}";
                    }
                    // heights, as conformance check 9 samples them: the floor every 3 m, the ceiling straight above
                    var lids = new List<float>(); int open = 0;
                    bool oldBack = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
                    for (float x = CellCenter(room.r, room.c).x - Tile / 2f + 1.5f; x < CellCenter(room.r, room.c).x + Tile / 2f; x += 3f)
                    for (float z = CellCenter(room.r, room.c).z - Tile / 2f + 1.5f; z < CellCenter(room.r, room.c).z + Tile / 2f; z += 3f)
                    {
                        if (!UnityEngine.AI.NavMesh.SamplePosition(new Vector3(x, 0.5f, z), out var fh, 1f, filter) || Mathf.Abs(fh.position.y) > 0.6f) continue;
                        if (Physics.Raycast(fh.position + Vector3.up * 0.1f, Vector3.up, out var up, 60f, AllLayers, QueryTriggerInteraction.Ignore)) lids.Add(up.distance + 0.1f);
                        else open++;
                    }
                    Physics.queriesHitBackfaces = oldBack;
                    lids.Sort();
                    int n = lids.Count + open;
                    string heights = n == 0 ? "no floor samples" :
                        $"{lids.Count(v => v <= 6.5f) * 100 / n}% of {n} floor samples under <= 6.5 m, {lids.Count(v => v >= 8f) * 100 / n}% under >= 8 m, median lid {(lids.Count > 0 ? lids[lids.Count / 2].ToString("0.0") : "-")} m";
                    sb.AppendLine($"  {(path == pick.prefab ? "* " : "  ")}{Path.GetFileNameWithoutExtension(path)}: {verdict}; {heights}");
                    inst.Remove();
                    Object.DestroyImmediate(data);
                    Object.DestroyImmediate(go);
                }
            }
            Directory.CreateDirectory(OutRoot + "/logs");
            File.WriteAllText(OutRoot + "/logs/rooms-picks-trial.txt", sb.ToString());
            Debug.Log("[IronCitadelRooms] picks trial\n" + sb);
        }

        public const string CanonLevel = "C:/Repos/Github/Crawfis-Software/UnityAssets/docs/research/AI-DungeonLevelComparison/level.json";

        /// <summary>-executeMethod entry for one whole round: [-makePlayer] re-makes the walker prefab first; then
        /// build and save the scene, the door check, the captures, the line probe, and the conformance score of the
        /// saved scene (level: -level, default the talk's level.json; out: -out, default
        /// C:/Repos/IronCitadel/scores/rooms). Exits 0, 2 when the door check fails, 1 on an exception.</summary>
        public static void BuildAndScore()
        {
            int code = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                string Arg(string n) { int i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                if (Array.IndexOf(args, "-makePlayer") >= 0) IronCitadel.Interact.Editor.IronCitadelPlayerSetup.MakePlayer();
                var trial = Arg("-trialPicks");
                if (trial != null) TryRoomPicksCore(trial.Split(','), Array.IndexOf(args, "-trialAll") >= 0);
                EnsurePlayerPrefab();
                BuildScene();
                bool ok = CheckDoors(out _);
                Captures();
                ProbeLinesCore();
                ProbeVaultCore();
                ProbeStageCore();
                ProbeHallCore();
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                IronCitadel.Conformance.ConformanceCli.ScoreScene(scene, Arg("-level") ?? CanonLevel, Arg("-out") ?? OutRoot + "/scores/rooms", true);
                code = ok ? 0 : 2;
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log($"[IronCitadelRooms] build and score done, exit {code}");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ level model

        public class Room { public string id, kind, state, height; public int r, c; }
        public class Door { public string id, kind, note; public bool passable; public int r1, c1, r2, c2; public bool outside; public char outsideSide; }
        public class Pick { public string room, prefab; public int rotation; }

        public class Level
        {
            public int rows, cols;
            public string[] grid;
            public List<Room> rooms = new List<Room>();
            public List<Door> doors = new List<Door>();
            public JObject json;
            public Room RoomAt(int r, int c) => rooms.FirstOrDefault(x => x.r == r && x.c == c);
            public Room ById(string id) => rooms.First(x => x.id == id);
        }

        public static Level LoadLevel()
        {
            var j = JObject.Parse(File.ReadAllText(LevelPath));
            var L = new Level { json = j };
            L.rows = (int)j["world"]["rows"];
            L.cols = (int)j["world"]["cols"];
            L.grid = j["grid"].Select(t => (string)t).ToArray();
            foreach (var r in j["rooms"])
                L.rooms.Add(new Room { id = (string)r["id"], kind = (string)r["kind"], state = (string)r["state"], height = (string)r["height"], r = (int)r["cell"][0], c = (int)r["cell"][1] });
            foreach (var d in j["doors"])
            {
                var b = (JArray)d["between"];
                var door = new Door { id = (string)d["id"], kind = (string)d["kind"], note = (string)d["note"], passable = (bool)d["passable"], r1 = (int)b[0][0], c1 = (int)b[0][1] };
                if (b[1].Type == JTokenType.String)
                {
                    var s = ((string)b[1]).ToLowerInvariant();
                    door.outside = true; door.r2 = -1; door.c2 = -1;
                    door.outsideSide = s.Contains("south") ? 'S' : s.Contains("north") ? 'N' : s.Contains("east") ? 'E' : 'W';
                }
                else { door.r2 = (int)b[1][0]; door.c2 = (int)b[1][1]; }
                L.doors.Add(door);
            }
            return L;
        }

        public static Dictionary<string, Pick> LoadPicks()
        {
            var j = JObject.Parse(File.ReadAllText(PicksPath));
            string root = (string)j["rooms_root"];
            var d = new Dictionary<string, Pick>();
            foreach (var p in j["picks"])
                d[(string)p["room"]] = new Pick { room = (string)p["room"], prefab = root + "/" + (string)p["prefab"], rotation = (int)p["rotation"] };
            return d;
        }

        public static Vector3 CellCenter(int r, int c) => new Vector3(Tile * c + Tile / 2f, 0f, -Tile * r - Tile / 2f);

        /// <summary>A tile edge seen from cell (r, c) on side N/E/S/W: its centre, the unit normal from
        /// (r, c) out across it, and the unit direction along it.</summary>
        public struct Edge
        {
            public int r, c, r2, c2; public char side;
            public Vector3 centre, n, along;
            public override string ToString() => $"[{r},{c}]{side}";
        }

        public static Edge EdgeOf(int r, int c, char side)
        {
            var e = new Edge { r = r, c = c, side = side };
            switch (side)
            {
                case 'N': e.r2 = r - 1; e.c2 = c; e.centre = new Vector3(Tile * c + Tile / 2, 0, -Tile * r); e.n = Vector3.forward; break;
                case 'S': e.r2 = r + 1; e.c2 = c; e.centre = new Vector3(Tile * c + Tile / 2, 0, -Tile * (r + 1)); e.n = Vector3.back; break;
                case 'E': e.r2 = r; e.c2 = c + 1; e.centre = new Vector3(Tile * (c + 1), 0, -Tile * r - Tile / 2); e.n = Vector3.right; break;
                default: e.r2 = r; e.c2 = c - 1; e.centre = new Vector3(Tile * c, 0, -Tile * r - Tile / 2); e.n = Vector3.left; break;
            }
            e.along = Mathf.Abs(e.n.z) > 0.5f ? Vector3.right : Vector3.forward;
            return e;
        }

        public static Edge EdgeBetween(int r1, int c1, int r2, int c2)
        {
            char side = r2 == r1 - 1 ? 'N' : r2 == r1 + 1 ? 'S' : c2 == c1 + 1 ? 'E' : 'W';
            return EdgeOf(r1, c1, side);
        }

        public static Edge EdgeOfDoor(Door d) => d.outside ? EdgeOf(d.r1, d.c1, d.outsideSide) : EdgeBetween(d.r1, d.c1, d.r2, d.c2);

        // ------------------------------------------------------------------ build

        public static GameObject BuildScene()
        {
            var level = LoadLevel();
            var picks = LoadPicks();
            Scene scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var roomsT = Child(root.transform, "Rooms");
            var featT = Child(root.transform, "Features");
            var markT = Child(root.transform, "Markers");

            var rooms = new Dictionary<string, GameObject>();
            foreach (var room in level.rooms)
            {
                if (!picks.TryGetValue(room.id, out var pick)) throw new Exception($"level.picks.json has no pick for {room.id}");
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(pick.prefab);
                if (asset == null) throw new Exception($"{room.id}: prefab not found: {pick.prefab}");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                go.name = room.id;
                go.transform.SetParent(roomsT, false);
                go.transform.position = CellCenter(room.r, room.c);
                go.transform.rotation = Quaternion.Euler(0f, pick.rotation, 0f);
                rooms[room.id] = go;
            }
            Physics.SyncTransforms();

            var lighting = new GameObject("Lighting");
            lighting.transform.SetParent(root.transform, false);
            AddNightFill(lighting.transform);
            ApplyNightLighting();

            var log = new StringBuilder();
            PatchMissingColliders(rooms, log);
            Physics.SyncTransforms();
            OpenVaultDoors(level, rooms, log);
            Physics.SyncTransforms();
            ClearStageFront(level, rooms, log);
            Physics.SyncTransforms();
            CapLowLids(level, rooms, log);
            Physics.SyncTransforms();
            MirrorHallLandingStub(level, rooms, log);
            Physics.SyncTransforms();
            s_openings.Clear();
            foreach (var door in level.doors)
            {
                var de = EdgeOfDoor(door);
                if (MeasureOpening(de, out var dtc, out var dw, out var dh)) log.AppendLine($"opening {door.id} at {de}: centre t {dtc:0.00}, width {dw:0.00}, height {dh:0.00}");
                else log.AppendLine($"opening {door.id} at {de}: NONE FOUND");
            }
            foreach (var door in level.doors)
            {
                if (door.kind == "portcullis") BuildPortcullis(level, door, featT, log);
                else if (door.kind == "secret door") BuildSecretBookcase(level, door, featT, log);
                else if (door.outside) BuildOuterDoor(level, door, featT, markT, log);
            }
            Physics.SyncTransforms();
            BuildMarkers(level, rooms, markT, log);
            BuildPlay(level, root, markT, log);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(OutRoot + "/logs");
            File.WriteAllText(OutRoot + "/logs/rooms-build.txt", log.ToString());
            Debug.Log("[IronCitadelRooms] saved " + ScenePath + "\n" + log);
            return root;
        }

        /// <summary>Some room pieces ship without a collider (seen: the floor tile inside the entry's south
        /// door), so a walker would drop through. Give every floor piece without one a box, and every
        /// wall piece without one a mesh collider, on the scene instance only; the log lists them.</summary>
        static void PatchMissingColliders(Dictionary<string, GameObject> rooms, StringBuilder log)
        {
            var found = new SortedDictionary<string, int>();
            foreach (var kv in rooms)
                foreach (var group in kv.Value.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Floor" || t.name == "Walls"))
                    foreach (var mf in group.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                        if (mf.GetComponentInParent<Collider>() != null && mf.GetComponentInParent<Collider>().transform != mf.transform) continue;
                        if (group.name == "Floor")
                        {
                            var bc = mf.gameObject.AddComponent<BoxCollider>(); // fits the mesh bounds
                            if (bc.size.y < 0.1f) { bc.center -= new Vector3(0f, (0.2f - bc.size.y) / 2f, 0f); bc.size = new Vector3(bc.size.x, 0.2f, bc.size.z); }
                        }
                        else mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                        string key = $"{kv.Key}/{group.parent.name}/{group.name}/{mf.name}";
                        found[key] = found.TryGetValue(key, out var n) ? n + 1 : 1;
                    }
            log.AppendLine(found.Count == 0 ? "colliders: every floor and wall piece has one" : $"colliders added where the prefab had none ({found.Values.Sum()}):");
            foreach (var kv in found) log.AppendLine($"  {kv.Key}{(kv.Value > 1 ? " x" + kv.Value : "")}");
        }

        /// <summary>The treasury prefab ships some SM_Prop_Door_Metal_* leaves shut across their SM_Env_Door_Frame,
        /// which cuts the vault's only route inside the room (round 1: treasury/Walls at x 12, z -105). Every leaf in
        /// the vault cell ends up open. A leaf at right angles to its frame (the nearest within 2 m) already stands
        /// open and is left as shipped. A leaf lying across it is swung 90 degrees about a hinge on one end and one
        /// face, so it lies against the frame's edge like a door left open. Of the four poses (either end, either
        /// face) the one whose swept leaf touches the fewest colliders wins (its own, floors, ceilings and door
        /// frames aside), then the one leaving the widest straight lane for the walker's capsule through the frame.
        /// The log lists each leaf, its pose and its lane.</summary>
        static void OpenVaultDoors(Level level, Dictionary<string, GameObject> rooms, StringBuilder log)
        {
            var vault = level.rooms.FirstOrDefault(r => r.kind == "V");
            if (vault == null || !rooms.TryGetValue(vault.id, out var room)) return;
            float x0 = Tile * vault.c, x1 = x0 + Tile, z1 = -Tile * vault.r, z0 = z1 - Tile;
            var leaves = room.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("SM_Prop_Door_Metal", StringComparison.Ordinal))
                .ToList();
            // a nested leaf (a child mesh named like its parent) moves with its parent
            leaves = leaves.Where(t => !leaves.Any(o => o != t && t.IsChildOf(o))).ToList();
            var frames = room.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("SM_Env_Door_Frame", StringComparison.Ordinal) && t.GetComponent<MeshFilter>() != null).ToList();
            int opened = 0, shipped = 0;
            foreach (var leaf in leaves)
            {
                var wp = leaf.position;
                if (wp.x < x0 || wp.x > x1 || wp.z < z0 || wp.z > z1) continue;
                // the leaf's box in its own space, from its meshes
                var mfs = leaf.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).ToList();
                if (mfs.Count == 0) { log.AppendLine($"vault door {PathOf(leaf)}: no mesh, left as is"); continue; }
                Bounds lb = default; bool first = true;
                foreach (var mf in mfs)
                {
                    var mb = mf.sharedMesh.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                        var lp = leaf.InverseTransformPoint(mf.transform.TransformPoint(corner));
                        if (first) { lb = new Bounds(lp, Vector3.zero); first = false; } else lb.Encapsulate(lp);
                    }
                }
                bool wideX = lb.size.x * Mathf.Abs(leaf.lossyScale.x) >= lb.size.z * Mathf.Abs(leaf.lossyScale.z);
                var wDir = (wideX ? leaf.right : leaf.forward).normalized;   // along the leaf
                var tDir = (wideX ? leaf.forward : leaf.right).normalized;   // through the leaf
                float wMin = wideX ? lb.min.x : lb.min.z, wMax = wideX ? lb.max.x : lb.max.z;
                float tMin = wideX ? lb.min.z : lb.min.x, tMax = wideX ? lb.max.z : lb.max.x;
                var centreW = leaf.TransformPoint(lb.center);
                var sizeW = Vector3.Scale(lb.size, new Vector3(Mathf.Abs(leaf.lossyScale.x), Mathf.Abs(leaf.lossyScale.y), Mathf.Abs(leaf.lossyScale.z)));
                float bottom = centreW.y - sizeW.y / 2f, top = centreW.y + sizeW.y / 2f;
                float pivotEnd = Mathf.Abs(wMin) <= Mathf.Abs(wMax) ? wMin : wMax;   // Synty leaves usually pivot on the hinge

                // its frame: the nearest SM_Env_Door_Frame within 2 m. A leaf lying across its frame is shut; a leaf
                // at right angles to it already stands open (the prefab ships some that way) and is left as shipped.
                Transform frame = null; Bounds fb = default; float fd = 2f;
                foreach (var f in frames)
                {
                    var b = RendererBounds(f);
                    float d = Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(centreW.x, centreW.z));
                    if (d < fd) { fd = d; frame = f; fb = b; }
                }
                var fAlong = frame == null ? wDir : (fb.size.x >= fb.size.z ? Vector3.right : Vector3.forward);
                var fN = frame == null ? tDir : (fb.size.x >= fb.size.z ? Vector3.forward : Vector3.right);
                var fCentre = frame == null ? centreW : fb.center;
                // the widest straight lane the walker's capsule finds through the frame, leaf as it stands
                float Lane() => LaneWidth(new Vector3(fCentre.x, bottom, fCentre.z), fAlong, fN, bottom);
                leaf.gameObject.SetActive(false); Physics.SyncTransforms();
                float frameLane = Lane();
                leaf.gameObject.SetActive(true); Physics.SyncTransforms();
                float shippedLane = Lane();
                string frameText = frame == null ? "no frame within 2 m" : $"frame {frame.name} at ({fb.center.x:0.0}, {fb.center.z:0.0})";
                if (frame != null && Mathf.Abs(Vector3.Dot(wDir, fAlong)) < 0.7f)
                {
                    shipped++;
                    log.AppendLine($"vault door {PathOf(leaf)} at ({wp.x:0.0}, {wp.z:0.0}): already open as shipped (at right angles to its {frameText}), left as is; " +
                                   $"capsule lane {shippedLane:0.00} m (the frame alone {frameLane:0.00} m)");
                    continue;
                }
                var poses = new List<(float score, int hits, float free, float lane, Vector3 hinge, float angle, string what, List<string> blockers)>();
                foreach (float wEnd in new[] { wMin, wMax })
                foreach (float tFace in new[] { tMin, tMax })
                {
                    var hingeL = wideX ? new Vector3(wEnd, 0f, tFace) : new Vector3(tFace, 0f, wEnd);
                    var hinge = leaf.TransformPoint(hingeL); hinge.y = centreW.y;
                    var faceSide = (tFace == tMax ? 1f : -1f) * tDir;        // the side this pose swings toward
                    var toFree = (wEnd == wMin ? 1f : -1f) * wDir;           // hinge -> free end
                    float angle = Vector3.SignedAngle(toFree, faceSide, Vector3.up);   // +-90
                    var rot = Quaternion.AngleAxis(angle, Vector3.up);
                    var c = hinge + rot * (centreW - hinge);
                    var q = rot * leaf.rotation;
                    // the swept leaf, a little smaller, with the 0.15 m at the hinge left out
                    var half = sizeW / 2f - new Vector3(0.03f, 0.03f, 0.03f);
                    if (wideX) half.x -= 0.075f; else half.z -= 0.075f;
                    c += rot * toFree * 0.075f;
                    half.y = Mathf.Max(0.05f, half.y - 0.1f);
                    var blockers = Physics.OverlapBox(c, half, q, AllLayers, QueryTriggerInteraction.Ignore)
                        .Where(col => !col.transform.IsChildOf(leaf))
                        .Where(col => col.bounds.max.y > bottom + 0.15f && col.bounds.min.y < top - 0.1f)
                        .Where(col => col.name.IndexOf("Door_Frame", StringComparison.Ordinal) < 0)
                        .Select(col => PathOf(col.transform)).Distinct().ToList();
                    // free space on the side it swings to, from the doorway out
                    var doorway = new Vector3(centreW.x, bottom, centreW.z);
                    float room2 = FreeRun(doorway + faceSide * 0.3f, faceSide, 6f);
                    bool outside = c.x < x0 || c.x > x1 || c.z < z0 || c.z > z1;
                    var keepPos = leaf.position; var keepRot = leaf.rotation;
                    leaf.RotateAround(hinge, Vector3.up, angle); Physics.SyncTransforms();
                    float lane = Lane();
                    leaf.SetPositionAndRotation(keepPos, keepRot); Physics.SyncTransforms();
                    float score = blockers.Count * 100f + (outside ? 1000f : 0f) - 10f * lane - Mathf.Min(room2, 4f) * 0.1f + (wEnd == pivotEnd ? 0f : 0.2f);
                    poses.Add((score, blockers.Count, room2, lane, hinge, angle, $"hinge at the {(wEnd == wMin ? "min" : "max")} end, {(tFace == tMax ? "+" : "-")} face", blockers));
                }
                var best = poses.OrderBy(p => p.score).First();
                leaf.RotateAround(best.hinge, Vector3.up, best.angle);
                Physics.SyncTransforms();
                opened++;
                log.AppendLine($"vault door {PathOf(leaf)} at ({wp.x:0.0}, {wp.z:0.0}), leaf {sizeW.x:0.00} x {sizeW.y:0.00} x {sizeW.z:0.00}, shut across its {frameText}: opened {best.angle:0} deg, {best.what}, " +
                               $"swept leaf touches {best.hits}{(best.hits > 0 ? " (" + string.Join(", ", best.blockers.Take(3)) + ")" : "")}, free run {best.free:0.0} m, " +
                               $"capsule lane {best.lane:0.00} m (shut {shippedLane:0.00} m, the frame alone {frameLane:0.00} m); " +
                               "other poses " + string.Join(" / ", poses.Where(p => p.what != best.what).Select(p => $"{p.what}: {p.hits} hit(s), {p.free:0.0} m, lane {p.lane:0.00} m")));
            }
            log.AppendLine($"vault doors: {opened} swung open, {shipped} already open as shipped, of {leaves.Count} SM_Prop_Door_Metal leaves in the vault prefab");
        }

        /// <summary>The throne room prefab (ThroneRoomAisledCourt) stands a stone lantern on the floor at the foot of the
        /// stage's steps, before their west half, which cut the steps off there for the walker (round 2: conformance
        /// check 8 found 75% of the front stepped against 80%). Every Furniture or Clutter prop rising above 0.3 m that
        /// stands nearer than StageFootBand to the foot of the steps, and overlaps them more than StageEndKeep in from
        /// either end, is moved straight back from the steps, toward the door, until it stands StageFootClear off the
        /// foot, then on in 0.25 m steps (up to 6 m) while it would overlap another collider. StageFootClear is just
        /// past the brazier the prefab already stands 2.69 m off the foot, which check 8's probe clears. The props that
        /// flank the two ends of the steps (the knight stands and the nave's side rows) stay. The scene copy only; the
        /// log lists each move ("stage front ...").</summary>
        const float StageFootBand = 2f, StageFootClear = 2.75f, StageEndKeep = 1.1f;
        static void ClearStageFront(Level level, Dictionary<string, GameObject> rooms, StringBuilder log)
        {
            var throne = level.rooms.FirstOrDefault(r => r.kind == "T");
            if (throne == null || !rooms.TryGetValue(throne.id, out var room)) return;
            var steps = room.GetComponentsInChildren<Collider>(true)
                .Where(c => c.name.StartsWith("SM_", StringComparison.Ordinal) && c.name.IndexOf("Stairs", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (steps.Count == 0) { log.AppendLine("stage front: no steps found in " + throne.id); return; }
            var sb = steps[0].bounds;
            foreach (var c in steps) sb.Encapsulate(c.bounds);
            var toDoor = DoorDirection(level, throne.id);              // flat unit vector, from the stage toward the door
            var along = Vector3.Cross(Vector3.up, toDoor);
            float D(Vector3 p) => Vector3.Dot(p, toDoor);
            float A(Vector3 p) => Vector3.Dot(p, along);
            (float lo, float hi) Span(Bounds b, Func<Vector3, float> f)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var p = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    lo = Mathf.Min(lo, f(p)); hi = Mathf.Max(hi, f(p));
                }
                return (lo, hi);
            }
            var (_, foot) = Span(sb, D);
            var (a0, a1) = Span(sb, A);
            int moved = 0;
            foreach (var group in room.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Furniture" || t.name == "Clutter").ToList())
                foreach (Transform prop in group)
                {
                    var cols = prop.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger).ToList();
                    if (cols.Count == 0) continue;
                    var pb = cols[0].bounds;
                    foreach (var c in cols) pb.Encapsulate(c.bounds);
                    if (pb.max.y < 0.3f) continue;                    // rugs and runners
                    var (d0, d1) = Span(pb, D);
                    var (p0, p1) = Span(pb, A);
                    if (d1 <= foot - 0.25f || d0 >= foot + StageFootBand) continue;
                    if (p1 <= a0 + StageEndKeep || p0 >= a1 - StageEndKeep) continue;
                    float shift = foot + StageFootClear - d0;
                    // the prop's box from 0.3 m up (clear of the 0.1 m floor tiles and the runners), less 0.02 m a side
                    string blocker = null;
                    bool Clear(float s)
                    {
                        var c = pb.center + toDoor * s;
                        var half = new Vector3(pb.extents.x - 0.02f, (pb.size.y - 0.3f) * 0.5f, pb.extents.z - 0.02f);
                        c.y = pb.min.y + 0.3f + half.y;
                        var hit = Physics.OverlapBox(c, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                            .FirstOrDefault(h => !h.transform.IsChildOf(prop));
                        if (hit != null && blocker == null) blocker = PathOf(hit.transform);
                        return hit == null;
                    }
                    while (shift < 6f && !Clear(shift)) shift += 0.25f;
                    if (blocker != null) log.AppendLine($"stage front: {prop.name} would overlap {blocker} at {StageFootClear:0.00} m; stepped on to {shift + d0 - foot:0.00} m");
                    var from = prop.position;
                    prop.position += toDoor * shift;
                    Physics.SyncTransforms();
                    moved++;
                    log.AppendLine($"stage front: moved {PathOf(prop)} {shift:0.00} m toward the door, {from.ToString("F2")} -> {prop.position.ToString("F2")} (it stood {d0 - foot:+0.00;-0.00} m off the foot of the steps)");
                }
            log.AppendLine($"stage front: steps x/z {sb.min.x:0.0}..{sb.max.x:0.0} / {sb.min.z:0.0}..{sb.max.z:0.0}; {moved} prop(s) moved off the foot of the steps");
        }

        /// <summary>level.json gives every room a height, and check 9 wants a low room's floor mostly under 6.5 m. No
        /// catalog kitchen is low and walkable with two adjacent doors (logs/rooms-picks-trial.txt): the Notch kitchen's
        /// hearth hall has a 10 m lid. So in every room level.json calls low, each Ceiling piece whose underside is more
        /// than 0.5 m above LowLid drops by the whole 5 m bands between them, and so does every Walls piece hung within
        /// LidHang under a dropped piece (none in the Notch kitchen). The Walls pieces that then stand wholly above the
        /// lid (the upper band, from about 5 m up: 29 in the Notch kitchen, its beam screen's upper half among them) are
        /// switched off. Lids already at 5 m are left as shipped. The scene copy only; the
        /// log lists what moved, what was switched off and any other piece that crosses the new lid ("low lid ...").
        /// Since PrefabSynthesis PR #208 the Notch kitchen ships low (ceiling 'low_course'), so this is a guard that
        /// changes nothing on the current picks.</summary>
        const float LowLid = 5f, LidHang = 2.5f;

        static void CapLowLids(Level level, Dictionary<string, GameObject> rooms, StringBuilder log)
        {
            List<Transform> Pieces(GameObject room, string group) =>
                room.GetComponentsInChildren<Transform>(true).Where(t => t.name == group).SelectMany(g => g.Cast<Transform>()).ToList();
            foreach (var r in level.rooms.Where(x => x.height == "low"))
            {
                if (!rooms.TryGetValue(r.id, out var room)) continue;
                var dropped = new List<(Bounds b, float by)>();
                foreach (var p in Pieces(room, "Ceiling"))
                {
                    var b = RendererBounds(p);
                    float by = 5f * Mathf.Round((b.min.y - LowLid) / 5f);
                    if (b.min.y <= LowLid + 0.5f || by <= 0f) continue;
                    p.position += Vector3.down * by;
                    dropped.Add((b, by));
                }
                int hung = 0, off = 0;
                var crossing = new List<string>();
                foreach (var p in Pieces(room, "Walls"))
                {
                    var b = RendererBounds(p);
                    if (b.size == Vector3.zero) continue;
                    var lid = dropped.Where(d => b.min.y >= d.b.min.y - LidHang && b.max.y <= d.b.max.y + 0.25f &&
                                                 b.min.x < d.b.max.x + 0.5f && b.max.x > d.b.min.x - 0.5f &&
                                                 b.min.z < d.b.max.z + 0.5f && b.max.z > d.b.min.z - 0.5f).ToList();
                    if (lid.Count > 0) { p.position += Vector3.down * lid[0].by; hung++; }
                    else if (dropped.Count > 0 && b.min.y >= LowLid - 0.25f) { p.gameObject.SetActive(false); off++; }
                    else if (dropped.Count > 0 && b.max.y > LowLid + 0.5f) crossing.Add($"{PathOf(p)} (y {b.min.y:0.00}..{b.max.y:0.00})");
                }
                foreach (var g in new[] { "Wall props", "Furniture", "Clutter" })
                    foreach (var p in Pieces(room, g))
                    {
                        var b = RendererBounds(p);
                        if (dropped.Count > 0 && b.size != Vector3.zero && b.max.y > LowLid + 0.25f) crossing.Add($"{PathOf(p)} (y {b.min.y:0.00}..{b.max.y:0.00})");
                    }
                log.AppendLine(dropped.Count == 0
                    ? $"low lid: {r.id} already at {LowLid} m or under; left as shipped"
                    : $"low lid: {r.id}: {dropped.Count} ceiling piece(s) dropped by {string.Join("/", dropped.Select(d => d.by).Distinct())} m, {hung} hung piece(s) with them, " +
                      $"{off} wall piece(s) above the new lid switched off; crossing it: {(crossing.Count == 0 ? "none" : string.Join(", ", crossing))}");
            }
        }

        /// <summary>The hall_gallery prefab (HallGalleryBackFeast) meets its two stair shafts unevenly. The east landing's
        /// edge to its shaft is an SM_Env_Wall_07 stub over the inner half, with the gap by the outer wall; the west one is
        /// a pair of round door frames with the gap in the middle, 6.7 to 8.3 m in from the west wall. So the walker's path
        /// from the west door to the gallery runs more than 7 m in at floor level, and check 7 counts that as the hall floor
        /// (logs/rooms-hall-probe.txt), though the path never leaves the side hall and the shaft. The east stub is mirrored
        /// onto the west edge (x to -x in the hall's own frame) and the frame pair switched off. The scene copy only; the
        /// source fix is PrefabSynthesis PR #207 (fam_gallery_b arches both landing mouths, mirrored); on the rebuilt hall
        /// no frame pair is found and nothing changes. The log says what changed ("hall landing ...").</summary>
        static void MirrorHallLandingStub(Level level, Dictionary<string, GameObject> rooms, StringBuilder log)
        {
            var hallRoom = level.rooms.FirstOrDefault(r => r.kind == "D");
            if (hallRoom == null || !rooms.TryGetValue(hallRoom.id, out var hallGo)) return;
            var H = hallGo.transform;
            Vector3 Local(Bounds b) => H.InverseTransformPoint(b.center);
            var under = H.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Walls" && t.parent != null && t.parent.name == "under")
                .SelectMany(g => g.Cast<Transform>()).ToList();
            foreach (var stub in under.Where(t => t.name.StartsWith("SM_Env_Wall_07", StringComparison.Ordinal)).ToList())
            {
                var sc = Local(RendererBounds(stub));
                var frames = under.Where(t => t.name.StartsWith("SM_Env_Wall_DoorFrame_Round", StringComparison.Ordinal))
                    .Where(t => { var fc = Local(RendererBounds(t)); return Mathf.Abs(fc.x + sc.x) < 0.3f && Mathf.Abs(fc.z - sc.z) < 0.3f; }).ToList();
                if (frames.Count == 0) continue;
                var copy = Object.Instantiate(stub.gameObject, stub.parent);   // a plain clone of the piece, not its prefab
                copy.name = stub.name;
                var lp = H.InverseTransformPoint(stub.position);
                float yaw = (Quaternion.Inverse(H.rotation) * stub.rotation).eulerAngles.y;
                copy.transform.position = H.TransformPoint(new Vector3(-lp.x, lp.y, lp.z));
                copy.transform.rotation = H.rotation * Quaternion.Euler(0f, -yaw, 0f);
                var s = stub.localScale;
                copy.transform.localScale = new Vector3(-s.x, s.y, s.z);
                foreach (var f in frames) f.gameObject.SetActive(false);
                Physics.SyncTransforms();
                var sb = RendererBounds(stub);
                var cb = RendererBounds(copy.transform);
                log.AppendLine($"hall landing: mirrored {PathOf(stub)} (x {sb.min.x:0.00}..{sb.max.x:0.00}) to x {cb.min.x:0.00}..{cb.max.x:0.00}, " +
                               $"z {cb.min.z:0.00}..{cb.max.z:0.00}; switched off {frames.Count} round door frame(s) there");
            }
        }

        static Bounds RendererBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var x in rs) b.Encapsulate(x.bounds);
            return b;
        }

        /// <summary>The widest run of positions along a doorway (every 0.05 m within 1.2 m of its centre; 0.05 m each) where the
        /// walker's capsule (r 0.35, h 1.8) fits at the doorway and 0.35, 0.7 and 1.05 m either side of it: a
        /// straight lane through. 0 means the capsule cannot pass straight through anywhere.</summary>
        static float LaneWidth(Vector3 centre, Vector3 along, Vector3 n, float floorY)
        {
            const float r = 0.35f, h = 1.8f;
            int best = 0, run = 0;
            for (int k = -24; k <= 24; k++)
            {
                bool free = true;
                for (int o = -3; o <= 3 && free; o++)
                {
                    var foot = centre + along * (0.05f * k) + n * (0.35f * o);
                    foot.y = floorY;
                    free = !Physics.OverlapCapsule(foot + Vector3.up * (0.04f + r), foot + Vector3.up * (h - r), r - 0.01f, AllLayers, QueryTriggerInteraction.Ignore)
                        .Any(c => c.bounds.max.y > floorY + 0.05f);
                }
                run = free ? run + 1 : 0;
                best = Mathf.Max(best, run);
            }
            return best * 0.05f;
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>The night fill: three dim, cool directional lights without shadows, 120 degrees apart,
        /// so every floor and wall gets some light under the rooms' own torches. Unity 6 here does not
        /// use the flat ambient colour (the project's URP asset samples probe volumes, and this scene
        /// has none), so the fill does the ambient's job; it works the same in edit and play mode.</summary>
        static void AddNightFill(Transform parent)
        {
            for (int k = 0; k < 3; k++)
            {
                var go = new GameObject($"Night fill {k + 1}");
                go.transform.SetParent(parent, false);
                go.transform.rotation = Quaternion.Euler(40f, 30f + 120f * k, 0f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = NightFillColor;
                l.intensity = NightFillIntensity;
                l.shadows = LightShadows.None;
            }
        }

        public static void ApplyNightLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = NightAmbient;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0.3f;
        }

        // ------------------------------------------------------------------ geometry probes

        static readonly float[] Heights = { 0.5f, 1.2f, 1.8f };
        const int AllLayers = ~0;

        static bool SegmentHits(Vector3 a, Vector3 b)
        {
            var d = b - a; float len = d.magnitude;
            if (len < 1e-4f) return false;
            d /= len;
            return Physics.Raycast(a, d, len, AllLayers, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(b, -d, len, AllLayers, QueryTriggerInteraction.Ignore);
        }

        static bool CrossClear(Vector3 a, Vector3 b)
        {
            foreach (var h in Heights)
                if (SegmentHits(a + Vector3.up * h, b + Vector3.up * h)) return false;
            return true;
        }

        /// <summary>Floor under a point: a ray down from just under the lowest lid (3.3 m).</summary>
        public static bool FloorAt(Vector3 p, out float y, float top = 3.3f)
        {
            y = 0f;
            if (Physics.Raycast(new Vector3(p.x, top, p.z), Vector3.down, out var hit, top + 2f, AllLayers, QueryTriggerInteraction.Ignore))
            { y = hit.point.y; return true; }
            return false;
        }

        /// <summary>The clear doorway across an edge nearest its middle: (offset of its centre along the
        /// edge, width, height of the lintel above the floor).</summary>
        static readonly Dictionary<string, Vector3> s_openings = new Dictionary<string, Vector3>();

        /// <summary><see cref="MeasureOpeningRaw"/>, remembered per edge for one build, so a feature placed in
        /// a doorway does not change what later steps measure there.</summary>
        public static bool MeasureOpening(Edge e, out float tc, out float width, out float height)
        {
            string k = e.centre.ToString("F2");
            if (!s_openings.TryGetValue(k, out var v))
            {
                v = MeasureOpeningRaw(e, out var a, out var b, out var c) ? new Vector3(a, b, c) : new Vector3(float.NaN, 0f, 0f);
                s_openings[k] = v;
            }
            tc = v.x; width = v.y; height = v.z;
            return !float.IsNaN(v.x);
        }

        public static bool MeasureOpeningRaw(Edge e, out float tc, out float width, out float height)
        {
            float step = 0.05f; var runs = new List<Vector2>(); float? s = null;
            for (float t = -5f; t <= 5f + 1e-3f; t += step)
            {
                var p = e.centre + e.along * t;
                bool clear = !SegmentHits(p - e.n * 0.6f + Vector3.up * 1.2f, p + e.n * 0.6f + Vector3.up * 1.2f)
                             && !SegmentHits(p - e.n * 0.6f + Vector3.up * 0.4f, p + e.n * 0.6f + Vector3.up * 0.4f);
                if (clear && s == null) s = t;
                if (!clear && s != null) { runs.Add(new Vector2(s.Value, t - step)); s = null; }
            }
            if (s != null) runs.Add(new Vector2(s.Value, 5f));
            tc = 0; width = 0; height = 0;
            if (runs.Count == 0) return false;
            var best = runs.OrderBy(r => Mathf.Abs((r.x + r.y) / 2f)).First();
            tc = (best.x + best.y) / 2f; width = best.y - best.x + step;
            var mid = e.centre + e.along * tc + Vector3.up * 0.5f;
            height = Physics.Raycast(mid, Vector3.up, out var hit, 8f, AllLayers, QueryTriggerInteraction.Ignore) ? 0.5f + hit.distance : 4f;
            return true;
        }

        /// <summary>Distance from the edge line, into the room on the -n side, of the wall's inner face
        /// at offset t along the edge.</summary>
        static float WallFaceInset(Edge e, float t)
        {
            var a = e.centre + e.along * t - e.n * 3f + Vector3.up * 1.2f;
            return Physics.Raycast(a, e.n, out var hit, 4f, AllLayers, QueryTriggerInteraction.Ignore) ? 3f - hit.distance : 0.27f;
        }

        /// <summary>Free run along a wall from a point 0.6 m off its face, in direction dir (m).</summary>
        static float FreeRun(Vector3 from, Vector3 dir, float max = 12f)
        {
            float best = max;
            foreach (var h in new[] { 0.6f, 1.6f })
                if (Physics.Raycast(from + Vector3.up * h, dir, out var hit, max, AllLayers, QueryTriggerInteraction.Ignore))
                    best = Mathf.Min(best, hit.distance);
            return best;
        }

        static GameObject Spawn(string path, Transform parent, string name = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new Exception("missing prefab " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            if (name != null) go.name = name;
            return go;
        }

        /// <summary>Renderers that draw: active, enabled, and not particles or trails (an inactive renderer
        /// reports empty bounds at the world origin).</summary>
        static Renderer[] Drawn(Transform t) => t.GetComponentsInChildren<Renderer>(false)
            .Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();

        static Bounds RendererBounds(GameObject go)
        {
            var rs = Drawn(go.transform);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ features

        static void BuildPortcullis(Level level, Door door, Transform featT, StringBuilder log)
        {
            // seen from the entry side (the cell that is not the armory), n points into the armory
            var entry = level.RoomAt(door.r1, door.c1);
            var e = entry.id == "armory" ? EdgeBetween(door.r2, door.c2, door.r1, door.c1) : EdgeBetween(door.r1, door.c1, door.r2, door.c2);
            if (!MeasureOpening(e, out var tc, out var w, out var h)) { log.AppendLine($"{door.id}: NO OPENING FOUND at {e}"); return; }
            log.AppendLine($"{door.id}: opening at {e} centre t {tc:0.00} width {w:0.00} height {h:0.00}");
            var centre = e.centre + e.along * tc;

            var gate = new GameObject(door.id);
            gate.transform.SetParent(featT, false);
            gate.transform.position = centre;
            gate.transform.rotation = Quaternion.LookRotation(-e.n, Vector3.up); // +z faces the entry side
            var mk = gate.AddComponent<IronCitadelMarker>();
            mk.kind = IronCitadelMarker.Kind.Feature; mk.room = door.id; mk.note = "lowered portcullis, impassable; spikes on the entry side";

            // the portcullis: 4.52 x 5.67 x 0.3 m, pivot at its centre; fit it to the arch (its sides and top
            // run into the frame)
            var pc = Spawn(PortcullisPrefab, gate.transform, "Portcullis (lowered)");
            float sy = (h + 0.25f) / 5.669f;
            float sx = Mathf.Max(sy, (w + 0.4f) / 4.521f);
            pc.transform.localScale = new Vector3(sx, sy, 1f);
            pc.transform.localRotation = Quaternion.identity;
            pc.transform.localPosition = new Vector3(0f, 5.669f * sy / 2f, 0f);

            // a solid blocker over the whole doorway, whatever the bars' collider does
            var blk = new GameObject("GateBlocker");
            blk.transform.SetParent(gate.transform, false);
            var bc = blk.AddComponent<BoxCollider>();
            bc.size = new Vector3(w + 0.6f, h + 0.2f, 0.5f);
            bc.center = new Vector3(0f, (h + 0.2f) / 2f, 0f);

            // a row of goblin spikes 1.7 m in front of it, on the entry side; side pieces only where they fit
            var row = new GameObject("Spikes (entry side)");
            row.transform.SetParent(gate.transform, false);
            row.transform.localPosition = new Vector3(0f, 0f, 1.8f);
            var mid = Spawn(SpikesWide, row.transform, "Spikes");
            mid.transform.localPosition = Vector3.zero;
            mid.transform.localRotation = Quaternion.identity;
            int sides = 0;
            foreach (var sgn in new[] { -1f, 1f })
            {
                var local = new Vector3(sgn * 3.9f, 0f, 0f);
                var world = row.transform.TransformPoint(local);
                var half = new Vector3(1.7f, 1.0f, 0.55f);
                if (Physics.CheckBox(world + Vector3.up * 1.3f, half, row.transform.rotation, AllLayers, QueryTriggerInteraction.Ignore)) continue;
                var sp = Spawn(SpikesNarrow, row.transform, "Spikes");
                sp.transform.localPosition = local;
                sp.transform.localRotation = Quaternion.identity;
                sides++;
            }
            log.AppendLine($"{door.id}: portcullis scale ({sx:0.00}, {sy:0.00}), spikes 1 + {sides} side pieces");
        }

        static void BuildSecretBookcase(Level level, Door door, Transform featT, StringBuilder log)
        {
            // the bookcase stands on the prison's side (the first cell of `between`), n points to the vault
            var e = EdgeBetween(door.r1, door.c1, door.r2, door.c2);
            if (!MeasureOpening(e, out var tc, out var w, out var h)) { log.AppendLine($"{door.id}: NO OPENING FOUND at {e}"); return; }
            float inset = WallFaceInset(e, tc + w / 2f + 0.8f);
            log.AppendLine($"{door.id}: opening at {e} centre t {tc:0.00} width {w:0.00} height {h:0.00}; wall face {inset:0.00} m inside the prison");
            var into = -e.n;                                  // into the prison
            var face = e.centre + e.along * tc + into * (inset + 0.03f);

            // which way along the wall has room for the decoys; the hinge goes on the other side
            var probe = face + into * 0.6f;
            float runPlus = FreeRun(probe, e.along), runMinus = FreeRun(probe, -e.along);
            var toDecoys = runPlus >= runMinus ? e.along : -e.along;

            // bookcase: 2.13 wide x 3.35 tall x 0.89 deep, front +z; stretch it to cover the arch
            float sx = Mathf.Max(1f, (w + 0.35f) / 2.13f);
            float sy = Mathf.Max(1f, (h + 0.15f) / 3.351f);
            float leafW = 2.13f * sx;

            // vault_door (marker) > SecretDoor (SecretBookcaseDoor) > Hinge > leaf + blocker; the decoys sit
            // under a sibling, so using a decoy does not open the door
            var doorRoot = new GameObject(door.id);
            doorRoot.transform.SetParent(featT, false);
            doorRoot.transform.position = face;
            var mk = doorRoot.AddComponent<IronCitadelMarker>();
            mk.kind = IronCitadelMarker.Kind.Feature; mk.room = door.id; mk.note = "secret bookcase door: use it (E) to swing it aside";
            var secret = new GameObject("SecretDoor");
            secret.transform.SetParent(doorRoot.transform, false);

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(secret.transform, false);
            hinge.position = face - toDecoys * (leafW / 2f);
            hinge.rotation = Quaternion.identity;

            PlaceBookcase(hinge, "Bookcase (secret door)", face, into, e.along, sx, sy);
            var blk = new GameObject("DoorBlocker");
            blk.transform.SetParent(hinge, true);
            blk.transform.position = face + into * 0.45f;
            blk.transform.rotation = Quaternion.LookRotation(into, Vector3.up);
            var bc = blk.AddComponent<BoxCollider>();
            bc.size = new Vector3(Mathf.Max(leafW, w + 0.3f), h + 0.1f, 0.8f);
            bc.center = new Vector3(0f, bc.size.y / 2f, 0f);

            var sbd = secret.AddComponent<SecretBookcaseDoor>();
            sbd.hinge = hinge;
            sbd.duration = 1f;
            // the free end (toward the decoys) swings into the prison
            sbd.openAngle = Vector3.SignedAngle(toDecoys, into, Vector3.up) >= 0f ? 90f : -90f;

            // decoys beside it, the same model and stretch, while they fit (the run is from the door's centre)
            var decoys = new GameObject("Decoys").transform;
            decoys.SetParent(doorRoot.transform, false);
            float run = toDecoys == e.along ? runPlus : runMinus;
            int placed = 0;
            for (int k = 1; k <= 2; k++)
            {
                float off = k * (leafW + 0.04f);
                if (off + leafW / 2f > run - 0.1f) break;
                PlaceBookcase(decoys, $"Bookcase {k}", face + toDecoys * off, into, e.along, sx, sy);
                placed++;
            }
            log.AppendLine($"{door.id}: leaf {leafW:0.00} m (x{sx:0.00}, y{sy:0.00}), hinge on the {(toDecoys == e.along ? "-" : "+")}along side, open {sbd.openAngle:0}, decoys {placed} (free run {runPlus:0.0}/{runMinus:0.0} m)");
        }

        /// <summary>A bookcase standing on the floor with its back on the wall face at <paramref name="face"/>,
        /// its front toward <paramref name="into"/>, centred on <paramref name="face"/> along the wall.</summary>
        static GameObject PlaceBookcase(Transform parent, string name, Vector3 face, Vector3 into, Vector3 along, float sx, float sy)
        {
            var go = Spawn(BookcasePrefab, parent, name);
            go.transform.rotation = Quaternion.Euler(0f, Yaw(into), 0f);
            go.transform.localScale = new Vector3(sx, sy, 1f);
            go.transform.position = face;
            var b = RendererBounds(go);
            // back onto the face, centred along the wall, feet on the floor
            float back = Mathf.Min(Vector3.Dot(b.min, into), Vector3.Dot(b.max, into));
            float shiftIn = Vector3.Dot(face, into) - back;
            float shiftAlong = Vector3.Dot(face - b.center, along);
            go.transform.position += into * shiftIn + along * shiftAlong + Vector3.up * (face.y - b.min.y);
            return go;
        }

        static void BuildOuterDoor(Level level, Door door, Transform featT, Transform markT, StringBuilder log)
        {
            var e = EdgeOfDoor(door); // n points outside
            if (!MeasureOpening(e, out var tc, out var w, out var h)) { log.AppendLine($"{door.id}: NO OPENING FOUND at {e}"); return; }
            log.AppendLine($"{door.id}: opening at {e} centre t {tc:0.00} width {w:0.00} height {h:0.00}");
            var centre = e.centre + e.along * tc;

            // closed behind the player: the level has no outside to walk into
            var root = new GameObject(door.id);
            root.transform.SetParent(featT, false);
            root.transform.position = centre;
            root.transform.rotation = Quaternion.LookRotation(-e.n, Vector3.up);
            var mk = root.AddComponent<IronCitadelMarker>();
            mk.kind = IronCitadelMarker.Kind.Feature; mk.room = door.id; mk.note = "the way in; shut behind the player (outside is not modelled)";
            var leaf = Spawn(OuterDoorPrefab, root.transform, "Door (shut)");
            leaf.transform.localRotation = Quaternion.identity;
            leaf.transform.localPosition = Vector3.zero;
            leaf.transform.localScale = Vector3.one;
            var b = RendererBounds(leaf);
            bool wideX = b.size.x >= b.size.z;
            if (!wideX) { leaf.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); b = RendererBounds(leaf); }
            float bw = Mathf.Max(b.size.x, b.size.z);
            float s = Mathf.Max((w + 0.3f) / bw, (h + 0.1f) / Mathf.Max(0.1f, b.size.y));
            leaf.transform.localScale = Vector3.one * s;
            b = RendererBounds(leaf);
            // sit it on the floor, centred in the arch, on the edge line (inside the wall's thickness)
            leaf.transform.position += new Vector3(centre.x - b.center.x, -b.min.y, centre.z - b.center.z) + e.n * 0.05f;
            var blk = new GameObject("DoorBlocker");
            blk.transform.SetParent(root.transform, false);
            blk.transform.localPosition = Vector3.zero;
            var bc = blk.AddComponent<BoxCollider>();
            bc.size = new Vector3(w + 0.6f, h + 0.2f, 0.4f);
            bc.center = new Vector3(0f, (h + 0.2f) / 2f, 0f);
            log.AppendLine($"{door.id}: shut with {Path.GetFileNameWithoutExtension(OuterDoorPrefab)} x{s:0.00}");
        }

        // ------------------------------------------------------------------ markers

        static Bounds MainGroupBounds(GameObject room, out Transform main)
        {
            main = room.transform.Cast<Transform>().First(t => t.name != "Ceiling" && t.name != "Lights");
            var rs = Drawn(main).Where(r => !UnderCeiling(r.transform)).ToArray();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            // never past the room's own cell
            var c = room.transform.position;
            var min = Vector3.Max(b.min, new Vector3(c.x - Tile / 2f, b.min.y, c.z - Tile / 2f));
            var max = Vector3.Min(b.max, new Vector3(c.x + Tile / 2f, b.max.y, c.z + Tile / 2f));
            b.SetMinMax(min, max);
            return b;
        }

        static bool UnderCeiling(Transform t)
        {
            for (var x = t; x != null; x = x.parent) if (IronCitadelCapture.IsCeiling(x)) return true;
            return false;
        }

        /// <summary>Free standing spots on a 0.75 m grid inside <paramref name="region"/> (xz) whose floor is
        /// at y in [yMin, yMax] with a 0.45 m-radius person-sized capsule clear of everything.</summary>
        static List<Vector3> FreeSpots(Bounds region, float yMin, float yMax, float step = 0.75f, float scale = 1f)
        {
            // the clearance capsule grows with the figure: 0.45 m wide and 0.55-1.7 m up for a person
            var list = new List<Vector3>();
            for (float x = region.min.x + 0.5f; x <= region.max.x - 0.5f; x += step)
                for (float z = region.min.z + 0.5f; z <= region.max.z - 0.5f; z += step)
                {
                    // keep 1.2 m off every tile edge, so nobody stands in a doorway
                    float fx = Mathf.Repeat(x, Tile), fz = Mathf.Repeat(z, Tile);
                    if (Mathf.Min(fx, Tile - fx) < 1.2f || Mathf.Min(fz, Tile - fz) < 1.2f) continue;
                    if (!FloorAt(new Vector3(x, 0, z), out var y) || y < yMin || y > yMax) continue;
                    var p = new Vector3(x, y, z);
                    if (Physics.CheckCapsule(p + Vector3.up * 0.55f * scale, p + Vector3.up * 1.7f * scale, 0.45f * scale, AllLayers, QueryTriggerInteraction.Ignore)) continue;
                    list.Add(p);
                }
            return list;
        }

        static List<Vector3> Choose(IEnumerable<Vector3> ordered, int n, float spacing, List<Vector3> taken)
        {
            var got = new List<Vector3>();
            foreach (var p in ordered)
            {
                if (got.Count >= n) break;
                if (got.Any(q => Flat(q - p).magnitude < spacing) || taken.Any(q => Flat(q - p).magnitude < spacing)) continue;
                got.Add(p);
            }
            taken.AddRange(got);
            return got;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static void BuildMarkers(Level level, Dictionary<string, GameObject> rooms, Transform markT, StringBuilder log)
        {
            var taken = new List<Vector3>();
            var defT = Child(markT, "Defenders");
            var pickT = Child(markT, "Pickups");

            // throne (for the throne room, the warlord and the Goal)
            var throneRoom = rooms["throne_room"];
            // room pieces are named after their Synty prefab (SM_Prop_Dwarf_Throne_01, ...)
            var throne = throneRoom.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("SM_", StringComparison.Ordinal) && t.name.IndexOf("Throne", StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponentInChildren<Renderer>() != null)
                .OrderByDescending(t => BoundsOf(t).size.y)
                .FirstOrDefault();
            var dais = throneRoom.transform.Cast<Transform>().FirstOrDefault(t => t.name == "dais");
            Vector3 thronePos = throne != null ? throne.position : (dais != null ? BoundsOf(dais).center : throneRoom.transform.position);
            if (throne != null) { var tb = BoundsOf(throne); thronePos = new Vector3(tb.center.x, tb.min.y, tb.center.z); }
            log.AppendLine($"throne: {(throne != null ? throne.name : "(not found, dais centre)")} at {thronePos}");
            var doorFacing = DoorDirection(level, "throne_room");

            foreach (var d in level.json["defenders"])
            {
                string roomId = (string)d["room"]; int count = (int)d["count"]; string note = (string)d["note"] ?? "";
                var roomGo = rooms[roomId];
                var mb = MainGroupBounds(roomGo, out var main);
                var cellB = new Bounds(roomGo.transform.position + Vector3.up * 2f, new Vector3(Tile - 1f, 4f, Tile - 1f));
                var spots = new List<(Vector3 p, float yaw, string role, string[] cast)>();

                if (roomId == "armory")
                {
                    var gateDoor = level.doors.First(x => x.kind == "portcullis");
                    var ge = level.RoomAt(gateDoor.r1, gateDoor.c1).id == "armory" ? EdgeBetween(gateDoor.r1, gateDoor.c1, gateDoor.r2, gateDoor.c2) : EdgeBetween(gateDoor.r2, gateDoor.c2, gateDoor.r1, gateDoor.c1);
                    MeasureOpening(ge, out var tc, out _, out _);
                    var gatePt = ge.centre + ge.along * tc;          // ge.n points from the armory to the entry
                    var target = gatePt - ge.n * 2.2f;
                    var free = FreeSpots(cellB, -0.2f, 0.35f);
                    var archers = Choose(free.Where(p => Flat(p - target).magnitude < 6f).OrderBy(p => Flat(p - target).magnitude), 3, 1.1f, taken);
                    foreach (var p in archers) spots.Add((p, Yaw(ge.n), "archer at the portcullis, facing south", Garrison));
                    var racks = Choose(FreeSpots(mb, -0.2f, 0.35f).OrderBy(p => Flat(p - mb.center).magnitude), count - archers.Count, 2.5f, taken);
                    foreach (var p in racks) spots.Add((p, Yaw(Flat(mb.center - p).sqrMagnitude > 1f ? mb.center - p : ge.n), "among the racks", Garrison));
                }
                else if (roomId == "great_hall")
                {
                    var tables = main.GetComponentsInChildren<Transform>(true)
                        .Where(t => t.name.StartsWith("SM_", StringComparison.Ordinal) && t.name.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponentInChildren<Renderer>() != null)
                        .Select(t => BoundsOf(t).center).ToList();
                    var free = FreeSpots(mb, -0.2f, 0.35f);
                    IEnumerable<Vector3> ordered = tables.Count > 0
                        ? free.OrderBy(p => Mathf.Abs(tables.Min(t => Flat(t - p).magnitude) - 1.3f))
                        : free.OrderBy(p => Flat(p - mb.center).magnitude);
                    foreach (var p in Choose(ordered, count, 1.6f, taken))
                    {
                        var tb = tables.Count > 0 ? tables.OrderBy(t => Flat(t - p).magnitude).First() : mb.center;
                        spots.Add((p, Yaw(Flat(tb - p).sqrMagnitude > 0.01f ? tb - p : Vector3.forward), "at the tables", Feasters));
                    }
                    log.AppendLine($"great_hall: {tables.Count} table pieces found");
                }
                else if (roomId == "throne_room")
                {
                    var stage = FreeSpots(cellB, 0.9f, 1.6f, 0.75f, WarlordScale).Where(p => Flat(p - thronePos).magnitude > 1.1f * WarlordScale).OrderBy(p => Flat(p - thronePos).magnitude);
                    foreach (var p in Choose(stage, 1, 1f * WarlordScale, taken)) spots.Add((p, Yaw(doorFacing), WarlordRole, new[] { Warlord }));
                    // "before it": a row at the foot of the steps, on the door side
                    var front = thronePos;
                    for (float st = 0f; st < 25f; st += 0.25f)
                    {
                        var q = thronePos + doorFacing * st;
                        if (FloorAt(q, out var fy) && fy < 0.3f) { front = q; break; }
                    }
                    // 4 m clear of the foot of the steps: a row any closer stands on the stage's apron and
                    // walls off the steps (conformance check 8 probes 4 m out from the stage edge)
                    var guardAt = Flat(front + doorFacing * 4f);
                    var floor = FreeSpots(cellB, -0.2f, 0.35f).OrderBy(p => Mathf.Abs(Vector3.Dot(Flat(p) - guardAt, doorFacing)) * 3f + Flat(p - guardAt).magnitude);
                    foreach (var p in Choose(floor, count - spots.Count, 2.0f, taken)) spots.Add((p, Yaw(doorFacing), "guard before the stage", Garrison));
                }
                else
                {
                    string[] cast = roomId == "prison" ? Prisoners : roomId == "kitchen" ? Cooks : roomId == "forge" ? Smiths : roomId == "study" ? Alchemist : Garrison;
                    var free = FreeSpots(mb, -0.2f, 0.35f);
                    foreach (var p in Choose(free.OrderBy(p => Flat(p - mb.center).magnitude), count, 2.2f, taken))
                        spots.Add((p, Yaw(Flat(mb.center - p).sqrMagnitude > 1f ? mb.center - p : DoorDirection(level, roomId)), note, cast));
                }

                if (spots.Count < count) log.AppendLine($"WARNING {roomId}: only {spots.Count} of {count} defender spots found");
                int i = 0;
                foreach (var s in spots)
                {
                    var m = new GameObject($"Defender {roomId} {i + 1}");
                    m.transform.SetParent(defT, false);
                    m.transform.position = s.p;
                    m.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);
                    var mk = m.AddComponent<IronCitadelMarker>();
                    mk.kind = IronCitadelMarker.Kind.Defender; mk.room = roomId; mk.note = s.role;
                    float k = s.role == WarlordRole ? WarlordScale : 1f;
                    var cap = m.AddComponent<CapsuleCollider>(); // solid, so nobody walks through them
                    cap.radius = 0.35f * k; cap.height = 1.8f * k; cap.center = new Vector3(0f, 0.9f * k, 0f);
                    var ch = Spawn(s.cast[i % s.cast.Length], m.transform);
                    ch.transform.localPosition = Vector3.zero;
                    ch.transform.localRotation = Quaternion.identity;
                    ch.transform.localScale = Vector3.one * k;
                    PoseArmsDown(ch);
                    i++;
                }
                var rc = roomGo.transform.position;
                int outside = spots.Count(x => Mathf.Abs(x.p.x - rc.x) > Tile / 2f || Mathf.Abs(x.p.z - rc.z) > Tile / 2f);
                log.AppendLine($"defenders {roomId}: {spots.Count}/{count}{(outside > 0 ? $"  WARNING {outside} outside the cell" : "")}  at " +
                               string.Join(" ", spots.Select(x => $"({x.p.x:0.0},{x.p.y:0.0},{x.p.z:0.0})")));
            }

            // pickups
            foreach (var pk in level.json["pickups"])
            {
                string roomId = (string)pk["room"]; string note = (string)pk["note"];
                var roomGo = rooms[roomId];
                var mb = MainGroupBounds(roomGo, out _);
                var free = FreeSpots(mb, -0.2f, 0.35f);
                var p = Choose(free.OrderBy(q => Flat(q - mb.center).magnitude), 1, 2.0f, taken).FirstOrDefault();
                if (p == default) { log.AppendLine($"WARNING pickup {roomId}: no spot"); continue; }
                var m = new GameObject($"Pickup {roomId}");
                m.transform.SetParent(pickT, false);
                m.transform.position = p;
                m.transform.rotation = Quaternion.Euler(0f, Yaw(DoorDirection(level, roomId)), 0f);
                var mk = m.AddComponent<IronCitadelMarker>();
                mk.kind = IronCitadelMarker.Kind.Pickup; mk.room = roomId; mk.note = note;
                var chest = Spawn(roomId == "vault" ? ChestHoard : ChestGear, m.transform, "Chest");
                chest.transform.localPosition = Vector3.zero; chest.transform.localRotation = Quaternion.identity;
                if (roomId == "vault")
                    for (int k = 0; k < 3; k++)
                    {
                        var c = Spawn(Coins, m.transform, "Coins");
                        c.transform.localPosition = new Vector3(-0.6f + 0.6f * k, 0f, 0.65f);
                        c.transform.localRotation = Quaternion.Euler(0f, 37f * k, 0f);
                    }
                var lg = new GameObject("PickupGlow");
                lg.transform.SetParent(m.transform, false);
                lg.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.45f); l.intensity = 1.5f; l.range = 3.5f; l.shadows = LightShadows.None;
                log.AppendLine($"pickup {roomId} at {p}");
            }

            // PlayerStart at the outer door, facing north
            var start = level.json["start"];
            var outer = level.doors.First(x => x.id == (string)start["at"]);
            var oe = EdgeOfDoor(outer);
            MeasureOpening(oe, out var otc, out _, out _);
            var sp = oe.centre + oe.along * otc - oe.n * 1.6f;
            if (FloorAt(sp, out var sy)) sp.y = sy;
            var ps = new GameObject("PlayerStart");
            ps.transform.SetParent(markT, false);
            ps.transform.position = sp;
            ps.transform.rotation = Quaternion.Euler(0f, FacingYaw((string)start["facing"]), 0f);
            TrySetTag(ps, "Respawn");
            var pm = ps.AddComponent<IronCitadelMarker>(); pm.kind = IronCitadelMarker.Kind.PlayerStart; pm.room = "entry_hall"; pm.note = "outer_door, facing north";
            log.AppendLine($"PlayerStart at {sp}");

            var goal = new GameObject("Goal");
            goal.transform.SetParent(markT, false);
            goal.transform.position = thronePos;
            goal.transform.rotation = Quaternion.Euler(0f, Yaw(doorFacing), 0f);
            TrySetTag(goal, "Finish");
            var gm = goal.AddComponent<IronCitadelMarker>(); gm.kind = IronCitadelMarker.Kind.Goal; gm.room = "throne_room"; gm.note = "the throne";
            var trig = goal.AddComponent<SphereCollider>(); trig.isTrigger = true; trig.radius = 2f; trig.center = Vector3.up;
            log.AppendLine($"Goal at {thronePos}");
        }

        // ------------------------------------------------------------------ play: walker, HUD, overview

        public const string PlayerPrefabPath = Dir + "/Walker/IronCitadelPlayer.prefab";

        /// <summary>The walker prefab (Starter Assets' first-person rig with InteractOnE) is made once by the
        /// walker kit's setup; make it here if it is missing.</summary>
        public static void EnsurePlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null) return;
            IronCitadel.Interact.Editor.IronCitadelPlayerSetup.MakePlayer();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                throw new Exception("could not make " + PlayerPrefabPath + " (is Starter Assets imported?)");
        }

        /// <summary>IronCitadel/Play: the walker at PlayerStart facing north, the HUD (room, time, throne) and
        /// the M overview (an ortho camera and a soft overhead light, both off until M). Under the root, so a
        /// rebuild keeps them.</summary>
        static void BuildPlay(Level level, GameObject root, Transform markT, StringBuilder log)
        {
            var play = Child(root.transform, "Play");
            var ps = markT.Find("PlayerStart");
            var goal = markT.Find("Goal");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null) throw new Exception("missing " + PlayerPrefabPath + "; run EnsurePlayerPrefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
            player.name = "IronCitadelPlayer";
            player.transform.SetParent(play, true);
            var capsule = player.GetComponentsInChildren<Transform>(true).First(t => t.name == "PlayerCapsule");
            // turn the rig about the capsule to PlayerStart's yaw, then move it so the capsule's feet are there
            float dyaw = Mathf.DeltaAngle(capsule.eulerAngles.y, ps.eulerAngles.y);
            player.transform.RotateAround(capsule.position, Vector3.up, dyaw);
            player.transform.position += ps.position + Vector3.up * 0.05f - capsule.position;
            // the main camera starts at the eye, so the editor's Game view is right before Play
            var camRoot = capsule.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PlayerCameraRoot");
            var mainCam = player.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (mainCam != null && camRoot != null) mainCam.transform.SetPositionAndRotation(camRoot.position, camRoot.rotation);
            log.AppendLine($"walker {PlayerPrefabPath} at {capsule.position} yaw {capsule.eulerAngles.y:0}; camera root {(camRoot != null ? camRoot.position.ToString() : "none")}");

            var hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(play, false);
            var hud = hudGo.AddComponent<IronCitadel.Play.IronCitadelHud>();
            hud.level = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelPath);
            hud.player = capsule;
            hud.goal = goal;

            var camGo = new GameObject("OverviewCamera");
            camGo.transform.SetParent(play, false);
            camGo.transform.SetPositionAndRotation(new Vector3(Tile * level.cols / 2f, 400f, -Tile * level.rows / 2f), Quaternion.Euler(90f, 0f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Tile * level.rows / 2f + 4f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            cam.enabled = false;

            var lightGo = new GameObject("OverviewLight");
            lightGo.transform.SetParent(play, false);
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
            ov.levelRoot = root.transform;
            ov.player = capsule;
            ov.centreXZ = new Vector2(Tile * level.cols / 2f, -Tile * level.rows / 2f);
            ov.sizeXZ = new Vector2(Tile * level.cols, Tile * level.rows);
            log.AppendLine("play: HUD (room, time, throne) and M overview added under IronCitadel/Play");
        }

        /// <summary>Switches IronCitadel/Play off for a check or a capture and back on when disposed.</summary>
        sealed class HidePlay : IDisposable
        {
            readonly GameObject play;
            public HidePlay(GameObject root)
            {
                var t = root.transform.Find("Play");
                if (t != null && t.gameObject.activeSelf) { play = t.gameObject; play.SetActive(false); Physics.SyncTransforms(); }
            }
            public void Dispose()
            {
                if (play != null) { play.SetActive(true); Physics.SyncTransforms(); }
            }
        }

        static void TrySetTag(GameObject go, string tag)
        {
            try { go.tag = tag; } catch { }
        }

        static float FacingYaw(string f) => f == "east" ? 90f : f == "south" ? 180f : f == "west" ? 270f : 0f;

        /// <summary>Unit vector from the room's centre toward the first door level.json gives it.</summary>
        static Vector3 DoorDirection(Level level, string roomId)
        {
            var room = level.ById(roomId);
            foreach (var d in level.doors)
            {
                if (d.r1 == room.r && d.c1 == room.c) return EdgeOfDoor(d).n;
                if (!d.outside && d.r2 == room.r && d.c2 == room.c) return -EdgeOfDoor(d).n;
            }
            return Vector3.forward;
        }

        static Bounds BoundsOf(Transform t)
        {
            var rs = Drawn(t);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>The Synty characters ship in a T-pose with no idle clip; drop the arms to the sides so a
        /// static marker reads as a person standing. Bones are named Shoulder_L/R, Elbow_L/R.</summary>
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
                var outward = Flat(cur).normalized;
                var target = (Vector3.down * Mathf.Cos(12f * Mathf.Deg2Rad) + outward * Mathf.Sin(12f * Mathf.Deg2Rad)).normalized;
                sh.rotation = Quaternion.FromToRotation(cur.normalized, target) * sh.rotation;
            }
        }

        // ------------------------------------------------------------------ door check

        /// <summary>Every tile edge of every room, at walking height (0.5, 1.2, 1.8 m) every 0.25 m: where
        /// both sides have a floor and nothing stands across the line, the tiles are joined. A listed
        /// door must have such a passage (with the features hidden); an unlisted edge must have none and
        /// no doorway opening onto a wall or the void. Then, with the features shown, the portcullis,
        /// the closed bookcase and the outer door must each block their doorway.</summary>
        public static bool CheckDoors(out string reportPath)
        {
            var level = LoadLevel();
            var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (root == null) throw new Exception("no IronCitadel root in the active scene; build first");
            var feat = root.transform.Find("Features");
            // the walker's capsule stands near the outer door; the check is about the rooms
            using var playOff = new HidePlay(root);
            var sb = new StringBuilder();
            bool ok = true;
            sb.AppendLine($"Iron Citadel rooms door check, {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"lights under {RootName}: {root.GetComponentsInChildren<Light>(false).Length}");

            // every edge once: interior edges between two cells, and the outer edges of room cells
            var edges = new List<(Edge e, Door door)>();
            for (int r = 0; r < level.rows; r++)
                for (int c = 0; c < level.cols; c++)
                {
                    if (level.RoomAt(r, c) == null) continue;
                    foreach (var side in "NESW")
                    {
                        var e = EdgeOf(r, c, side);
                        bool inside = e.r2 >= 0 && e.r2 < level.rows && e.c2 >= 0 && e.c2 < level.cols;
                        var other = inside ? level.RoomAt(e.r2, e.c2) : null;
                        if (other != null && (other.r * level.cols + other.c) < (r * level.cols + c)) continue; // seen from the other side
                        var door = level.doors.FirstOrDefault(d =>
                            (!d.outside && ((d.r1 == r && d.c1 == c && d.r2 == e.r2 && d.c2 == e.c2) || (d.r2 == r && d.c2 == c && d.r1 == e.r2 && d.c1 == e.c2)))
                            || (d.outside && d.r1 == r && d.c1 == c && d.outsideSide == side));
                        edges.Add((e, door));
                    }
                }

            feat.gameObject.SetActive(false);
            Physics.SyncTransforms();
            sb.AppendLine();
            sb.AppendLine("== geometry (features hidden)");
            var passages = new Dictionary<string, List<Vector2>>();
            foreach (var (e, door) in edges)
            {
                var a = level.RoomAt(e.r, e.c);
                bool inGrid = e.r2 >= 0 && e.r2 < level.rows && e.c2 >= 0 && e.c2 < level.cols;
                var b = inGrid ? level.RoomAt(e.r2, e.c2) : null;
                string bName = b != null ? b.id : inGrid ? "rock" : "outside";
                Scan(e, out var pass, out var dangA, out var dangB);
                passages[e.ToString()] = pass;
                string verdict;
                if (door != null && !door.outside)
                    verdict = pass.Any(p => p.y - p.x >= 0.9f) ? "OK" : "PROBLEM: listed door has no passage";
                else if (door != null)
                    verdict = dangA.Any(p => p.y - p.x >= 0.9f) ? "OK (opens to the outside)" : "PROBLEM: outer door has no opening";
                else
                    verdict = pass.Count == 0 && dangA.All(p => p.y - p.x < 0.5f) && dangB.All(p => p.y - p.x < 0.5f) ? "OK (closed)" : "PROBLEM: unlisted opening";
                if (verdict.StartsWith("PROBLEM")) ok = false;
                sb.AppendLine($"{a.id,-12} {e.side} {bName,-12} door {(door != null ? door.id + " (" + door.kind + ")" : "-"),-34} pass {Runs(pass, e)} | open-on-{a.id} only {Runs(dangA, e)} | open-on-{bName} only {Runs(dangB, e)} -> {verdict}");
                if (verdict.StartsWith("PROBLEM") && door != null)
                {
                    MeasureOpeningRaw(e, out var ptc, out _, out _);
                    sb.AppendLine($"    probe t {ptc:0.00}:{Probe(e, ptc)}");
                }
            }

            feat.gameObject.SetActive(true);
            Physics.SyncTransforms();
            sb.AppendLine();
            sb.AppendLine("== with features (portcullis, closed bookcase, shut outer door)");
            foreach (var (e, door) in edges)
            {
                if (door == null) continue;
                bool mustBlock = door.kind == "portcullis" || door.kind == "secret door" || door.outside;
                if (!mustBlock) continue;
                Scan(e, out var pass, out var dangA, out _);
                bool blocked = pass.All(p => p.y - p.x < 0.5f) && (!door.outside || dangA.All(p => p.y - p.x < 0.5f));
                if (!blocked) ok = false;
                sb.AppendLine($"{door.id,-12} {(blocked ? "blocked: OK" : "PROBLEM: still passable")} pass {Runs(pass, e)} open-one-side {Runs(dangA, e)}");
            }
            // the secret door swung open (as Interact leaves it, but with its colliders still on): the
            // leaf must clear the doorway
            var sbd = feat.GetComponentInChildren<SecretBookcaseDoor>(true);
            if (sbd != null && sbd.hinge != null)
            {
                var r0 = sbd.hinge.localRotation;
                sbd.hinge.localRotation = r0 * Quaternion.Euler(0f, sbd.openAngle, 0f);
                Physics.SyncTransforms();
                var sd = level.doors.First(d => d.kind == "secret door");
                var se = EdgeOfDoor(sd);
                Scan(se, out var spass, out _, out _);
                bool open = spass.Any(p => p.y - p.x >= 0.9f);
                if (!open) ok = false;
                sb.AppendLine($"{sd.id,-12} swung {sbd.openAngle:0} deg, colliders on: {(open ? "passable: OK" : "PROBLEM: the leaf still blocks")} pass {Runs(spass, se)}");
                sbd.hinge.localRotation = r0;
                Physics.SyncTransforms();
            }
            sb.AppendLine();
            sb.AppendLine(ok ? "RESULT: OK" : "RESULT: PROBLEMS (see above)");
            reportPath = OutRoot + "/logs/rooms-doorcheck.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, sb.ToString());
            Debug.Log("[IronCitadelRooms] door check\n" + sb);
            return ok;
        }

        /// <summary>What the door check sees at one point of an edge: the floor under each side and the first
        /// thing hit between each side and the edge line.</summary>
        static string Probe(Edge e, float t)
        {
            var p = e.centre + e.along * t;
            var pa = p - e.n * 0.6f; var pb = p + e.n * 0.6f;
            var sb = new StringBuilder();
            foreach (var (side, q) in new[] { ("A", pa), ("B", pb) })
            {
                if (Physics.Raycast(new Vector3(q.x, 3.3f, q.z), Vector3.down, out var hit, 5.3f, AllLayers, QueryTriggerInteraction.Ignore))
                    sb.Append($" floor{side} y {hit.point.y:0.00} on {PathOf(hit.collider.transform)};");
                else sb.Append($" floor{side} none;");
            }
            foreach (var hgt in Heights)
            {
                var a = pa + Vector3.up * hgt; var b = p + e.n * 0.05f + Vector3.up * hgt;
                if (Physics.Linecast(a, b, out var h1, AllLayers, QueryTriggerInteraction.Ignore)) sb.Append($" A>edge@{hgt} {PathOf(h1.collider.transform)} at {h1.point};");
                if (Physics.Linecast(b, a, out var h2, AllLayers, QueryTriggerInteraction.Ignore)) sb.Append($" edge>A@{hgt} {PathOf(h2.collider.transform)} at {h2.point};");
            }
            return sb.ToString();
        }

        static string PathOf(Transform t)
        {
            var s = t.name;
            for (var x = t.parent; x != null && x.parent != null; x = x.parent) s = x.name + "/" + s;
            return s;
        }

        static string Runs(List<Vector2> runs, Edge e)
            => runs.Count == 0 ? "none" : string.Join(", ", runs.Select(r => $"{r.x:0.0}..{r.y:0.0} (w {r.y - r.x + 0.25f:0.0})"));

        /// <summary>Runs along an edge (offsets from its centre, m) where the tiles are joined (pass),
        /// where only the near side (A) reaches the line (a doorway onto a wall or the void), and the
        /// same for the far side (B).</summary>
        static void Scan(Edge e, out List<Vector2> pass, out List<Vector2> dangA, out List<Vector2> dangB)
        {
            pass = new List<Vector2>(); dangA = new List<Vector2>(); dangB = new List<Vector2>();
            float? sp = null, sa = null, sb = null; float step = 0.25f, last = 0;
            for (float t = -Tile / 2 + 0.25f; t <= Tile / 2 - 0.25f + 1e-3f; t += step)
            {
                var p = e.centre + e.along * t;
                var pa = p - e.n * 0.6f; var pb = p + e.n * 0.6f;
                bool fa = FloorAt(pa, out var ya) && ya > -0.4f && ya < 0.6f;
                bool fb = FloorAt(pb, out var yb) && yb > -0.4f && yb < 0.6f;
                bool ca = fa && CrossClear(pa, p + e.n * 0.05f);
                bool cb = fb && CrossClear(pb, p - e.n * 0.05f);
                bool joined = ca && cb && CrossClear(pa, pb);
                Track(ref sp, joined, t, step, pass);
                Track(ref sa, ca && !joined, t, step, dangA);
                Track(ref sb, cb && !joined, t, step, dangB);
                last = t;
            }
            Close(sp, last, pass); Close(sa, last, dangA); Close(sb, last, dangB);
        }

        static void Track(ref float? start, bool on, float t, float step, List<Vector2> runs)
        {
            if (on && start == null) start = t;
            if (!on && start != null) { runs.Add(new Vector2(start.Value, t - step)); start = null; }
        }

        static void Close(float? start, float last, List<Vector2> runs)
        {
            if (start != null) runs.Add(new Vector2(start.Value, last));
        }

        // ------------------------------------------------------------------ captures

        /// <summary>Writes C:/Repos/IronCitadel/captures/rooms/: overview.png (ceilings hidden, flat map light,
        /// tile grid), overview-play.png (what M shows in play mode: ceilings hidden, the night lights plus the
        /// overview light), one eye-height shot per room (eye-&lt;room&gt;.png, under the night lights), and the
        /// two features (the gate and the bookcase door, closed and swung open).</summary>
        public static void Captures()
        {
            var level = LoadLevel();
            var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (root == null) throw new Exception("no IronCitadel root in the active scene; build first");
            string cap = OutRoot + "/captures/rooms/";
            Directory.CreateDirectory(cap);
            var overviewLight = root.transform.Find("Play/OverviewLight")?.GetComponent<Light>();
            using (new HidePlay(root))
            {
                var hidden = IronCitadelCapture.HideCeilings(root);
                try
                {
                    var c = new Vector2(Tile * level.cols / 2f, -Tile * level.rows / 2f);
                    IronCitadelCapture.TopDown(cap + "overview.png", c, Tile * level.cols / 2f + 2f, 2048, true, 300f, Tile, Vector2.zero);
                    if (overviewLight != null)
                    {
                        // Play is off, so its light is off; light the map the way M does
                        var lgo = new GameObject("~ICOverviewLight");
                        var l = lgo.AddComponent<Light>();
                        l.type = LightType.Directional; l.color = overviewLight.color; l.intensity = overviewLight.intensity; l.shadows = LightShadows.None;
                        lgo.transform.rotation = overviewLight.transform.rotation;
                        try { IronCitadelCapture.TopDown(cap + "overview-play.png", c, Tile * level.cols / 2f + 2f, 2048, false, 300f); }
                        finally { Object.DestroyImmediate(lgo); }
                    }
                }
                finally { IronCitadelCapture.Restore(hidden); }

                // one eye-height shot per room
                var lines = new StringBuilder();
                foreach (var room in level.rooms)
                {
                    var rt = root.transform.Find("Rooms/" + room.id);
                    if (rt == null) continue;
                    if (RoomView(level, root, rt.gameObject, room.id, out var eye, out var yaw, out var pitch, out var why))
                    {
                        IronCitadelCapture.Perspective(cap + $"eye-{room.id}.png", eye, yaw, pitch, 70f, 1280, 720, false);
                        lines.AppendLine($"eye-{room.id}.png: eye {eye} yaw {yaw:0} pitch {pitch:0} ({why})");
                    }
                    else lines.AppendLine($"eye-{room.id}.png: NO VIEWPOINT ({why})");
                }

                // the features: the gate from the entry side, the bookcase door closed and swung open
                var gate = root.transform.Find("Features/armory_gate");
                if (gate != null) Shot(cap + "feature-armory_gate.png", gate.position + gate.forward * 7f, gate.eulerAngles.y + 180f);
                var sdoor = root.GetComponentInChildren<SecretBookcaseDoor>(true);
                if (sdoor != null && sdoor.hinge != null)
                {
                    var sd = level.doors.First(d => d.kind == "secret door");
                    var se = EdgeBetween(sd.r1, sd.c1, sd.r2, sd.c2);
                    var vdp = sdoor.transform.parent.position;
                    Shot(cap + "feature-vault_door-closed.png", vdp - se.n * 3.6f + se.along * 1.8f, Yaw(se.n) - 25f);
                    var r0 = sdoor.hinge.localRotation;
                    sdoor.hinge.localRotation = r0 * Quaternion.Euler(0f, sdoor.openAngle, 0f);
                    try { Shot(cap + "feature-vault_door-open.png", vdp - se.n * 3.6f + se.along * 1.8f, Yaw(se.n) - 25f); }
                    finally { sdoor.hinge.localRotation = r0; }
                }
                GalleryShots(level, root, cap, lines);
                File.WriteAllText(cap + "captures.txt", lines.ToString());
            }
            Debug.Log("[IronCitadelRooms] captures written to " + cap);
        }

        /// <summary>The great hall's gallery (HallGalleryBack: a walled stair at each end of the south gallery, climbing
        /// south from a landing off the side hall): gallery-stair-west.png and gallery-stair-east.png stand on each landing,
        /// 8 m north of the lower flight's first step, and look up the stair at its head on the deck; gallery-rail.png
        /// stands on the deck 1.1 m behind the rail, 8 m in from the west end, and looks east along the gallery with the
        /// hall floor on its left. The stairs are the great hall's SM_*Stairs* pieces, split west and east of its centre.</summary>
        static void GalleryShots(Level level, GameObject root, string cap, StringBuilder lines)
        {
            var hallRoom = level.rooms.FirstOrDefault(r => r.kind == "D");
            var hall = hallRoom == null ? null : root.transform.Find("Rooms/" + hallRoom.id);
            if (hall == null) { lines.AppendLine("gallery shots: no great hall"); return; }
            var H = hall.transform;
            var stairs = Drawn(H).Where(r => r.name.StartsWith("SM_", StringComparison.Ordinal) && r.name.IndexOf("Stairs", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var (side, sign) in new[] { ("west", -1f), ("east", 1f) })
            {
                var mine = stairs.Where(r => Mathf.Sign(H.InverseTransformPoint(r.bounds.center).x) == sign).ToList();
                if (mine.Count == 0) { lines.AppendLine($"gallery-stair-{side}.png: NO STAIR PIECES"); continue; }
                // bounds in the hall's frame: the stairs climb toward local -z (the gallery side)
                float lx0 = float.MaxValue, lx1 = float.MinValue, lz0 = float.MaxValue, lz1 = float.MinValue, top = 0f;
                foreach (var r in mine)
                {
                    var b = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var lp = H.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                        lx0 = Mathf.Min(lx0, lp.x); lx1 = Mathf.Max(lx1, lp.x); lz0 = Mathf.Min(lz0, lp.z); lz1 = Mathf.Max(lz1, lp.z);
                    }
                    top = Mathf.Max(top, b.max.y);
                }
                float cx = (lx0 + lx1) / 2f;
                var feet = H.TransformPoint(new Vector3(cx, 0f, lz1 + 8f));
                if (FloorAt(feet, out var fy, 2f)) feet.y = fy;
                var eye = feet + Vector3.up * 1.7f;
                var head = H.TransformPoint(new Vector3(cx, 0f, lz0)) + Vector3.up * (top + 1.2f);
                var look = head - eye;
                float yaw = Yaw(look), pitch = Mathf.Clamp(-Mathf.Atan2(look.y, Flat(look).magnitude) * Mathf.Rad2Deg, -35f, 35f);
                IronCitadelCapture.Perspective(cap + $"gallery-stair-{side}.png", eye, yaw, pitch, 70f, 1280, 720, false);
                lines.AppendLine($"gallery-stair-{side}.png: eye {eye} yaw {yaw:0} pitch {pitch:0} (the {side} landing, up the stair to its head at {head})");
            }
            // along the gallery from the rail: the deck's front row, 1.1 m behind the rail line (local z -7.5)
            var railFeet = H.TransformPoint(new Vector3(-Tile / 2f + 5f + 8f, 0f, -8.6f));
            if (FloorAt(railFeet, out var dy, 6.5f) && dy > 4f)
            {
                railFeet.y = dy;
                float ryaw = H.eulerAngles.y + 78f;
                IronCitadelCapture.Perspective(cap + "gallery-rail.png", railFeet + Vector3.up * 1.7f, ryaw, 12f, 70f, 1280, 720, false);
                lines.AppendLine($"gallery-rail.png: eye {railFeet + Vector3.up * 1.7f} yaw {ryaw:0} pitch 12 (on the deck behind the rail, looking east along the gallery)");
            }
            else lines.AppendLine($"gallery-rail.png: NO DECK under {railFeet} (floor {dy:0.00})");
        }

        /// <summary>An eye-height viewpoint inside a room's main space: a free standing spot that sees the
        /// room's focus (the throne, the gallery, else the middle of the main space) and stands well back
        /// from it, on the side the player arrives from.</summary>
        static bool RoomView(Level level, GameObject root, GameObject room, string id, out Vector3 eye, out float yaw, out float pitch, out string why)
        {
            eye = default; yaw = 0f; pitch = 5f;
            var mb = MainGroupBounds(room, out var main);
            Vector3 focus = new Vector3(mb.center.x, 1.3f, mb.center.z);
            Vector3 back = DoorDirection(level, id);         // from the focus toward where the camera stands
            why = "main space " + main.name;
            if (id == "throne_room")
            {
                var goal = root.transform.Find("Markers/Goal");
                if (goal != null) { focus = goal.position + Vector3.up * 1.4f; why = "the throne"; }
            }
            else if (id == "great_hall")
            {
                // the gallery is on the south: look at it from the north part of the hall
                focus = new Vector3(mb.center.x, 4.5f, mb.min.z + 4f);
                back = Vector3.forward;
                why = "the gallery, from the north";
            }
            float want = Mathf.Clamp(0.45f * Mathf.Max(mb.size.x, mb.size.z), 6f, 18f);
            var cellB = new Bounds(room.transform.position + Vector3.up * 2f, new Vector3(Tile - 1f, 4f, Tile - 1f));
            var spots = FreeSpots(id == "throne_room" || id == "great_hall" ? cellB : mb, -0.2f, 0.35f, 1f);
            float best = float.NegativeInfinity;
            // first pass: an uncluttered spot (nothing within 1.2 m of the eye, so no door jamb, bunk or table
            // fills the frame) and, for the throne, far enough back to show the steps; then without those
            for (int pass = 0; pass < 2 && float.IsNegativeInfinity(best); pass++)
            foreach (var p in spots)
            {
                var e = p + Vector3.up * 1.7f;
                var to = Flat(focus - e);
                float d = to.magnitude;
                if (d < 3f) continue;
                if (pass == 0 && (Physics.CheckSphere(e, 1.2f, AllLayers, QueryTriggerInteraction.Ignore) || (id == "throne_room" && d < 12f)
                                  || (id == "great_hall" && !FrameClear(e, to)))) continue;
                int seen = 0;
                foreach (var off in new[] { Vector3.zero, Vector3.right * 2f, Vector3.left * 2f, Vector3.forward * 2f, Vector3.back * 2f })
                {
                    var t = focus + off;
                    var dir = t - e;
                    if (!Physics.Raycast(e, dir.normalized, dir.magnitude - 0.6f, AllLayers, QueryTriggerInteraction.Ignore)) seen++;
                }
                if (seen < 3) continue;
                float side = Vector3.Dot(Flat(e - focus).normalized, Flat(back).normalized);
                float score = seen * 2f + (side + 1f) * 3f - Mathf.Abs(d - want) * 0.6f;
                if (score > best) { best = score; eye = e; }
            }
            if (float.IsNegativeInfinity(best)) { why += ": no free spot sees it"; return false; }
            var look = focus - eye;
            yaw = Yaw(look);
            pitch = Mathf.Clamp(-Mathf.Atan2(look.y, Flat(look).magnitude) * Mathf.Rad2Deg + 3f, -20f, 20f);
            return true;
        }

        /// <summary>No wall, jamb or door leaf within 5 m of the eye across the frame: level rays at 20 and 35 degrees
        /// either side of the view direction (round 2: the great hall's shot had a door leaf filling its left third).</summary>
        static bool FrameClear(Vector3 eye, Vector3 dir)
        {
            foreach (float a in new[] { -35f, -20f, 20f, 35f })
                if (Physics.Raycast(eye, Quaternion.Euler(0f, a, 0f) * dir.normalized, 5f, AllLayers, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        static void Shot(string path, Vector3 feet, float yaw)
            => IronCitadelCapture.Perspective(path, feet + Vector3.up * 1.7f, yaw, 5f, 70f, 1280, 720, false);
    }
}
