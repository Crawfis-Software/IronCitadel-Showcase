using System.Collections;
using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// A bookcase that swings aside. Sits on a hinge object at the bookcase's edge; the bookcase mesh and its
    /// solid collider are children, so the player's E ray reaches Interact() through SendMessageUpwards.
    /// </summary>
    public class SecretDoor : MonoBehaviour
    {
        public float openAngle = -110f;
        public float duration = 1.4f;
        public bool isOpen;
        Coroutine _co;
        float _angle;

        public void Interact()
        {
            isOpen = !isOpen;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Swing(isOpen ? openAngle : 0f));
        }

        IEnumerator Swing(float target)
        {
            float from = _angle;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                _angle = Mathf.Lerp(from, target, k);
                transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
                yield return null;
            }
            _angle = target;
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }
    }
}
