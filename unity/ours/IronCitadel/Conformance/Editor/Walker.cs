// IronCitadel conformance kit: walks a temporary CharacterController along NavMesh paths in edit mode.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace IronCitadel.Conformance
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

        public static WalkResult Missing(string route, string why) =>
            new WalkResult { Route = route, Pass = false, Note = "not walked: " + why };

        public JObj ToJson()
        {
            return new JObj().Add("route", Route).Add("pass", Pass).Add("note", Note)
                .Add("planned_m", Planned).Add("walked_m", Walked).Add("corners", Corners.Count)
                .Add("snags", Snags.Select(s => (object)new JObj().Add("at", s.At).Add("room", s.Node)
                    .Add("what", s.What).Add("corner", s.Corner)).ToList());
        }
    }

    public static class Walker
    {
        /// <summary>Plans a NavMesh path through the waypoints, then drives a CharacterController
        /// (the Tune agent: r 0.5, h 2, step 0.25) along its corners with CharacterController.Move.</summary>
        public static WalkResult Walk(NavModel m, string route, List<Vector3?> waypoints)
        {
            var res = new WalkResult { Route = route };
            for (int i = 0; i < waypoints.Count; i++)
                if (waypoints[i] == null) { res.Note = "not walked: waypoint " + i + " has no NavMesh point"; return res; }

            // plan
            var path = new NavMeshPath();
            for (int i = 0; i + 1 < waypoints.Count; i++)
            {
                var a = waypoints[i].Value; var b = waypoints[i + 1].Value;
                if (NavMesh.SamplePosition(a, out var ha, 2f, m.Filter)) a = ha.position;
                if (NavMesh.SamplePosition(b, out var hb, 2f, m.Filter)) b = hb.position;
                if (!NavMesh.CalculatePath(a, b, m.Filter, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    res.Note = "no complete NavMesh path for leg " + (i + 1) + " (" + path.status + ") from " + V(a) + " to " + V(b);
                    return res;
                }
                var c = path.corners;
                for (int k = res.Corners.Count == 0 ? 0 : 1; k < c.Length; k++) res.Corners.Add(c[k]);
            }
            for (int i = 1; i < res.Corners.Count; i++) res.Planned += Vector3.Distance(res.Corners[i - 1], res.Corners[i]);

            // walk
            var go = new GameObject("__ConformanceWalker") { hideFlags = HideFlags.HideAndDontSave };
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
                    int maxIter = Mathf.CeilToInt(best / (Tune.WalkSpeed * Tune.WalkDt)) * 4 + 200;
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
                            res.Snags.Add(new Snag { At = now, Node = m.NodeName(m.NodeOf(now)), What = "blocked, " + nd.ToString("0.0") + " m short of corner " + i, Corner = i });
                            break;
                        }
                    }
                    if (res.Snags.Count > 0) break;
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
                res.Note = res.Pass ? "reached the end" : res.Snags[0].What + " at " + V(res.Snags[0].At) + " in " + res.Snags[0].Node;
            }
            finally
            {
                Object.DestroyImmediate(go);
                Physics.SyncTransforms();
            }
            return res;
        }

        static float HDist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static string V(Vector3 p) => "(" + p.x.ToString("0.#") + ", " + p.y.ToString("0.#") + ", " + p.z.ToString("0.#") + ")";
    }
}
