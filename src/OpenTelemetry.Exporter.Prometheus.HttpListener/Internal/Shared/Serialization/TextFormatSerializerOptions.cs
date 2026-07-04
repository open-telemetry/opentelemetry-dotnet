// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// Options for <see cref="TextFormatSerializer"/>.
/// </summary>
internal readonly struct TextFormatSerializerOptions
{
    /// <summary>
    /// A value indicating whether the scope information (name, version, schema URL) is suppressed from the scrape response.
    /// </summary>
    public readonly bool SuppressScopeInfo; // Inverted so the default struct is the default value (false = included).

    /// <summary>
    /// The resource attributes to add to each metric as constant labels, or <see langword="null"/> if none.
    /// </summary>
    public readonly IReadOnlyList<KeyValuePair<string, object>>? ResourceConstantLabels;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextFormatSerializerOptions"/> struct.
    /// </summary>
    /// <param name="suppressScopeInfo">Whether the scope information is suppressed from the scrape response.</param>
    /// <param name="resourceConstantLabels">The resource attributes to add to each metric as constant labels, or <see langword="null"/> if none.</param>
    public TextFormatSerializerOptions(
        bool suppressScopeInfo,
        IReadOnlyList<KeyValuePair<string, object>>? resourceConstantLabels)
    {
        this.SuppressScopeInfo = suppressScopeInfo;
        this.ResourceConstantLabels = resourceConstantLabels;
    }
}
