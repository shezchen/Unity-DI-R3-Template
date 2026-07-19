using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Architecture.Audio;
using Template.Editor.CodeGeneration;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Template.Editor.Audio
{
    [CustomEditor(typeof(AudioCatalog))]
    public sealed class AudioCatalogEditor : UnityEditor.Editor
    {
        private const string BgmFolder = "Assets/Audio/BGM";
        private const string SfxFolder = "Assets/Audio/SFX";
        private const string ConstantsPath = "Assets/Scripts/Generated/AudioClipName.cs";

        [MenuItem("Tools/Template/Audio/Validate Catalog Freshness")]
        public static void ValidateProjectCatalogOrThrow()
        {
            var catalog = FindProjectCatalog();
            if (!Validate(catalog, true))
            {
                throw new InvalidOperationException("Audio Catalog validation failed. See Console for details.");
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.Space();

            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Auto Generate Index", GUILayout.Height(30f)))
            {
                Generate((AudioCatalog)target);
            }
            GUI.backgroundColor = previousColor;

            if (GUILayout.Button("Validate Catalog", GUILayout.Height(24f)))
            {
                Validate((AudioCatalog)target, true);
            }
        }

        internal static void Generate(AudioCatalog catalog)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AudioCatalog] Addressable Asset Settings not found.");
                return;
            }

            if (!ValidateSourceFolderForGeneration(BgmFolder, settings) ||
                !ValidateSourceFolderForGeneration(SfxFolder, settings))
            {
                Debug.LogError("[AudioCatalog] Generation aborted; source folders contain invalid entries.");
                return;
            }

            var bgm = ScanFolder(BgmFolder, settings, catalog.Music);
            var sfx = ScanFolder(SfxFolder, settings, catalog.Sfx);
            string constantsContent;
            try
            {
                constantsContent = BuildConstantsContent(bgm, sfx);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"[AudioCatalog] Generation aborted. {exception.Message}");
                return;
            }

            Undo.RecordObject(catalog, "Generate Audio Catalog");
            catalog.ReplaceEntries(bgm, sfx);
            GeneratedCodeUtility.WriteIfChanged(ConstantsPath, constantsContent);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate(catalog, false);
            Debug.Log($"[AudioCatalog] Generated {bgm.Count} Music and {sfx.Count} SFX entries.");
        }

        internal static bool Validate(AudioCatalog catalog, bool logSuccess)
        {
            if (catalog == null)
            {
                Debug.LogError("[AudioCatalog] Catalog asset is missing.");
                return false;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[AudioCatalog] Addressable Asset Settings not found.");
                return false;
            }

            var isValid = ValidateEntries("Music", catalog.Music, settings);
            isValid &= ValidateEntries("SFX", catalog.Sfx, settings);
            isValid &= ValidateFreshness(catalog, settings);

            if (isValid && logSuccess)
            {
                Debug.Log($"[AudioCatalog] Validation passed: {catalog.Music.Count} Music, {catalog.Sfx.Count} SFX.");
            }

            return isValid;
        }

        private static IReadOnlyList<AudioCueDefinition> ScanFolder(
            string folderPath,
            AddressableAssetSettings settings,
            IReadOnlyList<AudioCueDefinition> currentEntries)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[AudioCatalog] Folder not found: {folderPath}");
                return Array.Empty<AudioCueDefinition>();
            }

            var entries = new List<AudioCueDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var currentGains = currentEntries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                .GroupBy(entry => entry.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().DefaultGain, StringComparer.Ordinal);

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var addressableEntry = settings.FindAssetEntry(guid);
                if (addressableEntry == null)
                {
                    Debug.LogWarning($"[AudioCatalog] Asset is not Addressable: {path}");
                    continue;
                }

                var id = Path.GetFileNameWithoutExtension(path);
                if (!ids.Add(id))
                {
                    Debug.LogWarning($"[AudioCatalog] Duplicate audio ID '{id}' at {path}; entry skipped.");
                    continue;
                }

                var defaultGain = currentGains.TryGetValue(id, out var existingGain) ? existingGain : 1f;
                entries.Add(new AudioCueDefinition(id, guid, defaultGain));
            }

            return entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
        }

        private static string BuildConstantsContent(
            IReadOnlyList<AudioCueDefinition> bgm,
            IReadOnlyList<AudioCueDefinition> sfx)
        {
            var bgmValid = GeneratedCodeUtility.TryCreateConstants(
                bgm.Select(entry => entry.Id),
                "Music cue IDs",
                out var bgmConstants,
                out var bgmError);
            var sfxValid = GeneratedCodeUtility.TryCreateConstants(
                sfx.Select(entry => entry.Id),
                "SFX cue IDs",
                out var sfxConstants,
                out var sfxError);
            if (!bgmValid || !sfxValid)
            {
                throw new InvalidOperationException(
                    string.Join(Environment.NewLine, new[] { bgmError, sfxError }
                        .Where(message => !string.IsNullOrEmpty(message))));
            }

            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine("// Generated from AudioCatalog source folders. Do not edit manually.");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine("namespace Generated");
            builder.AppendLine("{");
            builder.AppendLine("    public static class AudioClipName");
            builder.AppendLine("    {");
            WriteClass(builder, "BGM", bgmConstants);
            builder.AppendLine();
            WriteClass(builder, "SFX", sfxConstants);
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void WriteClass(
            StringBuilder builder,
            string className,
            IReadOnlyList<GeneratedConstant> constants)
        {
            builder.AppendLine($"        public static class {className}");
            builder.AppendLine("        {");

            foreach (var constant in constants)
            {
                builder.AppendLine(
                    $"            public const string {constant.Identifier} = \"" +
                    $"{GeneratedCodeUtility.EscapeStringLiteral(constant.Value)}\";");
            }

            builder.AppendLine("        }");
        }

        private static bool ValidateEntries(
            string category,
            IReadOnlyList<AudioCueDefinition> entries,
            AddressableAssetSettings settings)
        {
            var isValid = true;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var generatedNames = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    Debug.LogError($"[AudioCatalog] {category}[{index}] has an empty ID.");
                    isValid = false;
                    continue;
                }

                if (!ids.Add(entry.Id))
                {
                    Debug.LogError($"[AudioCatalog] Duplicate {category} ID '{entry.Id}'.");
                    isValid = false;
                }

                if (entry.DefaultGain < 0f)
                {
                    Debug.LogError(
                        $"[AudioCatalog] {category} cue '{entry.Id}' has a negative default gain.");
                    isValid = false;
                }

                GeneratedCodeUtility.TryCreateConstants(
                    new[] { entry.Id }, category, out var generatedConstant, out _);
                var generatedName = generatedConstant[0].Identifier;
                if (!generatedNames.Add(generatedName))
                {
                    Debug.LogError(
                        $"[AudioCatalog] {category} ID '{entry.Id}' collides on generated name '{generatedName}'.");
                    isValid = false;
                }

                var reference = entry.ClipReference;
                if (reference == null || string.IsNullOrEmpty(reference.AssetGUID))
                {
                    Debug.LogError($"[AudioCatalog] {category} cue '{entry.Id}' has no AudioClip reference.");
                    isValid = false;
                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
                if (string.IsNullOrEmpty(path) || AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(AudioClip))
                {
                    Debug.LogError($"[AudioCatalog] {category} cue '{entry.Id}' points to an invalid AudioClip.");
                    isValid = false;
                    continue;
                }

                if (settings.FindAssetEntry(reference.AssetGUID) == null)
                {
                    Debug.LogError($"[AudioCatalog] {category} cue '{entry.Id}' is not Addressable: {path}");
                    isValid = false;
                }
            }

            return isValid;
        }

        private static bool ValidateFreshness(
            AudioCatalog catalog,
            AddressableAssetSettings settings)
        {
            if (!AssetDatabase.IsValidFolder(BgmFolder) || !AssetDatabase.IsValidFolder(SfxFolder))
            {
                Debug.LogError(
                    $"[AudioCatalog] Source folders must exist: '{BgmFolder}' and '{SfxFolder}'.");
                return false;
            }

            var expectedMusic = ScanFolder(BgmFolder, settings, catalog.Music);
            var expectedSfx = ScanFolder(SfxFolder, settings, catalog.Sfx);
            var isValid = true;

            if (!EntriesMatch(catalog.Music, expectedMusic))
            {
                Debug.LogError("[AudioCatalog] Music entries are stale. Run Auto Generate Index.");
                isValid = false;
            }

            if (!EntriesMatch(catalog.Sfx, expectedSfx))
            {
                Debug.LogError("[AudioCatalog] SFX entries are stale. Run Auto Generate Index.");
                isValid = false;
            }

            string expectedConstants;
            try
            {
                expectedConstants = BuildConstantsContent(expectedMusic, expectedSfx);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"[AudioCatalog] Generated constant validation failed. {exception.Message}");
                return false;
            }

            if (!GeneratedCodeUtility.ContentMatches(ConstantsPath, expectedConstants))
            {
                Debug.LogError($"[AudioCatalog] Generated constants are stale: {ConstantsPath}");
                isValid = false;
            }

            isValid &= ValidateAllSourceClipsAreAddressable(BgmFolder, settings);
            isValid &= ValidateAllSourceClipsAreAddressable(SfxFolder, settings);
            return isValid;
        }

        private static bool EntriesMatch(
            IReadOnlyList<AudioCueDefinition> actual,
            IReadOnlyList<AudioCueDefinition> expected)
        {
            if (actual.Count != expected.Count)
            {
                return false;
            }

            for (var index = 0; index < actual.Count; index++)
            {
                var actualEntry = actual[index];
                var expectedEntry = expected[index];
                if (actualEntry == null || expectedEntry == null ||
                    !string.Equals(actualEntry.Id, expectedEntry.Id, StringComparison.Ordinal) ||
                    actualEntry.ClipReference == null || expectedEntry.ClipReference == null ||
                    !string.Equals(
                        actualEntry.ClipReference.AssetGUID,
                        expectedEntry.ClipReference.AssetGUID,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateAllSourceClipsAreAddressable(
            string folderPath,
            AddressableAssetSettings settings)
        {
            var isValid = true;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath }))
            {
                if (settings.FindAssetEntry(guid) != null)
                {
                    continue;
                }

                Debug.LogError(
                    $"[AudioCatalog] AudioClip is not Addressable: {AssetDatabase.GUIDToAssetPath(guid)}");
                isValid = false;
            }

            return isValid;
        }

        private static bool ValidateSourceFolderForGeneration(
            string folderPath,
            AddressableAssetSettings settings)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError($"[AudioCatalog] Source folder not found: {folderPath}");
                return false;
            }

            var isValid = true;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var id = Path.GetFileNameWithoutExtension(path);
                if (!ids.Add(id))
                {
                    Debug.LogError($"[AudioCatalog] Duplicate source cue ID '{id}' in {folderPath}.");
                    isValid = false;
                }

                if (settings.FindAssetEntry(guid) == null)
                {
                    Debug.LogError($"[AudioCatalog] Source AudioClip is not Addressable: {path}");
                    isValid = false;
                }
            }

            return isValid;
        }

        private static AudioCatalog FindProjectCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:AudioCatalog");
            if (guids.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one AudioCatalog asset, found {guids.Length}.");
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<AudioCatalog>(path);
        }

    }
}
