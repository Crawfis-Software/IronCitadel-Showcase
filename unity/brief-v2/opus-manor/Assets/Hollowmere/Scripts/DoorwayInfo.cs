using UnityEngine;

namespace Hollowmere
{
    /// <summary>
    /// One opening between two rooms. Sits at the middle of the opening, at the bottom of the opening;
    /// <see cref="axis"/> points through it from roomA to roomB. The map exporter measures the clear width
    /// from the jamb colliders around it.
    /// </summary>
    public class DoorwayInfo : MonoBehaviour
    {
        public string roomA;
        public string roomB;
        [Tooltip("open | shut | hidden")]
        public string state;
        public float designWidth;
        public float designHeight;
        public Vector3 axis = Vector3.forward;
        public SwingDoor door;

        public bool Connects(string room) => roomA == room || roomB == room;
        public string Other(string room) => roomA == room ? roomB : roomA;
    }
}
