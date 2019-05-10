// <copyright file="NoopViewManagerTest.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Common;
    using OpenTelemetry.Stats.Aggregations;
    using OpenTelemetry.Stats.Measures;
    using OpenTelemetry.Tags;
    using Xunit;

    public class NoopViewManagerTest
    {
        private static readonly IMeasureDouble MEASURE = MeasureDouble.Create("my measure", "description", "s");
        private static readonly ITagKey KEY = TagKey.Create("KEY");
        private static readonly IViewName VIEW_NAME = ViewName.Create("my view");
        private static readonly String VIEW_DESCRIPTION = "view description";
        private static readonly ISum AGGREGATION = Sum.Create();
        // private static readonly Cumulative CUMULATIVE = Cumulative.create();
        private static readonly Duration TEN_SECONDS = Duration.Create(10, 0);
        // private static readonly Interval INTERVAL = Interval.create(TEN_SECONDS);

        // @Rule public readonly ExpectedException thrown = ExpectedException.none();

        [Fact]
        public void NoopViewManager_RegisterView_DisallowRegisteringDifferentViewWithSameName()
        {
            IView view1 =
                View.Create(
                    VIEW_NAME, "description 1", MEASURE, AGGREGATION, new List<ITagKey> { KEY });
            IView view2 =
                View.Create(
                    VIEW_NAME, "description 2", MEASURE, AGGREGATION, new List<ITagKey> { KEY });
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            viewManager.RegisterView(view1);

            try
            {
                Assert.Throws<ArgumentException>(() => viewManager.RegisterView(view2));
            }
            finally
            {
                Assert.Equal(view1, viewManager.GetView(VIEW_NAME).View);
            }
        }

        [Fact]
        public void NoopViewManager_RegisterView_AllowRegisteringSameViewTwice()
        {
            IView view =
                View.Create(
                    VIEW_NAME, VIEW_DESCRIPTION, MEASURE, AGGREGATION, new List<ITagKey> { KEY });
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            viewManager.RegisterView(view);
            viewManager.RegisterView(view);
        }

        [Fact]
        public void NoopViewManager_RegisterView_DisallowNull()
        {
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            Assert.Throws<ArgumentNullException>(() => viewManager.RegisterView(null));
        }

        [Fact]
        public void NoopViewManager_GetView_GettingNonExistentViewReturnsNull()
        {
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            Assert.Null(viewManager.GetView(VIEW_NAME));
        }

        [Fact]
        public void NoopViewManager_GetView_Cumulative()
        {
            IView view =
                View.Create(
                    VIEW_NAME, VIEW_DESCRIPTION, MEASURE, AGGREGATION, new List<ITagKey> { KEY });
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            viewManager.RegisterView(view);

            IViewData viewData = viewManager.GetView(VIEW_NAME);
            Assert.Equal(view, viewData.View);
            Assert.Empty(viewData.AggregationMap);
            Assert.Equal(DateTimeOffset.MinValue, viewData.Start);
            Assert.Equal(DateTimeOffset.MinValue, viewData.End);

        }

        [Fact]
        public void noopViewManager_GetView_Interval()
        {
            IView view =
                View.Create(
                    VIEW_NAME, VIEW_DESCRIPTION, MEASURE, AGGREGATION, new List<ITagKey> { KEY });
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            viewManager.RegisterView(view);

            IViewData viewData = viewManager.GetView(VIEW_NAME);
            Assert.Equal(view, viewData.View);
            Assert.Empty(viewData.AggregationMap);
            Assert.Equal(DateTimeOffset.MinValue, viewData.Start);
            Assert.Equal(DateTimeOffset.MinValue, viewData.End);

        }

        [Fact]
        public void NoopViewManager_GetView_DisallowNull()
        {
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            Assert.Throws<ArgumentNullException>(() => viewManager.GetView(null));
        }

        [Fact]
        public void GetAllExportedViews()
        {
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            Assert.Empty(viewManager.AllExportedViews);
            IView cumulativeView1 =
                View.Create(
                    ViewName.Create("View 1"),
                    VIEW_DESCRIPTION,
                    MEASURE,
                    AGGREGATION,
                    new List<ITagKey> { KEY });
            IView cumulativeView2 =
                View.Create(
                    ViewName.Create("View 2"),
                    VIEW_DESCRIPTION,
                    MEASURE,
                    AGGREGATION,
                    new List<ITagKey> { KEY });


            viewManager.RegisterView(cumulativeView1);
            viewManager.RegisterView(cumulativeView2);

            // Only cumulative views should be exported.
            Assert.Equal(2, viewManager.AllExportedViews.Count);
            Assert.Contains(cumulativeView1, viewManager.AllExportedViews);
            Assert.Contains(cumulativeView2, viewManager.AllExportedViews);
        }

        [Fact]
        public void GetAllExportedViews_ResultIsUnmodifiable()
        {
            IViewManager viewManager = NoopStats.NewNoopViewManager();
            IView view1 =
                View.Create(
                    ViewName.Create("View 1"), VIEW_DESCRIPTION, MEASURE, AGGREGATION, new List<ITagKey> { KEY });
            viewManager.RegisterView(view1);
            ISet<IView> exported = viewManager.AllExportedViews;

            IView view2 =
                View.Create(
                    ViewName.Create("View 2"), VIEW_DESCRIPTION, MEASURE, AGGREGATION, new List<ITagKey> { KEY });
            Assert.Throws<NotSupportedException>(() => exported.Add(view2));
        }
    }
}
