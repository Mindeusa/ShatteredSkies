using UnityEngine;

namespace ShatteredSkies.Clouds
{
    public sealed class CloudDrift : MonoBehaviour
    {
        [Min(0)] public float speed = 0.3f;
        [Min(0.01f)] public float travel = 12f;
        private Vector3 origin;
        private float phase;

        private void OnEnable()
        {
            origin = transform.localPosition;
            phase = 0f;
        }

        private void Update()
        {
            phase += Time.deltaTime * speed / Mathf.Max(0.01f, travel);
            transform.localPosition = origin + Vector3.right * (travel * Mathf.Sin(phase));
        }

        private void OnDisable() => transform.localPosition = origin;
    }
}
