using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// Marks one opening between two rooms. Sits at the middle of the opening at floor level, its forward
    /// axis across the opening. The leaf (a SwingDoor or SecretBookcase), if any, is referenced so checks
    /// can open it or set it aside while they scan for openings.
    /// </summary>
    public class DoorLink : MonoBehaviour
    {
        public string roomA;
        public string roomB;
        [Tooltip("open | shut | hidden")]
        public string state = "open";
        public GameObject leaf;
    }
}
