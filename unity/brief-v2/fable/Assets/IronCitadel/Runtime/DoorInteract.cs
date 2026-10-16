using UnityEngine;

namespace IronCitadel
{
    /// <summary>A hinged door leaf. Interact() (from the E key) swings it open or shut about this object's up axis.</summary>
    public class DoorInteract : MonoBehaviour
    {
        public float openAngle = 95f;
        public float duration = 0.8f;
        public bool open;
        float _t; Quaternion _closedRot;
        bool _init;

        void Awake() { _closedRot = transform.localRotation; _init = true; _t = open ? 1f : 0f; }

        public void Interact()
        {
            open = !open;
        }

        public bool FullyOpen => open && _t >= 0.999f;

        void Update()
        {
            if (!_init) return;
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.01f, duration));
            float a = Mathf.SmoothStep(0f, openAngle, _t);
            transform.localRotation = _closedRot * Quaternion.Euler(0f, a, 0f);
        }
    }
}
