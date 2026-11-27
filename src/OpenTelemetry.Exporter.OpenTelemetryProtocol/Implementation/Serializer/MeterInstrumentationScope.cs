// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

/// <summary>
/// Identifies the instrumentation scope metrics are grouped by. Meters sharing
/// a name but reporting different versions, schema URLs or tags are distinct
/// scopes and must be emitted as separate ScopeMetrics entries.
/// </summary>
/// <remarks>
/// The fields come from <see cref="MetricStreamIdentity"/>, which already
/// normalizes a missing version or schema URL to an empty string and holds the
/// meter tags as <see cref="Metrics.Tags"/>. Its hash code is computed once
/// when the metric stream is created, and its equality is the one the SDK uses
/// to tell metric streams apart.
/// </remarks>
internal readonly struct MeterInstrumentationScope : IEquatable<MeterInstrumentationScope>
{
    public readonly string Name;
    public readonly string Version;
    public readonly string SchemaUrl;
    public readonly Tags? Tags;

    public MeterInstrumentationScope(Metric metric)
    {
        var identity = metric.InstrumentIdentity;

        this.Name = identity.MeterName;
        this.Version = identity.MeterVersion;
        this.SchemaUrl = identity.MeterSchemaUrl;

        // Note: A meter without tags and a meter with an empty set of tags are
        // both exported without scope attributes.
        this.Tags = identity.MeterTags is { KeyValuePairs.Length: > 0 } tags ? tags : null;
    }

    /// <summary>
    /// Cheap check for two scopes taken from metrics of the same meter, which
    /// share its string instances. A <see langword="false"/> result does not
    /// mean the scopes differ; use <see cref="Equals(MeterInstrumentationScope)"/> for that.
    /// </summary>
    /// <param name="other">The scope to compare with.</param>
    /// <returns><see langword="true"/> if the scopes are known to be equal.</returns>
    public bool IsSameMeterAs(in MeterInstrumentationScope other)
        => ReferenceEquals(this.Name, other.Name)
        && ReferenceEquals(this.Version, other.Version)
        && ReferenceEquals(this.SchemaUrl, other.SchemaUrl)
        && this.TagsEqual(in other);

    public bool Equals(MeterInstrumentationScope other)
        => string.Equals(this.Name, other.Name, StringComparison.Ordinal)
        && string.Equals(this.Version, other.Version, StringComparison.Ordinal)
        && string.Equals(this.SchemaUrl, other.SchemaUrl, StringComparison.Ordinal)
        && this.TagsEqual(in other);

    public override bool Equals(object? obj)
        => obj is MeterInstrumentationScope other && this.Equals(other);

    public override int GetHashCode()
    {
        var tagsHashCode = this.Tags.HasValue ? this.Tags.GetValueOrDefault().GetHashCode() : 0;

#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(this.Name),
            StringComparer.Ordinal.GetHashCode(this.Version),
            StringComparer.Ordinal.GetHashCode(this.SchemaUrl),
            tagsHashCode);
#else
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(this.Name);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(this.Version);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(this.SchemaUrl);
            hash = (hash * 31) + tagsHashCode;
            return hash;
        }
#endif
    }

    private bool TagsEqual(in MeterInstrumentationScope other)
        => this.Tags.HasValue == other.Tags.HasValue
        && (!this.Tags.HasValue || this.Tags.GetValueOrDefault().Equals(other.Tags.GetValueOrDefault()));
}
