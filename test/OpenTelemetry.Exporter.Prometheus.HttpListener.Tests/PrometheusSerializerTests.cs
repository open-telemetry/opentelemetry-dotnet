// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PrometheusSerializerTests
{
    public static TheoryData<object?, string> LabelValueBoundaryCases => new()
    {
        { false, "false" },
        { true, "true" },
        { null, string.Empty },
        { string.Empty, string.Empty },
        { "tagValue", "tagValue" },
        { "tagValueWith\"Quote", "tagValueWith\\\"Quote" },
        { "tagValueWith\\Backslash", "tagValueWith\\\\Backslash" },
        { "tagValueWith\nNewline", "tagValueWith\\nNewline" },
        { "\"line1\\\nline2\"", "\\\"line1\\\\\\nline2\\\"" },
        { "caf\u00e9", "caf\u00e9" },
        { sbyte.MinValue, "-128" },
        { (sbyte)0, "0" },
        { sbyte.MaxValue, "127" },
        { byte.MinValue, "0" },
        { byte.MaxValue, "255" },
        { short.MinValue, "-32768" },
        { (short)0, "0" },
        { short.MaxValue, "32767" },
        { ushort.MinValue, "0" },
        { ushort.MaxValue, "65535" },
        { int.MinValue, "-2147483648" },
        { 0, "0" },
        { int.MaxValue, "2147483647" },
        { uint.MinValue, "0" },
        { uint.MaxValue, "4294967295" },
        { long.MinValue, "-9223372036854775808" },
        { 0L, "0" },
        { long.MaxValue, "9223372036854775807" },
        { ulong.MinValue, "0" },
        { ulong.MaxValue, "18446744073709551615" },
        { float.MinValue, "-3.4028234663852886E+38" },
        { 0f, "0" },
        { float.NaN, "NaN" },
        { float.NegativeInfinity, "-Inf" },
        { float.PositiveInfinity, "+Inf" },
        { float.MaxValue, "3.4028234663852886E+38" },
        { double.MinValue, "-1.7976931348623157E+308" },
        { 0d, "0" },
        { double.NegativeInfinity, "-Inf" },
        { double.PositiveInfinity, "+Inf" },
        { double.NaN, "NaN" },
        { double.MaxValue, "1.7976931348623157E+308" },
        { decimal.MinValue, "-79228162514264337593543950335" },
        { 0m, "0" },
        { decimal.MaxValue, "79228162514264337593543950335" },
    };

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

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);
        var expected =
            ("^"
                + "# TYPE test_gauge gauge\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 123\n"
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
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 123\n"
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
                + $"test_gauge_seconds{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 123\n"
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
                + $"test_gauge_seconds{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 123\n"
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
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',tagKey='tagValue'}} 123\n"
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
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',tagKey='true'}} 123\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void GaugeEmptyDimensionName()
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
            () => new Measurement<long>(123, new KeyValuePair<string, object?>(string.Empty, "tagValue")));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_gauge gauge\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',_='tagValue'}} 123\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void WriteLabelKeyNullOrEmptyName(string? labelName)
    {
        var buffer = new byte[32];

        var cursor = PrometheusSerializer.WriteLabelKey(buffer, 0, labelName!);

        Assert.Equal("_", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteMetricNameSanitizesNonAsciiCharacters()
    {
        var buffer = new byte[32];
        var metric = new PrometheusMetric("A\u010A", string.Empty, PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var cursor = PrometheusSerializer.WriteMetricName(buffer, 0, metric, openMetricsRequested: false);

        Assert.Equal("A_", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteMetricNameSanitizesNonAsciiUnitCharacters()
    {
        var buffer = new byte[32];
        var metric = new PrometheusMetric("metric", "s\u010A", PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var cursor = PrometheusSerializer.WriteMetricName(buffer, 0, metric, openMetricsRequested: false);

        Assert.Equal("metric_s", Encoding.UTF8.GetString(buffer, 0, cursor));
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
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2'}} -Inf\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='3',y='4'}} \\+Inf\n"
                + $"test_gauge{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='5',y='6'}} NaN\n"
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
                + $"test_counter_total{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} \\+Inf\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void SumLongSerializesBoundaryValues(long value)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using (var provider = Sdk.CreateMeterProviderBuilder()
                                 .AddMeter(meter.Name)
                                 .AddInMemoryExporter(metrics)
                                 .Build())
        {
            var counter = meter.CreateCounter<long>("test_counter");
            counter.Add(value);

            provider.ForceFlush();
        }

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        Assert.Matches(
            ("^"
                + "# TYPE test_counter_total counter\n"
                + $"test_counter_total{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} {value.ToString(CultureInfo.InvariantCulture)}\n"
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
                + $"test_updown_counter{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} -1\n"
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

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);
        var expected =
            ("^"
                + "# TYPE test_histogram histogram\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='0'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='25'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='50'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='75'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='100'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='250'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='750'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='1000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='2500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='7500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='\\+Inf'}} 2\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 118\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 2\n"
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
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='0'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='5'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='10'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='25'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='50'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='75'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='100'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='250'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='750'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='1000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='2500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='5000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='7500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='10000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='\\+Inf'}} 2\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} 118\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} 2\n"
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
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='0'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='5'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='10'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='25'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='50'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='75'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='100'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='250'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='750'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='1000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='2500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='5000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='7500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='10000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2',le='\\+Inf'}} 2\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2'}} 118\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',y='2'}} 2\n"
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
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='0'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='25'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='50'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='75'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='100'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='250'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='500'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='750'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='1000'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='2500'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5000'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='7500'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10000'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='\\+Inf'}} 3\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} \\+Inf\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 3\n"
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
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='0'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='25'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='50'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='75'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='100'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='250'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='500'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='750'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='1000'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='2500'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='5000'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='7500'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='10000'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',le='\\+Inf'}} 3\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} NaN\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} 3\n"
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
    public void SumWithOpenMetricsFormat()
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

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);
        var expected =
            ("^"
                + "# TYPE test_updown_counter gauge\n"
                + $"test_updown_counter{{otel_scope_name='{Utils.GetCurrentMethodName()}'}} -1\n"
                + "$").Replace('\'', '"');
        Assert.Matches(expected, output);
    }

    [Fact]
    public void HistogramOneDimensionWithOpenMetricsFormat()
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

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);
        var expected =
            ("^"
                + "# TYPE test_histogram histogram\n"
                + "# TYPE test_histogram_created gauge\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='0'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='5'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='10'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='25'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='50'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='75'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='100'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='250'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='750'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='1000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='2500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='5000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='7500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='10000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1',le='\\+Inf'}} 2\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} 118\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} 2\n"
                + $"test_histogram_created{{otel_scope_name='{Utils.GetCurrentMethodName()}',x='1'}} [0-9]+(?:\\.[0-9]+)?\n"
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
             + $"test_updown_counter{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0'}} -1\n"
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
                + "# TYPE test_histogram_created gauge\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='0'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='5'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='10'}} 0\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='25'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='50'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='75'}} 1\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='100'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='250'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='750'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='1000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='2500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='5000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='7500'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='10000'}} 2\n"
                + $"test_histogram_bucket{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1',le='\\+Inf'}} 2\n"
                + $"test_histogram_sum{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1'}} 118\n"
                + $"test_histogram_count{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1'}} 2\n"
                + $"test_histogram_created{{otel_scope_name='{Utils.GetCurrentMethodName()}',otel_scope_version='1.0.0',x='1'}} [0-9]+(?:\\.[0-9]+)?\n"
                + "$").Replace('\'', '"'),
            Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteAsciiStringNoEscapeWritesAsciiBytes()
    {
        var value = "metric_name_total";
        var buffer = new byte[64];

        var cursor = PrometheusSerializer.WriteAsciiStringNoEscape(buffer, 0, value);

        Assert.Equal("metric_name_total", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteAsciiStringNoEscapeThrowsExceptionWhenBufferTooSmall()
    {
        var buffer = new byte[4];

        Assert.Throws<IndexOutOfRangeException>(() => PrometheusSerializer.WriteAsciiStringNoEscape(buffer, 0, "metric"));
    }

    [Fact]
    public void WriteLabelValueEscapesSpecialCharacters()
    {
        var buffer = new byte[128];

        var cursor = PrometheusSerializer.WriteLabelValue(buffer, 0, "\"line1\\\nline2\"");

        Assert.Equal("\\\"line1\\\\\\nline2\\\"", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
#pragma warning disable xUnit1045 // Avoid using TheoryData type arguments that might not be serializable
    [MemberData(nameof(LabelValueBoundaryCases))]
#pragma warning restore xUnit1045 // Avoid using TheoryData type arguments that might not be serializable
    public void WriteMetricSerializesStaticMeterTagBoundaryValues(object? meterTagValue, string expectedTagValue)
    {
        var output = WriteGaugeMetricWithMeterTags(new KeyValuePair<string, object?>("meter_tag", meterTagValue));

        Assert.Equal(
            ("# TYPE test_gauge gauge\n"
             + $"test_gauge{{otel_scope_name='test_meter',meter_tag='{expectedTagValue}'}} 123\n").Replace('\'', '"'),
            output);
    }

    [Fact]
    public void WriteLabelFormatsTypedValues()
    {
        var buffer = new byte[128];

        var cursor = PrometheusSerializer.WriteLabel(buffer, 0, "value", 18446744073709551615UL);

        Assert.Equal("value=\"18446744073709551615\"", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void WriteLongMatchesInvariantFormatting(long value)
    {
        var buffer = new byte[64];

        var cursor = PrometheusSerializer.WriteLong(buffer, 0, value);

        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData(double.NegativeInfinity, "-Inf")]
    [InlineData(-1234.5, "-1234.5")]
    [InlineData(0d, "0")]
    [InlineData(1234.5, "1234.5")]
    [InlineData(double.PositiveInfinity, "+Inf")]
    public void WriteDoubleMatchesInvariantFormatting(double value, string expected)
    {
        var buffer = new byte[64];

        var cursor = PrometheusSerializer.WriteDouble(buffer, 0, value);

        Assert.Equal(expected, Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteDoubleFormatsNaN()
    {
        var buffer = new byte[64];

        var cursor = PrometheusSerializer.WriteDouble(buffer, 0, double.NaN);

        Assert.Equal("NaN", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CounterExportsCreatedMetric(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("test_meter");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateCounter<double>("test_counter");
        counter.Add(1, [new KeyValuePair<string, object?>("key", "value")]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        if (useOpenMetrics)
        {
            Assert.Contains("# TYPE test_counter_created gauge", output, StringComparison.Ordinal);
            Assert.Matches("test_counter_created\\{otel_scope_name=\"test_meter\",key=\"value\"\\} [0-9]+(?:\\.[0-9]+)?", output);
        }
        else
        {
            Assert.DoesNotContain("_created{", output, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistogramExportsCreatedMetric(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("test_meter");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(1, [new KeyValuePair<string, object?>("key", "value")]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        if (useOpenMetrics)
        {
            Assert.Contains("# TYPE test_histogram_created gauge", output, StringComparison.Ordinal);
            Assert.Matches("test_histogram_created\\{otel_scope_name=\"test_meter\",key=\"value\"\\} [0-9]+(?:\\.[0-9]+)?", output);
        }
        else
        {
            Assert.DoesNotContain("_created{", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WriteUnicodeStringEncodesSurrogatePairsAsUtf8ScalarValues()
    {
        const string value = "rocket:\uD83D\uDE80";
        var buffer = new byte[128];

        var cursor = PrometheusSerializer.WriteUnicodeString(buffer, 0, value);
        var actual = ToHexString(buffer, cursor);
        var expected = ToHexString(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetByteCount(value));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WriteUnicodeStringReplacesInvalidSurrogates()
    {
        const string value = "rocket:\uD83D";
        var buffer = new byte[128];

        var cursor = PrometheusSerializer.WriteUnicodeString(buffer, 0, value);
        var actual = ToHexString(buffer, cursor);
        var expected = ToHexString(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetByteCount(value));

        Assert.Equal(expected, actual);
    }

#if NET
    [Fact]
    public void WriteHistogramMetricSerializesStaticTagsWithoutPreSerializedTags()
    {
        var buffer = new byte[85000];

        var metric = GetSingleHistogramMetric(
            meterName: "\u65e5\u672c",
            meterTags: [new KeyValuePair<string, object?>(string.Empty, "meterTagValue")]);

        var prometheusMetric = new PrometheusMetric(metric.Name, metric.Unit, PrometheusType.Histogram, disableTotalNameSuffixForCounters: false);

        var cursor = PrometheusSerializer.WriteMetric(buffer, 0, metric, prometheusMetric, openMetricsRequested: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        Assert.Contains("test_histogram_bucket{otel_scope_name=\"\u65e5\u672c\",_=\"meterTagValue\",le=\"0\"} 0\n", output, StringComparison.Ordinal);
        Assert.Contains("test_histogram_sum{otel_scope_name=\"\u65e5\u672c\",_=\"meterTagValue\"} 18\n", output, StringComparison.Ordinal);
        Assert.Contains("test_histogram_count{otel_scope_name=\"\u65e5\u672c\",_=\"meterTagValue\"} 1\n", output, StringComparison.Ordinal);
    }

    private static Metric GetSingleHistogramMetric(string meterName, params KeyValuePair<string, object?>[] meterTags)
    {
        var metrics = new List<Metric>();

        using var meter = new Meter(name: meterName, version: null, tags: meterTags);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18);

        provider.ForceFlush();

        return metrics.Single();
    }
#endif

    private static string ToHexString(byte[] buffer, int length)
    {
        var chars = new char[length * 2];

        for (var i = 0; i < length; i++)
        {
            var value = buffer[i];
            chars[i * 2] = GetHexValue(value >> 4);
            chars[(i * 2) + 1] = GetHexValue(value & 0xF);
        }

        return new string(chars);

        static char GetHexValue(int value) => (char)(value < 10 ? '0' + value : 'A' + (value - 10));
    }

    private static int WriteMetric(byte[] buffer, int cursor, Metric metric, bool useOpenMetrics = false)
        => PrometheusSerializer.WriteMetric(buffer, cursor, metric, PrometheusMetric.Create(metric, false), useOpenMetrics);

    private static string WriteGaugeMetricWithMeterTags(params KeyValuePair<string, object?>[] meterTags)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(name: "test_meter", version: null, tags: meterTags);
        using (var provider = Sdk.CreateMeterProviderBuilder()
                                 .AddMeter(meter.Name)
                                 .AddInMemoryExporter(metrics)
                                 .Build())
        {
            meter.CreateObservableGauge("test_gauge", () => 123);
            provider.ForceFlush();
        }

        var cursor = WriteMetric(buffer, 0, metrics[0]);
        return Encoding.UTF8.GetString(buffer, 0, cursor);
    }
}
