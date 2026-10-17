// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The OpenTelemetry configuration schema this package understands.
/// </summary>
internal sealed class ConfigurationSchema
{
    /// <summary>
    /// The semantic version of the pinned schema.
    /// </summary>
    internal const string Version = "1.2.0";

    /// <summary>
    /// The tag the pinned schema was taken from.
    /// </summary>
    internal const string SourceTag = "v1.2.0";

    /// <summary>
    /// The commit the pinned schema was taken from.
    /// </summary>
    internal const string SourceCommit = "3b04ae78576ec407854d734eb4da4f78a67f902c";

    /// <summary>
    /// The location of the pinned schema file.
    /// </summary>
    internal const string SourceUrl = "https://github.com/open-telemetry/opentelemetry-configuration/blob/v1.2.0/opentelemetry_configuration.json";

    /// <summary>
    /// The name of the embedded resource holding the pinned schema.
    /// </summary>
    internal const string ResourceName = "OpenTelemetry.Configuration.Declarative.Schema.opentelemetry_configuration.json";

    private static readonly Lazy<ConfigurationSchema> PinnedSchema =
        new(() => new(ConfigurationSchemaReader.Read(OpenPinnedResource())), LazyThreadSafetyMode.ExecutionAndPublication);

    private ConfigurationSchema(SchemaNode root)
    {
        this.Root = root;
    }

    /// <summary>
    /// Gets the pinned schema, which is read on first use.
    /// </summary>
    internal static ConfigurationSchema Pinned => PinnedSchema.Value;

    /// <summary>
    /// Gets the root node of the schema.
    /// </summary>
    internal SchemaNode Root { get; }

    /// <summary>
    /// Determines whether a property name marks the property as under development.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>
    /// <see langword="true"/> if the name ends in <c>/development</c>, <c>/alpha</c> or <c>/beta</c>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsExperimentalPropertyName(string name) =>
        name.EndsWith("/development", StringComparison.Ordinal)
        || name.EndsWith("/alpha", StringComparison.Ordinal)
        || name.EndsWith("/beta", StringComparison.Ordinal);

    private static Stream OpenPinnedResource() =>
        typeof(ConfigurationSchema).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' was not found.");
}
