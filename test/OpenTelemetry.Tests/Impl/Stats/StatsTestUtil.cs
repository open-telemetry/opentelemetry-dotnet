// <copyright file="StatsTestUtil.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Stats.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenTelemetry.Stats.Aggregations;
    using OpenTelemetry.Tags;
    using Xunit;

    internal static class StatsTestUtil
    {
        internal static IAggregationData CreateAggregationData(IAggregation aggregation, IMeasure measure, params double[] values)
        {
            var mutableAggregation = MutableViewData.CreateMutableAggregation(aggregation);
            foreach (var value in values)
            {
                mutableAggregation.Add(value);
            }
            return MutableViewData.CreateAggregationData(mutableAggregation, measure);
        }

        internal static IViewData CreateEmptyViewData(IView view)
        {
            return ViewData.Create(
                view,
                new Dictionary<TagValues, IAggregationData>(),
                DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        }

        internal static void AssertAggregationMapEquals(
            IDictionary<TagValues, IAggregationData> actual,
            IDictionary<TagValues, IAggregationData> expected,
            double tolerance)
        {
            Assert.Equal(expected.Count, actual.Count);

            // Check actual/expected tags match indpendent of order
            foreach (var tagValue in actual.Keys)
            {
                Assert.True(expected.Keys.Contains(tagValue));
            }

            foreach (var tagValue in actual.Keys)
            {
                Assert.True(expected.Keys.Contains(tagValue));
            }

            // Confirm data for tagsValues matches
            foreach (var tagValue in actual.Keys)
            {
                var act = actual[tagValue];
                var exp = expected[tagValue];
                AssertAggregationDataEquals(exp, act, tolerance);
            }
        }

        internal static void AssertAggregationDataEquals(
            IAggregationData expected,
            IAggregationData actual,
            double tolerance)
        {
            expected.Match<object>(
                (arg) =>
                {
                    Assert.IsType<SumDataDouble>(actual);
                    Assert.InRange(((SumDataDouble)actual).Sum, arg.Sum - tolerance, arg.Sum + tolerance);
                    return null;
                },
                (arg) =>

                {
                    Assert.IsType<SumDataLong>(actual);
                    Assert.InRange(((SumDataLong)actual).Sum, arg.Sum - tolerance, arg.Sum + tolerance);
                    return null;
                },
                (arg) =>

                {
                    Assert.IsType<CountData>(actual);
                    Assert.Equal(arg.Count, ((CountData)actual).Count);
                    return null;
                },
                (arg) =>
                {
                    Assert.IsType<MeanData>(actual);
                    Assert.InRange(((MeanData)actual).Mean, arg.Mean - tolerance, arg.Mean + tolerance);
                    return null;
                },
                (arg) =>
                {
                    Assert.IsType<DistributionData>(actual);
                    AssertDistributionDataEquals(arg, (IDistributionData)actual, tolerance);
                    return null;
                },
                (arg) =>
                {
                    Assert.IsType<LastValueDataDouble>(actual);
                    Assert.InRange(((LastValueDataDouble)actual).LastValue, arg.LastValue - tolerance, arg.LastValue + tolerance);
                    return null;
                },
                (arg) =>
                {
                    Assert.IsType<LastValueDataLong>(actual);
                    Assert.Equal(arg.LastValue, ((LastValueDataLong)actual).LastValue);
                    return null;
                },
                (arg) =>
                 {
                     throw new ArgumentException();
                 });
        }

        private static void AssertDistributionDataEquals(
            IDistributionData expected,
            IDistributionData actual,
            double tolerance)
        {
            
            Assert.InRange(actual.Mean, expected.Mean - tolerance, expected.Mean + tolerance);
            Assert.Equal(expected.Count, actual.Count);
            Assert.InRange(actual.SumOfSquaredDeviations, expected.SumOfSquaredDeviations - tolerance, expected.SumOfSquaredDeviations + tolerance);

            if (expected.Max == Double.NegativeInfinity
                && expected.Min == Double.PositiveInfinity)
            {
                Assert.True(Double.IsNegativeInfinity(actual.Max));
                Assert.True(Double.IsPositiveInfinity(actual.Min));
            }
            else
            {
                Assert.InRange(actual.Max, expected.Max - tolerance, expected.Max + tolerance);
                Assert.InRange(actual.Min, expected.Min - tolerance, expected.Min + tolerance);
            }

            Assert.Equal(RemoveTrailingZeros(expected.BucketCounts), RemoveTrailingZeros((actual).BucketCounts));
        }

        private static IEnumerable<long> RemoveTrailingZeros(IEnumerable<long> longs)
        {
            if (longs == null || longs.Any())
            {
                return longs;
            }

            var buffer = new List<long>();
            var result = new List<long>();
            foreach (var item in longs)
            {
                if (item == 0)
                {
                    buffer.Add(item);
                }
                else
                {
                    foreach (var bufferedItem in buffer)
                    {
                        result.Add(bufferedItem);
                    }
                    buffer.Clear();
                    result.Add(item);
                }
            }

            return result;
        }
    }
}
