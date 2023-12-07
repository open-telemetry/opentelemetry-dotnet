// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

internal interface IMetricMeasurementHandler
{
    void RecordMeasurement<T>(
        AggregatorStore aggregatorStore,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags);

    void RecordMeasurementOnMetricPoint<T>(
        AggregatorStore aggregatorStore,
        ref MetricPoint metricPoint,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags);

    int CollectMeasurements(AggregatorStore aggregatorStore);

    void CollectMeasurementsOnMetricPoint(ref MetricPoint metricPoint);
}
