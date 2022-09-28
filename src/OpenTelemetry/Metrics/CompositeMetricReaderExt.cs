// <copyright file="CompositeMetricReaderExt.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// CompositeMetricReader that deals with adding metrics and recording measurements.
    /// </summary>
    internal sealed partial class CompositeMetricReader
    {
        internal List<Metric> AddMetricsWithNoViews(Instrument instrument)
        {
            var metrics = new List<Metric>(this.count);
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                var metric = cur.Value.AddMetricWithNoViews(instrument);
                metrics.Add(metric);
            }

            return metrics;
        }

        internal void RecordSingleStreamLongMeasurements(List<Metric> metrics, long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            Debug.Assert(metrics.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

            int index = 0;
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                if (metrics[index] != null)
                {
                    cur.Value.RecordSingleStreamLongMeasurement(metrics[index], value, tags);
                }

                index++;
            }
        }

        internal void RecordSingleStreamDoubleMeasurements(List<Metric> metrics, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            Debug.Assert(metrics.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

            int index = 0;
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                if (metrics[index] != null)
                {
                    cur.Value.RecordSingleStreamDoubleMeasurement(metrics[index], value, tags);
                }

                index++;
            }
        }

        internal List<List<Metric>> AddMetricsSuperListWithViews(Instrument instrument, List<MetricStreamConfiguration> metricStreamConfigs)
        {
            var metricsSuperList = new List<List<Metric>>(this.count);
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                var metrics = cur.Value.AddMetricsListWithViews(instrument, metricStreamConfigs);
                metricsSuperList.Add(metrics);
            }

            return metricsSuperList;
        }

        internal void RecordLongMeasurements(List<List<Metric>> metricsSuperList, long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            Debug.Assert(metricsSuperList.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

            int index = 0;
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                if (metricsSuperList[index].Count > 0)
                {
                    cur.Value.RecordLongMeasurement(metricsSuperList[index], value, tags);
                }

                index++;
            }
        }

        internal void RecordDoubleMeasurements(List<List<Metric>> metricsSuperList, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            Debug.Assert(metricsSuperList.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

            int index = 0;
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                if (metricsSuperList[index].Count > 0)
                {
                    cur.Value.RecordDoubleMeasurement(metricsSuperList[index], value, tags);
                }

                index++;
            }
        }

        internal void CompleteSingleStreamMeasurements(List<Metric> metrics)
        {
            Debug.Assert(metrics.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

            int index = 0;
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                if (metrics[index] != null)
                {
                    cur.Value.CompleteSingleStreamMeasurement(metrics[index]);
                }

                index++;
            }
        }

        internal void CompleteMesaurements(List<List<Metric>> metricsSuperList)
        {
            Debug.Assert(metricsSuperList.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

            int index = 0;
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                if (metricsSuperList[index].Count > 0)
                {
                    cur.Value.CompleteMeasurement(metricsSuperList[index]);
                }

                index++;
            }
        }
    }
}
