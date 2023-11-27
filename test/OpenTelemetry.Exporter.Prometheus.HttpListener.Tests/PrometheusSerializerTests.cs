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

using System.Diagnostics.Metrics;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_gauge gauge\n"
                + "# HELP test_gauge Hello, world!\n"
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_gauge_seconds gauge\n"
                + "# UNIT test_gauge_seconds seconds\n"
                + "test_gauge_seconds 123 \\d+\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void GaugeZeroDimensionWithDescriptionAndUnit()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge("test_gauge", () => 123, unit: "seconds", description: "Hello, world!");

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_gauge_seconds gauge\n"
                + "# UNIT test_gauge_seconds seconds\n"
                + "# HELP test_gauge_seconds Hello, world!\n"
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

        meter.CreateObservableGauge(
            "test_gauge",
            () => new Measurement<long>(123, new KeyValuePair<string, object>("tagKey", "tagValue")));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_gauge gauge\n"
                + "test_gauge{tagKey='tagValue'} 123 \\d+\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void GaugeBoolDimension()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge(
            "test_gauge",
            () => new Measurement<long>(123, new KeyValuePair<string, object>("tagKey", true)));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_gauge gauge\n"
                + "test_gauge{tagKey='true'} 123 \\d+\n"
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
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
    public void SumDoubleInfinities()
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_counter_total counter\n"
                + "test_counter_total \\+Inf \\d+\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void SumNonMonotonicDouble()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateUpDownCounter<double>("test_updown_counter");
        counter.Add(10);
        counter.Add(-11);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_updown_counter gauge\n"
                + "test_updown_counter -1 \\d+\n"
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
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
                + "test_histogram_bucket{le='750'} 2 \\d+\n"
                + "test_histogram_bucket{le='1000'} 2 \\d+\n"
                + "test_histogram_bucket{le='2500'} 2 \\d+\n"
                + "test_histogram_bucket{le='5000'} 2 \\d+\n"
                + "test_histogram_bucket{le='7500'} 2 \\d+\n"
                + "test_histogram_bucket{le='10000'} 2 \\d+\n"
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
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
                + "test_histogram_bucket{x='1',le='750'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',le='1000'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',le='2500'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',le='5000'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',le='7500'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',le='10000'} 2 \\d+\n"
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
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
                + "test_histogram_bucket{x='1',y='2',le='750'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',y='2',le='1000'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',y='2',le='2500'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',y='2',le='5000'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',y='2',le='7500'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',y='2',le='10000'} 2 \\d+\n"
                + "test_histogram_bucket{x='1',y='2',le='\\+Inf'} 2 \\d+\n"
                + "test_histogram_sum{x='1',y='2'} 118 \\d+\n"
                + "test_histogram_count{x='1',y='2'} 2 \\d+\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void HistogramInfinities()
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
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
                + "test_histogram_bucket{le='750'} 1 \\d+\n"
                + "test_histogram_bucket{le='1000'} 1 \\d+\n"
                + "test_histogram_bucket{le='2500'} 1 \\d+\n"
                + "test_histogram_bucket{le='5000'} 1 \\d+\n"
                + "test_histogram_bucket{le='7500'} 1 \\d+\n"
                + "test_histogram_bucket{le='10000'} 1 \\d+\n"
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

        var cursor = WriteMetric(buffer, 0, metrics[0]);
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
                + "test_histogram_bucket{le='750'} 1 \\d+\n"
                + "test_histogram_bucket{le='1000'} 1 \\d+\n"
                + "test_histogram_bucket{le='2500'} 1 \\d+\n"
                + "test_histogram_bucket{le='5000'} 1 \\d+\n"
                + "test_histogram_bucket{le='7500'} 1 \\d+\n"
                + "test_histogram_bucket{le='10000'} 1 \\d+\n"
                + "test_histogram_bucket{le='\\+Inf'} 3 \\d+\n"
                + "test_histogram_sum Nan \\d+\n"
                + "test_histogram_count 3 \\d+\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void ExponentialHistogramIsIgnoredForNow()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView(instrument => new Base2ExponentialBucketHistogramConfiguration())
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18);
        histogram.Record(100);

        provider.ForceFlush();

        Assert.False(PrometheusSerializer.CanWriteMetric(metrics[0]));
    }

    [Fact]
    public void ScopeInfo()
    {
        var buffer = new byte[85000];

        var cursor = PrometheusSerializer.WriteScopeInfo(buffer, 0, "test_meter");

        Assert.Matches(
            ("^"
             + "# TYPE otel_scope_info info\n"
             + "# HELP otel_scope_info Scope metadata\n"
             + "otel_scope_info{otel_scope_name='test_meter'} 1\n"
             + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void SumWithScopeInfo()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("meter_name", "meter_version");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateUpDownCounter<double>("test_updown_counter");
        counter.Add(10);
        counter.Add(-11);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        Assert.Matches(
            ("^"
             + "# TYPE test_updown_counter gauge\n"
             + "test_updown_counter{otel_scope_name='meter_name',otel_scope_version='meter_version'} -1 \\d+\n"
             + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void HistogramOneDimensionWithScopeInfo()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("meter_name", "meter_version");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18, new KeyValuePair<string, object>("x", "1"));
        histogram.Record(100, new KeyValuePair<string, object>("x", "1"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        Assert.Matches(
            ("^"
                + "# TYPE test_histogram histogram\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='0'} 0 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='5'} 0 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='10'} 0 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='25'} 1 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='50'} 1 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='75'} 1 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='100'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='250'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='500'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='750'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='1000'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='2500'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='5000'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='7500'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='10000'} 2 \\d+\n"
                + "test_histogram_bucket{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1',le='\\+Inf'} 2 \\d+\n"
                + "test_histogram_sum{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1'} 118 \\d+\n"
                + "test_histogram_count{otel_scope_name='meter_name',otel_scope_version='meter_version',x='1'} 2 \\d+\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    private static int WriteMetric(byte[] buffer, int cursor, Metric metric, bool scopeInfoEnabled = false)
    {
        return PrometheusSerializer.WriteMetric(buffer, cursor, metric, PrometheusMetric.Create(metric), scopeInfoEnabled);
    }
}
