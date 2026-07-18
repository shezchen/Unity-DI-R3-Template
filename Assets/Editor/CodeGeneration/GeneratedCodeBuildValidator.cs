using Template.Editor.Audio;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Template.Editor.CodeGeneration
{
    public static class GeneratedCodeValidationCommands
    {
        [MenuItem("Tools/Template/Code Generation/Validate Generated Code")]
        public static void ValidateAllOrThrow()
        {
            AddressableKeyGenerator.ValidateOrThrow();
            AudioCatalogEditor.ValidateProjectCatalogOrThrow();
            Debug.Log("[CodeGeneration] All generated code is current.");
        }
    }

    internal sealed class GeneratedCodeBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            GeneratedCodeValidationCommands.ValidateAllOrThrow();
            Debug.Log("[CodeGeneration] Generated code freshness validation passed before build.");
        }
    }
}
