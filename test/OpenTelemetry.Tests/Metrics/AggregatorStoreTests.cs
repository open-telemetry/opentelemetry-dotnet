// <copyright file="AggregatorStoreTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class AggregatorStoreTests
    {
        [Fact]
        public void MetricPointStatusIsNoPendingUpdateAfterCollect()
        {
            var aggregatorStore = new AggregatorStore(AggregationType.LongGauge, AggregationTemporality.Delta, Metric.DefaultHistogramBounds);

            var maxMetricPoints = AggregatorStore.MaxMetricPoints;

            // MetricPoint with no tags is reserved.
            aggregatorStore.Update(100, ReadOnlySpan<KeyValuePair<string, object>>.Empty);

            for (var i = 0; i < maxMetricPoints - 1; ++i)
            {
                aggregatorStore.Update(100, new[] { new KeyValuePair<string, object>("key", i) });
            }

            aggregatorStore.SnapShot();
            var batch = aggregatorStore.GetMetricPoints();

            var batchCount = 0;
            foreach (var point in batch)
            {
                Assert.Equal(MetricPointStatus.NoPendingUpdate, point.MetricPointStatus);
                batchCount++;
            }

            Assert.Equal(maxMetricPoints, batchCount);

            aggregatorStore.SnapShot();
            batch = aggregatorStore.GetMetricPoints();

            batchCount = 0;
            foreach (var point in batch)
            {
                batchCount++;
            }

            Assert.Equal(0, batchCount);
        }
    }
}
