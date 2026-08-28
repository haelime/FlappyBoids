using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private const string ProfileFolder = Root + "/Art/Profiles";
        private const string SceneFolder = Root + "/Scenes";
        private const string ScenePath = SceneFolder + "/FlappyBoids.unity";
        private const string DeepOceanVolumePath = ProfileFolder + "/VP_DeepOcean.asset";
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
        private const string BlueFrameSpritePath =
            Root + "/Imported/IdleTogetherUI/Textures/FrameBlueDoubleBorder.png";
        private const string BluePanelSpritePath =
            Root + "/Imported/IdleTogetherUI/Textures/PanelBlueRelief.png";
        private const string BlueModalPrefabPath = UiPrefabFolder + "/P_BlueCurrentModal.prefab";
        private const string BlueHudPlatePrefabPath = UiPrefabFolder + "/P_BlueCurrentHudPlate.prefab";
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
            GameObject uiFramePrefab = CreateBlueCurrentModalPrefab();
            GameObject hudCardPrefab = CreateBlueCurrentHudPlatePrefab();
            VolumeProfile volumeProfile = CreateDeepOceanVolumeProfile();
            GameObject bubblePrefab = LoadRequiredAsset<GameObject>(BubblePrefabPath);
            CreateScene(
                materials, fishPrefabs, gatePrefab, bubblePrefab,
                uiFramePrefab, hudCardPrefab, volumeProfile);
            DeleteLegacyEnvironmentPrefabs();
            DeleteLegacyUiPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateScenePrefabReferences();
            Debug.Log($"FlappyBoids authored assets rebuilt: {ScenePath}");
        }

        [MenuItem("Tools/Flappy Boids/Rebuild Authored HUD")]
        public static void RebuildAuthoredHud()
        {
            EnsureFolders();
            GameObject modalPrefab = CreateBlueCurrentModalPrefab();
            GameObject cardPrefab = CreateBlueCurrentHudPlatePrefab();
            VolumeProfile volumeProfile = CreateDeepOceanVolumeProfile();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FlappyBoidsGame game = Object.FindAnyObjectByType<FlappyBoidsGame>(FindObjectsInactive.Include);
            Camera camera = Object.FindAnyObjectByType<FlappyBoidsCamera>(FindObjectsInactive.Include)
                ?.GetComponent<Camera>();
            if (game == null || camera == null)
                throw new System.InvalidOperationException("The authored game root or camera is missing.");

            Transform oldUi = game.transform.Find("05_UI");
            if (oldUi != null) Object.DestroyImmediate(oldUi.gameObject);
            FlappyBoidsHud hud = BuildHud(game.transform, camera, modalPrefab, cardPrefab);
            ApplyUnderwaterLook(game.transform, camera, volumeProfile);
            var serializedGame = new SerializedObject(game);
            serializedGame.FindProperty("_hud").objectReferenceValue = hud;
            serializedGame.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            DeleteLegacyUiPrefabs();
            AssetDatabase.SaveAssets();
            ValidateScenePrefabReferences();
            Debug.Log($"FlappyBoids authored HUD rebuilt: {ScenePath}");
        }

        public static void RebuildAuthoredHudBatch() => RebuildAuthoredHud();

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

        private static void DeleteLegacyUiPrefabs()
        {
            string[] legacyAssets =
            {
                UiPrefabFolder + "/P_IdleTogetherPixelFrame.prefab",
                UiPrefabFolder + "/P_UnderwaterHudCard.prefab"
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
            EnsureFolder(ProfileFolder);
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

        private static GameObject CreateBlueCurrentModalPrefab()
        {
            Sprite frameSprite = LoadRequiredAsset<Sprite>(BlueFrameSpritePath);
            Sprite panelSprite = LoadRequiredAsset<Sprite>(BluePanelSpritePath);

            var root = new GameObject(
                "P_BlueCurrentModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(820f, 470f);
            ConfigureSlicedImage(
                root.GetComponent<Image>(), frameSprite, new Color(0.08f, 0.42f, 0.66f, 1f));

            RectTransform content = CreateUiImage(
                "Deep Blue Glass", rootRect, panelSprite, new Color(0.012f, 0.075f, 0.15f, 0.985f));
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(12f, 12f);
            content.offsetMax = new Vector2(-12f, -12f);

            RectTransform currentLine = CreateUiPanel(
                "Current Line", rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(690f, 4f),
                new Color(0.15f, 0.62f, 0.86f, 0.92f));
            currentLine.pivot = new Vector2(0.5f, 1f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BlueModalPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBlueCurrentHudPlatePrefab()
        {
            Sprite frameSprite = LoadRequiredAsset<Sprite>(BlueFrameSpritePath);
            Sprite panelSprite = LoadRequiredAsset<Sprite>(BluePanelSpritePath);
            var root = new GameObject(
                "P_BlueCurrentHudPlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(360f, 88f);
            ConfigureSlicedImage(
                root.GetComponent<Image>(), frameSprite, new Color(0.06f, 0.35f, 0.60f, 0.96f));

            RectTransform inset = CreateUiImage(
                "Pelagic Glass", rootRect, panelSprite, new Color(0.01f, 0.07f, 0.14f, 0.96f));
            inset.anchorMin = Vector2.zero;
            inset.anchorMax = Vector2.one;
            inset.offsetMin = new Vector2(6f, 6f);
            inset.offsetMax = new Vector2(-6f, -6f);

            RectTransform accent = CreateUiPanel(
                "Current Index", rootRect, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(9f, 0f), new Vector2(5f, -18f), new Color(0.14f, 0.62f, 0.86f, 1f));
            accent.pivot = new Vector2(0f, 0.5f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BlueHudPlatePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static VolumeProfile CreateDeepOceanVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DeepOceanVolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, DeepOceanVolumePath);
            }

            ColorAdjustments color = GetOrAddVolumeComponent<ColorAdjustments>(profile);
            color.postExposure.Override(0.12f);
            color.contrast.Override(8f);
            color.colorFilter.Override(new Color(0.84f, 0.96f, 1f, 1f));
            color.saturation.Override(-6f);

            Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile);
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(0.34f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(0.38f, 0.72f, 1f, 1f));
            bloom.highQualityFiltering.Override(true);

            Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile);
            vignette.color.Override(new Color(0.002f, 0.025f, 0.075f, 1f));
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.62f);
            vignette.rounded.Override(false);

            Tonemapping tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            profile.Reset();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void CreateScene(
            Materials materials,
            GameObject[] fishPrefabs,
            GameObject gatePrefab,
            GameObject bubblePrefab,
            GameObject uiFramePrefab,
            GameObject hudCardPrefab,
            VolumeProfile volumeProfile)
        {
            if (File.Exists(ScenePath))
            {
                UpdateExistingSceneEnvironment(
                    materials, gatePrefab, uiFramePrefab, hudCardPrefab, volumeProfile);
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
            FlappyBoidsHud hud = BuildHud(gameRoot.transform, camera, uiFramePrefab, hudCardPrefab);
            ApplyUnderwaterLook(gameRoot.transform, camera, volumeProfile);

            game.ConfigureScene(swarm, followCamera, audio, hud, gates);
            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(swarm);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = gameRoot;
        }

        private static void UpdateExistingSceneEnvironment(
            Materials materials,
            GameObject gatePrefab,
            GameObject uiFramePrefab,
            GameObject hudCardPrefab,
            VolumeProfile volumeProfile)
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
            Camera camera = followCamera != null ? followCamera.GetComponent<Camera>() : null;
            if (swarm == null || followCamera == null || audio == null || camera == null)
                throw new InvalidDataException(
                    "The existing scene is missing a gameplay component and cannot be rebuilt safely.");

            Transform existingUi = game.transform.Find("05_UI");
            if (existingUi != null) Object.DestroyImmediate(existingUi.gameObject);
            FlappyBoidsHud hud = BuildHud(game.transform, camera, uiFramePrefab, hudCardPrefab);
            ApplyUnderwaterLook(game.transform, camera, volumeProfile);

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
            Transform gameRoot, Camera camera, GameObject modalPrefab, GameObject cardPrefab)
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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            canvasObject.AddComponent<GraphicRaycaster>();
            FlappyBoidsHud hud = canvasObject.AddComponent<FlappyBoidsHud>();

            Font font = LoadRequiredAsset<Font>(IdleBodyFontPath);
            Font headingFont = LoadRequiredAsset<Font>(IdleHeadingFontPath);
            Sprite panelSprite = LoadRequiredAsset<Sprite>(BluePanelSpritePath);
            Color abyss = new Color(0.005f, 0.035f, 0.09f, 0.96f);
            Color deepBlue = new Color(0.012f, 0.09f, 0.18f, 0.97f);
            Color pelagicBlue = new Color(0.055f, 0.34f, 0.58f, 1f);
            Color currentBlue = new Color(0.14f, 0.62f, 0.86f, 1f);
            Color mutedBlue = new Color(0.43f, 0.69f, 0.82f, 1f);
            Color iceBlue = new Color(0.80f, 0.94f, 1f, 1f);

            RectTransform safeArea = CreateUiRect(
                "Safe Area", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<FlappyBoidsSafeArea>();
            RectTransform gameplayLayer = CreateUiRect(
                "Gameplay Layer", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform topRail = CreateUiRect(
                "Top HUD Rail", gameplayLayer, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -24f), new Vector2(-56f, 92f));
            topRail.pivot = new Vector2(0.5f, 1f);
            var topLayout = topRail.gameObject.AddComponent<HorizontalLayoutGroup>();
            topLayout.spacing = 16f;
            topLayout.childAlignment = TextAnchor.UpperCenter;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = true;

            RectTransform schoolCard = InstantiateHudCard(
                cardPrefab, topRail, "School Status Plate", 270f, 0f);
            CreateUiText("Label", schoolCard, "SCHOOL", font, 14, mutedBlue,
                TextAnchor.MiddleCenter, new Vector2(0f, 21f), new Vector2(246f, 22f), FontStyle.Bold);
            Text schoolCount = CreateUiText(
                "Count", schoolCard, $"{BoidSwarm.StartingBoids:00} / {BoidSwarm.StartingBoids}",
                headingFont, 29, iceBlue, TextAnchor.MiddleCenter,
                new Vector2(0f, -12f), new Vector2(246f, 42f), FontStyle.Bold);

            RectTransform gatesCard = InstantiateHudCard(
                cardPrefab, topRail, "Gate Progress Plate", 220f, 0f);
            CreateUiText("Label", gatesCard, "GATES", font, 14, mutedBlue,
                TextAnchor.MiddleCenter, new Vector2(0f, 21f), new Vector2(196f, 22f), FontStyle.Bold);
            Text gatesCount = CreateUiText(
                "Count", gatesCard, "00", headingFont, 29, iceBlue,
                TextAnchor.MiddleCenter, new Vector2(0f, -12f), new Vector2(196f, 42f), FontStyle.Bold);

            RectTransform passageCard = InstantiateHudCard(
                cardPrefab, topRail, "Passage Telemetry Plate", 610f, 0f);
            CreateUiText("Next Label", passageCard, "NEXT", font, 12, mutedBlue,
                TextAnchor.MiddleCenter, new Vector2(-205f, 22f), new Vector2(104f, 20f), FontStyle.Bold);
            Text nextDistance = CreateUiText("Next Distance", passageCard, "18 m", headingFont, 20, iceBlue,
                TextAnchor.MiddleCenter, new Vector2(-205f, -5f), new Vector2(112f, 30f), FontStyle.Bold);
            CreateUiText("Aperture Label", passageCard, "OPENING", font, 12, mutedBlue,
                TextAnchor.MiddleCenter, new Vector2(-58f, 22f), new Vector2(126f, 20f), FontStyle.Bold);
            Text aperture = CreateUiText("Aperture", passageCard, "6.1 m", headingFont, 20, iceBlue,
                TextAnchor.MiddleCenter, new Vector2(-58f, -5f), new Vector2(126f, 30f), FontStyle.Bold);
            CreateUiText("Fit Label", passageCard, "SCHOOL FIT", font, 12, mutedBlue,
                TextAnchor.MiddleCenter, new Vector2(150f, 22f), new Vector2(132f, 20f), FontStyle.Bold);
            Text fitPercent = CreateUiText("Fit Percent", passageCard, "100%", headingFont, 20, iceBlue,
                TextAnchor.MiddleCenter, new Vector2(150f, -5f), new Vector2(112f, 30f), FontStyle.Bold);
            RectTransform fitBackground = CreateUiPanel(
                "School Fit Bar", passageCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(150f, -29f), new Vector2(150f, 7f), abyss);
            RectTransform fillRect = CreateUiPanel(
                "Fill", fitBackground, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(-4f, 3f), currentBlue);
            Image fitFill = fillRect.GetComponent<Image>();
            fitFill.type = Image.Type.Filled;
            fitFill.fillMethod = Image.FillMethod.Horizontal;
            fitFill.fillOrigin = 0;
            fitFill.fillAmount = 1f;

            RectTransform readyLayer = CreateUiRect(
                "Ready Layer", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateUiPanel(
                "Abyss Screen Wash", readyLayer, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.002f, 0.025f, 0.07f, 0.52f));
            RectTransform readyPanel = InstantiateUiFrame(
                modalPrefab, readyLayer, "Start Card", new Vector2(860f, 500f));
            CreateUiText("Title", readyPanel, "FLAPPY BOIDS", headingFont, 62,
                iceBlue, TextAnchor.MiddleCenter, new Vector2(0f, 142f), new Vector2(760f, 82f), FontStyle.Bold);
            CreateUiPanel(
                "Title Divider", readyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 86f), new Vector2(540f, 3f), pelagicBlue);

            RectTransform steerControl = CreateUiPanel(
                "Steer Control", readyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 28f), new Vector2(650f, 58f), deepBlue, panelSprite);
            CreateUiText("Key", steerControl, "LEFT / RIGHT", headingFont, 21, currentBlue,
                TextAnchor.MiddleCenter, new Vector2(-190f, 0f), new Vector2(230f, 34f), FontStyle.Bold);
            CreateUiText("Action", steerControl, "STEER THE SCHOOL", font, 18, iceBlue,
                TextAnchor.MiddleLeft, new Vector2(135f, 0f), new Vector2(330f, 34f), FontStyle.Bold);

            RectTransform kickControl = CreateUiPanel(
                "Kick Control", readyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -48f), new Vector2(650f, 58f), deepBlue, panelSprite);
            CreateUiText("Key", kickControl, "SPACE", headingFont, 21, currentBlue,
                TextAnchor.MiddleCenter, new Vector2(-190f, 0f), new Vector2(230f, 34f), FontStyle.Bold);
            CreateUiText("Action", kickControl, "KICK UP + FORM TIGHT", font, 18, iceBlue,
                TextAnchor.MiddleLeft, new Vector2(135f, 0f), new Vector2(330f, 34f), FontStyle.Bold);

            CreateUiText("Launch", readyPanel, "PRESS ANY BUTTON", font, 22, currentBlue,
                TextAnchor.MiddleCenter, new Vector2(0f, -158f), new Vector2(560f, 38f), FontStyle.Bold);

            RectTransform resultLayer = CreateUiRect(
                "Result Layer", safeArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateUiPanel(
                "Abyss Screen Wash", resultLayer, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0.002f, 0.025f, 0.07f, 0.62f));
            RectTransform resultPanel = InstantiateUiFrame(
                modalPrefab, resultLayer, "Result Card", new Vector2(820f, 410f));
            Text resultTitle = CreateUiText("Title", resultPanel, "RUN OVER", headingFont, 48,
                currentBlue, TextAnchor.MiddleCenter, new Vector2(0f, 108f), new Vector2(700f, 68f), FontStyle.Bold);
            Text resultStats = CreateUiText("Run Stats", resultPanel, "GATES  00     FINAL SCHOOL  00", font, 21,
                iceBlue, TextAnchor.MiddleCenter, new Vector2(0f, 30f), new Vector2(650f, 42f), FontStyle.Bold);
            Text resultBest = CreateUiText("Best", resultPanel, "BEST  00 gates / 00 fish", font, 18,
                mutedBlue, TextAnchor.MiddleCenter, new Vector2(0f, -32f), new Vector2(620f, 64f));
            CreateUiText("Restart", resultPanel, "SPACE / R  TO DIVE AGAIN", font, 18,
                currentBlue, TextAnchor.MiddleCenter, new Vector2(0f, -128f), new Vector2(580f, 34f), FontStyle.Bold);
            resultLayer.gameObject.SetActive(false);

            hud.ConfigureView(
                schoolCount, gatesCount, nextDistance, aperture, fitPercent, fitFill,
                gameplayLayer.gameObject,
                readyLayer.gameObject, resultLayer.gameObject,
                resultTitle, resultStats, resultBest);
            gameplayLayer.gameObject.SetActive(false);
            EditorUtility.SetDirty(hud);
            return hud;
        }

        private static RectTransform CreateUiRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform InstantiateHudCard(
            GameObject prefab, Transform parent, string name, float preferredWidth, float flexibleWidth)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(preferredWidth, 88f);
            LayoutElement layout = instance.AddComponent<LayoutElement>();
            layout.minWidth = Mathf.Min(260f, preferredWidth);
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = flexibleWidth;
            layout.minHeight = 88f;
            layout.preferredHeight = 88f;
            return rect;
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

        private static void ApplyUnderwaterLook(
            Transform gameRoot, Camera camera, VolumeProfile volumeProfile)
        {
            ApplyRenderSettings();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.006f, 0.06f, 0.14f, 1f);
            camera.farClipPlane = 240f;
            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            Transform lighting = gameRoot.Find("03_Lighting");
            if (lighting == null) lighting = Group("03_Lighting", gameRoot);
            Transform oldVolume = lighting.Find("Deep Ocean Global Volume");
            if (oldVolume != null) Object.DestroyImmediate(oldVolume.gameObject);

            var volumeObject = new GameObject("Deep Ocean Global Volume");
            volumeObject.transform.SetParent(lighting, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            volume.weight = 1f;
            volume.sharedProfile = volumeProfile;

            Light courseLight = lighting.Find("Course Light")?.GetComponent<Light>();
            if (courseLight != null)
            {
                courseLight.color = new Color(0.43f, 0.72f, 1f, 1f);
                courseLight.intensity = 1.34f;
                courseLight.shadows = LightShadows.Soft;
                RenderSettings.sun = courseLight;
                EditorUtility.SetDirty(courseLight);
            }

            Light schoolLight = camera.transform.Find("School Dive Light")?.GetComponent<Light>();
            if (schoolLight != null)
            {
                schoolLight.color = new Color(0.25f, 0.65f, 1f, 1f);
                schoolLight.intensity = 23f;
                schoolLight.range = 24f;
                schoolLight.spotAngle = 62f;
                schoolLight.innerSpotAngle = 40f;
                EditorUtility.SetDirty(schoolLight);
            }

            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(cameraData);
            EditorUtility.SetDirty(volume);
        }

        private static void ApplyRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.018f, 0.16f, 0.30f);
            RenderSettings.fogStartDistance = 7f;
            RenderSettings.fogEndDistance = 112f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.45f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.28f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.025f, 0.11f, 0.23f);
            RenderSettings.ambientIntensity = 1.34f;
            RenderSettings.reflectionIntensity = 0.74f;
            RenderSettings.subtractiveShadowColor = new Color(0.005f, 0.025f, 0.07f);
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
