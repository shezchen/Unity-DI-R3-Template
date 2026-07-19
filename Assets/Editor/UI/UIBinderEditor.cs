using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;
using UI;

namespace Template.Editor.UIValidation
{
    internal sealed class BinderIssue
    {
        public BinderIssue(UIBinder binder, string message)
        {
            Binder = binder;
            Message = message;
        }

        public UIBinder Binder { get; }
        public string Message { get; }

        public override string ToString() => $"{GetPath(Binder.transform)}: {Message}";

        private static string GetPath(Transform transform)
        {
            var names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", names);
        }
    }

    internal static class UIBinderValidator
    {
        private static readonly string[] ValidPrefixes =
        {
            "Button_",
            "Text_",
            "Image_",
            "Slider_",
            "Toggle_",
            "Input_",
            "Panel_",
            "Object_"
        };

        public static List<BinderIssue> Validate(UIBinder binder)
        {
            var issues = new List<BinderIssue>();
            var serializedBinder = new SerializedObject(binder);
            var widgets = serializedBinder.FindProperty("_widgetList");
            if (widgets == null)
            {
                issues.Add(new BinderIssue(
                    binder,
                    "Serialized field '_widgetList' was not found."));
                return issues;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < widgets.arraySize; index++)
            {
                var element = widgets.GetArrayElementAtIndex(index);
                var id = element.FindPropertyRelative("ID").stringValue;
                var widget = element.FindPropertyRelative("Object").objectReferenceValue as GameObject;
                var label = $"Entry {index}";

                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(new BinderIssue(binder, $"{label} has an empty ID."));
                    continue;
                }

                if (!ids.Add(id))
                {
                    issues.Add(new BinderIssue(binder, $"Duplicate ID '{id}'."));
                }

                if (!ValidPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    issues.Add(new BinderIssue(
                        binder,
                        $"ID '{id}' does not use a supported prefix."));
                }

                if (widget == null)
                {
                    issues.Add(new BinderIssue(
                        binder,
                        $"ID '{id}' has no GameObject reference."));
                    continue;
                }

                if (!widget.transform.IsChildOf(binder.transform))
                {
                    issues.Add(new BinderIssue(
                        binder,
                        $"ID '{id}' references an object outside this UIBinder hierarchy."));
                }

                if (!string.Equals(id, widget.name, StringComparison.Ordinal))
                {
                    issues.Add(new BinderIssue(
                        binder,
                        $"ID '{id}' no longer matches object name '{widget.name}'."));
                }

                if (!HasRequiredComponent(id, widget, out var requiredComponent))
                {
                    issues.Add(new BinderIssue(
                        binder,
                        $"ID '{id}' requires component {requiredComponent}."));
                }
            }

            return issues;
        }

        public static List<BinderIssue> ValidateProject(out int binderCount)
        {
            var issues = new List<BinderIssue>();
            binderCount = 0;

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                foreach (var binder in prefab.GetComponentsInChildren<UIBinder>(true))
                {
                    binderCount++;
                    issues.AddRange(Validate(binder));
                }
            }

            return issues;
        }

        private static bool HasRequiredComponent(
            string id,
            GameObject widget,
            out string requiredComponent)
        {
            if (id.StartsWith("Button_", StringComparison.Ordinal))
            {
                requiredComponent = nameof(Button);
                return widget.GetComponent<Button>() != null;
            }

            if (id.StartsWith("Text_", StringComparison.Ordinal))
            {
                requiredComponent = nameof(TMP_Text);
                return widget.GetComponent<TMP_Text>() != null;
            }

            if (id.StartsWith("Image_", StringComparison.Ordinal))
            {
                requiredComponent = nameof(Image);
                return widget.GetComponent<Image>() != null;
            }

            if (id.StartsWith("Slider_", StringComparison.Ordinal))
            {
                requiredComponent = nameof(Slider);
                return widget.GetComponent<Slider>() != null;
            }

            if (id.StartsWith("Toggle_", StringComparison.Ordinal))
            {
                requiredComponent = nameof(Toggle);
                return widget.GetComponent<Toggle>() != null;
            }

            if (id.StartsWith("Input_", StringComparison.Ordinal))
            {
                requiredComponent = $"{nameof(TMP_InputField)} or {nameof(InputField)}";
                return widget.GetComponent<TMP_InputField>() != null || widget.GetComponent<InputField>() != null;
            }

            requiredComponent = null;
            return true;
        }
    }

    [CustomEditor(typeof(UIBinder))]
    internal sealed class UIBinderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var binder = (UIBinder)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("Auto Bind By Prefix"))
            {
                Undo.RecordObject(binder, "Auto Bind UI Widgets");
                binder.AutoBindByPrefix();
                EditorUtility.SetDirty(binder);
            }

            var issues = UIBinderValidator.Validate(binder);
            EditorGUILayout.Space();

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("UIBinder validation passed.", MessageType.Info);
            }
            else
            {
                foreach (var issue in issues)
                {
                    EditorGUILayout.HelpBox(issue.Message, MessageType.Error);
                }
            }

            if (GUILayout.Button("Validate All UIBinders"))
            {
                UIBinderValidationCommands.ValidateProjectFromMenu();
            }
        }
    }

    internal static class UIBinderValidationCommands
    {
        private const string MenuPath = "Tools/Template/UI/Validate UIBinders";

        [MenuItem(MenuPath)]
        public static void ValidateProjectFromMenu()
        {
            var issues = UIBinderValidator.ValidateProject(out var binderCount);
            foreach (var issue in issues)
            {
                Debug.LogError($"[UIBinderValidation] {issue}", issue.Binder);
            }

            if (issues.Count == 0)
            {
                Debug.Log($"[UIBinderValidation] PASS ({binderCount} binders).");
            }
            else
            {
                Debug.LogError(
                    $"[UIBinderValidation] FAIL ({issues.Count} issues across {binderCount} binders).");
            }
        }
    }

    internal sealed class UIBinderBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = UIBinderValidator.ValidateProject(out _);
            if (errors.Count == 0)
            {
                return;
            }

            var message = new StringBuilder("UIBinder validation failed before build:");
            foreach (var error in errors.Take(20))
            {
                message.AppendLine().Append("- ").Append(error);
            }

            if (errors.Count > 20)
            {
                message.AppendLine().Append($"- ... and {errors.Count - 20} more.");
            }

            throw new BuildFailedException(message.ToString());
        }
    }
}
