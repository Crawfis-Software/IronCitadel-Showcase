using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// Drives the real first-person controller along a scripted route in play mode and records what happens:
    /// rooms named by the HUD, heights reached (the gallery stair), the secret door, the gate, the throne.
    /// Added to the scene only by the editor's playtest command; never part of the shipped level.
    /// </summary>
    public class PlaytestDriver : MonoBehaviour
    {
        [System.Serializable]
        public class Step
        {
            public string name;
            public Vector3 target;
            public bool sprint;
            public string action;      // "", "shot", "interact", "overview", "expectBlocked"
            public float timeout = 30f;
        }

        public List<Step> steps = new List<Step>();
        public Transform player;           // the PlayerCapsule
        public LevelHud hud;
        public string reportPath = "Tools/playtest.txt";

        Component _inputs;                 // StarterAssets.StarterAssetsInputs, found by name
        Component _interact;               // IronCitadel.Interact.InteractOnE
        readonly StringBuilder _log = new StringBuilder();
        Vector3 _wanted;
        bool _sprint;
        bool _driving;

        IEnumerator Start()
        {
            yield return null;
            foreach (var c in player.GetComponents<Component>())
            {
                if (c == null) continue;
                var n = c.GetType().Name;
                if (n == "StarterAssetsInputs") _inputs = c;
                if (n == "InteractOnE") _interact = c;
            }
            Log("start " + Fmt(player.position) + " room=" + Room() + " inputs=" + (_inputs != null) + " interact=" + (_interact != null));
            yield return new WaitForSeconds(0.5f);
            foreach (var s in steps)
            {
                yield return Walk(s);
            }
            Log("done at " + Fmt(player.position) + " t=" + Time.timeSinceLevelLoad.ToString("F1"));
            File.WriteAllText(reportPath, _log.ToString());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        IEnumerator Walk(Step s)
        {
            _wanted = s.target;
            _sprint = s.sprint;
            _driving = true;
            float t0 = Time.timeSinceLevelLoad;
            float best = float.MaxValue;
            float lastProgress = t0;
            while (true)
            {
                var d = Flat(_wanted - player.position);
                float dist = d.magnitude;
                if (dist < best - 0.05f) { best = dist; lastProgress = Time.timeSinceLevelLoad; }
                if (dist < 0.45f) break;
                if (Time.timeSinceLevelLoad - t0 > s.timeout || Time.timeSinceLevelLoad - lastProgress > 6f)
                {
                    Log("STUCK " + s.name + " at " + Fmt(player.position) + " wanted " + Fmt(_wanted) + " dist " + dist.ToString("F2") + (s.name.Contains("blocked") ? " (expected: the gate is closed)" : ""));
                    break;
                }
                yield return null;
            }
            _driving = false;
            Drive(Vector2.zero, false);
            Log("reached " + s.name + " " + Fmt(player.position) + " y=" + player.position.y.ToString("F2") + " room=" + Room() + " t=" + Time.timeSinceLevelLoad.ToString("F1"));
            yield return null;
            switch (s.action)
            {
                case "shot":
                    yield return Shot("play_" + s.name);
                    break;
                case "interact":
                    if (_interact != null) _interact.SendMessage("Interact", SendMessageOptions.DontRequireReceiver);
                    Log("pressed E at " + Fmt(player.position));
                    yield return new WaitForSeconds(2f);
                    yield return Shot("play_" + s.name);
                    break;
                case "overview":
                    hud.ToggleOverview();
                    Log("overview on=" + hud.OverviewOn);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("play_" + s.name);
                    hud.ToggleOverview();
                    Log("overview on=" + hud.OverviewOn);
                    yield return null;
                    break;
            }
        }

        IEnumerator Shot(string name)
        {
            Directory.CreateDirectory("Captures");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Captures/" + name + ".png");
            Log("shot " + name);
            yield return new WaitForSeconds(0.4f);
        }

        Vector3 _lastPos;
        float _stallSince;
        float _sidestepUntil;
        float _sidestepDir = 1f;

        void Update()
        {
            if (!_driving || player == null) return;
            var d = Flat(_wanted - player.position);
            if (d.sqrMagnitude < 0.0001f) return;
            player.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            // if wedged on furniture, strafe for a moment, alternating sides
            float moved = Flat(player.position - _lastPos).magnitude;
            _lastPos = player.position;
            if (moved > 0.01f) _stallSince = Time.time;
            if (Time.time < _sidestepUntil) { Drive(new Vector2(_sidestepDir, 0.35f), false); return; }
            if (Time.time - _stallSince > 0.6f)
            {
                _sidestepUntil = Time.time + 0.7f;
                _sidestepDir = -_sidestepDir;
                _stallSince = Time.time;
            }
            Drive(new Vector2(0f, 1f), _sprint);
        }

        void Drive(Vector2 move, bool sprint)
        {
            if (_inputs == null) return;
            _inputs.SendMessage("MoveInput", move, SendMessageOptions.DontRequireReceiver);
            _inputs.SendMessage("SprintInput", sprint, SendMessageOptions.DontRequireReceiver);
        }

        string Room() => hud != null ? hud.RoomAt(player.position) : "?";
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static string Fmt(Vector3 v) => "(" + v.x.ToString("F1") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F1") + ")";
        void Log(string s) { _log.AppendLine(s); Debug.Log("[Playtest] " + s); }
    }
}
