using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace FlappyBoids
{
    // Thin presentation/input bridge: flock simulation, collision, and lifetime stay in ECS.
    [DefaultExecutionOrder(100)]
    public sealed class BoidSwarm : MonoBehaviour
    {
        public const int StartingBoids = 42;
        public const float BirdRadius = 0.24f;

        [Header("Runtime visual prefabs")]
        [SerializeField] private GameObject[] _fishVisualPrefabs;

        private sealed class BirdVisual
        {
            public GameObject Root;
            public Transform LeftFin;
            public Transform RightFin;
            public float WingPhase;
        }

        private readonly Dictionary<Entity, BirdVisual> _visuals = new Dictionary<Entity, BirdVisual>();
        private readonly List<Entity> _staleEntities = new List<Entity>();
        private EntityManager _entityManager;
        private EntityQuery _boidQuery;
        private Entity _controlEntity;
        private int _lastAliveCount;
        private bool _configured;
        private Vector2 _guideCenter;
        private float _guideRadius;
        private float _courseSpeed = FlappyBoidsGame.DefaultCourseSpeed;

        public int AliveCount { get; private set; }
        public int RemovedSinceLastFrame { get; private set; }
        public Vector3 Center { get; private set; }
        public float BoostNormalized { get; private set; }
        public float HorizontalInput { get; private set; }
        public float SpreadRadius { get; private set; }
        public float FormationFit { get; private set; }

        public void SetVisualPrefabs(GameObject[] fishVisualPrefabs)
        {
            _fishVisualPrefabs = fishVisualPrefabs;
        }

        public void Configure(IReadOnlyList<GateWall> gates, float courseSpeed = FlappyBoidsGame.DefaultCourseSpeed)
        {
            _courseSpeed = Mathf.Max(1f, courseSpeed);
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _boidQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BoidAgent>(), ComponentType.ReadOnly<LocalTransform>());
            _configured = true;
            CreateSingletons();
            CreateOrSyncGateEntities(gates);
            ResetSwarm();
        }

        public void RefreshGateObstacles(IReadOnlyList<GateWall> gates)
        {
            if (!_configured) return;
            CreateOrSyncGateEntities(gates);
        }

        public void ResetSwarm()
        {
            if (_configured) _entityManager.DestroyEntity(_boidQuery);
            foreach (BirdVisual visual in _visuals.Values)
                if (visual.Root != null) Destroy(visual.Root);
            _visuals.Clear();

            UnityEngine.Random.State previousState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(2451);
            float3 anchor = new float3(0f, 5.5f, 0f);

            for (int i = 0; i < StartingBoids; i++)
            {
                Vector3 random = UnityEngine.Random.insideUnitSphere;
                float3 position = anchor + new float3(random.x * 2f, random.y * 1.55f, random.z * 1.6f);
                float3 velocity = new float3(
                    UnityEngine.Random.Range(-0.5f, 0.5f),
                    UnityEngine.Random.Range(-0.35f, 0.35f),
                    UnityEngine.Random.Range(-0.22f, 0.22f));

                Entity entity = _entityManager.CreateEntity(typeof(BoidAgent), typeof(LocalTransform));
                _entityManager.SetComponentData(entity, new BoidAgent
                {
                    Velocity = velocity,
                    VisualIndex = i,
                    WingPhase = i * 0.73f
                });
                _entityManager.SetComponentData(entity,
                    LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
                _visuals.Add(entity, CreateFishVisual(i));
            }
            UnityEngine.Random.state = previousState;

            _entityManager.SetComponentData(_controlEntity, new FlockControl { Anchor = anchor });
            AliveCount = StartingBoids;
            _lastAliveCount = StartingBoids;
            RemovedSinceLastFrame = 0;
            Center = anchor;
            BoostNormalized = 0f;
            SpreadRadius = 2f;
            FormationFit = 1f;
        }

        public void Begin()
        {
            FlockControl control = _entityManager.GetComponentData<FlockControl>(_controlEntity);
            control.Running = 1;
            _entityManager.SetComponentData(_controlEntity, control);
        }

        public void Stop()
        {
            if (!_entityManager.Exists(_controlEntity)) return;
            FlockControl control = _entityManager.GetComponentData<FlockControl>(_controlEntity);
            control.Running = 0;
            _entityManager.SetComponentData(_controlEntity, control);
        }

        public void SetInput(float horizontalInput, bool flap)
        {
            HorizontalInput = horizontalInput;
            FlockControl control = _entityManager.GetComponentData<FlockControl>(_controlEntity);
            control.HorizontalInput = horizontalInput;
            if (flap) control.FlapRequested = 1;
            _entityManager.SetComponentData(_controlEntity, control);
        }

        public void SetGuidanceHole(Vector2 center, float radius)
        {
            _guideCenter = center;
            _guideRadius = radius;
        }

        private void LateUpdate()
        {
            if (!_configured || !_entityManager.Exists(_controlEntity)) return;

            FlockControl control = _entityManager.GetComponentData<FlockControl>(_controlEntity);
            BoostNormalized = Mathf.Clamp01(control.BoostTime / 0.72f);
            NativeArray<Entity> entities = _boidQuery.ToEntityArray(Allocator.Temp);
            Vector3 centerSum = Vector3.zero;
            Vector3 minimum = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maximum = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int fitCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!_visuals.TryGetValue(entity, out BirdVisual visual)) continue;
                LocalTransform localTransform = _entityManager.GetComponentData<LocalTransform>(entity);
                visual.Root.transform.SetPositionAndRotation(localTransform.Position, localTransform.Rotation);
                float angle = Mathf.Sin(control.Elapsed * (control.BoostTime > 0f ? 22f : 12f) + visual.WingPhase) * 32f;
                visual.LeftFin.localRotation = Quaternion.Euler(0f, -12f, angle * 0.55f - 12f);
                visual.RightFin.localRotation = Quaternion.Euler(0f, 12f, -angle * 0.55f + 12f);
                Vector3 position = localTransform.Position;
                centerSum += position;
                minimum = Vector3.Min(minimum, position);
                maximum = Vector3.Max(maximum, position);
                Vector2 guideOffset = new Vector2(position.x, position.y) - _guideCenter;
                if (_guideRadius <= 0f || guideOffset.sqrMagnitude < _guideRadius * _guideRadius) fitCount++;
            }

            _staleEntities.Clear();
            foreach (KeyValuePair<Entity, BirdVisual> pair in _visuals)
                if (!_entityManager.Exists(pair.Key)) _staleEntities.Add(pair.Key);
            for (int i = 0; i < _staleEntities.Count; i++)
            {
                Entity entity = _staleEntities[i];
                Destroy(_visuals[entity].Root);
                _visuals.Remove(entity);
            }

            AliveCount = entities.Length;
            RemovedSinceLastFrame = Mathf.Max(0, _lastAliveCount - AliveCount);
            _lastAliveCount = AliveCount;
            if (AliveCount > 0)
            {
                Center = centerSum / AliveCount;
                SpreadRadius = (maximum - minimum).magnitude * 0.5f;
                FormationFit = fitCount / (float)AliveCount;
            }
            else
            {
                SpreadRadius = 0f;
                FormationFit = 0f;
            }
            entities.Dispose();
        }

        private void OnDestroy()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || !_configured) return;
            _entityManager.DestroyEntity(_boidQuery);
            EntityQuery gates = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GateObstacle>());
            _entityManager.DestroyEntity(gates);
            gates.Dispose();
            if (_entityManager.Exists(_controlEntity)) _entityManager.DestroyEntity(_controlEntity);
            _boidQuery.Dispose();
            _configured = false;
        }

        private void CreateSingletons()
        {
            EntityQuery singletonQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<FlockControl>(), ComponentType.ReadOnly<FlockParameters>());
            if (!singletonQuery.IsEmptyIgnoreFilter)
                _controlEntity = singletonQuery.GetSingletonEntity();
            else
                _controlEntity = _entityManager.CreateEntity(typeof(FlockControl), typeof(FlockParameters));
            singletonQuery.Dispose();

            _entityManager.SetComponentData(_controlEntity, new FlockParameters
            {
                CourseSpeed = _courseSpeed,
                MaxSpeed = 11.2f,
                MaxSteerForce = 11f,
                CohesionRadius = 3.1f,
                AlignmentRadius = 2.7f,
                SeparationRadius = 0.72f,
                BirdRadius = BirdRadius,
                CorridorHalfWidth = GateWall.CorridorHalfWidth,
                CorridorHeight = GateWall.CorridorHeight
            });
        }

        private void CreateOrSyncGateEntities(IReadOnlyList<GateWall> gates)
        {
            EntityQuery existing = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GateObstacle>());
            if (existing.CalculateEntityCount() != gates.Count)
            {
                if (!existing.IsEmptyIgnoreFilter) _entityManager.DestroyEntity(existing);
                for (int i = 0; i < gates.Count; i++)
                    _entityManager.CreateEntity(typeof(GateObstacle));
            }

            NativeArray<Entity> obstacleEntities = existing.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < obstacleEntities.Length; i++)
            {
                GateWall gate = gates[i];
                _entityManager.SetComponentData(obstacleEntities[i], new GateObstacle
                {
                    Z = gate.Z,
                    HoleCenter = gate.HoleCenter,
                    HoleSize = gate.HoleSize,
                    Thickness = GateWall.Thickness
                });
            }
            obstacleEntities.Dispose();
            existing.Dispose();
        }

        private BirdVisual CreateFishVisual(int index)
        {
            if (_fishVisualPrefabs == null || _fishVisualPrefabs.Length == 0)
            {
                throw new System.InvalidOperationException(
                    "BoidSwarm requires authored fish prefabs. Rebuild the FlappyBoids authored scene.");
            }

            int prefabIndex = index % 7 == 0 ? Mathf.Min(1, _fishVisualPrefabs.Length - 1) :
                              index % 5 == 0 ? Mathf.Min(2, _fishVisualPrefabs.Length - 1) : 0;
            GameObject prefab = _fishVisualPrefabs[prefabIndex];
            var root = Instantiate(prefab, transform);
            root.name = $"Fish Boid {index + 1:00}";
            root.transform.SetParent(transform, false);
            Transform leftFin = root.transform.Find("Left Fin");
            Transform rightFin = root.transform.Find("Right Fin");
            if (leftFin == null || rightFin == null)
                throw new System.InvalidOperationException($"Fish prefab '{prefab.name}' is missing fin transforms.");
            return new BirdVisual
            {
                Root = root,
                LeftFin = leftFin,
                RightFin = rightFin,
                WingPhase = index * 0.73f
            };
        }
    }
}
