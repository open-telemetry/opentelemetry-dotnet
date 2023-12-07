// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// CompositeMetricReader that deals with adding metrics and recording measurements.
/// </summary>
internal sealed partial class CompositeMetricReader
{
    internal List<Metric?> AddMetricsWithNoViews(Instrument instrument)
    {
        var metrics = new List<Metric?>(this.count);

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            metrics.Add(cur.Value.AddMetricWithNoViews(instrument));
        }

        return metrics;
    }

    internal void RecordSingleStreamLongMeasurements(List<Metric?> metrics, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Debug.Assert(metrics.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

        int index = 0;
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            var metric = metrics[index];
            if (metric != null)
            {
                cur.Value.RecordSingleStreamLongMeasurement(metric, value, tags);
            }

            index++;
        }
    }

    internal void RecordSingleStreamDoubleMeasurements(List<Metric?> metrics, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Debug.Assert(metrics.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

        int index = 0;
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            var metric = metrics[index];
            if (metric != null)
            {
                cur.Value.RecordSingleStreamDoubleMeasurement(metric, value, tags);
            }

            index++;
        }
    }

    internal List<List<Metric>> AddMetricsSuperListWithViews(Instrument instrument, List<MetricStreamConfiguration?> metricStreamConfigs)
    {
        var metricsSuperList = new List<List<Metric>>(this.count);
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            var metrics = cur.Value.AddMetricsListWithViews(instrument, metricStreamConfigs);
            metricsSuperList.Add(metrics);
        }

        return metricsSuperList;
    }

    internal void RecordLongMeasurements(List<List<Metric>> metricsSuperList, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
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

    internal void RecordDoubleMeasurements(List<List<Metric>> metricsSuperList, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
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

    internal void CompleteSingleStreamMeasurements(List<Metric?> metrics)
    {
        Debug.Assert(metrics.Count == this.count, "The count of metrics to be updated for a CompositeReader must match the number of individual readers.");

        int index = 0;
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            var metric = metrics[index];
            if (metric != null)
            {
                cur.Value.CompleteSingleStreamMeasurement(metric);
            }

            index++;
        }
    }

    internal void CompleteMeasurements(List<List<Metric>> metricsSuperList)
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