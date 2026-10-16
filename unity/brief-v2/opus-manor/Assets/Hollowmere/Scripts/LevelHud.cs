using UnityEngine;

namespace Hollowmere
{
    /// <summary>
    /// A small IMGUI HUD: the room the player is in, the elapsed time, short notices, the announcement when the
    /// master's chair is reached, and room labels while the overview is up.
    /// </summary>
    public class LevelHud : MonoBehaviour
    {
        public static string CurrentRoomName { get; private set; } = "";
        public static float Elapsed => s_Instance != null ? Time.time - s_Instance._startTime : Time.timeSinceLevelLoad;

        static LevelHud s_Instance;
        static string s_Toast;
        static float s_ToastUntil;

        float _startTime;
        CharacterController _player;
        GUIStyle _box, _big, _label, _small;
        Texture2D _bg, _dot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { CurrentRoomName = ""; s_Instance = null; s_Toast = null; s_ToastUntil = 0f; }

        public static void Toast(string message, float seconds = 2.5f)
        {
            s_Toast = message;
            s_ToastUntil = Time.time + seconds;
        }

        public static string FormatTime(float t)
        {
            int s = Mathf.FloorToInt(t);
            return $"{s / 60:00}:{s % 60:00}";
        }

        void Awake()
        {
            s_Instance = this;
            _startTime = Time.time;
        }

        void Update()
        {
            if (_player == null) _player = FindAnyObjectByType<CharacterController>();
            if (_player == null) return;
            var room = RoomInfo.At(_player.transform.position + Vector3.up * 0.1f);
            if (room != null) CurrentRoomName = room.roomName;
        }

        void Styles()
        {
            if (_box != null && _bg != null && _dot != null) return;
            _bg = new Texture2D(1, 1); _bg.SetPixel(0, 0, new Color(0.03f, 0.02f, 0.02f, 0.72f)); _bg.Apply();
            _dot = new Texture2D(1, 1); _dot.SetPixel(0, 0, Color.white); _dot.Apply();
            _box = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, richText = true };
            _box.normal.background = _bg; _box.normal.textColor = new Color(0.93f, 0.9f, 0.82f);
            _box.padding = new RectOffset(12, 12, 6, 6);
            _big = new GUIStyle(_box) { alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            _label.normal.textColor = new Color(1f, 0.95f, 0.85f);
            _label.normal.background = _bg;
            _label.padding = new RectOffset(6, 6, 2, 2);
            _small = new GUIStyle(_box) { alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            Styles();
            float u = Mathf.Max(Screen.height / 820f, 0.85f);
            _box.fontSize = Mathf.RoundToInt(22 * u);
            _small.fontSize = Mathf.RoundToInt(15 * u);
            _big.fontSize = Mathf.RoundToInt(34 * u);
            _label.fontSize = Mathf.RoundToInt(17 * u);

            float pad = 16 * u;
            string room = string.IsNullOrEmpty(CurrentRoomName) ? "—" : CurrentRoomName;
            GUI.Label(new Rect(pad, pad, 360 * u, 40 * u), room, _box);
            GUI.Label(new Rect(Screen.width - pad - 130 * u, pad, 130 * u, 40 * u), FormatTime(Elapsed), _box);
            GUI.Label(new Rect(Screen.width - pad - 250 * u, Screen.height - pad - 30 * u, 250 * u, 30 * u),
                OverviewMode.Active ? "M  back to the player" : "M  overview      E  use", _small);

            if (ChairGoal.Reached)
            {
                var r = new Rect(Screen.width * 0.5f - 420 * u, Screen.height * 0.22f, 840 * u, 80 * u);
                GUI.Label(r, $"You stand before the master's chair  ({FormatTime(ChairGoal.ReachedAt)})", _big);
            }
            if (s_Toast != null && Time.time < s_ToastUntil)
            {
                var r = new Rect(Screen.width * 0.5f - 300 * u, Screen.height * 0.70f, 600 * u, 44 * u);
                GUI.Label(r, s_Toast, _small);
            }

            if (OverviewMode.Active && OverviewMode.Instance != null && OverviewMode.Instance.overviewCamera != null)
            {
                var cam = OverviewMode.Instance.overviewCamera;
                foreach (var info in RoomInfo.All)
                {
                    if (info.rects == null || info.rects.Length == 0) continue;
                    Vector3 sp = cam.WorldToScreenPoint(info.Centre);
                    var size = _label.CalcSize(new GUIContent(info.roomName));
                    var lr = new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y * 0.5f, size.x, size.y);
                    GUI.Label(lr, info.roomName, _label);
                }
                if (_player != null)
                {
                    Vector3 sp = cam.WorldToScreenPoint(_player.transform.position);
                    float s = 14 * u;
                    var c = GUI.color; GUI.color = new Color(1f, 0.25f, 0.2f);
                    GUI.DrawTexture(new Rect(sp.x - s * 0.5f, Screen.height - sp.y - s * 0.5f, s, s), _dot);
                    GUI.color = c;
                }
            }
        }
    }
}
