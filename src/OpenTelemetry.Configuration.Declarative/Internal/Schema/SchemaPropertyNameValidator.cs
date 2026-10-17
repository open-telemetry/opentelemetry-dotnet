// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Validates document property names against this package's pinned configuration schema.
/// </summary>
internal static class SchemaPropertyNameValidator
{
    internal const string ExperimentalReason = "the property is experimental";

    internal const string NewerFileFormatReason = "the document declares a newer schema version";

    /// <summary>
    /// Rejects keys the pinned schema does not define where it forbids additional keys.
    /// </summary>
    /// <remarks>
    /// Keys the schema does not define are retained, and returned, in three cases: at the document
    /// root, in a document that declares a newer schema version, and under a property that is under
    /// development. Type and value constraints are not checked.
    /// </remarks>
    /// <param name="properties">The document-rooted properties.</param>
    /// <param name="fileFormat">The validated <c>file_format</c> of the document.</param>
    /// <returns>The undefined keys that were retained, in document order.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// A stable key the schema does not define is present where the schema forbids additional keys.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The schema contains an unrecognized additional-property kind.
    /// </exception>
    internal static IReadOnlyList<UndefinedProperty> Validate(
        ConfigProperties properties,
        FileFormatVersion fileFormat) =>
        Validate(properties, fileFormat, ConfigurationSchema.Pinned.Root);

    /// <summary>
    /// Validates <paramref name="properties"/> against <paramref name="root"/>.
    /// </summary>
    /// <param name="properties">The document-rooted properties.</param>
    /// <param name="fileFormat">The validated <c>file_format</c> of the document.</param>
    /// <param name="root">The schema root to validate against.</param>
    /// <returns>The undefined keys that were retained, in document order.</returns>
    internal static IReadOnlyList<UndefinedProperty> Validate(
        ConfigProperties properties,
        FileFormatVersion fileFormat,
        SchemaNode root)
    {
        var walk = new Walk(fileFormat);
        walk.VisitMapping(properties, root, SchemaStability.Stable, string.Empty);
        walk.ThrowIfRejected();
        return walk.Retained;
    }

    /// <summary>
    /// Reports retained undefined keys.
    /// </summary>
    /// <param name="retained">The keys returned by <see cref="Validate(ConfigProperties, FileFormatVersion)"/>.</param>
    /// <param name="fileFormat">The validated <c>file_format</c> of the document.</param>
    /// <exception cref="InvalidOperationException">
    /// A retained property has an unknown or unrecognized retention kind.
    /// </exception>
    internal static void ReportRetained(IReadOnlyList<UndefinedProperty> retained, FileFormatVersion fileFormat)
    {
        foreach (var property in retained)
        {
            switch (property.Kind)
            {
                case UndefinedPropertyKind.Experimental:
                    LogRetained(property.Path, ExperimentalReason);
                    break;
                case UndefinedPropertyKind.NewerFileFormat:
                    LogRetained(property.Path, NewerFileFormatReason);
                    break;
                case UndefinedPropertyKind.Root:
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.UndefinedRootPropertyRetained(property.Path);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled {nameof(UndefinedPropertyKind)}: {property.Kind}.");
            }
        }

        void LogRetained(string path, string reason) =>
            OpenTelemetryDeclarativeConfigurationEventSource.Log.UndefinedPropertyRetained(
                path,
                fileFormat.Value,
                ConfigurationSchema.Version,
                reason);
    }

    private static bool Admits(SchemaNode node, SchemaValueTypes type) =>
        node.ValueTypes == SchemaValueTypes.None || (node.ValueTypes & type) != 0;

    private static string Child(string parent, string key)
    {
        var name = key.Length == 0 ? "<empty>" : key;
        return parent.Length == 0 ? name : YamlPath.Child(parent, name);
    }

    private readonly record struct VisitKey(object Instance, SchemaNode Node, SchemaStability Stability);

    private readonly record struct RejectedProperty(string Path, ConfigValuePosition Position);

    private sealed class Walk(FileFormatVersion fileFormat)
    {
        private readonly HashSet<VisitKey> visited = [];

        private readonly List<RejectedProperty> rejected = [];

        internal List<UndefinedProperty> Retained { get; } = [];

        internal void VisitMapping(ConfigProperties mapping, SchemaNode node, SchemaStability stability, string path)
        {
            // A node shared through a YAML anchor is visited once per schema node and stability.
            if (!this.visited.Add(new(mapping, node, stability)))
            {
                return;
            }

            var keys = mapping.Keys
                .OrderBy(key => mapping.GetKeyPosition(key).Line)
                .ThenBy(key => mapping.GetKeyPosition(key).Column);

            foreach (var key in keys)
            {
                _ = mapping.TryGetValue(key, out var value);

                var childPath = Child(path, key);
                var childStability = stability == SchemaStability.Experimental || ConfigurationSchema.IsExperimentalPropertyName(key)
                    ? SchemaStability.Experimental
                    : SchemaStability.Stable;

                if (node.Properties.TryGetValue(key, out var property))
                {
                    this.VisitValue(value, property, childStability, childPath);
                    continue;
                }

                switch (node.AdditionalProperties)
                {
                    case AdditionalPropertiesKind.Allowed:
                        if (path.Length == 0)
                        {
                            this.Retained.Add(new(childPath, UndefinedPropertyKind.Root));
                        }

                        break;
                    case AdditionalPropertiesKind.Forbidden:
                        this.Undefined(mapping, key, childPath, childStability);
                        break;
                    case AdditionalPropertiesKind.Schema:
                        this.VisitValue(value, node.AdditionalPropertiesSchema!, childStability, childPath);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unhandled {nameof(AdditionalPropertiesKind)}: {node.AdditionalProperties}.");
                }
            }
        }

        internal void ThrowIfRejected()
        {
            if (this.rejected.Count == 0)
            {
                return;
            }

            foreach (var (path, position) in this.rejected)
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.UndefinedConfigurationProperty(path, position.Line, position.Column);
            }

            var (firstPath, firstPosition) = this.rejected[0];
            var message =
                $"Property '{firstPath}' (line {firstPosition.Line.ToString(CultureInfo.InvariantCulture)}, " +
                $"column {firstPosition.Column.ToString(CultureInfo.InvariantCulture)}) is not defined by " +
                $"OpenTelemetry configuration schema {ConfigurationSchema.Version} and is not permitted at this location.";

            var others = this.rejected.Count - 1;
            if (others > 0)
            {
                message += $" {others.ToString(CultureInfo.InvariantCulture)} more undefined " +
                    $"{(others == 1 ? "property was" : "properties were")} reported through EventSource.";
            }

            throw new DeclarativeConfigurationException(message);
        }

        private void Undefined(ConfigProperties mapping, string key, string path, SchemaStability stability)
        {
            if (fileFormat.IsNewerMinorVersion)
            {
                this.Retained.Add(new(path, UndefinedPropertyKind.NewerFileFormat));
            }
            else if (stability == SchemaStability.Experimental)
            {
                this.Retained.Add(new(path, UndefinedPropertyKind.Experimental));
            }
            else
            {
                this.rejected.Add(new(path, mapping.GetKeyPosition(key)));
            }
        }

        private void VisitValue(ConfigValue value, SchemaNode node, SchemaStability stability, string path)
        {
            if (node.HasOneOf)
            {
                return;
            }

            switch (value.Kind)
            {
                case ConfigValueKind.Mapping when Admits(node, SchemaValueTypes.Object):
                    this.VisitMapping(value.AsMapping(), node, stability, path);
                    break;
                case ConfigValueKind.Sequence when Admits(node, SchemaValueTypes.Array) && node.Items is { } items:
                    this.VisitSequence(value.AsSequence(), items, stability, path);
                    break;
            }
        }

        private void VisitSequence(IReadOnlyList<ConfigValue> sequence, SchemaNode items, SchemaStability stability, string path)
        {
            if (!this.visited.Add(new(sequence, items, stability)))
            {
                return;
            }

            for (var i = 0; i < sequence.Count; i++)
            {
                this.VisitValue(sequence[i], items, stability, YamlPath.Index(path, i));
            }
        }
    }
}
