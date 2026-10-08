using UnityEngine;
using UnityEngine.InputSystem;

namespace IronCitadel
{
    /// <summary>
    /// The level's HUD and overview. Shows the room the player is in and the elapsed time, announces the throne,
    /// and M toggles a top-down orthographic overview of the whole level. Ceilings live on the "Ceiling" layer,
    /// which the overview camera does not draw; room labels, markers and the rock cap live on the "Overview"
    /// layer, which only the overview camera draws.
    /// </summary>
    public class CitadelHUD : MonoBehaviour
    {
        public static CitadelHUD Instance { get; private set; }

        [Header("Scene references")]
        public Transform player;
        public Camera overviewCamera;
        public Light overviewLight;

        [Header("Goal: the throne")]
        public Vector3 throneCentre = new Vector3(112.5f, 1.25f, -6f);
        public Vector3 throneHalfExtents = new Vector3(4f, 2.5f, 4.5f);

        [Header("Look")]
        public bool fogInFirstPerson = true;
        public Color overviewAmbient = new Color(0.55f, 0.55f, 0.6f);

        float _start;
        bool _reached;
        float _reachedAt;
        string _message;
        float _messageUntil;
        bool _overview;
        Camera _main;
        Color _ambient;
        GUIStyle _small, _room, _big, _hint;
        Transform _marker;

        public bool OverviewOn => _overview;
        public bool Reached => _reached;

        InputAction _overviewKey;

        void Awake() { Instance = this; }

        void OnEnable()
        {
            _overviewKey = new InputAction("Overview", InputActionType.Button, "<Keyboard>/m");
            _overviewKey.performed += _ => SetOverview(!_overview);
            _overviewKey.Enable();
        }

        void OnDisable()
        {
            if (_overviewKey == null) return;
            _overviewKey.Disable();
            _overviewKey.Dispose();
            _overviewKey = null;
        }

        void Start()
        {
            _start = Time.time;
            if (player == null)
            {
                var go = GameObject.Find("PlayerCapsule");
                if (go != null) player = go.transform;
            }
            _main = Camera.main;
            _ambient = RenderSettings.ambientLight;
            if (overviewCamera != null) overviewCamera.enabled = false;
            if (overviewLight != null) overviewLight.enabled = false;
            MakePlayerMarker();
            Announce("Reach the warlord's throne.  The armory gate is down — find the long way round.", 7f);
        }

        void MakePlayerMarker()
        {
            if (player == null) return;
            int layer = LayerMask.NameToLayer("Overview");
            if (layer < 0) return;
            var root = new GameObject("OverviewPlayerMarker").transform;
            root.SetParent(player, false);
            root.localPosition = new Vector3(0, 20f, 0);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(0.2f, 1f, 0.3f);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(root, false);
            disc.transform.localScale = new Vector3(3.2f, 0.05f, 3.2f);
            disc.GetComponent<Renderer>().sharedMaterial = mat;
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(root, false);
            nose.transform.localPosition = new Vector3(0, 0, 2.4f);
            nose.transform.localRotation = Quaternion.Euler(0, 45, 0);
            nose.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);
            nose.GetComponent<Renderer>().sharedMaterial = mat;
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
            _marker = root;
        }

        void Update()
        {
            if (!_reached && player != null)
            {
                Vector3 d = player.position - throneCentre;
                if (Mathf.Abs(d.x) <= throneHalfExtents.x && Mathf.Abs(d.y) <= throneHalfExtents.y && Mathf.Abs(d.z) <= throneHalfExtents.z)
                {
                    _reached = true;
                    _reachedAt = Time.time - _start;
                    Announce("THE THRONE IS REACHED\nThe warlord is at your mercy.  Time " + Clock(_reachedAt), 30f);
                }
            }
        }

        public void SetOverview(bool on)
        {
            _overview = on;
            if (overviewCamera != null) overviewCamera.enabled = on;
            if (_main == null) _main = Camera.main;
            if (_main != null) _main.enabled = !on;
            if (overviewLight != null) overviewLight.enabled = on;
            RenderSettings.fog = !on && fogInFirstPerson;
            RenderSettings.ambientLight = on ? overviewAmbient : _ambient;
        }

        public void Announce(string text, float seconds)
        {
            _message = text;
            _messageUntil = Time.time + seconds;
        }

        public float Elapsed => _reached ? _reachedAt : Time.time - _start;

        static string Clock(float t)
        {
            int s = Mathf.FloorToInt(t);
            return $"{s / 60:00}:{s % 60:00}";
        }

        void Styles()
        {
            if (_small != null) return;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperLeft };
            _small.normal.textColor = new Color(0.95f, 0.88f, 0.7f);
            _room = new GUIStyle(_small) { fontSize = 24, fontStyle = FontStyle.Bold };
            _big = new GUIStyle(_small) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _big.normal.textColor = new Color(1f, 0.85f, 0.4f);
            _hint = new GUIStyle(_small) { fontSize = 15, alignment = TextAnchor.LowerLeft };
            _hint.normal.textColor = new Color(0.85f, 0.8f, 0.7f, 0.8f);
        }

        void Shadowed(Rect r, string text, GUIStyle style)
        {
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.85f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        void OnGUI()
        {
            Styles();
            float s = Screen.height / 1080f;
            _small.fontSize = Mathf.RoundToInt(18 * s);
            _room.fontSize = Mathf.RoundToInt(26 * s);
            _big.fontSize = Mathf.RoundToInt(32 * s);
            _hint.fontSize = Mathf.RoundToInt(15 * s);

            string room = player != null ? CitadelMap.Describe(player.position) : "";
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(14 * s, 14 * s, 560 * s, 74 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Shadowed(new Rect(26 * s, 18 * s, 540 * s, 40 * s), room, _room);
            Shadowed(new Rect(26 * s, 54 * s, 540 * s, 30 * s), "Time  " + Clock(Elapsed) + (_reached ? "   (throne reached)" : ""), _small);

            Shadowed(new Rect(18 * s, Screen.height - 40 * s, 900 * s, 30 * s),
                _overview ? "OVERVIEW — ceilings hidden.  M: back to first person" : "WASD walk · Shift sprint · E use · M overview", _hint);

            if (!string.IsNullOrEmpty(_message) && Time.time < _messageUntil)
            {
                var r = new Rect(Screen.width * 0.15f, Screen.height * 0.30f, Screen.width * 0.7f, 140 * s);
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                Shadowed(r, _message, _big);
            }
        }
    }
}
