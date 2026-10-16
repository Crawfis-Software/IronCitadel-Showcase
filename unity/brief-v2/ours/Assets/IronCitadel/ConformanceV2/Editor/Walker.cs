// IronCitadel conformance kit v2: walks a temporary CharacterController along NavMesh paths in edit mode (v1's walker),
// opening shut doors through the E contract when it is blocked.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace IronCitadel.ConformanceV2
{
    public sealed class Snag
    {
        public Vector3 At;
        public string Node, What;
        public int Corner;
    }

    public sealed class WalkResult
    {
        public string Route, Note;
        public bool Pass;
        public float Planned, Walked;
        public readonly List<Snag> Snags = new List<Snag>();
        public readonly List<Vector3> Trace = new List<Vector3>();
        public readonly List<Vector3> Corners = new List<Vector3>();
        public readonly List<string> Opened = new List<string>();
        public readonly List<string> Rooms = new List<string>();

        public static WalkResult Missing(string route, string why) =>
            new WalkResult { Route = route, Pass = false, Note = "not walked: " + why };

        public JObj ToJson()
        {
            return new JObj().Add("route", Route).Add("pass", Pass).Add("note", Note)
                .Add("planned_m", Planned).Add("walked_m", Walked).Add("corners", Corners.Count)
                .Add("rooms_planned", Rooms.Cast<object>().ToList())
                .Add("doors_opened_by_e", Opened.Cast<object>().ToList())
                .Add("snags", Snags.Select(s => (object)new JObj().Add("at", s.At).Add("room", s.Node)
                    .Add("what", s.What).Add("corner", s.Corner)).ToList());
        }
    }

    /// <summary>The E contract: a ray from eye height, SendMessageUpwards("Interact") on the first solid collider.
    /// In edit mode a door's own swing never runs, so an Interact() receiver is opened by switching its colliders off.</summary>
    public static class Doors
    {
        static readonly Dictionary<Type, bool> s_has = new Dictionary<Type, bool>();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool HasInteract(Type t)
        {
            if (s_has.TryGetValue(t, out var b)) return b;
            b = t.GetMethods(Flags).Any(m => m.Name == "Interact" && m.GetParameters().Length <= 1);
            s_has[t] = b;
            return b;
        }

        /// <summary>The MonoBehaviour that SendMessageUpwards("Interact") would reach first, or null.</summary>
        public static MonoBehaviour Receiver(Collider c)
        {
            for (var t = c != null ? c.transform : null; t != null; t = t.parent)
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                    if (mb != null && HasInteract(mb.GetType())) return mb;
            return null;
        }

        /// <summary>The colliders opening a receiver switches off: all solid colliders under it, or only those on the hit
        /// object when the receiver holds too many or spans too much to be a door.</summary>
        public static List<Collider> CollidersOf(MonoBehaviour rec, Collider hit)
        {
            var cols = rec.GetComponentsInChildren<Collider>(false).Where(Solid).ToList();
            if (cols.Count > 0)
            {
                var b = cols[0].bounds;
                foreach (var c in cols) b.Encapsulate(c.bounds);
                if (cols.Count <= Tune.ReceiverMaxColliders && b.size.x <= Tune.ReceiverMaxSize && b.size.z <= Tune.ReceiverMaxSize) return cols;
            }
            return hit != null ? hit.GetComponents<Collider>().Where(Solid).ToList() : new List<Collider>();
        }

        public static bool Solid(Collider c) => c != null && c.enabled && !c.isTrigger && !(c is CharacterController);

        public static string PathOf(Transform t)
        {
            if (t == null) return "(none)";
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        /// <summary>Points to aim E at: the closest bounds point of each unopened receiver's solid colliders within
        /// InteractReach of the eye, nearest first (a player turns to the door they slid along).</summary>
        public static List<Vector3> NearReceivers(Vector3 eye, HashSet<MonoBehaviour> opened)
        {
            var best = new Dictionary<MonoBehaviour, Vector3>();
            foreach (var c in Physics.OverlapSphere(eye, Tune.InteractReach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!Solid(c)) continue;
                var rec = Receiver(c);
                if (rec == null || opened.Contains(rec)) continue;
                var p = c.bounds.ClosestPoint(eye);
                if (!best.TryGetValue(rec, out var q) || (p - eye).sqrMagnitude < (q - eye).sqrMagnitude) best[rec] = p;
            }
            return best.Values.OrderBy(p => (p - eye).sqrMagnitude).ToList();
        }

        /// <summary>Casts the E ray. Returns the receiver it reaches (null when none) and what it hit.</summary>
        public static MonoBehaviour Cast(Vector3 eye, Vector3 dir, out RaycastHit hit, out string what)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) { hit = default; what = "no direction"; return null; }
            // the first hit that is not a CharacterController: the walker's own capsule holds the eye, and with
            // queriesHitBackfaces on, a ray from inside it would hit it first
            hit = default;
            bool any = false;
            foreach (var h in Physics.RaycastAll(eye, dir.normalized, Tune.InteractReach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is CharacterController || (any && h.distance >= hit.distance)) continue;
                hit = h; any = true;
            }
            if (!any)
            {
                what = "the E ray hits nothing within " + Tune.InteractReach + " m";
                return null;
            }
            var rec = Receiver(hit.collider);
            what = rec != null
                ? "E hits " + PathOf(hit.collider.transform) + " at " + hit.distance.ToString("0.0") + " m; Interact() on " + rec.GetType().Name + " (" + rec.name + ")"
                : "E hits " + PathOf(hit.collider.transform) + " at " + hit.distance.ToString("0.0") + " m, with no Interact() on it or a parent";
            return rec;
        }
    }

    public static class Walker
    {
        /// <summary>Plans a NavMesh path through the waypoints with <paramref name="filter"/>, then drives a
        /// CharacterController (r 0.35, h 1.8, step 0.3, skin 0.02) along its corners. Blocked, it casts the E ray
        /// toward the next corner, then the one after, then at the nearest unopened door within reach and, on a
        /// receiver, calls Interact and switches the door's colliders off (after the last, it walks back to the previous
        /// corner first).</summary>
        public static WalkResult Walk(NavModel m, string route, List<Vector3?> waypoints, NavMeshQueryFilter filter)
        {
            var res = new WalkResult { Route = route };
            for (int i = 0; i < waypoints.Count; i++)
                if (waypoints[i] == null) { res.Note = "not walked: waypoint " + i + " has no NavMesh point"; return res; }

            var path = new NavMeshPath();
            for (int i = 0; i + 1 < waypoints.Count; i++)
            {
                var a = waypoints[i].Value; var b = waypoints[i + 1].Value;
                if (NavMesh.SamplePosition(a, out var ha, 2f, filter)) a = ha.position;
                if (NavMesh.SamplePosition(b, out var hb, 2f, filter)) b = hb.position;
                if (!NavMesh.CalculatePath(a, b, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    res.Note = "no complete NavMesh path for leg " + (i + 1) + " (" + path.status + ") from " + V(a) + " to " + V(b) +
                               (path.corners.Length > 0 ? "; it ends at " + V(path.corners[path.corners.Length - 1]) + " in " + m.NodeName(m.NodeOf(path.corners[path.corners.Length - 1])) : "");
                    return res;
                }
                var c = path.corners;
                for (int k = res.Corners.Count == 0 ? 0 : 1; k < c.Length; k++) res.Corners.Add(c[k]);
            }
            for (int i = 1; i < res.Corners.Count; i++) res.Planned += Vector3.Distance(res.Corners[i - 1], res.Corners[i]);
            string lastRoom = null;
            for (int i = 0; i < res.Corners.Count; i++)
            {
                var a = res.Corners[i]; var b = i + 1 < res.Corners.Count ? res.Corners[i + 1] : a;
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / Tune.PathSampleStep));
                for (int k = 0; k < n; k++)
                {
                    var name = m.NodeName(m.NodeOf(Vector3.Lerp(a, b, k / (float)n)));
                    if (name == "no room" || name == lastRoom) continue;
                    res.Rooms.Add(name); lastRoom = name;
                }
            }

            var opened = new List<Collider>();
            var openedRec = new HashSet<MonoBehaviour>();
            var go = new GameObject("__ConformanceV2Walker") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cc = go.AddComponent<CharacterController>();
                cc.radius = Tune.AgentRadius;
                cc.height = Tune.AgentHeight;
                cc.stepOffset = Tune.AgentClimb;
                cc.slopeLimit = Tune.AgentSlope;
                cc.skinWidth = Tune.WalkSkin;
                cc.minMoveDistance = 0f;
                cc.center = new Vector3(0, Tune.AgentHeight * 0.5f + Tune.WalkSkin, 0);
                cc.enabled = false;
                go.transform.position = res.Corners[0] + Vector3.up * 0.3f;
                cc.enabled = true;
                Physics.SyncTransforms();

                float vy = 0f;
                var last = go.transform.position;
                res.Trace.Add(last);
                int stuckLimit = Mathf.CeilToInt(Tune.WalkStuckSeconds / Tune.WalkDt);
                for (int i = 1; i < res.Corners.Count; i++)
                {
                    var target = res.Corners[i];
                    float best = HDist(go.transform.position, target);
                    int stuck = 0;
                    bool back = false;
                    int maxIter = Mathf.CeilToInt(best / (Tune.WalkSpeed * Tune.WalkDt)) * 4 + 400;
                    float lowY = Mathf.Min(res.Corners[i - 1].y, target.y);
                    for (int it = 0; it < maxIter; it++)
                    {
                        var pos = go.transform.position;
                        var to = target - pos; to.y = 0;
                        float d = to.magnitude;
                        if (d <= Tune.WalkReach) break;
                        var step = to / d * Mathf.Min(Tune.WalkSpeed * Tune.WalkDt, d);
                        vy = cc.isGrounded ? -1f : vy - 9.81f * Tune.WalkDt;
                        cc.Move(step + Vector3.up * (vy * Tune.WalkDt));
                        Physics.SyncTransforms();
                        var now = go.transform.position;
                        res.Walked += HDist(now, pos);
                        if ((now - last).magnitude >= 0.5f) { res.Trace.Add(now); last = now; }
                        if (now.y < lowY - Tune.WalkFallDrop)
                        {
                            res.Snags.Add(new Snag { At = now, Node = m.NodeName(m.NodeOf(now)), What = "fell " + (lowY - now.y).ToString("0.0") + " m below the path", Corner = i });
                            break;
                        }
                        float nd = HDist(now, target);
                        if (nd < best - Tune.WalkStuckProgress) { best = nd; stuck = 0; }
                        else if (++stuck > stuckLimit)
                        {
                            // the E contract: a ray from eye height toward this corner, then the next
                            MonoBehaviour rec = null; RaycastHit hit = default; string what = null;
                            var eye = now + Vector3.up * Tune.EyeHeight;
                            foreach (var aim in new[] { target, i + 1 < res.Corners.Count ? res.Corners[i + 1] : target })
                            {
                                rec = Doors.Cast(eye, aim - now, out hit, out what);
                                if (rec != null && !openedRec.Contains(rec)) break;
                                rec = null;
                            }
                            // ... then at the nearest unopened door in reach: an oblique approach slides the walker
                            // along a shut leaf and past it before it stalls (Hollowmere Manor's shrine door, 2026-10-14)
                            // (the walker then lines up again from the previous corner)
                            if (rec == null)
                                foreach (var aim in Doors.NearReceivers(eye, openedRec))
                                {
                                    var r2 = Doors.Cast(eye, aim - eye, out var h2, out var w2);
                                    if (r2 == null || openedRec.Contains(r2)) continue;
                                    rec = r2; hit = h2; what = w2; back = true;
                                    break;
                                }
                            if (rec != null)
                            {
                                hit.collider.gameObject.SendMessageUpwards("Interact", SendMessageOptions.DontRequireReceiver);
                                foreach (var c in Doors.CollidersOf(rec, hit.collider)) { c.enabled = false; opened.Add(c); }
                                openedRec.Add(rec);
                                Physics.SyncTransforms();
                                res.Opened.Add(Doors.PathOf(rec.transform) + " (" + m.NodeName(m.NodeOf(now)) + ")");
                                stuck = 0;
                                if (back) break;
                                continue;
                            }
                            res.Snags.Add(new Snag { At = now, Node = m.NodeName(m.NodeOf(now)), What = "blocked, " + nd.ToString("0.0") + " m short of corner " + i + " (" + what + ")", Corner = i });
                            break;
                        }
                    }
                    if (res.Snags.Count > 0) break;
                    if (back) { i = Mathf.Max(0, i - 2); continue; }
                    if (HDist(go.transform.position, target) > Tune.WalkReach + 0.05f)
                    {
                        var now = go.transform.position;
                        res.Snags.Add(new Snag { At = now, Node = m.NodeName(m.NodeOf(now)), What = "ran out of time short of corner " + i, Corner = i });
                        break;
                    }
                }
                var end = go.transform.position;
                res.Trace.Add(end);
                var goal = res.Corners[res.Corners.Count - 1];
                if (res.Snags.Count == 0 && Mathf.Abs(end.y - goal.y) > Tune.WalkEndTolerance)
                    res.Snags.Add(new Snag { At = end, Node = m.NodeName(m.NodeOf(end)), What = "arrived " + (end.y - goal.y).ToString("0.0") + " m off the goal's height", Corner = res.Corners.Count - 1 });
                res.Pass = res.Snags.Count == 0;
                res.Note = res.Pass ? "reached the end" + (res.Opened.Count > 0 ? "; opened by E: " + string.Join(", ", res.Opened) : "")
                                    : res.Snags[0].What + " at " + V(res.Snags[0].At) + " in " + res.Snags[0].Node;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                foreach (var c in opened) if (c != null) c.enabled = true;
                Physics.SyncTransforms();
            }
            return res;
        }

        static float HDist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public static string V(Vector3 p) => "(" + p.x.ToString("0.#") + ", " + p.y.ToString("0.#") + ", " + p.z.ToString("0.#") + ")";
    }
}
