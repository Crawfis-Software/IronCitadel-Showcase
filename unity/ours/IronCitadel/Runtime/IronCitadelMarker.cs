using UnityEngine;

namespace IronCitadel.Rooms
{
    /// <summary>A static level marker (defender, pickup, start, goal, feature) so a validator or a scorer
    /// can find what level.json asked for without parsing names. Nothing moves and there is no combat.</summary>
    public class IronCitadelMarker : MonoBehaviour
    {
        public enum Kind { Defender, Pickup, PlayerStart, Goal, Feature }

        public Kind kind;
        [Tooltip("level.json room id (entry_hall, armory, ...) or door id for a feature.")]
        public string room;
        [Tooltip("Free text from level.json (archer at the portcullis, warlord, the hoard, ...).")]
        public string note;
    }
}
