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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
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
        private Metric[] metrics;
        private Metric[] metricsCurrentBatch;
        private int metricIndex = -1;

        internal AggregationTemporality GetAggregationTemporality(Type instrumentType)
        {
            return this.temporalityFunc(instrumentType);
        }

        internal Metric AddMetricWithNoViews(Instrument instrument)
        {
            var metricStreamIdentity = new MetricStreamIdentity(instrument, metricStreamConfiguration: null);
            lock (this.instrumentCreationLock)
            {
                if (this.instrumentIdentityToMetric.TryGetValue(metricStreamIdentity, out var existingMetric))
                {
                    return existingMetric;
                }

                if (this.metricStreamNames.Contains(metricStreamIdentity.MetricStreamName))
                {
                    OpenTelemetrySdkEventSource.Log.DuplicateMetricInstrument(
                        metricStreamIdentity.InstrumentName,
                        metricStreamIdentity.MeterName,
                        "Metric instrument has the same name as an existing one but differs by description, unit, or instrument type. Measurements from this instrument will still be exported but may result in conflicts.",
                        "Either change the name of the instrument or use MeterProviderBuilder.AddView to resolve the conflict.");
                }

                var index = ++this.metricIndex;
                if (index >= this.maxMetricStreams)
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricStreamIdentity.InstrumentName, metricStreamIdentity.MeterName, "Maximum allowed Metric streams for the provider exceeded.", "Use MeterProviderBuilder.AddView to drop unused instruments. Or use MeterProviderBuilder.SetMaxMetricStreams to configure MeterProvider to allow higher limit.");
                    return null;
                }
                else
                {
                    Metric metric = null;
                    try
                    {
                        metric = new Metric(metricStreamIdentity, this.GetAggregationTemporality(metricStreamIdentity.InstrumentType), this.maxMetricPointsPerMetricStream);
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
                    this.metrics[index] = metric;
                    this.metricStreamNames.Add(metricStreamIdentity.MetricStreamName);
                    return metric;
                }
            }
        }

        internal void RecordSingleStreamLongMeasurement(Metric metric, long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            metric.UpdateLong(value, tags);
        }

        internal void RecordSingleStreamDoubleMeasurement(Metric metric, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            metric.UpdateDouble(value, tags);
        }

        internal List<Metric> AddMetricsListWithViews(Instrument instrument, List<MetricStreamConfiguration> metricStreamConfigs)
        {
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

                    if (this.metricStreamNames.Contains(metricStreamIdentity.MetricStreamName))
                    {
                        OpenTelemetrySdkEventSource.Log.DuplicateMetricInstrument(
                            metricStreamIdentity.InstrumentName,
                            metricStreamIdentity.MeterName,
                            "Metric instrument has the same name as an existing one but differs by description, unit, instrument type, or aggregation configuration (like histogram bounds, tag keys etc. ). Measurements from this instrument will still be exported but may result in conflicts.",
                            "Either change the name of the instrument or use MeterProviderBuilder.AddView to resolve the conflict.");
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
                        Metric metric;
                        metric = new Metric(metricStreamIdentity, this.GetAggregationTemporality(metricStreamIdentity.InstrumentType), this.maxMetricPointsPerMetricStream, metricStreamIdentity.HistogramBucketBounds, metricStreamIdentity.TagKeys);

                        this.instrumentIdentityToMetric[metricStreamIdentity] = metric;
                        this.metrics[index] = metric;
                        metrics.Add(metric);
                        this.metricStreamNames.Add(metricStreamIdentity.MetricStreamName);
                    }
                }

                return metrics;
            }
        }

        internal void RecordLongMeasurement(List<Metric> metrics, long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
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

        internal void RecordDoubleMeasurement(List<Metric> metrics, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
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
            metric.InstrumentDisposed = true;
        }

        internal void CompleteMeasurement(List<Metric> metrics)
        {
            foreach (var metric in metrics)
            {
                metric.InstrumentDisposed = true;
            }
        }

        internal void SetMaxMetricStreams(int maxMetricStreams)
        {
            this.maxMetricStreams = maxMetricStreams;
            this.metrics = new Metric[maxMetricStreams];
            this.metricsCurrentBatch = new Metric[maxMetricStreams];
        }

        internal void SetMaxMetricPointsPerMetricStream(int maxMetricPointsPerMetricStream)
        {
            this.maxMetricPointsPerMetricStream = maxMetricPointsPerMetricStream;
        }

        private Batch<Metric> GetMetricsBatch()
        {
            try
            {
                var indexSnapshot = Math.Min(this.metricIndex, this.maxMetricStreams - 1);
                var target = indexSnapshot + 1;
                int metricCountCurrentBatch = 0;
                for (int i = 0; i < target; i++)
                {
                    var metric = this.metrics[i];
                    int metricPointSize = 0;
                    if (metric != null)
                    {
                        if (metric.InstrumentDisposed)
                        {
                            metricPointSize = metric.Snapshot();
                            this.instrumentIdentityToMetric.TryRemove(metric.InstrumentIdentity, out var _);
                            this.metrics[i] = null;
                        }
                        else
                        {
                            metricPointSize = metric.Snapshot();
                        }

                        if (metricPointSize > 0)
                        {
                            this.metricsCurrentBatch[metricCountCurrentBatch++] = metric;
                        }
                    }
                }

                return (metricCountCurrentBatch > 0) ? new Batch<Metric>(this.metricsCurrentBatch, metricCountCurrentBatch) : default;
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.GetMetricsBatch), ex);
                return default;
            }
        }
    }
}
