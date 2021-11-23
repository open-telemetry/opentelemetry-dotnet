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
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// MetricReader which processes individual metrics.
    /// </summary>
    public abstract partial class MetricReader
    {
        private readonly HashSet<string> metricStreamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object instrumentCreationLock = new object();
        private int maxMetricStreams;
        private int maxMetricPointsPerMetricStream;
        private Metric[] metrics;
        private Metric[] metricsCurrentBatch;
        private int metricIndex = -1;

        internal Metric AddMetricWithNoViews(Instrument instrument)
        {
            var meterName = instrument.Meter.Name;
            var metricName = instrument.Name;
            var metricStreamName = $"{meterName}.{metricName}";
            lock (this.instrumentCreationLock)
            {
                if (this.metricStreamNames.Contains(metricStreamName))
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricName, instrument.Meter.Name, "Metric name conflicting with existing name.", "Either change the name of the instrument or change name using View.");
                    return null;
                }

                var index = ++this.metricIndex;
                if (index >= this.maxMetricStreams)
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricName, instrument.Meter.Name, "Maximum allowed Metrics for the provider exceeded.", "Use views to drop unused instruments. Or configure Provider to allow higher limit.");
                    return null;
                }
                else
                {
                    var metric = new Metric(instrument, this.Temporality, metricName, instrument.Description, this.maxMetricPointsPerMetricStream);
                    this.metrics[index] = metric;
                    this.metricStreamNames.Add(metricStreamName);
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
                    var meterName = instrument.Meter.Name;
                    var metricName = metricStreamConfig?.Name ?? instrument.Name;
                    var metricStreamName = $"{meterName}.{metricName}";

                    if (!MeterProviderBuilderSdk.IsValidInstrumentName(metricName))
                    {
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                            metricName,
                            instrument.Meter.Name,
                            "Metric name is invalid.",
                            "The name must comply with the OpenTelemetry specification.");

                        continue;
                    }

                    if (this.metricStreamNames.Contains(metricStreamName))
                    {
                        // TODO: Log that instrument is ignored
                        // as the resulting Metric name is conflicting
                        // with existing name.
                        continue;
                    }

                    if (metricStreamConfig?.Aggregation == Aggregation.Drop)
                    {
                        // TODO: Log that instrument is ignored
                        // as user explicitly asked to drop it
                        // with View.
                        continue;
                    }

                    var index = ++this.metricIndex;
                    if (index >= this.maxMetricStreams)
                    {
                        // TODO: Log that instrument is ignored
                        // as max number of Metrics have reached.
                    }
                    else
                    {
                        Metric metric;
                        var metricDescription = metricStreamConfig?.Description ?? instrument.Description;
                        string[] tagKeysInteresting = metricStreamConfig?.TagKeys;
                        double[] histogramBucketBounds = (metricStreamConfig is ExplicitBucketHistogramConfiguration histogramConfig
                            && histogramConfig.Boundaries != null) ? histogramConfig.Boundaries : null;
                        metric = new Metric(instrument, this.Temporality, metricName, metricDescription, this.maxMetricPointsPerMetricStream, histogramBucketBounds, tagKeysInteresting);

                        this.metrics[index] = metric;
                        metrics.Add(metric);
                        this.metricStreamNames.Add(metricStreamName);
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
            catch (Exception)
            {
                // TODO: Log
                return default;
            }
        }
    }
}
