using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace IronCitadel
{
    /// <summary>
    /// The level's HUD: names the room the player stands in, shows the time since play began, announces
    /// reaching the throne, and toggles (M) a top-down overview of the whole level with the ceilings hidden.
    /// </summary>
    public class LevelHUD : MonoBehaviour
    {
        public Transform throne;
        [Tooltip("The stage's top surface height; the throne counts as reached on the stage near it.")]
        public float stageTopY;
        public float reachRadius = 2.6f;
        public Camera overviewCamera;

        public bool OverviewOn { get; private set; }
        public bool ThroneReached { get; private set; }
        public float ThroneReachedAt { get; private set; } = -1f;
        public string CurrentRoom { get; private set; } = "";
        public float Elapsed => Time.time - _startTime;

        Transform _player;
        float _startTime;
        float _announceUntil;
        readonly List<Renderer> _ceilingRenderers = new List<Renderer>();
        Camera _mainCam;
        Color _ambient; bool _fog; AmbientMode _ambientMode;
        GUIStyle _small, _big, _box;
        InputAction _overviewKey;

        void OnEnable()
        {
            _overviewKey = new InputAction("Overview", InputActionType.Button, "<Keyboard>/m");
            _overviewKey.performed += _ => ToggleOverview();
            _overviewKey.Enable();
        }

        void OnDisable()
        {
            if (_overviewKey != null) { _overviewKey.Disable(); _overviewKey.Dispose(); _overviewKey = null; }
        }

        void Start()
        {
            _startTime = Time.time;
            var cc = FindAnyObjectByType<CharacterController>();
            if (cc != null) _player = cc.transform;
            foreach (var r in Room.All)
                if (r.ceilingGroup != null) _ceilingRenderers.AddRange(r.ceilingGroup.GetComponentsInChildren<Renderer>(true));
            if (overviewCamera != null) overviewCamera.enabled = false;
        }

        void Update()
        {
            if (_player == null) return;
            var room = Room.At(_player.position);
            if (room != null)
            {
                CurrentRoom = room.roomName;
                if (room.galleryHeight > 0f && _player.position.y > room.floorY + room.galleryHeight - 0.5f) CurrentRoom += " — Gallery";
            }
            if (!ThroneReached && throne != null)
            {
                Vector3 d = _player.position - throne.position; d.y = 0f;
                if (d.magnitude <= reachRadius && _player.position.y >= stageTopY - 0.25f)
                {
                    ThroneReached = true; ThroneReachedAt = Elapsed; _announceUntil = Time.time + 8f;
                    Debug.Log($"[IronCitadel] Throne reached after {ThroneReachedAt:F1} s");
                }
            }
        }

        public void ToggleOverview()
        {
            if (overviewCamera == null) return;
            OverviewOn = !OverviewOn;
            if (_mainCam == null) _mainCam = Camera.main;
            foreach (var r in _ceilingRenderers) if (r != null) r.enabled = !OverviewOn;
            if (OverviewOn)
            {
                _ambient = RenderSettings.ambientLight; _fog = RenderSettings.fog; _ambientMode = RenderSettings.ambientMode;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.75f, 0.72f, 0.68f);
                RenderSettings.fog = false;
                overviewCamera.enabled = true;
                if (_mainCam != null) _mainCam.enabled = false;
            }
            else
            {
                RenderSettings.ambientMode = _ambientMode; RenderSettings.ambientLight = _ambient; RenderSettings.fog = _fog;
                overviewCamera.enabled = false;
                if (_mainCam != null) _mainCam.enabled = true;
            }
        }

        void OnGUI()
        {
            if (_small == null)
            {
                _small = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                _small.normal.textColor = new Color(0.95f, 0.88f, 0.7f);
                _big = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _big.normal.textColor = new Color(1f, 0.82f, 0.35f);
                _box = new GUIStyle(GUI.skin.box);
            }
            int t = Mathf.FloorToInt(Elapsed);
            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.Box(new Rect(12, 12, 330, 58), GUIContent.none, _box);
            GUI.color = Color.white;
            GUI.Label(new Rect(22, 14, 320, 26), CurrentRoom, _small);
            GUI.Label(new Rect(22, 40, 320, 26), $"{t / 60:00}:{t % 60:00}" + (OverviewOn ? "   overview (M)" : "   M: overview"), _small);
            if (Time.time < _announceUntil)
            {
                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.Box(new Rect(Screen.width * 0.5f - 330, Screen.height * 0.3f - 40, 660, 80), GUIContent.none, _box);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 330, Screen.height * 0.3f - 40, 660, 80), "You have reached the Warlord's throne", _big);
            }
        }
    }
}
