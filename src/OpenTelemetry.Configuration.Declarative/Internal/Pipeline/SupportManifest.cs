// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Identifies schema-defined properties for unapplied-property diagnostics.
/// </summary>
/// <remarks>
/// This manifest is not a complete inventory of supported configuration. Paths handled by the
/// package, checked at provider build time, or delegated to consumers are excluded from these
/// diagnostics. Excluding a path does not guarantee that every setting below it is supported.
/// </remarks>
internal static class SupportManifest
{
    internal const string ResourceAttributes = $"{YamlKeys.Resource}.{YamlKeys.Attributes}";
    internal const string ResourceAttributesList = $"{YamlKeys.Resource}.{YamlKeys.AttributesList}";
    internal const string ResourceSchemaUrl = $"{YamlKeys.Resource}.{YamlKeys.SchemaUrl}";
    internal const string TracerProviderProcessors = $"{YamlKeys.TracerProvider}.{YamlKeys.Processors}";
    internal const string TracerProviderConfigurator = $"{YamlKeys.TracerProvider}.{YamlKeys.Development.TracerConfigurator}";
    internal const string TracerProviderSampler = $"{YamlKeys.TracerProvider}.{YamlKeys.Sampler}";
    internal const string TracerProviderRandomIdGenerator = $"{YamlKeys.TracerProvider}.{YamlKeys.IdGenerator}.{YamlKeys.Random}";

    internal const string TracerProviderPrefix = $"{YamlKeys.TracerProvider}.";

    /// <summary>
    /// The schema paths excluded from unapplied-property diagnostics, including everything below them.
    /// </summary>
    /// <remarks>
    /// These paths are applied, partly applied, handled at provider build time, or delegated to
    /// consumers. They do not represent a guarantee of full support for their nested settings.
    /// </remarks>
    internal static readonly IReadOnlyCollection<string> HandledPaths =
    [
        YamlKeys.FileFormat,
        YamlKeys.Disabled,
        YamlKeys.LogLevel,
        ResourceAttributes,
        ResourceAttributesList,

        // Partly applied: a merge input that the resource merge can drop.
        ResourceSchemaUrl,

        // Handled at tracer-provider build time, including plugin resolution and rejection of
        // unsupported component names. Exclude these paths from unapplied-property diagnostics.
        TracerProviderProcessors,
        TracerProviderConfigurator,
        TracerProviderSampler,
        TracerProviderRandomIdGenerator,

        // Delegated: exposed through DeclarativeConfigurationDocument.Properties, and not interpreted by the SDK.
        YamlKeys.Development.Instrumentation,
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
        EvaluateUnapplied(properties, ConfigurationSchema.Pinned.Root, HandledPaths);

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

    /// <summary>
    /// Determines whether an unapplied property should be reported as an error when building the
    /// tracer provider, rather than logged when reading the document.
    /// </summary>
    /// <remarks>
    /// This package builds the <c>tracer_provider</c> section, so any stable setting in that section
    /// that it cannot apply is a configuration error. Reporting the error at build time allows it
    /// to be included with other unsupported settings in a single exception.
    /// </remarks>
    /// <param name="property">The unapplied property.</param>
    /// <returns>
    /// <see langword="true"/> if the property is stable and belongs to the <c>tracer_provider</c>
    /// section; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsReportedByTracerProviderBuild(UnappliedProperty property) =>
        property.Stability == SchemaStability.Stable
        && property.Path.StartsWith(TracerProviderPrefix, StringComparison.Ordinal);

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
            }
            else
            {
                this.Unapplied.Add(new(path, stability, position));
            }
        }
    }
}
