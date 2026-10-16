using UnityEngine;

namespace IronCitadel
{
    /// <summary>Announces the throne when the player's CharacterController enters the trigger around it.</summary>
    [RequireComponent(typeof(Collider))]
    public class ThroneTrigger : MonoBehaviour
    {
        public string message = "You have reached the throne of the Iron Citadel";
        public static bool Reached;
        public static float ReachedAt = -1f;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterController>() == null) return;
            if (!Reached) { Reached = true; ReachedAt = Time.timeSinceLevelLoad; Debug.Log("[IronCitadel] " + message + " at " + ReachedAt.ToString("F1") + " s"); }
            var hud = FindFirstObjectByType<LevelHud>();
            if (hud != null) hud.Announce(message);
        }
    }
}
