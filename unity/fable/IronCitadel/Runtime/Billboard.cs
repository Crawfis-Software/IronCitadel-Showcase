using UnityEngine;

namespace IronCitadel
{
    /// <summary>Keeps a marker label facing the active camera.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var d = transform.position - cam.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
