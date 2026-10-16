using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronCitadel
{
    /// <summary>M toggles a top-down orthographic view of the whole level with the ceilings hidden, with room labels and a player marker.</summary>
    public class OverviewMap : MonoBehaviour
    {
        public int ceilingLayer = 6;
        public int mapOnlyLayer = 7;
        public float padding = 4f;
        public bool IsOn { get; private set; }
        Camera _cam; Transform _playerMarker; Transform _player; Bounds _levelBounds; readonly List<GameObject> _labels = new List<GameObject>();
        Light _mapLight;

        void Start()
        {
            bool has = false;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.gameObject.layer == mapOnlyLayer) continue;
                if (r.GetComponentInParent<IronMarker>() != null) continue;
                if (!has) { _levelBounds = r.bounds; has = true; } else _levelBounds.Encapsulate(r.bounds);
            }
            var camGo = new GameObject("OverviewCamera");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.depth = 50; _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.03f, 0.03f, 0.04f);
            _cam.cullingMask = ~(1 << ceilingLayer);
            _cam.nearClipPlane = 1f; _cam.farClipPlane = 400f;
            float h = _levelBounds.max.y + 60f;
            camGo.transform.position = new Vector3(_levelBounds.center.x, h, _levelBounds.center.z);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float halfZ = _levelBounds.extents.z + padding, halfX = _levelBounds.extents.x + padding;
            _cam.orthographicSize = Mathf.Max(halfZ, halfX / aspect);
            var extra = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            extra.renderPostProcessing = false; extra.renderShadows = false;
            _cam.enabled = false;

            // a soft top light so the plan reads evenly when the ceilings are hidden (only on while the map is up)
            var lightGo = new GameObject("MapLight"); lightGo.transform.SetParent(transform, false);
            _mapLight = lightGo.AddComponent<Light>(); _mapLight.type = LightType.Directional; _mapLight.intensity = 1.2f; _mapLight.color = new Color(0.9f, 0.92f, 1f);
            _mapLight.shadows = LightShadows.None; lightGo.transform.rotation = Quaternion.Euler(80f, 20f, 0f); _mapLight.enabled = false;

            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m.name = "MapPlayerMarker"; m.layer = mapOnlyLayer; Destroy(m.GetComponent<Collider>());
            m.transform.localScale = Vector3.one * 1.8f;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.color = new Color(0.2f, 1f, 0.3f);
            m.GetComponent<Renderer>().sharedMaterial = mat;
            _playerMarker = m.transform; m.SetActive(false);
            // room labels: screen-space UI text placed over each room's centre while the map is up (TextMesh does not render under URP)
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("MapLabelCanvas", typeof(Canvas)); canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            foreach (var room in FindObjectsByType<IronRoom>(FindObjectsSortMode.None))
            {
                var b = room.FloorBounds();
                var go = new GameObject("MapLabel " + room.roomName, typeof(UnityEngine.UI.Text)); go.transform.SetParent(canvasGo.transform, false);
                var t = go.GetComponent<UnityEngine.UI.Text>();
                t.text = room.roomName.ToUpperInvariant(); t.font = font; t.fontSize = room.isGallery ? 11 : 13; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
                t.color = room.isGallery ? new Color(1f, 0.9f, 0.5f) : Color.white; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                var sh = go.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(1, -1);
                var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(160, 20);
                _labelWorld.Add(new Vector3(b.center.x, b.max.y, b.center.z) + (room.isGallery ? new Vector3(0, 0, 0) : Vector3.zero));
                go.SetActive(false); _labels.Add(go);
            }
            canvasGo.SetActive(false); _labelCanvas = canvasGo;
        }
        readonly List<Vector3> _labelWorld = new List<Vector3>();
        GameObject _labelCanvas;

        public void Toggle() { Set(!IsOn); }

        public void Set(bool on)
        {
            IsOn = on;
            _cam.enabled = on;
            _mapLight.enabled = on;
            _playerMarker.gameObject.SetActive(on);
            if (_labelCanvas != null) _labelCanvas.SetActive(on);
            foreach (var l in _labels) l.SetActive(on);
            if (LevelHud.Instance != null) LevelHud.Instance.SetMapTitle(on ? "OVERVIEW MAP  (ceilings hidden)  -  M to close" : "");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) Toggle();
            if (_player == null) { var cc = FindFirstObjectByType<CharacterController>(); if (cc != null) _player = cc.transform; }
            if (IsOn && _player != null)
                _playerMarker.position = new Vector3(_player.position.x, _levelBounds.max.y + 22f, _player.position.z);
            if (IsOn)
                for (int i = 0; i < _labels.Count; i++)
                {
                    var sp = _cam.WorldToScreenPoint(_labelWorld[i]);
                    _labels[i].GetComponent<RectTransform>().position = new Vector3(sp.x, sp.y, 0f);
                }
        }

        public Camera MapCamera => _cam;
        public Bounds LevelBounds => _levelBounds;
    }
}
