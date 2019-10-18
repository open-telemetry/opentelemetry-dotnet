// <copyright file="MutableViewDataTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class MutableViewDataTest
    {

        private const double EPSILON = 1e-7;

        private static readonly TagKey ORIGINATOR = TagKey.Create("originator");
        private static readonly TagKey CALLER = TagKey.Create("caller");
        private static readonly TagKey METHOD = TagKey.Create("method");
        private static readonly TagValue CALLER_V = TagValue.Create("some caller");
        private static readonly TagValue METHOD_V = TagValue.Create("some method");
        private static readonly IMeasureDouble MEASURE_DOUBLE = MeasureDouble.Create("measure1", "description", "1");
        private static readonly IMeasureLong MEASURE_LONG = MeasureLong.Create("measure2", "description", "1");

        [Fact]
        public void TestConstants()
        {
            Assert.Null(MutableViewData.UnknownTagValue);
        }

        [Fact]
        public void TestGetTagValues()
        {
            IReadOnlyList<TagKey> columns = new List<TagKey>() { CALLER, METHOD, ORIGINATOR };
            IDictionary<TagKey, TagValue> tags = new Dictionary<TagKey, TagValue>() { { CALLER, CALLER_V }, { METHOD, METHOD_V } };

            Assert.Equal(new List<TagValue>() { CALLER_V, METHOD_V, MutableViewData.UnknownTagValue },
                MutableViewData.GetTagValues(tags, columns));

        }

        [Fact]
        public void CreateMutableAggregation()
        {
            var bucketBoundaries = BucketBoundaries.Create(new List<double>() { -1.0, 0.0, 1.0 });

            Assert.InRange(((MutableSum)MutableViewData.CreateMutableAggregation(Sum.Create())).Sum, 0.0 - EPSILON, 0.0 + EPSILON);
            Assert.Equal(0, ((MutableCount)MutableViewData.CreateMutableAggregation(Count.Create())).Count);
            Assert.InRange(((MutableMean)MutableViewData.CreateMutableAggregation(Mean.Create())).Mean, 0.0 - EPSILON, 0.0 + EPSILON);
            Assert.True(Double.IsNaN( ((MutableLastValue)MutableViewData.CreateMutableAggregation(LastValue.Create())).LastValue));

            var mutableDistribution =
                (MutableDistribution)MutableViewData.CreateMutableAggregation(Distribution.Create(bucketBoundaries));
            Assert.Equal(double.PositiveInfinity, mutableDistribution.Min);
            Assert.Equal(double.NegativeInfinity, mutableDistribution.Max);
            Assert.InRange(mutableDistribution.SumOfSquaredDeviations, 0.0 - EPSILON, 0.0 + EPSILON);
            Assert.Equal(new long[4], mutableDistribution.BucketCounts);
        }

        [Fact]
        public void CreateAggregationData()
        {
            var bucketBoundaries = BucketBoundaries.Create(new List<double>() { -1.0, 0.0, 1.0 });
            var mutableAggregations =
                new List<MutableAggregation>() {
                    MutableCount.Create(),
                    MutableMean.Create(),
                    MutableDistribution.Create(bucketBoundaries),};
            var aggregates = new List<IAggregationData>();

            aggregates.Add(MutableViewData.CreateAggregationData(MutableSum.Create(), MEASURE_DOUBLE));
            aggregates.Add(MutableViewData.CreateAggregationData(MutableSum.Create(), MEASURE_LONG));
            aggregates.Add(MutableViewData.CreateAggregationData(MutableLastValue.Create(), MEASURE_DOUBLE));
            aggregates.Add(MutableViewData.CreateAggregationData(MutableLastValue.Create(), MEASURE_LONG));

            foreach (var mutableAggregation in mutableAggregations)
            {
                aggregates.Add(MutableViewData.CreateAggregationData(mutableAggregation, MEASURE_DOUBLE));
            }
            var expected = new List<IAggregationData>()
            {
                SumDataDouble.Create(0),
                SumDataLong.Create(0),
                LastValueDataDouble.Create(Double.NaN),
                LastValueDataLong.Create(0),
                CountData.Create(0),
                MeanData.Create(0, 0, Double.MaxValue, Double.MinValue),
                DistributionData.Create(
                        0,
                        0,
                        Double.PositiveInfinity,
                        Double.NegativeInfinity,
                        0,
                        new List<long>() { 0L, 0L, 0L, 0L }),
            };
            Assert.Equal(expected, aggregates);

        }
    }
}
