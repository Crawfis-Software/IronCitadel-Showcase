using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronCitadel.EditorTools
{
    /// <summary>
    /// Checks and walks for the Iron Citadel. Entry point: RunAll() (menu Tools/Iron Citadel/Run checks and walks).
    /// 1. Static checks measure the saved scene (Assets/Scenes/IronCitadel.unity) with physics queries: ceilings, heights, doors, openings,
    ///    the gallery, the stage, markers, occluders.
    /// 2. Then the editor enters play mode and a WalkDriver drives the shipped player along the five walks through its own inputs.
    /// 3. Checks/results.txt is written with everything; Map/map.json is exported from the saved scene; two play-mode screenshots land in Captures/.
    /// </summary>
    public static class IronCitadelChecks
    {
        const string ResultsPath = "Checks/results.txt";
        const string StaticPath = "Checks/results.static.txt";
        const string PendingKey = "IronCitadel.WalksPending";
        static StringBuilder sb;
        static int passCount, failCount;

        [MenuItem("Tools/Iron Citadel/Run checks and walks")]
        public static void RunAll()
        {
            Directory.CreateDirectory("Checks"); Directory.CreateDirectory("Captures"); Directory.CreateDirectory("Map");
            if (EditorApplication.isPlaying) { Debug.LogWarning("[IronCitadelChecks] exit play mode first"); return; }
            string text = RunStatic();
            File.WriteAllText(StaticPath, text);
            MapExporter.Export("Map/map.json");
            SessionState.SetBool(PendingKey, true);
            PlayerSettings.runInBackground = true; // the game loop must tick while the editor window is unfocused (it is driven from the command line)
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Static checks only (no play mode). Returns the report text.</summary>
        public static string RunStatic()
        {
            if (SceneManager.GetActiveScene().path != IronCitadelBuilder.ScenePath) EditorSceneManager.OpenScene(IronCitadelBuilder.ScenePath);
            Physics.SyncTransforms();
            sb = new StringBuilder(); passCount = failCount = 0;
            sb.AppendLine("IRON CITADEL CHECKS  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  scene " + IronCitadelBuilder.ScenePath);
            sb.AppendLine("Every value below is measured on the saved scene with physics queries or scene data, not taken from the build plan.");
            sb.AppendLine();
            var rooms = UnityEngine.Object.FindObjectsByType<IronRoom>(FindObjectsSortMode.None).OrderBy(r => r.roomName).ToList();
            var doors = UnityEngine.Object.FindObjectsByType<IronDoor>(FindObjectsSortMode.None).OrderBy(d => d.name).ToList();
            var markers = UnityEngine.Object.FindObjectsByType<IronMarker>(FindObjectsSortMode.None).ToList();
            foreach (var r in rooms) r.Refresh();

            CheckRooms(rooms);
            var heights = CheckCeilings(rooms);
            CheckHeights(rooms, heights);
            var widths = CheckDoors(rooms, doors);
            CheckOpenings(rooms, doors);
            CheckArmory(rooms, doors, markers);
            CheckWayRound(doors);
            CheckGallery(rooms, doors);
            CheckStage(rooms, doors);
            CheckVault(doors);
            CheckOccluders(rooms);
            CheckMarkers(rooms, markers);
            CheckPlayer(rooms);

            sb.AppendLine();
            sb.AppendLine("STATIC SUMMARY: " + passCount + " passed, " + failCount + " failed");
            return sb.ToString();
        }

        static void Check(string name, bool pass, string detail)
        {
            if (pass) passCount++; else failCount++;
            sb.AppendLine((pass ? "[PASS] " : "[FAIL] ") + name);
            foreach (var line in detail.Split('\n')) if (line.Trim().Length > 0) sb.AppendLine("        " + line.TrimEnd());
        }

        static string F(Vector3 v) => string.Format("({0:F2}, {1:F2}, {2:F2})", v.x, v.y, v.z);
        static int Mask => ~((1 << IronCitadelBuilder.MapOnlyLayer) | (1 << 2)); // everything but map-only and Ignore Raycast

        // ---------------------------------------------------------------- rooms
        static readonly string[] RequiredKinds = { "entry hall", "armory", "guard room", "prison", "kitchen", "library", "alchemist's study", "forge", "treasure vault", "dining hall", "throne room", "crypt", "storeroom", "shrine", "torture chamber" };

        static void CheckRooms(List<IronRoom> rooms)
        {
            var d = new StringBuilder(); bool ok = true;
            foreach (var k in RequiredKinds)
            {
                var found = rooms.Where(r => r.kind == k).ToList();
                if (found.Count != 1) { ok = false; d.AppendLine("kind '" + k + "': " + found.Count + " rooms"); }
                else d.AppendLine(k + " -> '" + found[0].roomName + "' (" + found[0].state + ")");
            }
            foreach (var r in rooms.Where(r => r.kind == "extra")) d.AppendLine("extra: '" + r.roomName + "'" + (r.isGallery ? " (gallery deck of " + r.parentRoom + ")" : ""));
            if (rooms.Select(r => r.roomName).Distinct().Count() != rooms.Count) { ok = false; d.AppendLine("room names are not unique"); }
            foreach (var r in rooms) if (!new[] { "decayed", "looted", "stocked", "untouched" }.Contains(r.state)) { ok = false; d.AppendLine("bad state on " + r.roomName); }
            Check("One room of each kind, with unique names and valid states", ok, d.ToString());
        }

        // ---------------------------------------------------------------- ceilings and heights
        /// <summary>Casts up from a grid over every floor tile; returns the median ceiling height per room (above its floor).</summary>
        static Dictionary<string, float> CheckCeilings(List<IronRoom> rooms)
        {
            var result = new Dictionary<string, float>();
            var d = new StringBuilder(); bool ok = true;
            foreach (var r in rooms)
            {
                int total = 0, hit = 0; var hs = new List<float>();
                foreach (var rect in r.FloorRects())
                {
                    for (float x = rect.xMin + 0.35f; x <= rect.xMax - 0.3f; x += 0.5f)
                        for (float z = rect.yMin + 0.35f; z <= rect.yMax - 0.3f; z += 0.5f)
                        {
                            total++;
                            var origin = new Vector3(x, r.floorY + 0.25f, z);
                            if (Physics.Raycast(origin, Vector3.up, out var h, 60f, Mask, QueryTriggerInteraction.Ignore)) { hit++; hs.Add(h.point.y - r.floorY); }
                        }
                }
                hs.Sort();
                float median = hs.Count > 0 ? hs[hs.Count / 2] : -1f;
                float cover = total > 0 ? 100f * hit / total : 0f;
                result[r.roomName] = median;
                bool roomOk = cover >= 99.5f;
                if (!roomOk) ok = false;
                d.AppendLine(string.Format("{0,-18} ceiling over {1,5:F1}% of {2} samples; median {3:F2} m, min {4:F2}, max {5:F2}", r.roomName, cover, total, median, hs.Count > 0 ? hs[0] : -1f, hs.Count > 0 ? hs[hs.Count - 1] : -1f));
            }
            Check("Every room has a ceiling over its whole floor (up-rays from a 0.5 m grid hit something)", ok, d.ToString());
            return result;
        }

        static void CheckHeights(List<IronRoom> rooms, Dictionary<string, float> h)
        {
            var d = new StringBuilder(); bool ok = true;
            foreach (var k in new[] { "prison", "guard room", "kitchen", "treasure vault" })
            {
                var r = rooms.First(x => x.kind == k); float v = h[r.roomName];
                bool low = v > 0 && v <= 3.6f; if (!low) ok = false;
                d.AppendLine(string.Format("{0,-18} {1:F2} m  {2}", r.roomName, v, low ? "low" : "NOT LOW"));
            }
            foreach (var k in new[] { "dining hall", "throne room" })
            {
                var r = rooms.First(x => x.kind == k); float v = h[r.roomName];
                bool tall = v >= 8f; if (!tall) ok = false;
                d.AppendLine(string.Format("{0,-18} {1:F2} m  {2}", r.roomName, v, tall ? "tall" : "NOT TALL"));
            }
            var throne = rooms.First(x => x.kind == "throne room"); float th = h[throne.roomName];
            foreach (var r in rooms) if (r != throne && h[r.roomName] >= th - 0.05f) { ok = false; d.AppendLine(r.roomName + " is as tall as or taller than the throne room (" + h[r.roomName].ToString("F2") + " vs " + th.ToString("F2") + ")"); }
            d.AppendLine("all rooms: " + string.Join(", ", rooms.Select(r => r.roomName + " " + h[r.roomName].ToString("F1"))));
            Check("Prison, guard room, kitchen and vault are low; dining hall and throne room are tall; no room is taller than the throne room", ok, d.ToString());
        }

        // ---------------------------------------------------------------- doors
        /// <summary>Clear width of an opening at its centre: sweep a small sphere sideways at three heights until it hits a solid.
        /// The door's own leaf or plug is ignored (it is the frame's opening that is measured); the great doors of the large frames are not.</summary>
        public static float MeasureWidth(IronDoor door, out float clearHeight, bool ignoreLeaf = true)
        {
            var u = door.transform.right; var p = door.transform.position;
            var disabled = new List<Collider>();
            if (ignoreLeaf) foreach (var c in door.GetComponentsInChildren<Collider>()) { if (c.enabled && (c.GetComponentInParent<DoorInteract>() != null || c.GetComponentInParent<SecretDoor>() != null)) { c.enabled = false; disabled.Add(c); } }
            float best = 0f;
            foreach (float hgt in new[] { 0.5f, 1.0f, 1.6f })
            {
                float left = 0f, right = 0f;
                for (float t = 0f; t < 4f; t += 0.02f) { if (Physics.CheckSphere(p + Vector3.up * hgt + u * t, 0.06f, Mask, QueryTriggerInteraction.Ignore)) break; right = t; }
                for (float t = 0f; t < 4f; t += 0.02f) { if (Physics.CheckSphere(p + Vector3.up * hgt - u * t, 0.06f, Mask, QueryTriggerInteraction.Ignore)) break; left = t; }
                float w = left + right;
                if (hgt == 0.5f || w < best) best = w;
            }
            clearHeight = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.up, out var hit, 20f, Mask, QueryTriggerInteraction.Ignore) ? hit.point.y - p.y : 99f;
            foreach (var c in disabled) c.enabled = true;
            return best;
        }

        static Dictionary<IronDoor, float> CheckDoors(List<IronRoom> rooms, List<IronDoor> doors)
        {
            var widths = new Dictionary<IronDoor, float>();
            var d = new StringBuilder(); bool ok = true;
            foreach (var door in doors)
            {
                var a = rooms.FirstOrDefault(r => r.roomName == door.roomA); var b = rooms.FirstOrDefault(r => r.roomName == door.roomB);
                if (a == null || b == null) { ok = false; d.AppendLine(door.name + ": unknown room"); continue; }
                var p = door.transform.position; var n = door.transform.forward;
                bool onA = a.Contains(p - n * 0.8f + Vector3.up * 0.5f), onB = b.Contains(p + n * 0.8f + Vector3.up * 0.5f);
                bool hidden = door.state == "hidden";
                float w = MeasureWidth(door, out float h);
                widths[door] = w;
                bool blockedNow = hidden && Physics.CheckSphere(p + Vector3.up * 1.0f, 0.2f, Mask, QueryTriggerInteraction.Ignore);
                bool shutOk = door.state != "shut" || (door.GetComponentInChildren<DoorInteract>() != null && door.GetComponentInChildren<Collider>() != null);
                bool pass = onA && onB && w >= 0.8f && h >= 1.85f && (!hidden || blockedNow) && shutOk;
                if (!pass) ok = false;
                d.AppendLine(string.Format("{0,-40} {1,-6} at {2}  width {3:F2} m  clear height {4:F2} m  sides {5}/{6}{7}{8}", door.name, door.state, F(p), w, h, onA ? "ok" : "BAD", onB ? "ok" : "BAD",
                    hidden ? (blockedNow ? "  plug solid until used" : "  PLUG NOT SOLID") : "", door.state == "shut" ? (shutOk ? "  leaf+DoorInteract" : "  NO LEAF/DoorInteract") : ""));
            }
            Check("Every listed door sits between its two rooms, is wide (>= 0.8 m) and tall (>= 1.85 m) enough for the player, shut doors have a usable leaf, the hidden door is solid until used", ok, d.ToString());
            return widths;
        }

        /// <summary>Finds every opening along shared walls by linecasting across them and matches each to a listed door.</summary>
        static void CheckOpenings(List<IronRoom> rooms, List<IronDoor> doors)
        {
            var d = new StringBuilder(); bool ok = true; int pairs = 0, openings = 0;
            for (int i = 0; i < rooms.Count; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    var a = rooms[i]; var b = rooms[j];
                    if (a.isGallery && a.parentRoom == b.roomName || b.isGallery && b.parentRoom == a.roomName) continue;
                    var ba = a.FloorBounds(); var bb = b.FloorBounds();
                    // shared vertical boundary (x) or horizontal (z)
                    var segs = new List<(Vector3 from, Vector3 to, Vector3 normal)>();
                    if (Mathf.Abs(ba.max.x - bb.min.x) < 0.6f || Mathf.Abs(bb.max.x - ba.min.x) < 0.6f)
                    {
                        float x = Mathf.Abs(ba.max.x - bb.min.x) < 0.6f ? (ba.max.x + bb.min.x) / 2f : (bb.max.x + ba.min.x) / 2f;
                        float z0 = Mathf.Max(ba.min.z, bb.min.z), z1 = Mathf.Min(ba.max.z, bb.max.z);
                        if (z1 - z0 > 0.5f) segs.Add((new Vector3(x, 0, z0), new Vector3(x, 0, z1), ba.center.x < bb.center.x ? Vector3.right : Vector3.left));
                    }
                    if (Mathf.Abs(ba.max.z - bb.min.z) < 0.6f || Mathf.Abs(bb.max.z - ba.min.z) < 0.6f)
                    {
                        float z = Mathf.Abs(ba.max.z - bb.min.z) < 0.6f ? (ba.max.z + bb.min.z) / 2f : (bb.max.z + ba.min.z) / 2f;
                        float x0 = Mathf.Max(ba.min.x, bb.min.x), x1 = Mathf.Min(ba.max.x, bb.max.x);
                        if (x1 - x0 > 0.5f) segs.Add((new Vector3(x0, 0, z), new Vector3(x1, 0, z), ba.center.z < bb.center.z ? Vector3.forward : Vector3.back));
                    }
                    if (segs.Count == 0) continue;
                    pairs++;
                    var listed = doors.Where(dr => (dr.roomA == a.roomName && dr.roomB == b.roomName) || (dr.roomA == b.roomName && dr.roomB == a.roomName)).ToList();
                    var unexplained = new List<Vector3>();
                    var explained = new HashSet<IronDoor>();
                    foreach (var s in segs)
                    {
                        float len = Vector3.Distance(s.from, s.to); var dir = (s.to - s.from).normalized;
                        for (float t = 0.15f; t <= len - 0.15f; t += 0.2f)
                        {
                            var basePt = s.from + dir * t;
                            // sample at each room's floor level; where one room is a raised deck, only its own level counts (the other level belongs to the room beneath it)
                            var levels = a.isGallery ? new[] { a } : b.isGallery ? new[] { b } : new[] { a, b };
                            foreach (var room in levels)
                                foreach (float h in new[] { 0.5f, 1.3f })
                                {
                                    var pt = basePt; pt.y = room.floorY + h;
                                    var from = pt - s.normal * 0.9f; var to = pt + s.normal * 0.9f;
                                    if (!Physics.Linecast(from, to, Mask, QueryTriggerInteraction.Ignore) && !Physics.Linecast(to, from, Mask, QueryTriggerInteraction.Ignore))
                                    {
                                        var near = listed.FirstOrDefault(dr => Vector2.Distance(new Vector2(dr.transform.position.x, dr.transform.position.z), new Vector2(pt.x, pt.z)) < 2.6f && Mathf.Abs(dr.transform.position.y - room.floorY) < 1.5f);
                                        if (near != null) explained.Add(near); else unexplained.Add(pt);
                                    }
                                }
                        }
                    }
                    openings += explained.Count;
                    if (unexplained.Count > 0) { ok = false; d.AppendLine(a.roomName + " | " + b.roomName + ": " + unexplained.Count + " unlisted open sample(s), e.g. " + F(unexplained[0])); }
                    foreach (var dr in listed)
                    {
                        bool open = explained.Contains(dr);
                        d.AppendLine(string.Format("{0,-18} | {1,-18} {2,-6} {3}", a.roomName, b.roomName, dr.state, open ? "opening found and listed" : (dr.state == "open" ? "LISTED OPEN DOORWAY BUT BLOCKED" : "blocked while " + dr.state + " (as expected)")));
                        if (dr.state == "open" && !open) ok = false;
                    }
                    if (listed.Count == 0) d.AppendLine(string.Format("{0,-18} | {1,-18} shared wall, no door, no opening", a.roomName, b.roomName));
                }
            d.AppendLine(pairs + " adjacent room pairs scanned (0.2 m steps, two heights, both floor levels), " + openings + " listed doors found open");
            Check("Every opening between rooms is a listed door or doorway (no unlisted gaps in shared walls)", ok, d.ToString());
        }

        // ---------------------------------------------------------------- armory, way round, gallery, stage, vault
        static void CheckArmory(List<IronRoom> rooms, List<IronDoor> doors, List<IronMarker> markers)
        {
            var d = new StringBuilder(); bool ok = true;
            var armDoors = doors.Where(x => x.roomA == "Armory" || x.roomB == "Armory").ToList();
            var guarded = armDoors.FirstOrDefault(x => x.roomA == "Entry hall" || x.roomB == "Entry hall");
            var back = armDoors.FirstOrDefault(x => x.roomA == "Dining hall" || x.roomB == "Dining hall");
            d.AppendLine("doors into the armory: " + string.Join("; ", armDoors.Select(x => x.name + " (" + x.state + ")")));
            if (armDoors.Count != 2 || guarded == null || back == null) { ok = false; d.AppendLine("expected exactly two: one from the entry hall, one from the dining hall"); }
            var gallery = rooms.FirstOrDefault(r => r.isGallery);
            if (back != null && gallery != null)
            {
                var p = back.transform.position; var inside = gallery.FloorRects().Any(rc => rc.Contains(new Vector2(p.x, p.z)));
                bool under = inside && gallery.floorY > p.y + 2f;
                // and the deck is really overhead: cast up from the door
                bool overhead = Physics.Raycast(p + Vector3.up * 0.3f + back.transform.forward * -0.0f, Vector3.up, out var h, 20f, Mask, QueryTriggerInteraction.Ignore) && h.point.y < gallery.floorY + 0.5f && h.point.y > p.y + 2f;
                d.AppendLine("back door at " + F(p) + ": under the gallery deck outline = " + inside + ", deck " + (gallery.floorY - p.y).ToString("F2") + " m above it, something overhead at " + (overhead ? h.point.y.ToString("F2") + " m" : "none") );
                if (!under || !overhead) ok = false;
            }
            if (guarded != null)
            {
                var entry = rooms.First(r => r.roomName == "Entry hall"); var arm = rooms.First(r => r.roomName == "Armory");
                var toEntry = (entry.FloorBounds().center - arm.FloorBounds().center); toEntry.y = 0; toEntry.Normalize();
                var ranged = markers.Where(m => m.kind == "ranged" && IronRoom.RoomAt(m.transform.position)?.roomName == "Armory").ToList();
                int near = 0, facing = 0;
                foreach (var m in ranged)
                {
                    float dist = Vector3.Distance(m.transform.position, guarded.transform.position);
                    bool f = Vector3.Dot(m.transform.forward, toEntry) > 0.7f;
                    if (dist < 7f) near++; if (f) facing++;
                    d.AppendLine("ranged guard at " + F(m.transform.position) + " " + dist.ToString("F1") + " m from the guarded entrance, facing the entry hall: " + f);
                }
                var melee = markers.Count(m => m.kind == "melee" && IronRoom.RoomAt(m.transform.position)?.roomName == "Armory");
                d.AppendLine(ranged.Count + " ranged behind the entrance (" + near + " within 7 m, " + facing + " facing the entry hall), " + melee + " melee elsewhere in the armory");
                if (ranged.Count < 2 || near < 2 || facing < 2 || melee < 1) ok = false;
                // the entrance is seen from the entry hall: a line from the start to the doorway is unobstructed
                var player = GameObject.Find("IronCitadelPlayer");
                if (player != null)
                {
                    var eye = player.transform.position + Vector3.up * 1.6f; var target = guarded.transform.position + Vector3.up * 1.6f;
                    bool clear = !Physics.Linecast(eye, target - (target - eye).normalized * 0.6f, Mask, QueryTriggerInteraction.Ignore);
                    d.AppendLine("the guarded entrance is in clear sight from the start (" + Vector3.Distance(eye, target).ToString("F1") + " m): " + clear);
                    if (!clear) ok = false;
                }
            }
            Check("The armory has two ways in: the guarded entrance seen from the entry hall, and a back door from the dining hall beneath the gallery", ok, d.ToString());
        }

        static List<string> DoorPath(List<IronDoor> doors, string from, string to, Func<IronDoor, bool> allowDoor, Func<string, bool> allowRoom)
        {
            var prev = new Dictionary<string, string>(); var q = new Queue<string>(); q.Enqueue(from); prev[from] = null;
            while (q.Count > 0)
            {
                var r = q.Dequeue(); if (r == to) break;
                foreach (var dr in doors)
                {
                    if (!allowDoor(dr)) continue;
                    string other = dr.roomA == r ? dr.roomB : dr.roomB == r ? dr.roomA : null;
                    if (other == null || prev.ContainsKey(other) || !allowRoom(other)) continue;
                    prev[other] = r; q.Enqueue(other);
                }
            }
            if (!prev.ContainsKey(to)) return null;
            var path = new List<string>(); for (var r = to; r != null; r = prev[r]) path.Add(r); path.Reverse(); return path;
        }

        static void CheckWayRound(List<IronDoor> doors)
        {
            var path = DoorPath(doors, "Entry hall", "Dining hall", dr => dr.state != "hidden", r => r != "Armory");
            Check("A way leads from the entry hall to the dining hall without passing through the armory (door graph)", path != null, path != null ? string.Join(" > ", path) : "no such path");
        }

        static void CheckGallery(List<IronRoom> rooms, List<IronDoor> doors)
        {
            var d = new StringBuilder(); bool ok = true;
            var g = rooms.FirstOrDefault(r => r.isGallery); var hall = rooms.First(r => r.kind == "dining hall");
            if (g == null) { Check("A gallery runs along one side of the dining hall", false, "no gallery room"); return; }
            var gb = g.FloorBounds(); var hb = hall.FloorBounds();
            float drop = g.floorY - hall.floorY;
            bool alongSide = (Mathf.Abs(gb.min.x - hb.min.x) < 0.3f || Mathf.Abs(gb.max.x - hb.max.x) < 0.3f) && gb.size.z > hb.size.z * 0.8f;
            d.AppendLine("deck " + drop.ToString("F2") + " m above the hall floor, outline x " + gb.min.x + ".." + gb.max.x + " z " + gb.min.z + ".." + gb.max.z + ", hall x " + hb.min.x + ".." + hb.max.x + " z " + hb.min.z + ".." + hb.max.z + ", along one long side: " + alongSide);
            if (drop < 4f || !alongSide) ok = false;
            // the rail: pieces named Rail under the gallery; their bounds give the edge line
            var rails = g.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Rail")).ToList();
            Bounds rb = new Bounds(); bool has = false; foreach (var r in rails) { if (!has) { rb = r.bounds; has = true; } else rb.Encapsulate(r.bounds); }
            d.AppendLine(rails.Count + " rail pieces, edge line x " + rb.center.x.ToString("F2") + " from z " + rb.min.z.ToString("F1") + " to " + rb.max.z.ToString("F1") + ", rail top " + (rb.max.y - g.floorY).ToString("F2") + " m above the deck");
            if (rails.Count == 0 || rb.max.y - g.floorY < 0.9f) ok = false;
            // the way round comes into the hall through this door; a door path from that side reaches the gallery without entering the hall
            var entryDoor = doors.FirstOrDefault(x => (x.roomA == "Dining hall" || x.roomB == "Dining hall") && (x.roomA == "Stair hall" || x.roomB == "Stair hall") && !x.roomA.Contains("Gallery") && !x.roomB.Contains("Gallery"));
            string entryRoom = entryDoor == null ? null : (entryDoor.roomA == "Dining hall" ? entryDoor.roomB : entryDoor.roomA);
            var path = entryRoom == null ? null : DoorPath(doors, entryRoom, g.roomName, dr => dr.state != "hidden", r => r != "Dining hall");
            d.AppendLine("the way round enters the hall from '" + entryRoom + "'; path to the gallery avoiding the hall floor: " + (path != null ? string.Join(" > ", path) : "NONE"));
            if (path == null) ok = false;
            // visibility of the hall floor from the rail (eye 1.6 m above the deck, 0.5 m inside the rail), rail colliders ignored (open balusters)
            var railCols = g.GetComponentsInChildren<Collider>().Where(c => c.name.StartsWith("Rail") || c.transform.parent.name.StartsWith("Rail")).ToList();
            foreach (var c in railCols) c.enabled = false;
            float railX = rb.center.x; float inside = gb.center.x < hb.center.x ? -0.6f : 0.6f;
            var samples = new List<Vector3>();
            float fx0 = gb.center.x < hb.center.x ? gb.max.x + 0.5f : hb.min.x + 0.5f, fx1 = gb.center.x < hb.center.x ? hb.max.x - 0.5f : gb.min.x - 0.5f;
            for (float x = fx0; x <= fx1; x += 1f) for (float z = hb.min.z + 0.5f; z <= hb.max.z - 0.5f; z += 1f) samples.Add(new Vector3(x, hall.floorY + 1.0f, z));
            var seen = new HashSet<int>(); float bestSingle = 0;
            foreach (float z in new[] { hb.min.z + hb.size.z * 0.2f, hb.center.z, hb.max.z - hb.size.z * 0.2f })
            {
                var eye = new Vector3(railX + inside, g.floorY + 1.6f, z); int vis = 0;
                for (int i = 0; i < samples.Count; i++)
                    if (!Physics.Linecast(eye, samples[i], Mask, QueryTriggerInteraction.Ignore)) { vis++; seen.Add(i); }
                float frac = (float)vis / samples.Count; bestSingle = Mathf.Max(bestSingle, frac);
                d.AppendLine("from the rail at z " + z.ToString("F1") + ": " + (100f * frac).ToString("F0") + "% of " + samples.Count + " hall floor points (1 m grid, 1 m up) in line of sight");
            }
            float union = (float)seen.Count / samples.Count;
            d.AppendLine("union over the three rail points: " + (100f * union).ToString("F0") + "%");
            foreach (var c in railCols) c.enabled = true;
            if (bestSingle < 0.5f) ok = false;
            Check("A gallery runs along one side of the dining hall, high above the floor, reachable from where the way round comes in without crossing the hall floor, with much of the floor seen from its rail", ok, d.ToString());
        }

        static void CheckStage(List<IronRoom> rooms, List<IronDoor> doors)
        {
            var d = new StringBuilder(); bool ok = true;
            var throne = GameObject.Find("Throne"); var room = rooms.First(r => r.kind == "throne room");
            if (throne == null) { Check("The throne stands on a stage with steps", false, "no object named Throne"); return; }
            var tp = throne.transform.position;
            float stageTop = Physics.Raycast(tp + new Vector3(2f, 3f, -0.7f), Vector3.down, out var h, 10f, Mask, QueryTriggerInteraction.Ignore) ? h.point.y : -99f;
            float rise = stageTop - room.floorY;
            d.AppendLine("throne at " + F(tp) + ", stage top in front of it " + stageTop.ToString("F2") + " = " + rise.ToString("F2") + " m above the room floor; throne inside the throne room: " + (IronRoom.RoomAt(tp)?.roomName == room.roomName));
            if (rise < 0.6f || IronRoom.RoomAt(tp)?.roomName != room.roomName) ok = false;
            // steps: walk the surface from the throne toward the room's entrance; the drop must happen in risers <= 0.3 m or ramps
            var entrance = doors.FirstOrDefault(x => x.roomA == room.roomName || x.roomB == room.roomName);
            var target = entrance != null ? entrance.transform.position : room.FloorBounds().center;
            var dir = (target - tp); dir.y = 0; dir.Normalize();
            float prev = stageTop, maxStep = 0f, descended = 0f; int n = 0; bool reachedFloor = false; float startDesc = -1f, endDesc = -1f;
            for (float t = 1.6f; t < 15f; t += 0.25f)
            {
                var o = tp + dir * t + Vector3.up * 3f;
                if (!Physics.Raycast(o, Vector3.down, out var s, 6f, Mask, QueryTriggerInteraction.Ignore)) continue;
                float y = s.point.y; float step = prev - y; maxStep = Mathf.Max(maxStep, Mathf.Abs(step));
                if (step > 0.02f) { if (startDesc < 0) startDesc = t; endDesc = t; descended += step; }
                prev = y; n++;
                if (Mathf.Abs(y - room.floorY) < 0.05f) { reachedFloor = true; break; }
            }
            d.AppendLine("walking from the throne toward the entrance: descends " + descended.ToString("F2") + " m between " + startDesc.ToString("F1") + " and " + endDesc.ToString("F1") + " m out, largest change per 0.25 m " + maxStep.ToString("F2") + " m, reaches the floor: " + reachedFloor);
            if (!reachedFloor || maxStep > 0.3f || descended < 0.6f) ok = false;
            Check("The throne stands on a stage with steps (raised top, walkable descent to the floor)", ok, d.ToString());
        }

        static void CheckVault(List<IronDoor> doors)
        {
            var d = new StringBuilder(); bool ok = true;
            var v = doors.Where(x => x.roomA == "Treasure vault" || x.roomB == "Treasure vault").ToList();
            d.AppendLine("doors into the vault: " + string.Join("; ", v.Select(x => x.name + " (" + x.state + ")")));
            if (v.Count != 1 || v[0].state != "hidden") ok = false;
            if (v.Count == 1)
            {
                var sd = v[0].GetComponentInChildren<SecretDoor>();
                d.AppendLine("hidden door has a SecretDoor (opens on E): " + (sd != null) + "; the vault lies on no way through: it has one door, so no path can pass through it");
                if (sd == null) ok = false;
            }
            Check("The vault has exactly one way in, hidden, and lies on no way through", ok, d.ToString());
        }

        // ---------------------------------------------------------------- occluders, markers, player
        static void CheckOccluders(List<IronRoom> rooms)
        {
            var d = new StringBuilder(); int tallTotal = 0, lowTotal = 0; bool ok = true;
            var perRoom = new Dictionary<string, (int tall, int low)>();
            foreach (var r in rooms)
            {
                if (r.isGallery) continue;
                var fb = r.FloorBounds(); int tall = 0, low = 0;
                var props = r.transform.Find("Props");
                var cols = new List<Collider>();
                if (props != null) cols.AddRange(props.GetComponentsInChildren<Collider>());
                var stage = r.transform.Find("Stage"); if (stage != null) cols.AddRange(stage.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.name != "StageSteps"));
                foreach (var c in cols)
                {
                    if (c.isTrigger) continue;
                    var b = c.bounds;
                    if (b.min.x < fb.min.x + 0.45f || b.max.x > fb.max.x - 0.45f || b.min.z < fb.min.z + 0.45f || b.max.z > fb.max.z - 0.45f) continue; // against a wall
                    if (b.min.y > r.floorY + 0.4f) continue; // hanging or on a table
                    float hgt = b.max.y - r.floorY; float ext = Mathf.Min(b.size.x, b.size.z);
                    if (hgt >= 1.7f && ext >= 0.45f) tall++;
                    else if (hgt >= 0.8f && hgt < 1.7f && ext >= 0.6f) low++;
                }
                perRoom[r.roomName] = (tall, low); tallTotal += tall; lowTotal += low;
                d.AppendLine(string.Format("{0,-18} tall {1,3}  low {2,3}", r.roomName, tall, low));
            }
            d.AppendLine("level total: tall " + tallTotal + ", low " + lowTotal + " (tall: >= 1.7 m high; low: 0.8..1.7 m; both >= 0.45/0.6 m across, free-standing away from walls)");
            if (tallTotal < 5 || lowTotal < 5) ok = false;
            foreach (var k in new[] { "Dining hall", "Armory" }) if (!perRoom.ContainsKey(k) || perRoom[k].tall < 1 || perRoom[k].low < 1) { ok = false; d.AppendLine(k + " lacks a tall or a low occluder"); }
            Check("The level has tall and low free-standing occluders to hide behind (dining hall and armory included)", ok, d.ToString());
        }

        static void CheckMarkers(List<IronRoom> rooms, List<IronMarker> markers)
        {
            var d = new StringBuilder(); bool ok = true;
            var byRoom = new Dictionary<string, List<IronMarker>>();
            foreach (var m in markers)
            {
                var r = IronRoom.RoomAt(m.transform.position); string rn = r == null ? "(none)" : r.roomName;
                if (rn != m.room) { ok = false; d.AppendLine(m.name + " says room '" + m.room + "' but stands in '" + rn + "'"); }
                if (!byRoom.ContainsKey(rn)) byRoom[rn] = new List<IronMarker>(); byRoom[rn].Add(m);
            }
            int Count(string room, string kind) => byRoom.ContainsKey(room) ? byRoom[room].Count(m => m.kind == kind) : 0;
            int Enemies(string room) => byRoom.ContainsKey(room) ? byRoom[room].Count(m => m.IsEnemy) : 0;
            foreach (var kv in byRoom.OrderBy(k => k.Key)) d.AppendLine(string.Format("{0,-18} {1}", kv.Key, string.Join(", ", kv.Value.GroupBy(m => m.kind).Select(g => g.Count() + " " + g.Key))));
            int hallGroup = Enemies("Dining hall") + Enemies("Gallery");
            int largestOther = rooms.Where(r => r.roomName != "Dining hall" && r.roomName != "Gallery").Select(r => Enemies(r.roomName)).DefaultIfEmpty(0).Max();
            d.AppendLine("dining hall group (floor + gallery) " + hallGroup + " vs largest other room " + largestOther);
            if (hallGroup <= largestOther) ok = false;
            if (Count("Gallery", "ranged") < 2) { ok = false; d.AppendLine("gallery needs ranged enemies"); }
            if (Count("Throne room", "warlord") != 1 || Count("Throne room", "melee") < 2) { ok = false; d.AppendLine("throne room needs the warlord and guards"); }
            if (Enemies("Entry hall") != 0) { ok = false; d.AppendLine("the entry hall must hold no one"); }
            if (Count("Armory", "gear") != 1 || Count("Treasure vault", "hoard") != 1 || markers.Count(m => !m.IsEnemy) != 2) { ok = false; d.AppendLine("pickups: exactly the armory's best gear and the vault's hoard"); }
            Check("Defenders and pickups stand where the brief puts them (markers measured against the rooms they stand in)", ok, d.ToString());
        }

        static void CheckPlayer(List<IronRoom> rooms)
        {
            var d = new StringBuilder(); bool ok = true;
            var player = GameObject.Find("IronCitadelPlayer");
            if (player == null) { Check("The player starts in the entry hall with the shipped controller", false, "no IronCitadelPlayer"); return; }
            var cc = player.GetComponentInChildren<CharacterController>();
            var room = IronRoom.RoomAt(player.transform.position + Vector3.up * 0.5f);
            d.AppendLine("player root at " + F(player.transform.position) + " in '" + (room == null ? "(none)" : room.roomName) + "', facing " + F(player.transform.forward));
            bool sameAsPrefab = cc != null && Mathf.Abs(cc.radius - 0.35f) < 0.001f && Mathf.Abs(cc.height - 1.8f) < 0.001f && Mathf.Abs(cc.stepOffset - 0.3f) < 0.001f && Mathf.Abs(cc.slopeLimit - 45f) < 0.01f;
            d.AppendLine("CharacterController r " + cc?.radius + " h " + cc?.height + " step " + cc?.stepOffset + " slope " + cc?.slopeLimit + " (unchanged from the shipped prefab: " + sameAsPrefab + ")");
            var src = PrefabUtility.GetCorrespondingObjectFromSource(player);
            d.AppendLine("instance of " + (src != null ? AssetDatabase.GetAssetPath(src) : "(not a prefab instance)"));
            if (room == null || room.kind != "entry hall" || !sameAsPrefab || src == null) ok = false;
            Check("The player starts in the entry hall with the shipped controller", ok, d.ToString());
        }

        // ---------------------------------------------------------------- play mode: the walks
        [InitializeOnLoadMethod]
        static void Hook() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

        static void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            EditorApplication.update += WaitForPlayer;
        }

        static float _waitStart;
        static void WaitForPlayer()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= WaitForPlayer; return; }
            var cc = UnityEngine.Object.FindAnyObjectByType<CharacterController>();
            if (cc == null) return;
            if (_waitStart == 0f) { _waitStart = (float)EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - _waitStart < 1.0) return; // let the scene settle for a second
            EditorApplication.update -= WaitForPlayer; _waitStart = 0f;
            var driver = cc.gameObject.AddComponent<WalkDriver>();
            driver.walks = BuildWalks();
            driver.onAllDone = OnWalksDone;
            Debug.Log("[IronCitadelChecks] walks started: " + driver.walks.Count);
        }

        static WalkDriver.Step S(float x, float z, string interact = null, string note = null, float reach = 0.45f, bool sprint = true) => new WalkDriver.Step { target = new Vector3(x, 0, z), interact = interact, note = note, reach = reach, sprint = sprint };

        /// <summary>The five walks. Waypoints are read from the scene's door and marker objects where they matter, so they follow the built scene.</summary>
        static List<WalkDriver.Walk> BuildWalks()
        {
            Vector3 D(string name) { var g = GameObject.Find(name); return g != null ? g.transform.position : Vector3.zero; }
            var eg = D("Door Entry hall - Guard room"); var pt = D("Door Prison - Torture chamber"); var tc = D("Door Torture chamber - Crypt"); var cs = D("Door Crypt - Stair hall");
            var sd = D("Door Stair hall - Dining hall"); var sg = D("Door Stair hall - Gallery"); var ad = D("Door Armory - Dining hall"); var cv = D("Door Crypt - Treasure vault");
            var dl = D("Door Dining hall - Library"); var la = D("Door Library - Alchemist's study"); var ash = D("Door Alchemist's study - Shrine"); var sa = D("Door Shrine - Antechamber"); var at = D("Door Antechamber - Throne room");
            var gp = D("Door Guard room - Prison");
            var gear = D("Marker gear (Armory)"); var hoard = D("Marker hoard (Treasure vault)"); var throne = D("Throne");
            var hall = UnityEngine.Object.FindObjectsByType<IronRoom>(FindObjectsSortMode.None).First(r => r.kind == "dining hall");
            var gallery = UnityEngine.Object.FindObjectsByType<IronRoom>(FindObjectsSortMode.None).First(r => r.isGallery);
            var hb = hall.FloorBounds(); var gb = gallery.FloorBounds();
            float openFloorX = gb.max.x + 0.5f; // east of this is the open hall floor (the strip under the deck is not)

            var walks = new List<WalkDriver.Walk>();
            // 1: the way round
            var w1 = new WalkDriver.Walk { name = "The way round: start to the dining hall without passing through the armory", mustEndIn = "Dining hall", violation = p => IronRoom.RoomAt(p)?.roomName == "Armory" ? "entered the armory" : null };
            w1.steps.AddRange(new[] {
                S(eg.x + 1.6f, eg.z, "Door Entry hall - Guard room", "to the side door of the entry hall"),
                S(eg.x - 1.6f, eg.z, null, "through into the guard room"), S(-21.2f, eg.z), S(-21.2f, gp.z - 2.2f), S(gp.x, gp.z - 1.5f), S(gp.x, gp.z + 2.2f, null, "through the open barred door into the prison"),
                S(-20.3f, 2.4f), S(-20.3f, 12.3f, null, "along the cell corridor"), S(pt.x, pt.z - 1.7f, "Door Prison - Torture chamber", "the door to the torture chamber"),
                S(pt.x, pt.z + 1.6f), S(-20.5f, 20f), S(tc.x, tc.z - 1.5f), S(tc.x, tc.z + 1.6f, null, "into the crypt"),
                S(tc.x + 2.5f, 27.3f), S(cs.x - 1.6f, cs.z), S(cs.x + 1.6f, cs.z, null, "into the stair hall"), S(sd.x - 2.5f, sd.z), S(sd.x + 1.8f, sd.z, null, "into the dining hall beneath the gallery") });
            walks.Add(w1);
            // 2: up to the gallery without crossing the hall floor
            var w2 = new WalkDriver.Walk { name = "Up to the gallery from where the way round comes into the hall, without crossing the hall floor", mustEndIn = "Gallery",
                violation = p => (IronRoom.RoomAt(p)?.roomName == "Dining hall" && p.x > openFloorX && p.y < gb.min.y - 1f) ? "stepped onto the open hall floor" : null };
            w2.steps.AddRange(new[] { S(sd.x - 2.5f, sd.z, null, "back into the stair hall"), S(-14.2f, 28.4f), S(-14.2f, 32.5f, null, "the foot of the stairs"), S(-11f, 32.5f, null, "up flight 1"), S(-7f, 32.5f, null, "the landing"), S(-2.5f, 32.5f, null, "up flight 2"), S(sg.x - 0.4f, sg.z, null, "the upper doorway", 0.5f), S(sg.x + 2.4f, sg.z, null, "onto the gallery deck"), S(2.5f, 30.5f) });
            walks.Add(w2);
            // 3: from the dining hall into the armory beneath the gallery, to its best gear
            var w3 = new WalkDriver.Walk { name = "From the dining hall into the armory beneath the gallery, to its best gear", mustEndIn = "Armory" };
            w3.steps.AddRange(new[] { S(sg.x + 2.0f, sg.z), S(sg.x - 1.0f, sg.z, null, "back through the upper doorway"), S(-7f, 32.5f), S(-11f, 32.5f), S(-14.2f, 32.5f), S(-14.2f, 28.4f), S(sd.x - 2.5f, sd.z), S(sd.x + 1.8f, sd.z, null, "into the hall beneath the gallery"),
                S(2.5f, 20f), S(2.5f, ad.z + 1.5f), S(ad.x + 2.0f, ad.z, "Door Armory - Dining hall", "the armory's back door"), S(ad.x - 1.6f, ad.z, null, "into the armory from behind"), S(-7.5f, ad.z), S(gear.x, gear.z, null, "the best gear", 0.8f) });
            walks.Add(w3);
            // 4: the hidden way into the vault and back
            var w4 = new WalkDriver.Walk { name = "The hidden way into the vault and back", mustEndIn = "Crypt" };
            w4.steps.AddRange(new[] { S(-7.5f, ad.z), S(ad.x - 1.6f, ad.z), S(ad.x + 2.0f, ad.z, null, "out through the back door"), S(2.5f, ad.z + 1.5f), S(2.5f, 20f), S(sd.x + 1.8f, sd.z), S(sd.x - 2.5f, sd.z), S(cs.x + 1.6f, cs.z), S(cs.x - 1.6f, cs.z, null, "back into the crypt"),
                S(cv.x + 4f, cv.z), S(cv.x + 1.7f, cv.z, "Door Crypt - Treasure vault", "the bricked-up doorway: use it"), S(cv.x - 1.5f, cv.z, null, "into the vault"), S(hoard.x + 0.3f, hoard.z, null, "the hoard", 0.8f), S(cv.x - 1.5f, cv.z), S(cv.x + 1.7f, cv.z, null, "and back out"), S(cv.x + 4f, cv.z) });
            walks.Add(w4);
            // 5: from the dining hall on to the throne
            var w5 = new WalkDriver.Walk { name = "From the dining hall on to the throne", mustEndIn = "Throne room", timeBudget = 300f };
            w5.steps.AddRange(new[] { S(cs.x - 1.6f, cs.z), S(cs.x + 1.6f, cs.z), S(sd.x - 2.5f, sd.z), S(sd.x + 1.8f, sd.z, null, "into the dining hall"), S(3f, 24f), S(3f, 17f), S(6.5f, 16.5f, null, "across the hall floor through the cross-aisle"), S(dl.x - 1.5f, dl.z), S(dl.x + 1.6f, dl.z, null, "into the library"),
                S(27.5f, 17.5f), S(la.x - 1.6f, la.z, "Door Library - Alchemist's study", "the study door"), S(la.x + 1.6f, la.z), S(ash.x, ash.z - 1.5f), S(ash.x, ash.z + 1.6f, null, "into the shrine"), S(36.6f, 25f), S(sa.x + 1.7f, sa.z, "Door Shrine - Antechamber", "the antechamber door"),
                S(sa.x - 1.6f, sa.z), S(at.x, at.z - 1.7f), S(at.x, at.z + 1.6f, null, "through the great doors into the throne room"), S(at.x, 40f), S(at.x, 49f), S(throne.x, throne.z - 1.5f, null, "up the steps to the throne", 0.5f) });
            walks.Add(w5);
            return walks;
        }

        static void OnWalksDone(string walkReport, bool allPassed)
        {
            EditorApplication.update += AfterWalks;
            _walkReport = walkReport; _walksPassed = allPassed; _afterPhase = 0; _afterStart = (float)EditorApplication.timeSinceStartup;
        }
        static string _walkReport; static bool _walksPassed; static int _afterPhase; static float _afterStart;

        /// <summary>After the walks: HUD and map checks in play mode, two screenshots, then write results and leave play mode.</summary>
        static void AfterWalks()
        {
            float t = (float)EditorApplication.timeSinceStartup - _afterStart;
            var map = UnityEngine.Object.FindAnyObjectByType<OverviewMap>(); var hud = UnityEngine.Object.FindAnyObjectByType<LevelHud>();
            if (_afterPhase == 0)
            {
                var extra = new StringBuilder();
                extra.AppendLine("PLAY-MODE CHECKS");
                var cc = UnityEngine.Object.FindAnyObjectByType<CharacterController>();
                var room = cc != null ? IronRoom.RoomAt(cc.transform.position) : null;
                string hudRoom = hud != null ? hud.CurrentRoomName : "(no HUD)";
                bool hudOk = hud != null && room != null && hudRoom == room.roomName;
                extra.AppendLine((hudOk ? "[PASS] " : "[FAIL] ") + "HUD names the room the player is in: HUD says '" + hudRoom + "', player stands in '" + (room == null ? "(none)" : room.roomName) + "'");
                bool timeOk = hud != null && System.Text.RegularExpressions.Regex.IsMatch(hud.GetComponentInChildren<UnityEngine.UI.Text>()?.transform.parent.GetComponentsInChildren<UnityEngine.UI.Text>().FirstOrDefault(x => x.text.StartsWith("Time"))?.text ?? "", @"^Time \d\d:\d\d$");
                extra.AppendLine((timeOk ? "[PASS] " : "[FAIL] ") + "HUD shows elapsed time as mm:ss (" + Time.timeSinceLevelLoad.ToString("F0") + " s into the session)");
                bool announced = ThroneTrigger.Reached && hud != null && hud.AnnouncementText.Length > 0;
                extra.AppendLine((announced ? "[PASS] " : "[FAIL] ") + "Reaching the throne is announced: trigger fired at " + ThroneTrigger.ReachedAt.ToString("F1") + " s, HUD text '" + (hud != null ? hud.AnnouncementText : "") + "'");
                bool mapOk = false; string mapDetail = "no OverviewMap";
                if (map != null)
                {
                    map.Set(true);
                    var cam = map.MapCamera; var lb = map.LevelBounds;
                    bool ceilHidden = (cam.cullingMask & (1 << IronCitadelBuilder.CeilingLayer)) == 0;
                    float halfH = cam.orthographicSize, halfW = cam.orthographicSize * cam.aspect;
                    bool covers = lb.extents.x <= halfW + 0.01f && lb.extents.z <= halfH + 0.01f;
                    mapOk = map.IsOn && cam.enabled && cam.orthographic && ceilHidden && covers && Vector3.Dot(cam.transform.forward, Vector3.down) > 0.99f;
                    mapDetail = "M toggles an orthographic top-down camera: on " + map.IsOn + ", looking down " + (Vector3.Dot(cam.transform.forward, Vector3.down) > 0.99f) + ", ceilings layer culled " + ceilHidden + ", view half-size " + halfW.ToString("F1") + " x " + halfH.ToString("F1") + " m covers level extents " + lb.extents.x.ToString("F1") + " x " + lb.extents.z.ToString("F1") + " m: " + covers;
                }
                extra.AppendLine((mapOk ? "[PASS] " : "[FAIL] ") + "M overview map of the whole level with the ceilings hidden: " + mapDetail);
                _playReport = extra.ToString(); _afterPhase = 1; _afterStart = (float)EditorApplication.timeSinceStartup;
            }
            else if (_afterPhase == 1 && t > 0.6f) { ScreenCapture.CaptureScreenshot("Captures/overview_map_ingame.png"); _afterPhase = 2; }
            else if (_afterPhase == 2 && t > 1.6f) { if (map != null) map.Set(false); _afterPhase = 3; }
            else if (_afterPhase == 3 && t > 2.4f) { ScreenCapture.CaptureScreenshot("Captures/hud_throne_announced.png"); _afterPhase = 4; }
            else if (_afterPhase == 4 && t > 3.4f)
            {
                EditorApplication.update -= AfterWalks;
                string staticText = File.Exists(StaticPath) ? File.ReadAllText(StaticPath) : "(no static results)";
                var all = new StringBuilder();
                all.Append(staticText); all.AppendLine(); all.AppendLine("WALKS (play mode, shipped player driven by move/sprint input and the E script; a walk fails on any stall)");
                all.AppendLine(); all.Append(_walkReport); all.AppendLine(); all.Append(_playReport);
                int staticFails = staticText.Split('\n').Count(l => l.StartsWith("[FAIL]")); int playFails = _playReport.Split('\n').Count(l => l.StartsWith("[FAIL]"));
                int walkFails = _walkReport.Split('\n').Count(l => l.Contains("RESULT: FAIL"));
                all.AppendLine(); all.AppendLine("OVERALL: " + ((staticFails + playFails + walkFails) == 0 ? "ALL PASSED" : (staticFails + " static, " + walkFails + " walk and " + playFails + " play-mode failures")));
                File.WriteAllText(ResultsPath, all.ToString());
                if (File.Exists(StaticPath)) File.Delete(StaticPath);
                Debug.Log("[IronCitadelChecks] wrote " + ResultsPath + (_walksPassed ? " (walks passed)" : " (walks FAILED)"));
                EditorApplication.ExitPlaymode();
            }
        }
        static string _playReport;
    }

    /// <summary>Writes Map/map.json from the saved scene: room outlines from floor tiles, doors measured, gallery, stage, throne, markers.</summary>
    public static class MapExporter
    {
        static string N(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        static string V3(Vector3 v) => "[" + N(v.x) + ", " + N(v.y) + ", " + N(v.z) + "]";
        static string V2(Vector2 v) => "[" + N(v.x) + ", " + N(v.y) + "]";
        static string Poly(List<Vector2> pts) => "[" + string.Join(", ", pts.Select(V2)) + "]";
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>Outline loops of a union of axis-aligned rectangles that tile a grid of the given cell size.</summary>
        public static List<List<Vector2>> Outlines(List<Rect> rects, float cell = 5f)
        {
            var cells = new HashSet<(int, int)>(); var origin = new Vector2(rects.Min(r => r.xMin), rects.Min(r => r.yMin));
            foreach (var r in rects)
                for (float x = r.xMin + cell * 0.5f; x < r.xMax; x += cell) for (float z = r.yMin + cell * 0.5f; z < r.yMax; z += cell)
                        cells.Add((Mathf.FloorToInt((x - origin.x) / cell), Mathf.FloorToInt((z - origin.y) / cell)));
            var edges = new Dictionary<(int, int), (int, int)>(); // directed edges start -> end in grid coords (counter-clockwise around cells)
            foreach (var (i, j) in cells)
            {
                if (!cells.Contains((i, j - 1))) edges[(i, j)] = (i + 1, j);
                if (!cells.Contains((i + 1, j))) edges[(i + 1, j)] = (i + 1, j + 1);
                if (!cells.Contains((i, j + 1))) edges[(i + 1, j + 1)] = (i, j + 1);
                if (!cells.Contains((i - 1, j))) edges[(i, j + 1)] = (i, j);
            }
            var loops = new List<List<Vector2>>();
            while (edges.Count > 0)
            {
                var start = edges.Keys.First(); var loop = new List<(int, int)>(); var cur = start;
                while (edges.ContainsKey(cur)) { loop.Add(cur); var next = edges[cur]; edges.Remove(cur); cur = next; if (cur == start) break; }
                // drop collinear points
                var pts = new List<Vector2>();
                for (int k = 0; k < loop.Count; k++)
                {
                    var a = loop[(k + loop.Count - 1) % loop.Count]; var b = loop[k]; var c = loop[(k + 1) % loop.Count];
                    bool collinear = (a.Item1 == b.Item1 && b.Item1 == c.Item1) || (a.Item2 == b.Item2 && b.Item2 == c.Item2);
                    if (!collinear) pts.Add(new Vector2(origin.x + b.Item1 * cell, origin.y + b.Item2 * cell));
                }
                loops.Add(pts);
            }
            return loops;
        }

        public static string Export(string path)
        {
            if (SceneManager.GetActiveScene().path != IronCitadelBuilder.ScenePath) EditorSceneManager.OpenScene(IronCitadelBuilder.ScenePath);
            Physics.SyncTransforms();
            var rooms = UnityEngine.Object.FindObjectsByType<IronRoom>(FindObjectsSortMode.None).OrderBy(r => r.roomName).ToList();
            var doors = UnityEngine.Object.FindObjectsByType<IronDoor>(FindObjectsSortMode.None).OrderBy(d => d.name).ToList();
            var markers = UnityEngine.Object.FindObjectsByType<IronMarker>(FindObjectsSortMode.None).OrderBy(m => m.name).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("{");
            var player = GameObject.Find("IronCitadelPlayer");
            var pp = player != null ? player.transform.position : Vector3.zero; var pf = player != null ? player.transform.forward : Vector3.forward;
            sb.AppendLine("  \"start\": { \"position\": " + V3(pp) + ", \"facing\": " + V2(new Vector2(pf.x, pf.z)) + " },");
            sb.AppendLine("  \"rooms\": [");
            var roomLines = new List<string>();
            foreach (var r in rooms)
            {
                var loops = Outlines(r.FloorRects());
                float floorY = MeasuredFloor(r);
                roomLines.Add("    { \"name\": \"" + Esc(r.roomName) + "\", \"kind\": \"" + Esc(r.kind) + "\", \"state\": \"" + r.state + "\", \"floor_y\": " + N(floorY) + ", \"floor\": [" + string.Join(", ", loops.Select(Poly)) + "] }");
            }
            sb.AppendLine(string.Join(",\n", roomLines));
            sb.AppendLine("  ],");
            sb.AppendLine("  \"doors\": [");
            var doorLines = new List<string>();
            foreach (var d in doors)
            {
                float w = IronCitadelChecks.MeasureWidth(d, out _);
                var p = d.transform.position;
                doorLines.Add("    { \"rooms\": [\"" + Esc(d.roomA) + "\", \"" + Esc(d.roomB) + "\"], \"state\": \"" + d.state + "\", \"position\": " + V3(p) + ", \"width\": " + N(w) + " }");
            }
            sb.AppendLine(string.Join(",\n", doorLines));
            sb.AppendLine("  ],");
            var g = rooms.First(r => r.isGallery);
            var rails = g.GetComponentsInChildren<Renderer>().Where(x => x.name.StartsWith("Rail")).ToList();
            Bounds rb = new Bounds(); bool has = false; foreach (var x in rails) { if (!has) { rb = x.bounds; has = true; } else rb.Encapsulate(x.bounds); }
            var railLine = new List<Vector2> { new Vector2(rb.center.x, rb.min.z), new Vector2(rb.center.x, rb.max.z) };
            sb.AppendLine("  \"gallery\": { \"deck_y\": " + N(MeasuredFloor(g)) + ", \"deck\": [" + string.Join(", ", Outlines(g.FloorRects()).Select(Poly)) + "], \"rail\": " + Poly(railLine) + " },");
            // stage: the step blocks' footprint; top measured by a down-ray on the flat part
            var stageRoot = GameObject.Find("Stage");
            var blocks = stageRoot.GetComponentsInChildren<Renderer>().Where(x => x.name == "StageSteps").Select(x => Rect.MinMaxRect(Mathf.Round(x.bounds.min.x * 100f) / 100f, Mathf.Round(x.bounds.min.z * 100f) / 100f, Mathf.Round(x.bounds.max.x * 100f) / 100f, Mathf.Round(x.bounds.max.z * 100f) / 100f)).ToList();
            var throne = GameObject.Find("Throne").transform.position;
            // the stage top: a down-ray onto the flat strip beside the throne's plinth (triggers ignored)
            float top = Physics.Raycast(throne + new Vector3(2f, 3f, -0.7f), Vector3.down, out var th, 10f, ~(1 << IronCitadelBuilder.MapOnlyLayer), QueryTriggerInteraction.Ignore) ? Mathf.Round(th.point.y * 100f) / 100f : -1f;
            sb.AppendLine("  \"stage\": { \"top_y\": " + N(top) + ", \"outline\": " + Poly(Outlines(blocks)[0]) + " },");
            sb.AppendLine("  \"throne\": " + V3(throne) + ",");
            sb.AppendLine("  \"enemies\": [");
            sb.AppendLine(string.Join(",\n", markers.Where(m => m.IsEnemy).Select(m => "    { \"kind\": \"" + m.kind + "\", \"room\": \"" + Esc(RoomName(m)) + "\", \"position\": " + V3(Feet(m)) + ", \"facing\": " + V2(new Vector2(m.transform.forward.x, m.transform.forward.z)) + " }")));
            sb.AppendLine("  ],");
            sb.AppendLine("  \"pickups\": [");
            sb.AppendLine(string.Join(",\n", markers.Where(m => !m.IsEnemy).Select(m => "    { \"kind\": \"" + (m.kind == "gear" ? "best gear" : "hoard") + "\", \"room\": \"" + Esc(RoomName(m)) + "\", \"position\": " + V3(Feet(m)) + " }")));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        static string RoomName(IronMarker m) { var r = IronRoom.RoomAt(m.transform.position); return r == null ? "" : r.roomName; }
        /// <summary>A marker's position at floor level beneath it (the primitives float at body height).</summary>
        static Vector3 Feet(IronMarker m)
        {
            var p = m.transform.position;
            return Physics.Raycast(p, Vector3.down, out var h, 5f, ~(1 << IronCitadelBuilder.MapOnlyLayer), QueryTriggerInteraction.Ignore) && h.collider.transform.IsChildOf(m.transform) == false ? h.point : new Vector3(p.x, p.y - 1f, p.z);
        }
        /// <summary>The floor surface height measured by a down-ray at the room's centre (falls back to the nominal value).</summary>
        static float MeasuredFloor(IronRoom r)
        {
            var b = r.FloorBounds();
            foreach (var rect in r.FloorRects())
            {
                var o = new Vector3(rect.center.x, r.floorY + 0.5f, rect.center.y);
                if (Physics.Raycast(o, Vector3.down, out var h, 2f, ~(1 << IronCitadelBuilder.MapOnlyLayer), QueryTriggerInteraction.Ignore)) return Mathf.Round(h.point.y * 100f) / 100f;
            }
            return r.floorY;
        }
    }
}
