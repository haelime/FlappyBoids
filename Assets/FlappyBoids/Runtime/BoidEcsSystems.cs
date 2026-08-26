using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlappyBoids
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BoidFlockingSystem : ISystem
    {
        private EntityQuery _boidQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _boidQuery = SystemAPI.QueryBuilder().WithAllRW<LocalTransform, BoidAgent>().Build();
            state.RequireForUpdate<FlockControl>();
            state.RequireForUpdate<FlockParameters>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            FlockControl control = SystemAPI.GetSingleton<FlockControl>();
            if (control.Running == 0 || _boidQuery.IsEmptyIgnoreFilter)
            {
                control.FlapRequested = 0;
                SystemAPI.SetSingleton(control);
                return;
            }

            float deltaTime = SystemAPI.Time.DeltaTime;
            FlockParameters parameters = SystemAPI.GetSingleton<FlockParameters>();
            AdvanceLeader(ref control, parameters, deltaTime);

            NativeArray<LocalTransform> transforms = _boidQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            NativeArray<BoidAgent> agents = _boidQuery.ToComponentDataArray<BoidAgent>(Allocator.TempJob);
            var positions = new NativeArray<float3>(transforms.Length, Allocator.TempJob);
            var velocities = new NativeArray<float3>(agents.Length, Allocator.TempJob);
            var nextVelocities = new NativeArray<float3>(agents.Length, Allocator.TempJob);

            for (int i = 0; i < transforms.Length; i++)
            {
                positions[i] = transforms[i].Position;
                velocities[i] = agents[i].Velocity;
            }

            var flockJob = new FlockVelocityJob
            {
                Positions = positions,
                Velocities = velocities,
                OutputVelocities = nextVelocities,
                Parameters = parameters,
                Anchor = control.Anchor,
                LeaderVelocity = new float3(control.HorizontalVelocity, control.VerticalVelocity, parameters.ForwardSpeed),
                Boosted = control.BoostTime > 0f,
                DeltaTime = deltaTime
            };
            JobHandle flockHandle = flockJob.Schedule(transforms.Length, 16, state.Dependency);
            flockHandle.Complete();

            int index = 0;
            foreach ((RefRW<LocalTransform> transform, RefRW<BoidAgent> agent) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<BoidAgent>>())
            {
                float3 velocity = nextVelocities[index];
                LocalTransform value = transform.ValueRO;
                value.Position += velocity * deltaTime;
                value.Rotation = math.slerp(value.Rotation, quaternion.LookRotationSafe(velocity, math.up()),
                    math.saturate(10f * deltaTime));
                transform.ValueRW = value;

                BoidAgent agentValue = agent.ValueRO;
                agentValue.Velocity = velocity;
                agent.ValueRW = agentValue;
                index++;
            }

            transforms.Dispose();
            agents.Dispose();
            positions.Dispose();
            velocities.Dispose();
            nextVelocities.Dispose();
            SystemAPI.SetSingleton(control);
            state.Dependency = default;
        }

        private static void AdvanceLeader(ref FlockControl control, FlockParameters parameters, float deltaTime)
        {
            control.Elapsed += deltaTime;
            control.BoostTime = math.max(0f, control.BoostTime - deltaTime);
            control.HorizontalVelocity = MoveTowards(
                control.HorizontalVelocity, control.HorizontalInput * 6.7f, 19f * deltaTime);
            control.VerticalVelocity = math.max(control.VerticalVelocity - 8.4f * deltaTime, -6.5f);

            if (control.FlapRequested != 0)
            {
                control.VerticalVelocity = 5.7f;
                control.BoostTime = 0.72f;
                control.FlapRequested = 0;
            }

            control.Anchor += new float3(
                control.HorizontalVelocity,
                control.VerticalVelocity,
                parameters.ForwardSpeed) * deltaTime;
            control.Anchor.x = math.clamp(control.Anchor.x, -7.5f, 7.5f);
            control.Anchor.y = math.clamp(control.Anchor.y, 0.7f, parameters.CorridorHeight - 0.7f);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float difference = target - current;
            return math.abs(difference) <= maxDelta ? target : current + math.sign(difference) * maxDelta;
        }

        [BurstCompile]
        private struct FlockVelocityJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float3> Velocities;
            [WriteOnly] public NativeArray<float3> OutputVelocities;
            public FlockParameters Parameters;
            public float3 Anchor;
            public float3 LeaderVelocity;
            public bool Boosted;
            public float DeltaTime;

            public void Execute(int index)
            {
                float3 position = Positions[index];
                float3 velocity = Velocities[index];
                float3 separation = float3.zero;
                float3 alignment = float3.zero;
                float3 cohesion = float3.zero;
                int separationCount = 0;
                int alignmentCount = 0;
                int cohesionCount = 0;

                for (int otherIndex = 0; otherIndex < Positions.Length; otherIndex++)
                {
                    if (index == otherIndex) continue;
                    float3 difference = position - Positions[otherIndex];
                    float distance = math.length(difference);
                    if (distance <= 0.001f) continue;

                    if (distance <= Parameters.SeparationRadius)
                    {
                        separation += math.normalizesafe(difference) / distance;
                        separationCount++;
                    }
                    if (distance <= Parameters.AlignmentRadius)
                    {
                        alignment += Velocities[otherIndex];
                        alignmentCount++;
                    }
                    if (distance <= Parameters.CohesionRadius)
                    {
                        cohesion += Positions[otherIndex];
                        cohesionCount++;
                    }
                }

                float3 force = float3.zero;
                float cohesionMultiplier = Boosted ? 3.6f : 1f;
                float separationMultiplier = Boosted ? 0.58f : 1f;
                if (separationCount > 0)
                    force += Steer(separation / separationCount, velocity) * 2.25f * separationMultiplier;
                if (alignmentCount > 0)
                    force += Steer(alignment / alignmentCount, velocity) * 0.72f;
                if (cohesionCount > 0)
                    force += Steer(cohesion / cohesionCount - position, velocity) * 0.92f * cohesionMultiplier;

                force += Steer(Anchor + FormationOffset(index) - position, velocity) * 1.65f;
                force += Steer(LeaderVelocity, velocity) * 0.62f;
                force = Limit(force, Parameters.MaxSteerForce);

                float3 next = velocity + force * DeltaTime;
                next.z = MoveTowards(next.z, Parameters.ForwardSpeed, 5.5f * DeltaTime);
                OutputVelocities[index] = Limit(next, Parameters.MaxSpeed);
            }

            private float3 Steer(float3 desiredDirection, float3 currentVelocity)
            {
                if (math.lengthsq(desiredDirection) < 0.0001f) return float3.zero;
                float3 desiredVelocity = math.normalizesafe(desiredDirection) * Parameters.MaxSpeed;
                return Limit(desiredVelocity - currentVelocity, Parameters.MaxSteerForce);
            }

            private static float3 FormationOffset(int index)
            {
                const int startingBoids = 42;
                float angle = index * 2.399963f;
                float radius = 0.38f + math.sqrt(index / (float)startingBoids) * 1.15f;
                return new float3(math.cos(angle) * radius, math.sin(angle) * radius * 0.7f,
                    ((index % 5) - 2) * 0.12f);
            }

            private static float3 Limit(float3 value, float maximum)
            {
                float length = math.length(value);
                return length > maximum && length > 0f ? value * (maximum / length) : value;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BoidFlockingSystem))]
    public partial struct BoidWallCollisionSystem : ISystem
    {
        private EntityQuery _gateQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _gateQuery = SystemAPI.QueryBuilder().WithAll<GateObstacle>().Build();
            state.RequireForUpdate<BoidAgent>();
            state.RequireForUpdate<GateObstacle>();
            state.RequireForUpdate<FlockParameters>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            FlockParameters parameters = SystemAPI.GetSingleton<FlockParameters>();
            NativeArray<GateObstacle> gates = _gateQuery.ToComponentDataArray<GateObstacle>(Allocator.TempJob);
            var commandBuffer = new EntityCommandBuffer(Allocator.TempJob);

            foreach ((RefRO<LocalTransform> transform, Entity entity) in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<BoidAgent>().WithEntityAccess())
            {
                float3 position = transform.ValueRO.Position;
                bool hit = position.y <= parameters.BirdRadius ||
                           position.y >= parameters.CorridorHeight - parameters.BirdRadius ||
                           math.abs(position.x) >= parameters.CorridorHalfWidth - parameters.BirdRadius;

                for (int i = 0; i < gates.Length && !hit; i++)
                    hit = IntersectsWall(position, parameters.BirdRadius, gates[i]);

                if (hit) commandBuffer.DestroyEntity(entity);
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
            gates.Dispose();
        }

        public static bool IntersectsWall(float3 position, float radius, GateObstacle gate)
        {
            if (math.abs(position.z - gate.Z) > gate.Thickness * 0.5f + radius) return false;
            float safeRadius = math.min(gate.HoleSize.x, gate.HoleSize.y) * 0.5f - radius;
            float2 offset = position.xy - gate.HoleCenter;
            bool fullyInsideHole = safeRadius > 0f && math.lengthsq(offset) < safeRadius * safeRadius;
            return !fullyInsideHole;
        }
    }
}
