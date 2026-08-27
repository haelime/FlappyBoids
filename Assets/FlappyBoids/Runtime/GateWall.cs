using UnityEngine;

namespace FlappyBoids
{
    [DisallowMultipleComponent]
    public sealed class GateWall : MonoBehaviour
    {
        public const float CorridorHalfWidth = 9.5f;
        public const float CorridorHeight = 12f;
        public const float Thickness = 1.1f;

        private const float OuterHalfWidth = 13f;
        private const int RingSegments = 28;

        [SerializeField, Min(1f)] private float _holeDiameter = 6.1f;

        public float Z => transform.position.z;
        public Vector2 HoleCenter => new Vector2(transform.position.x, transform.position.y);
        public Vector2 HoleSize => Vector2.one * _holeDiameter;
        public float Diameter => _holeDiameter;
        public float Radius => _holeDiameter * 0.5f;
        public bool Passed { get; set; }

        public void SetHoleDiameter(float diameter)
        {
            _holeDiameter = Mathf.Max(1f, diameter);
            ApplyVisualDiameter();
        }

        private void ApplyVisualDiameter()
        {
            float radius = Radius;
            float segmentLength = 2f * Mathf.PI * radius / RingSegments * 1.12f;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Wall Left"))
                {
                    ResizeWallSide(child, radius, true);
                }
                else if (child.name.StartsWith("Wall Right"))
                {
                    ResizeWallSide(child, radius, false);
                }
                else if (child.name.StartsWith("Safety Ring") &&
                         int.TryParse(child.name.Substring("Safety Ring ".Length), out int segment))
                {
                    float angle = segment * Mathf.PI * 2f / RingSegments;
                    Vector3 position = child.localPosition;
                    position.x = Mathf.Cos(angle) * radius;
                    position.y = Mathf.Sin(angle) * radius;
                    child.localPosition = position;
                    Vector3 scale = child.localScale;
                    scale.x = segmentLength;
                    child.localScale = scale;
                }
            }
        }

        private static void ResizeWallSide(Transform wall, float radius, bool left)
        {
            float y = wall.localPosition.y;
            float openingHalf = Mathf.Abs(y) < radius
                ? Mathf.Sqrt(radius * radius - y * y)
                : 0f;
            float sideWidth = OuterHalfWidth - openingHalf;
            Vector3 position = wall.localPosition;
            position.x = (left ? -1f : 1f) * (openingHalf + sideWidth * 0.5f);
            wall.localPosition = position;
            Vector3 scale = wall.localScale;
            scale.x = sideWidth;
            wall.localScale = scale;
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
