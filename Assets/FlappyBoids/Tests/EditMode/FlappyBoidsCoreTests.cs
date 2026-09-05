using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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
        private const string MarchingCubesGatePrefabPath =
            "Assets/FlappyBoids/Prefabs/Environment/P_MarchingCubesRockGate.prefab";
        private const string MarchingCubesGateRuntimePath =
            "Assets/FlappyBoids/Runtime/MarchingCubesGateVisual.cs";
        private const string MarchingCubesSeaChunkRuntimePath =
            "Assets/FlappyBoids/Runtime/MarchingCubesSeaChunk.cs";
        private const string PipeSafetyMaterialPath =
            "Assets/FlappyBoids/Art/Materials/M_Pipe_SafetyRing.mat";
        private const string GodRayShaderPath =
            "Assets/FlappyBoids/Art/Shaders/UnderwaterVolumetricGodRays.shader";
        private const string GodRayMaterialPath =
            "Assets/FlappyBoids/Art/Materials/M_Underwater_GodRays.mat";
        private const string GodRayPrefabPath =
            "Assets/FlappyBoids/Prefabs/Environment/P_UnderwaterGodRayVolume.prefab";
        private const string GameRuntimePath = "Assets/FlappyBoids/Runtime/FlappyBoidsGame.cs";
        private const string HudRuntimePath = "Assets/FlappyBoids/Runtime/FlappyBoidsHud.cs";
        private const string AudioRuntimePath = "Assets/FlappyBoids/Runtime/FlappyBoidsAudio.cs";
        private const string BoidSystemsRuntimePath = "Assets/FlappyBoids/Runtime/BoidEcsSystems.cs";
        private const string BoidSwarmRuntimePath = "Assets/FlappyBoids/Runtime/BoidSwarm.cs";
        private const string WebBuildPath = "Assets/FlappyBoids/Editor/FlappyBoidsWebBuild.cs";
        private const string MobileRenderPipelinePath = "Assets/Settings/Mobile_RPAsset.asset";

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
        public void MarchingCubesGate_HasAuthoredMineralLipWithoutRuntimeFallback()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarchingCubesGatePrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Transform lip = prefab.transform.Find("Marching Cubes Mineral Lip");
            Assert.That(lip, Is.Not.Null);
            Assert.That(lip.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(lip.GetComponent<MeshRenderer>(), Is.Not.Null);

            MarchingCubesGateVisual visual = prefab.GetComponent<MarchingCubesGateVisual>();
            Assert.That(visual, Is.Not.Null);
            var serializedVisual = new SerializedObject(visual);
            Assert.That(serializedVisual.FindProperty("_rockFilter").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedVisual.FindProperty("_rockRenderer").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedVisual.FindProperty("_lipRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedVisual.FindProperty("_lipFilter").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedVisual.FindProperty("_lipRenderer").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedVisual.FindProperty("_crossSectionCells").intValue, Is.EqualTo(28));
            Assert.That(serializedVisual.FindProperty("_depthCells").intValue, Is.EqualTo(6));
            Assert.That(serializedVisual.FindProperty("_lipThickness").floatValue, Is.EqualTo(0.16f));
            Assert.That(serializedVisual.FindProperty("_lipDiameterScale").floatValue, Is.EqualTo(0.9f));
            Assert.That(lip.localScale.x, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(lip.localScale.y, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(lip.localScale.z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(lip.GetComponent<MeshRenderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));

            Material safetyMaterial = AssetDatabase.LoadAssetAtPath<Material>(PipeSafetyMaterialPath);
            Assert.That(safetyMaterial, Is.Not.Null);
            Assert.That(lip.GetComponent<MeshRenderer>().sharedMaterial, Is.EqualTo(safetyMaterial));
            Color safetyColor = safetyMaterial.GetColor("_BaseColor");
            Assert.That(safetyColor.maxColorComponent, Is.GreaterThan(1f),
                "The thin safety lip needs an HDR unlit color so it remains visible through underwater fog.");
            Assert.That(safetyColor.r, Is.GreaterThan(safetyColor.g * 4f));

            string source = File.ReadAllText(MarchingCubesGateRuntimePath);
            Assert.That(source, Does.Not.Match(@"new\s+GameObject\s*\("));
            Assert.That(source, Does.Not.Match(@"\bAddComponent\s*<"));
        }

        [Test]
        public void MarchingCubesSeaPool_ReusesAuthoredRenderersWithoutRuntimeRebuildFallback()
        {
            string source = File.ReadAllText(MarchingCubesSeaChunkRuntimePath);
            Assert.That(source, Does.Not.Match(@"new\s+GameObject\s*\("));
            Assert.That(source, Does.Not.Match(@"\bAddComponent\s*<"));

            Match setIndex = Regex.Match(source,
                @"public void SetChunkIndex\(int chunkIndex\)(?<body>[\s\S]*?)public void ResetChunk");
            Assert.That(setIndex.Success, Is.True);
            Assert.That(setIndex.Groups["body"].Value, Does.Not.Contain("Rebuild()"),
                "Returning a sea chunk to the pool must not synchronously regenerate its mesh.");

            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            Assert.That(Regex.Matches(sceneYaml, @"\n  _meshFilter: \{fileID: \d+\}").Count,
                Is.EqualTo(12));
            Assert.That(Regex.Matches(sceneYaml, @"\n  _meshRenderer: \{fileID: \d+\}").Count,
                Is.EqualTo(12));
        }

        [TestCase("M_Fish_Blue")]
        [TestCase("M_Fish_Gold")]
        [TestCase("M_Fish_Coral")]
        [TestCase("M_Fish_Fin")]
        [TestCase("M_Fish_Eye")]
        public void FishMaterials_EnableGpuInstancing(string materialName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                $"Assets/FlappyBoids/Art/Materials/{materialName}.mat");

            Assert.That(material, Is.Not.Null);
            Assert.That(material.enableInstancing, Is.True,
                "The 42 multi-part fish should be GPU-instanced instead of issuing one draw per renderer.");
        }

        [Test]
        public void UnderwaterGodRays_AreShaderDrivenAndAuthoredAsOnePrefabVolume()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GodRayShaderPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GodRayMaterialPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GodRayPrefabPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.EqualTo(shader));
            Assert.That(prefab, Is.Not.Null);

            MeshFilter filter = prefab.GetComponent<MeshFilter>();
            MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
            Assert.That(filter, Is.Not.Null);
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.EqualTo(material));

            string shaderSource = File.ReadAllText(GodRayShaderPath);
            Assert.That(shaderSource, Does.Contain("defined(SHADER_API_GLES3)"));
            Assert.That(shaderSource, Does.Contain("const int StepCount = 8"));
            Assert.That(shaderSource, Does.Contain("const int StepCount = 12"));
            Assert.That(shaderSource, Does.Contain("SampleSceneDepth"));
            Assert.That(shaderSource, Does.Contain("ComputeWorldSpacePosition"));
            Assert.That(shaderSource, Does.Not.Contain("GrabPass"));

            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            string prefabGuid = AssetDatabase.AssetPathToGUID(GodRayPrefabPath);
            Assert.That(sceneYaml, Does.Contain("03_Shader Volumetric God Rays"));
            Assert.That(Regex.Matches(
                sceneYaml,
                $@"m_SourcePrefab: \{{fileID: 100100000, guid: {prefabGuid}, type: 3\}}").Count,
                Is.EqualTo(1));
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
            Assert.That(sceneYaml, Does.Contain("m_Name: School Fit Bar"));
            Assert.That(sceneYaml, Does.Not.Contain("Gate Pass School Fit Feedback"));
            Assert.That(sceneYaml, Does.Not.Contain("m_Text: SCHOOL FIT"));
            Assert.That(sceneYaml, Does.Not.Contain("m_Text: 100%"));
            Assert.That(sceneYaml, Does.Not.Contain("m_Text: NEXT"));
            Assert.That(sceneYaml, Does.Not.Contain("m_Text: OPENING"));
            Assert.That(sceneYaml, Does.Not.Contain("_nextDistance:"));
            Assert.That(sceneYaml, Does.Not.Contain("_aperture:"));
            Assert.That(sceneYaml, Does.Contain("m_RenderMode: 0"));
            Assert.That(sceneYaml, Does.Contain("m_PixelPerfect: 1"));
            Assert.That(sceneYaml, Does.Contain("m_ReferenceResolution: {x: 1600, y: 900}"));
            Assert.That(sceneYaml, Does.Contain("m_FontSize: 76"));
            Assert.That(sceneYaml, Does.Contain("m_FontSize: 38"));
            Assert.That(sceneYaml, Does.Contain("m_Name: Ready Layer"));
            Assert.That(sceneYaml, Does.Contain("m_Text: PRESS ANY BUTTON"));
            Assert.That(Regex.Matches(
                sceneYaml,
                $@"m_SourcePrefab: \{{fileID: 100100000, guid: {plateGuid}, type: 3\}}").Count,
                Is.EqualTo(2));
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
            Assert.That(source, Does.Not.Contain("ShowGatePassFeedback"));
            Assert.That(source, Does.Not.Contain("private void LateUpdate()"),
                "The HUD should react to game-state events instead of polling every frame.");

            string gameSource = File.ReadAllText(GameRuntimePath);
            Match hudSignature = Regex.Match(gameSource,
                @"private struct HudSignature(?<body>[\s\S]*?)\n        \}");
            Assert.That(hudSignature.Success, Is.True);
            Assert.That(hudSignature.Groups["body"].Value, Does.Not.Contain("NextDistanceMeters"));
            Assert.That(hudSignature.Groups["body"].Value, Does.Not.Contain("ApertureTenths"));
            Assert.That(hudSignature.Groups["body"].Value, Does.Not.Contain("FitPercent"));
            Assert.That(hudSignature.Groups["body"].Value, Does.Not.Contain("FitSteps"));
            Assert.That(source, Does.Contain("FormationFitChanged"),
                "The centered fit bar should react immediately to the swarm's calculated fit event.");
        }

        [Test]
        public void AuthoredAudio_UsesImportedIdleTogetherClipsWithoutRuntimeConstructionFallback()
        {
            string[] clipPaths =
            {
                "Assets/FlappyBoids/Imported/IdleTogetherSounds/click1.wav",
                "Assets/FlappyBoids/Imported/IdleTogetherSounds/click2.wav",
                "Assets/FlappyBoids/Imported/IdleTogetherSounds/pop1.mp3",
                "Assets/FlappyBoids/Imported/IdleTogetherSounds/pop_variation-01.mp3",
                "Assets/FlappyBoids/Imported/IdleTogetherSounds/pop_variation-04.mp3",
                "Assets/FlappyBoids/Imported/IdleTogetherSounds/pop_variation-05.mp3",
                "Assets/FlappyBoids/Imported/IdleTogetherSounds/pop_variation-08.mp3"
            };
            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            for (int i = 0; i < clipPaths.Length; i++)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPaths[i]);
                Assert.That(clip, Is.Not.Null, $"Missing copied IdleTogether audio: {clipPaths[i]}");
                Assert.That(sceneYaml, Does.Contain(AssetDatabase.AssetPathToGUID(clipPaths[i])));
            }

            string audioSource = File.ReadAllText(AudioRuntimePath);
            Assert.That(audioSource, Does.Not.Contain("AudioClip.Create"));
            Assert.That(audioSource, Does.Not.Match(@"\bAddComponent\s*<"));
            Assert.That(audioSource, Does.Not.Match(@"new\s+GameObject\s*\("));
            Assert.That(audioSource, Does.Contain("_gateClips"));
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

        [Test]
        public void InfiniteCourse_UsesDoubleGateSpacingAndRecyclesOnlyBehindCamera()
        {
            Assert.That(FlappyBoidsGame.DefaultGateSpacing, Is.EqualTo(48f));
            Assert.That(FlappyBoidsGame.ShouldRecyclePassedGate(true, -9f, -10f), Is.False);
            Assert.That(FlappyBoidsGame.ShouldRecyclePassedGate(true, -14.1f, -10f), Is.True);
            Assert.That(FlappyBoidsGame.ShouldRecyclePassedGate(false, -30f, -10f), Is.False);

            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            Assert.That(sceneYaml, Does.Contain("_gateSpacing: 48"));
            Assert.That(sceneYaml, Does.Contain("_gateRecycleBehindCameraDistance: 4"));
        }

        [Test]
        public void WebGlRelease_UsesLeanRenderingAndBuildSettings()
        {
            string sceneYaml = File.ReadAllText(AuthoredScenePath);
            Assert.That(Regex.Matches(sceneYaml, @"\n  _lateralCells: 20").Count, Is.EqualTo(12));
            Assert.That(Regex.Matches(sceneYaml, @"\n  _lengthCells: 16").Count, Is.EqualTo(12));
            Assert.That(sceneYaml, Does.Not.Contain("m_CastShadows: 1"));

            string mobilePipeline = File.ReadAllText(MobileRenderPipelinePath);
            Assert.That(mobilePipeline, Does.Contain("m_RenderScale: 0.8"));
            Assert.That(mobilePipeline, Does.Contain("m_MainLightShadowsSupported: 0"));
            Assert.That(mobilePipeline, Does.Contain("m_AnyShadowsSupported: 0"));

            string buildSource = File.ReadAllText(WebBuildPath);
            Assert.That(buildSource, Does.Contain("BuildTarget.WebGL"));
            Assert.That(buildSource, Does.Contain("BuildOptions.None"));
            Assert.That(buildSource, Does.Contain("WebGLCompressionFormat.Brotli"));
            Assert.That(buildSource, Does.Contain("ManagedStrippingLevel.High"));
            Assert.That(buildSource, Does.Contain("Il2CppCodeGeneration.OptimizeSize"));
            Assert.That(buildSource, Does.Contain("WebGLExceptionSupport.None"));
            Assert.That(buildSource, Does.Contain("WebGLDebugSymbolMode.Off"));
        }

        [Test]
        public void BoidHotPath_ReusesBuffersAndRunsSynchronouslyOnWebGl()
        {
            string systems = File.ReadAllText(BoidSystemsRuntimePath);
            Match flockSystem = Regex.Match(systems,
                @"public partial struct BoidFlockingSystem(?<body>[\s\S]*?)public partial struct GateCourseScrollSystem");
            Assert.That(flockSystem.Success, Is.True);
            Assert.That(flockSystem.Groups["body"].Value, Does.Not.Contain("Allocator.TempJob"));
            Assert.That(flockSystem.Groups["body"].Value, Does.Not.Contain("ToComponentDataArray"));
            Assert.That(flockSystem.Groups["body"].Value, Does.Contain("Allocator.Persistent"));
            Assert.That(flockSystem.Groups["body"].Value, Does.Contain("UNITY_WEBGL"));
            Assert.That(flockSystem.Groups["body"].Value, Does.Contain("flockJob.Run(activeCount)"));

            string swarm = File.ReadAllText(BoidSwarmRuntimePath);
            Match lateUpdate = Regex.Match(swarm,
                @"private void LateUpdate\(\)(?<body>[\s\S]*?)private void OnDestroy");
            Assert.That(lateUpdate.Success, Is.True);
            Assert.That(lateUpdate.Groups["body"].Value, Does.Not.Contain("ToEntityArray"));
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
