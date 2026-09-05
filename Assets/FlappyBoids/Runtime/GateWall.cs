using UnityEngine;

namespace FlappyBoids
{
    [DisallowMultipleComponent]
    public sealed class GateWall : MonoBehaviour
    {
        public const float CorridorHalfWidth = 9.5f;
        public const float CorridorHeight = 12f;
        public const float Thickness = 1.1f;

        [SerializeField, Min(1f)] private float _holeDiameter = 6.1f;

        public float Z => transform.position.z;
        public Vector2 HoleCenter => new Vector2(transform.position.x, transform.position.y);
        public Vector2 HoleSize => Vector2.one * _holeDiameter;
        public float Diameter => _holeDiameter;
        public float Radius => _holeDiameter * 0.5f;
        public bool Passed { get; set; }

        public void SetHoleDiameter(float diameter)
        {
            float nextDiameter = Mathf.Max(1f, diameter);
            if (Mathf.Approximately(_holeDiameter, nextDiameter)) return;
            _holeDiameter = nextDiameter;
            MarchingCubesGateVisual visual = GetComponent<MarchingCubesGateVisual>();
            if (visual != null) visual.Rebuild();
        }

        public bool Intersects(Vector3 position, float radius)
        {
            return IntersectsWall(position, radius, Z, HoleCenter, HoleSize, Thickness);
        }

        public static bool IntersectsWall(
            Vector3 position,
            float radius,
            float wallZ,
            Vector2 holeCenter,
            Vector2 holeSize,
            float thickness)
        {
            if (Mathf.Abs(position.z - wallZ) > thickness * 0.5f + radius)
            {
                return false;
            }

            float safeRadius = Mathf.Min(holeSize.x, holeSize.y) * 0.5f - radius;
            Vector2 offset = new Vector2(position.x - holeCenter.x, position.y - holeCenter.y);
            bool fullyInsideHole = safeRadius > 0f && offset.sqrMagnitude < safeRadius * safeRadius;
            return !fullyInsideHole;
        }
    }
}
