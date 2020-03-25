// <copyright file="LastValueAggregatorTest.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Metrics.Export;
using Xunit;

namespace OpenTelemetry.Metrics.Test
{
    public class LastValueAggregatorTest
    {
        [Fact]
        public void LastValueAggregatorSupportsLong()
        {
            LastValueAggregator<long> aggregator = new LastValueAggregator<long>();
        }

        [Fact]
        public void LastValueAggregatorSupportsDouble()
        {
            LastValueAggregator<double> aggregator = new LastValueAggregator<double>();
        }

        [Fact]
        public void LastValueAggregatorConstructorThrowsForUnSupportedTypeInt()
        {
            Assert.Throws<Exception>(() => new LastValueAggregator<int>());
        }

        [Fact]
        public void LastValueAggregatorConstructorThrowsForUnSupportedTypeByte()
        {
            Assert.Throws<Exception>(() => new LastValueAggregator<byte>());
        }

        [Fact]
        public void LastValueAggregatorAggregatesCorrectly()
        {
            // create an aggregator
            LastValueAggregator<long> aggregator = new LastValueAggregator<long>();
            var sum = aggregator.ToMetricData() as SumData<long>;

            // we start with 0.
            Assert.Equal(0, sum.Sum);

            aggregator.Update(10);
            aggregator.Update(20);
            aggregator.Update(30);
            aggregator.Update(40);

            aggregator.Checkpoint();
            sum = aggregator.ToMetricData() as SumData<long>;
            Assert.Equal(40, sum.Sum);
        }
    }
}
