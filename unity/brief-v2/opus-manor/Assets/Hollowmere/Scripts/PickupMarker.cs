using UnityEngine;

namespace Hollowmere
{
    /// <summary>A pickup marker.</summary>
    public class PickupMarker : MonoBehaviour
    {
        [Tooltip("best gear | hoard")]
        public string kind;
        public string room;
    }
}
