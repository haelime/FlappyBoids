using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;

namespace FlappyBoids.Tests
{
    public sealed class FlappyBoidsCoreTests
    {
        private const string AuthoredScenePath = "Assets/FlappyBoids/Scenes/FlappyBoids.unity";
        private const string BubblePrefabPath =
            "Assets/FlappyBoids/ThirdParty/URPUnderwaterEffects/Prefabs/BubblesZone.prefab";

        [Test]
        public void AuthoredScene_AllPrefabSourceGuidsResolve()
        {
            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            MatchCollection prefabReferences = Regex.Matches(
                sceneYaml,
                @"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]{32}), type: 3\}");

            Assert.That(prefabReferences.Count, Is.GreaterThan(0));
            foreach (Match reference in prefabReferences)
            {
                string guid = reference.Groups[1].Value;
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Not.Empty,
                    $"Scene contains a missing prefab asset GUID: {guid}");
            }
        }

        [Test]
        public void AuthoredScene_ContainsAllSevenImportedBubbleZones()
        {
            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            string bubbleGuid = AssetDatabase.AssetPathToGUID(BubblePrefabPath);

            Assert.That(bubbleGuid, Is.Not.Empty, "The imported BubblesZone prefab is missing.");
            Assert.That(Regex.Matches(
                sceneYaml,
                $@"m_SourcePrefab: \{{fileID: 100100000, guid: {bubbleGuid}, type: 3\}}").Count,
                Is.EqualTo(7));
        }

        [Test]
        public void GateHole_AllowsBoidWhollyInside()
        {
            bool hit = GateWall.IntersectsWall(
                new Vector3(0f, 5f, 20f), 0.24f, 20f,
                new Vector2(0f, 5f), new Vector2(6f, 5f), 1.1f);

            Assert.That(hit, Is.False);
        }

        [Test]
        public void GateEdge_RemovesBoidWhoseRadiusTouchesFrame()
        {
            var gate = new GateObstacle
            {
                Z = 20f,
                HoleCenter = new float2(0f, 5f),
                HoleSize = new float2(6f, 5f),
                Thickness = 1.1f
            };

            bool hit = BoidWallCollisionSystem.IntersectsWall(
                new float3(2.85f, 5f, 20f), 0.24f, gate);

            Assert.That(hit, Is.True);
        }

        [Test]
        public void CircularGate_RemovesBoidInFormerRectangularCorner()
        {
            var gate = new GateObstacle
            {
                Z = 20f,
                HoleCenter = new float2(0f, 5f),
                HoleSize = new float2(6f, 6f),
                Thickness = 1.1f
            };

            bool hit = BoidWallCollisionSystem.IntersectsWall(
                new float3(2.3f, 7.3f, 20f), 0.24f, gate);

            Assert.That(hit, Is.True,
                "A fish inside the old square bounds but outside the circular opening must hit the pipe.");
        }

        [TestCase(-5f, 0f, 5.5f)]
        [TestCase(10f, 2f, 6f)]
        [TestCase(25f, 4f, 6.5f)]
        public void CameraRoute_ClampsAndInterpolatesBetweenHoleCenters(
            float sampleZ, float expectedX, float expectedY)
        {
            Vector2 point = FlappyBoidsGame.InterpolateClampedHole(
                new Vector2(0f, 5.5f), 0f,
                new Vector2(4f, 6.5f), 20f,
                sampleZ);

            Assert.That(point.x, Is.EqualTo(expectedX).Within(0.0001f));
            Assert.That(point.y, Is.EqualTo(expectedY).Within(0.0001f));
        }

        [TestCase(0, 6.1f)]
        [TestCase(10, 5.2f)]
        [TestCase(26, 3.8f)]
        [TestCase(200, 3.8f)]
        public void InfiniteDifficulty_ShrinksHoleAndClampsAtMinimum(int gateIndex, float expected)
        {
            float diameter = FlappyBoidsGame.CalculateHoleDiameter(gateIndex);

            Assert.That(diameter, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(diameter, Is.GreaterThanOrEqualTo(FlappyBoidsGame.DefaultMinimumHoleDiameter));
        }

        [Test]
        public void InfiniteDifficulty_NeverLetsInvalidMinimumExceedInitialDiameter()
        {
            float diameter = FlappyBoidsGame.CalculateHoleDiameter(100, 5f, 1f, 8f);

            Assert.That(diameter, Is.EqualTo(5f));
        }

        [TestCase(3, 8, 2, 42, true)]
        [TestCase(3, 9, 3, 8, true)]
        [TestCase(3, 7, 3, 8, false)]
        [TestCase(2, 42, 3, 0, false)]
        public void Record_OrdersByWallsThenSurvivors(
            int walls, int survivors, int bestWalls, int bestSurvivors, bool expected)
        {
            Assert.That(FlappyBoidsRecord.IsBetter(walls, survivors, bestWalls, bestSurvivors), Is.EqualTo(expected));
        }
    }
}
