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
        private const string HudPlatePrefabPath =
            "Assets/FlappyBoids/Prefabs/UI/P_BlueCurrentHudPlate.prefab";
        private const string HudModalPrefabPath =
            "Assets/FlappyBoids/Prefabs/UI/P_BlueCurrentModal.prefab";
        private const string DeepOceanVolumePath =
            "Assets/FlappyBoids/Art/Profiles/VP_DeepOcean.asset";
        private const string GameRuntimePath = "Assets/FlappyBoids/Runtime/FlappyBoidsGame.cs";
        private const string HudRuntimePath = "Assets/FlappyBoids/Runtime/FlappyBoidsHud.cs";

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
        public void AuthoredScene_ContainsOnlyCameraBubbleZone()
        {
            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            string bubbleGuid = AssetDatabase.AssetPathToGUID(BubblePrefabPath);

            Assert.That(bubbleGuid, Is.Not.Empty, "The imported BubblesZone prefab is missing.");
            Assert.That(Regex.Matches(
                sceneYaml,
                $@"m_SourcePrefab: \{{fileID: 100100000, guid: {bubbleGuid}, type: 3\}}").Count,
                Is.EqualTo(1));
        }

        [Test]
        public void AuthoredScene_UsesMarchingCubesWithoutLegacyDecorations()
        {
            string sceneYaml = File.ReadAllText(AuthoredScenePath);

            Assert.That(sceneYaml, Does.Contain("01_Marching Cubes Infinite Sea"));
            Assert.That(sceneYaml, Does.Contain("02_Marching Cubes Rock Gates"));
            Assert.That(sceneYaml, Does.Not.Contain("01_Corridor"));
            Assert.That(sceneYaml, Does.Not.Contain("03_Decorations"));
        }

        [Test]
        public void AuthoredHud_UsesSafeAreaAndReusableBlueCurrentPrefabs()
        {
            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            string plateGuid = AssetDatabase.AssetPathToGUID(HudPlatePrefabPath);
            string modalGuid = AssetDatabase.AssetPathToGUID(HudModalPrefabPath);

            Assert.That(plateGuid, Is.Not.Empty, "The reusable blue HUD plate prefab is missing.");
            Assert.That(modalGuid, Is.Not.Empty, "The reusable blue modal prefab is missing.");
            Assert.That(sceneYaml, Does.Contain("m_Name: Safe Area"));
            Assert.That(sceneYaml, Does.Contain("m_Name: Gameplay Layer"));
            Assert.That(sceneYaml, Does.Contain("m_Name: Top HUD Rail"));
            Assert.That(sceneYaml, Does.Contain("School Status Plate"));
            Assert.That(sceneYaml, Does.Contain("Gate Progress Plate"));
            Assert.That(sceneYaml, Does.Contain("Passage Telemetry Plate"));
            Assert.That(sceneYaml, Does.Contain("m_Name: Ready Layer"));
            Assert.That(sceneYaml, Does.Contain("m_Text: PRESS ANY BUTTON"));
            Assert.That(Regex.Matches(
                sceneYaml,
                $@"m_SourcePrefab: \{{fileID: 100100000, guid: {plateGuid}, type: 3\}}").Count,
                Is.EqualTo(3));
            Assert.That(Regex.Matches(
                sceneYaml,
                $@"m_SourcePrefab: \{{fileID: 100100000, guid: {modalGuid}, type: 3\}}").Count,
                Is.EqualTo(2));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(
                "Assets/FlappyBoids/Prefabs/UI/P_IdleTogetherPixelFrame.prefab"), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(
                "Assets/FlappyBoids/Prefabs/UI/P_UnderwaterHudCard.prefab"), Is.Null);
        }

        [Test]
        public void AuthoredScene_ContainsDeepOceanPostProcessingAndLighting()
        {
            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            string profileGuid = AssetDatabase.AssetPathToGUID(DeepOceanVolumePath);

            Assert.That(profileGuid, Is.Not.Empty, "The deep-ocean Volume Profile is missing.");
            Assert.That(sceneYaml, Does.Contain("m_Name: Deep Ocean Global Volume"));
            Assert.That(sceneYaml, Does.Contain(profileGuid));
            Assert.That(sceneYaml, Does.Contain("m_Name: Course Light"));
            Assert.That(sceneYaml, Does.Contain("m_Name: School Dive Light"));
        }

        [Test]
        public void HudRuntime_HasNoHiddenVisualConstructionFallback()
        {
            string source = File.ReadAllText(HudRuntimePath);

            Assert.That(source, Does.Not.Match(@"new\s+GameObject\s*\("));
            Assert.That(source, Does.Not.Match(@"\bInstantiate\s*\("));
            Assert.That(source, Does.Not.Match(@"\bAddComponent\s*<"));
            Assert.That(source, Does.Not.Contain("private void LateUpdate()"),
                "The HUD should react to game-state events instead of polling every frame.");
        }

        [Test]
        public void ReadyState_StartsFromInputSystemsAnyButtonStream()
        {
            string source = File.ReadAllText(GameRuntimePath);

            Assert.That(source, Does.Contain("InputSystem.onAnyButtonPress.Call"));
            Assert.That(source, Does.Contain("if (startPressed) BeginRun(horizontal);"));
        }

        [Test]
        public void SafeArea_ConvertsPixelsToNormalizedAnchors()
        {
            FlappyBoidsSafeArea.GetNormalizedAnchors(
                new Rect(80f, 40f, 1760f, 1000f), new Vector2(1920f, 1080f),
                out Vector2 minimum, out Vector2 maximum);

            Assert.That(minimum.x, Is.EqualTo(80f / 1920f).Within(0.0001f));
            Assert.That(minimum.y, Is.EqualTo(40f / 1080f).Within(0.0001f));
            Assert.That(maximum.x, Is.EqualTo(1840f / 1920f).Within(0.0001f));
            Assert.That(maximum.y, Is.EqualTo(1040f / 1080f).Within(0.0001f));
        }

        [Test]
        public void MarchingCubes_ExtractsClosedSphereSurface()
        {
            Mesh mesh = MarchingCubesMeshBuilder.Build(
                "Test Sphere",
                new Bounds(Vector3.zero, Vector3.one * 4f),
                new Vector3Int(12, 12, 12),
                point => 1f - point.magnitude);

            try
            {
                Assert.That(mesh.vertexCount, Is.GreaterThan(100));
                Assert.That(mesh.triangles.Length, Is.GreaterThan(300));
                Assert.That(mesh.bounds.extents.x, Is.EqualTo(1f).Within(0.12f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MarchingCubes_FacetedConversionGivesEveryTriangleHardEdges()
        {
            Mesh mesh = MarchingCubesMeshBuilder.Build(
                "Faceted Test Sphere",
                new Bounds(Vector3.zero, Vector3.one * 4f),
                new Vector3Int(8, 8, 8),
                point => 1f - point.magnitude);
            int triangleIndexCount = mesh.triangles.Length;

            try
            {
                MarchingCubesMeshBuilder.MakeFaceted(mesh);

                Assert.That(mesh.vertexCount, Is.EqualTo(triangleIndexCount));
                Assert.That(mesh.triangles.Length, Is.EqualTo(triangleIndexCount));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void RuggedRockNoise_IsDeterministicBoundedAndSeeded()
        {
            var point = new Vector2(3.25f, 18.75f);
            float first = RuggedRockNoise.Mass(point, 917, 0.16f);
            float repeat = RuggedRockNoise.Mass(point, 917, 0.16f);
            float otherSeed = RuggedRockNoise.Mass(point, 1337, 0.16f);

            Assert.That(first, Is.InRange(0f, 1f));
            Assert.That(repeat, Is.EqualTo(first).Within(0.000001f));
            Assert.That(Mathf.Abs(otherSeed - first), Is.GreaterThan(0.0001f));
            Assert.That(RuggedRockNoise.Ridge(point, 917, 0.43f), Is.InRange(0f, 1f));
            Assert.That(RuggedRockNoise.Chips(point, 917, 0.91f), Is.InRange(0f, 1f));
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
