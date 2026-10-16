using System;
using UnityEngine;

namespace IronCitadelV2
{
    /// <summary>The v2 play-mode HUD, forked from v1's IronCitadelHud: the room the player stands in, the elapsed
    /// time, an "E: open" line while the player looks at a shut door, and an announcement when the player reaches
    /// the throne (the timer stops there). v1's reads its rooms from v1's level.json; this one carries the room grid
    /// itself (the builder fills <see cref="rooms"/> from v2's level.json), so it needs no TextAsset. IMGUI, so it
    /// needs no canvas, font asset or event system. The M map is v1's IronCitadelOverview, unchanged.</summary>
    public class IronCitadelV2Hud : MonoBehaviour
    {
        [Serializable]
        public class RoomCell
        {
            public string id;
            public string name;
            public int row, col;
        }

        [Tooltip("Tile size (m); cell [r, c] spans x 45c..45c+45 and z -45r-45..-45r.")]
        public float tile = 45f;
        public int rows, cols;
        public RoomCell[] rooms = new RoomCell[0];
        [Tooltip("What the goal is called in the announcement: 'You have reached the <goalWord>'.")]
        public string goalWord = "throne";
        [Tooltip("The walker's moving transform (Starter Assets' PlayerCapsule). Falls back to the object tagged Player.")]
        public Transform player;
        [Tooltip("The throne: the level's Goal marker, at the throne's foot on the stage.")]
        public Transform goal;
        [Tooltip("How close to the throne, across the floor, counts as reaching it (m).")]
        public float goalReach = 3f;
        [Tooltip("The player's feet must be at least this high relative to the goal (m), i.e. up on the stage.")]
        public float goalBelowSlack = 0.6f;
        [Tooltip("How far the E prompt looks for a door (m); the walker's E reaches 3 m.")]
        public float promptRange = 3f;

        float startTime;
        float? reachedAfter;
        float reachedAt;
        string roomName = "", roomId, prompt;
        GUIStyle panel, big, small, banner, bannerSmall;
        Texture2D panelTex;

        public bool ThroneReached => reachedAfter.HasValue;
        public string RoomId => roomId;
        public string RoomName => roomName;

        void Start()
        {
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
            startTime = Time.time;
        }

        /// <summary>The room's name at a world point: "Solid rock" in a cell with no room, "Outside" off the grid.</summary>
        public string RoomNameAt(Vector3 p, out string id)
        {
            id = null;
            int c = Mathf.FloorToInt(p.x / tile), r = Mathf.FloorToInt(-p.z / tile);
            if (r < 0 || r >= rows || c < 0 || c >= cols) return "Outside";
            foreach (var room in rooms)
                if (room.row == r && room.col == c) { id = room.id; return room.name; }
            return "Solid rock";
        }

        void Update()
        {
            if (player == null) return;
            var p = player.position;
            roomName = RoomNameAt(p, out roomId);
            if (!reachedAfter.HasValue && goal != null)
            {
                var d = p - goal.position;
                if (new Vector2(d.x, d.z).magnitude <= goalReach && d.y >= -goalBelowSlack)
                {
                    reachedAfter = Time.time - startTime;
                    reachedAt = Time.time;
                    Debug.Log($"[IronCitadelV2] The {goalWord} is reached after {Clock(reachedAfter.Value)}.");
                }
            }
            prompt = null;
            var cam = Camera.main;
            if (cam != null && cam.isActiveAndEnabled &&
                Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, promptRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var door = hit.collider.GetComponentInParent<IronCitadelV2Door>();
                if (door != null && door.showPrompt && !door.IsOpen && !door.IsMoving) prompt = "E: open the door";
            }
        }

        static string Clock(float s)
        {
            int t = Mathf.FloorToInt(s);
            return $"{t / 60:00}:{t % 60:00}";
        }

        void Styles()
        {
            if (panel != null) return;
            panelTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            panelTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.07f, 0.62f));
            panelTex.Apply();
            panel = new GUIStyle { normal = { background = panelTex } };
            big = new GUIStyle { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.93f, 0.78f) } };
            small = new GUIStyle { normal = { textColor = new Color(0.85f, 0.85f, 0.9f) } };
            banner = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.35f) } };
            bannerSmall = new GUIStyle { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        void OnGUI()
        {
            Styles();
            float u = Mathf.Max(1f, Screen.height / 720f);
            big.fontSize = Mathf.RoundToInt(22 * u);
            small.fontSize = Mathf.RoundToInt(15 * u);
            banner.fontSize = Mathf.RoundToInt(40 * u);
            bannerSmall.fontSize = Mathf.RoundToInt(18 * u);

            // top-left: room and time
            float elapsed = reachedAfter ?? (Time.time - startTime);
            float w = Mathf.Max(300 * u, big.CalcSize(new GUIContent(roomName)).x + 24 * u);
            var box = new Rect(14 * u, 14 * u, w, 66 * u);
            GUI.Box(box, GUIContent.none, panel);
            GUI.Label(new Rect(box.x + 12 * u, box.y + 7 * u, box.width - 24 * u, 30 * u), roomName, big);
            GUI.Label(new Rect(box.x + 12 * u, box.y + 38 * u, box.width - 24 * u, 22 * u),
                (reachedAfter.HasValue ? "Time " + Clock(elapsed) + "  (stopped)" : "Time " + Clock(elapsed)) + "      M: map", small);

            // the door under the cross-hair
            if (!string.IsNullOrEmpty(prompt))
            {
                var pr = new Rect(Screen.width / 2f - 110 * u, Screen.height * 0.62f, 220 * u, 30 * u);
                GUI.Box(pr, GUIContent.none, panel);
                GUI.Label(pr, prompt, bannerSmall);
            }

            // the throne: a banner for six seconds, then a line under the panel
            if (reachedAfter.HasValue)
            {
                if (Time.time - reachedAt < 6f)
                {
                    var b = new Rect(Screen.width / 2f - 330 * u, Screen.height * 0.22f, 660 * u, 110 * u);
                    GUI.Box(b, GUIContent.none, panel);
                    GUI.Label(new Rect(b.x, b.y + 8 * u, b.width, 60 * u), "You have reached the " + goalWord, banner);
                    GUI.Label(new Rect(b.x, b.y + 66 * u, b.width, 30 * u), "The Iron Citadel falls in " + Clock(reachedAfter.Value), bannerSmall);
                }
                else
                {
                    var b = new Rect(box.x, box.yMax + 6 * u, box.width, 30 * u);
                    GUI.Box(b, GUIContent.none, panel);
                    GUI.Label(new Rect(b.x + 12 * u, b.y + 5 * u, b.width, 22 * u),
                        (string.IsNullOrEmpty(goalWord) ? "Goal" : char.ToUpperInvariant(goalWord[0]) + goalWord.Substring(1)) + " reached in " + Clock(reachedAfter.Value), small);
                }
            }
        }

        void OnDestroy()
        {
            if (panelTex != null) Destroy(panelTex);
        }
    }
}
