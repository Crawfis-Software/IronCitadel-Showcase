using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using StarterAssets;
using IronCitadel.Interact;

namespace IronCitadel
{
    /// <summary>
    /// Drives the shipped player through its own inputs (move, sprint) along a list of walks, each a list of waypoints.
    /// The only things it touches on the player are StarterAssetsInputs.move/sprint, the capsule's yaw (to face where it goes),
    /// and InteractOnE.Interact() at waypoints marked as interactions. A walk fails when the player stalls or runs out of time.
    /// Used by the editor check runner; not part of the game.
    /// </summary>
    public class WalkDriver : MonoBehaviour
    {
        public class Step
        {
            public Vector3 target;
            public bool sprint = true;
            public string interact;      // name of an object to face and use (E) once the target is reached
            public float reach = 0.45f;  // how close counts as reached (horizontal)
            public string note;
        }
        public class Walk
        {
            public string name;
            public List<Step> steps = new List<Step>();
            public Func<Vector3, string> violation; // returns a message when the position breaks a rule
            public string mustEndIn;                // room name the walk must end in
            public float timeBudget = 240f;
            public Action<WalkDriver> onDone;
        }

        public List<Walk> walks = new List<Walk>();
        public Action<string, bool> onAllDone; // (report text, all passed)
        public StringBuilder report = new StringBuilder();
        public bool allPassed = true;
        public List<string> roomTrail = new List<string>();

        StarterAssetsInputs _input; InteractOnE _interact; CharacterController _cc;
        int _walk = -1, _step; float _walkStart, _bestDist, _lastProgress; bool _busy, _finished;
        string _violation; float _wait; int _phase; bool _walkFailed;
        float _speedSum; int _speedN; float _maxY, _minY;

        void Start()
        {
            _input = GetComponent<StarterAssetsInputs>();
            _interact = GetComponent<InteractOnE>();
            _cc = GetComponent<CharacterController>();
            _input.cursorInputForLook = false; // keep the editor mouse from turning the player during the run
            Application.runInBackground = true; // keep ticking while the editor is unfocused
            _wait = 1.0f; // let physics settle
        }

        void BeginWalk(int i)
        {
            _walk = i; _step = 0; _walkStart = Time.time; _walkFailed = false; _violation = null; roomTrail.Clear();
            _bestDist = float.MaxValue; _lastProgress = Time.time; _phase = 0; _speedSum = 0; _speedN = 0; _maxY = -999; _minY = 999;
            var w = walks[i];
            report.AppendLine("WALK " + (i + 1) + ": " + w.name);
            report.AppendLine("  start at " + F(transform.position) + " in '" + RoomName() + "' at t=" + Time.timeSinceLevelLoad.ToString("F1") + " s");
        }

        string RoomName() { var r = IronRoom.RoomAt(transform.position); return r == null ? "(none)" : r.roomName; }
        static string F(Vector3 v) => string.Format("({0:F2}, {1:F2}, {2:F2})", v.x, v.y, v.z);

        void Update()
        {
            if (_finished) return;
            if (_wait > 0f) { _wait -= Time.deltaTime; _input.move = Vector2.zero; return; }
            if (_walk < 0) { if (walks.Count == 0) { Finish(); return; } BeginWalk(0); }
            var w = walks[_walk];
            if (_step >= w.steps.Count) { EndWalk(true); return; }

            // trail and rule checks
            string rn = RoomName();
            if (roomTrail.Count == 0 || roomTrail[roomTrail.Count - 1] != rn) roomTrail.Add(rn);
            if (w.violation != null && _violation == null) { var v = w.violation(transform.position); if (v != null) { _violation = v + " at " + F(transform.position); } }
            _maxY = Mathf.Max(_maxY, transform.position.y); _minY = Mathf.Min(_minY, transform.position.y);

            var s = w.steps[_step];
            var to = s.target - transform.position; to.y = 0f;
            float dist = to.magnitude;

            if (_phase == 0)
            {
                if (dist <= s.reach)
                {
                    _input.move = Vector2.zero;
                    if (!string.IsNullOrEmpty(s.interact)) { _phase = 1; _wait = 0.25f; return; }
                    NextStep(); return;
                }
                // face the target and push forward
                transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f);
                _input.move = new Vector2(0f, 1f);
                _input.sprint = s.sprint;
                var hv = _cc.velocity; hv.y = 0; _speedSum += hv.magnitude; _speedN++;
                if (dist < _bestDist - 0.05f) { _bestDist = dist; _lastProgress = Time.time; }
                if (Time.time - _lastProgress > 3.0f)
                {
                    report.AppendLine("  STALL: no progress for 3 s toward " + F(s.target) + " at " + F(transform.position) + (s.note != null ? " (" + s.note + ")" : ""));
                    EndWalk(false); return;
                }
                if (Time.time - _walkStart > w.timeBudget) { report.AppendLine("  TIMEOUT after " + w.timeBudget + " s at " + F(transform.position)); EndWalk(false); return; }
            }
            else if (_phase == 1)
            {
                // face the thing to use; the camera follows the capsule's yaw a frame later (Cinemachine), so E is pressed in the next phase
                var target = GameObject.Find(s.interact);
                if (target == null) { report.AppendLine("  FAIL: nothing named '" + s.interact + "' to use"); EndWalk(false); return; }
                var d = target.transform.position - transform.position; d.y = 0f;
                transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                _phase = 5; _wait = 0.3f; return;
            }
            else if (_phase == 5)
            {
                var target = GameObject.Find(s.interact);
                var d = target.transform.position - transform.position; d.y = 0f;
                _interact.Interact();
                report.AppendLine("  used E on '" + s.interact + "' from " + F(transform.position) + " (distance " + d.magnitude.ToString("F2") + " m)");
                _phase = 2; _wait = 2.3f; return;
            }
            else if (_phase == 2)
            {
                var target = GameObject.Find(s.interact);
                var door = target != null ? target.GetComponentInChildren<DoorInteract>() : null;
                var secret = target != null ? target.GetComponentInChildren<SecretDoor>() : null;
                bool ok = (door != null && door.open) || (secret != null && secret.open);
                if (door == null && secret == null) ok = false;
                report.AppendLine("  " + (ok ? "opened" : "FAIL: did not open") + " '" + s.interact + "'");
                if (!ok) { EndWalk(false); return; }
                NextStep();
            }
        }

        void NextStep()
        {
            _step++; _phase = 0; _bestDist = float.MaxValue; _lastProgress = Time.time;
            var w = walks[_walk];
            if (_step < w.steps.Count && w.steps[_step].note != null) report.AppendLine("  -> " + w.steps[_step].note);
        }

        void EndWalk(bool reachedEnd)
        {
            var w = walks[_walk];
            _input.move = Vector2.zero; _input.sprint = false;
            bool pass = reachedEnd;
            string endRoom = RoomName();
            report.AppendLine("  end at " + F(transform.position) + " in '" + endRoom + "' after " + (Time.time - _walkStart).ToString("F1") + " s; mean speed " + (_speedN > 0 ? (_speedSum / _speedN).ToString("F2") : "-") + " m/s; y range " + _minY.ToString("F2") + ".." + _maxY.ToString("F2"));
            report.AppendLine("  rooms: " + string.Join(" > ", roomTrail));
            if (!string.IsNullOrEmpty(w.mustEndIn) && endRoom != w.mustEndIn) { report.AppendLine("  FAIL: ended in '" + endRoom + "', expected '" + w.mustEndIn + "'"); pass = false; }
            if (_violation != null) { report.AppendLine("  FAIL: " + _violation); pass = false; }
            report.AppendLine("  RESULT: " + (pass ? "PASS" : "FAIL"));
            report.AppendLine();
            if (!pass) allPassed = false;
            w.onDone?.Invoke(this);
            if (!pass)
            {
                for (int i = _walk + 1; i < walks.Count; i++) report.AppendLine("WALK " + (i + 1) + ": " + walks[i].name + "\n  NOT RUN: the previous walk failed and the player may only move by walking\n  RESULT: FAIL\n");
                allPassed = false; Finish(); return;
            }
            if (_walk + 1 < walks.Count) { _wait = 0.5f; BeginWalk(_walk + 1); }
            else Finish();
        }

        void Finish()
        {
            _finished = true; _input.move = Vector2.zero;
            onAllDone?.Invoke(report.ToString(), allPassed);
        }
    }
}
