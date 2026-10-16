using UnityEngine;

namespace Hollowmere
{
    /// <summary>
    /// The master's chair. When the player stands on the stage within <see cref="radius"/> of the chair,
    /// the HUD announces it.
    /// </summary>
    public class ChairGoal : MonoBehaviour
    {
        public float radius = 1.7f;
        public static bool Reached { get; private set; }
        public static float ReachedAt { get; private set; }

        CharacterController _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Reached = false; ReachedAt = 0f; }

        void Awake() { Reached = false; ReachedAt = 0f; }

        void Update()
        {
            if (Reached) return;
            if (_player == null) _player = FindAnyObjectByType<CharacterController>();
            if (_player == null) return;
            Vector3 p = _player.transform.position;
            Vector3 d = p - transform.position; d.y = 0f;
            if (d.magnitude <= radius && p.y >= transform.position.y - 0.35f)
            {
                Reached = true;
                ReachedAt = LevelHud.Elapsed;
                Debug.Log($"[Hollowmere] The master's chair reached at {LevelHud.FormatTime(ReachedAt)}");
            }
        }
    }
}
