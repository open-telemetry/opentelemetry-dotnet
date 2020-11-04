// <copyright file="AggregatorTest.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
using System.Threading;
using OpenTelemetry.Metrics.Aggregators;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class AggregatorTest
    {
        [Fact]
        public void TracksStartAndEndTimesOfAggregation()
        {
            // create an aggregator
            var aggregator = new Int64CounterSumAggregator();
            aggregator.Update(1);
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            aggregator.Checkpoint();
            var metricData = aggregator.ToMetricData();
            aggregator.Update(2);
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            aggregator.Checkpoint();
            var otherMetricData = aggregator.ToMetricData();

            Assert.True(DateTime.Compare(metricData.StartTimestamp, metricData.Timestamp) < 0);
            Assert.True(DateTime.Compare(metricData.Timestamp, otherMetricData.StartTimestamp) < 0);
            Assert.True(
                DateTime.Compare(
                    metricData.Timestamp.Add(TimeSpan.FromTicks(1)),
                    otherMetricData.StartTimestamp) == 0);
            Assert.True(DateTime.Compare(otherMetricData.StartTimestamp, otherMetricData.Timestamp) < 0);
        }
    }
}
