// <copyright file="MetricTestsBase.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricTestsBase
{
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

    public static long GetLongSum(List<ExportableMetricCopy> metrics)
    {
        long sum = 0;
        foreach (var metric in metrics)
        {
            foreach (var metricPoint in metric.MetricPoints)
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

    public static double GetDoubleSum(List<ExportableMetricCopy> metrics)
    {
        double sum = 0;
        foreach (var metric in metrics)
        {
            foreach (var metricPoint in metric.MetricPoints)
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

    public static int GetNumberOfMetricPoints(List<ExportableMetricCopy> metrics)
    {
        int count = 0;
        foreach (var metric in metrics)
        {
            foreach (var metricPoint in metric.MetricPoints)
            {
                count++;
            }
        }

        return count;
    }

    public static MetricPoint? GetFirstMetricPoint(List<ExportableMetricCopy> metrics)
    {
        foreach (var metric in metrics)
        {
            foreach (var metricPoint in metric.MetricPoints)
            {
                return metricPoint;
            }
        }

        return null;
    }

    // Provide tags input sorted by Key
    public static void CheckTagsForNthMetricPoint(List<ExportableMetricCopy> metrics, List<KeyValuePair<string, object>> tags, int n)
    {
        var metric = metrics[0];
        var metricPointEnumerator = metric.MetricPoints.GetEnumerator();

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
}
