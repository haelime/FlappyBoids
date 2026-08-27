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
        private const string UiPrefabFolder = Root + "/Prefabs/UI";
        private const string SceneFolder = Root + "/Scenes";
        private const string ScenePath = SceneFolder + "/FlappyBoids.unity";
        private const string WaterMaterialPath =
            Root + "/ThirdParty/UberStylizedWater/Template Materials/UWa-Template-Murky.mat";
        private const string BubblePrefabPath =
            Root + "/ThirdParty/URPUnderwaterEffects/Prefabs/BubblesZone.prefab";
        private const string AmbiencePath =
            Root + "/Audio/Ambience/Underwater_Theme_II_CC0.ogg";
        private const string IdleBodyFontPath =
            Root + "/Imported/IdleTogetherUI/Fonts/FusionPixel10Korean.ttf";
        private const string IdleHeadingFontPath =
            Root + "/Imported/IdleTogetherUI/Fonts/FusionPixel12Latin.ttf";
        private const string IdleFrameSpritePath =
            Root + "/Imported/IdleTogetherUI/Textures/FrameBlueDoubleBorder.png";
        private const string IdlePanelSpritePath =
            Root + "/Imported/IdleTogetherUI/Textures/PanelGrayRelief.png";
        private const string ExternalAssetSceneMarker = "01_Marching Cubes Infinite Sea";

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
            GameObject gatePrefab = CreateMarchingCubesGatePrefab(materials);
            GameObject uiFramePrefab = CreateIdleTogetherUiFramePrefab();
            GameObject bubblePrefab = LoadRequiredAsset<GameObject>(BubblePrefabPath);
            CreateScene(materials, fishPrefabs, gatePrefab, bubblePrefab, uiFramePrefab);
            DeleteLegacyEnvironmentPrefabs();
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
            if (bubbleInstances != 1)
                throw new InvalidDataException(
                    $"Expected only the camera bubble prefab instance, but found {bubbleInstances} in {ScenePath}.");
        }

        private static void DeleteLegacyEnvironmentPrefabs()
        {
            string[] legacyAssets =
            {
                EnvironmentPrefabFolder + "/P_CircularPipeGate.prefab",
                EnvironmentPrefabFolder + "/P_SeaGrass.prefab",
                EnvironmentPrefabFolder + "/P_ShortPipe_Straight_CC0.prefab",
                EnvironmentPrefabFolder + "/P_ShortPipe_Corner_CC0.prefab"
            };
            foreach (string asset in legacyAssets)
                if (AssetDatabase.LoadMainAssetAtPath(asset) != null) AssetDatabase.DeleteAsset(asset);
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root + "/Art");
            EnsureFolder(MaterialFolder);
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(FishPrefabFolder);
            EnsureFolder(EnvironmentPrefabFolder);
            EnsureFolder(UiPrefabFolder);
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
                PipeAccent = CreateMaterial("M_Pipe_SafetyRing", new Color(0.85f, 0.20f, 0.03f), 0.06f, false, true),
                Rock = CreateMaterial("M_Rock_Basalt", new Color(0.105f, 0.125f, 0.115f), 0.025f),
                Seabed = CreateMaterial("M_Seabed", new Color(0.16f, 0.17f, 0.135f), 0.035f),
                WaterSurface = LoadRequiredAsset<Material>(WaterMaterialPath),
                Guide = CreateMaterial("M_Guide_Bioluminescent", new Color(0.29f, 0.68f, 0.42f), 0.22f, true),
                FishBlue = CreateMaterial("M_Fish_Blue", new Color(0.20f, 0.58f, 0.82f), 0.34f),
                FishGold = CreateMaterial("M_Fish_Gold", new Color(1.0f, 0.74f, 0.26f), 0.34f),
                FishCoral = CreateMaterial("M_Fish_Coral", new Color(1.0f, 0.39f, 0.22f), 0.34f),
                FishFin = CreateMaterial("M_Fish_Fin", new Color(0.04f, 0.16f, 0.18f), 0.36f),
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

        private static Material CreateMaterial(
            string name, Color color, float smoothness, bool emission = false, bool unlit = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = unlit
                ? Shader.Find("Universal Render Pipeline/Unlit")
                : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
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
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.65f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else if (material.HasProperty("_EmissionColor"))
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
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

        private static GameObject CreateMarchingCubesGatePrefab(Materials materials)
        {
            var root = new GameObject("P_MarchingCubesRockGate");
            GateWall gate = root.AddComponent<GateWall>();
            MarchingCubesGateVisual visual = root.AddComponent<MarchingCubesGateVisual>();
            visual.Configure(materials.Rock, materials.PipeAccent);
            gate.SetHoleDiameter(6.1f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root,
                $"{EnvironmentPrefabFolder}/P_MarchingCubesRockGate.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateIdleTogetherUiFramePrefab()
        {
            Sprite frameSprite = LoadRequiredAsset<Sprite>(IdleFrameSpritePath);
            Sprite panelSprite = LoadRequiredAsset<Sprite>(IdlePanelSpritePath);

            var root = new GameObject(
                "P_IdleTogetherPixelFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(620f, 350f);
            ConfigureSlicedImage(
                root.GetComponent<Image>(), frameSprite, new Color(0.28f, 0.35f, 0.32f, 1f));

            RectTransform content = CreateUiImage(
                "Content Backdrop", rootRect, panelSprite, new Color(0.055f, 0.09f, 0.105f, 0.98f));
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(12f, 12f);
            content.offsetMax = new Vector2(-12f, -58f);

            RectTransform title = CreateUiImage(
                "Title Band", rootRect, panelSprite, new Color(0.72f, 0.31f, 0.12f, 1f));
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = Vector2.one;
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -10f);
            title.sizeDelta = new Vector2(-20f, 48f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root, $"{UiPrefabFolder}/P_IdleTogetherPixelFrame.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateScene(
            Materials materials,
            GameObject[] fishPrefabs,
            GameObject gatePrefab,
            GameObject bubblePrefab,
            GameObject uiFramePrefab)
        {
            if (File.Exists(ScenePath))
            {
                UpdateExistingSceneEnvironment(materials, gatePrefab);
                return;
            }

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
            cameraObject.transform.position = new Vector3(0f, 4.30f, -9.4f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.35f, 13.9f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 59f;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 330f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.09f, 0.105f);
            cameraObject.AddComponent<AudioListener>();
            FlappyBoidsCamera followCamera = cameraObject.AddComponent<FlappyBoidsCamera>();
            GameObject cameraBubbles = InstantiatePrefab(
                bubblePrefab, cameraObject.transform, scene, "Camera Bubble Drift");
            cameraBubbles.transform.localPosition = new Vector3(0f, -2.5f, 5.5f);
            cameraBubbles.transform.localScale = Vector3.one * 0.035f;

            var diveLightObject = new GameObject("School Dive Light");
            diveLightObject.transform.SetParent(cameraObject.transform, false);
            Light diveLight = diveLightObject.AddComponent<Light>();
            diveLight.type = LightType.Spot;
            diveLight.color = new Color(0.72f, 0.82f, 0.68f);
            diveLight.intensity = 18f;
            diveLight.range = 18f;
            diveLight.spotAngle = 58f;
            diveLight.innerSpotAngle = 38f;
            diveLight.shadows = LightShadows.None;

            Transform lighting = Group("03_Lighting", gameRoot.transform);
            var lightObject = new GameObject("Course Light");
            lightObject.transform.SetParent(lighting, false);
            lightObject.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.66f, 0.82f, 0.72f);
            sun.intensity = 0.88f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            Transform environment = Group("04_Environment", gameRoot.transform);
            BuildMarchingCubesSea(environment, materials);
            GateWall[] gates = BuildGates(environment, gatePrefab, scene);
            FlappyBoidsHud hud = BuildHud(gameRoot.transform, camera, uiFramePrefab);

            game.ConfigureScene(swarm, followCamera, audio, hud, gates);
            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(swarm);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = gameRoot;
        }

        private static void UpdateExistingSceneEnvironment(Materials materials, GameObject gatePrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FlappyBoidsGame game = Object.FindAnyObjectByType<FlappyBoidsGame>(FindObjectsInactive.Include);
            if (game == null)
                throw new InvalidDataException(
                    $"The existing scene has no FlappyBoidsGame root and cannot be upgraded safely: {ScenePath}");

            Transform existingEnvironment = game.transform.Find("04_Environment");
            if (existingEnvironment != null) Object.DestroyImmediate(existingEnvironment.gameObject);

            Transform environment = Group("04_Environment", game.transform);
            BuildMarchingCubesSea(environment, materials);
            GateWall[] gates = BuildGates(environment, gatePrefab, scene);

            BoidSwarm swarm = game.GetComponentInChildren<BoidSwarm>(true);
            FlappyBoidsCamera followCamera = game.GetComponentInChildren<FlappyBoidsCamera>(true);
            FlappyBoidsAudio audio = game.GetComponentInChildren<FlappyBoidsAudio>(true);
            FlappyBoidsHud hud = game.GetComponentInChildren<FlappyBoidsHud>(true);
            if (swarm == null || followCamera == null || audio == null || hud == null)
                throw new InvalidDataException(
                    "The existing scene is missing a gameplay component. UI was left untouched; repair the scene references before rebuilding.");

            game.ConfigureScene(swarm, followCamera, audio, hud, gates);
            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(swarm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = game.gameObject;
        }

        private static void BuildMarchingCubesSea(Transform environment, Materials materials)
        {
            Transform root = Group("01_Marching Cubes Infinite Sea", environment);
            InfiniteSeaTerrain terrain = root.gameObject.AddComponent<InfiniteSeaTerrain>();
            const int chunkCount = 12;
            var chunks = new MarchingCubesSeaChunk[chunkCount];
            for (int i = 0; i < chunkCount; i++)
            {
                int chunkIndex = i - 1;
                Transform chunkRoot = Group($"Sea Chunk {i + 1:00} [MC {chunkIndex}]", root);
                MarchingCubesSeaChunk chunk = chunkRoot.gameObject.AddComponent<MarchingCubesSeaChunk>();
                chunk.Configure(chunkIndex, materials.Seabed);
                GameObject waterSurface = CreatePart(
                    PrimitiveType.Plane,
                    "Uber Water Surface (MIT Asset)",
                    chunkRoot,
                    new Vector3(0f, GateWall.CorridorHeight + 18f, 0f),
                    new Vector3(2.5f, 1f, MarchingCubesSeaChunk.DefaultLength / 10f),
                    materials.WaterSurface,
                    false);
                waterSurface.transform.localRotation = Quaternion.identity;
                chunks[i] = chunk;
            }
            terrain.Configure(chunks);
            EditorUtility.SetDirty(terrain);
        }

        private static GateWall[] BuildGates(Transform environment, GameObject gatePrefab, Scene scene)
        {
            Transform root = Group("02_Marching Cubes Rock Gates (Prefab Instances)", environment);
            var gates = new List<GateWall>(FlappyBoidsGame.GatePoolSize);
            Vector2 previousCenter = new Vector2(0f, 5.5f);
            for (int i = 0; i < FlappyBoidsGame.GatePoolSize; i++)
            {
                float z = 23f + i * FlappyBoidsGame.DefaultGateSpacing;
                Vector2 center = FlappyBoidsGame.CalculateHoleCenter(i, previousCenter);
                GameObject instance = InstantiatePrefab(gatePrefab, root, scene, $"Rock Gate {i + 1:00}");
                instance.transform.position = new Vector3(center.x, center.y, z);
                GateWall gate = instance.GetComponent<GateWall>();
                gate.SetHoleDiameter(FlappyBoidsGame.CalculateHoleDiameter(i));
                gates.Add(gate);
                previousCenter = center;
            }
            return gates.ToArray();
        }

        private static FlappyBoidsHud BuildHud(
            Transform gameRoot, Camera camera, GameObject uiFramePrefab)
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

            Font font = LoadRequiredAsset<Font>(IdleBodyFontPath);
            Font headingFont = LoadRequiredAsset<Font>(IdleHeadingFontPath);
            Sprite panelSprite = LoadRequiredAsset<Sprite>(IdlePanelSpritePath);
            Color panelColor = new Color(0.055f, 0.085f, 0.10f, 0.96f);
            Color primary = new Color(0.91f, 0.87f, 0.74f, 1f);
            Color secondary = new Color(0.60f, 0.68f, 0.59f, 1f);
            Color accent = new Color(1f, 0.44f, 0.16f, 1f);
            Color amber = new Color(0.95f, 0.66f, 0.24f, 1f);

            RectTransform schoolPanel = CreateUiPanel(
                "School Counter", canvasObject.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(137f, -63f), new Vector2(230f, 82f), panelColor, panelSprite);
            CreateUiText("Label", schoolPanel, "SCHOOL REMAINING", font, 14, secondary,
                TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(210f, 24f), FontStyle.Bold);
            Text schoolCount = CreateUiText("Value", schoolPanel, $"{BoidSwarm.StartingBoids:00} / {BoidSwarm.StartingBoids}",
                font, 28, primary, TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(210f, 42f), FontStyle.Bold);

            RectTransform pipesPanel = CreateUiPanel(
                "Pipe Counter", canvasObject.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-137f, -63f), new Vector2(230f, 82f), panelColor, panelSprite);
            CreateUiText("Label", pipesPanel, "PIPES CLEARED", font, 14, secondary,
                TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(210f, 24f), FontStyle.Bold);
            Text pipeCount = CreateUiText("Value", pipesPanel, "00",
                font, 28, primary, TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(210f, 42f), FontStyle.Bold);

            RectTransform routePanel = CreateUiPanel(
                "Route Guidance", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(470f, 68f), panelColor, panelSprite);
            Text routeStatus = CreateUiText("Next Pipe + Flock Fit", routePanel,
                "NEXT  23m    HOLE  6.1m    FIT  100%", font, 17, primary,
                TextAnchor.MiddleCenter, new Vector2(0f, 17f), new Vector2(450f, 24f), FontStyle.Bold);
            RectTransform fitBackground = CreateUiPanel(
                "Flock Fit Bar", routePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -18f), new Vector2(430f, 12f), new Color(0.01f, 0.025f, 0.045f, 0.95f));
            RectTransform fillRect = CreateUiPanel(
                "Fill", fitBackground, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(426f, 8f), new Color(0.36f, 0.66f, 0.43f, 1f));
            Image fitFill = fillRect.GetComponent<Image>();
            fitFill.type = Image.Type.Filled;
            fitFill.fillMethod = Image.FillMethod.Horizontal;
            fitFill.fillOrigin = 0;
            fitFill.fillAmount = 1f;

            RectTransform readyPanel = InstantiateUiFrame(
                uiFramePrefab, canvasObject.transform, "Ready Panel - Infinite Course v3",
                new Vector2(620f, 310f));
            CreateUiText("Title", readyPanel, "FLAPPY BOIDS: DEEP RUN", headingFont, 44,
                primary, TextAnchor.MiddleCenter,
                new Vector2(0f, 96f), new Vector2(570f, 64f), FontStyle.Bold);
            CreateUiText("Brief", readyPanel,
                "Swim through an endless chain of underwater pipes.\nThe opening narrows to 3.8m. Every collision costs one fish.",
                font, 19, primary, TextAnchor.MiddleCenter, new Vector2(0f, 25f), new Vector2(540f, 72f));
            CreateUiText("Controls", readyPanel,
                "LEFT / RIGHT  STEER        SPACE  KICK + SCHOOL UP", font, 19, accent,
                TextAnchor.MiddleCenter, new Vector2(0f, -55f), new Vector2(560f, 38f), FontStyle.Bold);
            CreateUiText("Launch", readyPanel, "PRESS SPACE TO LAUNCH", font, 15, amber,
                TextAnchor.MiddleCenter, new Vector2(0f, -104f), new Vector2(420f, 30f), FontStyle.Bold);

            RectTransform resultPanel = InstantiateUiFrame(
                uiFramePrefab, canvasObject.transform, "Result Panel - Infinite Course v3",
                new Vector2(620f, 350f));
            Text resultTitle = CreateUiText("Title", resultPanel, "RUN ENDED", headingFont, 44,
                primary, TextAnchor.MiddleCenter,
                new Vector2(0f, 112f), new Vector2(570f, 60f), FontStyle.Bold);
            Text resultStats = CreateUiText("Run Stats", resultPanel, "PIPES  00     FINAL FISH  00", font, 20,
                accent, TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(540f, 38f), FontStyle.Bold);
            Text resultBest = CreateUiText("Best", resultPanel, "BEST  00 pipes / 00 fish", font, 19,
                primary, TextAnchor.MiddleCenter, new Vector2(0f, -18f), new Vector2(520f, 66f));
            CreateUiText("Restart", resultPanel, "SPACE or R  -  SWIM AGAIN", font, 20,
                amber, TextAnchor.MiddleCenter, new Vector2(0f, -114f), new Vector2(500f, 36f), FontStyle.Bold);
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
            Color color,
            Sprite sprite = null)
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
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 3f;
            }
            return rect;
        }

        private static RectTransform InstantiateUiFrame(
            GameObject prefab, Transform parent, string name, Vector2 size)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform CreateUiImage(
            string name, Transform parent, Sprite sprite, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            ConfigureSlicedImage(gameObject.GetComponent<Image>(), sprite, color);
            return rect;
        }

        private static void ConfigureSlicedImage(Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 3f;
            image.color = color;
            image.raycastTarget = false;
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
            RenderSettings.fogColor = new Color(0.018f, 0.11f, 0.12f);
            RenderSettings.fogStartDistance = 48f;
            RenderSettings.fogEndDistance = 205f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.34f, 0.31f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.20f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.09f, 0.07f);
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
            public Material PipeAccent;
            public Material Rock;
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
