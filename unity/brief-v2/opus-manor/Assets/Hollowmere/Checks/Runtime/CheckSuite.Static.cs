using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using static Hollowmere.Checks.LevelSurvey;

namespace Hollowmere.Checks
{
    public partial class CheckSuite
    {
        static readonly string[] Kinds = { "foyer", "gun room", "watch room", "cellar cells", "kitchen", "library", "laboratory", "boiler room", "strong room",
                                           "dining hall", "ballroom", "crypt", "storeroom", "chapel", "butcher's room" };
        public const int AreaGunRoom = 3, AreaHallFloor = 4, AreaHidden = 5;

        // ---------------------------------------------------------------- rooms and heights

        void StaticRooms()
        {
            var missing = Kinds.Where(k => _rooms.Count(r => r.kind == k) != 1).ToList();
            var extras = _rooms.Where(r => r.kind == "extra").Select(r => r.roomName).ToList();
            Check("S01", "one room of each kind in the brief", missing.Count == 0,
                $"{_rooms.Count} rooms; kinds missing or doubled: [{string.Join(", ", missing)}]; extra rooms: [{string.Join(", ", extras)}]");
            var bad = _rooms.Where(r => !new[] { "decayed", "looted", "stocked", "untouched" }.Contains(r.state)).Select(r => r.roomName).ToList();
            bool states = bad.Count == 0 && Room("gun room").state == "stocked" && Room("strong room").state == "untouched"
                          && _rooms.Where(r => r.kind != "gun room" && r.kind != "strong room").All(r => r.state == "decayed" || r.state == "looted");
            Check("S02", "rooms decayed or looted, the gun room stocked, the strong room untouched", states,
                string.Join("; ", _rooms.Select(r => $"{r.roomName}: {r.state}")));
        }

        Dictionary<string, float> _height = new Dictionary<string, float>();

        void StaticHeights()
        {
            foreach (var r in _rooms) _height[r.roomName] = CeilingY(r) - FloorY(r);
            string H(string kind) => F(_height[Room(kind).roomName]);
            var low = new[] { "cellar cells", "watch room", "kitchen", "strong room" };
            var lowAlso = new[] { "crypt", "butcher's room" };
            var standard = new[] { "foyer", "gun room", "storeroom" };
            var tall = new[] { "dining hall", "ballroom" };
            bool okLow = low.Concat(lowAlso).All(k => _height[Room(k).roomName] <= 2.8f);
            bool okStd = standard.All(k => _height[Room(k).roomName] >= 3.0f && _height[Room(k).roomName] <= 4.0f);
            bool okTall = tall.All(k => _height[Room(k).roomName] >= 6.0f) && _height[Room("chapel").roomName] >= 6f;
            float ball = _height[Room("ballroom").roomName];
            var taller = _rooms.Where(r => r.kind != "ballroom" && _height[r.roomName] >= ball).Select(r => r.roomName).ToList();
            Check("S03", "low rooms are low (cells, watch room, kitchen, strong room; crypt and butcher's room too): floor to ceiling <= 2.8 m", okLow,
                string.Join(", ", low.Concat(lowAlso).Select(k => $"{k} {H(k)}")));
            Check("S04", "standard rooms 3.0-4.0 m (foyer, gun room, storeroom)", okStd, string.Join(", ", standard.Select(k => $"{k} {H(k)}")));
            Check("S05", "dining hall and ballroom tall (>= 6 m), chapel tall", okTall, $"dining hall {H("dining hall")}, ballroom {H("ballroom")}, chapel {H("chapel")}, library {H("library")}");
            Check("S06", "no room is taller than the ballroom", taller.Count == 0,
                $"ballroom {F(ball)} m; tallest other: {_rooms.Where(r => r.kind != "ballroom").OrderByDescending(r => _height[r.roomName]).Select(r => r.roomName + " " + F(_height[r.roomName])).First()}");
        }

        void StaticCeilings()
        {
            int samples = 0, covered = 0; var bad = new List<string>();
            foreach (var r in _rooms)
            {
                float fy = FloorY(r), h = _height[r.roomName];
                int rs = 0, rc = 0;
                foreach (var p in Grid(Outline(r), 1.0f, 0.3f, fy + 0.3f))
                {
                    rs++;
                    if (Physics.Raycast(p, Vector3.up, h + 0.5f, 1 << LayerCeiling, QueryTriggerInteraction.Ignore)) rc++;
                }
                samples += rs; covered += rc;
                if (rc < rs) bad.Add($"{r.roomName} {rc}/{rs}");
            }
            Check("S07", "every room has a ceiling: upward rays from a 1 m grid over every floor hit the room's ceiling", bad.Count == 0 && samples > 0,
                $"{covered}/{samples} points covered{(bad.Count > 0 ? "; gaps: " + string.Join(", ", bad) : "")}");
        }

        void StaticIndoors()
        {
            int rays = 0, escaped = 0; var where = new List<string>();
            foreach (var r in _rooms)
            {
                float fy = FloorY(r), h = _height[r.roomName];
                var q = Outline(r);
                var pts = new List<Vector3> { new Vector3(q.center.x, 0, q.center.y), new Vector3(q.xMin + 0.6f, 0, q.yMin + 0.6f), new Vector3(q.xMax - 0.6f, 0, q.yMax - 0.6f) };
                foreach (var p0 in pts)
                    foreach (var y in new[] { 1.0f, h - 0.3f })
                        for (int a = 0; a < 24; a++)
                        {
                            var dir = Quaternion.Euler(0, a * 15f, 0) * Vector3.forward;
                            var o = new Vector3(p0.x, fy + y, p0.z);
                            rays++;
                            if (!Physics.Raycast(o, dir, 150f, ~(1 << LayerSeeThrough), QueryTriggerInteraction.Ignore)) { escaped++; if (where.Count < 6) where.Add($"{r.roomName} {P(o)} dir {a * 15}"); }
                            if (!Physics.Raycast(o, Vector3.down, y + 0.5f, ~0, QueryTriggerInteraction.Ignore)) { escaped++; if (where.Count < 6) where.Add($"{r.roomName} no floor under {P(o)}"); }
                        }
            }
            // the walkable NavMesh stays inside the rooms and their doorways
            var tri = NavMesh.CalculateTriangulation();
            int outside = 0;
            for (int i = 0; i < tri.indices.Length; i += 3)
            {
                var c = (tri.vertices[tri.indices[i]] + tri.vertices[tri.indices[i + 1]] + tri.vertices[tri.indices[i + 2]]) / 3f;
                bool inRoom = _rooms.Any(r => InFloor(r, c, 0.05f)) || _doors.Any(d => { var o = Measure(d); return Vector3.Scale(c - o.centre, new Vector3(1, 0, 1)).magnitude < o.width * 0.5f + 0.5f; });
                if (!inRoom) outside++;
            }
            Check("S08", "everything is indoors: rays from every room in 24 directions at two heights all hit walls; floors under every probe; no walkable surface outside the rooms",
                escaped == 0 && outside == 0, $"{rays} rays, {escaped} escaped{(where.Count > 0 ? " (" + string.Join("; ", where) + ")" : "")}; NavMesh triangles outside rooms/doorways: {outside} of {tri.indices.Length / 3}");
        }

        // ---------------------------------------------------------------- openings

        void StaticOpenings()
        {
            // listed openings: clear width, height, and that they are open (a ray passes through the middle)
            var lines = new List<string>(); bool ok = true;
            foreach (var d in _doors)
            {
                var o = Measure(d);
                var mid = o.centre + Vector3.up * Mathf.Min(1.0f, o.height * 0.5f) + o.along * Mathf.Min(0.2f, o.width * 0.2f);
                var a = mid - o.through * 1.2f; var b = mid + o.through * 1.2f;
                bool passes = !Physics.Linecast(a, b, ~((1 << LayerDoorLeaf) | (1 << LayerSeeThrough) | (1 << LayerMarker)), QueryTriggerInteraction.Ignore);
                bool shutLeaf = d.door != null && Physics.Linecast(a, b, 1 << LayerDoorLeaf, QueryTriggerInteraction.Ignore);
                bool good = passes && o.width >= 0.9f && o.height >= 2.0f && (d.state == "open" ? !shutLeaf : shutLeaf);
                if (!good) ok = false;
                lines.Add($"{d.roomA} | {d.roomB} [{d.state}] w {F(o.width)} h {F(o.height)}{(good ? "" : " <- FAIL")}");
            }
            Check("S09", "every listed opening is a door or doorway at least 0.9 m clear and 2 m high; open ones clear, shut and hidden ones closed by a leaf", ok,
                $"{_doors.Count} openings: " + string.Join("; ", lines));

            // unlisted openings: probe every wall of every room outward through the wall gap
            int probes = 0, holes = 0; var where = new List<string>();
            int structural = ~((1 << LayerDoorLeaf) | (1 << LayerSeeThrough) | (1 << LayerMarker) | (1 << LayerCeiling));
            foreach (var r in _rooms)
            {
                var q = Outline(r); float fy = FloorY(r), h = _height[r.roomName];
                var sides = new (Vector3 o, Vector3 along, float len, Vector3 n)[]
                {
                    (new Vector3(q.xMin, 0, q.yMin), Vector3.right, q.width, Vector3.back),
                    (new Vector3(q.xMin, 0, q.yMax), Vector3.right, q.width, Vector3.forward),
                    (new Vector3(q.xMin, 0, q.yMin), Vector3.forward, q.height, Vector3.left),
                    (new Vector3(q.xMax, 0, q.yMin), Vector3.forward, q.height, Vector3.right),
                };
                foreach (var s in sides)
                    for (float t = 0.15f; t < s.len - 0.1f; t += 0.25f)
                        for (float y = 0.25f; y < h - 0.15f; y += 0.35f)
                        {
                            var p = s.o + s.along * t + Vector3.up * (fy + y) - s.n * 0.08f;
                            probes++;
                            if (Physics.Raycast(p, s.n, 0.75f, structural, QueryTriggerInteraction.Ignore)) continue;
                            // an opening: it must lie inside a listed doorway of this room
                            bool listed = _doors.Any(d => (d.roomA == r.roomName || d.roomB == r.roomName) && InOpening(d, p + s.n * 0.3f));
                            if (!listed) { holes++; if (where.Count < 8) where.Add($"{r.roomName} {P(p)}"); }
                        }
            }
            Check("S10", "the scene has no opening between rooms (or to outside) that is not listed: probes through every wall every 0.25 m x 0.35 m",
                holes == 0, $"{probes} probes, {holes} unlisted openings{(where.Count > 0 ? ": " + string.Join("; ", where) : "")}");
        }

        bool InOpening(DoorwayInfo d, Vector3 p)
        {
            var o = Measure(d);
            float along = Vector3.Dot(p - o.centre, o.along);
            float through = Vector3.Dot(p - o.centre, o.through);
            return Mathf.Abs(along) <= o.width * 0.5f + 0.1f && p.y >= o.centre.y - 0.05f && p.y <= o.centre.y + o.height + 0.05f && Mathf.Abs(through) < 1.2f;
        }

        // ---------------------------------------------------------------- the gun room and its entrance

        void StaticGunRoom()
        {
            var gun = Room("gun room"); var hall = Room("dining hall"); var foyer = Room("foyer");
            var ways = _doors.Where(d => d.Connects(gun.roomName)).ToList();
            var toFoyer = ways.FirstOrDefault(d => d.Other(gun.roomName) == foyer.roomName);
            var toHall = ways.FirstOrDefault(d => d.Other(gun.roomName) == hall.roomName);
            var deck = Find("GalleryDeck").bounds;
            bool under = false; string underStr = "-";
            if (toHall != null)
            {
                var o = Measure(toHall);
                var hallSide = o.centre + (toHall.roomA == hall.roomName ? -o.through : o.through) * 0.8f;
                under = hallSide.x >= deck.min.x && hallSide.x <= deck.max.x && hallSide.z >= deck.min.z && hallSide.z <= deck.max.z && deck.min.y > hallSide.y + 2.2f;
                underStr = $"hall side of the back door {P(hallSide)}; deck x {F(deck.min.x)}..{F(deck.max.x)} z {F(deck.min.z)}..{F(deck.max.z)}, underside {F(deck.min.y)}";
            }
            Check("S11", "the gun room has exactly two ways in: the entrance from the foyer and a way from the dining hall beneath the gallery",
                ways.Count == 2 && toFoyer != null && toHall != null && under, $"{ways.Count} openings: {string.Join(", ", ways.Select(d => d.Other(gun.roomName)))}; {underStr}");
            // the player sees the entrance from the foyer
            var eye = _player.position + Vector3.up * 1.6f;
            var oe = Measure(toFoyer);
            var target = oe.centre + Vector3.up * 1.4f;
            bool seen = !FirstHit(eye, target, SightMask, out var h);
            int visibleFrom = 0, foyerPts = 0;
            foreach (var p in Grid(Outline(foyer), 0.5f, 0.5f, FloorY(foyer) + 1.6f))
            {
                foyerPts++;
                if (!FirstHit(p, target, SightMask, out _)) visibleFrom++;
            }
            Check("S12", "the foyer is where the player sees the gun room's entrance: clear sight line from the start position to the doorway",
                seen, $"start eye {P(eye)} -> entrance {P(target)}: {(seen ? "clear" : "blocked by " + h.collider.name)}; the doorway is in view from {visibleFrom}/{foyerPts} foyer points");
        }

        void StaticGuardsHidden()
        {
            var gun = Room("gun room"); var foyer = Room("foyer");
            var guards = Object.FindObjectsByType<EnemyMarker>(FindObjectsInactive.Exclude).Where(e => RoomAt(e.transform.position + Vector3.up * 0.1f, _rooms) == gun).ToList();
            int pairs = 0, seen = 0; var who = new HashSet<string>();
            foreach (var eyeY in new[] { 1.6f, 1.0f })
                foreach (var p in Grid(Outline(foyer), 0.5f, 0.45f, FloorY(foyer) + eyeY))
                    foreach (var g in guards)
                    {
                        pairs++;
                        if (SeesBody(p, g)) { seen++; who.Add($"{g.kind}@{P(g.transform.position)} from {P(p)}"); }
                    }
            Check("S13", "the gun room's guards are out of sight from everywhere in the foyer (standing and crouched eyes on a 0.5 m grid, 6 body points each)",
                seen == 0 && guards.Count > 0, $"{guards.Count} gun-room markers, {pairs} eye/marker pairs, {seen} with a clear line{(who.Count > 0 ? ": " + string.Join("; ", who.Take(5)) : "")}");
            // ... and come into view at the doorway
            var door = _doors.First(d => d.Connects(gun.roomName) && d.Connects(foyer.roomName));
            var o = Measure(door);
            var towardGun = door.roomA == gun.roomName ? -o.through : o.through;
            var ranged = guards.Where(g => g.kind == "ranged").ToList();
            var inView = new List<string>();
            foreach (var step in new[] { 0.3f, 0.6f })
            {
                var eye = o.centre + towardGun * step + Vector3.up * 1.6f;
                foreach (var g in ranged) if (SeesBody(eye, g)) inView.Add(P(g.transform.position));
            }
            Check("S14", "the guards come into view at the doorway (eye in the doorway, 0.3-0.6 m past its middle)",
                inView.Distinct().Count() >= 2, $"ranged guards in view from the doorway: {inView.Distinct().Count()} of {ranged.Count} [{string.Join(", ", inView.Distinct())}]");
            // ranged guards face the foyer
            var facing = ranged.Select(g => Vector3.Dot(g.transform.forward, (o.centre - g.transform.position).normalized)).ToList();
            Check("S15", "ranged enemies stand behind the guarded entrance, facing the foyer",
                ranged.Count >= 2 && facing.All(f => f > 0.5f) && ranged.All(g => Vector3.Distance(g.transform.position, o.centre) < 6.5f),
                string.Join("; ", ranged.Select((g, i) => $"{P(g.transform.position)} dist {F(Vector3.Distance(g.transform.position, o.centre))} m, facing.dirToEntrance {F(facing[i])}")));
        }

        // ---------------------------------------------------------------- way round and gallery

        public static Vector3 HallEntry, GalleryRail;

        void FindWayPoints()
        {
            var hall = Room("dining hall");
            var stairDoor = _doors.First(d => d.Connects(hall.roomName) && d.Other(hall.roomName) == "Back Stair" && d.state == "open");
            var o = Measure(stairDoor);
            var into = stairDoor.roomA == hall.roomName ? -o.through : o.through;
            HallEntry = o.centre + into * 0.6f;
            var rb = _rooms.First(r => r.kind == "dining hall").GetComponentsInChildren<Collider>().First(c => c.name == "RailBlocker").bounds;
            GalleryRail = new Vector3(12.6f, Find("GalleryDeck").bounds.max.y, rb.min.z - 0.5f);
        }

        void StaticWayRound()
        {
            FindWayPoints();
            var path = new NavMeshPath();
            NavMesh.SamplePosition(_player.position, out var s, 1f, NavMesh.AllAreas);
            NavMesh.SamplePosition(HallEntry, out var e, 1f, NavMesh.AllAreas);
            bool round = NavMesh.CalculatePath(s.position, e.position, NavMesh.AllAreas & ~(1 << AreaGunRoom), path) && path.status == NavMeshPathStatus.PathComplete;
            float lenRound = Length(path);
            var direct = new NavMeshPath();
            NavMesh.CalculatePath(s.position, e.position, NavMesh.AllAreas, direct);
            var through = direct.corners.Select(c => RoomAt(c + Vector3.up * 0.1f, _rooms)).Where(r => r != null).Select(r => r.roomName).Distinct();
            var roundRooms = new List<string>();
            for (int i = 0; i + 1 < path.corners.Length; i++)
                for (float t = 0; t <= 1f; t += 0.1f)
                {
                    var r = RoomAt(Vector3.Lerp(path.corners[i], path.corners[i + 1], t) + Vector3.up * 0.1f, _rooms);
                    if (r != null && (roundRooms.Count == 0 || roundRooms[roundRooms.Count - 1] != r.roomName)) roundRooms.Add(r.roomName);
                }
            Check("S16", "a way leads from the foyer to the dining hall without passing through the gun room (NavMesh with the gun room's floor excluded)",
                round && !roundRooms.Contains(Room("gun room").roomName), $"path {path.status}, {F(lenRound)} m via {string.Join(" > ", roundRooms)}; the direct way is {F(Length(direct))} m");
        }

        static float Length(NavMeshPath p) { float l = 0; for (int i = 0; i + 1 < p.corners.Length; i++) l += Vector3.Distance(p.corners[i], p.corners[i + 1]); return l; }

        void StaticGallery()
        {
            var hall = Room("dining hall");
            var deck = Find("GalleryDeck").bounds;
            var q = Outline(hall); float fy = FloorY(hall);
            float rise = deck.max.y - fy;
            float runs = (Mathf.Min(deck.max.x, q.xMax) - Mathf.Max(deck.min.x, q.xMin)) / q.width;
            bool alongSide = runs >= 0.9f && (Mathf.Abs(deck.min.z - q.yMin) < 0.05f || Mathf.Abs(deck.max.z - q.yMax) < 0.05f);
            Check("S17", "a gallery runs along one side of the dining hall, high above the floor", rise >= 3.5f && alongSide,
                $"deck top {F(deck.max.y)} m = {F(rise)} m above the hall floor; covers {F(runs * 100f)}% of the {F(q.width)} m south side, {F(deck.size.z)} m deep");
            // much of the floor can be seen from the rail
            var rb = hall.GetComponentsInChildren<Collider>().First(c => c.name == "RailBlocker").bounds;
            var eyes = new List<Vector3>();
            for (float x = rb.min.x + 0.5f; x <= rb.max.x - 0.5f; x += 1f) eyes.Add(new Vector3(x, deck.max.y + 1.6f, rb.min.z - 0.4f));
            int pts = 0, vis = 0; int visCrouch = 0, all = 0, allVis = 0;
            foreach (var p in Grid(q, 0.5f, 0.25f, fy + 0.02f))
            {
                if (p.z <= deck.max.z) continue; // under the deck
                all++;
                bool anyEye = false;
                foreach (var e in eyes) if (!FirstHit(e, p, SightMask & ~(1 << LayerMarker), out _)) { anyEye = true; break; }
                if (anyEye) allVis++;
                // the floor a defender or a player can stand on: on the NavMesh and clear of furniture
                if (!NavMesh.SamplePosition(p, out var nh, 0.2f, NavMesh.AllAreas) || Physics.CheckSphere(p + Vector3.up * 0.6f, 0.15f, ~((1 << LayerSeeThrough) | (1 << LayerMarker)), QueryTriggerInteraction.Ignore)) continue;
                pts++;
                bool v = false;
                foreach (var e in eyes) if (!FirstHit(e, p, SightMask & ~(1 << LayerMarker), out _)) { v = true; break; }
                if (v) vis++;
                bool vc = false;
                foreach (var e in eyes) if (!FirstHit(e - Vector3.up * 0.6f, p, SightMask & ~(1 << LayerMarker), out _)) { vc = true; break; }
                if (vc) visCrouch++;
            }
            float frac = pts > 0 ? (float)vis / pts : 0f;
            Check("S18", "much of the hall floor can be seen from the gallery's rail (standing eye at the rail; open floor, i.e. walkable and not under furniture, on a 0.5 m grid beyond the deck)",
                frac >= 0.6f, $"{vis}/{pts} open-floor points in view = {F(frac * 100f)}% (crouched at the rail: {F(100f * visCrouch / Mathf.Max(1, pts))}%); counting the floor under tables and chairs too: {allVis}/{all} = {F(100f * allVis / Mathf.Max(1, all))}%");
            // reachable from where the way round comes in, without crossing the hall floor or being seen
            var path = new NavMeshPath();
            NavMesh.SamplePosition(HallEntry, out var s, 1f, NavMesh.AllAreas);
            NavMesh.SamplePosition(GalleryRail, out var g, 1f, NavMesh.AllAreas);
            bool ok = NavMesh.CalculatePath(s.position, g.position, NavMesh.AllAreas & ~(1 << AreaHallFloor), path) && path.status == NavMeshPathStatus.PathComplete;
            var enemies = Object.FindObjectsByType<EnemyMarker>(FindObjectsInactive.Exclude);
            int samples = 0, seenAt = 0; Vector3 firstSeen = Vector3.zero;
            for (int i = 0; i + 1 < path.corners.Length; i++)
            {
                float len = Vector3.Distance(path.corners[i], path.corners[i + 1]);
                for (float t = 0; t < len; t += 0.3f)
                {
                    var p = Vector3.Lerp(path.corners[i], path.corners[i + 1], t / len);
                    if (p.y > deck.max.y - 0.2f && p.x > deck.min.x - 0.05f) break; // on the gallery itself
                    samples++;
                    if (enemies.Any(e => EnemySees(e, p))) { if (seenAt == 0) firstSeen = p; seenAt++; }
                }
            }
            Check("S19", "the gallery is reachable from where the way round comes into the hall without crossing the hall floor, and unseen (no defender's 140-degree view cone has a clear line) until the gallery",
                ok && seenAt == 0, $"path {path.status}, {F(Length(path))} m from {P(s.position)} to {P(g.position)} with the hall floor excluded; {samples} points before the gallery, {seenAt} in an enemy's sight{(seenAt > 0 ? " first at " + P(firstSeen) : "")}");
        }

        // ---------------------------------------------------------------- stage, strong room, doors

        void StaticStage()
        {
            var ball = Room("ballroom");
            var stage = Find("StageTop").bounds;
            float rise = stage.max.y - FloorY(ball);
            var steps = ball.transform.Find("Stage").GetComponentsInChildren<Renderer>().Where(r => r.name == "Step").Select(r => r.bounds).OrderBy(b => b.max.y).ToList();
            var risers = new List<float>(); float prev = FloorY(ball);
            foreach (var b in steps) { risers.Add(b.max.y - prev); prev = b.max.y; }
            bool stepsOk = steps.Count >= 3 && risers.All(x => x > 0.05f && x <= 0.3f) && Mathf.Abs(prev - stage.max.y) < 0.02f;
            var chair = Object.FindAnyObjectByType<ChairGoal>().transform.position;
            bool onStage = chair.x > stage.min.x && chair.x < stage.max.x && chair.z > stage.min.z && chair.z < stage.max.z && Mathf.Abs(chair.y - stage.max.y) < 0.05f;
            float far = (chair.z - Outline(ball).yMin) / Outline(ball).height;
            Check("S20", "the master's chair stands on a stage with steps, at the ballroom's far end", rise >= 0.6f && stepsOk && onStage && far > 0.8f,
                $"stage top {F(rise)} m above the floor; {steps.Count} steps, risers [{string.Join(", ", risers.Select(F))}]; chair at {P(chair)} on the stage: {onStage}; {F(far * 100)}% of the way from the entrance wall");
        }

        void StaticStrongRoom()
        {
            var sr = Room("strong room");
            var ways = _doors.Where(d => d.Connects(sr.roomName)).ToList();
            bool hidden = ways.Count == 1 && ways[0].state == "hidden" && ways[0].door != null && !ways[0].door.isOpen;
            var path = new NavMeshPath();
            var q = Outline(sr);
            NavMesh.SamplePosition(new Vector3(q.center.x, FloorY(sr), q.center.y), out var a, 1.5f, NavMesh.AllAreas);
            NavMesh.SamplePosition(_player.position, out var b, 1f, NavMesh.AllAreas);
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas & ~(1 << AreaHidden), path);
            bool sealedOff = path.status != NavMeshPathStatus.PathComplete;
            var withDoor = new NavMeshPath();
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, withDoor);
            // the hidden leaf looks like its neighbours: the same bookcase model as the other bookcases on that wall
            string leafMesh = ways.Count == 1 && ways[0].door != null ? ways[0].door.hinges[0].GetComponentInChildren<MeshFilter>().sharedMesh.name : "-";
            var lib = _rooms.First(r => r.roomName == ways[0].Other(sr.roomName));
            int same = lib.GetComponentsInChildren<MeshFilter>().Count(m => m.sharedMesh != null && m.sharedMesh.name == leafMesh);
            Check("S21", "the strong room has one way in, hidden (a bookcase that swings on E), shut at the start; no other way reaches it",
                hidden && sealedOff && withDoor.status == NavMeshPathStatus.PathComplete,
                $"{ways.Count} opening(s): {string.Join(", ", ways.Select(d => d.Other(sr.roomName) + " [" + d.state + "]"))}; path with the hidden threshold excluded: {path.status}; through it: {withDoor.status}; the leaf is a '{leafMesh}', like {same - 1} other bookcases in the {lib.roomName}");
            var doorsOfLib = _doors.Count(d => d.Connects(sr.roomName));
            Check("S22", "the strong room lies on no way through (a dead end: one opening)", doorsOfLib == 1, $"{doorsOfLib} opening(s)");
        }

        void StaticDoorsOpenable()
        {
            var lines = new List<string>(); bool ok = true;
            foreach (var d in _doors.Where(x => x.state != "open"))
            {
                var sw = d.door;
                bool has = sw != null && sw.hinges != null && sw.hinges.Length > 0;
                var cols = has ? sw.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.gameObject.layer == LayerDoorLeaf).ToList() : new List<Collider>();
                bool reach = cols.Count > 0 && cols.All(c => c.GetComponentInParent<SwingDoor>() == sw);
                if (!(has && reach && !sw.locked)) ok = false;
                lines.Add($"{d.roomA} | {d.roomB} [{d.state}]: {cols.Count} solid leaf collider(s) under a SwingDoor with Interact(){(sw != null && sw.locked ? " LOCKED" : "")}");
            }
            Check("S23", "every shut door (and the hidden one) has a solid collider whose parent carries void Interact(), and is not locked", ok, string.Join("; ", lines));
        }

        // ---------------------------------------------------------------- cover

        void StaticOccluders()
        {
            var parts = new List<string>(); bool ok = true;
            foreach (var kind in new[] { "gun room", "dining hall", "ballroom" })
            {
                var r = Room(kind);
                var cols = r.transform.Find("Props").GetComponentsInChildren<Collider>().Concat(r.transform.Find("Structure")?.GetComponentsInChildren<Collider>() ?? new Collider[0])
                    .Concat(r.GetComponentsInChildren<Collider>().Where(c => c.transform.parent != null && c.transform.parent.name == "Gallery"))
                    .Where(c => !c.isTrigger && c.gameObject.layer == 0 && !c.name.StartsWith("Wall")).Distinct().ToList();
                float fy = FloorY(r);
                var groups = cols.Select(c => c.bounds).Where(b => b.min.y < fy + 0.2f).ToList();
                int tall = groups.Count(b => b.max.y - fy >= 1.7f && Mathf.Max(b.size.x, b.size.z) >= 0.6f);
                int low = groups.Count(b => b.max.y - fy >= 0.75f && b.max.y - fy <= 1.4f && Mathf.Max(b.size.x, b.size.z) >= 0.8f);
                if (tall < 3 || low < 3) ok = false;
                parts.Add($"{kind}: {tall} tall (>= 1.7 m), {low} low (0.75-1.4 m)");
            }
            // behind cover, a crouched player in the hall is hidden from the gallery where a standing one is not
            var hall = Room("dining hall");
            var gallery = Object.FindObjectsByType<EnemyMarker>(FindObjectsInactive.Exclude).Where(e => e.transform.position.y > 3f).ToList();
            int pts = 0, hiddenStand = 0, hiddenCrouch = 0;
            foreach (var p in Grid(Outline(hall), 0.5f, 0.4f, FloorY(hall)))
            {
                if (p.z < Find("GalleryDeck").bounds.max.z + 0.5f) continue;
                if (Physics.CheckSphere(p + Vector3.up * 0.5f, 0.3f, 1, QueryTriggerInteraction.Ignore)) continue; // inside furniture
                pts++;
                bool st = gallery.All(e => !Sees(e.Eye, p + Vector3.up * 1.6f, null));
                bool cr = gallery.All(e => !Sees(e.Eye, p + Vector3.up * 1.0f, null));
                if (st) hiddenStand++; if (cr) hiddenCrouch++;
            }
            Check("S24", "tall and low occluders to hide behind, standing or crouching", ok && hiddenCrouch > hiddenStand,
                string.Join("; ", parts) + $"; hall floor points hidden from every gallery gun: standing {hiddenStand}/{pts}, crouched {hiddenCrouch}/{pts}");
        }

        // ---------------------------------------------------------------- markers

        void StaticMarkers()
        {
            var enemies = Object.FindObjectsByType<EnemyMarker>(FindObjectsInactive.Exclude).ToList();
            var by = enemies.GroupBy(e => RoomAt(e.transform.position + Vector3.up * 0.1f, _rooms)?.roomName ?? "?").ToDictionary(g => g.Key, g => g.ToList());
            var hall = Room("dining hall").roomName; var gun = Room("gun room").roomName; var ball = Room("ballroom").roomName; var foyer = Room("foyer").roomName;
            int hallN = by.TryGetValue(hall, out var hl) ? hl.Count : 0;
            bool largest = by.All(kv => kv.Key == hall || kv.Value.Count < hallN);
            var deckY = Find("GalleryDeck").bounds.max.y;
            int galleryRanged = hl?.Count(e => e.kind == "ranged" && Mathf.Abs(e.transform.position.y - deckY) < 0.1f) ?? 0;
            int gunMelee = by.TryGetValue(gun, out var gl) ? gl.Count(e => e.kind == "melee") : 0;
            int master = by.TryGetValue(ball, out var bl) ? bl.Count(e => e.kind == "master") : 0;
            int guards = bl?.Count(e => e.kind != "master") ?? 0;
            bool ok = largest && galleryRanged >= 2 && gunMelee >= 2 && master == 1 && guards >= 2 && !by.ContainsKey(foyer) && !by.ContainsKey("?");
            Check("S25", "defenders: ranged hold the gallery, the dining hall holds the largest group, melee fill the gun room, the master and his bodyguards hold the ballroom, the foyer holds no one",
                ok, string.Join(", ", by.OrderByDescending(kv => kv.Value.Count).Select(kv => $"{kv.Key} {kv.Value.Count} ({string.Join("/", kv.Value.GroupBy(e => e.kind).Select(g => g.Count() + " " + g.Key))})")) + $"; ranged on the gallery deck: {galleryRanged}");
            var pick = Object.FindObjectsByType<PickupMarker>(FindObjectsInactive.Exclude).ToList();
            var best = pick.FirstOrDefault(p => p.kind == "best gear"); var hoard = pick.FirstOrDefault(p => p.kind == "hoard");
            bool pk = pick.Count == 2 && best != null && hoard != null && RoomAt(best.transform.position + Vector3.up * 0.05f, _rooms)?.kind == "gun room" && RoomAt(hoard.transform.position + Vector3.up * 0.05f, _rooms)?.kind == "strong room";
            Check("S26", "two pickups: the gun room's best gear and the strong room's hoard", pk, string.Join("; ", pick.Select(p => $"{p.kind} in {RoomAt(p.transform.position + Vector3.up * 0.05f, _rooms)?.roomName} at {P(p.transform.position)}")));
        }

        void StaticSparse()
        {
            var lines = new List<(string room, float frac)>();
            foreach (var r in _rooms)
            {
                var props = r.transform.Find("Props");
                if (props == null) continue;
                var cols = props.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToList();
                int n = 0, occ = 0; float fy = FloorY(r);
                foreach (var p in Grid(Outline(r), 0.5f, 0.25f, fy + 0.5f))
                {
                    n++;
                    foreach (var c in cols) { var b = c.bounds; if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && b.max.y > fy + 0.3f && b.min.y < fy + 2.0f) { occ++; break; } }
                }
                lines.Add((r.roomName, (float)occ / Mathf.Max(1, n)));
            }
            var ball = Room("ballroom").roomName;
            float bf = lines.First(l => l.room == ball).frac;
            Check("S27", "no room is sparser than the ballroom (share of floor under furniture, 0.5 m grid)", lines.All(l => l.room == ball || l.frac > bf),
                string.Join(", ", lines.OrderBy(l => l.frac).Select(l => $"{l.room} {F(l.frac * 100)}%")));
        }

        // ---------------------------------------------------------------- the map

        void StaticMap()
        {
            string path = Path.Combine(Root, "Map", "map.json");
            if (!File.Exists(path)) { Check("S28", "Map/map.json matches the scene", false, "Map/map.json missing"); return; }
            var map = MiniJson.D(MiniJson.Parse(File.ReadAllText(path)));
            var errs = new List<string>();
            var rooms = MiniJson.L(map["rooms"]);
            if (rooms.Count != _rooms.Count) errs.Add($"rooms {rooms.Count} vs scene {_rooms.Count}");
            foreach (var o in rooms)
            {
                var d = MiniJson.D(o); var name = (string)d["name"];
                var r = _rooms.FirstOrDefault(x => x.roomName == name);
                if (r == null) { errs.Add("no room " + name); continue; }
                var outline = MiniJson.L(MiniJson.L(d["floor"])[0]);
                var c0 = MiniJson.L(outline[0]); var c2 = MiniJson.L(outline[2]);
                var q = Outline(r);
                if (Mathf.Abs(MiniJson.N(c0[0]) - q.xMin) > 0.02f || Mathf.Abs(MiniJson.N(c0[1]) - q.yMin) > 0.02f || Mathf.Abs(MiniJson.N(c2[0]) - q.xMax) > 0.02f || Mathf.Abs(MiniJson.N(c2[1]) - q.yMax) > 0.02f) errs.Add("outline " + name);
                if ((string)d["kind"] != r.kind) errs.Add("kind " + name);
                if (Mathf.Abs(MiniJson.N(d["floor_y"]) - FloorY(r)) > 0.02f) errs.Add("floor_y " + name);
            }
            var doors = MiniJson.L(map["doors"]);
            if (doors.Count != _doors.Count) errs.Add($"doors {doors.Count} vs scene {_doors.Count}");
            foreach (var o in doors)
            {
                var d = MiniJson.D(o); var rs = MiniJson.L(d["rooms"]); var pos = MiniJson.L(d["position"]);
                var p = new Vector3(MiniJson.N(pos[0]), MiniJson.N(pos[1]), MiniJson.N(pos[2]));
                var match = _doors.FirstOrDefault(x => x.Connects((string)rs[0]) && x.Connects((string)rs[1]) && Vector3.Distance(Measure(x).centre, p) < 0.05f);
                if (match == null) errs.Add($"door {rs[0]}|{rs[1]} at {P(p)}");
                else if (Mathf.Abs(Measure(match).width - MiniJson.N(d["width"])) > 0.03f || (string)d["state"] != match.state) errs.Add($"door {rs[0]}|{rs[1]} width/state");
            }
            var enemies = MiniJson.L(map["enemies"]);
            var sceneEnemies = Object.FindObjectsByType<EnemyMarker>(FindObjectsInactive.Exclude).ToList();
            if (enemies.Count != sceneEnemies.Count) errs.Add($"enemies {enemies.Count} vs {sceneEnemies.Count}");
            foreach (var o in enemies)
            {
                var d = MiniJson.D(o); var pos = MiniJson.L(d["position"]);
                var p = new Vector3(MiniJson.N(pos[0]), MiniJson.N(pos[1]), MiniJson.N(pos[2]));
                var e = sceneEnemies.FirstOrDefault(x => Vector3.Distance(x.transform.position, p) < 0.05f && x.kind == (string)d["kind"]);
                if (e == null) errs.Add($"enemy {d["kind"]} at {P(p)}");
                else if (RoomAt(e.transform.position + Vector3.up * 0.1f, _rooms)?.roomName != (string)d["room"]) errs.Add($"enemy room at {P(p)}");
            }
            var pickups = MiniJson.L(map["pickups"]);
            if (pickups.Count != 2) errs.Add("pickups " + pickups.Count);
            var chair = MiniJson.L(map["chair"]);
            if (Vector3.Distance(new Vector3(MiniJson.N(chair[0]), MiniJson.N(chair[1]), MiniJson.N(chair[2])), Object.FindAnyObjectByType<ChairGoal>().transform.position) > 0.05f) errs.Add("chair");
            var gal = MiniJson.D(map["gallery"]);
            if (Mathf.Abs(MiniJson.N(gal["deck_y"]) - Find("GalleryDeck").bounds.max.y) > 0.02f) errs.Add("deck_y");
            var start = MiniJson.D(map["start"]); var sp = MiniJson.L(start["position"]);
            var spos = new Vector3(MiniJson.N(sp[0]), MiniJson.N(sp[1]), MiniJson.N(sp[2]));
            if (new Vector2(spos.x - _player.position.x, spos.z - _player.position.z).magnitude > 0.05f || Mathf.Abs(spos.y - _player.position.y) > 0.1f) errs.Add($"start {P(spos)} vs player {P(_player.position)}");
            Check("S28", "Map/map.json matches the scene (rooms, outlines, doors, gallery, chair, start, enemies, pickups re-measured)", errs.Count == 0,
                errs.Count == 0 ? $"{rooms.Count} rooms, {doors.Count} doors, {enemies.Count} enemies, {pickups.Count} pickups agree" : string.Join("; ", errs.Take(10)));
        }

        // ---------------------------------------------------------------- light

        IEnumerator Brightness()
        {
            var go = new GameObject("LumCam");
            var cam = go.AddComponent<Camera>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            cam.fieldOfView = 60f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.enabled = false;
            var rt = new RenderTexture(192, 108, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(192, 108, TextureFormat.RGB24, false);
            var res = new List<(string room, string kind, float lum, float lit)>();
            foreach (var r in _rooms)
            {
                var q = Outline(r); float fy = FloorY(r);
                float sum = 0, litSum = 0; int views = 0;
                // stand where a player can stand, as near the room's middle as the floor allows
                var stand = RoomPoint(r);
                foreach (var a in new[] { 0f, 90f, 180f, 270f })
                {
                    var c = new Vector3(stand.x, fy + 1.6f, stand.z);
                    go.transform.SetPositionAndRotation(c, Quaternion.Euler(12f, a, 0f));
                    var req = new RenderPipeline.StandardRequest { destination = rt };
                    RenderPipeline.SubmitRenderRequest(cam, req);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 192, 108), 0, 0); tex.Apply(); RenderTexture.active = prev;
                    var px = tex.GetPixels();
                    float s = 0; int lit = 0;
                    foreach (var p in px) { float l = 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b; s += l; if (l > 0.06f) lit++; }
                    sum += s / px.Length; litSum += (float)lit / px.Length; views++;
                    yield return null;
                }
                res.Add((r.roomName, r.kind, sum / views, litSum / views));
            }
            Object.Destroy(go); rt.Release();
            var sorted = res.OrderByDescending(x => x.lum).ToList();
            float median = sorted[sorted.Count / 2].lum;
            var hall = res.First(x => x.kind == "dining hall"); var ball = res.First(x => x.kind == "ballroom");
            Check("S29", "the dining hall and the ballroom are bright: both above the median room (mean screen luminance, 4 views from the walkable point nearest the room's middle)",
                hall.lum > median && ball.lum > median, string.Join(", ", sorted.Select(x => $"{x.room} {F(x.lum)}")));
            Check("S30", "lit so a player can see where to walk, dark rooms included: in every room at least 35% of the view is above luminance 0.06",
                res.All(x => x.lit >= 0.35f), string.Join(", ", res.OrderBy(x => x.lit).Select(x => $"{x.room} {F(x.lit * 100)}%")));
        }

        IEnumerator OverviewToggle()
        {
            var ov = OverviewMode.Instance;
            bool ok = ov != null && ov.overviewCamera != null;
            string m = "no OverviewMode";
            if (ok)
            {
                ov.SetActive(true);
                yield return null; yield return null;
                yield return StartCoroutine(Screen("overview_M", 1.0f));
                bool on = OverviewMode.Active && ov.overviewCamera.enabled && ov.overviewCamera.orthographic && (ov.overviewCamera.cullingMask & (1 << LayerCeiling)) == 0;
                // every ceiling is on the Ceiling layer
                int ceilings = 0, offLayer = 0;
                foreach (var r in _rooms) foreach (var rend in r.transform.Find("Ceiling").GetComponentsInChildren<Renderer>()) { ceilings++; if (rend.gameObject.layer != LayerCeiling) offLayer++; }
                // the whole level fits the view
                var b = new Bounds(); bool init = false;
                foreach (var r in _rooms) foreach (var q in FloorRects(r)) { var bb = new Bounds(new Vector3(q.center.x, 0, q.center.y), new Vector3(q.width, 1, q.height)); if (!init) { b = bb; init = true; } else b.Encapsulate(bb); }
                var vp0 = ov.overviewCamera.WorldToViewportPoint(b.min); var vp1 = ov.overviewCamera.WorldToViewportPoint(b.max);
                bool fits = vp0.x >= 0 && vp0.y >= 0 && vp1.x <= 1 && vp1.y <= 1;
                ov.SetActive(false);
                yield return null;
                ok = on && offLayer == 0 && ceilings >= _rooms.Count && fits && !OverviewMode.Active;
                m = $"overview on: {on} (orthographic, Ceiling layer culled); {ceilings} ceiling renderers, {offLayer} off the Ceiling layer; whole level in view: {fits}; toggled back: {!OverviewMode.Active}";
            }
            Check("S31", "M toggles a top-down overview of the whole level with the ceilings hidden (OverviewMode.Toggle, bound to the M key)", ok, m);
        }
    }
}
