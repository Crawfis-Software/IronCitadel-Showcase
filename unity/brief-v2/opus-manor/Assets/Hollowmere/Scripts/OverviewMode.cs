using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowmere
{
    /// <summary>
    /// M toggles a top-down orthographic overview of the whole level. The overview camera leaves out the
    /// Ceiling layer, so every room is open to view; fog is lifted and the ambient light raised while it is up.
    /// </summary>
    public class OverviewMode : MonoBehaviour
    {
        public Camera overviewCamera;
        public Vector3 levelMin;
        public Vector3 levelMax;
        public Color overviewAmbient = new Color(0.5f, 0.48f, 0.5f);

        public static bool Active { get; private set; }
        public static OverviewMode Instance { get; private set; }

        bool _fog;
        Color _ambient;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; Instance = null; }

        void Awake()
        {
            Instance = this;
            if (overviewCamera != null) overviewCamera.enabled = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) Toggle();
            if (Active) Fit();
        }

        public void Toggle() => SetActive(!Active);

        public void SetActive(bool on)
        {
            if (overviewCamera == null || on == Active) return;
            Active = on;
            if (on)
            {
                _fog = RenderSettings.fog;
                _ambient = RenderSettings.ambientLight;
                RenderSettings.fog = false;
                RenderSettings.ambientLight = overviewAmbient;
                Fit();
            }
            else
            {
                RenderSettings.fog = _fog;
                RenderSettings.ambientLight = _ambient;
            }
            overviewCamera.enabled = on;
        }

        /// <summary>Frames the whole level for the current aspect ratio.</summary>
        public void Fit()
        {
            if (overviewCamera == null) return;
            Vector3 c = (levelMin + levelMax) * 0.5f;
            float halfX = (levelMax.x - levelMin.x) * 0.5f + 2f;
            float halfZ = (levelMax.z - levelMin.z) * 0.5f + 2f;
            float aspect = overviewCamera.aspect > 0.01f ? overviewCamera.aspect : 16f / 9f;
            overviewCamera.orthographic = true;
            overviewCamera.orthographicSize = Mathf.Max(halfZ, halfX / aspect);
            overviewCamera.transform.SetPositionAndRotation(new Vector3(c.x, levelMax.y + 30f, c.z), Quaternion.Euler(90f, 0f, 0f));
            overviewCamera.nearClipPlane = 1f;
            overviewCamera.farClipPlane = levelMax.y - levelMin.y + 80f;
        }
    }
}
