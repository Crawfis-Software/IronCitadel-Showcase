using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace IronCitadel.Checks
{
    /// <summary>
    /// Checks on the saved scene, each measured from the built geometry so it can fail.
    /// </summary>
    public static class StaticChecks
    {
        static List<string> _out;
        static int _pass, _fail;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        static void Check(string id, string what, bool ok, string measured)
        {
            if (ok) _pass++; else _fail++;
            _out.Add($"{(ok ? "PASS" : "FAIL")} {id} {what}");
            foreach (var line in measured.Split('\n')) if (line.Length > 0) _out.Add("    " + line);
        }

        static readonly string[] Kinds = { "entry hall", "armory", "guard room", "prison", "kitchen", "library", "alchemist's study", "forge", "treasure vault", "dining hall", "throne room", "crypt", "storeroom", "shrine", "torture chamber" };

        public static List<string> Run(out int pass, out int fail)
        {
            _out = new List<string>(); _pass = 0; _fail = 0;
            Physics.SyncTransforms();
            var rooms = SceneQuery.Rooms();
            SceneQuery.RoomInfo R(string kind) => rooms.First(r => r.kind == kind);
            var doors = Object.FindObjectsByType<DoorLink>().ToList();
            var markers = Object.FindObjectsByType<Marker>().ToList();
            var enemies = markers.Where(m => m.IsEnemy).ToList();
            SceneQuery.RoomInfo RoomOf(Vector3 p) => SceneQuery.RoomAt(rooms, p + Vector3.up * 0.1f);

            // S01 ---------------------------------------------------------------
            {
                var lines = rooms.Select(r => $"{r.name}: kind '{r.kind}', state '{r.state}', floor y {r.floorY:F2}, inner {r.x1 - r.x0:F2} x {r.z1 - r.z0:F2} m").ToList();
                var missing = Kinds.Where(k => rooms.Count(r => r.kind == k) != 1).ToList();
                bool namesUnique = rooms.Select(r => r.name).Distinct().Count() == rooms.Count;
                bool statesOk = rooms.All(r => new[] { "decayed", "looted", "stocked", "untouched" }.Contains(r.state))
                    && R("armory").state == "stocked" && R("treasure vault").state == "untouched"
                    && rooms.Where(r => r.kind != "armory" && r.kind != "treasure vault").All(r => r.state == "decayed" || r.state == "looted");
                bool extrasOk = rooms.All(r => Kinds.Contains(r.kind) || r.kind == "extra");
                Check("S01", "one room of each of the brief's 15 kinds, unique names, states as the brief says (armory stocked, vault untouched, the rest decayed or looted)",
                    missing.Count == 0 && namesUnique && statesOk && extrasOk,
                    string.Join("\n", lines) + (missing.Count > 0 ? "\nmissing or duplicated: " + string.Join(", ", missing) : ""));
            }

            // S02 openings vs doors ---------------------------------------------
            var leaves = doors.Where(d => d.leaf != null).Select(d => d.leaf).ToList();
            foreach (var l in leaves) l.SetActive(false);
            Physics.SyncTransforms();
            var openings = SceneQuery.Openings(rooms);
            var widths = doors.ToDictionary(d => d, d => SceneQuery.ClearWidth(d));
            foreach (var l in leaves) l.SetActive(true);
            Physics.SyncTransforms();
            var matched = new Dictionary<SceneQuery.Opening, DoorLink>();
            foreach (var o in openings)
            {
                var d = doors.FirstOrDefault(dl => ((dl.roomA == o.from.name && dl.roomB == o.toRoom) || (dl.roomB == o.from.name && dl.roomA == o.toRoom))
                    && Flat(dl.transform.position - o.center) < 1.0f && o.yMin <= dl.transform.position.y + 2.0f && o.yMax >= dl.transform.position.y);
                if (d != null) matched[o] = d;
            }
            {
                var unmatched = openings.Where(o => !matched.ContainsKey(o)).ToList();
                var unseen = doors.Where(d => !matched.Values.Contains(d)).ToList();
                var lines = new List<string>();
                lines.Add($"{openings.Count} wall gaps found scanning every room's walls (rays across the wall every 0.1 m x 0.25 m, door leaves set aside); {doors.Count} doors listed");
                foreach (var d in doors)
                {
                    var os = matched.Where(kv => kv.Value == d).Select(kv => kv.Key).ToList();
                    lines.Add($"{d.roomA} - {d.roomB} [{d.state}] at {V3(d.transform.position)}: clear width {widths[d]:F2} m; gap seen from {string.Join(" and ", os.Select(o => o.from.name + $" ({o.width:F1} m wide, y {o.yMin:F1}-{o.yMax + 0.25f:F1})"))}");
                }
                foreach (var o in unmatched) lines.Add($"UNLISTED gap in {o.from.name}'s {o.side} wall at {V3(o.center)} {o.width:F2} m wide, y {o.yMin:F2}-{o.yMax:F2}, to {o.toRoom}");
                foreach (var d in unseen) lines.Add($"listed door with no gap found: {d.name}");
                Check("S02", "every opening between rooms is a listed door or doorway, every listed door is a real opening, and no room opens to the outside",
                    unmatched.Count == 0 && unseen.Count == 0 && openings.All(o => o.toRoom != "(outside)") && widths.Values.All(w => w > 0.9f), string.Join("\n", lines));
            }

            var gallery = MapExport.MeasureGallery();
            var hall = R("dining hall"); var armory = R("armory"); var entry = R("entry hall");
            // S03 armory's two ways in ---------------------------------------------
            {
                var aDoors = doors.Where(d => d.roomA == armory.name || d.roomB == armory.name).ToList();
                var toEntry = aDoors.FirstOrDefault(d => d.roomA == entry.name || d.roomB == entry.name);
                var toHall = aDoors.FirstOrDefault(d => d.roomA == hall.name || d.roomB == hall.name);
                var gaps = openings.Where(o => o.from == armory || o.toRoom == armory.name).Select(o => matched.ContainsKey(o) ? matched[o] : null).Distinct().ToList();
                bool beneath = toHall != null && gallery != null && toHall.transform.position.x >= gallery.x0 && toHall.transform.position.x <= gallery.x1
                    && toHall.transform.position.z >= gallery.z0 - 0.9f && toHall.transform.position.z <= gallery.z1 && toHall.transform.position.y + 2f < gallery.deckY;
                Check("S03", "the armory has exactly two ways in: its entrance from the entry hall and a way in from the dining hall beneath the gallery",
                    aDoors.Count == 2 && gaps.Count == 2 && !gaps.Contains(null) && toEntry != null && toHall != null && beneath,
                    $"doors touching the armory: {aDoors.Count}; distinct wall gaps in its walls: {gaps.Count}\n" +
                    (toHall != null && gallery != null ? $"hall-side door at {V3(toHall.transform.position)}; gallery deck x {gallery.x0:F2}..{gallery.x1:F2}, z {gallery.z0:F2}..{gallery.z1:F2} at y {gallery.deckY:F2}: door is beneath it = {beneath}" : "no hall door or gallery"));
            }

            // S04/S05 the guarded entrance seen from the entry hall; its guards only from the doorway -----------
            {
                var door = doors.First(d => (d.roomA == entry.name && d.roomB == armory.name) || (d.roomB == entry.name && d.roomA == armory.name));
                var c = door.transform.position; var n = door.transform.forward; if (Vector3.Dot(armory.Center - c, n) < 0) n = -n;
                var hallPts = Grid(entry, 0.5f, 0.4f).Select(p => p + Vector3.up * 1.6f).ToList();
                int sees = hallPts.Count(p => SceneQuery.Clear(p, c + Vector3.up * 1.5f - n * 0.05f));
                var start = Object.FindAnyObjectByType<CharacterController>().transform.position + Vector3.up * 1.6f;
                bool fromStart = SceneQuery.Clear(start, c + Vector3.up * 1.5f - n * 0.05f);
                Check("S04", "the entry hall is where the player sees the armory's guarded entrance",
                    sees >= hallPts.Count / 2 && fromStart,
                    $"the entrance's middle (1.5 m up) is in clear sight from {sees} of {hallPts.Count} eye points (1.6 m) on a 0.5 m grid over the entry hall floor; from the start: {fromStart}");

                var guards = enemies.Where(m => RoomOf(m.transform.position) == armory).ToList();
                var behind = guards.Where(m => m.kind == "ranged" && Flat(m.transform.position - c) < 7f).ToList();
                // eye points in the hall proper: more than 0.8 m back from the opening's plane
                var hallOnly = hallPts.Where(p => Vector3.Dot(p - c, n) < -0.8f).ToList();
                int leaks = 0; string leakEx = "";
                foreach (var p in hallOnly)
                {
                    bool seen = false;
                    foreach (var g in guards)
                    {
                        foreach (var t in Body(g))
                            if (SceneQuery.Clear(p, t)) { seen = true; if (leakEx.Length == 0) leakEx = $" e.g. {g.kind} at {V3(g.transform.position)} from {V3(p)}"; break; }
                        if (seen) break;
                    }
                    if (seen) leaks++;
                }
                // standing in the doorway itself
                var doorPts = new List<Vector3>();
                for (float a = -0.3f; a <= 0.9f; a += 0.3f) for (float l = -1.2f; l <= 1.2f; l += 0.4f) doorPts.Add(c + n * a + door.transform.right * l + Vector3.up * 1.6f);
                int seenAtDoor = behind.Count(g => doorPts.Any(p => Body(g).Any(t => SceneQuery.Clear(p, t))));
                var facing = behind.Select(g => Vector3.Dot(g.transform.forward, (c - g.transform.position).normalized)).ToList();
                Check("S05", "ranged guards stand behind the armory's entrance facing the entry hall, and no armory defender is in sight from the entry hall until the player stands in the doorway",
                    behind.Count >= 2 && leaks == 0 && seenAtDoor == behind.Count && facing.All(f => f > 0.3f),
                    $"{behind.Count} ranged within 7 m behind the entrance; facing towards it (cos) {string.Join(", ", facing.Select(f => f.ToString("F2", IC)))}\n" +
                    $"hall eye points (>0.8 m back from the opening) that see any part (head, chest, shoulders) of any of the {guards.Count} armory defenders: {leaks} of {hallOnly.Count}{leakEx}\n" +
                    $"guards behind the entrance seen from at least one eye point in the doorway: {seenAtDoor} of {behind.Count}");
            }

            // S06 way round ------------------------------------------------------
            {
                var path = GraphPath(doors, entry.name, hall.name, armory.name);
                Check("S06", "a way leads from the entry hall to the dining hall without passing through the armory (door graph; walked in W1)",
                    path != null, path != null ? string.Join(" > ", path) : "no path");
            }

            // S07/S08 gallery -------------------------------------------------------
            if (gallery != null)
            {
                float lenX = gallery.x1 - gallery.x0, depth = gallery.z1 - gallery.z0;
                bool alongSide = Mathf.Abs(gallery.z0 - hall.z0) < 0.3f || Mathf.Abs(gallery.z1 - hall.z1) < 0.3f;
                Check("S07", "a gallery runs along one side of the dining hall, high above the floor",
                    gallery.deckY - hall.floorY >= 4f && lenX >= 0.8f * (hall.x1 - hall.x0) && alongSide && depth < 0.4f * (hall.z1 - hall.z0),
                    $"deck at {gallery.deckY - hall.floorY:F2} m above the hall floor (hall ceiling {hall.ceilMax:F2} m); deck {lenX:F2} m long of the {hall.x1 - hall.x0:F2} m side, {depth:F2} m deep, against the wall: {alongSide}");

                var floorPts = Grid(hall, 0.5f, 0.25f).Select(p => p + Vector3.up * 0.05f).ToList();
                float railZ = gallery.railA.z;
                var eyes = new List<Vector3>();
                // a person standing against the rail (its middle 0.5 m back from the rail's line), eyes 1.6 m above
                // whatever they stand on there (the deck, or the archers' step along the rail)
                float Stand(float x)
                {
                    var h = Physics.RaycastAll(new Vector3(x, gallery.deckY + 3f, railZ - 0.5f), Vector3.down, 4f).Where(t => SceneQuery.IsStructure(t.collider)).OrderBy(t => t.distance).FirstOrDefault();
                    return h.collider != null ? h.point.y : gallery.deckY;
                }
                for (float x = gallery.x0 + 1f; x <= gallery.x1 - 1f; x += 1f) eyes.Add(new Vector3(x, Stand(x) + 1.6f, railZ - 0.5f));
                int vis = floorPts.Count(p => eyes.Any(e => SceneQuery.Clear(e, p)));
                var open = floorPts.Where(p => p.z > gallery.z1 + 0.1f).ToList();
                int visOpen = open.Count(p => eyes.Any(e => SceneQuery.Clear(e, p)));
                var mid = new Vector3((gallery.x0 + gallery.x1) * 0.5f, Stand((gallery.x0 + gallery.x1) * 0.5f) + 1.6f, railZ - 0.5f);
                int visMid = floorPts.Count(p => SceneQuery.Clear(mid, p));
                float frac = vis / (float)floorPts.Count;
                Check("S08", "much of the hall floor can be seen from the gallery's rail (taken as: more than half of the whole hall floor, and 60% of the floor not under the gallery)",
                    frac >= 0.5f && visOpen / (float)open.Count >= 0.6f,
                    $"standing against the rail, eyes at {eyes[0].y - gallery.deckY:F2} m above the deck; floor points (0.5 m grid, whole hall incl. under the gallery and under tables) in sight: {vis}/{floorPts.Count} = {frac:P0}; of the floor not under the deck: {visOpen}/{open.Count} = {visOpen / (float)open.Count:P0}; from the middle of the rail alone: {visMid / (float)floorPts.Count:P0}");
            }
            else Check("S07", "gallery", false, "no Gallery object");

            // S09 throne on a stage with steps --------------------------------------
            {
                var st = MapExport.MeasureStage(); var throne = GameObject.Find("Throne").transform; var tr = R("throne room");
                var tp = throne.position;
                bool on = tp.x >= st.x0 && tp.x <= st.x1 && tp.z >= st.z0 && tp.z <= st.z1 && Mathf.Abs(tp.y - st.topY) < 0.15f;
                // step profile down the approach, ignoring the invisible ramp helper
                var prof = new List<float>();
                for (float z = st.z0 - 4.5f; z <= st.z0 + 0.25f; z += 0.05f)
                {
                    var h = Physics.RaycastAll(new Vector3(tp.x - 1.5f, st.topY + 2f, z), Vector3.down, 6f).Where(x => !x.collider.name.Contains("StepRamp") && !SceneQuery.IsPlayer(x.collider) && x.collider.GetComponentInParent<Marker>() == null).OrderBy(x => x.distance).FirstOrDefault();
                    prof.Add(h.collider != null ? h.point.y : float.NaN);
                }
                var risers = new List<float>();
                for (int i = 1; i < prof.Count; i++) { float d = prof[i] - prof[i - 1]; if (d > 0.05f) risers.Add(d); }
                Check("S09", "the throne stands on a stage with steps at the far end of the throne room",
                    on && st.topY - tr.floorY >= 0.75f && risers.Count >= 3 && risers.All(r => r <= 0.31f) && st.z1 > tr.z1 - 1f,
                    $"stage top {st.topY - tr.floorY:F2} m above the floor, x {st.x0:F2}..{st.x1:F2}, z {st.z0:F2}..{st.z1:F2} (room's far wall at z {tr.z1:F2}); throne at {V3(tp)} on it: {on}\n" +
                    $"steps up the front, measured by rays down the approach (from {st.z0 - 4.5f:F1} to the stage edge, ramp helper ignored): {risers.Count} risers of {string.Join(", ", risers.Select(r => r.ToString("F2", IC)))} m");
            }

            // S10 vault ---------------------------------------------------------------
            {
                var vault = R("treasure vault");
                var vDoors = doors.Where(d => d.roomA == vault.name || d.roomB == vault.name).ToList();
                var vGaps = openings.Where(o => o.from == vault || o.toRoom == vault.name).Select(o => matched.ContainsKey(o) ? matched[o] : null).Distinct().ToList();
                var hidden = vDoors.FirstOrDefault();
                bool blocked = false; string bc = ""; int sameCases = 0, lightsNear = 0;
                if (hidden != null && hidden.leaf != null)
                {
                    var c = hidden.transform.position; var n = hidden.transform.forward; if (Vector3.Dot(vault.Center - c, n) < 0) n = -n;
                    blocked = true;
                    for (float l = -0.6f; l <= 0.6f; l += 0.3f) for (float y = 0.3f; y <= 1.9f; y += 0.4f)
                    {
                        var o = c - n * 1.5f + hidden.transform.right * l + Vector3.up * y;
                        if (SceneQuery.Clear(o, o + n * 2.5f)) blocked = false;
                    }
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(hidden.leaf);
                    var lib = rooms.First(r => r.name == (hidden.roomA == vault.name ? hidden.roomB : hidden.roomA));
                    sameCases = lib.room.GetComponentsInChildren<Transform>().Select(t => t.gameObject).Where(g => g != hidden.leaf && PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabUtility.GetCorrespondingObjectFromSource(g) == src).Count();
                    lightsNear = Object.FindObjectsByType<Light>().Count(lt => lt.enabled && Flat(lt.transform.position - c) < 2.5f && lt.transform.position.x < 31f);
                    bc = $"hidden by '{hidden.leaf.name}' (prefab {(src != null ? src.name : "?")}); the same bookcase stands {sameCases} more times in the {lib.name}; lights within 2.5 m of it: {lightsNear}";
                }
                int degree = vDoors.Count;
                Check("S10", "the vault has one way in, a hidden one the player has to search for, and lies on no way through",
                    vDoors.Count == 1 && vGaps.Count == 1 && !vGaps.Contains(null) && hidden.state == "hidden" && blocked && sameCases >= 3 && lightsNear == 0 && degree == 1,
                    $"doors touching the vault: {vDoors.Count}; distinct wall gaps in its walls: {vGaps.Count}; state '{hidden?.state}'; with the bookcase in place every ray through the opening (0.3-1.9 m up) is blocked: {blocked}\n{bc}\nthe vault is a dead end (one door), so no way through passes it");
            }

            // S11/S12 heights, ceilings, indoors ---------------------------------------
            {
                var lines = rooms.Select(r => $"{r.name} ({r.kind}): ceiling {r.ceilMin:F2}-{r.ceilMax:F2} m above the floor, {Band(r.ceilMax)}; ceiling overhead at {r.ceilCoverage:P0} of 1 m floor samples").ToList();
                var low = new[] { "prison", "guard room", "kitchen", "treasure vault" }.Select(R).ToList();
                var tall = new[] { "dining hall", "throne room" }.Select(R).ToList();
                var tr = R("throne room");
                bool lowOk = low.All(r => r.ceilMax <= 3.6f), tallOk = tall.All(r => r.ceilMin >= 6.5f), tallest = rooms.Where(r => r != tr).All(r => r.ceilMax < tr.ceilMin);
                bool std = new[] { "entry hall", "armory", "storeroom" }.Select(R).All(r => r.ceilMin > 3.6f && r.ceilMax < 6.5f);
                Check("S11", "the prison, guard room, kitchen and vault are low; the dining hall and throne room are tall; no room is taller than the throne room; the standard rooms are standard",
                    lowOk && tallOk && tallest && std, string.Join("\n", lines) + "\n(bands: low < 3.6 m, standard 3.6-6.5 m, tall >= 6.5 m)");
                Check("S12", "everything is indoors and every room has a ceiling",
                    rooms.All(r => r.ceilCoverage >= 0.999f) && openings.All(o => o.toRoom != "(outside)"),
                    $"rooms with a ceiling over every floor sample: {rooms.Count(r => r.ceilCoverage >= 0.999f)}/{rooms.Count}; wall gaps leading outside: {openings.Count(o => o.toRoom == "(outside)")}");
            }

            // S13 shut doors ------------------------------------------------------------
            {
                var shut = doors.Where(d => d.state == "shut").ToList();
                var lines = shut.Select(d =>
                {
                    var sw = d.leaf != null ? d.leaf.GetComponent<SwingDoor>() : null;
                    var col = d.leaf != null ? d.leaf.GetComponentsInChildren<Collider>().Count(c => !c.isTrigger) : 0;
                    return $"{d.roomA} - {d.roomB}: leaf {(sw != null ? "SwingDoor" : "none")}, {col} solid collider(s), shut now: {(sw != null && !sw.isOpen)}";
                }).ToList();
                bool ok = shut.Count >= 2 && shut.All(d => d.leaf != null && d.leaf.GetComponent<SwingDoor>() != null && !d.leaf.GetComponent<SwingDoor>().isOpen && d.leaf.GetComponentsInChildren<Collider>().Any(c => !c.isTrigger));
                int open = doors.Count(d => d.state == "open");
                Check("S13", "some openings are open doorways and some are shut doors the player opens with E (Interact on a solid collider); the walks open every shut door on a way through",
                    ok && open >= 2, $"{open} open, {shut.Count} shut, {doors.Count(d => d.state == "hidden")} hidden\n" + string.Join("\n", lines));
            }

            // S14 occluders ---------------------------------------------------------------
            {
                var lines = new List<string>(); bool all = true;
                foreach (var kind in new[] { "armory", "dining hall", "throne room" })
                {
                    var r = R(kind);
                    var es = enemies.Where(m => RoomOf(m.transform.position) == r).ToList();
                    int standHide = 0, crouchHide = 0; var blockers = new Dictionary<string, int>();
                    foreach (var p in Grid(r, 0.75f, 0.5f))
                    {
                        bool s = false, cr = false;
                        foreach (var e in es)
                        {
                            if (Flat(e.transform.position - p) > 14f || Flat(e.transform.position - p) < 1.5f) continue;
                            var eye = e.transform.position + Vector3.up * 1.6f;
                            bool standSeen = SceneQuery.Clear(eye, p + Vector3.up * 1.6f) && SceneQuery.Clear(eye, p + Vector3.up * 1.2f);
                            bool crouchSeen = SceneQuery.Clear(eye, p + Vector3.up * 1.0f) || SceneQuery.Clear(eye, p + Vector3.up * 0.6f);
                            if (!standSeen && !crouchSeen && Blocker(eye, p + Vector3.up * 1.6f, r, blockers)) s = true;
                            if (SceneQuery.Clear(eye, p + Vector3.up * 1.6f) && !crouchSeen && Blocker(eye, p + Vector3.up * 0.8f, r, blockers)) cr = true;
                        }
                        if (s) standHide++; if (cr) crouchHide++;
                    }
                    bool ok = standHide >= 4 && crouchHide >= 4; all &= ok;
                    lines.Add($"{r.name}: {standHide} floor points (0.75 m grid) hidden standing from a defender 1.5-14 m away by something in the room, {crouchHide} hidden only when crouching; blockers: {string.Join(", ", blockers.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key + " x" + kv.Value))}");
                }
                Check("S14", "the level has tall and low occluders the player can hide behind, standing or crouching (measured against the defenders' sight in the armory, dining hall and throne room)", all, string.Join("\n", lines));
            }

            // S15 defenders and pickups ---------------------------------------------------
            {
                var byRoom = rooms.ToDictionary(r => r, r => enemies.Count(m => RoomOf(m.transform.position) == r));
                var lines = byRoom.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key.name}: {string.Join(", ", enemies.Where(m => RoomOf(m.transform.position) == kv.Key).GroupBy(m => m.kind).Select(g => g.Count() + " " + g.Key))}").ToList();
                var tr = R("throne room");
                var onGallery = enemies.Where(m => m.kind == "ranged" && gallery != null && m.transform.position.y >= gallery.deckY - 0.2f && m.transform.position.x >= gallery.x0 && m.transform.position.x <= gallery.x1 && m.transform.position.z >= gallery.z0 && m.transform.position.z <= gallery.z1).Count();
                var gear = markers.Where(m => m.kind == "best gear").ToList(); var hoard = markers.Where(m => m.kind == "hoard").ToList();
                bool ok = byRoom[entry] == 0 && byRoom[hall] == byRoom.Values.Max() && byRoom.Count(kv => kv.Value == byRoom[hall]) == 1
                    && onGallery >= 2 && enemies.Count(m => m.kind == "warlord" && RoomOf(m.transform.position) == tr) == 1 && byRoom[tr] >= 3
                    && enemies.Count(m => m.kind == "melee" && RoomOf(m.transform.position) == armory) >= 2
                    && markers.Count(m => !m.IsEnemy) == 2 && gear.Count == 1 && RoomOf(gear[0].transform.position) == armory && hoard.Count == 1 && RoomOf(hoard[0].transform.position) == R("treasure vault")
                    && enemies.All(m => RoomOf(m.transform.position) != null);
                Check("S15", "defenders and pickups: none in the entry hall, ranged behind the armory entrance and melee in the rest of it, ranged on the gallery, the largest group in the dining hall, the warlord and guards in the throne room, the best gear in the armory and the hoard in the vault",
                    ok, string.Join("\n", lines) + $"\nranged on the gallery deck: {onGallery}; pickups: {string.Join(", ", markers.Where(m => !m.IsEnemy).Select(m => m.kind + " in " + RoomOf(m.transform.position)?.name))}");
            }

            // S16/S17 furnishing ------------------------------------------------------------
            {
                var want = new Dictionary<string, (string label, string[] keys, int min)[]>
                {
                    ["entry hall"] = new[] { ("benches", new[] { "Bench" }, 2), ("weapon stands", new[] { "WeaponRack", "KnightStand" }, 2), ("table", new[] { "Table" }, 1) },
                    ["armory"] = new[] { ("weapon racks", new[] { "Weapon_Rack", "WeaponRack", "Weapon_Barrel" }, 10), ("armour stands", new[] { "KnightStand" }, 8) },
                    ["guard room"] = new[] { ("overturned furniture", new[] { "Table_Round_Broken", "Chair_Broken", "Stool" }, 3), ("bunks", new[] { "Bed" }, 1) },
                    ["prison"] = new[] { ("barred doors", new[] { "Door_Bars" }, 6), ("bars", new[] { "Metal_Fence" }, 12), ("bedding", new[] { "Bed", "Camp_Roll" }, 5) },
                    ["kitchen"] = new[] { ("hearth", new[] { "Fireplace" }, 1), ("worktables", new[] { "Table" }, 2), ("barrels", new[] { "Barrel" }, 2), ("sacks", new[] { "Sack" }, 2) },
                    ["library"] = new[] { ("tall bookcases", new[] { "Bookcase" }, 12), ("reading tables", new[] { "Table" }, 2), ("books down", new[] { "Book_", "Papers" }, 15) },
                    ["alchemist's study"] = new[] { ("glassware", new[] { "Potion", "Vial", "Jar" }, 20), ("benches", new[] { "Table" }, 2), ("shelves", new[] { "Shelf" }, 4), ("smashed glass", new[] { "Shard", "Vase_Broken" }, 6) },
                    ["forge"] = new[] { ("hearth", new[] { "FirePit" }, 1), ("hood and flue", new[] { "Ventilation" }, 2), ("anvils", new[] { "Anvil" }, 2), ("quench barrels", new[] { "Barrel" }, 2), ("tools", new[] { "Hammer", "Tongs", "Pickaxe" }, 3) },
                    ["treasure vault"] = new[] { ("chests and coffers", new[] { "Chest" }, 4), ("gold", new[] { "Gold", "Coins" }, 3) },
                    ["dining hall"] = new[] { ("long tables", new[] { "Prop_Table_01", "Dwarf_Table" }, 10), ("benches", new[] { "Bench" }, 10), ("chandeliers", new[] { "Chandelier_01" }, 3), ("banners in tatters", new[] { "Banner_Damaged" }, 6) },
                    ["throne room"] = new[] { ("throne", new[] { "Throne" }, 1), ("torn runner", new[] { "Rug" }, 2) },
                    ["crypt"] = new[] { ("tombs", new[] { "Tomb", "Coffin", "Sarcophagus" }, 4), ("burial niches", new[] { "Alcove" }, 3) },
                    ["storeroom"] = new[] { ("crates", new[] { "Crate" }, 4), ("barrels", new[] { "Barrel" }, 4), ("sacks", new[] { "Sack" }, 2) },
                    ["shrine"] = new[] { ("altar", new[] { "Alter" }, 1), ("candles", new[] { "Candle" }, 4), ("statues", new[] { "Statue" }, 4) },
                    ["torture chamber"] = new[] { ("rack", new[] { "StretchTable" }, 1), ("chains", new[] { "Chain" }, 3), ("cages", new[] { "Cage" }, 1), ("cold brazier", new[] { "Brazier" }, 1) },
                };
                var lines = new List<string>(); bool all = true;
                var density = new Dictionary<SceneQuery.RoomInfo, float>();
                foreach (var r in rooms)
                {
                    var names = r.room.GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).Select(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? t.name).ToList();
                    var furn = r.room.transform.Find("Props").GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                        .Select(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name ?? "").Where(n => !n.StartsWith("FX_") && !n.Contains("SpiderWeb") && !n.Contains("Torch") && !n.Contains("Pillar")).Count();
                    density[r] = furn / r.Area;
                    if (!want.TryGetValue(r.kind, out var reqs)) continue;
                    var parts = new List<string>(); bool ok = true;
                    foreach (var q in reqs)
                    {
                        int c = names.Count(n => q.keys.Any(k => n.Contains(k)));
                        parts.Add($"{q.label} {c}/{q.min}"); ok &= c >= q.min;
                    }
                    all &= ok;
                    lines.Add($"{(ok ? "ok  " : "SHORT")} {r.name}: {string.Join(", ", parts)}");
                }
                Check("S16", "each room is furnished as its kind (counted from the prefabs placed in it)", all, string.Join("\n", lines));
                var trr = R("throne room");
                Check("S17", "the throne room is the sparsest room (fewest furnishings per square metre, pillars, torches and effects not counted)",
                    density.Where(kv => kv.Key != trr).All(kv => kv.Value > density[trr]),
                    string.Join(", ", density.OrderBy(kv => kv.Value).Select(kv => $"{kv.Key.name} {kv.Value:F3}")));
            }

            // S18 light -----------------------------------------------------------------------
            {
                var lum = RoomLuminance(rooms);
                var dark = new[] { "prison", "crypt", "torture chamber" }.Select(R).ToList();
                var bright = new[] { "dining hall", "throne room" }.Select(R).ToList();
                float median = lum.Values.Select(v => v.mean).OrderBy(v => v).ElementAt(lum.Count / 2);
                bool readable = lum.All(kv => kv.Value.floor >= 0.035f);
                bool order = dark.Max(r => lum[r].mean) < bright.Min(r => lum[r].mean);
                bool brightOk = bright.All(r => lum[r].mean >= median);
                Check("S18", "lit so a player can see where to walk, dark rooms included; the dark rooms darker and the dining hall and throne room brighter than most",
                    readable && order && brightOk,
                    string.Join("\n", lum.OrderBy(kv => kv.Value.mean).Select(kv => $"{kv.Key.name}: mean luminance {kv.Value.mean:F3}, floor (lower third of four views tilted 20 deg down) {kv.Value.floor:F3}")) +
                    $"\n(rendered from the room's middle at 1.6 m, markers and player hidden; floor luminance needed >= 0.035; median room {median:F3})");
            }

            // S19 player and HUD ------------------------------------------------------------------
            {
                var cc = Object.FindAnyObjectByType<CharacterController>();
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(cc.gameObject);
                string src = root != null ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root)) : "";
                var fpc = cc.GetComponent<FirstPersonController>();
                var mods = root != null ? PrefabUtility.GetPropertyModifications(root) ?? new PropertyModification[0] : new PropertyModification[0];
                var bad = mods.Where(m => m.target is CharacterController || m.target is FirstPersonController || m.target is StarterAssetsInputs || m.target is IronCitadel.Interact.InteractOnE).Select(m => m.propertyPath).ToList();
                var hud = Object.FindAnyObjectByType<LevelHUD>();
                var startRoom = RoomOf(cc.transform.position);
                var oc = hud != null ? hud.overviewCamera : null;
                bool covers = oc != null && oc.orthographic && rooms.All(r => Mathf.Abs(r.x0 - oc.transform.position.x) <= oc.orthographicSize * 16f / 9f && Mathf.Abs(r.x1 - oc.transform.position.x) <= oc.orthographicSize * 16f / 9f
                    && Mathf.Abs(r.z0 - oc.transform.position.z) <= oc.orthographicSize && Mathf.Abs(r.z1 - oc.transform.position.z) <= oc.orthographicSize);
                Check("S19", "the shipped player prefab starts in the entry hall, unmodified; the HUD (room name, time, throne announcement) and the M overview camera are in the scene",
                    src == "Assets/Interact/IronCitadelPlayer.prefab" && bad.Count == 0 && startRoom == entry && hud != null && hud.throne != null && covers,
                    $"player instance of '{src}' at {V3(cc.transform.position)} in {startRoom?.name}; overrides on its controller components: {(bad.Count == 0 ? "none" : string.Join(", ", bad))}; CharacterController r {cc.radius:F2} h {cc.height:F2} step {cc.stepOffset:F2}; FPC move {fpc.MoveSpeed} sprint {fpc.SprintSpeed}\n" +
                    $"HUD: {(hud != null)}, throne ref: {(hud != null && hud.throne != null)}, overview camera orthographic covering every room: {covers}");
            }

            pass = _pass; fail = _fail;
            return _out;
        }

        // ------------------------------------------------------------ helpers
        static string Band(float h) => h < 3.6f ? "low" : h < 6.5f ? "standard" : "tall";
        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
        static string V3(Vector3 v) => $"({v.x.ToString("F2", IC)}, {v.y.ToString("F2", IC)}, {v.z.ToString("F2", IC)})";

        static IEnumerable<Vector3> Grid(SceneQuery.RoomInfo r, float step, float inset)
        {
            for (float x = r.x0 + inset; x <= r.x1 - inset + 0.001f; x += step)
                for (float z = r.z0 + inset; z <= r.z1 - inset + 0.001f; z += step)
                    yield return new Vector3(x, r.floorY, z);
        }

        static IEnumerable<Vector3> Body(Marker m)
        {
            var p = m.transform.position; var right = m.transform.right;
            yield return p + Vector3.up * 1.7f; yield return p + Vector3.up * 1.2f;
            yield return p + Vector3.up * 1.3f + right * 0.3f; yield return p + Vector3.up * 1.3f - right * 0.3f;
            yield return p + Vector3.up * 0.6f;
        }

        /// The first thing on the line, if it belongs to the room (furniture or the room's own structure, not a wall of another room).
        static bool Blocker(Vector3 a, Vector3 b, SceneQuery.RoomInfo r, Dictionary<string, int> tally)
        {
            var d = b - a; float len = d.magnitude;
            var h = Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore).Where(x => !SceneQuery.IsPlayer(x.collider)).OrderBy(x => x.distance).FirstOrDefault();
            if (h.collider == null || !h.collider.transform.IsChildOf(r.room.transform)) return false;
            if (h.collider.transform.parent != null && h.collider.transform.parent.name == "Walls" && h.collider.transform.parent.parent == r.room.transform) return false; // the room's own outer walls don't count
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(h.collider.gameObject);
            string n = root != null ? (PrefabUtility.GetCorrespondingObjectFromSource(root)?.name ?? root.name) : h.collider.name;
            tally[n] = tally.TryGetValue(n, out var c) ? c + 1 : 1;
            return true;
        }

        static List<string> GraphPath(List<DoorLink> doors, string from, string to, string avoid)
        {
            var prev = new Dictionary<string, string> { [from] = null };
            var q = new Queue<string>(); q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to) break;
                foreach (var d in doors)
                {
                    string n = d.roomA == c ? d.roomB : d.roomB == c ? d.roomA : null;
                    if (n == null || n == avoid || prev.ContainsKey(n)) continue;
                    prev[n] = c; q.Enqueue(n);
                }
            }
            if (!prev.ContainsKey(to)) return null;
            var path = new List<string>();
            for (var c = to; c != null; c = prev[c]) path.Insert(0, c);
            return path;
        }

        public static Dictionary<SceneQuery.RoomInfo, (float mean, float floor)> RoomLuminance(List<SceneQuery.RoomInfo> rooms)
        {
            var hide = new List<Renderer>();
            foreach (var m in Object.FindObjectsByType<Marker>()) hide.AddRange(m.GetComponentsInChildren<Renderer>().Where(r => r.enabled));
            var cc = Object.FindAnyObjectByType<CharacterController>();
            hide.AddRange(cc.transform.root.GetComponentsInChildren<Renderer>().Where(r => r.enabled));
            foreach (var r in hide) r.enabled = false;
            var res = new Dictionary<SceneQuery.RoomInfo, (float, float)>();
            var go = new GameObject("LumCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 70; cam.nearClipPlane = 0.05f; cam.farClipPlane = 150f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                var cd = cam.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = true;
                const int W = 192, H = 108;
                var rt = new RenderTexture(W, H, 24); var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                foreach (var r in rooms)
                {
                    double sum = 0, floorSum = 0; int n = 0, fn = 0;
                    var c = r.Center + Vector3.up * 1.6f;
                    foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                    {
                        cam.transform.SetPositionAndRotation(c, Quaternion.Euler(20f, yaw, 0));
                        cam.targetTexture = rt; cam.Render();
                        RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
                        var px = tex.GetPixels();
                        for (int i = 0; i < px.Length; i++)
                        {
                            float l = 0.2126f * px[i].r + 0.7152f * px[i].g + 0.0722f * px[i].b;
                            sum += l; n++;
                            if (i / W < H / 3) { floorSum += l; fn++; }   // GetPixels starts at the bottom row
                        }
                    }
                    res[r] = ((float)(sum / n), (float)(floorSum / fn));
                }
                cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
            finally
            {
                Object.DestroyImmediate(go);
                foreach (var r in hide) if (r != null) r.enabled = true;
            }
            return res;
        }
    }
}
