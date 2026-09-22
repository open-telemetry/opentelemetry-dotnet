// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An <see cref="IResourceDetector"/> that reads <c>resource.attributes</c> and <c>resource.schema_url</c>
/// from the declarative configuration document and applies them to the SDK resource.
/// </summary>
internal sealed partial class DeclarativeResourceDetector : IResourceDetector, IResourceSchemaUrlOverrideProvider
{
    // Per the OTel attribute naming spec: starts with a letter or underscore,
    // followed by letters, digits, underscores, hyphens, or dots.
    private const string AttributeNamePatternString = @"^[a-zA-Z_][-a-zA-Z0-9_.]*$";

#if !NET
    private static readonly Regex AttributeNamePatternInstance = new(
        AttributeNamePatternString,
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromSeconds(1));
#endif

    private readonly DeclarativeConfigurationDocumentAccessor accessor;
    private bool schemaUrlOverrideSet;
    private string? schemaUrlOverride;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeResourceDetector"/> class.
    /// </summary>
    /// <param name="accessor">The accessor used to retrieve the declarative configuration document.</param>
    public DeclarativeResourceDetector(DeclarativeConfigurationDocumentAccessor accessor)
    {
        this.accessor = accessor;
    }

    /// <inheritdoc/>
    public Resource Detect()
    {
        var document = this.accessor.GetDocument();
        if (!document.Model.Resource.TryGetValue(out var resourceConfig))
        {
            return Resource.Empty;
        }

        // resource.schema_url is a three-state property: absent means no override (leave
        // whatever the merged resource ends up with), present-null means an explicit override
        // to "no schema URL", and present means an explicit override to the given URL. Both of
        // the latter two must be applied, so schemaUrlOverride alone can't tell them apart from
        // "absent" (both leave it null); schemaUrlOverrideSet disambiguates.
        if (!resourceConfig.SchemaUrl.IsAbsent)
        {
            this.schemaUrlOverride = resourceConfig.SchemaUrl.IsPresent
                ? resourceConfig.SchemaUrl.Value
                : null;
            this.schemaUrlOverrideSet = true;
        }

        var attributes = new List<KeyValuePair<string, object>>();

        if (resourceConfig.Attributes.TryGetValue(out var entries))
        {
            var seenNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                var mapped = ResourceAttributeMapper.TryMap(entry);
                if (!mapped.HasValue)
                {
                    continue;
                }

                if (!GetAttributeNamePattern().IsMatch(entry.Name))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.ResourceAttributeNameNotCompliant(entry.Name);
                }

                if (!seenNames.Add(entry.Name))
                {
                    OpenTelemetryDeclarativeConfigurationEventSource.Log.DuplicateResourceAttributeName(entry.Name);
                    continue;
                }

                attributes.Add(mapped.Value);
            }
        }

        return attributes.Count == 0
            ? Resource.Empty
            : new Resource(attributes);
    }

    /// <inheritdoc/>
    public bool TryGetSchemaUrlOverride(out string? schemaUrl)
    {
        schemaUrl = this.schemaUrlOverride;
        return this.schemaUrlOverrideSet;
    }

#if NET
    [GeneratedRegex(AttributeNamePatternString, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex GetAttributeNamePattern();
#else
    private static Regex GetAttributeNamePattern() => AttributeNamePatternInstance;
#endif
}
