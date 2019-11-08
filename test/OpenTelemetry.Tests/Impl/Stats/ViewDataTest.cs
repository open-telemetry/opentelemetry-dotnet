// <copyright file="ViewDataTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class ViewDataTest
    {
        // tag keys
        private static readonly string K1 = "k1";
        private static readonly string K2 = "k2";
        private static readonly IReadOnlyList<string> TAG_KEYS = new List<string>() { K1, K2 };

        // tag values
        private static readonly string V1 = "v1";
        private static readonly string V2 = "v2";
        private static readonly string V10 = "v10";
        private static readonly string V20 = "v20";

        // private static readonly AggregationWindow CUMULATIVE = Cumulative.Create();
        // private static readonly AggregationWindow INTERVAL_HOUR = Interval.Create(Duration.Create(3600, 0));

        private static readonly IBucketBoundaries BUCKET_BOUNDARIES =
            BucketBoundaries.Create(new List<double>() { 10.0, 20.0, 30.0, 40.0 });

        private static readonly IAggregation DISTRIBUTION = Distribution.Create(BUCKET_BOUNDARIES);

        private static readonly IDictionary<TagValues, IAggregationData> ENTRIES =
            new Dictionary<TagValues, IAggregationData>() {
          { TagValues.Create(new List<string>(){ V1, V2 }), DistributionData.Create(1, 1, 1, 1, 0, new List<long>() {0L, 1L, 0L }) },
          { TagValues.Create(new List<string>(){ V10, V20 }), DistributionData.Create(-5, 6, -20, 5, 100.1,  new List<long>() {5L, 0L, 1L }) },
            };

        // name
        private static readonly IViewName NAME = ViewName.Create("test-view");
        // description
        private static readonly String DESCRIPTION = "test-view-descriptor description";
        // measure
        private static readonly IMeasure MEASURE_DOUBLE =
            MeasureDouble.Create("measure1", "measure description", "1");

        private static readonly IMeasure MEASURE_LONG =
            MeasureLong.Create("measure2", "measure description", "1");

        [Fact]
        public void TestCumulativeViewData()
        {
            var view = View.Create(NAME, DESCRIPTION, MEASURE_DOUBLE, DISTRIBUTION, TAG_KEYS);
            var start = DateTimeOffset.FromUnixTimeMilliseconds(1000);
            var end = DateTimeOffset.FromUnixTimeMilliseconds(2000);
            var viewData = ViewData.Create(view, ENTRIES, start, end);
            Assert.Equal(view, viewData.View);
            Assert.Equal(ENTRIES, viewData.AggregationMap);
        }

        // [Fact]
        // public void TestIntervalViewData()
        // {
        //    View view =
        //        View.Create(NAME, DESCRIPTION, MEASURE_DOUBLE, DISTRIBUTION, TAG_KEYS, INTERVAL_HOUR);
        //    Timestamp end = Timestamp.fromMillis(2000);
        //    AggregationWindowData windowData = IntervalData.Create(end);
        //    ViewData viewData = ViewData.Create(view, ENTRIES, windowData);
        //    assertThat(viewData.getView()).isEqualTo(view);
        //    assertThat(viewData.getAggregationMap()).isEqualTo(ENTRIES);
        //    assertThat(viewData.getWindowData()).isEqualTo(windowData);
        // }

        [Fact]
        public void TestViewDataEquals()
        {
            var cumulativeView =
                View.Create(NAME, DESCRIPTION, MEASURE_DOUBLE, DISTRIBUTION, TAG_KEYS);
            // View intervalView =
            //    View.Create(NAME, DESCRIPTION, MEASURE_DOUBLE, DISTRIBUTION, TAG_KEYS, INTERVAL_HOUR);

            // new EqualsTester()
            //    .addEqualityGroup(
            var data1 = ViewData.Create(
                        cumulativeView,
                        ENTRIES,
                        DateTimeOffset.FromUnixTimeMilliseconds(1000), DateTimeOffset.FromUnixTimeMilliseconds(2000));
            var data2 = ViewData.Create(
                        cumulativeView,
                        ENTRIES,
                        DateTimeOffset.FromUnixTimeMilliseconds(1000), DateTimeOffset.FromUnixTimeMilliseconds(2000));
            Assert.Equal(data1, data2);

            // .addEqualityGroup(
            var data3 = ViewData.Create(
                        cumulativeView,
                        ENTRIES,
                        DateTimeOffset.FromUnixTimeMilliseconds(1000), DateTimeOffset.FromUnixTimeMilliseconds(3000));
            Assert.NotEqual(data1, data3);
            Assert.NotEqual(data2, data3);

            // .addEqualityGroup(
            // IViewData data4 = ViewData.Create(intervalView, ENTRIES, IntervalData.Create(Timestamp.fromMillis(2000))),
            // IViewData data5 = ViewData.Create(intervalView, ENTRIES, IntervalData.Create(Timestamp.fromMillis(2000))))
            // .addEqualityGroup(
            //    ViewData.Create(
            //        intervalView,
            //        Collections.< List<string>, AggregationData > emptyMap(),
            //        IntervalData.Create(Timestamp.fromMillis(2000))))
            // .testEquals();
        }

        // [Fact]
        //  public void testAggregationWindowDataMatch()
        //        {
        //            final Timestamp start = Timestamp.fromMillis(1000);
        //            final Timestamp end = Timestamp.fromMillis(2000);
        //            final AggregationWindowData windowData1 = CumulativeData.Create(start, end);
        //            final AggregationWindowData windowData2 = IntervalData.Create(end);
        //            windowData1.match(
        //                new Function<CumulativeData, Void>() {
        //          @Override
        //                  public Void apply(CumulativeData windowData)
        //            {
        //                assertThat(windowData.getStart()).isEqualTo(start);
        //                assertThat(windowData.getEnd()).isEqualTo(end);
        //                return null;
        //            }
        //        },
        //        new Function<IntervalData, Void>() {
        //          @Override
        //          public Void apply(IntervalData windowData)
        //        {
        //            fail("CumulativeData expected.");
        //            return null;
        //        }
        //    },
        //        Functions.<Void>throwIllegalArgumentException());
        //    windowData2.match(
        //        new Function<CumulativeData, Void>() {
        //          @Override
        //          public Void apply(CumulativeData windowData)
        //    {
        //        fail("IntervalData expected.");
        //        return null;
        //    }
        // },
        //        new Function<IntervalData, Void>() {
        //          @Override
        //          public Void apply(IntervalData windowData)
        // {
        //    assertThat(windowData.getEnd()).isEqualTo(end);
        //    return null;
        // }
        //        },
        //        Functions.<Void>throwIllegalArgumentException());
        //  }

        // [Fact]
        //  public void preventWindowAndAggregationWindowDataMismatch()
        // {
        //    thrown.expect(IllegalArgumentException);
        //    thrown.expectMessage("AggregationWindow and AggregationWindowData types mismatch. ");
        //    ViewData.Create(
        //        View.Create(NAME, DESCRIPTION, MEASURE_DOUBLE, DISTRIBUTION, TAG_KEYS, INTERVAL_HOUR),
        //        ENTRIES,
        //        CumulativeData.Create(Timestamp.fromMillis(1000), Timestamp.fromMillis(2000)));
        //  }

        // [Fact]
        //  public void preventWindowAndAggregationWindowDataMismatch2()
        // {
        //    thrown.expect(IllegalArgumentException.class);
        //    thrown.expectMessage("AggregationWindow and AggregationWindowData types mismatch. ");
        //    ViewData.Create(
        //        View.Create(NAME, DESCRIPTION, MEASURE_DOUBLE, DISTRIBUTION, TAG_KEYS, CUMULATIVE),
        //        ENTRIES,
        //        IntervalData.Create(Timestamp.fromMillis(1000)));
        //  }

        // [Fact]
        // public void PreventStartTimeLaterThanEndTime()
        // {
        //   // thrown.expect(IllegalArgumentException.class);
        //    CumulativeData.Create(Timestamp.fromMillis(3000), Timestamp.fromMillis(2000));
        //  }

        [Fact]
        public void PreventAggregationAndAggregationDataMismatch_SumDouble_SumLong()
        {
            var tagValues = TagValues.Create(new List<string>() { V1, V2 });
            AggregationAndAggregationDataMismatch(
                CreateView(Sum.Create(), MEASURE_DOUBLE),
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tagValues, SumDataLong.Create(100) },
                });
        }

        [Fact]
        public void PreventAggregationAndAggregationDataMismatch_SumLong_SumDouble()
        {
            var tagValues = TagValues.Create(new List<string>() { V1, V2 });
            AggregationAndAggregationDataMismatch(
                CreateView(Sum.Create(), MEASURE_LONG),
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tagValues, SumDataDouble.Create(100) },
                });
        }

        [Fact]
        public void PreventAggregationAndAggregationDataMismatch_Count_Distribution()
        {
            AggregationAndAggregationDataMismatch(CreateView(Count.Create()), ENTRIES);
        }

        [Fact]
        public void PreventAggregationAndAggregationDataMismatch_Mean_Distribution()
        {
            AggregationAndAggregationDataMismatch(CreateView(Mean.Create()), ENTRIES);
        }

        [Fact]
        public void PreventAggregationAndAggregationDataMismatch_Distribution_Count()
        {
            var tagValues1 = TagValues.Create(new List<string>() { V1, V2 });
            var tagValues2 = TagValues.Create(new List<string>() { V10, V20 });
            AggregationAndAggregationDataMismatch(
                CreateView(DISTRIBUTION),
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tagValues1, DistributionData.Create(1, 1, 1, 1, 0, new List<long>() {0L, 1L, 0L }) },
                    { tagValues2, CountData.Create(100) },
                });
        }

        [Fact]
        public void PreventAggregationAndAggregationDataMismatch_LastValueDouble_LastValueLong()
        {
            var tagValues = TagValues.Create(new List<string>() { V1, V2 });
            AggregationAndAggregationDataMismatch(
                CreateView(LastValue.Create(), MEASURE_DOUBLE),
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tagValues, LastValueDataLong.Create(100) },
                });
        }

        [Fact]
        public void PreventAggregationAndAggregationDataMismatch_LastValueLong_LastValueDouble()
        {
            var tagValues = TagValues.Create(new List<string>() { V1, V2 });
            AggregationAndAggregationDataMismatch(
                CreateView(LastValue.Create(), MEASURE_LONG),
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tagValues, LastValueDataDouble.Create(100) },
                });
        }

        private static IView CreateView(IAggregation aggregation)
        {
            return CreateView(aggregation, MEASURE_DOUBLE);
        }

        private static IView CreateView(IAggregation aggregation, IMeasure measure)
        {
            return View.Create(NAME, DESCRIPTION, measure, aggregation, TAG_KEYS);
        }

        private void AggregationAndAggregationDataMismatch(IView view, IDictionary<TagValues, IAggregationData> entries)
        {
            // CumulativeData cumulativeData =
            //    CumulativeData.Create(Timestamp.fromMillis(1000), Timestamp.fromMillis(2000));
            // thrown.expect(IllegalArgumentException);
            // thrown.expectMessage("Aggregation and AggregationData types mismatch. ");
            Assert.Throws<ArgumentException>(() => ViewData.Create(view, entries, DateTimeOffset.FromUnixTimeMilliseconds(1000), DateTimeOffset.FromUnixTimeMilliseconds(2000)));
        }
    }
}
