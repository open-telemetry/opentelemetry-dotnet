// <copyright file="ViewManagerTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Api.Utils;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class ViewManagerTest
    {
        private static readonly string Key = "Key";
        private static readonly string Value = "Value";
        private static readonly string Value2 = "Value2";
        private static readonly String MeasureUnit = "us";
        private static readonly String MeasureDescription = "measure description";
        private static readonly IMeasureDouble MeasureDouble = CreateRandomMeasureDouble();
        private static readonly IMeasureLong MeasureLong = Measures.MeasureLong.Create(CreateRandomMeasureName(), MeasureDescription, MeasureUnit);

        private static readonly IViewName ViewName = OpenTelemetry.Stats.ViewName.Create("my view");
        private static readonly IViewName ViewName2 = OpenTelemetry.Stats.ViewName.Create("my view 2");

        private static readonly string VIEW_DESCRIPTION = "view description";

        private static readonly double Epsilon = 1e-7;
        private static readonly int RandomNameLen = 8;
        private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);

        private static readonly IBucketBoundaries BucketBoundaries =
            OpenTelemetry.Stats.BucketBoundaries.Create(
                new List<double>() {
              0.0, 0.2, 0.5, 1.0, 2.0, 3.0, 4.0, 5.0, 7.0, 10.0, 15.0, 20.0, 30.0, 40.0, 50.0,});

        private static readonly ISum Sum = Aggregations.Sum.Create();
        private static readonly IMean Mean = Aggregations.Mean.Create();
        private static readonly IDistribution Distribution = Aggregations.Distribution.Create(BucketBoundaries);
        private static readonly ILastValue LastValue = Aggregations.LastValue.Create();

        private static readonly IViewManager viewManager = Stats.ViewManager;
        private static readonly IStatsRecorder statsRecorder = Stats.StatsRecorder;
        private static readonly CurrentTaggingState state = new CurrentTaggingState();
        private readonly ITagger tagger;

        public ViewManagerTest()
        {
            tagger = new Tagger(state);
        }

        private static IView CreateCumulativeView()
        {
            return CreateCumulativeView(CreateRandomViewName(), CreateRandomMeasureDouble(), Distribution, new List<string>() { Key });
        }

        private static IView CreateCumulativeView(
            IViewName name, IMeasure measure, IAggregation aggregation, List<string> keys)
        {
            return View.Create(name, VIEW_DESCRIPTION, measure, aggregation, keys);
        }

        [Fact]
        public void TestRegisterAndGetCumulativeView()
        {
            IView view = CreateCumulativeView();
            viewManager.RegisterView(view);
            Assert.Equal(view, viewManager.GetView(view.Name).View);
            Assert.Empty(viewManager.GetView(view.Name).AggregationMap);
            // Assert.Equal(viewManager.GetView(view.Name).getWindowData()).isInstanceOf(CumulativeData);
        }

        [Fact]
        public void TestGetAllExportedViews()
        {
            //Assert.Empty(viewManager.AllExportedViews);

            IViewName viewName1 = CreateRandomViewName();
            IViewName viewName2 = CreateRandomViewName();
            IMeasureDouble measure = CreateRandomMeasureDouble();

            IView cumulativeView1 =
                CreateCumulativeView(
                    viewName1, measure, Distribution, new List<string>() { Key });
            IView cumulativeView2 =
                CreateCumulativeView(
                    viewName2, measure, Distribution, new List<string>() { Key });
            // View intervalView =
            //    View.Create(
            //        View.Name.Create("View 3"),
            //        VIEW_DESCRIPTION,
            //        measure,
            //        Distribution,
            //        Arrays.asList(Key),
            //        INTERVAL);
            viewManager.RegisterView(cumulativeView1);
            viewManager.RegisterView(cumulativeView2);

            // Only cumulative views should be exported.
            Assert.Contains(cumulativeView1, viewManager.AllExportedViews);
            Assert.Contains(cumulativeView2, viewManager.AllExportedViews);
            //Assert.Equal(2, viewManager.AllExportedViews.Count);
        }

        [Fact]
        public void GetAllExportedViewsResultIsUnmodifiable()
        {
            IViewName viewName1 = CreateRandomViewName();
            IViewName viewName2 = CreateRandomViewName();
            IMeasureDouble measure = CreateRandomMeasureDouble();

            IView view1 =
                View.Create(
                    viewName1,
                    VIEW_DESCRIPTION,
                    measure,
                    Distribution,
                    new List<string>() { Key });
            viewManager.RegisterView(view1);
            ISet<IView> exported = viewManager.AllExportedViews;

            IView view2 =
                View.Create(
                    viewName2,
                    VIEW_DESCRIPTION,
                    measure,
                    Distribution,
                    new List<string>() { Key });
            Assert.Throws<NotSupportedException>(() => exported.Add(view2));
        }

        // [Fact]
        //  public void TestRegisterAndGetIntervalView()
        // {
        //    View intervalView =
        //        View.Create(
        //            VIEW_NAME,
        //            VIEW_DESCRIPTION,
        //            MeasureDouble,
        //            Distribution,
        //            Arrays.asList(Key),
        //            INTERVAL);
        //    viewManager.RegisterView(intervalView);
        //    Assert.Equal(viewManager.GetView(VIEW_NAME).GetView()).isEqualTo(intervalView);
        //    Assert.Equal(viewManager.GetView(VIEW_NAME).AggregationMap).isEmpty();
        //    Assert.Equal(viewManager.GetView(VIEW_NAME).getWindowData()).isInstanceOf(IntervalData.class);
        //  }

        [Fact]
        public void AllowRegisteringSameViewTwice()
        {
            IView view = CreateCumulativeView();
            viewManager.RegisterView(view);
            viewManager.RegisterView(view);
            Assert.Equal(view, viewManager.GetView(view.Name).View);
        }

        [Fact]
        public void PreventRegisteringDifferentViewWithSameName()
        {
            IViewName viewName = CreateRandomViewName();
            IMeasureDouble measure = CreateRandomMeasureDouble();

            IView view1 =
                View.Create(
                    viewName,
                    "View description.",
                    measure,
                    Distribution,
                    new List<string>() { Key });
            IView view2 =
                View.Create(
                    viewName,
                    "This is a different description.",
                    measure,
                    Distribution,
                    new List<string>() { Key });
            TestFailedToRegisterView(view1, view2, "A different view with the same name is already registered");
        }

        [Fact]
        public void PreventRegisteringDifferentMeasureWithSameName()
        {
            IMeasureDouble measure1 = Measures.MeasureDouble.Create("measure", "description", "1");
            IMeasureLong measure2 = Measures.MeasureLong.Create("measure", "description", "1");

            IViewName viewName1 = CreateRandomViewName();
            IViewName viewName2 = CreateRandomViewName();

            IView view1 =
                View.Create(
                    viewName1, VIEW_DESCRIPTION, measure1, Distribution, new List<string>() { Key });
            IView view2 =
                View.Create(
                    viewName2, VIEW_DESCRIPTION, measure2, Distribution, new List<string>() { Key });
            TestFailedToRegisterView(view1, view2, "A different measure with the same name is already registered");
        }

        private void TestFailedToRegisterView(IView view1, IView view2, String message)
        {
            viewManager.RegisterView(view1);
            try
            {
                Assert.Throws<ArgumentException>(() => viewManager.RegisterView(view2));
            } finally {
                Assert.Equal(view1, viewManager.GetView(view1.Name).View);
            }
        }

        [Fact]
        public void ReturnNullWhenGettingNonexistentViewData()
        {
            Assert.Null(viewManager.GetView(CreateRandomViewName()));
        }

        [Fact]
        public void TestRecordDouble_Distribution_Cumulative()
        {
            TestRecordCumulative(CreateRandomMeasureDouble(), Distribution, 10.0, 20.0, 30.0, 40.0);
        }

        [Fact]
        public void TestRecordLong_Distribution_Cumulative()
        {
            TestRecordCumulative(CreateRandomMeasureLong(), Distribution, 1000, 2000, 3000, 4000);
        }

        [Fact]
        public void TestRecordDouble_Sum_Cumulative()
        {
            TestRecordCumulative(CreateRandomMeasureDouble(), Sum, 11.1, 22.2, 33.3, 44.4);
        }

        [Fact]
        public void TestRecordLong_Sum_Cumulative()
        {
            TestRecordCumulative(CreateRandomMeasureLong(), Sum, 1000, 2000, 3000, 4000);
        }

        [Fact]
        public void TestRecordDouble_Lastvalue_Cumulative()
        {
            TestRecordCumulative(CreateRandomMeasureDouble(), LastValue, 11.1, 22.2, 33.3, 44.4);
        }

        [Fact]
        public void TestRecordLong_Lastvalue_Cumulative()
        {
            TestRecordCumulative(CreateRandomMeasureLong(), LastValue, 1000, 2000, 3000, 4000);
        }

        private void TestRecordCumulative(IMeasure measure, IAggregation aggregation, params double[] values)
        {
            IView view = CreateCumulativeView(CreateRandomViewName(), measure, aggregation, new List<string>() { Key });
            viewManager.RegisterView(view);
            ITagContext tags = tagger.EmptyBuilder.Put(Key, Value).Build();
            foreach (double val in values)
            {
                PutToMeasureMap(statsRecorder.NewMeasureMap(), measure, val).Record(tags);
            }
            IViewData viewData = viewManager.GetView(view.Name);

            Assert.Equal(view, viewData.View);

            var tv = TagValues.Create(new List<string>() { Value });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv,  StatsTestUtil.CreateAggregationData(aggregation, measure, values) },
                },
                Epsilon);
        }


        [Fact]
        public void GetViewDoesNotClearStats()
        {
            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Distribution, new List<string>() { Key });
            viewManager.RegisterView(view);
            ITagContext tags = tagger.EmptyBuilder.Put(Key, Value).Build();
            statsRecorder.NewMeasureMap().Put(MeasureDouble, 0.1).Record(tags);
            IViewData viewData1 = viewManager.GetView(view.Name);
            var tv = TagValues.Create(new List<string>() { Value });

            StatsTestUtil.AssertAggregationMapEquals(
                viewData1.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv,  StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 0.1) },
                },
                Epsilon);

            statsRecorder.NewMeasureMap().Put(MeasureDouble, 0.2).Record(tags);
            IViewData viewData2 = viewManager.GetView(view.Name);

            // The second view should have the same start time as the first view, and it should include both
            // Recorded values:

            StatsTestUtil.AssertAggregationMapEquals(
                viewData2.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv,  StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 0.1, 0.2) },
                },
                Epsilon);
        }

        [Fact]
        public void TestRecordCumulativeMultipleTagValues()
        {
            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Distribution, new List<string>() { Key });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 10.0)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 30.0)
                .Record(tagger.EmptyBuilder.Put(Key, Value2).Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 50.0)
                .Record(tagger.EmptyBuilder.Put(Key, Value2).Build());
            IViewData viewData = viewManager.GetView(view.Name);
            var tv = TagValues.Create(new List<string>() { Value });
            var tv2 = TagValues.Create(new List<string>() { Value2 });

            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 10.0)},
                    { tv2, StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 30.0, 50.0)},
                },
                Epsilon);
        }


        // This test checks that MeasureMaper.Record(...) does not throw an exception when no views are
        // registered.
        [Fact]
        public void AllowRecordingWithoutRegisteringMatchingViewData()
        {
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 10)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
        }

        [Fact]
        public void TestRecordWithEmptyStatsContext()
        {
            Stats.State = StatsCollectionState.ENABLED;

            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Distribution, new List<string>() { Key });
            viewManager.RegisterView(view);
            // DEFAULT doesn't have tags, but the view has tag key "Key".
            statsRecorder.NewMeasureMap().Put(MeasureDouble, 10.0).Record(tagger.Empty);
            IViewData viewData = viewManager.GetView(view.Name);
            var tv = TagValues.Create(new List<string>() { MutableViewData.UnknownTagValue });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    // Tag is missing for associated measureValues, should use default tag value
                    // "unknown/not set".
                    { tv,
                    // Should Record stats with default tag value: "Key" : "unknown/not set".
                    StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 10.0)
                    },
                },
                Epsilon);

        }

        [Fact]
        public void TestRecord_MeasureNameNotMatch()
        {
            TestRecord_MeasureNotMatch(
                Measures.MeasureDouble.Create(CreateRandomMeasureName(), "measure", MeasureUnit),
                Measures.MeasureDouble.Create(CreateRandomMeasureName(), "measure", MeasureUnit),
                10.0);
        }

        [Fact]
        public void TestRecord_MeasureTypeNotMatch()
        {
            string name = CreateRandomMeasureName();
            TestRecord_MeasureNotMatch(
                Measures.MeasureLong.Create(name, "measure", MeasureUnit),
                Measures.MeasureDouble.Create(name, "measure", MeasureUnit),
                10.0);
        }

        private void TestRecord_MeasureNotMatch(IMeasure measure1, IMeasure measure2, double value)
        {
            IView view = CreateCumulativeView(CreateRandomViewName(), measure1, Mean, new List<string>() { Key });
            viewManager.RegisterView(view);
            ITagContext tags = tagger.EmptyBuilder.Put(Key, Value).Build();
            PutToMeasureMap(statsRecorder.NewMeasureMap(), measure2, value).Record(tags);
            IViewData viewData = viewManager.GetView(view.Name);
            Assert.Empty(viewData.AggregationMap);
        }

        [Fact]
        public void TestRecordWithTagsThatDoNotMatchViewData()
        {
            Stats.State = StatsCollectionState.ENABLED;

            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Distribution, new List<string>() { Key });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 10.0)
                .Record(tagger.EmptyBuilder.Put("wrong key", Value).Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 50.0)
                .Record(tagger.EmptyBuilder.Put("another wrong key", Value).Build());
            IViewData viewData = viewManager.GetView(view.Name);
            var tv = TagValues.Create(new List<string>() { MutableViewData.UnknownTagValue });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                { 
                    // Won't Record the unregistered tag key, for missing registered keys will use default
                    // tag value : "unknown/not set".
                    { tv,
                    // Should Record stats with default tag value: "Key" : "unknown/not set".
                    StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 10.0, 50.0)
                    },
                },
                Epsilon);
        }

        [Fact]
        public void TestViewDataWithMultipleTagKeys()
        {
            string key1 = "Key-1";
            string key2 = "Key-2";
            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Distribution, new List<string>() { key1, key2 });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 1.1)
                .Record(
                    tagger
                        .EmptyBuilder
                        .Put(key1, "v1")
                        .Put(key2, "v10")
                        .Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 2.2)
                .Record(
                    tagger
                        .EmptyBuilder
                        .Put(key1, "v1")
                        .Put(key2, "v20")
                        .Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 3.3)
                .Record(
                    tagger
                        .EmptyBuilder
                        .Put(key1, "v2")
                        .Put(key2, "v10")
                        .Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 4.4)
                .Record(
                    tagger
                        .EmptyBuilder
                        .Put(key1, "v1")
                        .Put(key2, "v10")
                        .Build());
            IViewData viewData = viewManager.GetView(view.Name);
            var tv1 = TagValues.Create(new List<string>() { "v1", "v10" });
            var tv2 = TagValues.Create(new List<string>() { "v1", "v20" });
            var tv3 = TagValues.Create(new List<string>() { "v2", "v10" });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv1,  StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 1.1, 4.4) },
                    { tv2,  StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 2.2) },
                    { tv3,  StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 3.3)},
                },
                Epsilon);
        }

        [Fact]
        public void TestMultipleViewSameMeasure()
        {
            IView view1 =
                CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Distribution, new List<string>() { Key });
            IView view2 =
                CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Distribution, new List<string>() { Key });
            viewManager.RegisterView(view1);
            viewManager.RegisterView(view2);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 5.0)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
            IViewData viewData1 = viewManager.GetView(view1.Name);
            IViewData viewData2 = viewManager.GetView(view2.Name);
            var tv = TagValues.Create(new List<string>() { Value });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData1.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 5.0) },
                },
                Epsilon);

            StatsTestUtil.AssertAggregationMapEquals(
                viewData2.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(Distribution, MeasureDouble, 5.0) },
                },
                Epsilon);
        }

        [Fact]
        public void TestMultipleViews_DifferentMeasureNames()
        {
            TestMultipleViews_DifferentMeasures(
                Measures.MeasureDouble.Create(CreateRandomMeasureName(), MeasureDescription, MeasureUnit),
                Measures.MeasureDouble.Create(CreateRandomMeasureName(), MeasureDescription, MeasureUnit),
                1.1,
                2.2);
        }

        [Fact]
        public void TestMultipleViews_DifferentMeasureTypes()
        {
            TestMultipleViews_DifferentMeasures(
                Measures.MeasureDouble.Create(CreateRandomMeasureName(), MeasureDescription, MeasureUnit),
                Measures.MeasureLong.Create(CreateRandomMeasureName(), MeasureDescription, MeasureUnit),
                1.1,
                5000);
        }

        private void TestMultipleViews_DifferentMeasures(IMeasure measure1, IMeasure measure2, double value1, double value2)
        {
            IView view1 = CreateCumulativeView(CreateRandomViewName(), measure1, Distribution, new List<string>() { Key });
            IView view2 = CreateCumulativeView(CreateRandomViewName(), measure2, Distribution, new List<string>() { Key });
            viewManager.RegisterView(view1);
            viewManager.RegisterView(view2);
            ITagContext tags = tagger.EmptyBuilder.Put(Key, Value).Build();
            IMeasureMap measureMap = statsRecorder.NewMeasureMap();
            PutToMeasureMap(measureMap, measure1, value1);
            PutToMeasureMap(measureMap, measure2, value2);
            measureMap.Record(tags);
            IViewData viewData1 = viewManager.GetView(view1.Name);
            IViewData viewData2 = viewManager.GetView(view2.Name);
            var tv = TagValues.Create(new List<string>() { Value });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData1.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(Distribution, measure1, value1) },
                },
                Epsilon);

            StatsTestUtil.AssertAggregationMapEquals(
                viewData2.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(Distribution, measure2, value2) },
                },
                Epsilon);
        }

        [Fact]
        public void TestGetCumulativeViewDataWithEmptyBucketBoundaries()
        {
            IAggregation noHistogram =
                Aggregations.Distribution.Create(OpenTelemetry.Stats.BucketBoundaries.Create(Enumerable.Empty<double>()));
            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, noHistogram, new List<string>() { Key });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 1.1)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
            IViewData viewData = viewManager.GetView(view.Name);
            var tv = TagValues.Create(new List<string>() { Value });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(noHistogram, MeasureDouble, 1.1) },
                },
                Epsilon);
        }

        [Fact]
        public void TestGetCumulativeViewDataWithoutBucketBoundaries()
        {
            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Mean, new List<string>() { Key });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 1.1)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
            IViewData viewData = viewManager.GetView(view.Name);
            var tv = TagValues.Create(new List<string>() { Value });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(Mean, MeasureDouble, 1.1) },
                },
                Epsilon);
        }

        [Fact]
        public void RegisterRecordAndGetView_StatsDisabled()
        {
            Stats.State = StatsCollectionState.DISABLED;
            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Mean, new List<string>() { Key });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 1.1)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
            Assert.Equal(StatsTestUtil.CreateEmptyViewData(view), viewManager.GetView(view.Name));
        }

        [Fact]
        public void RegisterRecordAndGetView_StatsReenabled()
        {
            Stats.State = StatsCollectionState.DISABLED;
            Stats.State = StatsCollectionState.ENABLED;
            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Mean, new List<string>() { Key });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 1.1)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
            TagValues tv = TagValues.Create(new List<string>() { Value });
            StatsTestUtil.AssertAggregationMapEquals(
                viewManager.GetView(view.Name).AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(Mean, MeasureDouble, 1.1) },
                },
                Epsilon);
        }

        [Fact]
        public void RegisterViewWithStatsDisabled_RecordAndGetViewWithStatsEnabled()
        {
            Stats.State = StatsCollectionState.DISABLED;
            IView view = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Mean, new List<string>() { Key });
            viewManager.RegisterView(view); // view will still be registered.

            Stats.State = StatsCollectionState.ENABLED;
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 1.1)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
            TagValues tv = TagValues.Create(new List<string>() { Value });
            StatsTestUtil.AssertAggregationMapEquals(
                viewManager.GetView(view.Name).AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(Mean, MeasureDouble, 1.1) },
                },
                Epsilon);
        }

        [Fact]
        public void RegisterDifferentViewWithSameNameWithStatsDisabled()
        {
            Stats.State = StatsCollectionState.DISABLED;
            IViewName viewName = CreateRandomViewName();
            IView view1 =
                View.Create(
                    viewName,
                    "View description.",
                    MeasureDouble,
                    Distribution,
                    new List<string>() { Key });
            IView view2 =
                View.Create(
                    viewName,
                    "This is a different description.",
                    MeasureDouble,
                    Distribution,
                    new List<string>() { Key });

            TestFailedToRegisterView(
                view1, view2, "A different view with the same name is already registered");
        }

        [Fact]
        public void SettingStateToDisabledWillClearStats_Cumulative()
        {
            IView cumulativeView = CreateCumulativeView(CreateRandomViewName(), MeasureDouble, Mean, new List<string>() { Key });
            SettingStateToDisabledWillClearStats(cumulativeView);
        }

        // [Fact]
        // public void SettingStateToDisabledWillClearStats_Interval()
        // {
        //    View intervalView =
        //        View.Create(
        //            VIEW_NAME_2,
        //            VIEW_DESCRIPTION,
        //            MeasureDouble,
        //            Mean,
        //            Arrays.asList(Key),
        //            Interval.Create(Duration.Create(60, 0)));
        //    settingStateToDisabledWillClearStats(intervalView);
        // }

        private void SettingStateToDisabledWillClearStats(IView view)
        {
            // TODO: deal with timestamp validation
            var timestamp1 = PreciseTimestamp.GetUtcNow().AddSeconds(-10);
            //clock.Time = timestamp1;
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MeasureDouble, 1.1)
                .Record(tagger.EmptyBuilder.Put(Key, Value).Build());
            TagValues tv = TagValues.Create(new List<string>() { Value });
            StatsTestUtil.AssertAggregationMapEquals(
                viewManager.GetView(view.Name).AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(view.Aggregation, view.Measure, 1.1) },
                },
                Epsilon);

            var timestamp2 = timestamp1.AddSeconds(2);
            //clock.Time = timestamp2;
            Stats.State = StatsCollectionState.DISABLED; // This will clear stats.
            Assert.Equal(StatsTestUtil.CreateEmptyViewData(view), viewManager.GetView(view.Name));

            var timestamp3 = timestamp1.AddSeconds(3);
            //clock.Time = timestamp3;
            Stats.State = StatsCollectionState.ENABLED;

            var timestamp4 = timestamp1.AddSeconds(4);
            //clock.Time = timestamp4;
            // This ViewData does not have any stats, but it should not be an empty ViewData, since it has
            // non-zero TimeStamps.
            IViewData viewData = viewManager.GetView(view.Name);
            Assert.Empty(viewData.AggregationMap);
            //Assert.Equal(timestamp3, viewData.Start);
            //Assert.Equal(timestamp4, viewData.End);
            // if (windowData instanceof CumulativeData) {
            //    Assert.Equal(windowData).isEqualTo(CumulativeData.Create(timestamp3, timestamp4));
            // } else {
            //    Assert.Equal(windowData).isEqualTo(IntervalData.Create(timestamp4));
            // }
        }

        private static IMeasureMap PutToMeasureMap(IMeasureMap measureMap, IMeasure measure, double value)
        {
            if (measure is MeasureDouble) {
                return measureMap.Put((IMeasureDouble)measure, value);
            } else if (measure is MeasureLong) {
                return measureMap.Put((IMeasureLong)measure, (long)Math.Round(value));
            } else {
                // Future measures.
                throw new Exception();
            }
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private static IViewName CreateRandomViewName()
        {
            return OpenTelemetry.Stats.ViewName.Create(RandomString(RandomNameLen));
        }

        private static string CreateRandomMeasureName()
        {
            return RandomString(RandomNameLen);
        }

        private static IMeasureDouble CreateRandomMeasureDouble()
        {
            return Measures.MeasureDouble.Create(CreateRandomMeasureName(), MeasureDescription, MeasureUnit);
        }

        private static IMeasureLong CreateRandomMeasureLong()
        {
            return Measures.MeasureLong.Create(CreateRandomMeasureName(), MeasureDescription, MeasureUnit);
        }
    }
}
