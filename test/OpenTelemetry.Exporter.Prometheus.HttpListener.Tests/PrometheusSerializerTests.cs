// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OpenTelemetry.Exporter.Prometheus.Serialization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed partial class PrometheusSerializerTests
{
    internal static readonly VerifySettings VerifySettings = CreateVerifySettings();

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
        { float.MinValue, "-3.40282346638528860e+038" },
        { 0f, "0.0" },
        { float.NaN, "NaN" },
        { float.NegativeInfinity, "-Inf" },
        { float.PositiveInfinity, "+Inf" },
        { float.MaxValue, "3.40282346638528860e+038" },
#if NET
        { double.MinValue, "-1.79769313486231571e+308" },
#else
        { double.MinValue, "-1.79769313486231570e+308" },
#endif
        { 0d, "0.0" },
        { double.NegativeInfinity, "-Inf" },
        { double.PositiveInfinity, "+Inf" },
        { double.NaN, "NaN" },
#if NET
        { double.MaxValue, "1.79769313486231571e+308" },
#else
        { double.MaxValue, "1.79769313486231570e+308" },
#endif
        { decimal.MinValue, "-79228162514264337593543950335" },
        { 0m, "0" },
        { decimal.MaxValue, "79228162514264337593543950335" },
        { 1.5d, "1.5" },
        { new Guid("12345678-1234-1234-1234-1234567890ab"), "12345678-1234-1234-1234-1234567890ab" },
        { new Version(1, 2, 3), "1.2.3" },
    };

    [Fact]
    public async Task GaugeZeroDimension()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge("test_gauge", () => 123);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task GaugeZeroDimensionWithDescription()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge("test_gauge", () => 123, description: "Hello, world!");

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task GaugeZeroDimensionWithUnit()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge("test_gauge", () => 123, unit: "seconds");

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task GaugeZeroDimensionWithDescriptionAndUnit()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge("test_gauge", () => 123, unit: "seconds", description: "Hello, world!");

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task GaugeOneDimension()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge(
            "test_gauge",
            () => new Measurement<long>(123, new KeyValuePair<string, object?>("tagKey", "tagValue")));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task GaugeBoolDimension()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge(
            "test_gauge",
            () => new Measurement<long>(123, new KeyValuePair<string, object?>("tagKey", true)));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task GaugeEmptyDimensionName()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge(
            "test_gauge",
            () => new Measurement<long>(123, new KeyValuePair<string, object?>(string.Empty, "tagValue")));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void WriteLabelKeyNullOrEmptyName(string? labelName)
    {
        var buffer = new byte[32];

        var cursor = TextFormatSerializer.WriteLabelKey(buffer, 0, labelName!);

        Assert.Equal("_", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteLabelKeyNormalizesPrometheusLabelNames()
    {
        var buffer = new byte[32];

        var cursor = TextFormatSerializer.WriteLabelKey(buffer, 0, "a_b:c");

        Assert.Equal("a_b_c", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteLabelKeyCollapsesPrometheusInvalidCharacters()
    {
        var buffer = new byte[32];

        var cursor = TextFormatSerializer.WriteLabelKey(buffer, 0, "a../b");

        Assert.Equal("a_b", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteLabelKeyPrefixesLeadingDigits()
    {
        var buffer = new byte[32];

        var cursor = TextFormatSerializer.WriteLabelKey(buffer, 0, "2foo");

        Assert.Equal("_2foo", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteLabelKeyNormalizesOpenMetricsLabelNames()
    {
        var buffer = new byte[32];

        var cursor = TextFormatSerializer.WriteLabelKey(buffer, 0, "a_b:c");

        Assert.Equal("a_b_c", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteLabelKeyCollapsesOpenMetricsInvalidCharacters()
    {
        var buffer = new byte[32];

        var cursor = TextFormatSerializer.WriteLabelKey(buffer, 0, "a../b");

        Assert.Equal("a_b", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData("dots", false, "library.mascot", "dotnetbot", "otel_scope_library_mascot", "otter")]
    [InlineData("dots", true, "library.mascot", "dotnetbot", "otel_scope_library_mascot", "otter")]
    [InlineData("plain", false, "z", "scope", "otel_scope_z", "point")]
    [InlineData("plain", true, "z", "scope", "otel_scope_z", "point")]
    public async Task WriteMetricConcatenatesPointTagsThatCollideWithScopeLabels(
        string snapshotName,
        bool useOpenMetrics,
        string scopeTagKey,
        string scopeTagValue,
        string pointTagKey,
        string pointTagValue)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(
            nameof(this.WriteMetricConcatenatesPointTagsThatCollideWithScopeLabels),
            "1.0.0",
            [new(scopeTagKey, scopeTagValue)],
            scope: null);
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateCounter<int>("test_counter").Add(
            1,
            new KeyValuePair<string, object?>(pointTagKey, pointTagValue));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UseParameters(snapshotName, useOpenMetrics);
    }

    [Fact]
    public void WriteMetricNameSanitizesNonAsciiCharacters()
    {
        var buffer = new byte[32];
        var metric = new PrometheusMetric("A\u010A", string.Empty, PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.PrometheusV1.WriteMetricName(buffer, 0, metric);

        Assert.Equal("A_", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteMetricNameSanitizesNonAsciiUnitCharacters()
    {
        var buffer = new byte[32];
        var metric = new PrometheusMetric("metric", "s\u010A", PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.PrometheusV1.WriteMetricName(buffer, 0, metric);

        Assert.Equal("metric_s", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public async Task GaugeDoubleSubnormal()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
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

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task SumDoubleInfinities()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateCounter<double>("test_counter");
        counter.Add(1.0E308);
        counter.Add(1.0E308);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public async Task SumLongSerializesBoundaryValues(long value)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using (var provider = Sdk.CreateMeterProviderBuilder()
                                 .AddMeter(meter.Name)
                                 .AddInMemoryExporter(metrics)
                                 .Build())
        {
            var counter = meter.CreateCounter<long>("test_counter");
            counter.Add(value);

            provider.ForceFlush();
        }

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UseParameters(value);
    }

    [Fact]
    public async Task SumNonMonotonicDouble()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateUpDownCounter<double>("test_updown_counter");
        counter.Add(10);
        counter.Add(-11);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramZeroDimension()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
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

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramOneDimension()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18, new KeyValuePair<string, object?>("x", "1"));
        histogram.Record(100, new KeyValuePair<string, object?>("x", "1"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramTwoDimensions()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18, new("x", "1"), new("y", "2"));
        histogram.Record(100, new("x", "1"), new("y", "2"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramInfinities()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18);
        histogram.Record(double.PositiveInfinity);
        histogram.Record(double.PositiveInfinity);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramNaN()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18);
        histogram.Record(double.PositiveInfinity);
        histogram.Record(double.NaN);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public void ExponentialHistogramIsIgnoredForNow()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView(instrument => new Base2ExponentialBucketHistogramConfiguration())
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18);
        histogram.Record(100);

        provider.ForceFlush();

        Assert.False(TextFormatSerializer.CanWriteMetric(metrics[0]));
    }

    [Fact]
    public async Task SumWithOpenMetricsFormat()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
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

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task CounterWithOpenMetricsFormatEmitsLatestExemplar()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("test_counter");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(
                counter.Name,
                new MetricStreamConfiguration
                {
                    TagKeys = ["keep"],
                    ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                })
            .AddInMemoryExporter(metrics)
            .Build();

        counter.Add(1, new("keep", "value"), new("filtered", "first"));

        WaitForNextExemplarTimestamp();

        using var activity = new Activity("test");
        activity.Start();
        counter.Add(2, new("keep", "value"), new("filtered", "second"), new("trace_id", "ignored-trace"), new("span_id", "ignored-span"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task CounterWithOpenMetricsFormatEmitsExemplarWithoutLabelsWhenOnlyReservedTagNamesAreFiltered()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("test_counter");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(
                counter.Name,
                new MetricStreamConfiguration
                {
                    TagKeys = ["keep"],
                    ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                })
            .AddInMemoryExporter(metrics)
            .Build();

        counter.Add(2, new("keep", "value"), new("trace_id", "ignored-trace"), new("span_id", "ignored-span"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task CounterWithOpenMetricsFormatFiltersSanitizedReservedExemplarTagNames()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("test_counter");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(
                counter.Name,
                new MetricStreamConfiguration
                {
                    TagKeys = ["keep"],
                    ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                })
            .AddInMemoryExporter(metrics)
            .Build();

        counter.Add(2, new("keep", "value"), new("trace.id", "ignored-trace"), new("span-id", "ignored-span"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task CounterWithOpenMetricsFormatDropsExemplarLabelsExceedingLimit()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();
        var droppedValue = new string('x', 80);

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("test_counter");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(
                counter.Name,
                new MetricStreamConfiguration
                {
                    TagKeys = ["keep"],
                    ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                })
            .AddInMemoryExporter(metrics)
            .Build();

        using var activity = new Activity("test");
        activity.Start();
        counter.Add(2, new("keep", "value"), new("short", "ok"), new("too.long", droppedValue));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public void TryGetLatestExemplarPrefersLaterCandidateWhenTimestampsMatch()
    {
        var timestamp = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.True(OpenMetricsSerializer.ShouldPreferExemplar(timestamp, timestamp));
        Assert.False(OpenMetricsSerializer.ShouldPreferExemplar(timestamp, timestamp.AddTicks(-1)));
    }

    [Fact]
    public async Task HistogramOneDimensionWithOpenMetricsFormat()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
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

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramWithOpenMetricsFormatEmitsLatestBucketExemplar()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var histogram = meter.CreateHistogram<double>("test_histogram");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(
                histogram.Name,
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [5, 10],
                    TagKeys = ["keep"],
                    ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                })
            .AddInMemoryExporter(metrics)
            .Build();

        histogram.Record(4, new("keep", "value"), new("filtered", "older"));
        histogram.Record(8, new("keep", "value"), new("filtered", "first"));

        WaitForNextExemplarTimestamp();

        using var activity = new Activity("test");
        activity.Start();
        histogram.Record(9, new("keep", "value"), new("filtered", "latest"), new("trace_id", "ignored-trace"), new("span_id", "ignored-span"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramWithOpenMetricsFormatDropsCollidingLeLabelKeys()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18, new KeyValuePair<string, object?>("le", "user-value"), new KeyValuePair<string, object?>("x", "1"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramWithOpenMetricsFormatFiltersSanitizedReservedExemplarTagNames()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var histogram = meter.CreateHistogram<double>("test_histogram");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(
                histogram.Name,
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [5, 10],
                    TagKeys = ["keep"],
                    ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                })
            .AddInMemoryExporter(metrics)
            .Build();

        histogram.Record(9, new("keep", "value"), new("trace.id", "ignored-trace"), new("span-id", "ignored-span"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public void TryGetLatestHistogramBucketExemplarMatchesNegativeInfinityInFirstBucket()
    {
        Assert.True(OpenMetricsSerializer.IsHistogramBucketExemplarMatch(double.NegativeInfinity, double.NegativeInfinity, 5));
        Assert.False(OpenMetricsSerializer.IsHistogramBucketExemplarMatch(double.NegativeInfinity, 5, 10));
    }

    [Fact]
    public async Task WriteMetricPrefixesScopeAttributesAndDropsConflictingScopeAttributeNames()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

#if NET
        using var meter = new Meter(
            new MeterOptions("test_meter")
            {
                Version = "1.0.0",
                TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.0.0",
                Tags =
                [
                    new("library.mascot", "dotnetbot"),
                    new("name", "ignored-name"),
                    new("version", "ignored-version"),
                    new("schema_url", "ignored-schema"),
                ],
            });
#else
        using var meter = new Meter(
            name: "test_meter",
            version: "1.0.0",
            tags:
            [
                new("library.mascot", "dotnetbot"),
                new("name", "ignored-name"),
                new("version", "ignored-version"),
                new("schema_url", "ignored-schema"),
            ]);
#endif

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge<long>(
            "test_gauge",
            () => [new Measurement<long>(123, new KeyValuePair<string, object?>("metric_tag", "value"))]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UniqueForTargetFrameworkAndVersion();
    }

#if NET
    [Fact]
    public async Task WriteMetricDropsScopeAttributesWhoseNormalizedNamesConflictWithGeneratedScopeLabels()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(
            new MeterOptions("test_meter")
            {
                Version = "1.0.0",
                TelemetrySchemaUrl = "https://opentelemetry.io/schemas/1.0.0",
                Tags =
                [
                    new("library.mascot", "dotnetbot"),
                    new("schema-url", "ignored-schema"),
                ],
            });

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge<long>(
            "test_gauge",
            () => [new Measurement<long>(123, new KeyValuePair<string, object?>("metric_tag", "value"))]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task WriteMetricDropsScopeAttributesWhoseNormalizedNamesConflictWithGeneratedScopeNameAndVersionLabels()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(
            new MeterOptions("test_meter")
            {
                Version = "1.0.0",
                Tags =
                [
                    new("na-me", "ignored-name"),
                    new("ver-sion", "ignored-version"),
                    new("library.mascot", "dotnetbot"),
                ],
            });

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge<long>(
            "test_gauge",
            () => [new Measurement<long>(123, new KeyValuePair<string, object?>("metric_tag", "value"))]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

#endif

    [Fact]
    public async Task SumWithScopeVersion()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();
        using var meter = new Meter(nameof(this.SumWithScopeVersion), "1.0.0");
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

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramOneDimensionWithScopeVersion()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(nameof(this.HistogramOneDimensionWithScopeVersion), "1.0.0");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18, new KeyValuePair<string, object?>("x", "1"));
        histogram.Record(100, new KeyValuePair<string, object?>("x", "1"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task WriteMetricConcatenatesCollidingSanitizedLabelValuesInLexicographicOrder()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("test_meter");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge<long>(
            "test_gauge",
            () =>
            [
                new Measurement<long>(
                    123,
                    new("foo.bar", "dot"),
                    new("foo-bar", "hyphen"),
                    new("foo_bar", "underscore")),
            ]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task WriteMetricConcatenatesCollidingEmptyAndUnderscoreLabelKeys()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("test_meter");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge<long>(
            "test_gauge",
            () =>
            [
                new Measurement<long>(
                    123,
                    new(string.Empty, "empty"),
                    new("_", "underscore")),
            ]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task WriteMetricConcatenatesCollidingLeadingDigitLabelKeys()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("test_meter");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateObservableGauge<long>(
            "test_gauge",
            () =>
            [
                new Measurement<long>(
                    123,
                    new("1foo", "digit"),
                    new("_1foo", "underscore")),
            ]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramWithNegativeBucketBoundsOmitsSumAndCountWithOpenMetricsFormat()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView(instrument => new ExplicitBucketHistogramConfiguration { Boundaries = [-1, 0, 1] })
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(-0.5, new KeyValuePair<string, object?>("x", "1"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task HistogramWithLargeTagValueSerializesUsingGrownBuffer()
    {
        // A tag value larger than the SerializeTags initial 128-byte scratch buffer must
        // still serialize correctly by growing that buffer (now bounded by the size cap).
        var largeTagValue = new string('a', 4096);

        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView(instrument => new ExplicitBucketHistogramConfiguration { Boundaries = [1, 2] })
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(1.5, new KeyValuePair<string, object?>("key", largeTagValue));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public void SerializeTagsBufferGrowthIsCappedToPreventUnboundedAllocation()
    {
        // Grows by doubling while under the cap.
        Assert.Equal(256, TextFormatSerializer.GetNextSerializedTagsBufferSize(128));

        // Growth up to exactly the maximum is permitted.
        Assert.Equal(
            TextFormatSerializer.MaxSerializedTagsBufferSize,
            TextFormatSerializer.GetNextSerializedTagsBufferSize(TextFormatSerializer.MaxSerializedTagsBufferSize / 2));

        // Growth beyond the maximum fails fast rather than allocating without bound.
        Assert.Throws<InvalidOperationException>(
            () => TextFormatSerializer.GetNextSerializedTagsBufferSize((TextFormatSerializer.MaxSerializedTagsBufferSize / 2) + 1));
    }

    [Fact]
    public void WriteAsciiStringNoEscapeWritesAsciiBytes()
    {
        var value = "metric_name_total";
        var buffer = new byte[64];

        var cursor = TextFormatSerializer.WriteAsciiStringNoEscape(buffer, 0, value);

        Assert.Equal("metric_name_total", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void UntypedMetricUsesUnknownTypeForOpenMetrics()
    {
        var buffer = new byte[64];
        var metric = new PrometheusMetric("test_metric", string.Empty, PrometheusType.Untyped, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.OpenMetricsV1.WriteTypeMetadata(buffer, 0, metric);

        Assert.Equal("# TYPE test_metric unknown\n", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void UntypedMetricUsesUntypedTypeForPrometheusTextFormat()
    {
        var buffer = new byte[64];
        var metric = new PrometheusMetric("test_metric", string.Empty, PrometheusType.Untyped, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.PrometheusV1.WriteTypeMetadata(buffer, 0, metric);

        Assert.Equal("# TYPE test_metric untyped\n", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteAsciiStringNoEscapeThrowsExceptionWhenBufferTooSmall()
    {
        var buffer = new byte[4];

        Assert.Throws<IndexOutOfRangeException>(() => TextFormatSerializer.WriteAsciiStringNoEscape(buffer, 0, "metric"));
    }

    [Fact]
    public void WriteLabelValueEscapesSpecialCharacters()
    {
        var buffer = new byte[128];

        var cursor = TextFormatSerializer.WriteLabelValue(buffer, 0, "\"line1\\\nline2\"");

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
             + $"test_gauge{{otel_scope_name='test_meter',otel_scope_meter_tag='{expectedTagValue}'}} 123\n").Replace('\'', '"'),
            output);
    }

    [Fact]
    public void WriteMetricSerializesCollidingStaticMeterTagValuesUsingInvariantFormatting()
    {
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            var output = WriteGaugeMetricWithMeterTags(
                new("meter tag", 1.23m),
                new("meter_tag", 4.56m));

            Assert.Equal(
                "# TYPE test_gauge gauge\n"
                 + "test_gauge{otel_scope_name=\"test_meter\",otel_scope_meter_tag=\"1.23;4.56\"} 123\n",
                output);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void WriteLabelFormatsTypedValues()
    {
        var buffer = new byte[128];

        var cursor = TextFormatSerializer.WriteLabel(buffer, 0, "value", 18446744073709551615UL);

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

        var cursor = TextFormatSerializer.WriteLong(buffer, 0, value);

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

        var cursor = TextFormatSerializer.WriteDouble(buffer, 0, value);

        Assert.Equal(expected, Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteDoubleFormatsNaN()
    {
        var buffer = new byte[64];

        var cursor = TextFormatSerializer.WriteDouble(buffer, 0, double.NaN);

        Assert.Equal("NaN", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CounterExportsCreatedMetric(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("test_meter");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateCounter<double>("test_counter");
        counter.Add(1, [new("key", "value1")]);
        counter.Add(2, [new("key", "value2")]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UseParameters(useOpenMetrics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistogramExportsCreatedMetric(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("test_meter");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(1, [new("key", "value1")]);
        histogram.Record(2, [new("key", "value2")]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UseParameters(useOpenMetrics);
    }

    [Fact]
    public async Task HistogramCreatedMetricSkipsReservedHistogramLabels()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter("test_meter");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(1, [new("key", "value1"), new("le", "reserved")]);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteTargetInfoSkipsEmptyNonSingletonResource(bool openMetricsRequested)
    {
        var buffer = new byte[128];
        var resource = Resource.Empty.Merge(Resource.Empty);

        var cursor = (openMetricsRequested ? (TextFormatSerializer)TextFormatSerializer.OpenMetricsV1 : TextFormatSerializer.PrometheusV1).WriteTargetInfo(buffer, 0, resource);

        Assert.Equal(0, cursor);
    }

    [Theory]
    [InlineData(double.NegativeInfinity, "-Inf")]
    [InlineData(-1e10d, "-1e+10")]
    [InlineData(-1e-10d, "-1e-10")]
    [InlineData(0d, "0.0")]
    [InlineData(0.001d, "0.001")]
    [InlineData(0.002d, "0.002")]
    [InlineData(0.01d, "0.01")]
    [InlineData(0.1d, "0.1")]
    [InlineData(0.9d, "0.9")]
    [InlineData(0.95d, "0.95")]
    [InlineData(0.99d, "0.99")]
    [InlineData(0.999d, "0.999")]
    [InlineData(1d, "1.0")]
    [InlineData(1.7d, "1.7")]
    [InlineData(10d, "10.0")]
    [InlineData(1e-10d, "1e-10")]
    [InlineData(1e-09d, "1e-09")]
    [InlineData(1e-05d, "1e-05")]
    [InlineData(0.0001d, "0.0001")]
    [InlineData(100000d, "100000.0")]
    [InlineData(1e6d, "1e+06")]
    [InlineData(1e10d, "1e+10")]
    [InlineData(double.PositiveInfinity, "+Inf")]
    public void WriteCanonicalLabelValueUsesOpenMetricsCanonicalNumbers(double value, string expected)
    {
        var buffer = new byte[64];

        var cursor = TextFormatSerializer.WriteCanonicalLabelValue(buffer, 0, value);

        Assert.Equal(expected, Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteCanonicalLabelValueFormatsNaN()
    {
        var buffer = new byte[64];

        var cursor = TextFormatSerializer.WriteCanonicalLabelValue(buffer, 0, double.NaN);

        Assert.Equal("NaN", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

#if NET
    [Theory]
    [InlineData(double.PositiveInfinity, 3)]
    [InlineData(0d, 2)]
    [InlineData(1e6d, 4)]
    public void WriteCanonicalLabelValueThrowsArgumentExceptionWhenBufferTooSmall(double value, int bufferLength)
    {
        var buffer = new byte[bufferLength];

        var exception = Assert.Throws<ArgumentException>(() => TextFormatSerializer.WriteCanonicalLabelValue(buffer, 0, value));

        Assert.Equal("Destination buffer too small.", exception.Message);
    }
#endif

    [Theory]
    [InlineData(0.00011d, "0.00011")]
    [InlineData(1e11d, "1.00000000000000000e+011")]
    [InlineData(1234567.89d, "1.23456788999999990e+006")]
    public void WriteCanonicalLabelValueUsesBuiltInFormattingForNonCanonicalNumbers(double value, string expected)
    {
        var buffer = new byte[64];

        var cursor = TextFormatSerializer.WriteCanonicalLabelValue(buffer, 0, value);

        Assert.Equal(expected, Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteCanonicalLabelValueDoesNotRoundNearPowersOfTen()
    {
        const double ExactPowerOfTen = 1e6d;
        const double NearPowerOfTen = 1000000.0000005d;

        var exact = WriteCanonicalLabelValueToString(ExactPowerOfTen);
        var near = WriteCanonicalLabelValueToString(NearPowerOfTen);

        Assert.Equal("1e+06", exact);
        Assert.Equal(NearPowerOfTen.ToString("e17", CultureInfo.InvariantCulture), near);
        Assert.NotEqual(exact, near);

        static string WriteCanonicalLabelValueToString(double value)
        {
            var buffer = new byte[64];

            var cursor = TextFormatSerializer.WriteCanonicalLabelValue(buffer, 0, value);

            return Encoding.UTF8.GetString(buffer, 0, cursor);
        }
    }

    [Fact]
    public void WriteUnicodeStringEncodesSurrogatePairsAsUtf8ScalarValues()
    {
        const string value = "rocket:\uD83D\uDE80";
        var buffer = new byte[128];

        var cursor = TextFormatSerializer.WriteUnicodeString(buffer, 0, value);
        var actual = ToHexString(buffer, cursor);
        var expected = ToHexString(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetByteCount(value));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WriteUnicodeStringReplacesInvalidSurrogates()
    {
        const string value = "rocket:\uD83D";
        var buffer = new byte[128];

        var cursor = TextFormatSerializer.WriteUnicodeString(buffer, 0, value);
        var actual = ToHexString(buffer, cursor);
        var expected = ToHexString(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetByteCount(value));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WriteUnicodeStringReplacesLoneLowSurrogate()
    {
        const string value = "rocket:\uDE80";
        var buffer = new byte[128];

        var cursor = TextFormatSerializer.WriteUnicodeString(buffer, 0, value);
        var actual = ToHexString(buffer, cursor);
        var expected = ToHexString(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetByteCount(value));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetSerializerThrowsForUnsupportedProtocolVersion(bool isOpenMetrics)
    {
        var protocol = new PrometheusProtocol(
            isOpenMetrics ? PrometheusProtocol.OpenMetricsMediaType : PrometheusProtocol.PrometheusTextMediaType,
            escaping: null,
            version: new Version(2, 0, 0),
            isOpenMetrics: isOpenMetrics);

        Assert.Throws<NotSupportedException>(() => TextFormatSerializer.GetSerializer(protocol));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteTargetInfoSkipsSingletonEmptyResource(bool openMetricsRequested)
    {
        var buffer = new byte[128];
        var serializer = openMetricsRequested ? (TextFormatSerializer)TextFormatSerializer.OpenMetricsV1 : TextFormatSerializer.PrometheusV1;

        var cursor = serializer.WriteTargetInfo(buffer, 0, Resource.Empty);

        Assert.Equal(0, cursor);
    }

    [Theory]
#pragma warning disable xUnit1045 // Avoid using TheoryData type arguments that might not be serializable
    [MemberData(nameof(LabelValueBoundaryCases))]
#pragma warning restore xUnit1045 // Avoid using TheoryData type arguments that might not be serializable
    public void WriteLabelValueFormatsTypedValues(object? labelValue, string expectedValue)
    {
        var buffer = new byte[128];

        var cursor = TextFormatSerializer.WriteLabelValue(buffer, 0, labelValue);

        Assert.Equal(expectedValue, Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void MapMetricTypeReturnsExpectedTypeName()
    {
        var serializer = TextFormatSerializer.PrometheusV1;

        Assert.Equal("gauge", serializer.MapMetricType(PrometheusType.Gauge));
        Assert.Equal("counter", serializer.MapMetricType(PrometheusType.Counter));
        Assert.Equal("summary", serializer.MapMetricType(PrometheusType.Summary));
        Assert.Equal("histogram", serializer.MapMetricType(PrometheusType.Histogram));
    }

    [Fact]
    public void WriteUnitMetadataWritesUnitOverrideThatDiffersFromMetricUnit()
    {
        var buffer = new byte[64];
        var metric = new PrometheusMetric("test", string.Empty, PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.PrometheusV1.WriteUnitMetadata(buffer, 0, metric, "seconds");

        Assert.Equal("# UNIT test seconds\n", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteSerializedTagValuesThrowsWhenBufferTooSmall()
    {
        var buffer = new byte[2];
        var serializedTags = "abc"u8.ToArray();

        var exception = Assert.Throws<ArgumentException>(
            () => TextFormatSerializer.WriteSerializedTagValues(buffer, 0, serializedTags));

        Assert.Equal("buffer", exception.ParamName);
    }

    [Fact]
    public void WriteMetricNameThrowsWhenBufferTooSmall()
    {
        var buffer = new byte[2];
        var metric = new PrometheusMetric("test_metric", string.Empty, PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var exception = Assert.Throws<ArgumentException>(
            () => TextFormatSerializer.PrometheusV1.WriteMetricName(buffer, 0, metric));

        Assert.Equal("buffer", exception.ParamName);
    }

    [Fact]
    public void IsHistogramBucketExemplarMatchReturnsFalseForNaN()
        => Assert.False(OpenMetricsSerializer.IsHistogramBucketExemplarMatch(double.NaN, double.NegativeInfinity, double.PositiveInfinity));

    [Fact]
    public void ExemplarLabelWithSurrogatePairCountsUnicodeCodePoints()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("test_counter");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(
                counter.Name,
                new MetricStreamConfiguration
                {
                    TagKeys = ["keep"],
                    ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                })
            .AddInMemoryExporter(metrics)
            .Build();

        counter.Add(1, new("keep", "value"), new("rocket", "rkt:🚀"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        Assert.Contains("rkt:🚀", output, StringComparison.Ordinal);
    }

#if NET
    [Fact]
    public async Task WriteHistogramMetricSerializesStaticTagsWithoutPreSerializedTags()
    {
        var buffer = new byte[85000];

        var metric = GetSingleHistogramMetric(
            meterName: "\u65e5\u672c",
            meterTags: [new(string.Empty, "meterTagValue")]);

        var prometheusMetric = new PrometheusMetric(metric.Name, metric.Unit, PrometheusType.Histogram, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.PrometheusV1.WriteMetric(
            buffer,
            0,
            metric,
            prometheusMetric,
            writeType: true,
            writeUnit: true,
            writeHelp: true,
            unitOverride: null,
            helpOverride: null);

        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
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

        static char GetHexValue(int value)
        {
            return (char)(value < 10 ? '0' + value : 'A' + (value - 10));
        }
    }

    private static int WriteMetric(byte[] buffer, int cursor, Metric metric, bool useOpenMetrics) =>
        (useOpenMetrics ? (TextFormatSerializer)TextFormatSerializer.OpenMetricsV1 : TextFormatSerializer.PrometheusV1)
        .WriteMetric(
            buffer,
            cursor,
            metric,
            PrometheusMetric.Create(metric, false),
            writeType: true,
            writeUnit: true,
            writeHelp: true,
            unitOverride: null,
            helpOverride: null);

    private static void WaitForNextExemplarTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow <= timestamp)
        {
            Thread.Sleep(1);
        }
    }

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

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: false);
        return Encoding.UTF8.GetString(buffer, 0, cursor);
    }

    private static Meter CreateMeter([CallerMemberName] string name = "") => new(name);

    private static VerifySettings CreateVerifySettings()
    {
        var snapshotsPath =
            typeof(PrometheusSerializerTests).Assembly
            .GetCustomAttributes()
            .OfType<AssemblyMetadataAttribute>()
            .FirstOrDefault((p) => p.Key is "PrometheusSerializerTestsSnapshotsPath")?.Value ?? "snapshots";

        var settings = new VerifySettings();

        settings.UseDirectory(snapshotsPath);

        // Shorten name to avoid PATH_TOO_LONG issues on Windows
        settings.UseTypeName("PrometheusSerializer");

        // Scrub unstable values from snapshots
        settings.ScrubLinesWithReplace((line) => CreatedMetric().Replace(line, "$1<TIMESTAMP>"));
        settings.ScrubLinesWithReplace((line) => ExemplarTimestamp().Replace(line, "$1<TIMESTAMP>"));
        settings.ScrubLinesWithReplace((line) => SpanOrTraceIds().Replace(line, "$1=\"<ID>\""));
        settings.ScrubLinesWithReplace((line) => SdkVersion().Replace(line, "telemetry_sdk_version=\"<VERSION>\""));

        return settings;
    }

#if NET
    [GeneratedRegex("(?m)^([^\\s]*_created(?:\\{[^}]*\\})?\\s+)\\S+$")]
    private static partial Regex CreatedMetric();

    [GeneratedRegex("(?m)^(.+?\\s#\\s\\{[^}]*\\}\\s+\\S+\\s+)\\S+$")]
    private static partial Regex ExemplarTimestamp();

    [GeneratedRegex("(?m)(trace_id|span_id)=\"[^\"]*\"")]
    private static partial Regex SpanOrTraceIds();

    [GeneratedRegex("telemetry_sdk_version=\"[^\"]*\"")]
    private static partial Regex SdkVersion();
#else
    private static Regex CreatedMetric() => new("(?m)^([^\\s]*_created(?:\\{[^}]*\\})?\\s+)\\S+$", RegexOptions.Compiled);

    private static Regex ExemplarTimestamp() => new("(?m)^(.+?\\s#\\s\\{[^}]*\\}\\s+\\S+\\s+)\\S+$", RegexOptions.Compiled);

    private static Regex SpanOrTraceIds() => new("(?m)(trace_id|span_id)=\"[^\"]*\"", RegexOptions.Compiled);

    private static Regex SdkVersion() => new("telemetry_sdk_version=\"[^\"]*\"", RegexOptions.Compiled);
#endif
}
