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
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class AggregatorTest
    {
        [Fact]
        public void HistogramDistributeToAllBuckets()
        {
            var histogramPoint = new MetricPoint(AggregationType.Histogram, DateTimeOffset.Now, null, null);
            histogramPoint.Update(-1);
            histogramPoint.Update(0);
            histogramPoint.Update(2);
            histogramPoint.Update(5);
            histogramPoint.Update(8);
            histogramPoint.Update(10);
            histogramPoint.Update(11);
            histogramPoint.Update(25);
            histogramPoint.Update(40);
            histogramPoint.Update(50);
            histogramPoint.Update(70);
            histogramPoint.Update(75);
            histogramPoint.Update(99);
            histogramPoint.Update(100);
            histogramPoint.Update(246);
            histogramPoint.Update(250);
            histogramPoint.Update(499);
            histogramPoint.Update(500);
            histogramPoint.Update(999);
            histogramPoint.Update(1000);
            histogramPoint.Update(1001);
            histogramPoint.Update(10000000);
            histogramPoint.TakeSnapShot(true);

            Assert.Equal(22, histogramPoint.LongValue);
            for (int i = 0; i < histogramPoint.BucketCounts.Length; i++)
            {
                Assert.Equal(2, histogramPoint.BucketCounts[i]);
            }
        }
    }
}
