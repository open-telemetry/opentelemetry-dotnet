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
    private MetricState()
    {
    }

    internal delegate void RecordMeasurementAction<T>(T value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    public required Action CompleteMeasurement { get; init; }

    public required RecordMeasurementAction<long> RecordLongMeasurement { get; init; }

    public required RecordMeasurementAction<double> RecordDoubleMeasurement { get; init; }

    public static MetricState BuildForSingleMetric(
        Metric metric)
    {
        Debug.Assert(metric != null, "metric was null");

        return new()
        {
            CompleteMeasurement = () => MetricReader.DeactivateMetric(metric),
            RecordLongMeasurement = metric!.AggregatorStore.RecordMeasurement,
            RecordDoubleMeasurement = metric.AggregatorStore.RecordMeasurement,
        };
    }

    public static MetricState BuildForMetricList(
        List<Metric> metrics)
    {
        Debug.Assert(metrics != null, "metrics was null");

        return new()
        {
            CompleteMeasurement = () =>
            {
                foreach (var metric in metrics!)
                {
                    MetricReader.DeactivateMetric(metric);
                }
            },
            RecordLongMeasurement = (v, t) =>
            {
                foreach (var metric in metrics!)
                {
                    metric.AggregatorStore.RecordMeasurement(v, t);
                }
            },
            RecordDoubleMeasurement = (v, t) =>
            {
                foreach (var metric in metrics!)
                {
                    metric.AggregatorStore.RecordMeasurement(v, t);
                }
            },
        };
    }
}
