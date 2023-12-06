// <copyright file="MetricState.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

internal sealed class MetricState
{
    public readonly Action CompleteMeasurement;

    public readonly RecordMeasurementAction<long> RecordMeasurementLong;
    public readonly RecordMeasurementAction<double> RecordMeasurementDouble;

    private MetricState(
        Action completeMeasurement,
        RecordMeasurementAction<long> recordMeasurementLong,
        RecordMeasurementAction<double> recordMeasurementDouble)
    {
        this.CompleteMeasurement = completeMeasurement;
        this.RecordMeasurementLong = recordMeasurementLong;
        this.RecordMeasurementDouble = recordMeasurementDouble;
    }

    internal delegate void RecordMeasurementAction<T>(T value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    public static MetricState BuildForSingleMetric(
        Metric metric)
    {
        Debug.Assert(metric != null, "metric was null");
        Debug.Assert(metric!.AggregatorStore != null, "aggregatorStore was null");

        var aggregatorStore = metric!.AggregatorStore!;

        var recordMeasurementLong = aggregatorStore.MeasurementHandler.RecordMeasurement<long>;
        var recordMeasurementDouble = aggregatorStore.MeasurementHandler.RecordMeasurement<double>;

        return new(
            completeMeasurement: () => MetricReader.DeactivateMetric(metric),
            recordMeasurementLong: (v, t) => recordMeasurementLong(aggregatorStore, v, t),
            recordMeasurementDouble: (v, t) => recordMeasurementDouble(aggregatorStore!, v, t));
    }

    public static MetricState BuildForMetricList(
        List<Metric> metrics)
    {
        Debug.Assert(metrics != null, "metrics was null");
        Debug.Assert(!metrics.Any(m => m == null), "metrics contained null elements");

        var metricHandlers = metrics.Select(m =>
        {
            Debug.Assert(m.AggregatorStore != null, "aggregatorStore was null");

            var aggregatorStore = m!.AggregatorStore!;

            var recordMeasurementLong = aggregatorStore.MeasurementHandler.RecordMeasurement<long>;
            var recordMeasurementDouble = aggregatorStore.MeasurementHandler.RecordMeasurement<double>;

            return new
            {
                Metric = m,
                AggregatorStore = aggregatorStore,
                RecordMeasurementLong = recordMeasurementLong,
                RecordMeasurementDouble = recordMeasurementDouble,
            };
        }).ToArray();

        return new(
            completeMeasurement: () =>
            {
                for (int i = 0; i < metricHandlers.Length; i++)
                {
                    var h = metricHandlers[i];

                    MetricReader.DeactivateMetric(h.Metric);
                }
            },
            recordMeasurementLong: (v, t) =>
            {
                for (int i = 0; i < metricHandlers.Length; i++)
                {
                    var h = metricHandlers[i];

                    h.RecordMeasurementLong(
                        h.AggregatorStore,
                        v,
                        t);
                }
            },
            recordMeasurementDouble: (v, t) =>
            {
                for (int i = 0; i < metricHandlers.Length; i++)
                {
                    var h = metricHandlers[i];

                    h.RecordMeasurementDouble(
                        h.AggregatorStore,
                        v,
                        t);
                }
            });
    }
}
