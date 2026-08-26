using UnityEngine;

namespace FlappyBoids
{
    [DefaultExecutionOrder(200)]
    public sealed class FlappyBoidsCamera : MonoBehaviour
    {
        [Header("Close TPS Framing")]
        [SerializeField, Min(4f)] private float _compactFollowDistance = 8.8f;
        [SerializeField, Min(6f)] private float _spreadFollowDistance = 12.8f;
        [SerializeField] private Vector2 _fovRange = new Vector2(59f, 72f);

        private FlappyBoidsGame _game;
        private BoidSwarm _swarm;
        private Vector3 _velocity;
        private Camera _camera;

        public void Configure(FlappyBoidsGame game)
        {
            _game = game;
            _swarm = game.Swarm;
            _camera = GetComponent<Camera>();
            transform.position = _swarm.Center + new Vector3(0f, -1.0f, -9.4f);
            transform.LookAt(_swarm.Center + Vector3.forward * 4.2f + Vector3.up * 0.35f);
        }

        private void LateUpdate()
        {
            if (_swarm == null) return;
            Vector3 center = _swarm.Center;
            float spread = Mathf.Clamp(_swarm.SpreadRadius, 1.5f, 5f);
            float followDistance = Mathf.Lerp(
                _compactFollowDistance,
                _spreadFollowDistance,
                Mathf.InverseLerp(1.5f, 5f, spread));
            float cameraZ = center.z - followDistance;
            Vector3 pathPoint = _game.GetCameraPathPoint(cameraZ);
            float routeError = Vector2.Distance(
                new Vector2(center.x, center.y), new Vector2(pathPoint.x, pathPoint.y));
            followDistance += Mathf.Clamp(routeError * 0.7f, 0f, 3.2f);
            cameraZ = center.z - followDistance;
            pathPoint = _game.GetCameraPathPoint(cameraZ);
            Vector3 desiredPosition = new Vector3(
                pathPoint.x,
                Mathf.Max(0.85f, pathPoint.y - 1.10f),
                cameraZ);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, 0.20f);

            Vector3 lookPoint = center + Vector3.forward * 4.8f + Vector3.up * 0.38f;
            Quaternion look = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            Quaternion bank = Quaternion.AngleAxis(-_swarm.HorizontalInput * 2.2f, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, look * bank, 7f * Time.deltaTime);

            if (_camera != null)
            {
                float targetFov = Mathf.Lerp(_fovRange.x, _fovRange.y, Mathf.InverseLerp(1.8f, 4.6f, spread)) +
                                  Mathf.Clamp(routeError * 1.8f, 0f, 7f);
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, 4f * Time.deltaTime);
            }
        }
    }
}
