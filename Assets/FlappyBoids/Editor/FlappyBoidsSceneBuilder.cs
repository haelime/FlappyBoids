using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlappyBoids.Editor
{
    public static class FlappyBoidsSceneBuilder
    {
        private const string Root = "Assets/FlappyBoids";
        private const string MaterialFolder = Root + "/Art/Materials";
        private const string FishPrefabFolder = Root + "/Prefabs/Fish";
        private const string EnvironmentPrefabFolder = Root + "/Prefabs/Environment";
        private const string SceneFolder = Root + "/Scenes";
        private const string ScenePath = SceneFolder + "/FlappyBoids.unity";
        private const string StraightPipeModelPath =
            Root + "/ThirdParty/ModularLowPolyPipes/Models/Pipe2-1.obj";
        private const string CornerPipeModelPath =
            Root + "/ThirdParty/ModularLowPolyPipes/Models/Pipe2-2.obj";
        private const string WaterMaterialPath =
            Root + "/ThirdParty/UberStylizedWater/Template Materials/UWa-Template-Murky.mat";
        private const string BubblePrefabPath =
            Root + "/ThirdParty/URPUnderwaterEffects/Prefabs/BubblesZone.prefab";
        private const string AmbiencePath =
            Root + "/Audio/Ambience/Underwater_Theme_II_CC0.ogg";
        private const string ExternalAssetSceneMarker = "Uber Water Surface (MIT Asset)";

        [InitializeOnLoadMethod]
        private static void QueueExternalAssetSceneUpgrade()
        {
            EditorApplication.delayCall += UpgradeSceneAfterExternalAssetsImport;
        }

        private static void UpgradeSceneAfterExternalAssetsImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += UpgradeSceneAfterExternalAssetsImport;
                return;
            }

            if (File.Exists(ScenePath) &&
                File.ReadAllText(ScenePath).Contains(ExternalAssetSceneMarker))
                return;

            try
            {
                BuildAll();
                Debug.Log("FlappyBoids external water, bubble and ambience assets were applied to the authored scene.");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Flappy Boids/Rebuild Authored Scene")]
        public static void BuildAll()
        {
            EnsureFolders();
            Materials materials = CreateMaterials();
            GameObject[] fishPrefabs =
            {
                CreateFishPrefab("P_Fish_Blue", materials.FishBlue, materials),
                CreateFishPrefab("P_Fish_Gold", materials.FishGold, materials),
                CreateFishPrefab("P_Fish_Coral", materials.FishCoral, materials)
            };
            GameObject pipePrefab = CreatePipePrefab(materials);
            GameObject[] shortPipePrefabs =
            {
                CreateImportedPipePrefab("P_ShortPipe_Straight_CC0", StraightPipeModelPath, materials.Pipe),
                CreateImportedPipePrefab("P_ShortPipe_Corner_CC0", CornerPipeModelPath, materials.Pipe)
            };
            GameObject seaGrassPrefab = CreateSeaGrassPrefab(materials);
            GameObject bubblePrefab = LoadRequiredAsset<GameObject>(BubblePrefabPath);
            CreateScene(materials, fishPrefabs, pipePrefab, shortPipePrefabs, seaGrassPrefab, bubblePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateScenePrefabReferences();
            Debug.Log($"FlappyBoids authored assets rebuilt: {ScenePath}");
        }

        public static void BuildAllBatch()
        {
            BuildAll();
        }

        private static void ValidateScenePrefabReferences()
        {
            string sceneYaml = File.ReadAllText(ScenePath);
            MatchCollection prefabReferences = Regex.Matches(
                sceneYaml,
                @"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]{32}), type: 3\}");

            foreach (Match reference in prefabReferences)
            {
                string guid = reference.Groups[1].Value;
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                    throw new InvalidDataException(
                        $"Authored scene contains a missing prefab reference: {guid} ({ScenePath})");
            }

            string bubbleGuid = AssetDatabase.AssetPathToGUID(BubblePrefabPath);
            int bubbleInstances = Regex.Matches(
                sceneYaml,
                $@"m_SourcePrefab: \{{fileID: 100100000, guid: {bubbleGuid}, type: 3\}}").Count;
            if (bubbleInstances != 7)
                throw new InvalidDataException(
                    $"Expected 7 imported bubble prefab instances, but found {bubbleInstances} in {ScenePath}.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root + "/Art");
            EnsureFolder(MaterialFolder);
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(FishPrefabFolder);
            EnsureFolder(EnvironmentPrefabFolder);
            EnsureFolder(SceneFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Materials CreateMaterials()
        {
            return new Materials
            {
                Pipe = CreateMaterial("M_Pipe_Aged", new Color(0.055f, 0.23f, 0.20f), 0.32f),
                PipeAccent = CreateMaterial("M_Pipe_SafetyRing", new Color(0.95f, 0.52f, 0.12f), 0.48f, true),
                Seabed = CreateMaterial("M_Seabed", new Color(0.25f, 0.28f, 0.22f), 0.08f),
                WaterSurface = LoadRequiredAsset<Material>(WaterMaterialPath),
                Guide = CreateMaterial("M_Guide_Bioluminescent", new Color(0.08f, 0.72f, 0.68f), 0.25f, true),
                FishBlue = CreateMaterial("M_Fish_Blue", new Color(0.10f, 0.74f, 0.88f), 0.58f, true),
                FishGold = CreateMaterial("M_Fish_Gold", new Color(1.00f, 0.68f, 0.12f), 0.58f, true),
                FishCoral = CreateMaterial("M_Fish_Coral", new Color(1.00f, 0.27f, 0.31f), 0.58f, true),
                FishFin = CreateMaterial("M_Fish_Fin", new Color(0.025f, 0.15f, 0.18f), 0.42f),
                FishEye = CreateMaterial("M_Fish_Eye", new Color(0.006f, 0.012f, 0.018f), 0.82f)
            };
        }

        private static T LoadRequiredAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException($"Required third-party asset was not imported: {path}");
            return asset;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, bool emission = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.5f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateFishPrefab(string name, Material body, Materials materials)
        {
            var root = new GameObject(name);
            CreatePart(PrimitiveType.Sphere, "Body", root.transform, Vector3.zero,
                new Vector3(0.48f, 0.32f, 0.86f), body);
            Transform tail = CreatePart(PrimitiveType.Cube, "Tail Fin", root.transform,
                new Vector3(0f, 0f, -0.49f), new Vector3(0.055f, 0.42f, 0.34f), body).transform;
            tail.localRotation = Quaternion.Euler(45f, 0f, 0f);
            Transform dorsal = CreatePart(PrimitiveType.Cube, "Dorsal Fin", root.transform,
                new Vector3(0f, 0.22f, -0.10f), new Vector3(0.055f, 0.24f, 0.27f), materials.FishFin).transform;
            dorsal.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            CreatePart(PrimitiveType.Cube, "Left Fin", root.transform,
                new Vector3(-0.30f, -0.01f, -0.02f), new Vector3(0.36f, 0.045f, 0.25f), materials.FishFin, false);
            CreatePart(PrimitiveType.Cube, "Right Fin", root.transform,
                new Vector3(0.30f, -0.01f, -0.02f), new Vector3(0.36f, 0.045f, 0.25f), materials.FishFin, false);
            CreatePart(PrimitiveType.Sphere, "Left Eye", root.transform,
                new Vector3(-0.18f, 0.075f, 0.34f), Vector3.one * 0.075f, materials.FishEye, false);
            CreatePart(PrimitiveType.Sphere, "Right Eye", root.transform,
                new Vector3(0.18f, 0.075f, 0.34f), Vector3.one * 0.075f, materials.FishEye, false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{FishPrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePipePrefab(Materials materials)
        {
            var root = new GameObject("P_CircularPipeGate");
            GateWall gate = root.AddComponent<GateWall>();
            gate.SetHoleDiameter(6.1f);

            const float outerHalfWidth = 13f;
            const float outerHalfHeight = 9f;
            const float radius = 3.05f;
            const int rows = 20;
            float rowHeight = outerHalfHeight * 2f / rows;
            for (int row = 0; row < rows; row++)
            {
                float y = -outerHalfHeight + (row + 0.5f) * rowHeight;
                float openingHalf = Mathf.Abs(y) < radius
                    ? Mathf.Sqrt(radius * radius - y * y)
                    : 0f;
                if (openingHalf <= 0.01f)
                {
                    CreatePart(PrimitiveType.Cube, $"Wall Row {row:00}", root.transform,
                        new Vector3(0f, y, 0f), new Vector3(outerHalfWidth * 2f, rowHeight + 0.02f, GateWall.Thickness), materials.Pipe);
                    continue;
                }

                float sideWidth = outerHalfWidth - openingHalf;
                CreatePart(PrimitiveType.Cube, $"Wall Left {row:00}", root.transform,
                    new Vector3(-(openingHalf + sideWidth * 0.5f), y, 0f),
                    new Vector3(sideWidth, rowHeight + 0.02f, GateWall.Thickness), materials.Pipe);
                CreatePart(PrimitiveType.Cube, $"Wall Right {row:00}", root.transform,
                    new Vector3(openingHalf + sideWidth * 0.5f, y, 0f),
                    new Vector3(sideWidth, rowHeight + 0.02f, GateWall.Thickness), materials.Pipe);
            }

            const int segments = 28;
            float segmentLength = 2f * Mathf.PI * radius / segments * 1.12f;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                Transform rim = CreatePart(PrimitiveType.Cube, $"Safety Ring {segment:00}", root.transform,
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -GateWall.Thickness * 0.57f),
                    new Vector3(segmentLength, 0.18f, GateWall.Thickness + 0.18f), materials.PipeAccent, false).transform;
                rim.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root,
                $"{EnvironmentPrefabFolder}/P_CircularPipeGate.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateImportedPipePrefab(string prefabName, string modelPath, Material material)
        {
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                throw new System.InvalidOperationException($"CC0 pipe model could not be imported: {modelPath}");

            var root = new GameObject(prefabName);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "CC0 Pipe Mesh";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.28f;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root,
                $"{EnvironmentPrefabFolder}/{prefabName}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSeaGrassPrefab(Materials materials)
        {
            var root = new GameObject("P_SeaGrass");
            Transform stem = CreatePart(PrimitiveType.Cylinder, "Stem", root.transform,
                new Vector3(0f, 0.65f, 0f), new Vector3(0.10f, 0.65f, 0.10f), materials.Pipe, false).transform;
            stem.localRotation = Quaternion.Euler(0f, 0f, -7f);
            CreatePart(PrimitiveType.Sphere, "Glow Tip", root.transform,
                new Vector3(0.15f, 1.35f, 0f), Vector3.one * 0.13f, materials.Guide, false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root,
                $"{EnvironmentPrefabFolder}/P_SeaGrass.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateScene(
            Materials materials,
            GameObject[] fishPrefabs,
            GameObject pipePrefab,
            GameObject[] shortPipePrefabs,
            GameObject seaGrassPrefab,
            GameObject bubblePrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ApplyRenderSettings();

            var gameRoot = new GameObject("FlappyBoids");
            FlappyBoidsGame game = gameRoot.AddComponent<FlappyBoidsGame>();

            Transform systems = Group("01_Systems", gameRoot.transform);
            var swarmObject = new GameObject("ECS Fish School (Runtime Entities)");
            swarmObject.transform.SetParent(systems, false);
            BoidSwarm swarm = swarmObject.AddComponent<BoidSwarm>();
            swarm.SetVisualPrefabs(fishPrefabs);
            var audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(systems, false);
            FlappyBoidsAudio audio = audioObject.AddComponent<FlappyBoidsAudio>();
            AudioSource effectsSource = audioObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.volume = 0.32f;
            AudioSource ambientSource = audioObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = true;
            ambientSource.volume = 0.16f;
            AssetDatabase.ImportAsset(AmbiencePath, ImportAssetOptions.ForceSynchronousImport);
            AudioClip ambience = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbiencePath);
            if (ambience == null) throw new System.InvalidOperationException("Underwater ambience was not imported.");
            ambientSource.clip = ambience;
            audio.ConfigureSceneAudio(ambience, effectsSource, ambientSource);

            Transform cameras = Group("02_Camera", gameRoot.transform);
            var cameraObject = new GameObject("TPS Flock Camera");
            cameraObject.transform.SetParent(cameras, false);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3.95f, -9.4f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 2.25f, 13.9f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 59f;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 330f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.13f, 0.18f);
            cameraObject.AddComponent<AudioListener>();
            FlappyBoidsCamera followCamera = cameraObject.AddComponent<FlappyBoidsCamera>();
            GameObject cameraBubbles = InstantiatePrefab(
                bubblePrefab, cameraObject.transform, scene, "Camera Bubble Drift");
            cameraBubbles.transform.localPosition = new Vector3(0f, -2.5f, 5.5f);
            cameraBubbles.transform.localScale = Vector3.one * 0.035f;

            Transform lighting = Group("03_Lighting", gameRoot.transform);
            var lightObject = new GameObject("Course Light");
            lightObject.transform.SetParent(lighting, false);
            lightObject.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.48f, 0.90f, 0.92f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            Transform environment = Group("04_Environment", gameRoot.transform);
            BuildCorridor(environment, materials);
            BuildGuides(environment, materials);
            BuildDecorations(environment, seaGrassPrefab, bubblePrefab, shortPipePrefabs, scene);
            GateWall[] gates = BuildGates(environment, pipePrefab, scene);
            BuildFinish(environment, materials);
            FlappyBoidsHud hud = BuildHud(gameRoot.transform, camera);

            game.ConfigureScene(swarm, followCamera, audio, hud, gates);
            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(swarm);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = gameRoot;
        }

        private static void BuildCorridor(Transform environment, Materials materials)
        {
            Transform corridor = Group("01_Corridor", environment);
            CreatePart(PrimitiveType.Cube, "Seabed", corridor, new Vector3(0f, -0.24f, 117f),
                new Vector3(19f, 0.48f, 280f), materials.Seabed);
            GameObject waterSurface = CreatePart(PrimitiveType.Plane, "Uber Water Surface (MIT Asset)", corridor,
                new Vector3(0f, GateWall.CorridorHeight + 0.42f, 117f),
                new Vector3(1.9f, 1f, 28f), materials.WaterSurface, false);
            waterSurface.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            CreatePart(PrimitiveType.Cube, "Left Tunnel Edge", corridor,
                new Vector3(-GateWall.CorridorHalfWidth - 0.2f, GateWall.CorridorHeight * 0.5f, 117f),
                new Vector3(0.4f, GateWall.CorridorHeight, 280f), materials.Seabed);
            CreatePart(PrimitiveType.Cube, "Right Tunnel Edge", corridor,
                new Vector3(GateWall.CorridorHalfWidth + 0.2f, GateWall.CorridorHeight * 0.5f, 117f),
                new Vector3(0.4f, GateWall.CorridorHeight, 280f), materials.Seabed);
        }

        private static void BuildGuides(Transform environment, Materials materials)
        {
            Transform guides = Group("02_Bioluminescent Guides", environment);
            int index = 0;
            for (int z = -18; z <= 255; z += 9)
            {
                Transform segment = Group($"Guide Segment {index++:00}", guides);
                CreatePart(PrimitiveType.Cube, "Center", segment, new Vector3(0f, 0.015f, z),
                    new Vector3(0.12f, 0.03f, 4.8f), materials.Guide, false);
                CreatePart(PrimitiveType.Cube, "Left", segment, new Vector3(-6.3f, 0.02f, z),
                    new Vector3(0.06f, 0.035f, 4.8f), materials.Guide, false);
                CreatePart(PrimitiveType.Cube, "Right", segment, new Vector3(6.3f, 0.02f, z),
                    new Vector3(0.06f, 0.035f, 4.8f), materials.Guide, false);
            }
        }

        private static void BuildDecorations(
            Transform environment,
            GameObject seaGrassPrefab,
            GameObject bubblePrefab,
            GameObject[] shortPipePrefabs,
            Scene scene)
        {
            Transform decorations = Group("03_Decorations (Prefab Instances)", environment);
            int index = 0;
            for (int z = -8; z <= 245; z += 12)
            {
                float sway = Mathf.Sin(z * 0.31f) * 0.7f;
                GameObject left = InstantiatePrefab(seaGrassPrefab, decorations, scene, $"Sea Grass L {index:00}");
                left.transform.position = new Vector3(-8.2f + sway, 0f, z);
                GameObject right = InstantiatePrefab(seaGrassPrefab, decorations, scene, $"Sea Grass R {index:00}");
                right.transform.position = new Vector3(8.2f - sway, 0f, z + 4f);
                right.transform.localScale = new Vector3(0.82f, 0.82f, 0.82f);
                if (index % 4 == 0)
                {
                    GameObject bubbles = InstantiatePrefab(bubblePrefab, decorations, scene, $"Bubble Column {index / 4 + 1:00}");
                    bubbles.transform.position = new Vector3(index % 8 == 0 ? -7.3f : 7.3f, 0.05f, z + 6f);
                    bubbles.transform.localScale = Vector3.one * 0.045f;
                }
                if (shortPipePrefabs != null && shortPipePrefabs.Length > 0 && index % 3 == 1)
                {
                    GameObject shortPipe = InstantiatePrefab(
                        shortPipePrefabs[(index / 3) % shortPipePrefabs.Length], decorations, scene,
                        $"CC0 Short Pipe {index / 3 + 1:00}");
                    bool placeLeft = index % 2 == 0;
                    shortPipe.transform.position = new Vector3(placeLeft ? -8.75f : 8.75f, 1.15f, z + 2f);
                    shortPipe.transform.rotation = Quaternion.Euler(
                        placeLeft ? 0f : 180f, 0f, placeLeft ? 12f : -12f);
                }
                index++;
            }
        }

        private static GateWall[] BuildGates(Transform environment, GameObject pipePrefab, Scene scene)
        {
            Transform root = Group("04_Pipe Gates (Prefab Instances)", environment);
            var gates = new List<GateWall>(FlappyBoidsGame.TotalGates);
            var random = new System.Random(7331);
            Vector2 previousCenter = new Vector2(0f, 5.5f);
            for (int i = 0; i < FlappyBoidsGame.TotalGates; i++)
            {
                float z = 23f + i * 18f;
                float x = i == 0 ? 0f : Mathf.Lerp(previousCenter.x,
                    Mathf.Lerp(-3.25f, 3.25f, (float)random.NextDouble()), 0.72f);
                float y = i == 0 ? 5.5f : Mathf.Lerp(previousCenter.y,
                    Mathf.Lerp(3.25f, 8.65f, (float)random.NextDouble()), 0.72f);
                GameObject instance = InstantiatePrefab(pipePrefab, root, scene, $"Pipe Gate {i + 1:00}");
                instance.transform.position = new Vector3(x, y, z);
                GateWall gate = instance.GetComponent<GateWall>();
                gate.SetHoleDiameter(6.1f);
                gates.Add(gate);
                previousCenter = new Vector2(x, y);
            }
            return gates.ToArray();
        }

        private static void BuildFinish(Transform environment, Materials materials)
        {
            Transform finish = Group("05_Finish", environment);
            float z = 23f + (FlappyBoidsGame.TotalGates - 1) * 18f + 8f;
            CreatePart(PrimitiveType.Cube, "Finish Left", finish,
                new Vector3(-6.5f, 4f, z), new Vector3(0.4f, 8f, 0.4f), materials.FishGold, false);
            CreatePart(PrimitiveType.Cube, "Finish Right", finish,
                new Vector3(6.5f, 4f, z), new Vector3(0.4f, 8f, 0.4f), materials.FishGold, false);
            CreatePart(PrimitiveType.Cube, "Finish Beam", finish,
                new Vector3(0f, 8f, z), new Vector3(13.4f, 0.4f, 0.4f), materials.FishGold, false);
        }

        private static FlappyBoidsHud BuildHud(Transform gameRoot, Camera camera)
        {
            Transform uiRoot = Group("05_UI", gameRoot);
            var canvasObject = new GameObject("Gameplay HUD Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(uiRoot, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            FlappyBoidsHud hud = canvasObject.AddComponent<FlappyBoidsHud>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Color panelColor = new Color(0.018f, 0.045f, 0.085f, 0.90f);
            Color cyan = new Color(0.48f, 0.92f, 1f, 1f);
            Color body = new Color(0.85f, 0.92f, 1f, 1f);
            Color gold = new Color(1f, 0.76f, 0.22f, 1f);

            RectTransform schoolPanel = CreateUiPanel(
                "School Counter", canvasObject.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(137f, -63f), new Vector2(230f, 82f), panelColor);
            CreateUiText("Label", schoolPanel, "SCHOOL REMAINING", font, 14, cyan,
                TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(210f, 24f), FontStyle.Bold);
            Text schoolCount = CreateUiText("Value", schoolPanel, $"{BoidSwarm.StartingBoids:00} / {BoidSwarm.StartingBoids}",
                font, 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(210f, 42f), FontStyle.Bold);

            RectTransform pipesPanel = CreateUiPanel(
                "Pipe Counter", canvasObject.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-137f, -63f), new Vector2(230f, 82f), panelColor);
            CreateUiText("Label", pipesPanel, "PIPES CLEARED", font, 14, cyan,
                TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(210f, 24f), FontStyle.Bold);
            Text pipeCount = CreateUiText("Value", pipesPanel, $"00 / {FlappyBoidsGame.TotalGates}",
                font, 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(210f, 42f), FontStyle.Bold);

            RectTransform routePanel = CreateUiPanel(
                "Route Guidance", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(390f, 68f), panelColor);
            Text routeStatus = CreateUiText("Next Pipe + Flock Fit", routePanel,
                "NEXT PIPE  23m    FLOCK FIT  100%", font, 14, cyan,
                TextAnchor.MiddleCenter, new Vector2(0f, 17f), new Vector2(370f, 24f), FontStyle.Bold);
            RectTransform fitBackground = CreateUiPanel(
                "Flock Fit Bar", routePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -18f), new Vector2(350f, 12f), new Color(0.01f, 0.025f, 0.045f, 0.95f));
            RectTransform fillRect = CreateUiPanel(
                "Fill", fitBackground, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(346f, 8f), new Color(0.12f, 0.95f, 0.78f, 1f));
            Image fitFill = fillRect.GetComponent<Image>();
            fitFill.type = Image.Type.Filled;
            fitFill.fillMethod = Image.FillMethod.Horizontal;
            fitFill.fillOrigin = 0;
            fitFill.fillAmount = 1f;

            RectTransform readyPanel = CreateUiPanel(
                "Ready Panel", canvasObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(620f, 310f), new Color(0.012f, 0.040f, 0.075f, 0.94f));
            CreateUiText("Title", readyPanel, "FLAPPY BOIDS: DEEP RUN", font, 44,
                new Color(0.20f, 0.95f, 0.92f), TextAnchor.MiddleCenter,
                new Vector2(0f, 96f), new Vector2(570f, 64f), FontStyle.Bold);
            CreateUiText("Brief", readyPanel,
                "Guide the whole school through 12 underwater pipes.\nEvery fish that touches a pipe is lost.",
                font, 19, body, TextAnchor.MiddleCenter, new Vector2(0f, 25f), new Vector2(540f, 72f));
            CreateUiText("Controls", readyPanel,
                "LEFT / RIGHT  STEER        SPACE  KICK + SCHOOL UP", font, 19, gold,
                TextAnchor.MiddleCenter, new Vector2(0f, -55f), new Vector2(560f, 38f), FontStyle.Bold);
            CreateUiText("Launch", readyPanel, "PRESS SPACE TO LAUNCH", font, 15, cyan,
                TextAnchor.MiddleCenter, new Vector2(0f, -104f), new Vector2(420f, 30f), FontStyle.Bold);

            RectTransform resultPanel = CreateUiPanel(
                "Result Panel", canvasObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(620f, 350f), new Color(0.012f, 0.040f, 0.075f, 0.95f));
            Text resultTitle = CreateUiText("Title", resultPanel, "SCHOOL MADE IT!", font, 44,
                new Color(0.20f, 0.95f, 0.92f), TextAnchor.MiddleCenter,
                new Vector2(0f, 112f), new Vector2(570f, 60f), FontStyle.Bold);
            Text resultStats = CreateUiText("Run Stats", resultPanel, "PIPES  00     FINAL FISH  00", font, 20,
                gold, TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(540f, 38f), FontStyle.Bold);
            Text resultBest = CreateUiText("Best", resultPanel, "BEST  00 pipes / 00 fish", font, 19,
                body, TextAnchor.MiddleCenter, new Vector2(0f, -18f), new Vector2(520f, 66f));
            CreateUiText("Restart", resultPanel, "SPACE or R  -  SWIM AGAIN", font, 20,
                gold, TextAnchor.MiddleCenter, new Vector2(0f, -114f), new Vector2(500f, 36f), FontStyle.Bold);
            resultPanel.gameObject.SetActive(false);

            hud.ConfigureView(
                schoolCount, pipeCount, routeStatus, fitFill,
                readyPanel.gameObject, resultPanel.gameObject,
                resultTitle, resultStats, resultBest);
            EditorUtility.SetDirty(hud);
            return hud;
        }

        private static RectTransform CreateUiPanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateUiText(
            string name,
            Transform parent,
            string value,
            Font font,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 size,
            FontStyle fontStyle = FontStyle.Normal)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Text text = gameObject.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void ApplyRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.015f, 0.14f, 0.18f);
            RenderSettings.fogStartDistance = 48f;
            RenderSettings.fogEndDistance = 205f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.08f, 0.34f, 0.39f);
            RenderSettings.ambientEquatorColor = new Color(0.025f, 0.17f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.07f, 0.065f);
            RenderSettings.ambientIntensity = 1.15f;
        }

        private static GameObject InstantiatePrefab(
            GameObject prefab, Transform parent, Scene scene, string name)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(parent, true);
            instance.name = name;
            return instance;
        }

        private static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreatePart(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool shadows = true)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadows;
            return part;
        }

        private sealed class Materials
        {
            public Material Pipe;
            public Material PipeAccent;
            public Material Seabed;
            public Material WaterSurface;
            public Material Guide;
            public Material FishBlue;
            public Material FishGold;
            public Material FishCoral;
            public Material FishFin;
            public Material FishEye;
        }
    }
}
