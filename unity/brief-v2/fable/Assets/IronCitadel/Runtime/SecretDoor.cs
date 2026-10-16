using UnityEngine;

namespace IronCitadel
{
    /// <summary>A bricked-up section of wall that sinks into the floor when the player uses it (E). Solid until then.</summary>
    public class SecretDoor : MonoBehaviour
    {
        public float slideDistance = 2.4f;
        public float duration = 2.0f;
        public bool open;
        float _t; Vector3 _closedPos; bool _init;

        void Awake() { _closedPos = transform.localPosition; _init = true; }

        public void Interact() { open = true; }

        public bool FullyOpen => open && _t >= 0.999f;

        void Update()
        {
            if (!_init) return;
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.01f, duration));
            transform.localPosition = _closedPos + Vector3.down * (slideDistance * Mathf.SmoothStep(0f, 1f, _t));
        }
    }
}
