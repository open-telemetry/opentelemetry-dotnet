// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

        return new(
            completeMeasurement: () => MetricReader.DeactivateMetric(metric!),
            recordMeasurementLong: metric!.UpdateLong,
            recordMeasurementDouble: metric!.UpdateDouble);
    }

    public static MetricState BuildForMetricList(
        List<Metric> metrics)
    {
        Debug.Assert(!metrics.Any(m => m == null), "metrics contained null elements");

        // Note: Use an array here to elide bounds checks.
        var metricsArray = metrics.ToArray();

        return new(
            completeMeasurement: () =>
            {
                for (int i = 0; i < metricsArray.Length; i++)
                {
                    MetricReader.DeactivateMetric(metricsArray[i]);
                }
            },
            recordMeasurementLong: (v, t) =>
            {
                for (int i = 0; i < metricsArray.Length; i++)
                {
                    metricsArray[i].UpdateLong(v, t);
                }
            },
            recordMeasurementDouble: (v, t) =>
            {
                for (int i = 0; i < metricsArray.Length; i++)
                {
                    metricsArray[i].UpdateDouble(v, t);
                }
            });
    }
}
