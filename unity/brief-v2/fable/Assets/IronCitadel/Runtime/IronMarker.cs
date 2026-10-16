using UnityEngine;

namespace IronCitadel
{
    /// <summary>A defender or pickup marker. kind: ranged, melee, warlord, gear (best gear) or hoard.</summary>
    public class IronMarker : MonoBehaviour
    {
        public string kind;
        public string room;
        public bool IsEnemy => kind == "ranged" || kind == "melee" || kind == "warlord";
    }
}
