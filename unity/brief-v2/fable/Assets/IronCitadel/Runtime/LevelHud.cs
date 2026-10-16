using UnityEngine;
using UnityEngine.UI;

namespace IronCitadel
{
    /// <summary>A small HUD: the room the player is in, elapsed time, a key hint, and the throne announcement. Built in code at start.</summary>
    public class LevelHud : MonoBehaviour
    {
        public Transform player;
        Text _room, _time, _hint, _announce, _mapTitle;
        float _announceUntil = -1f;
        public string CurrentRoomName { get; private set; } = "";
        public static LevelHud Instance;

        void Awake()
        {
            Instance = this;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

            var panel = new GameObject("Panel", typeof(Image)); panel.transform.SetParent(canvasGo.transform, false);
            var pimg = panel.GetComponent<Image>(); pimg.color = new Color(0, 0, 0, 0.45f);
            var prt = panel.GetComponent<RectTransform>(); prt.anchorMin = new Vector2(0, 1); prt.anchorMax = new Vector2(0, 1); prt.pivot = new Vector2(0, 1);
            prt.anchoredPosition = new Vector2(16, -16); prt.sizeDelta = new Vector2(440, 100);

            _room = MakeText(canvasGo.transform, font, 30, new Vector2(0, 1), new Vector2(28, -22), new Vector2(420, 40), TextAnchor.UpperLeft, FontStyle.Bold);
            _time = MakeText(canvasGo.transform, font, 22, new Vector2(0, 1), new Vector2(28, -58), new Vector2(420, 30), TextAnchor.UpperLeft, FontStyle.Normal);
            _hint = MakeText(canvasGo.transform, font, 18, new Vector2(0, 1), new Vector2(28, -86), new Vector2(420, 26), TextAnchor.UpperLeft, FontStyle.Normal);
            _hint.text = "E: use    M: map    Shift: sprint"; _hint.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            _announce = MakeText(canvasGo.transform, font, 44, new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1400, 70), TextAnchor.MiddleCenter, FontStyle.Bold);
            _announce.color = new Color(1f, 0.85f, 0.4f); _announce.text = "";
            var shadow = _announce.gameObject.AddComponent<Shadow>(); shadow.effectDistance = new Vector2(2, -2); shadow.effectColor = new Color(0, 0, 0, 0.9f);
            _mapTitle = MakeText(canvasGo.transform, font, 26, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(900, 36), TextAnchor.UpperCenter, FontStyle.Bold);
            _mapTitle.text = ""; _mapTitle.color = new Color(1f, 1f, 1f, 0.95f);
        }

        static Text MakeText(Transform parent, Font font, int size, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, TextAnchor align, FontStyle style)
        {
            var go = new GameObject("Text", typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = align; t.fontStyle = style; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            return t;
        }

        public void Announce(string msg) { _announce.text = msg; _announceUntil = float.MaxValue; }
        public string AnnouncementText => _announce != null ? _announce.text : "";
        public void SetMapTitle(string s) { if (_mapTitle != null) _mapTitle.text = s; }

        void Update()
        {
            if (player == null)
            {
                var cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) player = cc.transform;
            }
            if (player != null)
            {
                var r = IronRoom.RoomAt(player.position);
                string name = r == null ? "(between rooms)" : (r.isGallery ? r.parentRoom + " (" + r.roomName + ")" : r.roomName);
                CurrentRoomName = r == null ? "" : r.roomName;
                _room.text = name;
            }
            float t = Time.timeSinceLevelLoad;
            _time.text = string.Format("Time {0:00}:{1:00}", Mathf.FloorToInt(t / 60f), Mathf.FloorToInt(t % 60f));
            if (_announceUntil < Time.time) _announce.text = "";
        }
    }
}
