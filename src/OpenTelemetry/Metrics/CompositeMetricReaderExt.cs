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

        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            var innerMetrics = cur.Value.AddMetricWithNoViews(instrument);
            if (innerMetrics.Count > 0)
            {
                metrics.AddRange(innerMetrics);
            }
        }

        return metrics;
    }

    internal override List<Metric> AddMetricWithViews(Instrument instrument, List<MetricStreamConfiguration?> metricStreamConfigs)
    {
        var metrics = new List<Metric>(this.count);
        for (var cur = this.Head; cur != null; cur = cur.Next)
        {
            var innerMetrics = cur.Value.AddMetricWithViews(instrument, metricStreamConfigs);

            metrics.AddRange(innerMetrics);
        }

        return metrics;
    }
}
