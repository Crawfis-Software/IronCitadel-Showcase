using UnityEngine;

namespace IronCitadel.Play
{
    /// <summary>What the HUD asks of a level: the name of the room under a world point. LevelMap answers from
    /// level.json's 45 m grid (the rooms level); PatternRegionMap answers from the pattern-tile level's regions.</summary>
    public interface IRoomNames
    {
        string RoomNameAt(Vector3 p, out string id);
    }
}
