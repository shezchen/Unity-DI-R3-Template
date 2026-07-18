using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Template.Editor.CodeGeneration
{
    internal sealed class GeneratedConstant
    {
        public GeneratedConstant(string identifier, string value)
        {
            Identifier = identifier;
            Value = value;
        }

        public string Identifier { get; }
        public string Value { get; }
    }

    internal static class GeneratedCodeUtility
    {
        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
            "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed",
            "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator",
            "out", "override", "params", "private", "protected", "public", "readonly", "ref",
            "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        public static bool TryCreateConstants(
            IEnumerable<string> rawValues,
            string category,
            out IReadOnlyList<GeneratedConstant> constants,
            out string error)
        {
            var result = new List<GeneratedConstant>();
            var values = new HashSet<string>(StringComparer.Ordinal);
            var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
            var errors = new List<string>();

            foreach (var value in rawValues.OrderBy(item => item, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"{category} contains an empty value.");
                    continue;
                }

                if (!values.Add(value))
                {
                    errors.Add($"{category} contains duplicate value '{value}'.");
                    continue;
                }

                var identifier = SanitizeIdentifier(value);
                if (identifiers.TryGetValue(identifier, out var existing))
                {
                    errors.Add(
                        $"{category} values '{existing}' and '{value}' collide on identifier '{identifier}'.");
                    continue;
                }

                identifiers.Add(identifier, value);
                result.Add(new GeneratedConstant(identifier, value));
            }

            constants = result;
            error = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
            return errors.Count == 0;
        }

        public static string EscapeStringLiteral(string value) => value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");

        public static bool ContentMatches(string path, string expectedContent) =>
            File.Exists(path) && string.Equals(
                NormalizeLineEndings(File.ReadAllText(path)),
                NormalizeLineEndings(expectedContent),
                StringComparison.Ordinal);

        public static bool WriteIfChanged(string path, string content)
        {
            if (ContentMatches(path, content))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content, new UTF8Encoding(false));
            return true;
        }

        private static string SanitizeIdentifier(string value)
        {
            var identifier = Regex.Replace(value, @"[^a-zA-Z0-9_]", "_");
            if (string.IsNullOrEmpty(identifier))
            {
                identifier = "_";
            }

            if (char.IsDigit(identifier[0]) || CSharpKeywords.Contains(identifier))
            {
                identifier = "_" + identifier;
            }

            return identifier;
        }

        private static string NormalizeLineEndings(string value) =>
            value.Replace("\r\n", "\n").Replace("\r", "\n");
    }
}
