using UnityEngine;

namespace Hollowmere
{
    /// <summary>A defender marker. Nothing moves; forward is the way it faces.</summary>
    public class EnemyMarker : MonoBehaviour
    {
        [Tooltip("ranged | melee | master")]
        public string kind;
        public string room;
        public string note;

        /// <summary>Eye point used for sight-line checks.</summary>
        public Vector3 Eye => transform.position + Vector3.up * 1.65f;
    }
}
