using UnityEngine;

namespace IronCitadel.Level
{
    /// <summary>
    /// The root of one room of the Iron Citadel. Its floor tiles are children named "Floor*", its ceiling tiles
    /// "Ceiling*", and everything furnished in it sits under the same root, so the room a point is in is the
    /// LevelRoom above the floor under it.
    /// </summary>
    public class LevelRoom : MonoBehaviour
    {
        public string roomName;
        public string kind;
        public string state;
        public float ceilingY;
    }
}
