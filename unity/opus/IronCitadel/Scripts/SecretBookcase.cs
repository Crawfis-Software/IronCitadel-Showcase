using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// A bookcase on a hinge that swings aside when the player presses E on it (InteractOnE sends "Interact"
    /// up from the collider it hits). Sits on the hinge object; the bookcase and its collider are children.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SecretBookcase : MonoBehaviour
    {
        [Tooltip("Degrees about the hinge's vertical axis when fully open.")]
        public float openYaw = -90f;
        public float seconds = 1.8f;
        public bool startOpen;

        Quaternion _closed, _open;
        float _t;
        bool _target;
        Rigidbody _body;

        public bool IsOpen => _target;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _closed = transform.localRotation;
            _open = _closed * Quaternion.Euler(0f, openYaw, 0f);
            if (startOpen) { _target = true; _t = 1f; transform.localRotation = _open; }
        }

        void Interact()
        {
            _target = !_target;
            if (CitadelHUD.Instance != null && _target)
                CitadelHUD.Instance.Announce("The bookcase swings aside on a hidden hinge.", 4f);
        }

        void FixedUpdate()
        {
            float goal = _target ? 1f : 0f;
            if (Mathf.Approximately(_t, goal)) return;
            _t = Mathf.MoveTowards(_t, goal, Time.fixedDeltaTime / seconds);
            float e = Mathf.SmoothStep(0f, 1f, _t);
            var rot = Quaternion.Slerp(_closed, _open, e);
            var world = transform.parent != null ? transform.parent.rotation * rot : rot;
            _body.MoveRotation(world);
        }
    }
}
