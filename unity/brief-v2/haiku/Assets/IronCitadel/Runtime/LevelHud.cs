using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using IronCitadel.Interact;

namespace IronCitadel.Level
{
    /// <summary>
    /// The Iron Citadel's HUD: names the room the player is in, shows the elapsed time, announces the throne when
    /// the player reaches it, and toggles a top-down overview of the whole level with M. The overview is a second
    /// camera that sees everything except the Ceiling layer. Put it on any object in the level scene.
    /// </summary>
    public class LevelHud : MonoBehaviour
    {
        public const string CeilingLayer = "Ceiling";

        [Tooltip("How close, on the floor, the player must come to the throne for it to be announced (metres).")]
        public float throneRadius = 4f;

        Transform _player;
        Camera _overview;
        LevelRoom[] _rooms;
        Vector3[] _roomCentres;
        LevelFeature _throne;
        float _startTime, _throneTime = -1f;
        string _room = "";
        bool _map;
        Vector3 _mapCentre;
        float _mapHalfHeight;
        readonly RaycastHit[] _hits = new RaycastHit[32];
        GUIStyle _label, _banner, _small;

        void Start()
        {
            _startTime = Time.unscaledTime;
            var interact = Object.FindFirstObjectByType<InteractOnE>();
            if (interact != null) _player = interact.transform;
            _rooms = Object.FindObjectsByType<LevelRoom>(FindObjectsSortMode.None);
            _roomCentres = new Vector3[_rooms.Length];
            var all = Object.FindObjectsByType<LevelFeature>(FindObjectsSortMode.None);
            foreach (var f in all) if (f.feature == "throne") _throne = f;

            // the bounds of every floor tile of every room
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            for (int i = 0; i < _rooms.Length; i++)
            {
                var b = new Bounds(Vector3.zero, Vector3.zero);
                bool has = false;
                foreach (var r in _rooms[i].GetComponentsInChildren<Renderer>())
                {
                    if (!r.gameObject.name.StartsWith("Floor")) continue;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                    if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                }
                _roomCentres[i] = has ? b.center : _rooms[i].transform.position;
            }
            _mapCentre = any ? new Vector3(bounds.center.x, 0f, bounds.center.z) : Vector3.zero;
            _mapHalfHeight = any ? Mathf.Max(bounds.size.x, bounds.size.z) * 0.6f : 30f;

            var go = new GameObject("Overview camera");
            _overview = go.AddComponent<Camera>();
            _overview.orthographic = true;
            _overview.clearFlags = CameraClearFlags.SolidColor;
            _overview.backgroundColor = new Color(0.03f, 0.03f, 0.035f);
            _overview.cullingMask = ~(1 << LayerMask.NameToLayer(CeilingLayer));
            _overview.nearClipPlane = 0.5f;
            _overview.farClipPlane = 400f;
            _overview.depth = 10f;
            _overview.enabled = false;
            _overview.transform.SetParent(transform, false);

            _label = new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.92f, 0.86f, 0.72f) } };
            _small = new GUIStyle { fontSize = 14, normal = { textColor = new Color(0.8f, 0.78f, 0.7f) } };
            _banner = new GUIStyle { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.82f, 0.4f) } };
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) SetMap(!_map);

            if (_player == null) return;
            _room = RoomUnder(_player.position);

            if (_throne != null && _throneTime < 0f)
            {
                Vector3 d = _player.position - _throne.transform.position;
                d.y = 0f;
                if (d.magnitude < throneRadius && Mathf.Abs(_player.position.y - _throne.transform.position.y) < 2f)
                    _throneTime = Time.unscaledTime - _startTime;
            }
        }

        string RoomUnder(Vector3 feet)
        {
            // the first floor the ray meets below the player's feet, skipping the player's own colliders
            int n = Physics.RaycastNonAlloc(feet + Vector3.up * 0.3f, Vector3.down, _hits, 1.5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            string name = "";
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.transform.IsChildOf(_player)) continue;
                var room = h.collider.GetComponentInParent<LevelRoom>();
                if (room != null && h.distance < best) { best = h.distance; name = room.roomName; }
            }
            return name;
        }

        void SetMap(bool on)
        {
            if (on == _map) return;
            _map = on;
            _overview.enabled = on;
            // the level's fog is for eye level; from the overview's height it would hide the whole plan
            if (on) { _fogWas = RenderSettings.fog; RenderSettings.fog = false; }
            else RenderSettings.fog = _fogWas;
            if (!on) return;
            _overview.transform.SetPositionAndRotation(_mapCentre + Vector3.up * 200f, Quaternion.Euler(90f, 0f, 0f));
            _overview.orthographicSize = _mapHalfHeight;
        }

        bool _fogWas;

        void OnGUI()
        {
            float t = Time.unscaledTime - _startTime;
            GUI.Label(new Rect(16, 12, 600, 30), "Room: " + (_room == "" ? "-" : _room), _label);
            GUI.Label(new Rect(16, 40, 300, 24), "Time " + FormatTime(t), _small);
            GUI.Label(new Rect(16, Screen.height - 34, 700, 24), "WASD move  Shift sprint  E open  M overview map", _small);

            if (_throneTime >= 0f)
            {
                GUI.Label(new Rect(16, 66, 400, 24), "Throne reached at " + FormatTime(_throneTime), _small);
                if (t - _throneTime < 12f)
                    GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60),
                        "The warlord's throne is yours. The Iron Citadel falls silent.", _banner);
            }

            if (_map && _overview != null)
            {
                for (int i = 0; i < _rooms.Length; i++)
                {
                    Vector3 s = _overview.WorldToScreenPoint(_roomCentres[i]);
                    if (s.z < 0f) continue;
                    GUI.Label(new Rect(s.x - 80, Screen.height - s.y - 10, 160, 20), _rooms[i].roomName, _small);
                }
                if (_player != null)
                {
                    Vector3 p = _overview.WorldToScreenPoint(_player.position + Vector3.up);
                    GUI.Label(new Rect(p.x - 8, Screen.height - p.y - 10, 60, 20), "You", _label);
                }
            }
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }
    }
}
