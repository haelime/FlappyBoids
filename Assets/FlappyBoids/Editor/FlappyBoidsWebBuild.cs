using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlappyBoids.Editor
{
    public static class FlappyBoidsWebBuild
    {
        public const string DefaultOutputPath = "Builds/WebGL";

        [MenuItem("Build/Flappy Boids/WebGL Release")]
        public static void BuildWebGlRelease()
        {
            BuildReport report = Build(DefaultOutputPath);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"WebGL build failed with {summary.totalErrors} errors: {summary.result}");

            Debug.Log(
                $"[FlappyBoids WebGL] Release build succeeded: {summary.totalSize} bytes in {summary.totalTime}.");
        }

        // Invoke with -executeMethod FlappyBoids.Editor.FlappyBoidsWebBuild.BuildWebGlCi.
        public static void BuildWebGlCi()
        {
            try
            {
                BuildReport report = Build(ReadOutputArgument());
                BuildSummary summary = report.summary;
                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError(
                        $"[FlappyBoids WebGL] Build failed: {summary.totalErrors} errors, {summary.result}.");
                    EditorApplication.Exit(1);
                    return;
                }

                Debug.Log(
                    $"[FlappyBoids WebGL] Build succeeded: {summary.totalSize} bytes in {summary.totalTime}.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ConfigureWebGlReleaseSettings()
        {
            NamedBuildTarget web = NamedBuildTarget.WebGL;
            PlayerSettings.SetApiCompatibilityLevel(web, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(web, Il2CppCodeGeneration.OptimizeSize);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.wasm2023 = true;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
        }

        private static BuildReport Build(string outputPath)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException(
                    "Unity WebGL Build Support is not installed for this Editor version.");

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scene exists in Editor Build Settings.");

            ConfigureWebGlReleaseSettings();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Could not switch the active build target to WebGL.");

            Directory.CreateDirectory(outputPath);
            return BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
        }

        private static string ReadOutputArgument()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-webOutput");
            return index >= 0 && index + 1 < arguments.Length
                ? arguments[index + 1]
                : DefaultOutputPath;
        }
    }
}
