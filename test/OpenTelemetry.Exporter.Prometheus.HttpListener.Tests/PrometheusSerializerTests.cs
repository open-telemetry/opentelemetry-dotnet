// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PrometheusSerializerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GaugeZeroDimension(bool disableTimestamp)
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

        var cursor = WriteMetric(buffer, 0, metrics[0], false, disableTimestamp);
        var timestampPart = disableTimestamp ? string.Empty : " \\d+";
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);
        var expected =
            ("^"
                + "# TYPE test_gauge gauge\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 123{timestampPart}\n"
                + "$").Replace('\'', '"');
        Assert.Matches(expected, output);
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
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 123 \\d+\n"
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
                + $"test_gauge_seconds{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 123 \\d+\n"
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
                + $"test_gauge_seconds{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 123 \\d+\n"
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
            () => new Measurement<long>(123, new KeyValuePair<string, object?>("tagKey", "tagValue")));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_gauge gauge\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',tagKey='tagValue'}} 123 \\d+\n"
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
            () => new Measurement<long>(123, new KeyValuePair<string, object?>("tagKey", true)));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_gauge gauge\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',tagKey='true'}} 123 \\d+\n"
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
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2'}} -Inf \\d+\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='3',y='4'}} \\+Inf \\d+\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='5',y='6'}} Nan \\d+\n"
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
                + $"test_counter_total{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} \\+Inf \\d+\n"
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
                + $"test_updown_counter{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} -1 \\d+\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HistogramZeroDimension(bool disableTimestamp)
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

        var cursor = WriteMetric(buffer, 0, metrics[0], false, disableTimestamp);
        var timestampPart = disableTimestamp ? string.Empty : " \\d+";
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);
        var expected =
            ("^"
                + "# TYPE test_histogram histogram\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='0'}} 0{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5'}} 0{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10'}} 0{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='25'}} 1{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='50'}} 1{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='75'}} 1{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='100'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='250'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='500'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='750'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='1000'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='2500'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5000'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='7500'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10000'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='\\+Inf'}} 2{timestampPart}\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 118{timestampPart}\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 2{timestampPart}\n"
                + "$").Replace('\'', '"');
        Assert.Matches(expected, output);
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
        histogram.Record(18, new KeyValuePair<string, object?>("x", "1"));
        histogram.Record(100, new KeyValuePair<string, object?>("x", "1"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_histogram histogram\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='0'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='5'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='10'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='25'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='50'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='75'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='100'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='250'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='500'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='750'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='1000'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='2500'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='5000'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='7500'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='10000'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='\\+Inf'}} 2 \\d+\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} 118 \\d+\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} 2 \\d+\n"
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
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='0'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='5'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='10'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='25'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='50'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='75'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='100'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='250'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='500'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='750'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='1000'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='2500'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='5000'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='7500'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='10000'}} 2 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='\\+Inf'}} 2 \\d+\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2'}} 118 \\d+\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2'}} 2 \\d+\n"
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
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='0'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='25'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='50'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='75'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='100'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='250'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='500'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='750'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='1000'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='2500'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5000'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='7500'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10000'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='\\+Inf'}} 3 \\d+\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} \\+Inf \\d+\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 3 \\d+\n"
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
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='0'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10'}} 0 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='25'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='50'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='75'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='100'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='250'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='500'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='750'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='1000'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='2500'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5000'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='7500'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10000'}} 1 \\d+\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='\\+Inf'}} 3 \\d+\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} Nan \\d+\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 3 \\d+\n"
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SumWithOpenMetricsFormat(bool disableTimestamp)
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

        var cursor = WriteMetric(buffer, 0, metrics[0], true, disableTimestamp);
        var timestampPart = disableTimestamp ? string.Empty : " \\d+\\.\\d{3}";
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);
        var expected =
            ("^"
                + "# TYPE test_updown_counter gauge\n"
                + $"test_updown_counter{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} -1{timestampPart}\n"
                + "$").Replace('\'', '"');
        Assert.Matches(expected, output);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HistogramOneDimensionWithOpenMetricsFormat(bool disableTimestamp)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18, new KeyValuePair<string, object?>("x", "1"));
        histogram.Record(100, new KeyValuePair<string, object?>("x", "1"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true, disableTimestamp);
        var timestampPart = disableTimestamp ? string.Empty : " \\d+\\.\\d{3}";
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);
        var expected =
            ("^"
                + "# TYPE test_histogram histogram\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='0'}} 0{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='5'}} 0{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='10'}} 0{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='25'}} 1{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='50'}} 1{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='75'}} 1{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='100'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='250'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='500'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='750'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='1000'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='2500'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='5000'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='7500'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='10000'}} 2{timestampPart}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='\\+Inf'}} 2{timestampPart}\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} 118{timestampPart}\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} 2{timestampPart}\n"
                + "$").Replace('\'', '"');
        Assert.Matches(expected, output);
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
    public void SumWithScopeVersion()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();
        using var meter = new Meter(Utils.GetCurrentMethodName(), "1.0.0");
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
             + $"test_updown_counter{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0'}} -1 \\d+\\.\\d{{3}}\n"
             + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void HistogramOneDimensionWithScopeVersion()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName(), "1.0.0");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18, new KeyValuePair<string, object?>("x", "1"));
        histogram.Record(100, new KeyValuePair<string, object?>("x", "1"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        Assert.Matches(
            ("^"
                + "# TYPE test_histogram histogram\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='0'}} 0 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='5'}} 0 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='10'}} 0 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='25'}} 1 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='50'}} 1 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='75'}} 1 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='100'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='250'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='500'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='750'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='1000'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='2500'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='5000'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='7500'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='10000'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='\\+Inf'}} 2 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1'}} 118 \\d+\\.\\d{{3}}\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1'}} 2 \\d+\\.\\d{{3}}\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    private static int WriteMetric(byte[] buffer, int cursor, Metric metric, bool useOpenMetrics = false, bool disableTimestamp = false)
    {
        return PrometheusSerializer.WriteMetric(buffer, cursor, metric, PrometheusMetric.Create(metric, false), useOpenMetrics, disableTimestamp);
    }
}
