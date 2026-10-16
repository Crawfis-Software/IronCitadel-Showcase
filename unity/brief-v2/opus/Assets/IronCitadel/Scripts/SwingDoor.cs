using System.Collections;
using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// A hinged door. The leaf is a child of this transform (the hinge). Interact() (the player's E) swings
    /// it open, away from whoever opened it, or shuts it again.
    /// </summary>
    public class SwingDoor : MonoBehaviour
    {
        public float openAngle = 95f;
        public float duration = 0.7f;
        [Tooltip("+1 if the leaf extends along this hinge's +x, -1 if along -x.")]
        public float leafSide = 1f;
        public bool isOpen;
        Quaternion _closed;
        bool _busy;

        void Awake() { _closed = transform.localRotation; }

        public void Interact()
        {
            if (_busy) return;
            float sign = 1f;
            var cam = Camera.main;
            if (cam != null)
            {
                // Swing away from the person opening it. The hinge's forward is the door's normal; a positive
                // turn about y moves a leaf lying along +x towards -z, and one lying along -x towards +z.
                Vector3 toViewer = cam.transform.position - transform.position;
                bool viewerInFront = Vector3.Dot(toViewer, transform.forward) > 0f;
                sign = viewerInFront == (leafSide > 0f) ? 1f : -1f;
            }
            StartCoroutine(Swing(isOpen ? _closed : _closed * Quaternion.Euler(0f, openAngle * sign, 0f)));
            isOpen = !isOpen;
        }

        IEnumerator Swing(Quaternion target)
        {
            _busy = true;
            Quaternion from = transform.localRotation;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                transform.localRotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0, 1, t / duration));
                yield return null;
            }
            transform.localRotation = target;
            _busy = false;
        }
    }
}
