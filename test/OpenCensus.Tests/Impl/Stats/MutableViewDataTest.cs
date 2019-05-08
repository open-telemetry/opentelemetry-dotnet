// <copyright file="MutableViewDataTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Stats.Test
{
    using System;
    using System.Collections.Generic;
    using OpenCensus.Common;
    using OpenCensus.Stats.Aggregations;
    using OpenCensus.Stats.Measures;
    using OpenCensus.Tags;
    using Xunit;

    public class MutableViewDataTest
    {

        private const double EPSILON = 1e-7;

        private static readonly ITagKey ORIGINATOR = TagKey.Create("originator");
        private static readonly ITagKey CALLER = TagKey.Create("caller");
        private static readonly ITagKey METHOD = TagKey.Create("method");
        private static readonly ITagValue CALLER_V = TagValue.Create("some caller");
        private static readonly ITagValue METHOD_V = TagValue.Create("some method");
        private static readonly IMeasureDouble MEASURE_DOUBLE = MeasureDouble.Create("measure1", "description", "1");
        private static readonly IMeasureLong MEASURE_LONG = MeasureLong.Create("measure2", "description", "1");

        [Fact]
        public void TestConstants()
        {
            Assert.Null(MutableViewData.UnknownTagValue);
            Assert.Equal(Timestamp.Create(0, 0), MutableViewData.ZeroTimestamp);
        }

        [Fact]
        public void TestGetTagValues()
        {
            IReadOnlyList<ITagKey> columns = new List<ITagKey>() { CALLER, METHOD, ORIGINATOR };
            IDictionary<ITagKey, ITagValue> tags = new Dictionary<ITagKey, ITagValue>() { { CALLER, CALLER_V }, { METHOD, METHOD_V } };

            Assert.Equal(new List<ITagValue>() { CALLER_V, METHOD_V, MutableViewData.UnknownTagValue },
                MutableViewData.GetTagValues(tags, columns));

        }

        [Fact]
        public void CreateMutableAggregation()
        {
            IBucketBoundaries bucketBoundaries = BucketBoundaries.Create(new List<double>() { -1.0, 0.0, 1.0 });

            Assert.InRange(((MutableSum)MutableViewData.CreateMutableAggregation(Sum.Create())).Sum, 0.0 - EPSILON, 0.0 + EPSILON);
            Assert.Equal(0, ((MutableCount)MutableViewData.CreateMutableAggregation(Count.Create())).Count);
            Assert.InRange(((MutableMean)MutableViewData.CreateMutableAggregation(Mean.Create())).Mean, 0.0 - EPSILON, 0.0 + EPSILON);
            Assert.True(Double.IsNaN( ((MutableLastValue)MutableViewData.CreateMutableAggregation(LastValue.Create())).LastValue));

            MutableDistribution mutableDistribution =
                (MutableDistribution)MutableViewData.CreateMutableAggregation(Distribution.Create(bucketBoundaries));
            Assert.Equal(double.PositiveInfinity, mutableDistribution.Min);
            Assert.Equal(double.NegativeInfinity, mutableDistribution.Max);
            Assert.InRange(mutableDistribution.SumOfSquaredDeviations, 0.0 - EPSILON, 0.0 + EPSILON);
            Assert.Equal(new long[4], mutableDistribution.BucketCounts);
        }

        [Fact]
        public void CreateAggregationData()
        {
            IBucketBoundaries bucketBoundaries = BucketBoundaries.Create(new List<double>() { -1.0, 0.0, 1.0 });
            List<MutableAggregation> mutableAggregations =
                new List<MutableAggregation>() {
                    MutableCount.Create(),
                    MutableMean.Create(),
                    MutableDistribution.Create(bucketBoundaries),};
            List<IAggregationData> aggregates = new List<IAggregationData>();

            aggregates.Add(MutableViewData.CreateAggregationData(MutableSum.Create(), MEASURE_DOUBLE));
            aggregates.Add(MutableViewData.CreateAggregationData(MutableSum.Create(), MEASURE_LONG));
            aggregates.Add(MutableViewData.CreateAggregationData(MutableLastValue.Create(), MEASURE_DOUBLE));
            aggregates.Add(MutableViewData.CreateAggregationData(MutableLastValue.Create(), MEASURE_LONG));

            foreach (MutableAggregation mutableAggregation in mutableAggregations)
            {
                aggregates.Add(MutableViewData.CreateAggregationData(mutableAggregation, MEASURE_DOUBLE));
            }
            List<IAggregationData> expected = new List<IAggregationData>()
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

        [Fact]
        public void TestDurationToMillis()
        {
            Assert.Equal(0, MutableViewData.ToMillis(Duration.Create(0, 0)));
            Assert.Equal(987, MutableViewData.ToMillis(Duration.Create(0, 987000000)));
            Assert.Equal(3456, MutableViewData.ToMillis(Duration.Create(3, 456000000)));
            Assert.Equal(-1, MutableViewData.ToMillis(Duration.Create(0, -1000000)));
            Assert.Equal(-1000, MutableViewData.ToMillis(Duration.Create(-1, 0)));
            Assert.Equal(-3456, MutableViewData.ToMillis(Duration.Create(-3, -456000000)));
        }
    }
}
