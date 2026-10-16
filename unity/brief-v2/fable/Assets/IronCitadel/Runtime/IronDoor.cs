using UnityEngine;

namespace IronCitadel
{
    /// <summary>An opening between two rooms. The transform sits at the middle of the opening at floor level; forward points across the wall.</summary>
    public class IronDoor : MonoBehaviour
    {
        public string roomA;
        public string roomB;
        public string state;        // open, shut, hidden
        public float nominalWidth;  // what the piece was built to give; the map uses the measured width
        public string piece;        // the Synty wall piece used
    }
}
