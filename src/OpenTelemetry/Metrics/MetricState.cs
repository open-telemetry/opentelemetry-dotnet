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
        Metric metric,
        KeyValuePair<string, object?>[]? instrumentTags)
    {
        if (instrumentTags is not { Length: > 0 })
        {
            return new(
                completeMeasurement: () => MetricReader.DeactivateMetric(metric),
                recordMeasurementLong: metric.UpdateLong,
                recordMeasurementDouble: metric.UpdateDouble);
        }

        var boundMetricPoint = metric.Bind(instrumentTags);

        return new(
            completeMeasurement: () => MetricReader.DeactivateMetric(metric),
            recordMeasurementLong: (value, tags) =>
            {
                if (tags.IsEmpty)
                {
                    boundMetricPoint.Update(value);
                }
                else
                {
                    var storage = ThreadStaticStorage.GetStorage();
                    storage.CombineTags(instrumentTags, tags, out var combinedTags, out var combinedTagCount);
                    metric.UpdateLong(value, combinedTags.AsSpan(0, combinedTagCount));
                }
            },
            recordMeasurementDouble: (value, tags) =>
            {
                if (tags.IsEmpty)
                {
                    boundMetricPoint.Update(value);
                }
                else
                {
                    var storage = ThreadStaticStorage.GetStorage();
                    storage.CombineTags(instrumentTags, tags, out var combinedTags, out var combinedTagCount);
                    metric.UpdateDouble(value, combinedTags.AsSpan(0, combinedTagCount));
                }
            });
    }

    public static MetricState BuildForMetricList(
        List<Metric> metrics,
        KeyValuePair<string, object?>[]? instrumentTags)
    {
        Debug.Assert(!metrics.Any(m => m == null), "metrics contained null elements");

        // Note: Use an array here to elide bounds checks.
        var metricsArray = metrics.ToArray();

        if (instrumentTags is not { Length: > 0 })
        {
            return new(
                completeMeasurement: () =>
                {
                    for (var i = 0; i < metricsArray.Length; i++)
                    {
                        MetricReader.DeactivateMetric(metricsArray[i]);
                    }
                },
                recordMeasurementLong: (v, t) =>
                {
                    for (var i = 0; i < metricsArray.Length; i++)
                    {
                        metricsArray[i].UpdateLong(v, t);
                    }
                },
                recordMeasurementDouble: (v, t) =>
                {
                    for (var i = 0; i < metricsArray.Length; i++)
                    {
                        metricsArray[i].UpdateDouble(v, t);
                    }
                });
        }

        var boundMetricPoints = new MetricPointUpdateHandle[metricsArray.Length];
        for (var i = 0; i < metricsArray.Length; i++)
        {
            boundMetricPoints[i] = metricsArray[i].Bind(instrumentTags);
        }

        return new(
            completeMeasurement: () =>
            {
                for (var i = 0; i < metricsArray.Length; i++)
                {
                    MetricReader.DeactivateMetric(metricsArray[i]);
                }
            },
            recordMeasurementLong: (v, t) =>
            {
                if (t.IsEmpty)
                {
                    for (var i = 0; i < boundMetricPoints.Length; i++)
                    {
                        boundMetricPoints[i].Update(v);
                    }
                }
                else
                {
                    var storage = ThreadStaticStorage.GetStorage();
                    storage.CombineTags(instrumentTags, t, out var combinedTags, out var combinedTagCount);
                    var combinedTagsSpan = combinedTags.AsSpan(0, combinedTagCount);
                    for (var i = 0; i < metricsArray.Length; i++)
                    {
                        metricsArray[i].UpdateLong(v, combinedTagsSpan);
                    }
                }
            },
            recordMeasurementDouble: (v, t) =>
            {
                if (t.IsEmpty)
                {
                    for (var i = 0; i < boundMetricPoints.Length; i++)
                    {
                        boundMetricPoints[i].Update(v);
                    }
                }
                else
                {
                    var storage = ThreadStaticStorage.GetStorage();
                    storage.CombineTags(instrumentTags, t, out var combinedTags, out var combinedTagCount);
                    var combinedTagsSpan = combinedTags.AsSpan(0, combinedTagCount);
                    for (var i = 0; i < metricsArray.Length; i++)
                    {
                        metricsArray[i].UpdateDouble(v, combinedTagsSpan);
                    }
                }
            });
    }
}
