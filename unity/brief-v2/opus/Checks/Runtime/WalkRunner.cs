using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StarterAssets;
using UnityEngine;
using UnityEngine.AI;
using IronCitadel.Interact;

namespace IronCitadel.Checks
{
    /// <summary>
    /// Play-mode walks through the level, driving the shipped player prefab only through its own controls:
    /// move and sprint input on StarterAssetsInputs, the E-key script's Interact(), and turning the player to
    /// face where it is going. It never moves the player's transform, never jumps, and has no unstick or
    /// retry logic: a stall ends the walk as failed. Routes are planned on the baked NavMesh between
    /// waypoints read from the scene (door openings, markers, the throne).
    /// All walks run in one continuous session from the start position, in order; short connecting walks
    /// (also driven the same way) bring the player from the end of one walk to the start of the next.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class WalkRunner : MonoBehaviour
    {
        public static WalkRunner Instance;
        public bool Done;
        public readonly List<string> Lines = new List<string>();
        public int Passed, Failed;

        Transform _player;
        StarterAssetsInputs _input;
        InteractOnE _interact;
        CharacterController _cc;
        LevelHUD _hud;
        float _yaw;
        bool _steer;

        // per-walk record
        string _walk;
        bool _walkOk;
        string _walkWhy;
        float _walkStart, _walkDist;
        Vector3 _lastPos;
        readonly List<string> _rooms = new List<string>();
        readonly List<Vector3> _trace = new List<Vector3>();
        bool _recording;

        const float StallWindow = 1.5f, StallDist = 0.25f;

        void Awake() { Instance = this; }

        IEnumerator Start()
        {
            _cc = FindAnyObjectByType<CharacterController>();
            _player = _cc.transform;
            _input = _player.GetComponent<StarterAssetsInputs>();
            _interact = _player.GetComponent<InteractOnE>();
            _hud = FindAnyObjectByType<LevelHUD>();
            _input.cursorLocked = false;
            _yaw = _player.eulerAngles.y;
            Lines.Add($"player: {_player.name} CharacterController r={_cc.radius:F2} h={_cc.height:F2} step={_cc.stepOffset:F2} slope={_cc.slopeLimit:F0}; start {_player.position.ToString("F2")}");
            yield return new WaitForSeconds(1.0f);
            yield return RunAll();
            _input.MoveInput(Vector2.zero); _input.SprintInput(false);
            Lines.Add($"walks: {Passed} passed, {Failed} failed");
            Done = true;
        }

        void Update()
        {
            if (_steer && _player != null) _player.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_recording && _player != null)
            {
                var p = _player.position;
                _walkDist += Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(_lastPos.x, 0, _lastPos.z));
                _lastPos = p;
                _trace.Add(p);
                var room = Room.At(p);
                string n = room != null ? room.roomName : "(between rooms)";
                if (_rooms.Count == 0 || _rooms[_rooms.Count - 1] != n) _rooms.Add(n);
            }
        }

        // ------------------------------------------------------------ the walks
        IEnumerator RunAll()
        {
            var eS = Door("Entry Hall", "Storeroom"); var sK = Door("Storeroom", "Kitchen"); var kX = Door("Kitchen", "Servery Stair");
            var xD = Door("Servery Stair", "Great Dining Hall", false); var xG = Door("Servery Stair", "Great Dining Hall", true);
            var aD = Door("Armory", "Great Dining Hall"); var dL = Door("Great Dining Hall", "Library"); var lV = Door("Library", "Treasure Vault");
            var lH = Door("Library", "Shrine"); var hT = Door("Shrine", "Throne Room");
            var lAS = Door("Library", "Alchemist's Study"); var pTC = Door("Prison", "Torture Chamber");
            var eA = Door("Entry Hall", "Armory"); var eG = Door("Entry Hall", "Guard Room"); var gP = Door("Guard Room", "Prison");
            var hall = Room.Named("Great Dining Hall"); var armory = Room.Named("Armory");
            var gear = FindObjectsByType<Marker>().First(m => m.kind == "best gear");
            var hoard = FindObjectsByType<Marker>().First(m => m.kind == "hoard");

            // W1 ----------------------------------------------------------------
            Begin("W1 the way round: start (Entry Hall) to the Great Dining Hall without passing through the Armory");
            yield return Through(eS, true); if (!_walkOk) goto end1;
            yield return Through(sK, true); if (!_walkOk) goto end1;
            yield return Through(kX, true); if (!_walkOk) goto end1;
            yield return Through(xD, true); if (!_walkOk) goto end1;
            yield return Go(xD.transform.position + xD.transform.forward * 2.5f, true);
            end1: ;
            Require(Room.At(_player.position) == hall, "ends in the Great Dining Hall");
            Require(!_trace.Any(p => armory.Contains(p)), "no point of the walk inside the Armory");
            End();

            // connector back into the Servery Stair, where the way round comes into the hall
            Begin("connector: step back into the Servery Stair", true);
            yield return Go(xD.transform.position - xD.transform.forward * 1.4f, false);
            End();

            // W2 ----------------------------------------------------------------
            Begin("W2 up to the gallery from where the way round comes into the hall (Servery Stair), not crossing the hall floor");
            yield return Through(xG, false); if (!_walkOk) goto end2;
            yield return Go(new Vector3(3.0f, xG.transform.position.y, 21.8f), false);
            end2: ;
            Require(_player.position.y > hall.floorY + 4f && Room.At(_player.position) == hall, $"ends on the gallery (y={_player.position.y:F2})");
            Require(!_trace.Any(p => hall.Contains(p) && p.y < hall.floorY + 1.0f), "no point of the walk on the hall floor");
            Require(_trace.Max(p => p.y) - _trace.Min(p => p.y) > 4.5f, $"climbed {(_trace.Max(p => p.y) - _trace.Min(p => p.y)):F2} m by the stair");
            End();

            Begin("connector: back down the servery stair to the hall floor", true);
            yield return Through(xG, false);
            yield return Through(xD, false);
            yield return Go(xD.transform.position + xD.transform.forward * 2.0f, false);
            End();

            // W3 ----------------------------------------------------------------
            Begin("W3 from the dining hall into the Armory beneath the gallery, to its best gear");
            yield return Through(aD, false); if (!_walkOk) goto end3;
            yield return Go(gear.transform.position + new Vector3(0, 0, 0.7f), false);
            end3: ;
            float dGear = Flat(_player.position - gear.transform.position);
            Require(dGear < 1.2f, $"ends {dGear:F2} m from the best gear");
            Require(Room.At(_player.position) == armory, "ends in the Armory");
            End();

            Begin("connector: back out to the hall and on to the Library", true);
            yield return Through(aD, true);
            yield return Through(dL, true);
            End();

            // W4 ----------------------------------------------------------------
            Begin("W4 the hidden way into the Treasure Vault and back");
            yield return Through(lV, false, 2.4f); if (!_walkOk) goto end4;
            yield return Go(hoard.transform.position + new Vector3(-0.2f, 0, -0.7f), false); if (!_walkOk) goto end4;
            Require(Room.At(_player.position) == Room.Named("Treasure Vault") && Flat(_player.position - hoard.transform.position) < 1.2f, "reached the hoard inside the vault");
            yield return Through(lV, false, 1.3f, 2.0f);
            end4: ;
            Require(Room.At(_player.position) == Room.Named("Library"), "back in the Library");
            End();

            // W6 (extra) ---------------------------------------------------------
            Begin("W6 (extra) the shut door off the Library: into the Alchemist's Study and back");
            yield return Through(lAS, false); if (!_walkOk) goto end6;
            Require(Room.At(_player.position) == Room.Named("Alchemist's Study"), "inside the Alchemist's Study");
            yield return Through(lAS, false);
            end6: ;
            Require(Room.At(_player.position) == Room.Named("Library"), "back in the Library");
            End();

            Begin("connector: back to the hall", true);
            yield return Through(dL, true);
            End();

            // W5 ----------------------------------------------------------------
            Begin("W5 from the dining hall on to the throne");
            yield return Through(dL, true); if (!_walkOk) goto end5;
            yield return Through(lH, true); if (!_walkOk) goto end5;
            yield return Through(hT, true); if (!_walkOk) goto end5;
            var throne = _hud.throne;
            // in front of the throne, on the stage: 1.6 m from it towards the hall's entrance
            var toHall = hT.transform.position - throne.position; toHall.y = 0;
            yield return Go(throne.position + toHall.normalized * 1.6f + Vector3.up * 0.1f, false);
            yield return new WaitForSeconds(0.5f);
            end5: ;
            Require(_hud.ThroneReached, $"the HUD announced the throne (at {_hud.ThroneReachedAt:F1} s)");
            Require(_player.position.y > 1.2f, $"standing on the stage (y={_player.position.y:F2})");
            End();

            // W7 (extra) ---------------------------------------------------------
            Begin("W7 (extra) back from the throne by the obvious way (through the Armory) to the gaol, opening the Torture Chamber's shut door");
            yield return Through(hT, true); if (!_walkOk) goto end7;
            yield return Through(lH, true); if (!_walkOk) goto end7;
            yield return Through(dL, true); if (!_walkOk) goto end7;
            yield return Through(aD, true); if (!_walkOk) goto end7;
            yield return Through(eA, true); if (!_walkOk) goto end7;
            yield return Through(eG, true); if (!_walkOk) goto end7;
            yield return Through(gP, true); if (!_walkOk) goto end7;
            yield return Through(pTC, false);
            end7: ;
            Require(Room.At(_player.position) == Room.Named("Torture Chamber"), "ends in the Torture Chamber");
            End();
        }

        // ------------------------------------------------------------ steps
        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        DoorLink Door(string a, string b, bool? upper = null)
        {
            foreach (var d in FindObjectsByType<DoorLink>())
                if ((d.roomA == a && d.roomB == b) || (d.roomA == b && d.roomB == a))
                    if (upper == null || (d.transform.position.y > 2f) == upper.Value) return d;
            throw new Exception($"no door between {a} and {b}");
        }

        /// Walk up to an opening, open it with E if it is shut, and walk through.
        IEnumerator Through(DoorLink d, bool sprint, float near = 1.3f, float far = 1.4f)
        {
            var c = d.transform.position; var n = d.transform.forward;
            float side = Mathf.Sign(Vector3.Dot(_player.position - c, n));
            if (side == 0) side = 1;
            Vector3 a = c + n * side * near, b = c - n * side * far;
            yield return Go(a, sprint);
            if (!_walkOk) yield break;
            bool needsOpen = false; Func<bool> isOpen = () => true;
            if (d.leaf != null)
            {
                var sw = d.leaf.GetComponentInChildren<SwingDoor>() ?? d.leaf.GetComponentInParent<SwingDoor>();
                var sb = d.leaf.GetComponent<SecretBookcase>();
                if (sw != null) { needsOpen = !sw.isOpen; isOpen = () => sw.isOpen; }
                if (sb != null) { needsOpen = !sb.isOpen; isOpen = () => sb.isOpen; }
            }
            if (needsOpen)
            {
                Face(c);
                yield return null; yield return null;
                _interact.Interact();   // the E key's own ray from the camera
                yield return new WaitForSeconds(d.state == "hidden" ? 2.0f : 1.0f);
                if (!isOpen()) { Fail($"pressed E at {d.name} from {_player.position.ToString("F2")} but it did not open"); yield break; }
                Lines.Add($"    opened {d.name} with E from {Flat(_player.position - c):F2} m");
            }
            yield return Go(b, sprint);
        }

        void Face(Vector3 p) { var d = p - _player.position; d.y = 0; if (d.sqrMagnitude > 1e-4f) _yaw = Quaternion.LookRotation(d).eulerAngles.y; _steer = true; }

        /// Walk (or sprint) to a point along a NavMesh route, steering by turning only.
        IEnumerator Go(Vector3 target, bool sprint)
        {
            if (!_walkOk) yield break;
            if (!NavMesh.SamplePosition(_player.position, out var from, 1.5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(target, out var to, 1.5f, NavMesh.AllAreas))
            { Fail($"no NavMesh near {_player.position.ToString("F2")} or {target.ToString("F2")}"); yield break; }
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            { Fail($"no route from {from.position.ToString("F2")} to {to.position.ToString("F2")}"); yield break; }
            var corners = path.corners;
            float length = 0; for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            float timeout = Time.time + 8f + length / 1.5f;
            int k = 1;
            float progressT = Time.time; Vector3 progressP = _player.position;
            _steer = true;
            while (k < corners.Length)
            {
                var p = _player.position;
                var to2 = corners[k] - p; to2.y = 0;
                bool last = k == corners.Length - 1;
                if (to2.magnitude < (last ? 0.3f : 0.4f)) { k++; continue; }
                _yaw = Quaternion.LookRotation(to2).eulerAngles.y;
                _input.SprintInput(sprint && !last || sprint && to2.magnitude > 2f);
                _input.MoveInput(new Vector2(0, 1));
                yield return null;
                if (Flat(_player.position - progressP) > StallDist) { progressP = _player.position; progressT = Time.time; }
                else if (Time.time - progressT > StallWindow)
                {
                    _input.MoveInput(Vector2.zero);
                    Fail($"stalled at {_player.position.ToString("F2")} heading for {corners[k].ToString("F2")} (no {StallDist} m of progress in {StallWindow} s)");
                    yield break;
                }
                if (Time.time > timeout) { _input.MoveInput(Vector2.zero); Fail($"timed out at {_player.position.ToString("F2")}"); yield break; }
            }
            _input.MoveInput(Vector2.zero); _input.SprintInput(false);
            // let it come to rest
            for (int i = 0; i < 10; i++) yield return null;
            float miss = Flat(_player.position - to.position);
            if (miss > 0.8f) Fail($"stopped {miss:F2} m short of {to.position.ToString("F2")}");
        }

        // ------------------------------------------------------------ bookkeeping
        bool _connector;
        void Begin(string name, bool connector = false)
        {
            _walk = name; _walkOk = true; _walkWhy = ""; _walkStart = Time.time; _walkDist = 0; _lastPos = _player.position;
            _rooms.Clear(); _trace.Clear(); _recording = true; _connector = connector;
        }

        void Fail(string why) { if (_walkOk) { _walkOk = false; _walkWhy = why; } }

        void Require(bool ok, string what)
        {
            Lines.Add($"    {(ok ? "ok  " : "FAIL")} {what}");
            if (!ok) Fail(what);
        }

        void End()
        {
            _recording = false;
            _input.MoveInput(Vector2.zero); _input.SprintInput(false);
            string verdict = _walkOk ? "PASS" : "FAIL";
            if (!_connector) { if (_walkOk) Passed++; else Failed++; }
            Lines.Add($"{(_connector ? "  (connector) " : "")}{verdict} {_walk}: {Time.time - _walkStart:F1} s, {_walkDist:F1} m, ends at {_player.position.ToString("F2")}" + (_walkOk ? "" : " — " + _walkWhy));
            Lines.Add("    rooms: " + string.Join(" > ", _rooms));
        }
    }
}
