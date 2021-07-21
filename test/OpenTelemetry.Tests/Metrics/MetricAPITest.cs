// <copyright file="MetricAPITest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricApiTest
    {
        [Fact]
        public void CounterDelta()
        {
            var processor = new TestProcessor();

            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddMetricProcessor(processor)
                .Build();

            using var meter = new Meter("TestMeter", "0.0.1");
            var counter = meter.CreateCounter<int>("counter");

            // Delta Collector

            counter.Add(10);
            var metrics = processor.Collect(true);
            var metric = metrics.Metrics.Single() as ISumMetric;
            var value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            counter.Add(10);
            metrics = processor.Collect(true);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            counter.Add(10);
            metrics = processor.Collect(true);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            // Cumulative Collector

            counter.Add(10);
            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(40, value);

            counter.Add(10);
            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(50, value);

            counter.Add(10);
            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(60, value);
        }

        [Fact]
        public void ObservableCounterIncreasingCumulative()
        {
            var processor = new TestProcessor();

            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddMetricProcessor(processor)
                .Build();

            int c = 0;

            using var meter = new Meter("TestMeter", "0.0.1");
            var counter = meter.CreateObservableCounter<int>("counter", () =>
            {
                c++;
                return c * 10;
            });

            // Delta Collector

            var metrics = processor.Collect(true);
            var metric = metrics.Metrics.Single() as ISumMetric;
            var value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            metrics = processor.Collect(true);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            metrics = processor.Collect(true);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            // Cumulative Collector

            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(40, value);

            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(50, value);

            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(60, value);
        }

        [Fact]
        public void ObservableCounterConstantCumulative()
        {
            var processor = new TestProcessor();

            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddMetricProcessor(processor)
                .Build();

            using var meter = new Meter("TestMeter", "0.0.1");
            var counter = meter.CreateObservableCounter<int>("counter", () => 10);

            // Delta Collector

            var metrics = processor.Collect(true);
            var metric = metrics.Metrics.Single() as ISumMetric;
            var value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            metrics = processor.Collect(true);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(0, value);

            metrics = processor.Collect(true);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(0, value);

            // Cumulative Collector

            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(10, value);

            metrics = processor.Collect(false);
            metric = metrics.Metrics.Single() as ISumMetric;
            value = (long)metric.Sum.Value;
            Assert.Equal(10, value);
        }

        public class TestProcessor : MetricProcessor
        {
            private Func<bool, MetricItem>? getMetrics;

            public TestProcessor()
                : base(new TestExporter())
            {
            }

            public override void SetGetMetricFunction(Func<bool, MetricItem> getMetrics)
            {
                this.getMetrics = getMetrics;
            }

            public MetricItem Collect(bool isDelta)
            {
                if (this.getMetrics != null)
                {
                    return this.getMetrics(isDelta);
                }

                return null;
            }

            public class TestExporter : BaseExporter<MetricItem>
            {
                public override ExportResult Export(in Batch<MetricItem> batch)
                {
                    return ExportResult.Success;
                }
            }

        }
    }
}
