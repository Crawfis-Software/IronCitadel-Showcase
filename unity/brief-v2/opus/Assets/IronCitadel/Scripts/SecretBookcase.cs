using System.Collections;
using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// A bookcase that stands over a doorway like any other. Interact() (the player's E) slides it
    /// along the wall, opening the way behind it; a second E slides it back.
    /// </summary>
    public class SecretBookcase : MonoBehaviour
    {
        [Tooltip("Where it slides to, in its parent's space, relative to where it stands.")]
        public Vector3 slide = new Vector3(0f, 0f, -2.6f);
        public float duration = 1.6f;
        public bool isOpen;
        Vector3 _closed;
        bool _busy;

        void Awake() { _closed = transform.localPosition; }

        public void Interact()
        {
            if (_busy) return;
            StartCoroutine(Move(isOpen ? _closed : _closed + slide));
            isOpen = !isOpen;
        }

        IEnumerator Move(Vector3 target)
        {
            _busy = true;
            Vector3 from = transform.localPosition;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                transform.localPosition = Vector3.Lerp(from, target, Mathf.SmoothStep(0, 1, t / duration));
                yield return null;
            }
            transform.localPosition = target;
            _busy = false;
        }
    }
}
