using UnityEngine;

namespace IronCitadel.Rooms
{
    /// <summary>The vault's secret door: a bookcase that fills the doorway and, when used, swings aside
    /// about <see cref="hinge"/> over <see cref="duration"/> seconds, then stops blocking.
    ///
    /// The walker's contract: on E it raycasts 3 m from the camera and calls
    /// <c>hit.collider.SendMessageUpwards("Interact", SendMessageOptions.DontRequireReceiver)</c>, so this
    /// component sits on an ancestor of every collider the leaf carries.</summary>
    [DisallowMultipleComponent]
    public class SecretBookcaseDoor : MonoBehaviour
    {
        [Tooltip("Pivot the leaf turns about (local Y). The bookcase and its colliders are children of it.")]
        public Transform hinge;
        [Tooltip("Degrees about the hinge's local Y; the sign picks which way it swings.")]
        public float openAngle = 95f;
        public float duration = 1f;
        [Tooltip("When true the leaf's colliders are switched off once the swing ends, so nothing of it can block the way.")]
        public bool disableCollidersWhenOpen = true;

        public bool IsOpen { get; private set; }
        public bool IsMoving => _t >= 0f;

        Quaternion _closed;
        float _t = -1f;

        void Awake()
        {
            if (hinge == null) hinge = transform;
            _closed = hinge.localRotation;
        }

        /// <summary>Called by the walker through SendMessageUpwards.</summary>
        public void Interact()
        {
            if (IsOpen || IsMoving) return;
            _t = 0f;
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t));
            hinge.localRotation = _closed * Quaternion.Euler(0f, openAngle * k, 0f);
            if (_t < 1f) return;
            _t = -1f;
            IsOpen = true;
            if (disableCollidersWhenOpen)
                foreach (var c in hinge.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }
    }
}
