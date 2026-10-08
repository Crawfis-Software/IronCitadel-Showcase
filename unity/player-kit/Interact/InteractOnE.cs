using UnityEngine;
using UnityEngine.InputSystem;

namespace IronCitadel.Interact
{
    /// <summary>
    /// The E press for Unity's Starter Assets first-person controller, which has no "use" key of its own.
    /// E casts a 3 m ray from the main camera and calls SendMessageUpwards("Interact") on the nearest
    /// non-trigger collider that is not part of the player. A secret door opens on E when it has a solid
    /// collider and a <c>void Interact()</c> method on the same GameObject or a parent.
    /// Sits on the PlayerCapsule of IronCitadelPlayer.prefab.
    /// </summary>
    public class InteractOnE : MonoBehaviour
    {
        public const string InteractMessage = "Interact";

        [Tooltip("How far the E ray reaches, in metres.")]
        public float interactRange = 3f;
        public LayerMask interactMask = Physics.DefaultRaycastLayers;

        readonly RaycastHit[] _hits = new RaycastHit[16];

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) Interact();
        }

        public void Interact()
        {
            Camera cam = Camera.main;
            Transform origin = cam != null ? cam.transform : transform;
            var ray = new Ray(origin.position, origin.forward);
            int n = Physics.RaycastNonAlloc(ray, _hits, interactRange, interactMask, QueryTriggerInteraction.Ignore);
            int best = -1;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform)) continue;
                if (best < 0 || _hits[i].distance < _hits[best].distance) best = i;
            }
            if (best >= 0)
                _hits[best].collider.SendMessageUpwards(InteractMessage, SendMessageOptions.DontRequireReceiver);
        }
    }
}
