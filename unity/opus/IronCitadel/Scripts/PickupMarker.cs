using UnityEngine;

namespace IronCitadel
{
    /// <summary>A static pickup marker: when the player comes close it is announced and its glow goes out.</summary>
    public class PickupMarker : MonoBehaviour
    {
        public string label = "the armory's best gear";
        public float radius = 2.2f;
        public GameObject[] hideWhenTaken;

        static Transform _player;
        bool _taken;

        void Update()
        {
            if (_taken) return;
            if (_player == null)
            {
                var go = GameObject.Find("PlayerCapsule");
                if (go == null) return;
                _player = go.transform;
            }
            var d = _player.position - transform.position;
            d.y *= 0.5f;
            if (d.magnitude > radius) return;
            _taken = true;
            if (CitadelHUD.Instance != null) CitadelHUD.Instance.Announce("Taken: " + label, 5f);
            if (hideWhenTaken != null) foreach (var g in hideWhenTaken) if (g != null) g.SetActive(false);
        }
    }
}
