using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using static Hollowmere.Checks.LevelSurvey;

namespace Hollowmere.Checks
{
    public partial class CheckSuite
    {
        class Leg
        {
            public string id, name;
            public Vector3 goal;
            public int areaMask = NavMesh.AllAreas;
            public bool sprint;
            public float arrive = 0.6f;
            public bool scored = true;
            public Func<Vector3, string> rule;     // returns a violation or null
            // results
            public bool ok;
            public string why = "";
            public float time, walked;
            public int stalls;
            public List<string> doors = new List<string>();
            public List<string> violations = new List<string>();
            public List<string> rooms = new List<string>();
            public Vector3 start, end;
        }

        const float StallWindow = 2.5f, StallDistance = 0.3f;

        IEnumerator Walks()
        {
            FindWayPoints();
            var gun = Room("gun room"); var hall = Room("dining hall"); var sr = Room("strong room"); var lib = Room("library");
            var gunQ = Outline(gun); var hallQ = Outline(hall);
            var deck = Find("GalleryDeck").bounds;
            var best = Object.FindObjectsByType<PickupMarker>(FindObjectsInactive.Exclude).First(p => p.kind == "best gear");
            var hoard = Object.FindObjectsByType<PickupMarker>(FindObjectsInactive.Exclude).First(p => p.kind == "hoard");
            var chair = Object.FindAnyObjectByType<ChairGoal>();
            var hidden = _doors.First(d => d.state == "hidden");
            var hiddenO = Measure(hidden);
            var libSide = hidden.roomA == lib.roomName ? -hiddenO.through : hiddenO.through;
            Vector3 hiddenStand = hiddenO.centre + libSide * 2.6f + hiddenO.along * -0.9f;
            float hudStart = LevelHud.Elapsed;
            var enemies = Object.FindObjectsByType<EnemyMarker>(FindObjectsInactive.Exclude).ToList();
            bool seenOnClimb = false; Vector3 seenWhere = Vector3.zero; var seenAtDoor = new HashSet<string>();
            var galleryDoor = _doors.First(d => d.Connects(hall.roomName) && d.state == "shut" && Measure(d).centre.y > 2f);
            var galleryDoorO = Measure(galleryDoor);
            Vector3 gunEntry = Vector3.zero; bool enteredGun = false;

            var legs = new List<Leg>
            {
                new Leg { id = "W1", name = "the way round: from the start to the dining hall without passing through the gun room", goal = HallEntry,
                          areaMask = NavMesh.AllAreas & ~(1 << AreaGunRoom), arrive = 0.45f,
                          rule = p => InRect(gunQ, p, 0f) && p.y < 3f ? "entered the gun room at " + P(p) : null },
                new Leg { id = "W2", name = "up to the gallery from where the way round comes into the hall, without crossing the hall floor", goal = GalleryRail,
                          areaMask = NavMesh.AllAreas & ~(1 << AreaHallFloor), arrive = 0.5f,
                          rule = p =>
                          {
                              if (InRect(hallQ, p, -0.6f) && p.y < deck.max.y - 1f) return "on the hall floor at " + P(p);
                              bool onGallery = p.y > deck.max.y - 0.2f && p.x > hallQ.xMin;
                              bool atDoor = Vector3.Distance(p, galleryDoorO.centre) < 1.0f && p.y > deck.max.y - 0.2f;
                              if (atDoor) foreach (var e in enemies) if (EnemySees(e, p)) seenAtDoor.Add(e.kind + "@" + P(e.transform.position));
                              if (!onGallery && !atDoor && !seenOnClimb && enemies.Any(e => EnemySees(e, p))) { seenOnClimb = true; seenWhere = p; }
                              return null;
                          } },
                new Leg { id = "T1", name = "transit: back down the stair to the hall door", goal = HallEntry, scored = false, sprint = true },
                new Leg { id = "W3", name = "from the dining hall into the gun room beneath the gallery, to its best gear", goal = Vector3.zero, arrive = 0.5f,
                          rule = p =>
                          {
                              if (!enteredGun && InRect(gunQ, p, 0f)) { enteredGun = true; gunEntry = p; }
                              return null;
                          } },
                new Leg { id = "T2", name = "transit: across the hall to the library's west aisle", goal = hiddenStand, scored = false, sprint = true },
                new Leg { id = "W4", name = "the hidden way into the strong room, to the hoard", goal = Vector3.zero, arrive = 0.5f },
                new Leg { id = "W4b", name = "... and back out to the library", goal = hiddenStand, arrive = 0.6f },
                new Leg { id = "T3", name = "transit: back to the dining hall", goal = new Vector3(16f, FloorY(hall), 25.0f), scored = false, sprint = true },
                new Leg { id = "W5", name = "from the dining hall on to the master's chair", goal = Vector3.zero, arrive = 0.35f },
            };
            // goals that depend on the scene: stand by the gear, by the hoard, beside the chair
            legs[3].goal = StandBeside(best.transform.position);
            legs[5].goal = StandBeside(hoard.transform.position);
            legs[8].goal = StandBeside(chair.transform.position, 1.15f);
            // a tour of the remaining rooms, opening every other shut door on the way
            var tourRooms = _rooms.Where(r => new[] { "chapel", "crypt", "storeroom", "boiler room", "cellar cells", "watch room", "kitchen", "butcher's room", "laboratory" }.Contains(r.kind)).ToList();
            var order = new[] { "laboratory", "chapel", "crypt", "kitchen", "storeroom", "butcher's room", "cellar cells", "boiler room", "watch room" };
            foreach (var k in order)
            {
                var r = tourRooms.First(x => x.kind == k);
                legs.Add(new Leg { id = "R-" + k, name = "tour: into the " + k, goal = RoomPoint(r), scored = false, sprint = true, arrive = 0.8f });
            }

            yield return StartCoroutine(Screen("walk_00_start_in_the_foyer"));
            foreach (var leg in legs)
            {
                yield return StartCoroutine(Walk(leg));
                string hud = LevelHud.CurrentRoomName; var actual = RoomAt(_player.position + Vector3.up * 0.1f, _rooms)?.roomName;
                if (actual == null)
                {
                    // standing in a doorway's wall gap: either of its rooms is right
                    var gapDoor = _doors.OrderBy(d => Vector3.Distance(Measure(d).centre, _player.position)).First();
                    actual = gapDoor.Connects(hud) ? hud : gapDoor.roomA + " or " + gapDoor.roomB;
                }
                leg.rooms.Add($"HUD '{hud}' / measured '{actual}'");
                if (leg.ok && hud != actual) { leg.ok = false; leg.why += $" HUD names '{hud}' but the player is in '{actual}'"; }
                string tag = leg.ok ? "PASS" : "FAIL";
                string detail = $"{F(leg.time)} s, {F(leg.walked)} m walked from {P(leg.start)} to {P(leg.end)}; stalls {leg.stalls}; doors opened with E: [{string.Join(", ", leg.doors)}]; " +
                                $"rooms: {string.Join(" > ", leg.rooms)}{(leg.violations.Count > 0 ? "; violations: " + string.Join("; ", leg.violations.Take(3)) : "")}{(leg.why.Length > 0 ? "; " + leg.why : "")}";
                if (leg.scored) Check(leg.id, leg.name, leg.ok, detail);
                else { Log($"[{tag}] {leg.id} {leg.name}\n         measured: {detail}"); if (!leg.ok) _fail++; }
                if (!leg.ok) { Log("Walk failed; the remaining walks are not run (the player would start from the wrong place)."); break; }
                // per-walk extras
                if (leg.id == "W1") yield return StartCoroutine(Screen("walk_01_the_way_round_comes_into_the_hall"));
                if (leg.id == "W2")
                {
                    _player.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); // look out over the rail
                    yield return StartCoroutine(Screen("walk_02_at_the_gallery_rail"));
                }
                if (leg.id == "W3") yield return StartCoroutine(Screen("walk_03_at_the_best_gear"));
                if (leg.id == "W4") yield return StartCoroutine(Screen("walk_04_at_the_hoard"));
                if (leg.id == "W5")
                {
                    var toChair = chair.transform.position - _player.position; toChair.y = 0;
                    _player.rotation = Quaternion.LookRotation(toChair.normalized, Vector3.up);
                    yield return StartCoroutine(Screen("walk_05_the_masters_chair_announced"));
                }
                if (leg.id == "W2")
                    Check("W2s", "the climb to the gallery (stair and landing, up to the gallery door) is unseen by every defender (140-degree view cones, clear lines to head, chest or knees)", !seenOnClimb,
                        (seenOnClimb ? "first seen at " + P(seenWhere) : "unseen all the way up") + $"; standing in the opened gallery door (within 1 m of it) the player is in view of: [{string.Join(", ", seenAtDoor)}]");
                if (leg.id == "W3")
                {
                    bool under = enteredGun && gunEntry.z > gunQ.yMax - 0.6f;
                    var bt = best.transform.position; var pp = _player.position;
                    float reach = new Vector2(bt.x - pp.x, bt.z - pp.z).magnitude;
                    Check("W3s", "the player came into the gun room from behind (through the back door under the gallery) and reached the best gear",
                        under && reach < 1.3f, $"entered the gun room at {P(gunEntry)} (back wall at z {F(gunQ.yMax)}); {F(reach)} m from the best gear at {P(bt)}");
                }
                if (leg.id == "W4")
                {
                    var ht = hoard.transform.position; var pp = _player.position;
                    float reach = new Vector2(ht.x - pp.x, ht.z - pp.z).magnitude;
                    Check("W4s", "the player opened the hidden bookcase with E, entered the strong room and reached the hoard",
                        leg.doors.Count > 0 && RoomAt(pp + Vector3.up * 0.1f, _rooms) == sr && reach < 1.3f, $"{F(reach)} m from the hoard; in '{RoomAt(pp + Vector3.up * 0.1f, _rooms)?.roomName}'");
                }
                if (leg.id == "W5")
                    Check("W5s", "reaching the master's chair is announced", ChairGoal.Reached,
                        ChairGoal.Reached ? $"ChairGoal reached at HUD time {LevelHud.FormatTime(ChairGoal.ReachedAt)}; the HUD shows the announcement" : "no announcement");
            }
            float hudEnd = LevelHud.Elapsed;
            Check("S32", "the HUD names the room the player is in and shows elapsed time", hudEnd > hudStart + 5f,
                $"HUD clock went from {LevelHud.FormatTime(hudStart)} to {LevelHud.FormatTime(hudEnd)}; room names checked at the end of every leg above");
            _input.MoveInput(Vector2.zero); _input.SprintInput(false);
        }

        static bool InRect(Rect q, Vector3 p, float margin) => p.x > q.xMin - margin && p.x < q.xMax + margin && p.z > q.yMin - margin && p.z < q.yMax + margin;

        /// <summary>A walkable point next to a pickup or the chair, on the NavMesh, at most `reach` metres away.</summary>
        Vector3 StandBeside(Vector3 target, float reach = 0.95f)
        {
            Vector3 best = target; float bestD = float.MaxValue;
            for (int a = 0; a < 16; a++)
                for (float r = 0.6f; r <= reach + 0.01f; r += 0.1f)
                {
                    var p = target + Quaternion.Euler(0, a * 22.5f, 0) * Vector3.forward * r;
                    if (!NavMesh.SamplePosition(new Vector3(p.x, target.y, p.z), out var hit, 1.3f, NavMesh.AllAreas)) continue;
                    var flat = new Vector2(hit.position.x - target.x, hit.position.z - target.z).magnitude;
                    if (flat > reach) continue;
                    var path = new NavMeshPath();
                    NavMesh.SamplePosition(_player.position, out var s, 2f, NavMesh.AllAreas);
                    if (flat < bestD) { bestD = flat; best = hit.position; }
                }
            return best;
        }

        Vector3 RoomPoint(RoomInfo r)
        {
            var q = Outline(r); float fy = FloorY(r);
            var c = new Vector3(q.center.x, fy, q.center.y);
            for (float rad = 0f; rad < 5f; rad += 0.5f)
                for (int a = 0; a < 12; a++)
                {
                    var p = c + Quaternion.Euler(0, a * 30f, 0) * Vector3.forward * rad;
                    if (NavMesh.SamplePosition(p, out var hit, 0.4f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - fy) < 0.3f) return hit.position;
                }
            return c;
        }

        /// <summary>
        /// Walks the player to a leg's goal along a NavMesh route. The player is moved only by its own move
        /// (and sprint) input, turned to face along the route, and opens shut doors on the route with its
        /// E-key script. A stall (less than 0.3 m of progress in 2.5 s) fails the leg; there is no unsticking.
        /// </summary>
        IEnumerator Walk(Leg leg)
        {
            leg.start = _player.position;
            NavMesh.SamplePosition(_player.position, out var s, 1.5f, NavMesh.AllAreas);
            NavMesh.SamplePosition(leg.goal, out var g, 1.5f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(s.position, g.position, leg.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                leg.ok = false; leg.why = "no complete route " + path.status; leg.end = _player.position; yield break;
            }
            var pts = new List<Vector3>(path.corners);
            // shut doors that the route passes through
            var doors = _doors.Where(d => d.door != null && !d.door.isOpen && NearPolyline(Measure(d).centre, pts, 0.9f)).ToList();
            float t0 = Time.time, timeout = Length(path) / (leg.sprint ? 3.5f : 2.4f) + 25f;
            int seg = 0; var history = new List<(float t, Vector3 p)>();
            Vector3 last = _player.position; float lastTrace = -1f;
            string lastRoom = null;
            while (true)
            {
                var p = _player.position;
                leg.walked += Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(last.x, 0, last.z)); last = p;
                var room = RoomAt(p + Vector3.up * 0.1f, _rooms)?.roomName;
                if (room != null && room != lastRoom) { leg.rooms.Add(room); lastRoom = room; }
                if (Time.time - lastTrace > 0.2f) { lastTrace = Time.time; _trace.Append($"{leg.id},{F(Time.time - t0)},{F(p.x)},{F(p.y)},{F(p.z)},{room},{LevelHud.CurrentRoomName}\n"); }
                if (leg.rule != null) { var v = leg.rule(p); if (v != null && !leg.violations.Contains(v)) leg.violations.Add(v); }
                // arrived?
                var flat = new Vector2(p.x - leg.goal.x, p.z - leg.goal.z).magnitude;
                if (flat <= leg.arrive && Mathf.Abs(p.y - leg.goal.y) < 1.0f) break;
                if (Time.time - t0 > timeout) { leg.why = "timed out"; break; }
                // a shut door ahead on the route: stop, face it, press E
                var door = doors.FirstOrDefault(d => !d.door.isOpen && new Vector2(p.x - Measure(d).centre.x, p.z - Measure(d).centre.z).magnitude <= (d.state == "hidden" ? 2.7f : 1.9f) && Mathf.Abs(p.y - Measure(d).centre.y) < 1.5f);
                if (door != null)
                {
                    _input.MoveInput(Vector2.zero); _input.SprintInput(false);
                    yield return new WaitForSeconds(0.35f);
                    var c = Measure(door).centre;
                    var aim = c - _player.position; aim.y = 0;
                    _player.rotation = Quaternion.LookRotation(aim.normalized, Vector3.up);
                    yield return new WaitForSeconds(0.3f);
                    _interact.Interact();
                    float w = Time.time;
                    while (!door.door.IsFullyOpen && Time.time - w < 4f) yield return null;
                    if (!door.door.IsFullyOpen)
                    {
                        var cam = Camera.main.transform;
                        string hitName = Physics.Raycast(cam.position, cam.forward, out var h, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? h.collider.name : "nothing";
                        leg.why = $"the {door.roomA}|{door.roomB} door did not open (E ray hit {hitName})"; break;
                    }
                    leg.doors.Add($"{door.roomA}|{door.roomB}");
                    history.Clear(); t0 += 0f;
                    continue;
                }
                // follow the route: aim at a point a little ahead on it
                var target = Lookahead(pts, p, ref seg, 0.8f);
                var dir = target - p; dir.y = 0;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float cur = _player.eulerAngles.y;
                    _player.rotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(cur, yaw, 540f * Time.deltaTime), 0f);
                }
                float turn = Mathf.Abs(Mathf.DeltaAngle(_player.eulerAngles.y, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg));
                _input.MoveInput(turn > 70f ? Vector2.zero : Vector2.up);
                _input.SprintInput(leg.sprint && turn < 25f);
                // stall watch
                history.Add((Time.time, p));
                history.RemoveAll(hh => Time.time - hh.t > StallWindow + 0.1f);
                if (history.Count > 1 && Time.time - history[0].t >= StallWindow && Vector3.Distance(history[0].p, p) < StallDistance)
                {
                    leg.stalls++; leg.why = $"stalled at {P(p)}"; break;
                }
                yield return null;
            }
            _input.MoveInput(Vector2.zero); _input.SprintInput(false);
            yield return new WaitForSeconds(0.4f);
            leg.time = Time.time - t0;
            leg.end = _player.position;
            var fl = new Vector2(leg.end.x - leg.goal.x, leg.end.z - leg.goal.z).magnitude;
            leg.ok = leg.why.Length == 0 && leg.violations.Count == 0 && fl <= leg.arrive + 0.3f;
            if (leg.why.Length == 0 && fl > leg.arrive + 0.3f) leg.why = $"ended {F(fl)} m from the goal";
        }

        static bool NearPolyline(Vector3 c, List<Vector3> pts, float r)
        {
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(c - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                var q = a + ab * t;
                if (new Vector2(q.x - c.x, q.z - c.z).magnitude < r && Mathf.Abs(q.y - c.y) < 1.6f) return true;
            }
            return false;
        }

        /// <summary>Pure pursuit: the point `ahead` metres along the route past the player's nearest point on it.</summary>
        static Vector3 Lookahead(List<Vector3> pts, Vector3 p, ref int seg, float ahead)
        {
            if (pts.Count == 1) return pts[0];
            // nearest point on the route from the current segment onward (a few segments ahead)
            int bestSeg = seg; float bestT = 0, bestD = float.MaxValue;
            for (int i = seg; i < Mathf.Min(pts.Count - 1, seg + 4); i++)
            {
                var a = pts[i]; var b = pts[i + 1]; var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                var q = a + ab * t;
                float d = (q - p).sqrMagnitude;
                if (d < bestD) { bestD = d; bestSeg = i; bestT = t; }
            }
            seg = bestSeg;
            float left = ahead;
            var cur = Vector3.Lerp(pts[seg], pts[seg + 1], bestT);
            int k = seg;
            while (true)
            {
                var nxt = pts[k + 1];
                float d = Vector3.Distance(cur, nxt);
                if (d >= left) return cur + (nxt - cur).normalized * left;
                left -= d; cur = nxt; k++;
                if (k + 1 >= pts.Count) return pts[pts.Count - 1];
            }
        }
    }
}
