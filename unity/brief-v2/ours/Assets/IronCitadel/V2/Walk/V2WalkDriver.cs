// Iron Citadel v2: walks the player prefab, as it is, through a scripted route in play mode.
// It steers with the look input, walks with the move input and presses E (InteractOnE.Interact) at
// shut and hidden doors, as a player would. It never jumps, teleports or moves the player's transform.
//
// Script lines (from make_walks.py):
//   leg <name>               start a leg
//   go <x> <y> <z> <tag>     walk to the point along a NavMesh path
//   door <x> <y> <z> <id>    turn to face the point and press E
//   wait <seconds>
//   end                      close the leg
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using IronCitadel.Interact;
using StarterAssets;
using UnityEngine;
using UnityEngine.AI;

namespace IronCitadel.V2.Walk
{
    public sealed class V2WalkDriver : MonoBehaviour
    {
        public static V2WalkDriver Instance;
        public bool Done;
        public string Status = "idle";

        sealed class Leg
        {
            public string Name, Note = "";
            public bool Pass = true;
            public float Planned, Walked, T0, T1;
            public readonly List<string> Events = new List<string>();
            public readonly List<Vector4> Trace = new List<Vector4>();
        }

        string _script, _out;
        StarterAssetsInputs _input;
        FirstPersonController _fpc;
        InteractOnE _interact;
        float _k = 1f, _lastLookX, _lastYaw, _lastRecord;
        bool _haveLast;
        Vector3 _lastPos;
        Leg _leg;
        readonly List<Leg> _legs = new List<Leg>();

        /// <summary>Called from an eval in play mode. Returns at once; poll <see cref="Status"/> or the out file.</summary>
        public static string Begin(string scriptPath, string outPath)
        {
            var fpc = Object.FindFirstObjectByType<FirstPersonController>();
            if (fpc == null) return "no FirstPersonController in the scene";
            var d = fpc.gameObject.GetComponent<V2WalkDriver>();
            if (d == null) d = fpc.gameObject.AddComponent<V2WalkDriver>();
            d._script = scriptPath; d._out = outPath; d.Done = false; d._legs.Clear(); d._leg = null;
            Instance = d;
            d.StartCoroutine(d.Run());
            return "started";
        }

        IEnumerator Run()
        {
            Status = "running";
            _input = GetComponent<StarterAssetsInputs>();
            _fpc = GetComponent<FirstPersonController>();
            _interact = GetComponentInChildren<InteractOnE>();
            if (_interact == null) _interact = Object.FindFirstObjectByType<InteractOnE>();
            if (_input == null || _interact == null)
            {
                Status = "error: StarterAssetsInputs or InteractOnE missing"; Done = true; yield break;
            }
            _k = Mathf.Max(0.01f, _fpc.RotationSpeed);
            _lastPos = transform.position;
            yield return new WaitForSeconds(0.5f);

            string[] lines;
            try { lines = File.ReadAllLines(_script); }
            catch (System.Exception e) { Status = "error: " + e.Message; Done = true; yield break; }

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var t = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                switch (t[0])
                {
                    case "leg": BeginLeg(t.Length > 1 ? t[1] : "leg"); break;
                    case "go": if (_leg != null && _leg.Pass) yield return GoTo(V(t), t.Length > 4 ? t[4] : ""); break;
                    case "door": if (_leg != null && _leg.Pass) yield return PressE(V(t), t.Length > 4 ? t[4] : ""); break;
                    case "wait": Idle(); yield return new WaitForSeconds(F(t[1])); break;
                    case "carve": Carve(t.Length > 1 ? t[1] : "Enemies"); yield return new WaitForSeconds(0.5f); break;
                    case "end": EndLeg(); break;
                }
            }
            EndLeg();
            Idle();
            Status = "done";
            Done = true;
            Write();
        }

        /// <summary>Plans around the standing enemies: a carving obstacle on every solid capsule under the named
        /// root. Play mode only, so the scene is unchanged.</summary>
        void Carve(string root)
        {
            var go = GameObject.Find(root);
            if (go == null) { Status = "carve: no " + root; return; }
            int n = 0;
            foreach (var cap in go.GetComponentsInChildren<CapsuleCollider>())
            {
                if (cap.isTrigger || cap.GetComponent<NavMeshObstacle>() != null) continue;
                var o = cap.gameObject.AddComponent<NavMeshObstacle>();
                o.shape = NavMeshObstacleShape.Capsule;
                o.center = cap.center;
                o.radius = cap.radius + 0.1f;
                o.height = cap.height;
                o.carving = true;
                o.carveOnlyStationary = false;
                n++;
            }
            Status = "carved " + n;
        }

        void BeginLeg(string name)
        {
            EndLeg();
            _leg = new Leg { Name = name, T0 = Time.time };
            _lastPos = transform.position;
            Status = "leg " + name;
        }

        void EndLeg()
        {
            if (_leg == null) return;
            _leg.T1 = Time.time;
            _legs.Add(_leg);
            _leg = null;
            Write();
        }

        void Fail(string why)
        {
            Idle();
            if (_leg == null) return;
            _leg.Pass = false;
            _leg.Note = why;
            _leg.Events.Add("FAIL " + why);
        }

        void Idle()
        {
            if (_input == null) return;
            _input.move = Vector2.zero;
            _input.look = Vector2.zero;
            _haveLast = false;
        }

        static bool Plan(Vector3 from, Vector3 to, NavMeshPath path)
        {
            if (NavMesh.SamplePosition(from, out var a, 1.5f, NavMesh.AllAreas)) from = a.position;
            if (NavMesh.SamplePosition(to, out var b, 1.5f, NavMesh.AllAreas)) to = b.position;
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        IEnumerator GoTo(Vector3 target, string tag)
        {
            var path = new NavMeshPath();
            int replans = 0;
            float t0 = Time.time;
            Vector3 lastPos = transform.position;
            float lastT = Time.time;
            while (true)
            {
                if (!Plan(transform.position, target, path))
                {
                    Fail("no NavMesh path to " + tag + " " + Fmt(target) + " (" + path.status + ") from " + Fmt(transform.position));
                    yield break;
                }
                var c = path.corners;
                if (replans == 0) _leg.Planned += Length(c);
                bool replan = false;
                int i = 1;
                while (i < c.Length)
                {
                    Vector3 p = transform.position;
                    Vector3 d = c[i] - p; d.y = 0f;
                    bool last = i == c.Length - 1;
                    if (d.magnitude < (last ? 0.6f : 0.45f)) { i++; continue; }
                    float err = Mathf.DeltaAngle(transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                    Steer(err);
                    float a = Mathf.Abs(err);
                    _input.move = new Vector2(0f, a < 20f ? 1f : a < 50f ? 0.35f : 0f);
                    Record();
                    if ((p - lastPos).sqrMagnitude > 0.25f) { lastPos = p; lastT = Time.time; }
                    else if (Time.time - lastT > 3f)
                    {
                        _leg.Events.Add("snag at " + Fmt(p) + " going to " + tag);
                        if (++replans > 5) { Fail("stuck at " + Fmt(p) + " going to " + tag); yield break; }
                        _input.look = Vector2.zero; _haveLast = false;
                        _input.move = new Vector2(replans % 2 == 0 ? 1f : -1f, -0.6f);
                        yield return new WaitForSeconds(0.6f);
                        lastPos = transform.position; lastT = Time.time; replan = true;
                        break;
                    }
                    if (Time.time - t0 > 300f) { Fail("timed out going to " + tag); yield break; }
                    yield return null;
                }
                if (!replan) break;
            }
            Idle();
            _leg.Events.Add("at " + tag + " " + Fmt(transform.position));
        }

        IEnumerator PressE(Vector3 point, string id)
        {
            Idle();
            float t0 = Time.time;
            while (Time.time - t0 < 4f)
            {
                Vector3 d = point - transform.position; d.y = 0f;
                if (d.sqrMagnitude < 0.0001f) break;
                float err = Mathf.DeltaAngle(transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                if (Mathf.Abs(err) < 2f) break;
                Steer(err);
                _input.move = Vector2.zero;
                Record();
                yield return null;
            }
            Idle();
            yield return null;
            yield return null;
            var cam = Camera.main != null ? Camera.main.transform : transform;
            string hitName = Physics.Raycast(cam.position, cam.forward, out var hit, _interact.interactRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.collider.name + " at " + hit.distance.ToString("0.00", CultureInfo.InvariantCulture) + " m"
                : "nothing within " + _interact.interactRange + " m";
            _interact.Interact();
            _leg.Events.Add("E at " + id + ": the ray hit " + hitName);
            yield return new WaitForSeconds(2f);
        }

        void Steer(float err)
        {
            float yaw = transform.eulerAngles.y;
            if (_haveLast && Mathf.Abs(_lastLookX) > 0.05f)
            {
                float k = Mathf.DeltaAngle(_lastYaw, yaw) / _lastLookX;
                if (k > 0.001f && k < 1000f) _k = Mathf.Lerp(_k, k, 0.5f);
            }
            float max = 240f * Mathf.Max(Time.deltaTime, 0.001f);
            float turn = Mathf.Clamp(err, -max, max);
            float lx = turn / Mathf.Max(_k, 0.001f);
            _input.look = new Vector2(lx, 0f);
            _lastLookX = lx; _lastYaw = yaw; _haveLast = true;
        }

        void Record()
        {
            if (_leg == null) return;
            Vector3 p = transform.position;
            _leg.Walked += Vector3.Distance(p, _lastPos);
            _lastPos = p;
            if (Time.time - _lastRecord < 0.2f) return;
            _lastRecord = Time.time;
            _leg.Trace.Add(new Vector4(p.x, p.y, p.z, Time.time));
        }

        void Write()
        {
            if (string.IsNullOrEmpty(_out)) return;
            var sb = new StringBuilder();
            sb.Append("{\n  \"status\": \"").Append(Esc(Status)).Append("\",\n  \"legs\": [");
            for (int i = 0; i < _legs.Count; i++)
            {
                var l = _legs[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    {\"name\": \"").Append(Esc(l.Name)).Append("\", \"pass\": ").Append(l.Pass ? "true" : "false")
                  .Append(", \"note\": \"").Append(Esc(l.Note)).Append("\", \"planned_m\": ").Append(N(l.Planned))
                  .Append(", \"walked_m\": ").Append(N(l.Walked)).Append(", \"seconds\": ").Append(N(l.T1 - l.T0))
                  .Append(",\n     \"events\": [");
                for (int e = 0; e < l.Events.Count; e++)
                    sb.Append(e == 0 ? "" : ", ").Append('"').Append(Esc(l.Events[e])).Append('"');
                sb.Append("],\n     \"trace\": [");
                for (int k = 0; k < l.Trace.Count; k++)
                {
                    var v = l.Trace[k];
                    sb.Append(k == 0 ? "" : ",").Append('[').Append(N(v.x)).Append(',').Append(N(v.y)).Append(',')
                      .Append(N(v.z)).Append(',').Append(N(v.w)).Append(']');
                }
                sb.Append("]}");
            }
            sb.Append("\n  ]\n}\n");
            File.WriteAllText(_out, sb.ToString());
        }

        static Vector3 V(string[] t) => new Vector3(F(t[1]), F(t[2]), F(t[3]));
        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static string N(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);
        static string Fmt(Vector3 v) => "(" + N(v.x) + ", " + N(v.y) + ", " + N(v.z) + ")";
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        static float Length(Vector3[] c)
        {
            float s = 0f;
            for (int i = 1; i < c.Length; i++) s += Vector3.Distance(c[i - 1], c[i]);
            return s;
        }
    }
}
