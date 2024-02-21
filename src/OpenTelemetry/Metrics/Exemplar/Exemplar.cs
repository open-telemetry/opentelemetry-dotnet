// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Runtime.CompilerServices;
#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using OpenTelemetry.Internal;
#endif

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
internal
#endif
    struct Exemplar
{
    internal HashSet<string>? ViewDefinedTagKeys;

    private static readonly ReadOnlyFilteredTagCollection Empty = new(excludedKeys: null, Array.Empty<KeyValuePair<string, object?>>(), count: 0);
    private int tagCount;
    private KeyValuePair<string, object?>[]? tagStorage;
    private MetricPointValueStorage valueStorage;
    private int isCriticalSectionOccupied;

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// Gets the TraceId.
    /// </summary>
    public ActivityTraceId? TraceId { get; private set; }

    /// <summary>
    /// Gets the SpanId.
    /// </summary>
    public ActivitySpanId? SpanId { get; private set; }

    /// <summary>
    /// Gets the long value.
    /// </summary>
    public long LongValue
    {
        readonly get => this.valueStorage.AsLong;
        private set => this.valueStorage.AsLong = value;
    }

    /// <summary>
    /// Gets the double value.
    /// </summary>
    public double DoubleValue
    {
        readonly get => this.valueStorage.AsDouble;
        private set => this.valueStorage.AsDouble = value;
    }

    /// <summary>
    /// Gets the filtered tags.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="FilteredTags"/> represents the set of tags which were
    /// supplied at measurement but dropped due to filtering configured by a
    /// view (<see cref="MetricStreamConfiguration.TagKeys"/>). If view tag
    /// filtering is not configured <see cref="FilteredTags"/> will be empty.
    /// </remarks>
    public readonly ReadOnlyFilteredTagCollection FilteredTags
    {
        get
        {
            if (this.tagCount == 0)
            {
                return Empty;
            }
            else
            {
                Debug.Assert(this.tagStorage != null, "tagStorage was null");

                return new(this.ViewDefinedTagKeys, this.tagStorage!, this.tagCount);
            }
        }
    }

    internal void Update<T>(in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        if (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0)
        {
            // Some other thread is already writing, abort.
            return;
        }

        this.Timestamp = DateTimeOffset.UtcNow;

        if (typeof(T) == typeof(long))
        {
            this.LongValue = (long)(object)measurement.Value;
        }
        else if (typeof(T) == typeof(double))
        {
            this.DoubleValue = (double)(object)measurement.Value;
        }
        else
        {
            Debug.Fail("Invalid value type");
            this.DoubleValue = Convert.ToDouble(measurement.Value);
        }

        var currentActivity = Activity.Current;
        if (currentActivity != null)
        {
            this.TraceId = currentActivity.TraceId;
            this.SpanId = currentActivity.SpanId;
        }
        else
        {
            this.TraceId = default;
            this.SpanId = default;
        }

        this.StoreRawTags(measurement.Tags);

        Interlocked.Exchange(ref this.isCriticalSectionOccupied, 0);
    }

    internal void Reset()
    {
        this.Timestamp = default;
    }

    internal readonly bool IsUpdated()
    {
        if (Interlocked.CompareExchange(ref Unsafe.AsRef(in this.isCriticalSectionOccupied), 0, 0) != 0)
        {
            this.WaitForUpdateToCompleteRare();
            return true;
        }

        return this.Timestamp != default;
    }

    internal readonly void Copy(ref Exemplar destination)
    {
        destination.Timestamp = this.Timestamp;
        destination.TraceId = this.TraceId;
        destination.SpanId = this.SpanId;
        destination.valueStorage = this.valueStorage;
        destination.ViewDefinedTagKeys = this.ViewDefinedTagKeys;
        destination.tagCount = this.tagCount;
        if (destination.tagCount > 0)
        {
            Debug.Assert(this.tagStorage != null, "tagStorage was null");

            destination.tagStorage = new KeyValuePair<string, object?>[destination.tagCount];
            Array.Copy(this.tagStorage!, 0, destination.tagStorage, 0, destination.tagCount);
        }
    }

    private void StoreRawTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
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

    private readonly void WaitForUpdateToCompleteRare()
    {
        var spinWait = default(SpinWait);
        do
        {
            spinWait.SpinOnce();
        }
        while (Interlocked.CompareExchange(ref Unsafe.AsRef(in this.isCriticalSectionOccupied), 0, 0) != 0);
    }
}
