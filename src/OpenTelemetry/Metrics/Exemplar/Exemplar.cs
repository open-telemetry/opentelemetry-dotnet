// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using System.Diagnostics;
using System.Globalization;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Exemplar implementation.
/// </summary>
/// <remarks>
/// Specification: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplar"/>.
/// </remarks>
public struct Exemplar
{
#if NET
    internal FrozenSet<string>? ViewDefinedTagKeys;
#else
    internal HashSet<string>? ViewDefinedTagKeys;
#endif

    private static readonly ReadOnlyFilteredTagCollection Empty = new(excludedKeys: null, [], count: 0);
    private int tagCount;
    private KeyValuePair<string, object?>[]? tagStorage;
    private MetricPointValueStorage valueStorage;
    private int isCriticalSectionOccupied;

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { readonly get; private set; }

    /// <summary>
    /// Gets the TraceId.
    /// </summary>
    public ActivityTraceId TraceId { readonly get; private set; }

    /// <summary>
    /// Gets the SpanId.
    /// </summary>
    public ActivitySpanId SpanId { readonly get; private set; }

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
            // Note: If we reached here it means some other thread is already
            // updating the exemplar. Instead of spinning, we abort. The idea is
            // for two exemplars offered at more or less the same time there
            // really isn't a difference which one is stored so it is an
            // optimization to let the losing thread(s) get back to work instead
            // of spinning.
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
            this.DoubleValue = Convert.ToDouble(measurement.Value, CultureInfo.InvariantCulture);
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

        if (this.ViewDefinedTagKeys != null)
        {
            this.StoreRawTags(measurement.Tags);
        }

        Volatile.Write(ref this.isCriticalSectionOccupied, 0);
    }

    internal void Reset()
    {
        this.Timestamp = default;
    }

    internal readonly bool IsUpdated()
    {
        return this.Timestamp != default;
    }

    internal void Collect(ref Exemplar destination, bool reset)
    {
        if (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0)
        {
            this.AcquireLockRare();
        }

        if (this.IsUpdated())
        {
            this.Copy(ref destination);
            if (reset)
            {
                this.Reset();
            }
        }
        else
        {
            destination.Reset();
        }

        Volatile.Write(ref this.isCriticalSectionOccupied, 0);
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

    private void AcquireLockRare()
    {
        SpinWait spinWait = default;
        do
        {
            spinWait.SpinOnce();
        }
        while (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0);
    }
}
