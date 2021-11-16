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

using System;
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
        public void ZeroDimension()
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
                "^# TYPE test_gauge gauge\ntest_gauge 123 \\d+\n$",
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void ZeroDimensionWithDescription()
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
                "^# HELP test_gauge Hello, world!\n# TYPE test_gauge gauge\ntest_gauge 123 \\d+\n$",
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void ZeroDimensionWithUnit()
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
                "^# TYPE test_gauge_seconds gauge\ntest_gauge_seconds 123 \\d+\n$",
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void OneDimension()
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
                "^# TYPE test_counter counter\ntest_counter{tagKey='tagValue'} 123 \\d+\n$".Replace('\'', '"'),
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        [Fact]
        public void DoubleInfinites()
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
                "^# TYPE test_counter counter\ntest_counter \\+Inf \\d+\n$",
                Encoding.UTF8.GetString(buffer, 0, cursor));
        }
    }
}
