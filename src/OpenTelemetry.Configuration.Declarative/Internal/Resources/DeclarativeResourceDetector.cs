// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An <see cref="IResourceDetector"/> that reads <c>resource.attributes</c> and <c>resource.schema_url</c>
/// from the declarative configuration document and applies them to the SDK resource.
/// </summary>
internal sealed partial class DeclarativeResourceDetector : IResourceDetector
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

        // The schema URL goes through the standard Resource.Merge rules, so a value that differs from
        // one contributed by another detector (including the SDK defaults) results in no schema URL.
        resourceConfig.SchemaUrl.TryGetValue(out var schemaUrl);

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

        return attributes.Count == 0 && string.IsNullOrEmpty(schemaUrl)
            ? Resource.Empty
            : new Resource(attributes, schemaUrl);
    }

#if NET
    [GeneratedRegex(AttributeNamePatternString, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1_000)]
    private static partial Regex GetAttributeNamePattern();
#else
    private static Regex GetAttributeNamePattern() => AttributeNamePatternInstance;
#endif
}
