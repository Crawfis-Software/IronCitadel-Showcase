using UnityEngine;
using UnityEngine.AI;

namespace IronCitadelV2
{
    /// <summary>Loads the level's baked NavMesh (a NavMeshData asset the builder writes beside the scene) while this
    /// component is enabled, in edit mode and in play mode, and unloads it when disabled. The bake used the walker's
    /// size (radius 0.35, height 1.8, climb 0.3, slope 45) on agent type 0 with every door open.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class IronCitadelV2NavMesh : MonoBehaviour
    {
        public NavMeshData data;
        NavMeshDataInstance _inst;

        void OnEnable()
        {
            if (data != null && !_inst.valid) _inst = NavMesh.AddNavMeshData(data);
        }

        void OnDisable()
        {
            if (_inst.valid) _inst.Remove();
            _inst = default;
        }

        /// <summary>Re-add after the data was replaced (the builder calls this).</summary>
        public void Reload()
        {
            OnDisable();
            OnEnable();
        }

        public bool Loaded => _inst.valid;
    }
}
