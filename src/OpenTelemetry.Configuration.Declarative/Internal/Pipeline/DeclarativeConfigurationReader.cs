// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

internal static class DeclarativeConfigurationReader
{
    // Top-level keys this package recognises. Anything else is logged and ignored.
    private static readonly HashSet<string> KnownTopLevelKeys = new(StringComparer.Ordinal)
    {
        YamlKeys.FileFormat,
        YamlKeys.Disabled,
        YamlKeys.Resource,
    };

    /// <summary>
    /// Opens <paramref name="filePath"/>, validates <c>file_format</c>, parses the typed model,
    /// and returns a <see cref="DeclarativeConfigurationDocument"/>.
    /// </summary>
    /// <param name="filePath">The <see cref="FilePath"/> for the YAML file to be read.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <c>file_format</c> is missing or unsupported, or when an invalid <c>${...}</c>
    /// substitution reference is encountered, or when the document root is not a YAML mapping.
    /// </exception>
    /// <exception cref="YamlDotNet.Core.YamlException">
    /// Thrown when the input is not valid YAML (propagates from <see cref="YamlStream.Load(TextReader)"/>).
    /// </exception>
    /// <returns>A <see cref="DeclarativeConfigurationDocument"/> containing the typed model and flat keys.</returns>
    internal static DeclarativeConfigurationDocument Read(FilePath filePath)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        using var fileStream = File.OpenRead(filePath.Path);
        using var reader = new StreamReader(fileStream);

        var stream = new YamlStream();
        stream.Load(reader);

        if (stream.Documents.Count == 0)
        {
            // Empty file is a no-op in overlay mode; informational event for diagnostics.
            OpenTelemetryDeclarativeConfigurationEventSource.Log.EmptyConfigurationFile(filePath.DisplayPath);

            // A document is returned rather than null so that a consumer never has to handle an
            // absent model.
            return new DeclarativeConfigurationDocument(
                new DeclarativeConfiguration(string.Empty),
                new ReadOnlyDictionary<string, string?>(data));
        }

        if (stream.Documents.Count > 1)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.MultipleDocumentsDetected(stream.Documents.Count);
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new DeclarativeConfigurationException(
                "The declarative configuration document root must be a YAML mapping node.");
        }

        root.EnsureCoreCollectionTag("<root>");
        root.EnsureUniqueStringKeys("<root>");

        // Validate file_format. Throw (rather than warn) on a type mismatch: file_format decides
        // how the rest of the document is interpreted, so `file_format: 1.0` (a YAML 1.2 float)
        // must fail fast rather than fall through to the generic "missing file_format" message.
        var rawFileFormat = root
            .ReadString(YamlKeys.FileFormat)
            .TryGetValue(out var fmt) ? fmt : null;
        var fileFormat = FileFormatValidator.Validate(
            rawFileFormat,
            OpenTelemetryDeclarativeConfigurationEventSource.Log.FileFormatWarning);

        var config = DeclarativeConfigurationParser.Parse(root, fileFormat);
        ProcessUnrecognizedTopLevelSections(root);
        DeclarativeConfigurationConverter.Convert(config, data);

        return new DeclarativeConfigurationDocument(config, new ReadOnlyDictionary<string, string?>(data));
    }

    // Root additionalProperties=true: unrecognized top-level keys (schema extras or not-yet-
    // implemented named properties) are legal and ignored. Nested objects under known sections
    // remain strict via YamlNodeReader.EnsureNoUnrecognizedProperties.
    // Spec still requires invalid ${...} syntax anywhere in the document to fail the parse, so each
    // unrecognized section's scalar values are syntax-checked without resolving variables.
    private static void ProcessUnrecognizedTopLevelSections(YamlMappingNode root)
    {
        var visitedNodes = new HashSet<YamlNode>(YamlNodeReferenceEqualityComparer.Instance);

        foreach (var entry in root.Children)
        {
            if (IsKnownTopLevelKey(entry.Key))
            {
                continue;
            }

            LogUnrecognizedTopLevelSection(entry.Key);
            ValidateSubstitutionInNode(entry.Value, visitedNodes);
        }
    }

    private static bool IsKnownTopLevelKey(YamlNode key) =>
        key is YamlScalarNode { Value: { } name } && KnownTopLevelKeys.Contains(name);

    private static void LogUnrecognizedTopLevelSection(YamlNode key)
    {
        var display = key switch
        {
            YamlScalarNode { Value: null } => "<null>",
            YamlScalarNode { Value.Length: 0 } => "<empty>",
            YamlScalarNode { Value: { } name } => name,
            _ => "<non-scalar key>",
        };

        OpenTelemetryDeclarativeConfigurationEventSource.Log.UnknownConfigurationSection(display);
    }

    // Recursively walks a YAML subtree, validating substitution syntax on every scalar VALUE
    // (never on mapping keys, which the spec excludes from substitution). Uses ValidateReferences
    // rather than Substitute so ignored sections do not resolve variables or emit unset/empty
    // diagnostics for configuration this package does not apply.
    private static void ValidateSubstitutionInNode(YamlNode node, HashSet<YamlNode> visitedNodes)
    {
        // YamlDotNet resolves aliases to the anchored node, so the representation is a graph and
        // may contain cycles. Each scalar needs validation only once regardless of alias count.
        if (!visitedNodes.Add(node))
        {
            return;
        }

        switch (node)
        {
            case YamlScalarNode scalar when scalar.Value is not null:
                EnvironmentSubstitution.ValidateReferences(scalar.Value);
                break;

            case YamlMappingNode mapping:
                foreach (var child in mapping.Children)
                {
                    ValidateSubstitutionInNode(child.Value, visitedNodes);
                }

                break;

            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children)
                {
                    ValidateSubstitutionInNode(item, visitedNodes);
                }

                break;

            default:
                break;
        }
    }

    private sealed class YamlNodeReferenceEqualityComparer : IEqualityComparer<YamlNode>
    {
        internal static readonly YamlNodeReferenceEqualityComparer Instance = new();

        bool IEqualityComparer<YamlNode>.Equals(YamlNode? x, YamlNode? y) => ReferenceEquals(x, y);

        int IEqualityComparer<YamlNode>.GetHashCode(YamlNode obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
