// <copyright file="StatsExtensions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System;
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Tags;

namespace OpenTelemetry.Stats
{
    public static class StatsExtensions
    {
        public static bool ContainsKeys(this IView view, IEnumerable<TagKey> keys)
        {
            var columns = view.Columns;
            foreach (var key in keys)
            {
                if (!columns.Contains(key))
                {
                    return false;
                }
            }

            return true;
        }

        public static IAggregationData SumWithTags(this IViewData viewData, IEnumerable<TagValue> values = null)
        {
            return viewData.AggregationMap.WithTags(values).Sum(viewData.View);
        }

        public static IDictionary<TagValues, IAggregationData> WithTags(this IDictionary<TagValues, IAggregationData> aggMap, IEnumerable<TagValue> values)
        {
            var results = new Dictionary<TagValues, IAggregationData>();

            foreach (var kvp in aggMap)
            {
                if (TagValuesMatch(kvp.Key.Values, values))
                {
                    results.Add(kvp.Key, kvp.Value);
                }
            }

            return results;
        }

        public static IAggregationData Sum(this IDictionary<TagValues, IAggregationData> aggMap, IView view)
        {
            var sum = MutableViewData.CreateMutableAggregation(view.Aggregation);
            foreach (var agData in aggMap.Values)
            {
                Sum(sum, agData);
            }

            return MutableViewData.CreateAggregationData(sum, view.Measure);
        }

        private static bool TagValuesMatch(IEnumerable<TagValue> aggValues, IEnumerable<TagValue> values)
        {
            if (values == null)
            {
                return true;
            }

            if (aggValues.Count() != values.Count())
            {
                return false;
            }

            var first = aggValues.GetEnumerator();
            var second = values.GetEnumerator();

            while (first.MoveNext())
            {
                second.MoveNext();
                
                // Null matches any aggValue
                if (second.Current == null)
                {
                    continue;
                }

                if (first.Current != second.Current)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Sum(MutableAggregation combined, IAggregationData data)
        {
            data.Match<object>(
                (arg) =>
                {
                    if (combined is MutableSum sum)
                    {
                        sum.Add(arg.Sum);
                    }

                    return null;
                },
                (arg) =>
                {
                    if (combined is MutableSum sum)
                    {
                        sum.Add(arg.Sum);
                    }

                    return null;
                },
                (arg) =>
                {
                    if (combined is MutableCount count)
                    {
                        count.Add(arg.Count);
                    }

                    return null;
                },
                (arg) =>
                {
                    if (combined is MutableMean mean)
                    {
                        mean.Count = mean.Count + arg.Count;
                        mean.Sum = mean.Sum + (arg.Count * arg.Mean);
                        if (arg.Min < mean.Min)
                        {
                            mean.Min = arg.Min;
                        }

                        if (arg.Max > mean.Max)
                        {
                            mean.Max = arg.Max;
                        }
                    }

                    return null;
                },
                (arg) =>
                {
                    if (combined is MutableDistribution dist)
                    {
                        // Algorithm for calculating the combination of sum of squared deviations:
                        // https://en.wikipedia.org/wiki/Algorithms_for_calculating_variance#Parallel_algorithm.
                        if (dist.Count + arg.Count > 0)
                        {
                            var delta = arg.Mean - dist.Mean;
                            dist.SumOfSquaredDeviations =
                                dist.SumOfSquaredDeviations
                                    + arg.SumOfSquaredDeviations
                                    + (Math.Pow(delta, 2)
                                        * dist.Count
                                        * arg.Count
                                        / (dist.Count + arg.Count));
                        }

                        dist.Count += arg.Count;
                        dist.Sum += arg.Mean * arg.Count;
                        dist.Mean = dist.Sum / dist.Count;

                        if (arg.Min < dist.Min)
                        {
                            dist.Min = arg.Min;
                        }

                        if (arg.Max > dist.Max)
                        {
                            dist.Max = arg.Max;
                        }

                        var bucketCounts = arg.BucketCounts;
                        for (var i = 0; i < bucketCounts.Count; i++)
                        {
                            dist.BucketCounts[i] += bucketCounts[i];
                        }
                    }

                    return null;
                },
                (arg) =>
                {
                    if (combined is MutableLastValue lastValue)
                    {
                        lastValue.Initialized = true;
                        if (double.IsNaN(lastValue.LastValue))
                        {
                            lastValue.LastValue = arg.LastValue;
                        }
                        else
                        {
                            lastValue.LastValue += arg.LastValue;
                        }
                    }

                    return null;
                },
                (arg) =>
                {
                    if (combined is MutableLastValue lastValue)
                    {
                        lastValue.Initialized = true;
                        if (double.IsNaN(lastValue.LastValue))
                        {
                            lastValue.LastValue = arg.LastValue;
                        }
                        else
                        {
                            lastValue.LastValue += arg.LastValue;
                        }
                    }

                    return null;
                },
                (arg) =>
                {
                    throw new ArgumentException();
                });
        }
    }
}
