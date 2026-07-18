using System;
using System.IO;
using System.Linq;
using Template.Editor.CodeGeneration;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Template.Editor.Build
{
    public static class ProjectBuildCommands
    {
        private const string WindowsOutputPath = "Builds/Windows/DI-R3-Template.exe";

        [MenuItem("Tools/Template/Build/Packed Addressables")]
        public static void BuildPackedAddressables()
        {
            GeneratedCodeValidationCommands.ValidateAllOrThrow();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException($"Packed Addressables build failed: {result.Error}");
            }

            Debug.Log($"[Build] Packed Addressables completed: {result.OutputPath}");
        }

        [MenuItem("Tools/Template/Build/Windows Player")]
        public static void BuildWindowsPlayer()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("Windows Player build requires at least one enabled scene.");
            }

            var outputDirectory = Path.GetDirectoryName(WindowsOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows Player build failed with result {report.summary.result} and {report.summary.totalErrors} errors.");
            }

            Debug.Log($"[Build] Windows Player completed: {Path.GetFullPath(WindowsOutputPath)}");
        }

        [MenuItem("Tools/Template/Build/Packed Addressables + Windows Player")]
        public static void BuildPackedAddressablesAndWindowsPlayer()
        {
            BuildPackedAddressables();
            BuildWindowsPlayer();
        }
    }
}
