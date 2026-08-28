using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyBoids.Editor
{
    public static class FlappyBoidsScreenshotCapture
    {
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;
        private const string DefaultFileName = "FlappyBoids_BlueCurrent_Gameplay.png";

        [MenuItem("Tools/Flappy Boids/Capture Staged Gameplay Screenshot")]
        public static void CaptureStagedGameplay()
        {
            FlappyBoidsSceneBuilder.RebuildAuthoredHud();
            int captureWidth = ResolvePositiveIntArgument("-flappyScreenshotWidth", CaptureWidth);
            int captureHeight = ResolvePositiveIntArgument("-flappyScreenshotHeight", CaptureHeight);

            Camera camera = GameObject.Find("TPS Flock Camera")?.GetComponent<Camera>();
            Canvas canvas = GameObject.Find("Gameplay HUD Canvas")?.GetComponent<Canvas>();
            if (camera == null || canvas == null)
                throw new InvalidOperationException("The authored gameplay camera or HUD canvas is missing.");

            Transform previewRoot = null;
            RenderTexture renderTexture = null;
            Texture2D screenshot = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                StageHud(canvas.transform, HasArgument("-flappyScreenshotReady"));
                previewRoot = StageSchool();
                StageCamera(camera);
                StageBubbles();

                renderTexture = new RenderTexture(
                    captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                    name = "FlappyBoids Gameplay Capture"
                };
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                canvas.worldCamera = camera;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = renderTexture;
                screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0f, 0f, captureWidth, captureHeight), 0, 0);
                screenshot.Apply(false, false);

                string outputPath = ResolveOutputPath();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Application.dataPath);
                File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
                Debug.Log($"FlappyBoids gameplay screenshot saved: {outputPath}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
                if (previewRoot != null) UnityEngine.Object.DestroyImmediate(previewRoot.gameObject);
            }
        }

        private static void StageHud(Transform canvas, bool showReadyScreen)
        {
            SetActive(canvas, "Safe Area/Gameplay Layer", !showReadyScreen);
            SetActive(canvas, "Safe Area/Ready Layer", showReadyScreen);
            SetActive(canvas, "Safe Area/Result Layer", false);
            SetText(canvas, "Safe Area/Gameplay Layer/Top HUD Rail/School Status Plate/Count", "31 / 42");
            SetText(canvas, "Safe Area/Gameplay Layer/Top HUD Rail/Gate Progress Plate/Count", "05");
            SetText(canvas, "Safe Area/Gameplay Layer/Top HUD Rail/Passage Telemetry Plate/Next Distance", "8 m");
            SetText(canvas, "Safe Area/Gameplay Layer/Top HUD Rail/Passage Telemetry Plate/Aperture", "5.7 m");
            SetText(canvas, "Safe Area/Gameplay Layer/Top HUD Rail/Passage Telemetry Plate/Fit Percent", "86%");

            Transform fill = canvas.Find(
                "Safe Area/Gameplay Layer/Top HUD Rail/Passage Telemetry Plate/School Fit Bar/Fill");
            if (fill != null && fill.TryGetComponent(out Image image))
            {
                image.fillAmount = 0.86f;
                image.color = new Color(0.22f, 0.72f, 0.96f, 1f);
            }
        }

        private static Transform StageSchool()
        {
            GameObject[] prefabs =
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FlappyBoids/Prefabs/Fish/P_Fish_Blue.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FlappyBoids/Prefabs/Fish/P_Fish_Gold.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FlappyBoids/Prefabs/Fish/P_Fish_Coral.prefab")
            };
            if (Array.Exists(prefabs, prefab => prefab == null))
                throw new InvalidOperationException("One or more authored fish prefabs are missing.");

            var root = new GameObject("Screenshot Preview School").transform;
            Vector3 center = new Vector3(-0.40f, 5.45f, 15.2f);
            const int fishCount = 31;
            const float goldenAngle = 2.39996323f;
            for (int i = 0; i < fishCount; i++)
            {
                float angle = i * goldenAngle;
                float normalized = (i + 1f) / fishCount;
                float radius = 0.30f + Mathf.Sqrt(normalized) * 1.82f;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.66f,
                    ((i % 7) - 3f) * 0.38f + Mathf.Sin(angle * 0.7f) * 0.20f);

                GameObject fish = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i % prefabs.Length]);
                fish.name = $"Preview Fish {i + 1:00}";
                fish.transform.SetParent(root, false);
                fish.transform.position = center + offset;
                Vector3 heading = new Vector3(
                    Mathf.Sin(angle) * 0.055f,
                    Mathf.Cos(angle * 0.8f) * 0.035f,
                    1f).normalized;
                fish.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
                fish.transform.localScale *= Mathf.Lerp(0.88f, 1.08f, (i % 5) / 4f);
            }
            return root;
        }

        private static void StageCamera(Camera camera)
        {
            Vector3 schoolCenter = new Vector3(-0.40f, 5.45f, 15.2f);
            camera.transform.position = schoolCenter + new Vector3(0.95f, -1.05f, -8.65f);
            camera.transform.rotation = Quaternion.LookRotation(
                schoolCenter + Vector3.forward * 4.35f + Vector3.up * 0.38f - camera.transform.position,
                Vector3.up);
            camera.fieldOfView = 57f;
        }

        private static void StageBubbles()
        {
            foreach (ParticleSystem particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>(
                         FindObjectsInactive.Exclude))
            {
                particles.Simulate(2.8f, true, true, true);
            }
        }

        private static void SetText(Transform root, string path, string value)
        {
            Transform target = root.Find(path);
            if (target != null && target.TryGetComponent(out Text text)) text.text = value;
        }

        private static void SetActive(Transform root, string childName, bool active)
        {
            Transform target = root.Find(childName);
            if (target != null) target.gameObject.SetActive(active);
        }

        private static string ResolveOutputPath()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], "-flappyScreenshotPath", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(arguments[i + 1]);
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "Screenshots", DefaultFileName);
        }

        private static bool HasArgument(string expected) =>
            Array.Exists(Environment.GetCommandLineArgs(),
                argument => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase));

        private static int ResolvePositiveIntArgument(string name, int fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(arguments[i + 1], out int value) && value > 0)
                    return value;
            return fallback;
        }
    }
}
