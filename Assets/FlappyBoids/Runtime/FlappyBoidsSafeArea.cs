using UnityEngine;

namespace FlappyBoids
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FlappyBoidsSafeArea : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplyIfChanged(true);
        }

        private void OnEnable() => ApplyIfChanged(true);

        private void OnRectTransformDimensionsChange() => ApplyIfChanged(false);

        private void LateUpdate() => ApplyIfChanged(false);

        private void ApplyIfChanged(bool force)
        {
            if (_rectTransform == null) return;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            Rect safeArea = Screen.safeArea;
            if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize) return;

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            GetNormalizedAnchors(safeArea, screenSize, out Vector2 minimum, out Vector2 maximum);
            _rectTransform.anchorMin = minimum;
            _rectTransform.anchorMax = maximum;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

        public static void GetNormalizedAnchors(
            Rect safeArea, Vector2 screenSize, out Vector2 minimum, out Vector2 maximum)
        {
            float width = Mathf.Max(1f, screenSize.x);
            float height = Mathf.Max(1f, screenSize.y);
            minimum = new Vector2(safeArea.xMin / width, safeArea.yMin / height);
            maximum = new Vector2(safeArea.xMax / width, safeArea.yMax / height);
            minimum = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, minimum));
            maximum = Vector2.Max(minimum, Vector2.Min(Vector2.one, maximum));
        }
    }
}
