using UnityEngine;
using UnityEngine.InputSystem;

namespace IronCitadel
{
    /// <summary>
    /// On-screen HUD for the Iron Citadel: names the room the player is in, shows elapsed time,
    /// toggles a top-down overview (M) with the ceilings hidden, and announces reaching the throne.
    /// </summary>
    public class LevelHud : MonoBehaviour
    {
        [System.Serializable]
        public class RoomEntry { public string id; public string displayName; public int row; public int col; }

        public RoomEntry[] rooms;
        public Transform player;
        public Camera overviewCamera;
        public GameObject overviewMarker;
        public GameObject overviewLight;
        public float tileSize = 45f;
        public float galleryDeckY = 5f;
        public string hallId = "great_hall";

        string _announcement;
        float _announceTime = -1f;
        bool _overview;
        bool _fogDefault;
        bool _throneReached;
        float _throneTime;
        GUIStyle _big, _small, _box, _centre;
        Camera _main;

        void Start()
        {
            _fogDefault = RenderSettings.fog;
            if (overviewCamera != null) overviewCamera.enabled = false;
            if (overviewMarker != null) overviewMarker.SetActive(false);
            _main = Camera.main;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) ToggleOverview();
            if (overviewMarker != null && player != null && _overview)
                overviewMarker.transform.position = new Vector3(player.position.x, 40f, player.position.z);
        }

        public bool OverviewOn => _overview;

        public void ToggleOverview()
        {
            _overview = !_overview;
            if (_main == null) _main = Camera.main;
            if (overviewCamera != null) overviewCamera.enabled = _overview;
            if (overviewMarker != null) overviewMarker.SetActive(_overview);
            if (overviewLight != null) overviewLight.SetActive(_overview);
            if (_main != null) _main.enabled = !_overview;
            RenderSettings.fog = _overview ? false : _fogDefault;
        }

        public void Announce(string message)
        {
            _announcement = message;
            _announceTime = Time.timeSinceLevelLoad;
        }

        public void ThroneReached()
        {
            if (_throneReached) return;
            _throneReached = true;
            _throneTime = Time.timeSinceLevelLoad;
            Announce("You have reached the warlord’s throne  —  " + Clock(_throneTime));
        }

        public string RoomAt(Vector3 p)
        {
            int c = Mathf.FloorToInt(p.x / tileSize);
            int r = Mathf.FloorToInt(-p.z / tileSize);
            if (rooms != null)
                foreach (var e in rooms)
                    if (e.row == r && e.col == c)
                    {
                        if (e.id == hallId && p.y > galleryDeckY - 0.5f) return e.displayName + " — minstrels’ gallery";
                        return e.displayName;
                    }
            return "Solid rock";
        }

        static string Clock(float t)
        {
            int m = Mathf.FloorToInt(t / 60f), s = Mathf.FloorToInt(t % 60f);
            return m.ToString("00") + ":" + s.ToString("00");
        }

        void OnGUI()
        {
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                _big.normal.textColor = Color.white;
                _small = new GUIStyle(GUI.skin.label) { fontSize = 17 };
                _small.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                _box = new GUIStyle(GUI.skin.box);
                _centre = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter };
                _centre.normal.textColor = new Color(1f, 0.85f, 0.4f);
            }
            string room = player != null ? RoomAt(player.position) : "";
            string time = Clock(Time.timeSinceLevelLoad);
            GUI.Box(new Rect(16, 16, 460, 96), GUIContent.none, _box);
            GUI.Label(new Rect(28, 22, 440, 36), room, _big);
            GUI.Label(new Rect(28, 56, 440, 28), "Time " + time + (_overview ? "    [overview]" : ""), _small);
            GUI.Label(new Rect(28, 80, 440, 28), "WASD move   Shift sprint   E use   M overview", _small);

            if (!string.IsNullOrEmpty(_announcement))
            {
                float age = Time.timeSinceLevelLoad - _announceTime;
                if (_throneReached || age < 6f)
                {
                    var r = new Rect(Screen.width * 0.5f - 380, Screen.height * 0.5f - 170, 760, 64);
                    GUI.Box(r, GUIContent.none, _box);
                    GUI.Label(r, _announcement, _centre);
                }
            }
        }
    }
}
