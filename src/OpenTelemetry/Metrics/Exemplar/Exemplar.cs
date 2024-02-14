// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Represents an Exemplar data.
/// </summary>
/// <remarks><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</remarks>
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.ExemplarExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
/// <summary>
/// Represents an Exemplar data.
/// </summary>
#pragma warning disable SA1623 // The property's documentation summary text should begin with: `Gets or sets`
internal
#endif
    struct Exemplar
{
    internal HashSet<string>? KeyFilter;
    private int tagCount;
    private KeyValuePair<string, object?>[]? tagStorage;
    private MetricPointValueStorage valueStorage;

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; internal set; }

    /// <summary>
    /// Gets the TraceId.
    /// </summary>
    public ActivityTraceId? TraceId { get; internal set; }

    /// <summary>
    /// Gets the SpanId.
    /// </summary>
    public ActivitySpanId? SpanId { get; internal set; }

    /// <summary>
    /// Gets the long value.
    /// </summary>
    public long LongValue
    {
        readonly get => this.valueStorage.AsLong;
        internal set => this.valueStorage.AsLong = value;
    }

    /// <summary>
    /// Gets the double value.
    /// </summary>
    public double DoubleValue
    {
        readonly get => this.valueStorage.AsDouble;
        internal set => this.valueStorage.AsDouble = value;
    }

    /// <summary>
    /// Gets the FilteredTags (i.e any tags that were dropped during aggregation).
    /// </summary>
    public readonly ReadOnlyFilteredTagCollection FilteredTags
        => new(this.KeyFilter, this.tagStorage ?? Array.Empty<KeyValuePair<string, object?>>(), this.tagCount);

    internal void StoreFilteredTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.tagCount = tags.Length;
        if (tags.Length == 0)
        {
            return;
        }

        if (this.tagStorage == null || this.tagStorage.Length < this.tagCount)
        {
            this.tagStorage = new KeyValuePair<string, object?>[this.tagCount];
        }

        tags.CopyTo(this.tagStorage);
    }

    internal void Reset()
    {
        this.Timestamp = default;
    }
}
