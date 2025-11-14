// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    private readonly ConcurrentDictionary<MetricStreamIdentity, Metric?> instrumentIdentityToMetric = new();
    private readonly Lock instrumentCreationLock = new();
    private int metricLimit;
    private int cardinalityLimit;
    private Metric?[] metrics = [];
    private Metric[] metricsCurrentBatch = [];
    private int metricIndex = -1;
    private ExemplarFilterType? exemplarFilter;
    private ExemplarFilterType? exemplarFilterForHistograms;

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
        Debug.Assert(instrument != null, "instrument was null");
        Debug.Assert(this.metrics != null, "this.metrics was null");

        var metricStreamIdentity = new MetricStreamIdentity(instrument!, metricStreamConfiguration: null);

        var exemplarFilter = metricStreamIdentity.IsHistogram
            ? this.exemplarFilterForHistograms ?? this.exemplarFilter
            : this.exemplarFilter;

        lock (this.instrumentCreationLock)
        {
            if (this.TryGetExistingMetric(in metricStreamIdentity, out var existingMetric))
            {
                return new() { existingMetric };
            }

            var index = ++this.metricIndex;
            if (index >= this.metricLimit)
            {
                OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Maximum allowed Metric streams for the provider exceeded.", "Use MeterProviderBuilder.AddView to drop unused instruments. Or use MeterProviderBuilder.SetMaxMetricStreams to configure MeterProvider to allow higher limit.");
                return new();
            }
            else
            {
                Metric? metric = null;
                try
                {
                    metric = new Metric(
                        metricStreamIdentity,
                        this.GetAggregationTemporality(metricStreamIdentity.InstrumentType),
                        this.cardinalityLimit,
                        exemplarFilter);
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
        Debug.Assert(instrument != null, "instrument was null");
        Debug.Assert(metricStreamConfigs != null, "metricStreamConfigs was null");
        Debug.Assert(this.metrics != null, "this.metrics was null");

        var maxCountMetricsToBeCreated = metricStreamConfigs!.Count;

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
                var metricStreamIdentity = new MetricStreamIdentity(instrument!, metricStreamConfig);

                var exemplarFilter = metricStreamIdentity.IsHistogram
                    ? this.exemplarFilterForHistograms ?? this.exemplarFilter
                    : this.exemplarFilter;

                if (!MeterProviderBuilderSdk.IsValidInstrumentName(metricStreamIdentity.InstrumentName))
                {
                    if (metricStreamConfig?.Name == null)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                            metricStreamIdentity.InstrumentName,
                            metricStreamIdentity.MeterName,
                            "Instrument name is invalid.",
                            "The name must comply with the OpenTelemetry specification.");
                    }
                    else
                    {
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                            metricStreamIdentity.InstrumentName,
                            metricStreamIdentity.MeterName,
                            "View name is invalid.",
                            "The name must comply with the OpenTelemetry specification.");
                    }

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
                if (index >= this.metricLimit)
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Maximum allowed Metric streams for the provider exceeded.", "Use MeterProviderBuilder.AddView to drop unused instruments. Or use MeterProviderBuilder.SetMaxMetricStreams to configure MeterProvider to allow higher limit.");
                }
                else
                {
                    Metric metric = new(
                        metricStreamIdentity,
                        this.GetAggregationTemporality(metricStreamIdentity.InstrumentType),
                        metricStreamConfig?.CardinalityLimit ?? this.cardinalityLimit,
                        exemplarFilter,
                        metricStreamConfig?.ExemplarReservoirFactory);

                    this.instrumentIdentityToMetric[metricStreamIdentity] = metric;
                    this.metrics![index] = metric;
                    metrics.Add(metric);

                    this.CreateOrUpdateMetricStreamRegistration(in metricStreamIdentity);
                }
            }

            return metrics;
        }
    }

    internal void ApplyParentProviderSettings(
        int metricLimit,
        int cardinalityLimit,
        ExemplarFilterType? exemplarFilter,
        ExemplarFilterType? exemplarFilterForHistograms)
    {
        this.metricLimit = metricLimit;
        this.metrics = new Metric[metricLimit];
        this.metricsCurrentBatch = new Metric[metricLimit];
        this.cardinalityLimit = cardinalityLimit;
        this.exemplarFilter = exemplarFilter;
        this.exemplarFilterForHistograms = exemplarFilterForHistograms;
    }

    private bool TryGetExistingMetric(in MetricStreamIdentity metricStreamIdentity, [NotNullWhen(true)] out Metric? existingMetric)
        => this.instrumentIdentityToMetric.TryGetValue(metricStreamIdentity, out existingMetric)
            && existingMetric != null && existingMetric.Active;

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
            var indexSnapshot = Math.Min(this.metricIndex, this.metricLimit - 1);
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

        // Note: This is using TryUpdate and NOT TryRemove because there is a
        // race condition. If a metric is deactivated and then reactivated in
        // the same collection cycle
        // instrumentIdentityToMetric[metric.InstrumentIdentity] may already
        // point to the new activated metric and not the old deactivated one.
        this.instrumentIdentityToMetric.TryUpdate(metric.InstrumentIdentity, null, metric);

        // Note: metric is a reference to the array storage so
        // this clears the metric out of the array.
        metric = null;
    }
}
