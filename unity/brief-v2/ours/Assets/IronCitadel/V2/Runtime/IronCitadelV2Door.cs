using UnityEngine;

namespace IronCitadelV2
{
    /// <summary>A door of the v2 level that opens with E: a shut door (one leaf or a pair) or the vault's hidden
    /// bookcase. Each leaf hangs from a hinge transform; <see cref="Interact"/> swings every hinge by its angle about
    /// its local Y over <see cref="duration"/> seconds. The walker's InteractOnE ray calls
    /// <c>SendMessageUpwards("Interact")</c> on the collider it hits, so this component sits on an ancestor of every
    /// collider the leaves carry.
    ///
    /// The NavMesh is baked with every door open, so a shut door blocks the walker only through its colliders. A
    /// shut door's leaves keep their colliders once open (the bake saw them in that pose); the bookcase drops its own
    /// (<see cref="disableCollidersWhenOpen"/>), as v1's did.</summary>
    [DisallowMultipleComponent]
    public class IronCitadelV2Door : MonoBehaviour
    {
        [Tooltip("The design's door id (level.json doors[].id).")]
        public string doorId;
        [Tooltip("shut or hidden.")]
        public string kind = "shut";
        [Tooltip("The two rooms the door joins.")]
        public string roomA, roomB;
        [Tooltip("Pivots the leaves turn about (local Y); each leaf and its colliders are children of one.")]
        public Transform[] hinges = new Transform[0];
        [Tooltip("Degrees about each hinge's local Y; the sign picks the way it swings.")]
        public float[] openAngles = new float[0];
        public float duration = 1f;
        [Tooltip("When true the leaves' colliders are switched off once the swing ends.")]
        public bool disableCollidersWhenOpen;
        [Tooltip("When true the HUD offers 'E: open' while the player looks at it (never for the hidden door).")]
        public bool showPrompt = true;

        public bool IsOpen { get; private set; }
        public bool IsMoving => _t >= 0f;

        Quaternion[] _closed;
        float _t = -1f;

        void Awake() => Capture();

        void Capture()
        {
            _closed = new Quaternion[hinges.Length];
            for (int i = 0; i < hinges.Length; i++) _closed[i] = hinges[i] != null ? hinges[i].localRotation : Quaternion.identity;
        }

        /// <summary>Called by the walker through SendMessageUpwards; opens once.</summary>
        public void Interact()
        {
            if (IsOpen || IsMoving) return;
            if (_closed == null || _closed.Length != hinges.Length) Capture();
            _t = 0f;
            Debug.Log($"[IronCitadelV2] {(kind == "hidden" ? "hidden door" : "door")} {doorId} opens");
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime / Mathf.Max(0.01f, duration);
            Pose(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t)));
            if (_t < 1f) return;
            _t = -1f;
            IsOpen = true;
            if (disableCollidersWhenOpen)
                foreach (var h in hinges)
                    if (h != null)
                        foreach (var c in h.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        void Pose(float k)
        {
            for (int i = 0; i < hinges.Length; i++)
            {
                if (hinges[i] == null) continue;
                float a = i < openAngles.Length ? openAngles[i] : 90f;
                hinges[i].localRotation = _closed[i] * Quaternion.Euler(0f, a * k, 0f);
            }
        }
    }
}
