using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronCitadel.Play
{
    /// <summary>M toggles a top-down overview of the whole level with the ceilings hidden. It swaps the
    /// player's camera for an orthographic one above the level, hides every ceiling piece (a room prefab's
    /// "Ceiling" group and its "ceiling:" / "roof:" pieces), turns on a soft overhead light so the rooms read
    /// at night, and marks the player with a dot and a heading tick. M again puts everything back.</summary>
    public class IronCitadelOverview : MonoBehaviour
    {
        public Key toggleKey = Key.M;
        [Tooltip("The orthographic camera above the level; kept disabled until M.")]
        public Camera overviewCamera;
        [Tooltip("A soft overhead light used only in the overview; kept disabled until M.")]
        public Light overviewLight;
        [Tooltip("Ceilings are looked for under this root (the IronCitadel root).")]
        public Transform levelRoot;
        public Transform player;
        [Tooltip("World x, z of the level's centre and its size in metres (from level.json).")]
        public Vector2 centreXZ = new Vector2(90f, -90f);
        public Vector2 sizeXZ = new Vector2(180f, 180f);
        public float margin = 4f;

        readonly List<GameObject> ceilings = new List<GameObject>();
        Camera hiddenCamera;
        Texture2D dot;
        public bool IsOn { get; private set; }

        public static bool IsCeiling(Transform t)
        {
            string n = t.name;
            return n == "Ceiling" || n.StartsWith("ceiling:") || n.StartsWith("roof:");
        }

        void Start()
        {
            if (levelRoot != null)
                foreach (var t in levelRoot.GetComponentsInChildren<Transform>(true))
                    if (t.gameObject.activeSelf && IsCeiling(t)) ceilings.Add(t.gameObject);
            if (overviewCamera != null) overviewCamera.enabled = false;
            if (overviewLight != null) overviewLight.enabled = false;
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
            dot = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            dot.SetPixel(0, 0, Color.white);
            dot.Apply();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame) Toggle();
            if (IsOn) Fit();
        }

        public void Toggle() => SetOverview(!IsOn);

        public void SetOverview(bool on)
        {
            if (on == IsOn || overviewCamera == null) return;
            IsOn = on;
            foreach (var c in ceilings) if (c != null) c.SetActive(!on);
            if (overviewLight != null) overviewLight.enabled = on;
            if (on)
            {
                hiddenCamera = Camera.main;
                if (hiddenCamera != null && hiddenCamera != overviewCamera) hiddenCamera.enabled = false;
                Fit();
                overviewCamera.enabled = true;
            }
            else
            {
                overviewCamera.enabled = false;
                if (hiddenCamera != null) hiddenCamera.enabled = true;
            }
        }

        /// <summary>Whole level in view, north up, at any window shape.</summary>
        void Fit()
        {
            float aspect = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));
            overviewCamera.orthographic = true;
            overviewCamera.orthographicSize = Mathf.Max(sizeXZ.y / 2f, sizeXZ.x / 2f / aspect) + margin;
            var p = overviewCamera.transform.position;
            overviewCamera.transform.SetPositionAndRotation(new Vector3(centreXZ.x, p.y, centreXZ.y), Quaternion.Euler(90f, 0f, 0f));
        }

        void OnGUI()
        {
            if (!IsOn || player == null || overviewCamera == null) return;
            float u = Mathf.Max(1f, Screen.height / 720f);
            var sp = overviewCamera.WorldToScreenPoint(player.position);
            var fwd = player.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            var ahead = overviewCamera.WorldToScreenPoint(player.position + fwd.normalized * 3f);
            var at = new Vector2(sp.x, Screen.height - sp.y);
            var tip = new Vector2(ahead.x, Screen.height - ahead.y);
            var prev = GUI.color;
            // heading tick: small dots from the player toward where it faces
            GUI.color = new Color(1f, 0.9f, 0.2f);
            for (int i = 1; i <= 4; i++)
            {
                var q = Vector2.Lerp(at, tip, i / 4f);
                float s = 4f * u;
                GUI.DrawTexture(new Rect(q.x - s / 2f, q.y - s / 2f, s, s), dot);
            }
            GUI.color = Color.black;
            float r = 13f * u;
            GUI.DrawTexture(new Rect(at.x - r / 2f, at.y - r / 2f, r, r), dot);
            GUI.color = new Color(1f, 0.25f, 0.2f);
            r = 9f * u;
            GUI.DrawTexture(new Rect(at.x - r / 2f, at.y - r / 2f, r, r), dot);
            GUI.color = prev;
        }

        void OnDestroy()
        {
            if (IsOn) SetOverview(false);
            if (dot != null) Destroy(dot);
        }
    }
}
