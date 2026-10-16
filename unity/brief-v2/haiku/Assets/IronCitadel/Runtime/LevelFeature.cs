using UnityEngine;

namespace IronCitadel.Level
{
    /// <summary>
    /// A named feature of the level, read by the HUD and the map export. feature is one of:
    /// "gallery" (its deck tiles are children named "Deck*", its rail pieces "Rail*"),
    /// "stage" (its tiles are children named "Stage*"), "throne" (the transform is the throne's foot),
    /// "stair" (a flight that rises to a gallery).
    /// </summary>
    public class LevelFeature : MonoBehaviour
    {
        public string feature;
    }
}
