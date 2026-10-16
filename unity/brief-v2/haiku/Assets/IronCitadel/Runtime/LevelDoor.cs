using UnityEngine;

namespace IronCitadel.Level
{
    /// <summary>
    /// An opening between two rooms. The root sits at the middle of the opening on the floor. A shut or hidden door
    /// has a blocking collider in a child named "Blocker" and a leaf on a hinge pivot (or a stone slab that sinks).
    /// E on it (InteractOnE sends "Interact" up to here) opens it. An open doorway has no leaf and nothing to open.
    /// </summary>
    public class LevelDoor : MonoBehaviour
    {
        public string roomA, roomB;
        /// <summary>open (a doorway), shut (a door the player opens) or hidden (a secret way in).</summary>
        public string state;
        /// <summary>Clear width of the opening, metres.</summary>
        public float width;
        /// <summary>The part that moves: the hinge pivot of a door, or the stone slab of a hidden door. Null for a doorway.</summary>
        public Transform leaf;
        public bool slidesDown;
        public bool opened;

        const float SinkDistance = 6f;

        public void Interact()
        {
            if (opened || leaf == null) return;
            opened = true;
            var blocker = transform.Find("Blocker");
            if (blocker != null) blocker.gameObject.SetActive(false);
            foreach (var c in leaf.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            if (slidesDown) leaf.position += Vector3.down * SinkDistance;
            else leaf.Rotate(0f, 90f, 0f, Space.Self);
        }
    }
}
