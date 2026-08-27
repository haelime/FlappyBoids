using Unity.Entities;
using Unity.Mathematics;

namespace FlappyBoids
{
    public struct BoidAgent : IComponentData
    {
        public float3 Velocity;
        public int VisualIndex;
        public float WingPhase;
    }

    public struct FlockControl : IComponentData
    {
        public float HorizontalInput;
        public float HorizontalVelocity;
        public float VerticalVelocity;
        public float3 Anchor;
        public float BoostTime;
        public float Elapsed;
        public byte FlapRequested;
        public byte Running;
    }

    public struct FlockParameters : IComponentData
    {
        public float CourseSpeed;
        public float MaxSpeed;
        public float MaxSteerForce;
        public float CohesionRadius;
        public float AlignmentRadius;
        public float SeparationRadius;
        public float BirdRadius;
        public float CorridorHalfWidth;
        public float CorridorHeight;
    }

    public struct GateObstacle : IComponentData
    {
        public float Z;
        public float2 HoleCenter;
        public float2 HoleSize;
        public float Thickness;
    }
}
