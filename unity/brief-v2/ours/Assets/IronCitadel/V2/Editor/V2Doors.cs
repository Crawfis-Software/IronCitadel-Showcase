using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IronCitadel.Rooms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronCitadelV2.Editor
{
    public static partial class IronCitadelV2Builder
    {
        // ------------------------------------------------------------------ the rooms' own leaves

        /// <summary>A door leaf a room stands shut in a design door's doorway, lifted out by
        /// <see cref="LiftRoomLeaves"/>.</summary>
        sealed class RoomLeaf
        {
            public string room;
            public GameObject go;
            /// <summary>Its renderers were drawn: false for the twin a neighbour stands in the same place, which
            /// <see cref="HideDoubledEdgePieces"/> hid.</summary>
            public bool drawn;
            /// <summary>Where it stood (Synty's leaves pivot on the hinge) and the middle of its meshes, world space.</summary>
            public Vector3 pivot, mid, scale;
            public Quaternion rotation;
            /// <summary>The doorway set piece it stood in: that piece's walls and jambs do not block its open pose.</summary>
            public Transform holder;
        }

        static readonly Regex LeafName = new Regex(@"^SM_(Bld|Env)_(Base_)?Door_(\d|Large)", RegexOptions.CultureInvariant);

        /// <summary>The mansion's rooms stand each edge doorway with its leaves shut in it (the manor_room_door set
        /// piece, 2026-10-14; a set piece carries no script). At every design door's edge, each leaf either room stands
        /// there -- the outermost GameObject named SM_Bld_[Base_]Door_* or SM_Env_Door_* (never a frame) inside a
        /// doorway set piece (SP_*_Doorways_*), its pivot within 0.6 m of the edge line, 6 m of its middle and 1 m of
        /// the floor -- is lifted out (made inactive, colliders and all) before the openings are measured, so each
        /// doorway measures as built; <see cref="BuildDoors"/> stands the leaves again as the design door asks.</summary>
        static void LiftRoomLeaves(Ctx c)
        {
            var said = new List<string>();
            foreach (var d in c.level.doors)
            {
                var e = V2Model.EdgeOfDoor(c.level, d);
                var found = new List<RoomLeaf>();
                foreach (var id in new[] { d.a, d.b })
                {
                    if (id == null || !c.rooms.TryGetValue(id, out var room)) continue;
                    var taken = new HashSet<Transform>();
                    foreach (var t in room.GetComponentsInChildren<Transform>(false))
                    {
                        if (!LeafName.IsMatch(t.name) || HasAncestorIn(t, taken)) continue;
                        var holder = DoorwayOf(t, room.transform);
                        if (holder == null) continue;
                        var off = t.position - e.centre;
                        if (Mathf.Abs(Vector3.Dot(off, e.n)) > 0.6f || Mathf.Abs(Vector3.Dot(off, e.along)) > 6f || Mathf.Abs(off.y) > 1f) continue;
                        taken.Add(t);
                        found.Add(new RoomLeaf
                        {
                            room = id, go = t.gameObject, drawn = Drawn(t).Length > 0,
                            pivot = t.position, mid = t.TransformPoint(LocalBounds(t.gameObject, t).center), scale = t.lossyScale,
                            rotation = t.rotation, holder = holder,
                        });
                    }
                }
                if (found.Count == 0) continue;
                foreach (var l in found) l.go.SetActive(false);
                c.leaves[d.id] = found;
                said.Add($"{d.id} ({string.Join(", ", found.GroupBy(l => l.room).Select(g => $"{g.Key} x{g.Count()}"))})");
            }
            Physics.SyncTransforms();
            c.Log(said.Count == 0 ? "room leaves: no room stands a leaf in a design door's doorway" :
                $"room leaves lifted out of {said.Count} doorway(s) before measuring: {string.Join(", ", said)}");
        }

        static bool HasAncestorIn(Transform t, HashSet<Transform> set)
        {
            for (var x = t.parent; x != null; x = x.parent)
                if (set.Contains(x)) return true;
            return false;
        }

        /// <summary>The doorway set piece a leaf stands in: its nearest ancestor below the room named SP_*_Doorways_*,
        /// else null.</summary>
        static Transform DoorwayOf(Transform t, Transform room)
        {
            for (var x = t.parent; x != null && x != room; x = x.parent)
                if (x.name.StartsWith("SP_", StringComparison.Ordinal) && x.name.IndexOf("_Doorways_", StringComparison.Ordinal) >= 0) return x;
            return null;
        }

        /// <summary>The leaves a design door stands again: one of each pair of twins (where both rooms stand the same
        /// doorway, turned half round, their leaves coincide), the drawn one first, then room a's.</summary>
        static List<RoomLeaf> OneOfEachTwin(V2Door d, List<RoomLeaf> leaves)
        {
            var kept = new List<RoomLeaf>();
            foreach (var l in leaves.OrderByDescending(l => l.drawn).ThenBy(l => l.room == d.a ? 0 : 1))
                if (!kept.Any(k => k.room != l.room && V2Model.Flat(k.mid - l.mid).magnitude < 0.25f)) kept.Add(l);
            return kept;
        }

        /// <summary>A design door hung from the rooms' own leaves: each leaf <see cref="OneOfEachTwin"/> keeps stands
        /// again where it stood, a fresh copy of its prefab on a hinge at its pivot. The open pose swings every leaf 90
        /// degrees about its hinge into one room, its free end (from the hinge toward the leaf's middle) leading; of the
        /// two rooms the one whose open leaves touch the fewest colliders (the doorway set pieces' own walls and jambs
        /// aside) wins, then the room on <paramref name="firstSide"/> (-1 room a, +1 room b). Returns the door, its open
        /// pose's hits, the room it swings into and what each room's pose touched.</summary>
        static (GameObject root, BuiltDoor bd, int hits, string what, string tried) HangRoomLeaves(Ctx c, V2Door d, Opening o, List<RoomLeaf> leaves, float firstSide = -1f)
        {
            var e = o.e;
            var keep = OneOfEachTwin(d, leaves);
            var root = new GameObject(d.id);
            root.transform.SetParent(c.featT, false);
            root.transform.position = o.Centre;
            var door = root.AddComponent<IronCitadelV2Door>();
            door.doorId = d.id; door.kind = "shut"; door.roomA = d.a; door.roomB = d.b; door.showPrompt = true; door.duration = 1f;
            door.disableCollidersWhenOpen = false;
            var hinges = new List<Transform>(); var frees = new List<Vector3>();
            foreach (var l in keep)
            {
                float at = Vector3.Dot(l.pivot - e.centre, e.along) - o.tc;
                var hinge = new GameObject(keep.Count == 1 ? "Hinge" : at >= 0f ? "Hinge +" : "Hinge -").transform;
                hinge.SetParent(root.transform, false);
                hinge.SetPositionAndRotation(l.pivot, Quaternion.identity);
                StandLeafCopy(l, hinge);
                hinges.Add(hinge);
                frees.Add(e.along * (Vector3.Dot(l.mid - l.pivot, e.along) >= 0f ? 1f : -1f));
            }
            door.hinges = hinges.ToArray();
            var bd = new BuiltDoor { design = d, door = door, closed = hinges.Select(t => t.localRotation).ToArray() };
            var ignore = new HashSet<Collider>(leaves.Where(l => l.holder != null).SelectMany(l => l.holder.GetComponentsInChildren<Collider>(false)));
            Physics.SyncTransforms();

            float[] best = null; int bestHits = int.MaxValue; string bestWhat = null; var tried = new List<string>();
            foreach (float side in new[] { firstSide, -firstSide })
            {
                var into = e.n * side;                   // n points from a to b
                door.openAngles = frees.Select(f => Vector3.SignedAngle(f, into, Vector3.up) >= 0f ? 90f : -90f).ToArray();
                var blockers = OpenPoseBlockers(bd, ignore);
                string what = $"into {(side < 0 ? d.a : d.b)}";
                tried.Add($"{what}: {blockers.Count} hit(s){(blockers.Count > 0 ? " (" + string.Join(", ", blockers.Take(2)) + ")" : "")}");
                if (blockers.Count < bestHits) { best = door.openAngles; bestHits = blockers.Count; bestWhat = what; }
                if (bestHits == 0) break;
            }
            door.openAngles = best;
            return (root, bd, bestHits, bestWhat, string.Join("; ", tried));
        }

        /// <summary>A copy of a room's leaf on <paramref name="hinge"/>, where the leaf stood: its prefab, else a clone
        /// of it; never static (a static leaf would not swing), and given a box collider if it has none, so it bars the
        /// doorway and the walker's E ray finds it.</summary>
        static GameObject StandLeafCopy(RoomLeaf l, Transform hinge)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(l.go);
            var path = src != null ? AssetDatabase.GetAssetPath(src) : null;
            GameObject go;
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) go = Spawn(path, hinge, l.go.name);
            else { go = Object.Instantiate(l.go, hinge); go.name = l.go.name; }
            go.SetActive(true);
            go.transform.SetPositionAndRotation(l.pivot, l.rotation);
            go.transform.localScale = l.scale;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            if (go.GetComponentInChildren<Collider>(true) == null)
            {
                var lb = LocalBounds(go, go.transform);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = lb.center; bc.size = lb.size;
            }
            return go;
        }

        static string LeafNames(BuiltDoor bd) => string.Join(" and ", bd.door.hinges
            .Select(h => h.childCount > 0 ? h.GetChild(0).name : "?").GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Count()} {g.Key}" : g.Key));

        /// <summary>A shut door from the rooms' own leaves (<see cref="HangRoomLeaves"/>): it opens with E and keeps its
        /// colliders open, as <see cref="BuildShutDoor"/>'s does.</summary>
        static void BuildRoomLeafDoor(Ctx c, V2Door d, Opening o, List<RoomLeaf> leaves)
        {
            var (root, bd, hits, what, tried) = HangRoomLeaves(c, d, o, leaves);
            var mk = root.AddComponent<IronCitadelMarker>();
            mk.kind = IronCitadelMarker.Kind.Feature; mk.room = d.id; mk.note = $"shut door between {d.a} and {d.b}, the rooms' own leaves: use it (E) to open it";
            c.doors.Add(bd);
            if (hits > 0) c.Log($"WARNING door {d.id}: either room's open pose touches something; kept the least ({hits})");
            c.Log($"door {d.id}: shut, the rooms' own {LeafNames(bd)} in the {F(o.width)} x {F(o.height)} m doorway, swinging {what}; open pose: {tried}");
        }

        /// <summary>An open door whose doorway the rooms stand leaves in: they stand open for good (the pose of
        /// <see cref="HangRoomLeaves"/>; no E, and <see cref="SetDoorsOpen"/> leaves them be) when a room's open pose
        /// touches nothing; else they stay out and the doorway is bare. They stand into the room more doors from the
        /// start when they can, pushed open the way the player goes in: standing into the near room, a leaf hides the
        /// doorway from anyone who comes at it slantwise (Hollowmere's start lost sight of the armory's doorway).</summary>
        static void StandRoomLeavesOpen(Ctx c, V2Door d, Opening o, List<RoomLeaf> leaves)
        {
            var hops = HopsFromStart(c.level);
            int ha = hops.TryGetValue(d.a, out var x) ? x : int.MaxValue, hb = hops.TryGetValue(d.b, out var y) ? y : int.MaxValue;
            var (root, bd, hits, what, tried) = HangRoomLeaves(c, d, o, leaves, hb > ha ? 1f : -1f);
            if (hits > 0)
            {
                Object.DestroyImmediate(root);
                Physics.SyncTransforms();
                c.Log($"door {d.id}: open doorway, no leaf ({F(o.width)} x {F(o.height)} m): the rooms' leaves would touch something standing open, so they stay out; {tried}");
                return;
            }
            Pose(bd, true);
            string names = LeafNames(bd);
            Object.DestroyImmediate(bd.door);
            Physics.SyncTransforms();
            c.Log($"door {d.id}: open, the rooms' own {names} standing open {what} ({F(o.width)} x {F(o.height)} m)");
        }

        /// <summary>Each room's count of design doors from the start room (breadth first; a room it never reaches is absent).</summary>
        static Dictionary<string, int> HopsFromStart(V2Level level)
        {
            var hops = new Dictionary<string, int> { [level.startRoom] = 0 };
            var queue = new Queue<string>();
            queue.Enqueue(level.startRoom);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                foreach (var dd in level.doors)
                {
                    var next = dd.a == r ? dd.b : dd.b == r ? dd.a : null;
                    if (next == null || hops.ContainsKey(next)) continue;
                    hops[next] = hops[r] + 1;
                    queue.Enqueue(next);
                }
            }
            return hops;
        }

        // ------------------------------------------------------------------ openings

        /// <summary>Every design door's doorway as the two room prefabs built it: along the shared edge, every 0.05 m
        /// within 6 m of its middle, the run where nothing stands across the edge at 0.4 m and 1.2 m (0.6 m either
        /// side of it) and there is floor 0.8 m either side (so the void beside a hall does not count); the run
        /// nearest the middle is the doorway. Its height is the lowest lintel over its middle.</summary>
        static void MeasureOpenings(Ctx c)
        {
            foreach (var d in c.level.doors)
            {
                var o = MeasureOpening(V2Model.EdgeOfDoor(c.level, d));
                c.openings[d.id] = o;
                if (o.found) c.Log($"opening {d.id} ({d.kind}) at {o.e}: centre t {F(o.tc)}, width {F(o.width)}, height {F(o.height)}, floor {F(o.floorY)}");
                else c.Problem($"opening {d.id} at {o.e}: no doorway found across the edge");
            }
        }

        static Opening MeasureOpening(V2Edge e)
        {
            const float step = 0.05f;
            var o = new Opening { e = e };
            var runs = new List<Vector2>();
            float? s = null;
            for (int k = -120; k <= 120; k++)
            {
                float t = k * step;
                var p = e.centre + e.along * t;
                bool clear = !SegmentHits(p - e.n * 0.6f + Vector3.up * 1.2f, p + e.n * 0.6f + Vector3.up * 1.2f)
                             && !SegmentHits(p - e.n * 0.6f + Vector3.up * 0.4f, p + e.n * 0.6f + Vector3.up * 0.4f)
                             && FloorNear(p - e.n * 0.8f) && FloorNear(p + e.n * 0.8f);
                if (clear && s == null) s = t;
                if (!clear && s != null) { runs.Add(new Vector2(s.Value, t - step)); s = null; }
            }
            if (s != null) runs.Add(new Vector2(s.Value, 6f));
            if (runs.Count == 0) return o;
            var best = runs.OrderBy(r => Mathf.Abs((r.x + r.y) / 2f)).First();
            o.found = true;
            o.tc = (best.x + best.y) / 2f;
            o.width = best.y - best.x + step;
            var mid = e.centre + e.along * o.tc;
            o.floorY = Physics.Raycast(mid + Vector3.up * 1.0f, Vector3.down, out var fh, 1.6f, AllLayers, QueryTriggerInteraction.Ignore) ? fh.point.y : 0f;
            float h = 8f;
            foreach (float k in new[] { -0.12f, 0f, 0.12f })
                if (Physics.Raycast(mid + e.n * k + Vector3.up * (o.floorY + 0.5f), Vector3.up, out var hit, 8f, AllLayers, QueryTriggerInteraction.Ignore))
                    h = Mathf.Min(h, 0.5f + hit.distance);
            o.height = h;
            return o;
        }

        static bool FloorNear(Vector3 p) =>
            Physics.Raycast(new Vector3(p.x, 1.0f, p.z), Vector3.down, out var hit, 1.6f, AllLayers, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y) < 0.5f;

        static bool SegmentHits(Vector3 a, Vector3 b)
        {
            var d = b - a; float len = d.magnitude;
            if (len < 1e-4f) return false;
            d /= len;
            return Physics.Raycast(a, d, len, AllLayers, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(b, -d, len, AllLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>How far the wall face beside the doorway stands from the edge line on one side
        /// (<paramref name="sgn"/> -1: room a's side, +1: room b's side): the most protruding of eight rays cast at
        /// the edge from 2.5 m in, beside the opening. 0.27 m (the usual Synty frame) when nothing answers.</summary>
        static float FaceInset(Opening o, float sgn)
        {
            var e = o.e;
            float best = -1f;
            foreach (float off in new[] { o.width / 2f + 0.12f, o.width / 2f + 0.35f })
            foreach (float side in new[] { -1f, 1f })
            foreach (float h in new[] { 0.6f, 1.6f })
            {
                var from = e.centre + e.along * (o.tc + side * off) + e.n * (sgn * 2.5f) + Vector3.up * (o.floorY + h);
                if (!Physics.Raycast(from, -e.n * sgn, out var hit, 2.8f, AllLayers, QueryTriggerInteraction.Ignore)) continue;
                float inset = 2.5f - hit.distance;
                if (inset > 0.8f || inset < -0.3f) continue;
                best = Mathf.Max(best, inset);
            }
            return best < -0.5f ? 0.27f : best;
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

        // ------------------------------------------------------------------ doors

        /// <summary>Every design door as its kind asks. Where the rooms stand their own leaves in the doorway
        /// (<see cref="LiftRoomLeaves"/>), a shut door is those leaves (<see cref="BuildRoomLeafDoor"/>), an open one
        /// stands them open (<see cref="StandRoomLeavesOpen"/>) and the hidden one leaves them out; elsewhere a shut
        /// door is a Synty leaf stretched over the doorway (<see cref="BuildShutDoor"/>).</summary>
        static void BuildDoors(Ctx c)
        {
            foreach (var d in c.level.doors)
            {
                var o = c.openings[d.id];
                bool own = c.leaves.TryGetValue(d.id, out var leaves);
                if (d.kind == "open")
                {
                    if (own && o.found) StandRoomLeavesOpen(c, d, o, leaves);
                    else c.Log($"door {d.id}: open doorway, no leaf{(o.found ? $" ({F(o.width)} x {F(o.height)} m)" : "")}{(own ? $"; the rooms' {leaves.Count} leaves stay out" : "")}");
                    Physics.SyncTransforms();
                    continue;
                }
                if (!o.found)
                {
                    if (own) foreach (var l in leaves) l.go.SetActive(true);
                    c.Problem($"door {d.id} ({d.kind}): no doorway measured, so no leaf hung{(own ? "; the rooms' leaves stand as the rooms stood them" : "")}");
                    continue;
                }
                if (d.kind == "shut")
                {
                    if (own) BuildRoomLeafDoor(c, d, o, leaves);
                    else BuildShutDoor(c, d, o);
                }
                else
                {
                    BuildHiddenDoor(c, d, o);
                    if (own) c.Log($"door {d.id}: the rooms' {leaves.Count} leaves stay out of the hidden door's doorway");
                }
                Physics.SyncTransforms();
            }
        }

        /// <summary>The single Synty leaves the shut doors are cut from: (path, width, height). Each pivots on its hinge
        /// at its +x end and runs to -x. The one closest in shape to the doorway is stretched over it.</summary>
        static readonly (string path, float w, float h)[] DungeonLeaves =
        {
            (PD + "Environments/Walls/SM_Env_Door_01.prefab", 1.32f, 2.46f),
            (PD + "Environments/Walls/SM_Env_Door_Large_Wood_01.prefab", 1.70f, 4.61f),
            (PD + "Environments/Walls/SM_Env_Door_Large_Stone_01.prefab", 2.05f, 4.52f),
        };
        /// <summary>The mansion's: one panelled wooden leaf.</summary>
        static readonly (string path, float w, float h)[] MansionLeaves = { (HM + "Buildings/SM_Bld_Door_01.prefab", 1.13f, 2.47f) };
        static (string path, float w, float h)[] Leaves => s_mansion ? MansionLeaves : DungeonLeaves;

        /// <summary>A shut door: one leaf (a pair past 2.6 m) mounted on one room's wall face over the doorway, 0.15 m
        /// over each jamb and 0.1 m over the lintel (at most 4.8 m tall), hinged at a jamb so it swings 90 degrees into
        /// that room. Of the four mountings (either room, either jamb; a pair has two) the one whose open leaf touches
        /// the fewest colliders wins, then the first in that order. The leaves keep their colliders when open; the
        /// NavMesh is baked with them open.</summary>
        static void BuildShutDoor(Ctx c, V2Door d, Opening o)
        {
            float W = o.width + 0.3f, H = Mathf.Clamp(o.height + 0.1f, 2.3f, 4.8f);
            bool pair = W > 2.6f;
            float leafW = pair ? W / 2f : W;
            var leaf = Leaves.OrderBy(l => Mathf.Abs(Mathf.Log((leafW / l.w) / (H / l.h)))).First();
            var options = new List<(float side, float hs)>();
            foreach (float side in new[] { -1f, 1f })
                if (pair) options.Add((side, 0f));
                else { options.Add((side, 1f)); options.Add((side, -1f)); }

            GameObject bestGo = null; BuiltDoor bestDoor = null; int bestHits = int.MaxValue; string bestWhat = null; var tried = new List<string>();
            foreach (var (side, hs) in options)
            {
                var (go, bd) = MakeShutDoor(c, d, o, leaf, W, H, side, hs, pair);
                var blockers = OpenPoseBlockers(bd);
                string what = $"{(side < 0 ? d.a : d.b)}'s side{(pair ? "" : hs > 0 ? ", hinge +along" : ", hinge -along")}";
                tried.Add($"{what}: {blockers.Count} hit(s){(blockers.Count > 0 ? " (" + string.Join(", ", blockers.Take(2)) + ")" : "")}");
                if (blockers.Count < bestHits)
                {
                    if (bestGo != null) Object.DestroyImmediate(bestGo);
                    bestGo = go; bestDoor = bd; bestHits = blockers.Count; bestWhat = what;
                }
                else Object.DestroyImmediate(go);
                Physics.SyncTransforms();
                if (bestHits == 0) break;
            }
            c.doors.Add(bestDoor);
            if (bestHits > 0) c.Log($"WARNING door {d.id}: every mounting's open leaf touches something; kept the least ({bestHits})");
            c.Log($"door {d.id}: shut, {(pair ? "a pair of " : "")}{Path.GetFileNameWithoutExtension(leaf.path)} stretched to {F(leafW)} x {F(H)} m " +
                  $"over the {F(o.width)} x {F(o.height)} m doorway, mounted on {bestWhat}; open pose: {string.Join("; ", tried)}");
        }

        static (GameObject, BuiltDoor) MakeShutDoor(Ctx c, V2Door d, Opening o, (string path, float w, float h) leaf, float W, float H, float side, float hs, bool pair)
        {
            var e = o.e;
            var into = e.n * side;                       // into the room the leaf hangs in
            float face = FaceInset(o, side) + 0.01f;
            var root = new GameObject(d.id);
            root.transform.SetParent(c.featT, false);
            root.transform.SetPositionAndRotation(o.Centre, Quaternion.LookRotation(into, Vector3.up));
            var mk = root.AddComponent<IronCitadelMarker>();
            mk.kind = IronCitadelMarker.Kind.Feature; mk.room = d.id; mk.note = $"shut door between {d.a} and {d.b}: use it (E) to open it";
            var door = root.AddComponent<IronCitadelV2Door>();
            door.doorId = d.id; door.kind = "shut"; door.roomA = d.a; door.roomB = d.b; door.showPrompt = true; door.duration = 1f;
            door.disableCollidersWhenOpen = false;

            var hinges = new List<Transform>(); var angles = new List<float>();
            var ends = pair ? new[] { 1f, -1f } : new[] { hs };
            float lw = pair ? W / 2f : W;
            foreach (float h in ends)
            {
                // the leaf: width along `along`, its hinge end toward h*along, thickness toward `into`
                var hinge = new GameObject(ends.Length > 1 ? (h > 0 ? "Hinge +" : "Hinge -") : "Hinge").transform;
                hinge.SetParent(root.transform, false);
                var go = Spawn(leaf.path, hinge, "Leaf");
                var fwd = Vector3.Cross(e.along * h, Vector3.up);
                go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                go.transform.localScale = Vector3.one;
                var lb = LocalBounds(go, go.transform);
                go.transform.localScale = new Vector3(lw / Mathf.Max(0.05f, lb.size.x), H / Mathf.Max(0.05f, lb.size.y), 1f);
                var b = RendererBounds(go);
                float centreAlong = o.tc + (pair ? h * lw / 2f : 0f);
                float curAlong = Vector3.Dot(b.center - e.centre, e.along);
                float back = Mathf.Min(Vector3.Dot(b.min - e.centre, into), Vector3.Dot(b.max - e.centre, into));
                float thick = Mathf.Abs(Vector3.Dot(b.size, into));
                go.transform.position += e.along * (centreAlong - curAlong) + into * (face - back) + Vector3.up * (o.floorY - b.min.y);
                // the hinge: at the hinge end, on the leaf's room face, at the floor
                var hp = e.centre + e.along * (centreAlong + h * lw / 2f) + into * (face + thick);
                hp.y = o.floorY;
                var keep = go.transform.position; var keepRot = go.transform.rotation;
                hinge.position = hp;
                hinge.rotation = Quaternion.identity;
                go.transform.SetPositionAndRotation(keep, keepRot);
                hinges.Add(hinge);
                angles.Add(Vector3.SignedAngle(-e.along * h, into, Vector3.up) >= 0f ? 90f : -90f);
            }
            door.hinges = hinges.ToArray();
            door.openAngles = angles.ToArray();
            Physics.SyncTransforms();
            var bd = new BuiltDoor { design = d, door = door, closed = hinges.Select(t => t.localRotation).ToArray() };
            return (root, bd);
        }

        /// <summary>The colliders the door's leaves would touch standing open (their own, the floor under them, the
        /// door frames and <paramref name="ignore"/> aside).</summary>
        static List<string> OpenPoseBlockers(BuiltDoor bd, HashSet<Collider> ignore = null)
        {
            Pose(bd, true);
            Physics.SyncTransforms();
            var own = new HashSet<Collider>(bd.door.GetComponentsInChildren<Collider>(true));
            var hits = new List<string>();
            foreach (var col in own)
            {
                var b = col.bounds;
                var half = b.extents - new Vector3(0.04f, 0f, 0.04f);
                half.y = Mathf.Max(0.05f, (b.size.y - 0.25f) / 2f);
                var centre = new Vector3(b.center.x, b.min.y + 0.15f + half.y, b.center.z);
                foreach (var h in Physics.OverlapBox(centre, Vector3.Max(half, Vector3.one * 0.01f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore))
                {
                    if (own.Contains(h) || (ignore != null && ignore.Contains(h))) continue;
                    if (h.name.IndexOf("DoorFrame", StringComparison.OrdinalIgnoreCase) >= 0 || h.name.IndexOf("Door_Frame", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    hits.Add(PathOf(h.transform));
                }
            }
            Pose(bd, false);
            Physics.SyncTransforms();
            return hits.Distinct().ToList();
        }

        static void Pose(BuiltDoor bd, bool open)
        {
            for (int i = 0; i < bd.door.hinges.Length; i++)
                bd.door.hinges[i].localRotation = bd.closed[i] * Quaternion.Euler(0f, open ? bd.door.openAngles[i] : 0f, 0f);
        }

        /// <summary>Every shut and hidden door open (the NavMesh bake, floor.json and the room graph) or shut again. A
        /// door whose leaves drop their colliders when open (the bookcase) drops them here too.</summary>
        static void SetDoorsOpen(Ctx c, bool open)
        {
            foreach (var bd in c.doors)
            {
                Pose(bd, open);
                if (bd.door.disableCollidersWhenOpen)
                    foreach (var h in bd.door.hinges)
                        foreach (var col in h.GetComponentsInChildren<Collider>(true)) col.enabled = !open;
            }
            Physics.SyncTransforms();
            c.Log($"doors {(open ? "opened" : "shut")} ({c.doors.Count})");
        }

        /// <summary>v1's secret bookcase, on room a's side of the hidden door: a bookcase stretched over the doorway
        /// with its back on the wall face, hinged at the end away from the decoys so its free end swings into room a;
        /// a blocker box behind it so nobody squeezes past; up to two decoy bookcases beside it where the wall has room.
        /// Nothing marks it as a door (the HUD offers no prompt). Its colliders go when it opens.</summary>
        static void BuildHiddenDoor(Ctx c, V2Door d, Opening o)
        {
            var e = o.e;                                  // n points from a to b
            var into = -e.n;                              // into room a
            float inset = FaceInset(o, -1f);
            var face = e.centre + e.along * o.tc + into * (inset + 0.03f);
            face.y = o.floorY;
            var probe = face + into * 0.6f;
            float runPlus = FreeRun(probe, e.along), runMinus = FreeRun(probe, -e.along);
            var toDecoys = runPlus >= runMinus ? e.along : -e.along;
            float sx = Mathf.Max(1f, (o.width + 0.35f) / BookcaseW);
            float sy = Mathf.Max(1f, (o.height + 0.15f) / BookcaseH);
            float leafW = BookcaseW * sx;

            var root = new GameObject(d.id);
            root.transform.SetParent(c.featT, false);
            root.transform.position = face;
            var mk = root.AddComponent<IronCitadelMarker>();
            mk.kind = IronCitadelMarker.Kind.Feature; mk.room = d.id; mk.note = $"hidden door from {d.a} to {d.b}: a bookcase that swings aside with E";
            var secret = new GameObject("SecretDoor");
            secret.transform.SetParent(root.transform, false);
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(secret.transform, false);
            hinge.position = face - toDecoys * (leafW / 2f);
            hinge.rotation = Quaternion.identity;
            PlaceBookcase(hinge, "Bookcase", face, into, e.along, sx, sy);
            var blk = new GameObject("DoorBlocker");
            blk.transform.SetParent(hinge, true);
            blk.transform.position = face + into * 0.45f;
            blk.transform.rotation = Quaternion.LookRotation(into, Vector3.up);
            var bc = blk.AddComponent<BoxCollider>();
            bc.size = new Vector3(Mathf.Max(leafW, o.width + 0.3f), o.height + 0.1f, 0.8f);
            bc.center = new Vector3(0f, bc.size.y / 2f, 0f);

            var door = secret.AddComponent<IronCitadelV2Door>();
            door.doorId = d.id; door.kind = "hidden"; door.roomA = d.a; door.roomB = d.b; door.showPrompt = false; door.duration = 1f;
            door.disableCollidersWhenOpen = true;
            door.hinges = new[] { hinge };
            door.openAngles = new[] { Vector3.SignedAngle(toDecoys, into, Vector3.up) >= 0f ? 90f : -90f };
            c.doors.Add(new BuiltDoor { design = d, door = door, closed = new[] { hinge.localRotation } });

            var decoys = new GameObject("Decoys").transform;
            decoys.SetParent(root.transform, false);
            float run = toDecoys == e.along ? runPlus : runMinus;
            int placed = 0;
            for (int k = 1; k <= 2; k++)
            {
                float off = k * (leafW + 0.04f);
                if (off + leafW / 2f > run - 0.1f) break;
                PlaceBookcase(decoys, $"Bookcase {k}", face + toDecoys * off, into, e.along, sx, sy);
                placed++;
            }
            c.Log($"door {d.id}: hidden, a bookcase on {d.a}'s side, {F(leafW)} x {F(BookcaseH * sy)} m (x{F(sx)}, y{F(sy)}) over the {F(o.width)} x {F(o.height)} m doorway, " +
                  $"wall face {F(inset)} m in, opens {door.openAngles[0]:0} deg, {placed} decoy(s) (free run {F(runPlus, "0.0")}/{F(runMinus, "0.0")} m)");
        }

        static GameObject PlaceBookcase(Transform parent, string name, Vector3 face, Vector3 into, Vector3 along, float sx, float sy)
        {
            var go = Spawn(BookcasePrefab, parent, name);
            go.transform.rotation = Quaternion.Euler(0f, V2Model.Yaw(into), 0f);
            go.transform.localScale = new Vector3(sx, sy, 1f);
            go.transform.position = face;
            var b = RendererBounds(go);
            float back = Mathf.Min(Vector3.Dot(b.min, into), Vector3.Dot(b.max, into));
            float shiftIn = Vector3.Dot(face, into) - back;
            float shiftAlong = Vector3.Dot(face - b.center, along);
            go.transform.position += into * shiftIn + along * shiftAlong + Vector3.up * (face.y - b.min.y);
            return go;
        }
    }
}
