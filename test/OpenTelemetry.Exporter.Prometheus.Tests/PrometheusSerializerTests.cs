// <copyright file="PrometheusSerializerTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public sealed class PrometheusSerializerTests
    {
        [Fact]
        public void GaugeZeroDimension()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            meter.CreateObservableGauge("test_gauge", () => 123);

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_gauge gauge\n"
                    + "test_gauge 123 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void GaugeZeroDimensionWithDescription()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            meter.CreateObservableGauge("test_gauge", () => 123, description: "Hello, world!");

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# HELP test_gauge Hello, world!\n"
                    + "# TYPE test_gauge gauge\n"
                    + "test_gauge 123 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void GaugeZeroDimensionWithUnit()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            meter.CreateObservableGauge("test_gauge", () => 123, unit: "seconds");

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_gauge_seconds gauge\n"
                    + "test_gauge_seconds 123 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void GaugeOneDimension()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var counter = meter.CreateCounter<long>("test_counter");
            counter.Add(123, new KeyValuePair<string, object>("tagKey", "tagValue"));

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_counter counter\n"
                    + "test_counter{tagKey='tagValue'} 123 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void GaugeDoubleSubnormal()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            meter.CreateObservableGauge("test_gauge", () => new List<Measurement<double>>
            {
                new(double.NegativeInfinity, new("x", "1"), new("y", "2")),
                new(double.PositiveInfinity, new("x", "3"), new("y", "4")),
                new(double.NaN, new("x", "5"), new("y", "6")),
            });

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_gauge gauge\n"
                    + "test_gauge{x='1',y='2'} -Inf \\d+\n"
                    + "test_gauge{x='3',y='4'} \\+Inf \\d+\n"
                    + "test_gauge{x='5',y='6'} Nan \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void SumDoubleInfinites()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var counter = meter.CreateCounter<double>("test_counter");
            counter.Add(1.0E308);
            counter.Add(1.0E308);

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_counter counter\n"
                    + "test_counter \\+Inf \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void HistogramZeroDimension()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var histogram = meter.CreateHistogram<double>("test_histogram");
            histogram.Record(18);
            histogram.Record(100);

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_histogram histogram\n"
                    + "test_histogram_bucket{le='0'} 0 \\d+\n"
                    + "test_histogram_bucket{le='5'} 0 \\d+\n"
                    + "test_histogram_bucket{le='10'} 0 \\d+\n"
                    + "test_histogram_bucket{le='25'} 1 \\d+\n"
                    + "test_histogram_bucket{le='50'} 1 \\d+\n"
                    + "test_histogram_bucket{le='75'} 1 \\d+\n"
                    + "test_histogram_bucket{le='100'} 2 \\d+\n"
                    + "test_histogram_bucket{le='250'} 2 \\d+\n"
                    + "test_histogram_bucket{le='500'} 2 \\d+\n"
                    + "test_histogram_bucket{le='1000'} 2 \\d+\n"
                    + "test_histogram_bucket{le='\\+Inf'} 2 \\d+\n"
                    + "test_histogram_sum 118 \\d+\n"
                    + "test_histogram_count 2 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void HistogramOneDimension()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var histogram = meter.CreateHistogram<double>("test_histogram");
            histogram.Record(18, new KeyValuePair<string, object>("x", "1"));
            histogram.Record(100, new KeyValuePair<string, object>("x", "1"));

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_histogram histogram\n"
                    + "test_histogram_bucket{x='1',le='0'} 0 \\d+\n"
                    + "test_histogram_bucket{x='1',le='5'} 0 \\d+\n"
                    + "test_histogram_bucket{x='1',le='10'} 0 \\d+\n"
                    + "test_histogram_bucket{x='1',le='25'} 1 \\d+\n"
                    + "test_histogram_bucket{x='1',le='50'} 1 \\d+\n"
                    + "test_histogram_bucket{x='1',le='75'} 1 \\d+\n"
                    + "test_histogram_bucket{x='1',le='100'} 2 \\d+\n"
                    + "test_histogram_bucket{x='1',le='250'} 2 \\d+\n"
                    + "test_histogram_bucket{x='1',le='500'} 2 \\d+\n"
                    + "test_histogram_bucket{x='1',le='1000'} 2 \\d+\n"
                    + "test_histogram_bucket{x='1',le='\\+Inf'} 2 \\d+\n"
                    + "test_histogram_sum{x='1'} 118 \\d+\n"
                    + "test_histogram_count{x='1'} 2 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void HistogramTwoDimensions()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var histogram = meter.CreateHistogram<double>("test_histogram");
            histogram.Record(18, new("x", "1"), new("y", "2"));
            histogram.Record(100, new("x", "1"), new("y", "2"));

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_histogram histogram\n"
                    + "test_histogram_bucket{x='1',y='2',le='0'} 0 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='5'} 0 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='10'} 0 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='25'} 1 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='50'} 1 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='75'} 1 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='100'} 2 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='250'} 2 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='500'} 2 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='1000'} 2 \\d+\n"
                    + "test_histogram_bucket{x='1',y='2',le='\\+Inf'} 2 \\d+\n"
                    + "test_histogram_sum{x='1',y='2'} 118 \\d+\n"
                    + "test_histogram_count{x='1',y='2'} 2 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void HistogramInfinites()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var histogram = meter.CreateHistogram<double>("test_histogram");
            histogram.Record(18);
            histogram.Record(double.PositiveInfinity);
            histogram.Record(double.PositiveInfinity);

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_histogram histogram\n"
                    + "test_histogram_bucket{le='0'} 0 \\d+\n"
                    + "test_histogram_bucket{le='5'} 0 \\d+\n"
                    + "test_histogram_bucket{le='10'} 0 \\d+\n"
                    + "test_histogram_bucket{le='25'} 1 \\d+\n"
                    + "test_histogram_bucket{le='50'} 1 \\d+\n"
                    + "test_histogram_bucket{le='75'} 1 \\d+\n"
                    + "test_histogram_bucket{le='100'} 1 \\d+\n"
                    + "test_histogram_bucket{le='250'} 1 \\d+\n"
                    + "test_histogram_bucket{le='500'} 1 \\d+\n"
                    + "test_histogram_bucket{le='1000'} 1 \\d+\n"
                    + "test_histogram_bucket{le='\\+Inf'} 3 \\d+\n"
                    + "test_histogram_sum \\+Inf \\d+\n"
                    + "test_histogram_count 3 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void HistogramNaN()
        {
            var buffer = new byte[85000];
            var metrics = new List<Metric>();

            using var meter = new Meter(Utils.GetCurrentMethodName());
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(metrics)
                .Build();

            var histogram = meter.CreateHistogram<double>("test_histogram");
            histogram.Record(18);
            histogram.Record(double.PositiveInfinity);
            histogram.Record(double.NaN);

            provider.ForceFlush();

            var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metrics[0]);
            Assert.Matches(
                ("^"
                    + "# TYPE test_histogram histogram\n"
                    + "test_histogram_bucket{le='0'} 0 \\d+\n"
                    + "test_histogram_bucket{le='5'} 0 \\d+\n"
                    + "test_histogram_bucket{le='10'} 0 \\d+\n"
                    + "test_histogram_bucket{le='25'} 1 \\d+\n"
                    + "test_histogram_bucket{le='50'} 1 \\d+\n"
                    + "test_histogram_bucket{le='75'} 1 \\d+\n"
                    + "test_histogram_bucket{le='100'} 1 \\d+\n"
                    + "test_histogram_bucket{le='250'} 1 \\d+\n"
                    + "test_histogram_bucket{le='500'} 1 \\d+\n"
                    + "test_histogram_bucket{le='1000'} 1 \\d+\n"
                    + "test_histogram_bucket{le='\\+Inf'} 3 \\d+\n"
                    + "test_histogram_sum Nan \\d+\n"
                    + "test_histogram_count 3 \\d+\n"
                    + "$").Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }
    }
}
