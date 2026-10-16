using UnityEngine;

namespace IronCitadel.Level
{
    /// <summary>
    /// A defender or pickup marker. Its transform sits on the floor where it stands (feet, or the base of a pickup).
    /// markerKind is "enemy" or "pickup"; enemyKind is "ranged", "melee" or "warlord"; pickupKind is "best gear" or "hoard".
    /// </summary>
    public class LevelMarker : MonoBehaviour
    {
        public string markerKind;
        public string enemyKind;
        public string pickupKind;
        /// <summary>The room the builder put it in. The map export checks this against where it stands.</summary>
        public string room;
    }
}
