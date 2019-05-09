// <copyright file="ViewManagerTest.cs" company="OpenCensus Authors">
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
    using System.Linq;
    using OpenCensus.Common;
    using OpenCensus.Internal;
    using OpenCensus.Stats.Aggregations;
    using OpenCensus.Stats.Measures;
    using OpenCensus.Tags;
    using Xunit;

    public class ViewManagerTest
    {
        private static readonly ITagKey KEY = TagKey.Create("KEY");
        private static readonly ITagValue VALUE = TagValue.Create("VALUE");
        private static readonly ITagValue VALUE_2 = TagValue.Create("VALUE_2");
        private static readonly String MEASURE_NAME = "my measurement";
        private static readonly String MEASURE_NAME_2 = "my measurement 2";
        private static readonly String MEASURE_UNIT = "us";
        private static readonly String MEASURE_DESCRIPTION = "measure description";
        private static readonly IMeasureDouble MEASURE_DOUBLE = MeasureDouble.Create(MEASURE_NAME, MEASURE_DESCRIPTION, MEASURE_UNIT);
        private static readonly IMeasureLong MEASURE_LONG = MeasureLong.Create(MEASURE_NAME_2, MEASURE_DESCRIPTION, MEASURE_UNIT);

        private static readonly IViewName VIEW_NAME = ViewName.Create("my view");
        private static readonly IViewName VIEW_NAME_2 = ViewName.Create("my view 2");

        private static readonly String VIEW_DESCRIPTION = "view description";

        // private static readonly Cumulative CUMULATIVE = Cumulative.Create();

        private static readonly double EPSILON = 1e-7;
        private static readonly Duration TEN_SECONDS = Duration.Create(10, 0);
        // private static readonly Interval INTERVAL = Interval.Create(TEN_SECONDS);

        private static readonly IBucketBoundaries BUCKET_BOUNDARIES =
            BucketBoundaries.Create(
                new List<double>() {
              0.0, 0.2, 0.5, 1.0, 2.0, 3.0, 4.0, 5.0, 7.0, 10.0, 15.0, 20.0, 30.0, 40.0, 50.0,});

        private static readonly ISum SUM = Sum.Create();
        private static readonly IMean MEAN = Mean.Create();
        private static readonly IDistribution DISTRIBUTION = Distribution.Create(BUCKET_BOUNDARIES);
        private static readonly ILastValue LAST_VALUE = LastValue.Create();

        private readonly StatsComponent statsComponent;
        private readonly TagsComponent tagsComponent;

        private readonly ITagger tagger;
        private readonly IViewManager viewManager;
        private readonly IStatsRecorder statsRecorder;

        public ViewManagerTest()
        {
            statsComponent = new StatsComponent(new SimpleEventQueue());
            tagsComponent = new TagsComponent();

            tagger = tagsComponent.Tagger;
            viewManager = statsComponent.ViewManager;
            statsRecorder = statsComponent.StatsRecorder;
        }

        private static IView CreateCumulativeView()
        {
            return CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY });
        }

        private static IView CreateCumulativeView(
            IViewName name, IMeasure measure, IAggregation aggregation, List<ITagKey> keys)
        {
            return View.Create(name, VIEW_DESCRIPTION, measure, aggregation, keys);
        }

        [Fact]
        public void TestRegisterAndGetCumulativeView()
        {
            IView view = CreateCumulativeView();
            viewManager.RegisterView(view);
            Assert.Equal(view, viewManager.GetView(VIEW_NAME).View);
            Assert.Empty(viewManager.GetView(VIEW_NAME).AggregationMap);
            // Assert.Equal(viewManager.GetView(VIEW_NAME).getWindowData()).isInstanceOf(CumulativeData);
        }

        [Fact]
        public void TestGetAllExportedViews()
        {
            Assert.Empty(viewManager.AllExportedViews);
            IView cumulativeView1 =
                CreateCumulativeView(
                    ViewName.Create("View 1"), MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY });
            IView cumulativeView2 =
                CreateCumulativeView(
                    ViewName.Create("View 2"), MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY });
            // View intervalView =
            //    View.Create(
            //        View.Name.Create("View 3"),
            //        VIEW_DESCRIPTION,
            //        MEASURE_DOUBLE,
            //        DISTRIBUTION,
            //        Arrays.asList(KEY),
            //        INTERVAL);
            viewManager.RegisterView(cumulativeView1);
            viewManager.RegisterView(cumulativeView2);

            // Only cumulative views should be exported.
            Assert.Contains(cumulativeView1, viewManager.AllExportedViews);
            Assert.Contains(cumulativeView2, viewManager.AllExportedViews);
            Assert.Equal(2, viewManager.AllExportedViews.Count);
        }

        [Fact]
        public void GetAllExportedViewsResultIsUnmodifiable()
        {
            IView view1 =
                View.Create(
                    ViewName.Create("View 1"),
                    VIEW_DESCRIPTION,
                    MEASURE_DOUBLE,
                    DISTRIBUTION,
                    new List<ITagKey>() { KEY });
            viewManager.RegisterView(view1);
            ISet<IView> exported = viewManager.AllExportedViews;

            IView view2 =
                View.Create(
                    ViewName.Create("View 2"),
                    VIEW_DESCRIPTION,
                    MEASURE_DOUBLE,
                    DISTRIBUTION,
                    new List<ITagKey>() { KEY });
            Assert.Throws<NotSupportedException>(() => exported.Add(view2));

        }

        // [Fact]
        //  public void TestRegisterAndGetIntervalView()
        // {
        //    View intervalView =
        //        View.Create(
        //            VIEW_NAME,
        //            VIEW_DESCRIPTION,
        //            MEASURE_DOUBLE,
        //            DISTRIBUTION,
        //            Arrays.asList(KEY),
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
            Assert.Equal(view, viewManager.GetView(VIEW_NAME).View);
        }

        [Fact]
        public void PreventRegisteringDifferentViewWithSameName()
        {
            IView view1 =
                View.Create(
                    VIEW_NAME,
                    "View description.",
                    MEASURE_DOUBLE,
                    DISTRIBUTION,
                    new List<ITagKey>() { KEY });
            IView view2 =
                View.Create(
                    VIEW_NAME,
                    "This is a different description.",
                    MEASURE_DOUBLE,
                    DISTRIBUTION,
                    new List<ITagKey>() { KEY });
            TestFailedToRegisterView(view1, view2, "A different view with the same name is already registered");
        }

        [Fact]
        public void PreventRegisteringDifferentMeasureWithSameName()
        {
            IMeasureDouble measure1 = MeasureDouble.Create("measure", "description", "1");
            IMeasureLong measure2 = MeasureLong.Create("measure", "description", "1");
            IView view1 =
                View.Create(
                    VIEW_NAME, VIEW_DESCRIPTION, measure1, DISTRIBUTION, new List<ITagKey>() { KEY });
            IView view2 =
        View.Create(
            VIEW_NAME_2, VIEW_DESCRIPTION, measure2, DISTRIBUTION, new List<ITagKey>() { KEY });
            TestFailedToRegisterView(view1, view2, "A different measure with the same name is already registered");
        }

        private void TestFailedToRegisterView(IView view1, IView view2, String message)
        {
            viewManager.RegisterView(view1);
            try
            {
                Assert.Throws<ArgumentException>(() => viewManager.RegisterView(view2));
            } finally {
                Assert.Equal(view1, viewManager.GetView(VIEW_NAME).View);
            }
        }

        [Fact]
        public void ReturnNullWhenGettingNonexistentViewData()
        {
            Assert.Null(viewManager.GetView(VIEW_NAME));
        }

        [Fact]
        public void TestRecordDouble_Distribution_Cumulative()
        {
            TestRecordCumulative(MEASURE_DOUBLE, DISTRIBUTION, 10.0, 20.0, 30.0, 40.0);
        }

        [Fact]
        public void TestRecordLong_Distribution_Cumulative()
        {
            TestRecordCumulative(MEASURE_LONG, DISTRIBUTION, 1000, 2000, 3000, 4000);
        }

        [Fact]
        public void TestRecordDouble_Sum_Cumulative()
        {
            TestRecordCumulative(MEASURE_DOUBLE, SUM, 11.1, 22.2, 33.3, 44.4);
        }

        [Fact]
        public void TestRecordLong_Sum_Cumulative()
        {
            TestRecordCumulative(MEASURE_LONG, SUM, 1000, 2000, 3000, 4000);
        }

        [Fact]
        public void TestRecordDouble_Lastvalue_Cumulative()
        {
            TestRecordCumulative(MEASURE_DOUBLE, LAST_VALUE, 11.1, 22.2, 33.3, 44.4);
        }

        [Fact]
        public void TestRecordLong_Lastvalue_Cumulative()
        {
            TestRecordCumulative(MEASURE_LONG, LAST_VALUE, 1000, 2000, 3000, 4000);
        }

        private void TestRecordCumulative(IMeasure measure, IAggregation aggregation, params double[] values)
        {
            IView view = CreateCumulativeView(VIEW_NAME, measure, aggregation, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view);
            ITagContext tags = tagger.EmptyBuilder.Put(KEY, VALUE).Build();
            foreach (double val in values)
            {
                PutToMeasureMap(statsRecorder.NewMeasureMap(), measure, val).Record(tags);
            }
            IViewData viewData = viewManager.GetView(VIEW_NAME);
            Assert.Equal(view, viewData.View);

            var tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv,  StatsTestUtil.CreateAggregationData(aggregation, measure, values) },
                },
                EPSILON);
        }


        [Fact]
        public void GetViewDoesNotClearStats()
        {
            IView view = CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view);
            ITagContext tags = tagger.EmptyBuilder.Put(KEY, VALUE).Build();
            statsRecorder.NewMeasureMap().Put(MEASURE_DOUBLE, 0.1).Record(tags);
            IViewData viewData1 = viewManager.GetView(VIEW_NAME);
            var tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData1.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv,  StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 0.1) },
                },
                EPSILON);

            statsRecorder.NewMeasureMap().Put(MEASURE_DOUBLE, 0.2).Record(tags);
            IViewData viewData2 = viewManager.GetView(VIEW_NAME);

            // The second view should have the same start time as the first view, and it should include both
            // Recorded values:

            StatsTestUtil.AssertAggregationMapEquals(
                viewData2.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv,  StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 0.1, 0.2) },
                },
                EPSILON);
        }

        [Fact]
        public void TestRecordCumulativeMultipleTagValues()
        {
            viewManager.RegisterView(
                CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY }));
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 10.0)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 30.0)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE_2).Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 50.0)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE_2).Build());
            IViewData viewData = viewManager.GetView(VIEW_NAME);
            var tv = TagValues.Create(new List<ITagValue>() { VALUE });
            var tv2 = TagValues.Create(new List<ITagValue>() { VALUE_2 });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                { tv, StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 10.0)},
                { tv2, StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 30.0, 50.0)},
                },
                EPSILON);
        }


        // This test checks that MeasureMaper.Record(...) does not throw an exception when no views are
        // registered.
        [Fact]
        public void AllowRecordingWithoutRegisteringMatchingViewData()
        {
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 10)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
        }

        [Fact]
        public void TestRecordWithEmptyStatsContext()
        {
            viewManager.RegisterView(
                CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY }));
            // DEFAULT doesn't have tags, but the view has tag key "KEY".
            statsRecorder.NewMeasureMap().Put(MEASURE_DOUBLE, 10.0).Record(tagger.Empty);
            IViewData viewData = viewManager.GetView(VIEW_NAME);
            var tv = TagValues.Create(new List<ITagValue>() { MutableViewData.UnknownTagValue });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    // Tag is missing for associated measureValues, should use default tag value
                    // "unknown/not set".
                    { tv,
                    // Should Record stats with default tag value: "KEY" : "unknown/not set".
                    StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 10.0)
                    },
                },
                EPSILON);

        }

        [Fact]
        public void TestRecord_MeasureNameNotMatch()
        {
            TestRecord_MeasureNotMatch(
                MeasureDouble.Create(MEASURE_NAME, "measure", MEASURE_UNIT),
                MeasureDouble.Create(MEASURE_NAME_2, "measure", MEASURE_UNIT),
                10.0);
        }

        [Fact]
        public void TestRecord_MeasureTypeNotMatch()
        {
            TestRecord_MeasureNotMatch(
                MeasureLong.Create(MEASURE_NAME, "measure", MEASURE_UNIT),
                MeasureDouble.Create(MEASURE_NAME, "measure", MEASURE_UNIT),
                10.0);
        }

        private void TestRecord_MeasureNotMatch(IMeasure measure1, IMeasure measure2, double value)
        {
            viewManager.RegisterView(CreateCumulativeView(VIEW_NAME, measure1, MEAN, new List<ITagKey>() { KEY }));
            ITagContext tags = tagger.EmptyBuilder.Put(KEY, VALUE).Build();
            PutToMeasureMap(statsRecorder.NewMeasureMap(), measure2, value).Record(tags);
            IViewData view = viewManager.GetView(VIEW_NAME);
            Assert.Empty(view.AggregationMap);
        }

        [Fact]
        public void TestRecordWithTagsThatDoNotMatchViewData()
        {
            viewManager.RegisterView(
                CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY }));
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 10.0)
                .Record(tagger.EmptyBuilder.Put(TagKey.Create("wrong key"), VALUE).Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 50.0)
                .Record(tagger.EmptyBuilder.Put(TagKey.Create("another wrong key"), VALUE).Build());
            IViewData viewData = viewManager.GetView(VIEW_NAME);
            var tv = TagValues.Create(new List<ITagValue>() { MutableViewData.UnknownTagValue });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                { 
                // Won't Record the unregistered tag key, for missing registered keys will use default
                // tag value : "unknown/not set".
                { tv,
                    // Should Record stats with default tag value: "KEY" : "unknown/not set".
                    StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 10.0, 50.0) },
                },
                EPSILON);
        }

        [Fact]
        public void TestViewDataWithMultipleTagKeys()
        {
            ITagKey key1 = TagKey.Create("Key-1");
            ITagKey key2 = TagKey.Create("Key-2");
            viewManager.RegisterView(
                CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { key1, key2 }));
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.1)
                .Record(
                    tagger
                        .EmptyBuilder
                        .Put(key1, TagValue.Create("v1"))
                        .Put(key2, TagValue.Create("v10"))
                        .Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 2.2)
                .Record(
                    tagger
                        .EmptyBuilder
                        .Put(key1, TagValue.Create("v1"))
                        .Put(key2, TagValue.Create("v20"))
                        .Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 3.3)
                .Record(
                    tagger
                        .EmptyBuilder
                        .Put(key1, TagValue.Create("v2"))
                        .Put(key2, TagValue.Create("v10"))
                        .Build());
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 4.4)
                .Record(
                    tagger
                        .EmptyBuilder
                        .Put(key1, TagValue.Create("v1"))
                        .Put(key2, TagValue.Create("v10"))
                        .Build());
            IViewData viewData = viewManager.GetView(VIEW_NAME);
            var tv1 = TagValues.Create(new List<ITagValue>() { TagValue.Create("v1"), TagValue.Create("v10") });
            var tv2 = TagValues.Create(new List<ITagValue>() { TagValue.Create("v1"), TagValue.Create("v20") });
            var tv3 = TagValues.Create(new List<ITagValue>() { TagValue.Create("v2"), TagValue.Create("v10") });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv1,  StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 1.1, 4.4) },
                    { tv2,  StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 2.2) },
                    { tv3,  StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 3.3)},
                 },
                EPSILON);
        }

        [Fact]
        public void TestMultipleViewSameMeasure()
        {
            IView view1 =
                CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY });
            IView view2 =
                CreateCumulativeView(VIEW_NAME_2, MEASURE_DOUBLE, DISTRIBUTION, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view1);
            viewManager.RegisterView(view2);
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 5.0)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
            IViewData viewData1 = viewManager.GetView(VIEW_NAME);
            IViewData viewData2 = viewManager.GetView(VIEW_NAME_2);
            var tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData1.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 5.0) },
                },
                EPSILON);

            StatsTestUtil.AssertAggregationMapEquals(
                viewData2.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(DISTRIBUTION, MEASURE_DOUBLE, 5.0) },
                },
                EPSILON);
        }

        [Fact]
        public void TestMultipleViews_DifferentMeasureNames()
        {
            TestMultipleViews_DifferentMeasures(
                MeasureDouble.Create(MEASURE_NAME, MEASURE_DESCRIPTION, MEASURE_UNIT),
                MeasureDouble.Create(MEASURE_NAME_2, MEASURE_DESCRIPTION, MEASURE_UNIT),
                1.1,
                2.2);
        }

        [Fact]
        public void TestMultipleViews_DifferentMeasureTypes()
        {
            TestMultipleViews_DifferentMeasures(
                MeasureDouble.Create(MEASURE_NAME, MEASURE_DESCRIPTION, MEASURE_UNIT),
                MeasureLong.Create(MEASURE_NAME_2, MEASURE_DESCRIPTION, MEASURE_UNIT),
                1.1,
                5000);
        }

        private void TestMultipleViews_DifferentMeasures(IMeasure measure1, IMeasure measure2, double value1, double value2)
        {
            IView view1 = CreateCumulativeView(VIEW_NAME, measure1, DISTRIBUTION, new List<ITagKey>() { KEY });
            IView view2 =
                CreateCumulativeView(VIEW_NAME_2, measure2, DISTRIBUTION, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view1);
            viewManager.RegisterView(view2);
            ITagContext tags = tagger.EmptyBuilder.Put(KEY, VALUE).Build();
            IMeasureMap measureMap = statsRecorder.NewMeasureMap();
            PutToMeasureMap(measureMap, measure1, value1);
            PutToMeasureMap(measureMap, measure2, value2);
            measureMap.Record(tags);
            IViewData viewData1 = viewManager.GetView(VIEW_NAME);
            IViewData viewData2 = viewManager.GetView(VIEW_NAME_2);
            var tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData1.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(DISTRIBUTION, measure1, value1) },
                 },
                EPSILON);

            StatsTestUtil.AssertAggregationMapEquals(
                viewData2.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(DISTRIBUTION, measure2, value2) },
                },
                EPSILON);
        }

        [Fact]
        public void TestGetCumulativeViewDataWithEmptyBucketBoundaries()
        {
            IAggregation noHistogram =
                Distribution.Create(BucketBoundaries.Create(Enumerable.Empty<double>()));
            IView view = CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, noHistogram, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.1)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
            IViewData viewData = viewManager.GetView(VIEW_NAME);
            var tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(noHistogram, MEASURE_DOUBLE, 1.1) },
                },
                EPSILON);
        }

        [Fact]
        public void TestGetCumulativeViewDataWithoutBucketBoundaries()
        {
            IView view = CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, MEAN, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.1)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
            IViewData viewData = viewManager.GetView(VIEW_NAME);
            var tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    {tv, StatsTestUtil.CreateAggregationData(MEAN, MEASURE_DOUBLE, 1.1) },
                },
                EPSILON);
        }

        [Fact]
        public void RegisterRecordAndGetView_StatsDisabled()
        {
            statsComponent.State = StatsCollectionState.DISABLED;
            IView view = CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, MEAN, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.1)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
            Assert.Equal(StatsTestUtil.CreateEmptyViewData(view), viewManager.GetView(VIEW_NAME));
        }

        [Fact]
        public void RegisterRecordAndGetView_StatsReenabled()
        {
            statsComponent.State = StatsCollectionState.DISABLED;
            statsComponent.State = StatsCollectionState.ENABLED;
            IView view = CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, MEAN, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.1)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
            TagValues tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewManager.GetView(VIEW_NAME).AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(MEAN, MEASURE_DOUBLE, 1.1) },
                },
                EPSILON);
        }

        [Fact]
        public void RegisterViewWithStatsDisabled_RecordAndGetViewWithStatsEnabled()
        {
            statsComponent.State = StatsCollectionState.DISABLED;
            IView view = CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, MEAN, new List<ITagKey>() { KEY });
            viewManager.RegisterView(view); // view will still be registered.

            statsComponent.State = StatsCollectionState.ENABLED;
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.1)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
            TagValues tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewManager.GetView(VIEW_NAME).AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(MEAN, MEASURE_DOUBLE, 1.1) },
                },
                EPSILON);
        }

        [Fact]
        public void RegisterDifferentViewWithSameNameWithStatsDisabled()
        {
            statsComponent.State = StatsCollectionState.DISABLED;
            IView view1 =
                View.Create(
                    VIEW_NAME,
                    "View description.",
                    MEASURE_DOUBLE,
                    DISTRIBUTION,
                    new List<ITagKey>() { KEY });
            IView view2 =
                View.Create(
                    VIEW_NAME,
                    "This is a different description.",
                    MEASURE_DOUBLE,
                    DISTRIBUTION,
                    new List<ITagKey>() { KEY });

            TestFailedToRegisterView(
                view1, view2, "A different view with the same name is already registered");
        }

        [Fact]
        public void SettingStateToDisabledWillClearStats_Cumulative()
        {
            IView cumulativeView = CreateCumulativeView(VIEW_NAME, MEASURE_DOUBLE, MEAN, new List<ITagKey>() { KEY });
            SettingStateToDisabledWillClearStats(cumulativeView);
        }

        // [Fact]
        // public void SettingStateToDisabledWillClearStats_Interval()
        // {
        //    View intervalView =
        //        View.Create(
        //            VIEW_NAME_2,
        //            VIEW_DESCRIPTION,
        //            MEASURE_DOUBLE,
        //            MEAN,
        //            Arrays.asList(KEY),
        //            Interval.Create(Duration.Create(60, 0)));
        //    settingStateToDisabledWillClearStats(intervalView);
        // }

        private void SettingStateToDisabledWillClearStats(IView view)
        {
            // TODO: deal with timestamp validation
            Timestamp timestamp1 = Timestamp.Create(1, 0);
            //clock.Time = timestamp1;
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.1)
                .Record(tagger.EmptyBuilder.Put(KEY, VALUE).Build());
            TagValues tv = TagValues.Create(new List<ITagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewManager.GetView(view.Name).AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(view.Aggregation, view.Measure, 1.1) },
                },
                EPSILON);

            Timestamp timestamp2 = Timestamp.Create(2, 0);
            //clock.Time = timestamp2;
            statsComponent.State = StatsCollectionState.DISABLED; // This will clear stats.
            Assert.Equal(StatsTestUtil.CreateEmptyViewData(view), viewManager.GetView(view.Name));

            Timestamp timestamp3 = Timestamp.Create(3, 0);
            //clock.Time = timestamp3;
            statsComponent.State = StatsCollectionState.ENABLED;

            Timestamp timestamp4 = Timestamp.Create(4, 0);
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
    }

}
