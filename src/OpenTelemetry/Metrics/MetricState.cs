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
                    metric.UpdateLong(value, CombineTags(instrumentTags, tags));
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
                    metric.UpdateDouble(value, CombineTags(instrumentTags, tags));
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
                    var combinedTags = CombineTags(instrumentTags, t);
                    for (var i = 0; i < metricsArray.Length; i++)
                    {
                        metricsArray[i].UpdateLong(v, combinedTags);
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
                    var combinedTags = CombineTags(instrumentTags, t);
                    for (var i = 0; i < metricsArray.Length; i++)
                    {
                        metricsArray[i].UpdateDouble(v, combinedTags);
                    }
                }
            });
    }

    private static KeyValuePair<string, object?>[] CombineTags(
        KeyValuePair<string, object?>[] instrumentTags,
        ReadOnlySpan<KeyValuePair<string, object?>> measurementTags)
    {
        var combinedTags = new KeyValuePair<string, object?>[instrumentTags.Length + measurementTags.Length];
        instrumentTags.CopyTo(combinedTags, 0);

        var combinedTagCount = instrumentTags.Length;
        for (var i = 0; i < measurementTags.Length; i++)
        {
            var measurementTag = measurementTags[i];
            if (!ContainsTagKey(instrumentTags, measurementTag.Key))
            {
                combinedTags[combinedTagCount++] = measurementTag;
            }
        }

        if (combinedTagCount != combinedTags.Length)
        {
            Array.Resize(ref combinedTags, combinedTagCount);
        }

        return combinedTags;
    }

    private static bool ContainsTagKey(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        string key)
    {
        for (var i = 0; i < tags.Length; i++)
        {
            if (string.Equals(tags[i].Key, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
