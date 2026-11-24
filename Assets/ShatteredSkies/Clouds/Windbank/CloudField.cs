using UnityEngine;

namespace ShatteredSkies.Clouds
{
    public sealed class CloudField : MonoBehaviour
    {
        [Tooltip("Cloud prefab used for every bank.")] public GameObject bankPrefab;
        [Tooltip("Leave empty to follow the active Main Camera.")] public Transform follow;
        public int seed = 1241;
        [Min(20)] public float spacing = 68;
        public Vector2 altitudeRange = new Vector2(-28, 10);
        public Vector2 sizeRange = new Vector2(.55f, 1.55f);
        private const int Width = 5;
        private readonly Transform[] banks = new Transform[Width * Width];
        private readonly Vector2Int[] cells = new Vector2Int[Width * Width];
        private Camera playerCamera;
        private Vector2Int center;
        private bool dirty = true;

        private void OnValidate() => dirty = true;

        private void LateUpdate()
        {
            if (!bankPrefab) return;
            var target = follow;
            if (!target)
            {
                if (!playerCamera || !playerCamera.isActiveAndEnabled) playerCamera = Camera.main;
                if (!playerCamera) return;
                target = playerCamera.transform;
            }
            float step = Mathf.Max(20, spacing);
            var local = transform.InverseTransformPoint(target.position);
            var next = new Vector2Int(Mathf.RoundToInt(local.x / step), Mathf.RoundToInt(local.z / step));
            if (!dirty && center == next) return;
            center = next;
            for (int z = next.y - 2; z <= next.y + 2; z++)
                for (int x = next.x - 2; x <= next.x + 2; x++)
                    PlaceCell(new Vector2Int(x,z), step);
            dirty = false;
        }

        private static int Wrap(int value) => (value % Width + Width) % Width;

        private void PlaceCell(Vector2Int cell, float step)
        {
            int index = Wrap(cell.y) * Width + Wrap(cell.x);
            var bank = banks[index];
            if (bank && !dirty && cells[index] == cell) return;
            if (!bank) bank = banks[index] = Instantiate(bankPrefab, transform, false).transform;
            cells[index] = cell;
            uint state = unchecked((uint)seed ^ (uint)cell.x * 0x85EBCA6Bu ^ (uint)cell.y * 0xC2B2AE35u);
            float x = (cell.x + Mathf.Lerp(-.33f,.33f,Sample(ref state))) * step;
            float z = (cell.y + Mathf.Lerp(-.33f,.33f,Sample(ref state))) * step;
            float y = Mathf.Lerp(altitudeRange.x, altitudeRange.y, Sample(ref state));
            bank.localPosition = new Vector3(x,y,z);
            bank.localRotation = Quaternion.Euler(0, Mathf.Lerp(-35,35,Sample(ref state)), 0);
            float size = Mathf.Max(.1f, Mathf.Lerp(sizeRange.x,sizeRange.y,Sample(ref state)));
            bank.localScale = new Vector3(size,size * Mathf.Lerp(.67f,1.13f,Sample(ref state)),size);
            var drift = bank.GetComponentInChildren<CloudDrift>();
            if (drift) drift.speed = Mathf.Lerp(.18f,.42f,Sample(ref state));
        }

        private static float Sample(ref uint state)
        {
            unchecked
            {
                uint h = (state += 0x9E3779B9u);
                h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
                h = (h ^ (h >> 13)) * 0xC2B2AE35u;
                return ((h ^ (h >> 16)) >> 8) / 16777216f;
            }
        }
    }
}
