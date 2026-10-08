using UnityEngine;

namespace IronCitadel
{
    /// <summary>Trigger volume at the throne; tells the HUD when the player arrives.</summary>
    public class ThroneGoal : MonoBehaviour
    {
        public LevelHud hud;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterController>() == null) return;
            if (hud == null) hud = FindFirstObjectByType<LevelHud>();
            if (hud != null) hud.ThroneReached();
        }
    }
}
