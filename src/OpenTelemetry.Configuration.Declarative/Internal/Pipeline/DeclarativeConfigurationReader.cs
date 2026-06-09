// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// DeclarativeConfigurationException carries the OTEL1006 experimental attribute.
// Suppress once here rather than at every throw site.
#pragma warning disable OTEL1006

using System.Collections.ObjectModel;
using Microsoft.Extensions.Configuration;
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
    /// and converts it into a <see cref="ReadOnlyDictionary{TKey, TValue}"/> as flat declarative configuration keys.
    /// </summary>
    /// <param name="filePath">The <see cref="FilePath"/> for the YAML file to be read.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <c>file_format</c> is missing or unsupported, or when an invalid <c>${...}</c>
    /// substitution reference is encountered, or when the document root is not a YAML mapping.
    /// </exception>
    /// <exception cref="YamlDotNet.Core.YamlException">
    /// Thrown when the input is not valid YAML (propagates from <see cref="YamlStream.Load(TextReader)"/>).
    /// Callers that surface this to end users (e.g. <see cref="IConfigurationProvider.Load"/>)
    /// should catch and wrap it as a <see cref="DeclarativeConfigurationException"/>.
    /// </exception>
    /// <returns>A <see cref="ReadOnlyDictionary{TKey, TValue}"/> containing the flat declarative configuration.</returns>
    internal static ReadOnlyDictionary<string, string?> Read(FilePath filePath)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        using var fileStream = File.OpenRead(filePath.Path);
        using var reader = new StreamReader(fileStream);

        var stream = new YamlStream();
        stream.Load(reader);

        if (stream.Documents.Count == 0)
        {
            // Empty file is a no-op in overlay mode; informational event for diagnostics.
            OpenTelemetryDeclarativeConfigurationEventSource.Log.EmptyConfigurationFile(filePath.ToString());
            return new ReadOnlyDictionary<string, string?>(data);
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

        // Phase 1: validate file_format.
        var rawFileFormat = root.GetScalarString(YamlKeys.FileFormat);
        var fileFormat = FileFormatValidator.Validate(
            rawFileFormat,
            OpenTelemetryDeclarativeConfigurationEventSource.Log.FileFormatWarning);

        // Phase 2: walk the AST into the typed model.
        var config = DeclarativeConfigurationParser.Parse(root, fileFormat);

        LogUnknownTopLevelSections(root);

        // Phase 3: project the typed model into flat keys.
        DeclarativeConfigurationConverter.Convert(config, data);

        return new ReadOnlyDictionary<string, string?>(data);
    }

    // Logs each top-level section that this package does not recognise (lenient validation).
    private static void LogUnknownTopLevelSections(YamlMappingNode root)
    {
        foreach (var entry in root.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.UnknownConfigurationSection("<non-scalar key>");
                continue;
            }

            var key = keyNode.Value;
            if (key is not null && KnownTopLevelKeys.Contains(key))
            {
                continue;
            }

            var display = key is null ? "<null>" : key.Length == 0 ? "<empty>" : key;
            OpenTelemetryDeclarativeConfigurationEventSource.Log.UnknownConfigurationSection(display);
        }
    }
}
