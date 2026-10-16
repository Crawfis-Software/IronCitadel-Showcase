using UnityEngine;

namespace Hollowmere
{
    /// <summary>
    /// A hinged door (one or two leaves) or a hinged bookcase. The player's E ray reaches it through a leaf's
    /// collider and SendMessageUpwards("Interact"), which toggles it. Each leaf swings about its hinge's local
    /// Y axis to its open angle.
    /// </summary>
    public class SwingDoor : MonoBehaviour
    {
        public Transform[] hinges;
        [Tooltip("Signed open angle per hinge, degrees about the hinge's Y axis.")]
        public float[] openAngles;
        [Tooltip("Degrees per second.")]
        public float speed = 150f;
        public bool isOpen;
        public bool locked;
        public bool hidden;

        float _t; // 0 shut .. 1 open

        public bool IsMoving => Mathf.Abs(_t - (isOpen ? 1f : 0f)) > 0.001f;
        public bool IsFullyOpen => isOpen && !IsMoving;

        float MaxAngle
        {
            get
            {
                float m = 1f;
                if (openAngles != null) foreach (var a in openAngles) m = Mathf.Max(m, Mathf.Abs(a));
                return m;
            }
        }

        void Awake()
        {
            _t = isOpen ? 1f : 0f;
            Apply();
        }

        public void Interact()
        {
            if (locked) { LevelHud.Toast("It will not open."); return; }
            isOpen = !isOpen;
            if (hidden && isOpen) LevelHud.Toast("Something gives behind the books...");
        }

        public void SetOpenImmediate(bool open)
        {
            isOpen = open;
            _t = open ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, speed / MaxAngle * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            if (hinges == null) return;
            float e = Mathf.SmoothStep(0f, 1f, _t);
            for (int i = 0; i < hinges.Length; i++)
                if (hinges[i] != null) hinges[i].localRotation = Quaternion.Euler(0f, openAngles[i] * e, 0f);
        }
    }
}
