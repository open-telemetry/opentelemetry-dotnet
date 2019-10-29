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
using System;
using System.Collections.Generic;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class NoopViewManagerTest
    {
        private static readonly IMeasureDouble Measure = MeasureDouble.Create("my measure", "description", "s");
        private static readonly string Key = "KEY";
        private static readonly IViewName ViewName = OpenTelemetry.Stats.ViewName.Create("my view");
        private static readonly String ViewDescription = "view description";
        private static readonly ISum Aggregation = Sum.Create();
        // private static readonly Cumulative CUMULATIVE = Cumulative.create();
        private static readonly TimeSpan TenSeconds = TimeSpan.FromSeconds(10);
        // private static readonly Interval INTERVAL = Interval.create(TEN_SECONDS);

        // @Rule public readonly ExpectedException thrown = ExpectedException.none();

        [Fact]
        public void NoopViewManager_RegisterView_DisallowRegisteringDifferentViewWithSameName()
        {
            var view1 =
                View.Create(
                    ViewName, "description 1", Measure, Aggregation, new List<string> { Key });
            var view2 =
                View.Create(
                    ViewName, "description 2", Measure, Aggregation, new List<string> { Key });
            var viewManager = NoopStats.NewNoopViewManager();
            viewManager.RegisterView(view1);

            try
            {
                Assert.Throws<ArgumentException>(() => viewManager.RegisterView(view2));
            }
            finally
            {
                Assert.Equal(view1, viewManager.GetView(ViewName).View);
            }
        }

        [Fact]
        public void NoopViewManager_RegisterView_AllowRegisteringSameViewTwice()
        {
            var view =
                View.Create(
                    ViewName, ViewDescription, Measure, Aggregation, new List<string> { Key });
            var viewManager = NoopStats.NewNoopViewManager();
            viewManager.RegisterView(view);
            viewManager.RegisterView(view);
        }

        [Fact]
        public void NoopViewManager_RegisterView_DisallowNull()
        {
            var viewManager = NoopStats.NewNoopViewManager();
            Assert.Throws<ArgumentNullException>(() => viewManager.RegisterView(null));
        }

        [Fact]
        public void NoopViewManager_GetView_GettingNonExistentViewReturnsNull()
        {
            var viewManager = NoopStats.NewNoopViewManager();
            Assert.Null(viewManager.GetView(ViewName));
        }

        [Fact]
        public void NoopViewManager_GetView_Cumulative()
        {
            var view =
                View.Create(
                    ViewName, ViewDescription, Measure, Aggregation, new List<string> { Key });
            var viewManager = NoopStats.NewNoopViewManager();
            viewManager.RegisterView(view);

            var viewData = viewManager.GetView(ViewName);
            Assert.Equal(view, viewData.View);
            Assert.Empty(viewData.AggregationMap);
            Assert.Equal(DateTimeOffset.MinValue, viewData.Start);
            Assert.Equal(DateTimeOffset.MinValue, viewData.End);

        }

        [Fact]
        public void noopViewManager_GetView_Interval()
        {
            var view =
                View.Create(
                    ViewName, ViewDescription, Measure, Aggregation, new List<string> { Key });
            var viewManager = NoopStats.NewNoopViewManager();
            viewManager.RegisterView(view);

            var viewData = viewManager.GetView(ViewName);
            Assert.Equal(view, viewData.View);
            Assert.Empty(viewData.AggregationMap);
            Assert.Equal(DateTimeOffset.MinValue, viewData.Start);
            Assert.Equal(DateTimeOffset.MinValue, viewData.End);

        }

        [Fact]
        public void NoopViewManager_GetView_DisallowNull()
        {
            var viewManager = NoopStats.NewNoopViewManager();
            Assert.Throws<ArgumentNullException>(() => viewManager.GetView(null));
        }

        [Fact]
        public void GetAllExportedViews()
        {
            var viewManager = NoopStats.NewNoopViewManager();
            Assert.Empty(viewManager.AllExportedViews);
            var cumulativeView1 =
                View.Create(
                    OpenTelemetry.Stats.ViewName.Create("View 1"),
                    ViewDescription,
                    Measure,
                    Aggregation,
                    new List<string> { Key });
            var cumulativeView2 =
                View.Create(
                    OpenTelemetry.Stats.ViewName.Create("View 2"),
                    ViewDescription,
                    Measure,
                    Aggregation,
                    new List<string> { Key });


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
            var viewManager = NoopStats.NewNoopViewManager();
            var view1 =
                View.Create(
                    OpenTelemetry.Stats.ViewName.Create("View 1"), ViewDescription, Measure, Aggregation, new List<string> { Key });
            viewManager.RegisterView(view1);
            var exported = viewManager.AllExportedViews;

            var view2 =
                View.Create(
                    OpenTelemetry.Stats.ViewName.Create("View 2"), ViewDescription, Measure, Aggregation, new List<string> { Key });
            Assert.Throws<NotSupportedException>(() => exported.Add(view2));
        }
    }
}
