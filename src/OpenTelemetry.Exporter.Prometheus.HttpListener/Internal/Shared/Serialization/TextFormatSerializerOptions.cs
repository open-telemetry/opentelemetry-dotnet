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
    /// Initializes a new instance of the <see cref="TextFormatSerializerOptions"/> struct.
    /// </summary>
    /// <param name="suppressScopeInfo">Whether the scope information is suppressed from the scrape response.</param>
    public TextFormatSerializerOptions(bool suppressScopeInfo)
    {
        this.SuppressScopeInfo = suppressScopeInfo;
    }
}
