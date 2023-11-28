// <copyright file="Metric.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Represents a metric stream which can contain multiple metric points.
/// </summary>
public sealed class Metric
{
    internal const int DefaultExponentialHistogramMaxBuckets = 160;

    internal const int DefaultExponentialHistogramMaxScale = 20;

    internal static readonly double[] DefaultHistogramBounds = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000 };

    // Short default histogram bounds. Based on the recommended semantic convention values for http.server.request.duration.
    internal static readonly double[] DefaultHistogramBoundsShortSeconds = new double[] { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 };
    internal static readonly HashSet<(string, string)> DefaultHistogramBoundShortMappings = new()
    {
        ("Microsoft.AspNetCore.Hosting", "http.server.request.duration"),
        ("Microsoft.AspNetCore.RateLimiting", "aspnetcore.rate_limiting.request.time_in_queue"),
        ("Microsoft.AspNetCore.RateLimiting", "aspnetcore.rate_limiting.request_lease.duration"),
        ("Microsoft.AspNetCore.Server.Kestrel", "kestrel.tls_handshake.duration"),
        ("OpenTelemetry.Instrumentation.AspNetCore", "http.server.request.duration"),
        ("OpenTelemetry.Instrumentation.Http", "http.client.request.duration"),
        ("System.Net.Http", "http.client.request.duration"),
        ("System.Net.Http", "http.client.request.time_in_queue"),
        ("System.Net.NameResolution", "dns.lookup.duration"),
    };

    // Long default histogram bounds. Not based on a standard. May change in the future.
    internal static readonly double[] DefaultHistogramBoundsLongSeconds = new double[] { 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60, 120, 300 };
    internal static readonly HashSet<(string, string)> DefaultHistogramBoundLongMappings = new()
    {
        ("Microsoft.AspNetCore.Http.Connections", "signalr.server.connection.duration"),
        ("Microsoft.AspNetCore.Server.Kestrel", "kestrel.connection.duration"),
        ("System.Net.Http", "http.client.connection.duration"),
    };

    private readonly AggregatorStore aggStore;

    internal Metric(
        MetricStreamIdentity instrumentIdentity,
        AggregationTemporality temporality,
        int maxMetricPointsPerMetricStream,
        bool emitOverflowAttribute,
        bool shouldReclaimUnusedMetricPoints,
        ExemplarFilter? exemplarFilter = null)
    {
        this.InstrumentIdentity = instrumentIdentity;

        AggregationType aggType;
        if (instrumentIdentity.InstrumentType == typeof(ObservableCounter<long>)
            || instrumentIdentity.InstrumentType == typeof(ObservableCounter<int>)
            || instrumentIdentity.InstrumentType == typeof(ObservableCounter<short>)
            || instrumentIdentity.InstrumentType == typeof(ObservableCounter<byte>))
        {
            aggType = AggregationType.LongSumIncomingCumulative;
            this.MetricType = MetricType.LongSum;
        }
        else if (instrumentIdentity.InstrumentType == typeof(Counter<long>)
            || instrumentIdentity.InstrumentType == typeof(Counter<int>)
            || instrumentIdentity.InstrumentType == typeof(Counter<short>)
            || instrumentIdentity.InstrumentType == typeof(Counter<byte>))
        {
            aggType = AggregationType.LongSumIncomingDelta;
            this.MetricType = MetricType.LongSum;
        }
        else if (instrumentIdentity.InstrumentType == typeof(Counter<double>)
            || instrumentIdentity.InstrumentType == typeof(Counter<float>))
        {
            aggType = AggregationType.DoubleSumIncomingDelta;
            this.MetricType = MetricType.DoubleSum;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableCounter<double>)
            || instrumentIdentity.InstrumentType == typeof(ObservableCounter<float>))
        {
            aggType = AggregationType.DoubleSumIncomingCumulative;
            this.MetricType = MetricType.DoubleSum;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<long>)
            || instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<int>)
            || instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<short>)
            || instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<byte>))
        {
            aggType = AggregationType.LongSumIncomingCumulative;
            this.MetricType = MetricType.LongSumNonMonotonic;
        }
        else if (instrumentIdentity.InstrumentType == typeof(UpDownCounter<long>)
            || instrumentIdentity.InstrumentType == typeof(UpDownCounter<int>)
            || instrumentIdentity.InstrumentType == typeof(UpDownCounter<short>)
            || instrumentIdentity.InstrumentType == typeof(UpDownCounter<byte>))
        {
            aggType = AggregationType.LongSumIncomingDelta;
            this.MetricType = MetricType.LongSumNonMonotonic;
        }
        else if (instrumentIdentity.InstrumentType == typeof(UpDownCounter<double>)
            || instrumentIdentity.InstrumentType == typeof(UpDownCounter<float>))
        {
            aggType = AggregationType.DoubleSumIncomingDelta;
            this.MetricType = MetricType.DoubleSumNonMonotonic;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<double>)
            || instrumentIdentity.InstrumentType == typeof(ObservableUpDownCounter<float>))
        {
            aggType = AggregationType.DoubleSumIncomingCumulative;
            this.MetricType = MetricType.DoubleSumNonMonotonic;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableGauge<double>)
            || instrumentIdentity.InstrumentType == typeof(ObservableGauge<float>))
        {
            aggType = AggregationType.DoubleGauge;
            this.MetricType = MetricType.DoubleGauge;
        }
        else if (instrumentIdentity.InstrumentType == typeof(ObservableGauge<long>)
            || instrumentIdentity.InstrumentType == typeof(ObservableGauge<int>)
            || instrumentIdentity.InstrumentType == typeof(ObservableGauge<short>)
            || instrumentIdentity.InstrumentType == typeof(ObservableGauge<byte>))
        {
            aggType = AggregationType.LongGauge;
            this.MetricType = MetricType.LongGauge;
        }
        else if (instrumentIdentity.InstrumentType == typeof(Histogram<long>)
            || instrumentIdentity.InstrumentType == typeof(Histogram<int>)
            || instrumentIdentity.InstrumentType == typeof(Histogram<short>)
            || instrumentIdentity.InstrumentType == typeof(Histogram<byte>)
            || instrumentIdentity.InstrumentType == typeof(Histogram<float>)
            || instrumentIdentity.InstrumentType == typeof(Histogram<double>))
        {
            var explicitBucketBounds = instrumentIdentity.HistogramBucketBounds;
            var exponentialMaxSize = instrumentIdentity.ExponentialHistogramMaxSize;
            var histogramRecordMinMax = instrumentIdentity.HistogramRecordMinMax;

            this.MetricType = exponentialMaxSize == 0
                ? MetricType.Histogram
                : MetricType.ExponentialHistogram;

            if (this.MetricType == MetricType.Histogram)
            {
                aggType = explicitBucketBounds != null && explicitBucketBounds.Length == 0
                    ? (histogramRecordMinMax ? AggregationType.HistogramWithMinMax : AggregationType.Histogram)
                    : (histogramRecordMinMax ? AggregationType.HistogramWithMinMaxBuckets : AggregationType.HistogramWithBuckets);
            }
            else
            {
                aggType = histogramRecordMinMax ? AggregationType.Base2ExponentialHistogramWithMinMax : AggregationType.Base2ExponentialHistogram;
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported Instrument Type: {instrumentIdentity.InstrumentType.FullName}");
        }

        this.aggStore = new AggregatorStore(instrumentIdentity, aggType, temporality, maxMetricPointsPerMetricStream, emitOverflowAttribute, shouldReclaimUnusedMetricPoints, exemplarFilter);
        this.Temporality = temporality;
        this.InstrumentDisposed = false;
    }

    /// <summary>
    /// Gets the <see cref="Metrics.MetricType"/> for the metric stream.
    /// </summary>
    public MetricType MetricType { get; private set; }

    /// <summary>
    /// Gets the <see cref="AggregationTemporality"/> for the metric stream.
    /// </summary>
    public AggregationTemporality Temporality { get; private set; }

    /// <summary>
    /// Gets the name for the metric stream.
    /// </summary>
    public string Name => this.InstrumentIdentity.InstrumentName;

    /// <summary>
    /// Gets the description for the metric stream.
    /// </summary>
    public string Description => this.InstrumentIdentity.Description;

    /// <summary>
    /// Gets the unit for the metric stream.
    /// </summary>
    public string Unit => this.InstrumentIdentity.Unit;

    /// <summary>
    /// Gets the meter name for the metric stream.
    /// </summary>
    public string MeterName => this.InstrumentIdentity.MeterName;

    /// <summary>
    /// Gets the meter version for the metric stream.
    /// </summary>
    public string MeterVersion => this.InstrumentIdentity.MeterVersion;

    /// <summary>
    /// Gets the attributes (tags) for the metric stream.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object?>> MeterTags => this.InstrumentIdentity.MeterTags;

    /// <summary>
    /// Gets the <see cref="MetricStreamIdentity"/> for the metric stream.
    /// </summary>
    internal MetricStreamIdentity InstrumentIdentity { get; private set; }

    internal bool InstrumentDisposed { get; set; }

    /// <summary>
    /// Get the metric points for the metric stream.
    /// </summary>
    /// <returns><see cref="MetricPointsAccessor"/>.</returns>
    public MetricPointsAccessor GetMetricPoints()
        => this.aggStore.GetMetricPoints();

    internal void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => this.aggStore.Update(value, tags);

    internal void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => this.aggStore.Update(value, tags);

    internal int Snapshot()
        => this.aggStore.Snapshot();
}
