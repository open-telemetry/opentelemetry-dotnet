// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricTestsBase
{
    public const string EmitOverFlowAttributeConfigKey = "OTEL_DOTNET_EXPERIMENTAL_METRICS_EMIT_OVERFLOW_ATTRIBUTE";

    // This method relies on the assumption that MetricPoints are exported in the order in which they are emitted.
    // For Delta AggregationTemporality, this holds true only until the AggregatorStore has not begun recaliming the MetricPoints.
    public static void ValidateMetricPointTags(List<KeyValuePair<string, object>> expectedTags, ReadOnlyTagCollection actualTags)
    {
        int tagIndex = 0;
        foreach (var tag in actualTags)
        {
            Assert.Equal(expectedTags[tagIndex].Key, tag.Key);
            Assert.Equal(expectedTags[tagIndex].Value, tag.Value);
            tagIndex++;
        }

        Assert.Equal(expectedTags.Count, tagIndex);
    }

    public static long GetLongSum(List<Metric> metrics)
    {
        long sum = 0;
        foreach (var metric in metrics)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                if (metric.MetricType.IsSum())
                {
                    sum += metricPoint.GetSumLong();
                }
                else
                {
                    sum += metricPoint.GetGaugeLastValueLong();
                }
            }
        }

        return sum;
    }

    public static double GetDoubleSum(List<Metric> metrics)
    {
        double sum = 0;
        foreach (var metric in metrics)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                if (metric.MetricType.IsSum())
                {
                    sum += metricPoint.GetSumDouble();
                }
                else
                {
                    sum += metricPoint.GetGaugeLastValueDouble();
                }
            }
        }

        return sum;
    }

    public static int GetNumberOfMetricPoints(List<Metric> metrics)
    {
        int count = 0;
        foreach (var metric in metrics)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                count++;
            }
        }

        return count;
    }

    public static MetricPoint? GetFirstMetricPoint(List<Metric> metrics)
    {
        foreach (var metric in metrics)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                return metricPoint;
            }
        }

        return null;
    }

    // This method relies on the assumption that MetricPoints are exported in the order in which they are emitted.
    // For Delta AggregationTemporality, this holds true only until the AggregatorStore has not begun recaliming the MetricPoints.
    // Provide tags input sorted by Key
    public static void CheckTagsForNthMetricPoint(List<Metric> metrics, List<KeyValuePair<string, object>> tags, int n)
    {
        var metric = metrics[0];
        var metricPointEnumerator = metric.GetMetricPoints().GetEnumerator();

        for (int i = 0; i < n; i++)
        {
            Assert.True(metricPointEnumerator.MoveNext());
        }

        int index = 0;
        var metricPoint = metricPointEnumerator.Current;
        foreach (var tag in metricPoint.Tags)
        {
            Assert.Equal(tags[index].Key, tag.Key);
            Assert.Equal(tags[index].Value, tag.Value);
            index++;
        }
    }

    internal static Exemplar[] GetExemplars(MetricPoint mp)
    {
        return mp.GetExemplars().Where(exemplar => exemplar.Timestamp != default).ToArray();
    }
}
