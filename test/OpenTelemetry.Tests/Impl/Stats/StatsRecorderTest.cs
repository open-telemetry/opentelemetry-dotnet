﻿// <copyright file="StatsRecorderTest.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Collections.Generic;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using OpenTelemetry.Tags.Unsafe;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class StatsRecorderTest
    {
        private static readonly TagKey KEY = TagKey.Create("KEY");
        private static readonly TagValue VALUE = TagValue.Create("VALUE");
        private static readonly TagValue VALUE_2 = TagValue.Create("VALUE_2");
        private static readonly IMeasureDouble MEASURE_DOUBLE = MeasureDouble.Create("my measurement", "description", "us");
        private static readonly IMeasureDouble MEASURE_DOUBLE_NO_VIEW_1 = MeasureDouble.Create("my measurement no view 1", "description", "us");
        private static readonly IMeasureDouble MEASURE_DOUBLE_NO_VIEW_2 = MeasureDouble.Create("my measurement no view 2", "description", "us");
        private static readonly IViewName VIEW_NAME = ViewName.Create("my view");
        private static readonly int RANDOM_NAME_LEN = 8;

        private static readonly IViewManager viewManager = Stats.ViewManager;
        private static readonly IStatsRecorder statsRecorder = Stats.StatsRecorder;

        public StatsRecorderTest()
        {
        }

        [Fact]
        public void Record_CurrentContextNotSet()
        {
            Stats.State = StatsCollectionState.ENABLED;

            IViewName viewName = CreateRandomViewName();

            IView view =
                View.Create(
                    viewName,
                    "description",
                    MEASURE_DOUBLE,
                    Sum.Create(),
                    new List<TagKey>() { KEY });
            viewManager.RegisterView(view);
            statsRecorder.NewMeasureMap().Put(MEASURE_DOUBLE, 1.0).Record();
            IViewData viewData = viewManager.GetView(viewName);

            // record() should have used the default TagContext, so the tag value should be null.
            ICollection<TagValues> expected = new List<TagValues>() { TagValues.Create(new List<TagValue>() { null }) };
            ICollection<TagValues> actual = viewData.AggregationMap.Keys.ToList();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Record_CurrentContextSet()
        {
            Stats.State = StatsCollectionState.ENABLED;

            IViewName viewName = CreateRandomViewName();

            IView view =
                View.Create(
                    viewName,
                    "description",
                    MEASURE_DOUBLE,
                    Sum.Create(),
                    new List<TagKey>() { KEY });
            viewManager.RegisterView(view);
            var orig = AsyncLocalContext.CurrentTagContext;
            AsyncLocalContext.CurrentTagContext = new SimpleTagContext(Tag.Create(KEY, VALUE));
 
            try
            {
                statsRecorder.NewMeasureMap().Put(MEASURE_DOUBLE, 1.0).Record();
            }
            finally
            {
                AsyncLocalContext.CurrentTagContext = orig;
            }
            IViewData viewData = viewManager.GetView(viewName);

            // record() should have used the given TagContext.
            ICollection<TagValues> expected = new List<TagValues>() { TagValues.Create(new List<TagValue>() { VALUE }) };
            ICollection<TagValues> actual = viewData.AggregationMap.Keys.ToList();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Record_UnregisteredMeasure()
        {
            Stats.State = StatsCollectionState.ENABLED;

            IViewName viewName = CreateRandomViewName();

            IView view =
                View.Create(
                    viewName,
                    "description",
                    MEASURE_DOUBLE,
                    Sum.Create(),
                    new List<TagKey>() { KEY });
            viewManager.RegisterView(view);
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE_NO_VIEW_1, 1.0)
                .Put(MEASURE_DOUBLE, 2.0)
                .Put(MEASURE_DOUBLE_NO_VIEW_2, 3.0)
                .Record(new SimpleTagContext(Tag.Create(KEY, VALUE)));

            IViewData viewData = viewManager.GetView(viewName);

            // There should be one entry.
            var tv = TagValues.Create(new List<TagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>() {{ tv, StatsTestUtil.CreateAggregationData(Sum.Create(), MEASURE_DOUBLE, 2.0) }},
                1e-6);
        }

        [Fact]
        public void RecordTwice()
        {
            Stats.State = StatsCollectionState.ENABLED;

            IViewName viewName = CreateRandomViewName();

            IView view =
                View.Create(
                    viewName,
                    "description",
                    MEASURE_DOUBLE,
                    Sum.Create(),
                    new List<TagKey>() { KEY });

            viewManager.RegisterView(view);
            IMeasureMap statsRecord = statsRecorder.NewMeasureMap().Put(MEASURE_DOUBLE, 1.0);
            statsRecord.Record(new SimpleTagContext(Tag.Create(KEY, VALUE)));
            statsRecord.Record(new SimpleTagContext(Tag.Create(KEY, VALUE_2)));
            IViewData viewData = viewManager.GetView(viewName);

            // There should be two entries.
            var tv = TagValues.Create(new List<TagValue>() { VALUE });
            var tv2 = TagValues.Create(new List<TagValue>() { VALUE_2 });

            StatsTestUtil.AssertAggregationMapEquals(
                viewData.AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv, StatsTestUtil.CreateAggregationData(Sum.Create(), MEASURE_DOUBLE, 1.0) },
                    { tv2, StatsTestUtil.CreateAggregationData(Sum.Create(), MEASURE_DOUBLE, 1.0) },
                },
                1e-6);
        }

        [Fact]
        public void Record_StatsDisabled()
        {
            IView view =
                View.Create(
                    VIEW_NAME,
                    "description",
                    MEASURE_DOUBLE,
                    Sum.Create(),
                    new List<TagKey>() { KEY });

            viewManager.RegisterView(view);
            Stats.State = StatsCollectionState.DISABLED;
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.0)
                .Record(new SimpleTagContext(Tag.Create(KEY, VALUE)));
            Assert.Equal(CreateEmptyViewData(view), viewManager.GetView(VIEW_NAME));
        }

        [Fact]
        public void Record_StatsReenabled()
        {
            IView view =
                View.Create(
                    VIEW_NAME,
                    "description",
                    MEASURE_DOUBLE,
                    Sum.Create(),
                    new List<TagKey>() { KEY });

            viewManager.RegisterView(view);

            Stats.State = StatsCollectionState.DISABLED;
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 1.0)
                .Record(new SimpleTagContext(Tag.Create(KEY, VALUE)));
            Assert.Equal(CreateEmptyViewData(view), viewManager.GetView(VIEW_NAME));

            Stats.State = StatsCollectionState.ENABLED;
            Assert.Empty(viewManager.GetView(VIEW_NAME).AggregationMap);
            // assertThat(viewManager.getView(VIEW_NAME).getWindowData())
            //    .isNotEqualTo(CumulativeData.Create(ZERO_TIMESTAMP, ZERO_TIMESTAMP));
            statsRecorder
                .NewMeasureMap()
                .Put(MEASURE_DOUBLE, 4.0)
                .Record(new SimpleTagContext(Tag.Create(KEY, VALUE)));
            TagValues tv = TagValues.Create(new List<TagValue>() { VALUE });
            StatsTestUtil.AssertAggregationMapEquals(
                viewManager.GetView(VIEW_NAME).AggregationMap,
                new Dictionary<TagValues, IAggregationData>()
                {
                    { tv,  StatsTestUtil.CreateAggregationData(Sum.Create(), MEASURE_DOUBLE, 4.0) },
                },
                1e-6);
        }

        // Create an empty ViewData with the given View.
        static IViewData CreateEmptyViewData(IView view)
        {
            return ViewData.Create(
                view,
                new Dictionary<TagValues, IAggregationData>(),
                DateTimeOffset.MinValue, DateTimeOffset.MinValue);
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
            return ViewName.Create(RandomString(RANDOM_NAME_LEN));
        }

        class SimpleTagContext : TagContextBase
        {
            private readonly IEnumerable<Tag> tags;

            public SimpleTagContext(params Tag[] tags)
            {
                this.tags = new List<Tag>(tags);
            }

            public override IEnumerator<Tag> GetEnumerator()
            {
                return tags.GetEnumerator();
            }
        }
    }
}
