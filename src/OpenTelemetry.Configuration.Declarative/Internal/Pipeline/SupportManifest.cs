// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Lists the schema paths this package handles, and reports present properties it does not handle.
/// </summary>
internal static class SupportManifest
{
    internal const string ResourceAttributes = $"{YamlKeys.Resource}.{YamlKeys.Attributes}";
    internal const string ResourceAttributesList = $"{YamlKeys.Resource}.{YamlKeys.AttributesList}";
    internal const string ResourceSchemaUrl = $"{YamlKeys.Resource}.{YamlKeys.SchemaUrl}";

    /// <summary>
    /// The schema paths this package applies, partly applies, or delegates to consumers, including
    /// everything below them.
    /// </summary>
    internal static readonly IReadOnlyCollection<string> CoveredPaths =
    [
        YamlKeys.FileFormat,
        YamlKeys.Disabled,
        ResourceAttributes,
        ResourceAttributesList,

        // Partly applied: a merge input that the resource merge can drop.
        ResourceSchemaUrl,

        // Delegated: exposed through DeclarativeConfigurationDocument.Properties, and not interpreted by the SDK.
        YamlKeys.InstrumentationDevelopment,
        YamlKeys.Distribution,
    ];

    /// <summary>
    /// Returns the schema-defined properties present in <paramref name="properties"/> that this package
    /// does not handle. A property is reported as a whole: its nested properties are not reported
    /// separately.
    /// </summary>
    /// <param name="properties">The document-rooted properties.</param>
    /// <returns>The unapplied properties, in document order.</returns>
    internal static IReadOnlyList<UnappliedProperty> EvaluateUnapplied(ConfigProperties properties) =>
        EvaluateUnapplied(properties, ConfigurationSchema.Pinned.Root, CoveredPaths);

    /// <summary>
    /// <inheritdoc cref="EvaluateUnapplied(ConfigProperties)" path="/summary"/>
    /// </summary>
    /// <param name="properties">The document-rooted properties.</param>
    /// <param name="root">The schema root.</param>
    /// <param name="coveredPaths">The covered schema paths.</param>
    /// <returns><inheritdoc cref="EvaluateUnapplied(ConfigProperties)" path="/returns"/></returns>
    internal static IReadOnlyList<UnappliedProperty> EvaluateUnapplied(
        ConfigProperties properties,
        SchemaNode root,
        IEnumerable<string> coveredPaths)
    {
        var walk = new Walk(coveredPaths);
        walk.VisitMapping(properties, root, SchemaStability.Stable, string.Empty, string.Empty);
        return walk.Unapplied;
    }

    private static string Child(string parent, string key) => parent.Length == 0 ? key : YamlPath.Child(parent, key);

    private sealed class Walk
    {
        private readonly HashSet<string> covered;

        // Parents of covered paths (for example "resource" for "resource.attributes"). They are not
        // reported themselves; we look inside them for uncovered siblings.
        private readonly HashSet<string> containers = new(StringComparer.Ordinal);

        // A YAML anchor can place the same node under several paths, and it may be covered under one
        // and not another. Visit it once per manifest path.
        private readonly HashSet<(object Instance, string EntryPath)> visited = [];

        // A node shared through a YAML anchor is reported once per stability, at its first path for that stability.
        private readonly HashSet<(object Instance, SchemaStability Stability)> reported = [];

        public Walk(IEnumerable<string> coveredPaths)
        {
            this.covered = new(coveredPaths, StringComparer.Ordinal);
            foreach (var path in this.covered)
            {
                var segments = path.Split('.');
                var ancestor = segments[0];
                for (var i = 1; i < segments.Length; i++)
                {
                    // A path segment for a sequence ends in "[]", but the sequence value itself is
                    // looked up without it, so strip the suffix.
                    this.containers.Add(ancestor.EndsWith("[]", StringComparison.Ordinal) ? ancestor.Substring(0, ancestor.Length - 2) : ancestor);
                    ancestor += "." + segments[i];
                }
            }
        }

        internal List<UnappliedProperty> Unapplied { get; } = [];

        internal void VisitMapping(ConfigProperties mapping, SchemaNode node, SchemaStability stability, string path, string entryPath)
        {
            if (!this.visited.Add((mapping, entryPath)))
            {
                return;
            }

            // Source order, so that a shared node is reported at its first path.
            var keys = mapping.Keys
                .OrderBy(key => mapping.GetKeyPosition(key).Line)
                .ThenBy(key => mapping.GetKeyPosition(key).Column);

            foreach (var key in keys)
            {
                // Undefined keys are extensions, or have already been reported by the schema validator.
                if (!node.Properties.TryGetValue(key, out var property))
                {
                    continue;
                }

                // entryPath is matched against the manifest (items as "[]"); path is reported to the user (items as "[0]").
                var childEntryPath = Child(entryPath, key);
                if (this.covered.Contains(childEntryPath))
                {
                    continue;
                }

                _ = mapping.TryGetValue(key, out var value);
                var childStability = stability == SchemaStability.Experimental || ConfigurationSchema.IsExperimentalPropertyName(key)
                    ? SchemaStability.Experimental
                    : SchemaStability.Stable;
                var childPath = Child(path, key);

                if (this.containers.Contains(childEntryPath))
                {
                    this.VisitValue(value, property, childStability, childPath, childEntryPath);
                }
                else
                {
                    this.Report(value, childPath, childStability, mapping.GetKeyPosition(key));
                }
            }
        }

        private void VisitValue(ConfigValue value, SchemaNode node, SchemaStability stability, string path, string entryPath)
        {
            if (value.Kind == ConfigValueKind.Mapping)
            {
                this.VisitMapping(value.AsMapping(), node, stability, path, entryPath);
            }
            else if (value.Kind == ConfigValueKind.Sequence
                && node.Items is { } items
                && this.visited.Add((value.AsSequence(), entryPath)))
            {
                var sequence = value.AsSequence();
                for (var i = 0; i < sequence.Count; i++)
                {
                    this.VisitValue(sequence[i], items, stability, YamlPath.Index(path, i), entryPath + "[]");
                }
            }
        }

        // Reports the property as a whole, without looking inside it. A non-empty sequence is reported
        // per item, so each finding points at one item.
        private void Report(ConfigValue value, string path, SchemaStability stability, ConfigValuePosition position)
        {
            object? instance = value.Kind switch
            {
                ConfigValueKind.Mapping => value.AsMapping(),
                ConfigValueKind.Sequence => value.AsSequence(),
                _ => null,
            };

            if (instance is not null && !this.reported.Add((instance, stability)))
            {
                return;
            }

            if (value.Kind == ConfigValueKind.Sequence && value.AsSequence() is { Count: > 0 } sequence)
            {
                for (var i = 0; i < sequence.Count; i++)
                {
                    this.Unapplied.Add(new(YamlPath.Index(path, i), stability, sequence[i].Position));
                }

                return;
            }

            this.Unapplied.Add(new(path, stability, position));
        }
    }
}
