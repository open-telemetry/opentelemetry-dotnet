// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using System.Collections.ObjectModel;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

internal static class DeclarativeConfigurationReader
{
    // Top-level keys this package interprets. Anything else is logged, and retained in the
    // document-rooted properties without being interpreted.
#if NET
    private static readonly FrozenSet<string> KnownTopLevelKeys = FrozenSet.ToFrozenSet(
        [YamlKeys.FileFormat, YamlKeys.Disabled, YamlKeys.Resource],
        StringComparer.Ordinal);
#else
    private static readonly HashSet<string> KnownTopLevelKeys = new(StringComparer.Ordinal)
    {
        YamlKeys.FileFormat,
        YamlKeys.Disabled,
        YamlKeys.Resource,
    };
#endif

    /// <summary>
    /// Opens <paramref name="filePath"/>, validates <c>file_format</c>, parses the typed model,
    /// and returns a <see cref="DeclarativeConfigurationDocument"/>.
    /// </summary>
    /// <param name="filePath">The <see cref="FilePath"/> for the YAML file to be read.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <c>file_format</c> is missing or unsupported, when an invalid <c>${...}</c>
    /// substitution reference is encountered, when the document root is not a YAML mapping, or when
    /// any part of the document is not representable in the configuration data model, or collection
    /// nesting exceeds the depth limit.
    /// </exception>
    /// <exception cref="YamlException">
    /// Thrown when the input is not valid YAML (propagates from <see cref="YamlStream.Load(IParser)"/>).
    /// </exception>
    /// <returns>A <see cref="DeclarativeConfigurationDocument"/> containing the typed model, flat keys, and document properties.</returns>
    internal static DeclarativeConfigurationDocument Read(FilePath filePath) =>
        Read(filePath, Environment.GetEnvironmentVariable);

    /// <summary>
    /// <inheritdoc cref="Read(FilePath)" path="/summary"/>
    /// </summary>
    /// <param name="filePath"><inheritdoc cref="Read(FilePath)" path="/param[@name='filePath']"/></param>
    /// <param name="resolveVariable">
    /// Returns the value of a named environment variable, or <see langword="null"/> if not set.
    /// </param>
    /// <returns><inheritdoc cref="Read(FilePath)" path="/returns"/></returns>
    internal static DeclarativeConfigurationDocument Read(FilePath filePath, Func<string, string?> resolveVariable)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        using var fileStream = File.OpenRead(filePath.Path);
        using var reader = new StreamReader(fileStream);

        var stream = new YamlStream();
        var documentTrackingParser = new YamlDocumentTrackingParser(new Parser(reader));
        stream.Load(documentTrackingParser);

        if (stream.Documents.Count == 0)
        {
            // An empty file contributes no keys; emit an informational event for diagnostics.
            OpenTelemetryDeclarativeConfigurationEventSource.Log.EmptyConfigurationFile(filePath.DisplayPath);

            return new DeclarativeConfigurationDocument(
                new DeclarativeConfiguration(string.Empty),
                new ReadOnlyDictionary<string, string?>(data),
                ConfigProperties.Empty);
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

        root.EnsureCoreCollectionTag(YamlPath.Root);

        YamlDepthValidator.ThrowIfExceeded(root);

        // Reject unsupported YAML 1.1 syntax before interpreting any part of the document.
        YamlMergeKeyValidator.ThrowIfPresent(root);

        var context = new YamlParseContext(resolveVariable, documentTrackingParser);

        _ = context.ResolveMappingKeys(root, YamlPath.Root);

        // Validate file_format before reading the rest of the document so a YAML number is rejected
        // before the package falls through to the generic missing-file_format path.
        var rawFileFormat = new YamlPropertyReader(context)
            .ReadString(root, YamlKeys.FileFormat)
            .TryGetValue(out var fmt) ? fmt : null;
        var fileFormat = FileFormatValidator.Validate(
            rawFileFormat,
            OpenTelemetryDeclarativeConfigurationEventSource.Log.FileFormatWarning);

        var properties = YamlNodeConverter.ConvertDocument(root, context);

        var undefinedProperties = SchemaPropertyNameValidator.Validate(properties, fileFormat);

        var config = new DeclarativeConfigurationParser(context).Parse(root, fileFormat.Value);

        // Reported only once the document is accepted, because the events state that the property
        // was retained. A rejected document retained nothing.
        SchemaPropertyNameValidator.ReportRetained(undefinedProperties, fileFormat);

        LogUnrecognizedTopLevelSections(properties);

        DeclarativeConfigurationConverter.Convert(config, data);

        return new DeclarativeConfigurationDocument(
            config,
            new ReadOnlyDictionary<string, string?>(data),
            properties,
            context.ReferencedEnvironmentVariables);
    }

    // Sections the schema defines but this package does not interpret are retained in the
    // document-rooted properties. Keys the schema does not define are reported by the validator.
    private static void LogUnrecognizedTopLevelSections(ConfigProperties properties)
    {
        foreach (var key in properties.Keys)
        {
            if (!KnownTopLevelKeys.Contains(key) && ConfigurationSchema.Pinned.Root.Properties.ContainsKey(key))
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.UnknownConfigurationSection(key);
            }
        }
    }
}
