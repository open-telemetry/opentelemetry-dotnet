// <copyright file="MeasureToViewMapTest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class MeasureToViewMapTest
    {
        private static readonly IMeasure MEASURE = MeasureDouble.Create("my measurement", "measurement description", "By");

        private static readonly IViewName VIEW_NAME = ViewName.Create("my view");

        // private static readonly Cumulative CUMULATIVE = Cumulative.create();

        private static readonly IView VIEW =
            View.Create(
                VIEW_NAME,
                "view description",
                MEASURE,
                Mean.Create(),
                new List<string>() { "my key" });

        [Fact]
        public void TestRegisterAndGetView()
        {
            var measureToViewMap = new MeasureToViewMap();
            measureToViewMap.RegisterView(VIEW);
            var viewData = measureToViewMap.GetView(VIEW_NAME, StatsCollectionState.ENABLED);
            Assert.Equal(VIEW, viewData.View);
            Assert.Empty(viewData.AggregationMap);
        }
    }
}
