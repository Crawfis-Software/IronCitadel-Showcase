using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// A defender or pickup marker. Nothing moves; the marker's position is its feet, its forward its facing.
    /// kind: ranged | melee | warlord | best gear | hoard.
    /// </summary>
    public class Marker : MonoBehaviour
    {
        public string kind;
        public string roomName;
        public bool IsEnemy => kind == "ranged" || kind == "melee" || kind == "warlord";
    }
}
