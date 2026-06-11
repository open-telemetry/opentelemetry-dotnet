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
    internal override List<Metric> AddMetricWithNoViews(Instrument instrument)
    {
        var metrics = new List<Metric>(this.count);

        for (var current = this.Head; current != null; current = current.Next)
        {
            var innerMetrics = current.Value.AddMetricWithNoViews(instrument);
            if (innerMetrics.Count > 0)
            {
                Debug.Assert(innerMetrics.Count == 1, "Multiple metrics returned without view configuration");

                metrics.AddRange(innerMetrics);
            }
        }

        return metrics;
    }

    internal override List<Metric> AddMetricWithViews(Instrument instrument, List<MetricStreamConfiguration?> metricStreamConfigs)
    {
        var metrics = new List<Metric>(this.count * metricStreamConfigs.Count);

        for (var current = this.Head; current != null; current = current.Next)
        {
            var innerMetrics = current.Value.AddMetricWithViews(instrument, metricStreamConfigs);

            metrics.AddRange(innerMetrics);
        }

        return metrics;
    }
}
