// <copyright file="MetricReaderExt.cs" company="OpenTelemetry Authors">
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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader base class.
/// </summary>
public abstract partial class MetricReader
{
    private readonly ConcurrentDictionary<string, MetricStreamRegistration> metricStreamNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<MetricStreamIdentity, Metric> instrumentIdentityToMetric = new();
    private readonly object instrumentCreationLock = new();
    private int maxMetricStreams;
    private int maxMetricPointsPerMetricStream;
    private Metric?[]? metrics;
    private Metric[]? metricsCurrentBatch;
    private int metricIndex = -1;
    private bool emitOverflowAttribute;

    private ExemplarFilter? exemplarFilter;

    internal AggregationTemporality GetAggregationTemporality(Type instrumentType)
    {
        return this.temporalityFunc(instrumentType);
    }

    internal Metric? AddMetricWithNoViews(Instrument instrument)
    {
        Debug.Assert(this.metrics != null, "this.metrics was null");

        var metricStreamIdentity = new MetricStreamIdentity(instrument, metricStreamConfiguration: null);
        lock (this.instrumentCreationLock)
        {
            if (this.instrumentIdentityToMetric.TryGetValue(metricStreamIdentity, out var existingMetric))
            {
                if (existingMetric.CleanupState != Metric.MetricCleanupNoState)
                {
                    bool activated = false;

                    lock (existingMetric.CleanupLock)
                    {
                        if (existingMetric.CleanupState == Metric.MetricCleanupPending)
                        {
                            existingMetric.CleanupState = Metric.MetricCleanupNoState;
                            activated = true;
                        }
                    }

                    if (activated)
                    {
                        // Note: This case here is a metric was deactivated
                        // and then reactivated before an export ran to
                        // finish the cleanup.
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentReactivated(
                            metricStreamIdentity.InstrumentName,
                            metricStreamIdentity.MeterName);
                        return existingMetric;
                    }
                }
                else
                {
                    return existingMetric;
                }
            }

            var index = ++this.metricIndex;
            if (index >= this.maxMetricStreams)
            {
                OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Maximum allowed Metric streams for the provider exceeded.", "Use MeterProviderBuilder.AddView to drop unused instruments. Or use MeterProviderBuilder.SetMaxMetricStreams to configure MeterProvider to allow higher limit.");
                return null;
            }
            else
            {
                Metric? metric = null;
                try
                {
                    bool shouldReclaimUnusedMetricPoints = this.parentProvider is MeterProviderSdk meterProviderSdk && meterProviderSdk.ShouldReclaimUnusedMetricPoints;
                    metric = new Metric(metricStreamIdentity, this.GetAggregationTemporality(metricStreamIdentity.InstrumentType), this.maxMetricPointsPerMetricStream, this.emitOverflowAttribute, shouldReclaimUnusedMetricPoints, this.exemplarFilter);
                }
                catch (NotSupportedException nse)
                {
                    // TODO: This allocates string even if none listening.
                    // Could be improved with separate Event.
                    // Also the message could call out what Instruments
                    // and types (eg: int, long etc) are supported.
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Unsupported instrument. Details: " + nse.Message, "Switch to a supported instrument type.");
                    return null;
                }

                this.instrumentIdentityToMetric[metricStreamIdentity] = metric;
                this.metrics![index] = metric;

                this.CreateOrUpdateMetricStreamRegistration(in metricStreamIdentity);

                return metric;
            }
        }
    }

    internal void RecordSingleStreamLongMeasurement(Metric metric, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        metric.UpdateLong(value, tags);
    }

    internal void RecordSingleStreamDoubleMeasurement(Metric metric, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        metric.UpdateDouble(value, tags);
    }

    internal List<Metric> AddMetricsListWithViews(Instrument instrument, List<MetricStreamConfiguration?> metricStreamConfigs)
    {
        Debug.Assert(this.metrics != null, "this.metrics was null");

        var maxCountMetricsToBeCreated = metricStreamConfigs.Count;

        // Create list with initial capacity as the max metric count.
        // Due to duplicate/max limit, we may not end up using them
        // all, and that memory is wasted until Meter disposed.
        // TODO: Revisit to see if we need to do metrics.TrimExcess()
        var metrics = new List<Metric>(maxCountMetricsToBeCreated);
        lock (this.instrumentCreationLock)
        {
            for (int i = 0; i < maxCountMetricsToBeCreated; i++)
            {
                var metricStreamConfig = metricStreamConfigs[i];
                var metricStreamIdentity = new MetricStreamIdentity(instrument, metricStreamConfig);

                if (!MeterProviderBuilderSdk.IsValidInstrumentName(metricStreamIdentity.InstrumentName))
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                        metricStreamIdentity.InstrumentName,
                        metricStreamIdentity.MeterName,
                        "Metric name is invalid.",
                        "The name must comply with the OpenTelemetry specification.");

                    continue;
                }

                if (this.instrumentIdentityToMetric.TryGetValue(metricStreamIdentity, out var existingMetric))
                {
                    metrics.Add(existingMetric);
                    continue;
                }

                if (metricStreamConfig == MetricStreamConfiguration.Drop)
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "View configuration asks to drop this instrument.", "Modify view configuration to allow this instrument, if desired.");
                    continue;
                }

                var index = ++this.metricIndex;
                if (index >= this.maxMetricStreams)
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Maximum allowed Metric streams for the provider exceeded.", "Use MeterProviderBuilder.AddView to drop unused instruments. Or use MeterProviderBuilder.SetMaxMetricStreams to configure MeterProvider to allow higher limit.");
                }
                else
                {
                    bool shouldReclaimUnusedMetricPoints = this.parentProvider is MeterProviderSdk meterProviderSdk && meterProviderSdk.ShouldReclaimUnusedMetricPoints;
                    Metric metric = new(metricStreamIdentity, this.GetAggregationTemporality(metricStreamIdentity.InstrumentType), this.maxMetricPointsPerMetricStream, this.emitOverflowAttribute, shouldReclaimUnusedMetricPoints, this.exemplarFilter);

                    this.instrumentIdentityToMetric[metricStreamIdentity] = metric;
                    this.metrics![index] = metric;
                    metrics.Add(metric);

                    this.CreateOrUpdateMetricStreamRegistration(in metricStreamIdentity);
                }
            }

            return metrics;
        }
    }

    internal void RecordLongMeasurement(List<Metric> metrics, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (metrics.Count == 1)
        {
            // special casing the common path
            // as this is faster than the
            // foreach, when count is 1.
            metrics[0].UpdateLong(value, tags);
        }
        else
        {
            foreach (var metric in metrics)
            {
                metric.UpdateLong(value, tags);
            }
        }
    }

    internal void RecordDoubleMeasurement(List<Metric> metrics, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (metrics.Count == 1)
        {
            // special casing the common path
            // as this is faster than the
            // foreach, when count is 1.
            metrics[0].UpdateDouble(value, tags);
        }
        else
        {
            foreach (var metric in metrics)
            {
                metric.UpdateDouble(value, tags);
            }
        }
    }

    internal void CompleteSingleStreamMeasurement(Metric metric)
    {
        DeactivateMetric(metric);
    }

    internal void CompleteMeasurement(List<Metric> metrics)
    {
        foreach (var metric in metrics)
        {
            DeactivateMetric(metric);
        }
    }

    internal void SetMaxMetricStreams(int maxMetricStreams)
    {
        this.maxMetricStreams = maxMetricStreams;
        this.metrics = new Metric[maxMetricStreams];
        this.metricsCurrentBatch = new Metric[maxMetricStreams];
    }

    internal void SetExemplarFilter(ExemplarFilter? exemplarFilter)
    {
        this.exemplarFilter = exemplarFilter;
    }

    internal void SetMaxMetricPointsPerMetricStream(int maxMetricPointsPerMetricStream, bool isEmitOverflowAttributeKeySet)
    {
        this.maxMetricPointsPerMetricStream = maxMetricPointsPerMetricStream;

        if (isEmitOverflowAttributeKeySet)
        {
            // We need at least two metric points. One is reserved for zero tags and the other one for overflow attribute
            if (maxMetricPointsPerMetricStream > 1)
            {
                this.emitOverflowAttribute = true;
            }
        }
    }

    private static void DeactivateMetric(Metric metric)
    {
        metric.CleanupState = Metric.MetricCleanupPending;

        OpenTelemetrySdkEventSource.Log.MetricInstrumentDeactivated(metric.Name, metric.MeterName);
    }

    private void CreateOrUpdateMetricStreamRegistration(in MetricStreamIdentity metricStreamIdentity)
    {
        var registration = this.metricStreamNames.GetOrAdd(
            metricStreamIdentity.MetricStreamName,
            CreateRegistration);

        var currentRegistrationCount = Interlocked.CompareExchange(ref registration.RegistrationCount, 1, -1);

        if (currentRegistrationCount == -1)
        {
            // Most common case where instrument being added is the first registration.
            return;
        }

        if (currentRegistrationCount > 0)
        {
            OpenTelemetrySdkEventSource.Log.DuplicateMetricInstrument(
                metricStreamIdentity.InstrumentName,
                metricStreamIdentity.MeterName,
                "Metric instrument has the same name as an existing one but differs by description, unit, or instrument type. Measurements from this instrument will still be exported but may result in conflicts.",
                "Either change the name of the instrument or use MeterProviderBuilder.AddView to resolve the conflict.");
        }
        else
        {
            // Note: This case here is a metric was deactivated and then
            // reactivated after an export ran and finished the cleanup.
            OpenTelemetrySdkEventSource.Log.MetricInstrumentReactivated(
                metricStreamIdentity.InstrumentName,
                metricStreamIdentity.MeterName);
        }

        Interlocked.Increment(ref registration.RegistrationCount);

        static MetricStreamRegistration CreateRegistration(string metricStreamName)
            => new() { RegistrationCount = -1 };
    }

    private Batch<Metric> GetMetricsBatch()
    {
        Debug.Assert(this.metrics != null, "this.metrics was null");
        Debug.Assert(this.metricsCurrentBatch != null, "this.metricsCurrentBatch was null");

        try
        {
            var indexSnapshot = Math.Min(this.metricIndex, this.maxMetricStreams - 1);
            var target = indexSnapshot + 1;
            int metricCountCurrentBatch = 0;
            for (int i = 0; i < target; i++)
            {
                ref var metric = ref this.metrics![i];
                if (metric != null)
                {
                    int metricPointSize = metric.Snapshot();

                    if (metricPointSize > 0)
                    {
                        this.metricsCurrentBatch![metricCountCurrentBatch++] = metric;
                    }

                    if (metric.CleanupState == Metric.MetricCleanupPending)
                    {
                        this.CleanupMetric(ref metric);
                    }
                }
            }

            return (metricCountCurrentBatch > 0) ? new Batch<Metric>(this.metricsCurrentBatch!, metricCountCurrentBatch) : default;
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.GetMetricsBatch), ex);
            return default;
        }
    }

    private void CleanupMetric(ref Metric metric)
    {
        lock (metric.CleanupLock)
        {
            if (metric.CleanupState != Metric.MetricCleanupPending)
            {
                // Note: If we fall here it means the metric was reactivated
                // while we were waiting on the lock.
                return;
            }

            var metricStreamNamesLookupResult = this.metricStreamNames.TryGetValue(metric.InstrumentIdentity.MetricStreamName, out var registration);
            Debug.Assert(metricStreamNamesLookupResult, "result was false");
            Debug.Assert(registration != null, "registration was null");
            Interlocked.Decrement(ref registration!.RegistrationCount);

            metric.CleanupState = Metric.MetricCleanupComplete;
        }

        var instrumentIdentityToMetricLookupResult = this.instrumentIdentityToMetric.TryRemove(metric.InstrumentIdentity, out var _);
        Debug.Assert(instrumentIdentityToMetricLookupResult, "instrumentIdentityToMetricLookupResult was false");

        OpenTelemetrySdkEventSource.Log.MetricInstrumentRemoved(metric.Name, metric.MeterName);

        // Note: This is a pointer to the storage for the metric inside
        // this.metrics array. Clearing using the pointer is faster than
        // re-accessing it which will incur a bounds check (this.metrics[i] =
        // null).
        metric = null!;
    }

    private sealed class MetricStreamRegistration
    {
        public int RegistrationCount;
    }
}
