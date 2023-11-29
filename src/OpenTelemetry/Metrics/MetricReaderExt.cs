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
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader base class.
/// </summary>
public abstract partial class MetricReader
{
    private readonly HashSet<string> metricStreamNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<MetricStreamIdentity, Metric> instrumentIdentityToMetric = new();
    private readonly object instrumentCreationLock = new();
    private int maxMetricStreams;
    private int maxMetricPointsPerMetricStream;
    private Metric?[]? metrics;
    private Metric[]? metricsCurrentBatch;
    private int metricIndex = -1;
    private bool emitOverflowAttribute;

    private ExemplarFilter? exemplarFilter;

    internal static void DeactivateMetric(Metric metric)
    {
        if (metric.Active)
        {
            // TODO: This will cause the metric to be removed from the storage
            // array during the next collect/export. If this happens often we
            // will run out of storage. Would it be better instead to set the
            // end time on the metric and keep it around so it can be
            // reactivated?
            metric.Active = false;

            OpenTelemetrySdkEventSource.Log.MetricInstrumentDeactivated(
                metric.Name,
                metric.MeterName);
        }
    }

    internal AggregationTemporality GetAggregationTemporality(Type instrumentType)
    {
        return this.temporalityFunc(instrumentType);
    }

    internal virtual List<Metric> AddMetricWithNoViews(Instrument instrument)
    {
        Debug.Assert(this.metrics != null, "this.metrics was null");

        var metricStreamIdentity = new MetricStreamIdentity(instrument, metricStreamConfiguration: null);
        lock (this.instrumentCreationLock)
        {
            if (this.TryGetExistingMetric(in metricStreamIdentity, out var existingMetric))
            {
                return new() { existingMetric };
            }

            var index = ++this.metricIndex;
            if (index >= this.maxMetricStreams)
            {
                OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Maximum allowed Metric streams for the provider exceeded.", "Use MeterProviderBuilder.AddView to drop unused instruments. Or use MeterProviderBuilder.SetMaxMetricStreams to configure MeterProvider to allow higher limit.");
                return new();
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
                    return new();
                }

                this.instrumentIdentityToMetric[metricStreamIdentity] = metric;
                this.metrics![index] = metric;

                this.CreateOrUpdateMetricStreamRegistration(in metricStreamIdentity);

                return new() { metric };
            }
        }
    }

    internal virtual List<Metric> AddMetricWithViews(Instrument instrument, List<MetricStreamConfiguration?> metricStreamConfigs)
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

                if (this.TryGetExistingMetric(in metricStreamIdentity, out var existingMetric))
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

    private bool TryGetExistingMetric(in MetricStreamIdentity metricStreamIdentity, [NotNullWhen(true)] out Metric? existingMetric)
        => this.instrumentIdentityToMetric.TryGetValue(metricStreamIdentity, out existingMetric)
            && existingMetric.Active;

    private void CreateOrUpdateMetricStreamRegistration(in MetricStreamIdentity metricStreamIdentity)
    {
        if (!this.metricStreamNames.Add(metricStreamIdentity.MetricStreamName))
        {
            // TODO: If a metric is deactivated and then reactivated we log the
            // same warning as if it was a duplicate.
            OpenTelemetrySdkEventSource.Log.DuplicateMetricInstrument(
                metricStreamIdentity.InstrumentName,
                metricStreamIdentity.MeterName,
                "Metric instrument has the same name as an existing one but differs by description, unit, or instrument type. Measurements from this instrument will still be exported but may result in conflicts.",
                "Either change the name of the instrument or use MeterProviderBuilder.AddView to resolve the conflict.");
        }
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

                    if (!metric.Active)
                    {
                        this.RemoveMetric(ref metric);
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

    private void RemoveMetric(ref Metric? metric)
    {
        Debug.Assert(metric != null, "metric was null");

        // TODO: This logic removes the metric. If the same
        // metric is published again we will create a new metric
        // for it. If this happens often we will run out of
        // storage. Instead, should we keep the metric around
        // and set a new start time + reset its data if it comes
        // back?

        OpenTelemetrySdkEventSource.Log.MetricInstrumentRemoved(metric!.Name, metric.MeterName);

        var result = this.instrumentIdentityToMetric.TryRemove(metric.InstrumentIdentity, out var _);
        Debug.Assert(result, "result was false");

        // Note: metric is a reference to the array storage so
        // this clears the metric out of the array.
        metric = null;
    }
}
