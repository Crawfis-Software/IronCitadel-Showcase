using UnityEngine;

namespace IronCitadel
{
    /// <summary>Cheap firelight flicker on a point light.</summary>
    public class FlickerLight : MonoBehaviour
    {
        public float amount = 0.15f;
        public float speed = 7f;
        Light _light;
        float _base;
        float _seed;

        void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light != null ? _light.intensity : 0f;
            _seed = Random.value * 100f;
        }

        void Update()
        {
            if (_light == null) return;
            float n = Mathf.PerlinNoise(_seed, Time.time * speed) - 0.5f;
            _light.intensity = _base * (1f + n * 2f * amount);
        }
    }
}
