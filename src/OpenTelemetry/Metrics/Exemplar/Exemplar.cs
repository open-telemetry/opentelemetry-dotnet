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
    private readonly HashSet<string> keyFilter;
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
        get => this.valueStorage.AsLong;
        internal set => this.valueStorage.AsLong = value;
    }

    /// <summary>
    /// Gets the double value.
    /// </summary>
    public double DoubleValue
    {
        get => this.valueStorage.AsDouble;
        internal set => this.valueStorage.AsDouble = value;
    }

    /// <summary>
    /// Gets the FilteredTags (i.e any tags that were dropped during aggregation).
    /// </summary>
    public ReadOnlyTagCollection FilteredTags
        => new(this.keyFilter, this.tagStorage ?? Array.Empty<KeyValuePair<string, object?>>(), this.tagCount);

    internal void StoreFilteredTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // todo: We can't share a pointer to array when collecting hmm

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

public readonly ref struct ReadOnlyExemplarCollection
{
    private readonly Exemplar[] exemplars;

    internal ReadOnlyExemplarCollection(Exemplar[] exemplars)
    {
        Debug.Assert(exemplars != null, "exemplars was null");

        this.exemplars = exemplars;
    }

    public Enumerator GetEnumerator() => new(this.exemplars);

    /// <summary>
    /// Enumerates the elements of a <see cref="ReadOnlyExemplarCollection"/>.
    /// </summary>
    public struct Enumerator
    {
        private readonly Exemplar[] exemplars;
        private int index;

        internal Enumerator(Exemplar[] exemplars)
        {
            this.exemplars = exemplars;
            this.index = -1;
        }

        /// <summary>
        /// Gets the <see cref="Exemplar"/> at the current position of the enumerator.
        /// </summary>
        public readonly ref readonly Exemplar Current
            => ref this.exemplars[this.index];

        /// <summary>
        /// Advances the enumerator to the next element of the <see
        /// cref="ReadOnlyExemplarCollection"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was
        /// successfully advanced to the next element; <see
        /// langword="false"/> if the enumerator has passed the end of the
        /// collection.</returns>
        public bool MoveNext()
            => ++this.index < this.exemplars.Length;
    }
}
