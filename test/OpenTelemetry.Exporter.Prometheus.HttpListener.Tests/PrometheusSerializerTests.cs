// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTelemetry.Exporter.Prometheus.Serialization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

[Collection(TestMeterCollection.Name)]
public sealed partial class PrometheusSerializerTests
{
    internal static readonly VerifySettings VerifySettings = CreateVerifySettings();
    private static readonly int[] LabelValueArrayCase = [1, 2, 3];
    private static readonly string[] LabelValueOversizedArrayCase = ["0123456789", "0123456789", "0123456789", "0123456789"];
    private static readonly string[] LabelValueBoundaryArrayCase = ["0123456789012345678901234567890123"];
    private static readonly byte[] LabelValueByteArrayCase = [1, 2, 3];

    public static TheoryData<string> EscapingSchemes =>
    [
        PrometheusProtocol.AllowUtf8Escaping,
        PrometheusProtocol.DotsEscaping,
        PrometheusProtocol.UnderscoresEscaping,
        PrometheusProtocol.ValuesEscaping,
    ];

    public static TheoryData<string, bool> EscapingSchemesAndFormats
    {
        get
        {
            var data = new TheoryData<string, bool>();

            foreach (var escaping in new[]
            {
                PrometheusProtocol.AllowUtf8Escaping,
                PrometheusProtocol.DotsEscaping,
                PrometheusProtocol.UnderscoresEscaping,
                PrometheusProtocol.ValuesEscaping,
            })
            {
                data.Add(escaping, false);
                data.Add(escaping, true);
            }

            return data;
        }
    }

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
        { float.MinValue, "-3.4028234663852886e+38" },
        { 0f, "0.0" },
        { float.NaN, "NaN" },
        { float.NegativeInfinity, "-Inf" },
        { float.PositiveInfinity, "+Inf" },
        { float.MaxValue, "3.4028234663852886e+38" },
        { double.MinValue, "-1.7976931348623157e+308" },
        { 0d, "0.0" },
        { double.NegativeInfinity, "-Inf" },
        { double.PositiveInfinity, "+Inf" },
        { double.NaN, "NaN" },
        { double.MaxValue, "1.7976931348623157e+308" },
        { decimal.MinValue, "-79228162514264337593543950335" },
        { 0m, "0" },
        { decimal.MaxValue, "79228162514264337593543950335" },
        { 1.5d, "1.5" },
        { new Guid("12345678-1234-1234-1234-1234567890ab"), "12345678-1234-1234-1234-1234567890ab" },
        { new Version(1, 2, 3), "1.2.3" },
        { new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc), "05/06/2024 07:08:09" },
        { new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(2)), "05/06/2024 07:08:09 +02:00" },
        { TimeSpan.FromTicks(1234567890), "00:02:03.4567890" },
        { LabelValueByteArrayCase, "AQID" },
        { LabelValueArrayCase, "[1,2,3]" },
        { new Dictionary<string, object?> { ["a"] = 1 }, """{\"a\":1}""" },
        { new Dictionary<string, object?> { ["b"] = LabelValueByteArrayCase }, """{\"b\":\"AQID\"}""" },
        {
            new Dictionary<string, object?>
            {
                ["flag"] = true,
                ["dbl"] = 1.5,
                ["nan"] = double.NaN,
                ["empty"] = null,
                ["bad"] = new UnsupportedValue(),
            },
            """{\"flag\":true,\"dbl\":1.5,\"nan\":\"NaN\",\"empty\":null}"""
        },
        { new Dictionary<string, string?> { ["a"] = "b" }, """{\"a\":\"b\"}""" },
        { Array.Empty<KeyValuePair<string, string?>>(), "{}" },
        { new KeyValuePair<string, string?>[] { new("a", "b") }, """{\"a\":\"b\"}""" },
        { new object[] { new KeyValuePair<string, string?>[] { new("a", "b") } }, """[{\"a\":\"b\"}]""" },
        { new Hashtable { ["a"] = 1 }, """{\"a\":1}""" },
        { new double[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1.5 }, """[\"NaN\",\"Infinity\",\"-Infinity\",1.5]""" },
        { new object[] { LabelValueArrayCase }, "[[1,2,3]]" },
        { new object[] { LabelValueByteArrayCase }, """[\"AQID\"]""" },
        { new object[] { new Dictionary<string, object?> { ["a"] = 1 } }, """[{\"a\":1}]""" },
        { new sbyte[] { -1, 0, 127 }, "[-1,0,127]" },
        { new object[] { new sbyte[] { -1, 0, 127 } }, "[[-1,0,127]]" },
        { new object[] { new KeyValuePair<string, object?>[] { new("a", 1) } }, """[{\"a\":1}]""" },
        { new List<KeyValuePair<string, object?>> { new("a", 1), new("A", 2), new("a", 3) }, """{\"a\":3,\"A\":2}""" },
        { new object[] { new List<KeyValuePair<string, object?>> { new("a", 1), new("A", 2), new("a", 3) } }, """[{\"a\":3,\"A\":2}]""" },
        { CreateReferenceKeyedDictionary(), """{\"a\":2}""" },
        { new object[] { CreateReferenceKeyedDictionary() }, """[{\"a\":2}]""" },
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
    public async Task CounterWithUnitAndSuffixesDisabled()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateCounter<long>("http.server.duration", unit: "s").Add(1);

        provider.ForceFlush();

        var cursor = WriteMetric(
            buffer,
            0,
            metrics[0],
            useOpenMetrics: true,
            suppressScopeInfo: true,
            appendSuffixes: false);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteMetricSuppressesScopeLabelsWhenPointTagsCollide(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(
            nameof(this.WriteMetricSuppressesScopeLabelsWhenPointTagsCollide),
            "1.0.0",
            [new("meter_tag", "scope")],
            scope: null);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateCounter<int>("test_counter").Add(
            1,
            new KeyValuePair<string, object?>("a.b", "value1"),
            new KeyValuePair<string, object?>("a/b", "value2"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics.Single(), useOpenMetrics, suppressScopeInfo: true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UseParameters(useOpenMetrics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteMetricDropsPointsWithTagKeysThatCollideOnceEscaped(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateCounter<int>("test_counter");
        counter.Add(1, new KeyValuePair<string, object?>("a.b", "value"));
        counter.Add(2, new KeyValuePair<string, object?>("a_b", "value"));
        counter.Add(4, new KeyValuePair<string, object?>("a_b", "other"));

        provider.ForceFlush();

        var series = new SeriesRegistry();
        var cursor = WriteMetric(buffer, 0, metrics.Single(), useOpenMetrics, series: series);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        // The first of the two points that are the same series is written, and the other is dropped.
        var samples = GetSampleLines(output, "test_counter_total");

        Assert.Equal(2, samples.Count);
        Assert.Single(samples, line => line.Contains("a_b=\"value\"", StringComparison.Ordinal));
        Assert.Single(samples, line => line.Contains("a_b=\"other\"", StringComparison.Ordinal));
        Assert.Equal(1, series.DroppedCount);
        Assert.Equal("test_counter", series.FirstDroppedMetricName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteMetricDropsSeriesOfMetricsFromDifferentMetersWhenScopeInfoIsSuppressed(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var firstMeter = new Meter(nameof(this.WriteMetricDropsSeriesOfMetricsFromDifferentMetersWhenScopeInfoIsSuppressed) + ".first");
        using var secondMeter = new Meter(nameof(this.WriteMetricDropsSeriesOfMetricsFromDifferentMetersWhenScopeInfoIsSuppressed) + ".second");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(firstMeter.Name)
            .AddMeter(secondMeter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        firstMeter.CreateCounter<int>("test_counter").Add(1, new KeyValuePair<string, object?>("key", "value"));
        secondMeter.CreateCounter<int>("test_counter").Add(2, new KeyValuePair<string, object?>("key", "value"));
        secondMeter.CreateCounter<int>("test_counter").Add(3, new KeyValuePair<string, object?>("key", "unique"));

        provider.ForceFlush();

        var series = new SeriesRegistry();
        var cursor = 0;

        foreach (var metric in metrics.OrderBy(m => m.MeterName, StringComparer.Ordinal))
        {
            cursor = WriteMetric(buffer, cursor, metric, useOpenMetrics, suppressScopeInfo: true, series: series);
        }

        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        Assert.Equal(2, CountSamples(output, "test_counter_total"));
        Assert.Contains("key=\"unique\"", output, StringComparison.Ordinal);
        Assert.Equal(1, series.DroppedCount);

        // Without the registry, the series is written twice.
        cursor = 0;

        foreach (var metric in metrics.OrderBy(m => m.MeterName, StringComparer.Ordinal))
        {
            cursor = WriteMetric(buffer, cursor, metric, useOpenMetrics, suppressScopeInfo: true);
        }

        Assert.Equal(3, CountSamples(Encoding.UTF8.GetString(buffer, 0, cursor), "test_counter_total"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteMetricDropsHistogramsWithTheSameSeries(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var firstMeter = new Meter(nameof(this.WriteMetricDropsHistogramsWithTheSameSeries) + ".first");
        using var secondMeter = new Meter(nameof(this.WriteMetricDropsHistogramsWithTheSameSeries) + ".second");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(firstMeter.Name)
            .AddMeter(secondMeter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        firstMeter.CreateHistogram<double>("test_histogram").Record(10, new KeyValuePair<string, object?>("key", "value"));
        secondMeter.CreateHistogram<double>("test_histogram").Record(20, new KeyValuePair<string, object?>("key", "value"));
        secondMeter.CreateHistogram<double>("test_histogram").Record(30, new KeyValuePair<string, object?>("key", "unique"));

        provider.ForceFlush();

        var series = new SeriesRegistry();
        var cursor = 0;

        foreach (var metric in metrics.OrderBy(m => m.MeterName, StringComparer.Ordinal))
        {
            cursor = WriteMetric(buffer, cursor, metric, useOpenMetrics, suppressScopeInfo: true, series: series);
        }

        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        Assert.Equal(2, CountSamples(output, "test_histogram_count"));
        Assert.Equal(2, CountSamples(output, "test_histogram_sum"));
        Assert.Equal(1, series.DroppedCount);
        Assert.Contains("test_histogram_sum{key=\"value\"} 10", output, StringComparison.Ordinal);
        Assert.DoesNotContain("test_histogram_sum{key=\"value\"} 20", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteMetricDropsPointsWithTheSameLabelsInADifferentOrderOnceEscaped(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        // The tags are sorted by their key, so once the keys are escaped the labels of the two points are in a different order.
        var counter = meter.CreateCounter<int>("test_counter");
        counter.Add(1, new KeyValuePair<string, object?>("a.b", "1"), new KeyValuePair<string, object?>("aZ", "2"));
        counter.Add(2, new KeyValuePair<string, object?>("a_b", "1"), new KeyValuePair<string, object?>("aZ", "2"));

        provider.ForceFlush();

        var series = new SeriesRegistry();
        var cursor = WriteMetric(buffer, 0, metrics.Single(), useOpenMetrics, suppressScopeInfo: true, series: series);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        Assert.Single(GetSampleLines(output, "test_counter_total"));
        Assert.Equal(1, series.DroppedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteMetricDropsHistogramsWithoutBucketsWithTheSameSeries(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("test_histogram", new ExplicitBucketHistogramConfiguration { Boundaries = [] })
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(1, new KeyValuePair<string, object?>("a.b", "value"));
        histogram.Record(2, new KeyValuePair<string, object?>("a_b", "value"));
        histogram.Record(3, new KeyValuePair<string, object?>("a_b", "other"));

        provider.ForceFlush();

        var series = new SeriesRegistry();
        var cursor = WriteMetric(buffer, 0, metrics.Single(), useOpenMetrics, suppressScopeInfo: true, series: series);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        // Without any buckets, the series of the histogram are those of its sum and count.
        Assert.Empty(GetSampleLines(output, "test_histogram_bucket"));
        Assert.Equal(2, GetSampleLines(output, "test_histogram_sum").Count);
        Assert.Equal(2, GetSampleLines(output, "test_histogram_count").Count);
        Assert.Single(GetSampleLines(output, "test_histogram_sum"), line => line.Contains("a_b=\"value\"", StringComparison.Ordinal));
        Assert.Equal(1, series.DroppedCount);
    }

    [Theory]
    [InlineData(new[] { "a.b", "a_b" }, true)]
    [InlineData(new[] { "a.b", "aZ", "a_b" }, true)]
    [InlineData(new[] { "a b", "a_b" }, true)]
    [InlineData(new[] { "a.b", "a.c" }, false)]
    [InlineData(new[] { "a_b", "c_d" }, false)]
    [InlineData(new[] { "a.b" }, false)]
    public void HasCollidingTagKeysIsTrueWhenKeysAreTheSameOnceEscaped(string[] keys, bool expected)
    {
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateCounter<int>("test_counter").Add(1);

        provider.ForceFlush();

        var tracker = new TagKeyTracker();
        var index = 0;

        foreach (var key in keys ?? [])
        {
            tracker.Observe(index++, key, 1);
        }

        Assert.Equal(expected, TextFormatSerializer.PrometheusV1.HasCollidingTagKeys(tracker, metrics.Single()));
    }

    [Fact]
    public void HasCollidingTagKeysIsTrueForTheKeyOfAHistogramWhichIsNotWritten()
    {
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateHistogram<double>("test_histogram").Record(1);
        meter.CreateCounter<int>("test_counter").Add(1);

        provider.ForceFlush();

        var tracker = new TagKeyTracker();

        tracker.Observe(0, "le", "5000");

        // The label of a bucket is not written for a tag that has its name, so a point with the tag has the same series as one without it.
        Assert.True(TextFormatSerializer.PrometheusV1.HasCollidingTagKeys(tracker, metrics.Single(m => m.Name == "test_histogram")));
        Assert.False(TextFormatSerializer.PrometheusV1.HasCollidingTagKeys(tracker, metrics.Single(m => m.Name == "test_counter")));
    }

    [Fact]
    public void TagKeyTrackerObservesTheDistinctKeysOfTheTagsOfPoints()
    {
        var tracker = new TagKeyTracker();
        var first = new string("key".ToCharArray());
        var second = new string("key".ToCharArray());

        tracker.Observe(0, first, 1);
        tracker.Observe(0, first, 2);
        tracker.Observe(1, "other", "value");
        tracker.Observe(0, second, 3);

        Assert.Equal(2, tracker.Keys.Count);
        Assert.False(tracker.HasAmbiguousValues);

        // The same key with values of different types is written as the same label
        tracker.Observe(0, first, "1");

        Assert.True(tracker.HasAmbiguousValues);

        tracker.Reset();

        Assert.Empty(tracker.Keys);
        Assert.False(tracker.HasAmbiguousValues);

        // A label without a value is the same as one that does not exist
        tracker.Observe(0, first, string.Empty);

        Assert.True(tracker.HasAmbiguousValues);

        tracker.Reset();

        // Different arrays are written as the same label value if they have the same items
        int[] items = [1];
        tracker.Observe(0, first, items);

        Assert.True(tracker.HasAmbiguousValues);
    }

    [Fact]
    public void SeriesRegistryForgetsTheSeriesWhenAFamilyStarts()
    {
        var registry = new SeriesRegistry();
        var buffer = Encoding.UTF8.GetBytes("a{x=\"1\"}");

        Assert.True(registry.TryAdd(buffer, 0, buffer.Length));
        Assert.False(registry.TryAdd(buffer, 0, buffer.Length));

        registry.RecordDropped("a");
        registry.StartFamily();

        Assert.Equal(0, registry.Count);
        Assert.True(registry.TryAdd(buffer, 0, buffer.Length));

        // What was dropped is only forgotten when the response is
        Assert.Equal(1, registry.DroppedCount);

        registry.Clear();

        Assert.Equal(0, registry.DroppedCount);
        Assert.Null(registry.FirstDroppedMetricName);
    }

    [Fact]
    public void SeriesRegistryDoesNotDependOnTheBufferItsSeriesWereAddedFrom()
    {
        var registry = new SeriesRegistry();
        var buffer = Encoding.UTF8.GetBytes("a_sum{x=\"1\"} 1\n");

        Assert.True(registry.TryAdd(buffer, 0, 12));

        // The buffer is overwritten, as the buffer of a response is when the series that it contained is not written.
        Array.Clear(buffer, 0, buffer.Length);

        var other = Encoding.UTF8.GetBytes("a_sum{x=\"1\"} 2\n");

        Assert.False(registry.TryAdd(other, 0, 12));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void SeriesRegistryForgetsTheIdentitiesOfSeriesWhichAreRolledBackWhenTheyAreLongerThanTheRest()
    {
        var registry = new SeriesRegistry();

        var first = Encoding.UTF8.GetBytes("a{x=\"1\"}");
        var second = Encoding.UTF8.GetBytes("b{y=\"" + new string('v', 10_000) + "\"}");
        var third = Encoding.UTF8.GetBytes("c{z=\"3\"}");

        Assert.True(registry.TryAdd(first, 0, first.Length));

        var mark = registry.Count;

        Assert.True(registry.TryAdd(second, 0, second.Length));
        Assert.True(registry.TryAdd(third, 0, third.Length));

        registry.Rollback(mark);

        Assert.False(registry.TryAdd(first, 0, first.Length));
        Assert.True(registry.TryAdd(third, 0, third.Length));
        Assert.True(registry.TryAdd(second, 0, second.Length));
        Assert.False(registry.TryAdd(second, 0, second.Length));
    }

    [Fact]
    public void SeriesRegistryDropsSeriesThatHaveAlreadyBeenAdded()
    {
        var buffer = Encoding.UTF8.GetBytes("a{x=\"1\"}b{x=\"1\"}a{x=\"1\"}a{x=\"2\"}");
        var registry = new SeriesRegistry();

        Assert.True(registry.TryAdd(buffer, 0, 8));
        Assert.True(registry.TryAdd(buffer, 8, 8));
        Assert.False(registry.TryAdd(buffer, 16, 8));
        Assert.True(registry.TryAdd(buffer, 24, 8));
        Assert.Equal(3, registry.Count);
    }

    [Fact]
    public void SeriesRegistryForgetsSeriesWhenRolledBack()
    {
        var buffer = Encoding.UTF8.GetBytes("a{x=\"1\"}b{x=\"1\"}c{x=\"1\"}");
        var registry = new SeriesRegistry();

        Assert.True(registry.TryAdd(buffer, 0, 8));

        var mark = registry.Count;

        Assert.True(registry.TryAdd(buffer, 8, 8));
        Assert.True(registry.TryAdd(buffer, 16, 8));

        registry.Rollback(mark);

        Assert.Equal(1, registry.Count);
        Assert.False(registry.TryAdd(buffer, 0, 8));
        Assert.True(registry.TryAdd(buffer, 8, 8));
        Assert.True(registry.TryAdd(buffer, 16, 8));
        Assert.False(registry.TryAdd(buffer, 8, 8));
    }

    [Fact]
    public void SeriesRegistryDetectsDuplicatesAfterItHasGrown()
    {
        var series = Enumerable.Range(0, 5_000).Select(i => "series_" + i.ToString(CultureInfo.InvariantCulture) + "{}").ToList();
        var buffer = Encoding.UTF8.GetBytes(string.Concat(series));
        var registry = new SeriesRegistry();

        var offsets = new List<(int Start, int Length)>();
        var start = 0;

        foreach (var entry in series)
        {
            offsets.Add((start, entry.Length));
            Assert.True(registry.TryAdd(buffer, start, entry.Length));
            start += entry.Length;
        }

        Assert.Equal(series.Count, registry.Count);

        foreach (var (offset, length) in offsets)
        {
            Assert.False(registry.TryAdd(buffer, offset, length));
        }

        registry.Rollback(2_500);

        Assert.Equal(2_500, registry.Count);
        Assert.False(registry.TryAdd(buffer, offsets[0].Start, offsets[0].Length));
        Assert.True(registry.TryAdd(buffer, offsets[4_999].Start, offsets[4_999].Length));

        registry.Clear();

        Assert.Equal(0, registry.Count);
        Assert.True(registry.TryAdd(buffer, offsets[0].Start, offsets[0].Length));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteMetricWithResourceConstantLabels(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(nameof(this.WriteMetricWithResourceConstantLabels), "1.0.0");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateCounter<int>("test_counter");
        counter.Add(1, new KeyValuePair<string, object?>("x", "1"));
        counter.Add(2, new KeyValuePair<string, object?>("x", "2"), new KeyValuePair<string, object?>("service_name", "point"));
        counter.Add(3, new KeyValuePair<string, object?>("otel_scope_name", "point"));

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(18, new KeyValuePair<string, object?>("x", "1"));
        histogram.Record(100, new KeyValuePair<string, object?>("service_name", "point"));

        provider.ForceFlush();

        IReadOnlyList<KeyValuePair<string, object>> resourceConstantLabels =
        [
            new("service.name", "svc"),
            new("le", "reserved"),
            new("host.name", "host-1"),
            new("retries", 3),
        ];

        var output = new StringBuilder();

        foreach (var metric in metrics.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var cursor = WriteMetric(buffer, 0, metric, useOpenMetrics, resourceConstantLabels: resourceConstantLabels);
            output.Append(Encoding.UTF8.GetString(buffer, 0, cursor));
        }

        await Verify(output.ToString(), "txt", VerifySettings).UseParameters(useOpenMetrics);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistogramWithFractionalBoundaries(bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("test_histogram", new ExplicitBucketHistogramConfiguration
            {
                // The bucket boundaries used by the ASP.NET Core duration histograms,
                // none of which are exactly representable as a double.
                Boundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10],
            })
            .AddInMemoryExporter(metrics)
            .Build();

        var histogram = meter.CreateHistogram<double>("test_histogram", unit: "s");
        histogram.Record(0.0111996);

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UseParameters(useOpenMetrics);
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
        using (CultureSwitcher.UseCulture("fr-FR"))
        {
            var output = WriteGaugeMetricWithMeterTags(
                new("meter tag", 1.23m),
                new("meter_tag", 4.56m));

            Assert.Equal(
                "# TYPE test_gauge gauge\n"
                 + "test_gauge{otel_scope_name=\"test_meter\",otel_scope_meter_tag=\"1.23;4.56\"} 123\n",
                output);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WriteMetricSupportsScopeInfoEnabledOptionForCounters(bool useOpenMetrics, bool suppressScopeInfo)
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

        var histogram = meter.CreateHistogram<double>("test_histogram");
        histogram.Record(1, [new("key", "value1")]);
        histogram.Record(2, [new("key", "value2")]);

        provider.ForceFlush();

        var counterMetric = metrics.Single(m => m.Name == "test_counter");

        var cursor = WriteMetric(buffer, 0, counterMetric, useOpenMetrics, suppressScopeInfo);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UseParameters(useOpenMetrics, suppressScopeInfo);
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
    [InlineData(-0d, "-0")]
    [InlineData(1234.5, "1234.5")]
    [InlineData(double.PositiveInfinity, "+Inf")]
    //// Values are written using their shortest round-trippable representation, not padded out to
    //// the 17 significant digits that are only needed to round-trip the least tractable values.
    [InlineData(0.005d, "0.005")]
    [InlineData(0.075d, "0.075")]
    [InlineData(0.0111996d, "0.0111996")]
    [InlineData(1e-07d, "1E-07")]
    [InlineData(0.1d + 0.2d, "0.30000000000000004")]
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

    [Fact]
    public void WriteDoubleDoesNotLosePrecision()
    {
        // Values whose shortest representation needs 16 or 17 significant digits, which the
        // default formatting on .NET Framework would silently round to 15 digits.
        double[] values =
        [
            double.Epsilon,
            double.MinValue,
            double.MaxValue,
            0.1d + 0.2d,
            1d / 3d,
            Math.PI,
            2.2250738585072014e-308,
            7.793112545177861,
            -6.186496929997836e+44,
        ];

        var buffer = new byte[64];

        foreach (var value in values)
        {
            var cursor = TextFormatSerializer.WriteDouble(buffer, 0, value);
            var written = Encoding.UTF8.GetString(buffer, 0, cursor);

            Assert.True(
                double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed),
                $"'{written}' could not be parsed.");

            Assert.Equal(value, parsed);
        }
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
    //// The bucket boundaries used by the HTTP metric duration histograms
    [InlineData(0.005d, "0.005")]
    [InlineData(0.025d, "0.025")]
    [InlineData(0.05d, "0.05")]
    [InlineData(0.075d, "0.075")]
    [InlineData(0.25d, "0.25")]
    [InlineData(0.5d, "0.5")]
    [InlineData(0.75d, "0.75")]
    [InlineData(2.5d, "2.5")]
    [InlineData(7.5d, "7.5")]
    //// Values that are neither a power of ten nor exactly representable, whose
    //// shortest round-trippable representation has more than three decimal places.
    [InlineData(0.0025d, "0.0025")]
    [InlineData(0.0075d, "0.0075")]
    [InlineData(0.00025d, "0.00025")]
    [InlineData(1.5e-05d, "1.5e-05")]
    [InlineData(33.333d, "33.333")]
    [InlineData(12.5d, "12.5")]
    [InlineData(1234567.89d, "1.23456789e+06")]
    [InlineData(0.1d + 0.2d, "0.30000000000000004")]
    //// Powers of ten outside the range the specification gives examples for
    [InlineData(1e11d, "1e+11")]
    [InlineData(1e100d, "1e+100")]
    [InlineData(1e-100d, "1e-100")]
    [InlineData(-1e11d, "-1e+11")]
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

    // The specification only requires consistency for the values it gives examples for, but
    // every canonical number must still round-trip to the value it was rendered from.
    [Theory]
    [InlineData(0.00011d)]
    [InlineData(1e11d)]
    [InlineData(1234567.89d)]
    [InlineData(1000000.0000005d)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    [InlineData(0.1d + 0.2d)]
    [InlineData(Math.PI)]
    public void WriteCanonicalLabelValueRoundTrips(double value)
    {
        var buffer = new byte[64];

        var cursor = TextFormatSerializer.WriteCanonicalLabelValue(buffer, 0, value);
        var written = Encoding.UTF8.GetString(buffer, 0, cursor);

        Assert.True(
            double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed),
            $"'{written}' could not be parsed.");

        Assert.Equal(value, parsed);
    }

    [Fact]
    public void WriteCanonicalLabelValueDoesNotRoundNearPowersOfTen()
    {
        const double ExactPowerOfTen = 1e6d;
        const double NearPowerOfTen = 1000000.0000005d;

        var exact = WriteCanonicalLabelValueToString(ExactPowerOfTen);
        var near = WriteCanonicalLabelValueToString(NearPowerOfTen);

        Assert.Equal("1e+06", exact);
        Assert.Equal("1.0000000000005e+06", near);
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

    [Fact]
    public void WriteHelpMetadataDoesNotEscapeQuotationMarksForPrometheusText()
    {
        var buffer = new byte[128];
        var metric = new PrometheusMetric("test_metric", string.Empty, PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.PrometheusV1.WriteHelpMetadata(buffer, 0, metric, "A \"quoted\" description");
        var actual = Encoding.UTF8.GetString(buffer, 0, cursor);

        // The Prometheus text exposition format only requires escaping the backslash and line feed
        // characters in HELP text, so double quotes are written unescaped.
        Assert.Equal("# HELP test_metric A \"quoted\" description\n", actual);
    }

    [Fact]
    public void WriteHelpMetadataEscapesQuotationMarksForOpenMetrics()
    {
        var buffer = new byte[128];
        var metric = new PrometheusMetric("test_metric", string.Empty, PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.OpenMetricsV1.WriteHelpMetadata(buffer, 0, metric, "A \"quoted\" description");
        var actual = Encoding.UTF8.GetString(buffer, 0, cursor);

        // Per the OpenMetrics 1.0.0 ABNF, double quotes are excluded from "normal-char" and MUST be
        // escaped as \" within an "escaped-string" (which HELP text is), unlike the classic Prometheus
        // text format. See https://prometheus.io/docs/specs/om/open_metrics_spec/#escaping.
        Assert.Equal("# HELP test_metric A \\\"quoted\\\" description\n", actual);
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

    [Theory]
    [InlineData("""{"a":1}""")]
    [InlineData("TRUNCATED", "a", "012345678901234567890123456789012345678")]
    [InlineData("TRUNCATED", "a", "0123456789", "b", "0123456789", "c", "0123456789")]
    [InlineData("TRUNCATED", "a", "\u0001\u0001\u0001\u0001\u0001\u0001\u0001")]
    public void ToLabelValueStringTruncatesMapWhoseJsonExceedsMaximumLength(string expectedValue, params string[] keysAndValues)
    {
        var map = new Dictionary<string, object?> { ["a"] = 1 };

        if (keysAndValues.Length > 0)
        {
            map.Clear();
            for (var i = 0; i < keysAndValues.Length; i += 2)
            {
                map[keysAndValues[i]] = keysAndValues[i + 1];
            }
        }

        var writer = new PrometheusAnyValueWriter(40);

        Assert.Equal(expectedValue, writer.ToLabelValueString("key", map));
    }

    [Fact]
    public void ToLabelValueStringTruncatesArrayWhoseStructuralJsonExceedsMaximumLength()
    {
        var writer = new PrometheusAnyValueWriter(40);

        var nestedArrays = new object[20];
        for (var i = 0; i < nestedArrays.Length; i++)
        {
            nestedArrays[i] = Array.Empty<int>();
        }

        Assert.Equal("TRUNCATED", writer.ToLabelValueString("key", nestedArrays));
    }

    [Fact]
    public void ToLabelValueStringTruncatesArrayContainingMapWithOversizedKey()
    {
        var writer = new PrometheusAnyValueWriter(40);

        var map = new Dictionary<string, object?> { [new string('k', 1000)] = Array.Empty<int>() };

        Assert.Equal("TRUNCATED", writer.ToLabelValueString("key", new object[] { map }));
    }

    [Theory]
    [InlineData(38, """["0123456789012345678901234567890123"]""")]
    [InlineData(37, "TRUNCATED")]
    public void ToLabelValueStringTruncatesArrayOnlyWhenClosingTokenExceedsMaximumLength(int maxJsonLength, string expectedValue)
    {
        var writer = new PrometheusAnyValueWriter(maxJsonLength);

        Assert.Equal(expectedValue, writer.ToLabelValueString("key", LabelValueBoundaryArrayCase));
    }

    [Theory]
    [InlineData(33, """{"a":"0123456789012345678901234"}""")]
    [InlineData(32, "TRUNCATED")]
    public void ToLabelValueStringTruncatesMapOnlyWhenClosingTokenExceedsMaximumLength(int maxJsonLength, string expectedValue)
    {
        var writer = new PrometheusAnyValueWriter(maxJsonLength);

        Assert.Equal(expectedValue, writer.ToLabelValueString("key", new Dictionary<string, object?> { ["a"] = "0123456789012345678901234" }));
    }

    [Fact]
    public void ToLabelValueStringTruncatesMapWithOversizedStringValueBeforeCopyingIt()
    {
        var writer = new PrometheusAnyValueWriter(40);
        var map = new Dictionary<string, object?> { ["a"] = new string('x', 4 * 1024 * 1024) };

        AssertTruncatedWithoutBufferingValue(() => writer.ToLabelValueString("key", map));
    }

    [Fact]
    public void ToLabelValueStringTruncatesMapWithOversizedByteArrayValueBeforeEncodingIt()
    {
        var writer = new PrometheusAnyValueWriter(40);
        var map = new Dictionary<string, object?> { ["a"] = new byte[1024 * 1024] };

        AssertTruncatedWithoutBufferingValue(() => writer.ToLabelValueString("key", map));

        Assert.Equal("""{"a":"AQID"}""", new PrometheusAnyValueWriter(40).ToLabelValueString("key", new Dictionary<string, object?> { ["a"] = LabelValueByteArrayCase }));
    }

    [Theory]
    [InlineData("+AAA")]
    [InlineData("A+AA")]
    [InlineData("AA+A")]
    [InlineData("AAA+")]
    [InlineData("++++")]
    public void ToLabelValueStringTruncatesMapWithEscapedBase64BeforeEncodingIt(string base64Group)
    {
        var writer = new PrometheusAnyValueWriter(512 * 1024);
        var group = Convert.FromBase64String(base64Group);
        var bytes = new byte[192 * 1024];
        for (var offset = 0; offset < bytes.Length; offset += group.Length)
        {
            group.CopyTo(bytes, offset);
        }

        var map = new Dictionary<string, object?> { ["a"] = bytes };

        AssertTruncatedWithoutBufferingValue(() => writer.ToLabelValueString("key", map));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AQID")]
    [InlineData("+A==")]
    [InlineData("A+A=")]
    [InlineData("AA+A")]
    [InlineData("AAA+")]
    [InlineData("++++")]
    [InlineData("++A=")]
    [InlineData("////")]
    public void ToLabelValueStringPreservesEscapedBase64AtExactLimit(string base64)
    {
        var map = new Dictionary<string, object?> { ["a"] = Convert.FromBase64String(base64) };
        using var stream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(stream))
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString("a", base64);
            jsonWriter.WriteEndObject();
        }

        var expected = Encoding.UTF8.GetString(stream.ToArray());
        var length = stream.Length;

        Assert.Equal(expected, new PrometheusAnyValueWriter(length).ToLabelValueString("key", map));
        Assert.Equal("TRUNCATED", new PrometheusAnyValueWriter(length - 1).ToLabelValueString("key", map));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AQID")]
    [InlineData("+A==")]
    [InlineData("+/8=")]
    public void ToLabelValueStringPreservesNestedJsonAtExactLimit(string base64)
    {
        // Include a numeric exponent and escaped quotes/backslashes so the preflight
        // distinguishes plus signs in JSON strings from those in numbers.
        object[] array = [Convert.FromBase64String(base64), 1e100, "\"\\+"];
        var map = new Dictionary<string, object?> { ["a"] = array };
        var arrayJson = new PrometheusAnyValueWriter(1024).ToLabelValueString("key", array);
        using var stream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(stream))
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("a");
#if NET
            jsonWriter.WriteRawValue(arrayJson);
#else
            using var document = JsonDocument.Parse(arrayJson);
            document.RootElement.WriteTo(jsonWriter);
#endif
            jsonWriter.WriteEndObject();
        }

        var expected = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal(expected, new PrometheusAnyValueWriter(stream.Length).ToLabelValueString("key", map));
        Assert.Equal("TRUNCATED", new PrometheusAnyValueWriter(stream.Length - 1).ToLabelValueString("key", map));
    }

    [Fact]
    public void ToLabelValueStringTruncatesMapWithTooManyEntriesBeforeDeduplicatingThem()
    {
        var writer = new PrometheusAnyValueWriter(40);
        var entries = new List<KeyValuePair<string, object?>>();

        for (var i = 0; i < 100_000; i++)
        {
            entries.Add(new(i.ToString(CultureInfo.InvariantCulture), null));
        }

        AssertTruncatedWithoutBufferingValue(() => writer.ToLabelValueString("key", entries));
    }

    [Fact]
    public void ToLabelValueStringTruncatesNestedMapWithTooManyEntries()
    {
        var writer = new PrometheusAnyValueWriter(40);
        var entries = new List<KeyValuePair<string, object?>>();

        for (var i = 0; i < 100_000; i++)
        {
            entries.Add(new(i.ToString(CultureInfo.InvariantCulture), null));
        }

        AssertTruncatedWithoutBufferingValue(() => writer.ToLabelValueString("key", new object[] { entries }));
    }

    [Fact]
    public void ToLabelValueStringTruncatesStringWhoseEscapedFormExceedsMaximumLengthBeforeBufferingIt()
    {
        var writer = new PrometheusAnyValueWriter(512 * 1024);

        // Each character is escaped to 6 bytes, so the escaped form is 3 MiB for a 512 KiB limit.
        var map = new Dictionary<string, object?> { ["a"] = new string('\u00e9', 512 * 1024) };
        var array = new[] { new string('\u00e9', 512 * 1024) };

        AssertTruncatedWithoutBufferingValue(() => writer.ToLabelValueString("key", map));
        AssertTruncatedWithoutBufferingValue(() => writer.ToLabelValueString("key", array));
    }

    [Fact]
    public void ToLabelValueStringTruncatesNestedValuesWhoseJsonExceedsMaximumLength()
    {
        var writer = new PrometheusAnyValueWriter(40);

        Assert.Equal(
            """{"a":"TRUNCATED","b":1}""",
            writer.ToLabelValueString("key", new Dictionary<string, object?> { ["a"] = LabelValueOversizedArrayCase, ["b"] = 1 }));
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

        var cursor = TextFormatSerializer.OpenMetricsV1.WriteUnitMetadata(buffer, 0, metric, "seconds");

        Assert.Equal("# UNIT test seconds\n", Encoding.UTF8.GetString(buffer, 0, cursor));
    }

    [Fact]
    public void WriteUnitMetadataIsNotWrittenForPrometheusTextFormat()
    {
        var buffer = new byte[64];
        var metric = new PrometheusMetric("test", string.Empty, PrometheusType.Gauge, disableTotalNameSuffixForCounters: false);

        var cursor = TextFormatSerializer.PrometheusV1.WriteUnitMetadata(buffer, 0, metric, "seconds");

        Assert.Equal(0, cursor);
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

        counter.Add(1, new("keep", "value"), new("rocket", "rkt:\U0001F680"));

        provider.ForceFlush();

        var cursor = WriteMetric(buffer, 0, metrics[0], useOpenMetrics: true);
        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        Assert.Contains("rkt:\U0001F680", output, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EscapingSchemes))]
    public async Task WriteMetric_EscapesMetricAndLabelNamesUsingNegotiatedScheme(string escaping)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("http.server.requests");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        counter.Add(1, new KeyValuePair<string, object?>("http.method", "GET"));

        provider.ForceFlush();

        var protocol = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            escaping,
            PrometheusProtocol.OpenMetricsV1,
            isOpenMetrics: true);

        var serializer = TextFormatSerializer.GetSerializer(protocol);

        var cursor = serializer.WriteMetric(
            buffer,
            0,
            metrics[0],
            PrometheusMetric.Create(metrics[0], false),
            writeType: true,
            writeUnit: true,
            writeHelp: true,
            unitOverride: null,
            helpOverride: null);

        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings).UseParameters(escaping);
    }

    [Theory]
    [MemberData(nameof(EscapingSchemes))]
    public async Task WriteMetric_EscapesColonInPointTagKeyUsingLabelNameRules(string escaping)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("test_counter");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        counter.Add(1, new KeyValuePair<string, object?>("http:method", "GET"));

        provider.ForceFlush();

        var output = WriteMetricWithEscaping(buffer, metrics[0], escaping);

        await Verify(output, "txt", VerifySettings).UseParameters(escaping);
    }

    [Fact]
    public async Task WriteMetricEscapesScopeLabelKeysUsingNegotiatedDotsScheme()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(
            nameof(this.WriteMetricEscapesScopeLabelKeysUsingNegotiatedDotsScheme),
            "1.0.0",
            [new("dot_name", "value")],
            scope: null);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateCounter<int>("test_counter").Add(1);

        provider.ForceFlush();

        var output = WriteMetricWithDotsEscaping(buffer, metrics[0]);

        await Verify(output, "txt", VerifySettings);
    }

    [Theory]
    [InlineData(PrometheusProtocol.AllowUtf8Escaping, "otel_scope_name")]
    [InlineData(PrometheusProtocol.DotsEscaping, "otel/scope/name")]
    [InlineData(PrometheusProtocol.UnderscoresEscaping, "otel_scope_name")]
    [InlineData(PrometheusProtocol.ValuesEscaping, "otel_scope_name")]
    public async Task WriteMetricWritesScopeLabelsVerbatim(string escaping, string collidingPointTagKey)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = new Meter(
            nameof(this.WriteMetricWritesScopeLabelsVerbatim),
            "1.0.0",
            tags: null,
            scope: null);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        meter.CreateCounter<int>("test_counter").Add(
            1,
            new KeyValuePair<string, object?>(collidingPointTagKey, "point"));

        provider.ForceFlush();

        var output = WriteMetricWithEscaping(buffer, metrics[0], escaping);

        await Verify(output, "txt", VerifySettings).UseParameters(escaping);
    }

    [Fact]
    public async Task WriteExemplarEscapesReservedTraceIdKeyToPreventCollisionWithFilteredTag()
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

        using var activity = new Activity("test");
        activity.Start();

        // The filtered tag is literally named "trace_id"; under the dots scheme both it and the
        // built-in trace_id escape to the same output key "trace__id". The built-in trace ID MUST
        // take precedence so its value is emitted intact rather than having the filtered tag's
        // value concatenated onto it.
        counter.Add(2, new("keep", "value"), new("trace_id", "drop-me"));

        provider.ForceFlush();

        var output = WriteMetricWithDotsEscaping(buffer, metrics[0]);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task WriteMetric_AllowUtf8_EscapesQuoteBackslashAndLineFeedInQuotedName()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("original_name");

        // The Meter API rejects instrument names containing characters such as '"', '\' and '\n',
        // but the OpenTelemetry specification does not subject a View-provided stream name to the
        // instrument name syntax, so a renamed metric can contain them. Such a name is not a valid
        // legacy name, so the allow-utf-8 format writes it as a quoted string in which those
        // characters must themselves be escaped.
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView("original_name", "a\"b\\c\nd")
            .AddInMemoryExporter(metrics)
            .Build();

        counter.Add(1);

        provider.ForceFlush();

        var protocol = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            PrometheusProtocol.AllowUtf8Escaping,
            PrometheusProtocol.OpenMetricsV1,
            isOpenMetrics: true);

        var serializer = TextFormatSerializer.GetSerializer(protocol);

        var cursor = serializer.WriteMetric(
            buffer,
            0,
            metrics[0],
            PrometheusMetric.Create(metrics[0], false),
            writeType: true,
            writeUnit: false,
            writeHelp: false,
            unitOverride: null,
            helpOverride: null);

        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Fact]
    public async Task WriteMetric_AllowUtf8_EmbedsQuotedNameWhenLabelKeyCollidesWithScopeLabel()
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var counter = meter.CreateCounter<long>("http.server.requests");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        // A point tag whose output key collides with the emitted otel_scope_name scope label forces
        // the slow label-writing path, which must still embed the quoted metric name as the first
        // element inside the braces.
        counter.Add(1, new KeyValuePair<string, object?>("otel_scope_name", "collision"));

        provider.ForceFlush();

        var protocol = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            PrometheusProtocol.AllowUtf8Escaping,
            PrometheusProtocol.OpenMetricsV1,
            isOpenMetrics: true);

        var serializer = TextFormatSerializer.GetSerializer(protocol);

        var cursor = serializer.WriteMetric(
            buffer,
            0,
            metrics[0],
            PrometheusMetric.Create(metrics[0], false),
            writeType: false,
            writeUnit: false,
            writeHelp: false,
            unitOverride: null,
            helpOverride: null);

        var output = Encoding.UTF8.GetString(buffer, 0, cursor);

        await Verify(output, "txt", VerifySettings);
    }

    [Theory]
    [MemberData(nameof(EscapingSchemesAndFormats))]
    public async Task WriteMetric_SerializesMultibyteUtf8MetricNameProvidedByView(string escaping, bool useOpenMetrics)
    {
        var buffer = new byte[85000];
        var metrics = new List<Metric>();

        using var meter = CreateMeter();
        var histogram = meter.CreateHistogram<double>(
            "original_name",
            unit: "s",
            description: "Duration of HTTP server requests.");

        // A View-provided metric stream name is not subject to the OpenTelemetry instrument name
        // syntax (https://opentelemetry.io/docs/specs/otel/metrics/sdk/#stream-configuration), so it
        // may contain multibyte UTF-8 characters that the Meter API would otherwise reject. Renaming
        // to a name containing the multibyte characters U+82B1 U+706B (CJK) and U+1F680 (a rocket,
        // outside the Basic Multilingual Plane) exercises BuildAllowUtf8Names, the UTF-8 byte encoding
        // in NameSet, and the quoted/escaped TYPE, UNIT and HELP metadata together with the _bucket,
        // _sum, _count and _created series end-to-end. It is parameterized over all four escaping
        // schemes and both v1 serializers, and each response remains UTF-8:
        //   * allow-utf-8 preserves the multibyte characters using the quoted exposition form,
        //   * underscores replaces the unsupported characters with underscores,
        //   * dots encodes '.' and replaces the unsupported characters, and
        //   * values reversibly encodes the Unicode code points.
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddView(
                "original_name",
                new ExplicitBucketHistogramConfiguration
                {
                    Name = "http.\u82B1\u706B.\U0001F680.requests",
                    Boundaries = [5, 10],
                })
            .AddInMemoryExporter(metrics)
            .Build();

        histogram.Record(4, new KeyValuePair<string, object?>("http.method", "GET"));
        histogram.Record(8, new KeyValuePair<string, object?>("http.method", "GET"));

        provider.ForceFlush();

        var output = WriteMetricWithEscaping(buffer, metrics[0], escaping, useOpenMetrics);

        await Verify(output, "txt", VerifySettings).UseParameters(escaping, useOpenMetrics);
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

    private static int WriteMetric(
        byte[] buffer,
        int cursor,
        Metric metric,
        bool useOpenMetrics,
        bool suppressScopeInfo = false,
        bool appendSuffixes = true,
        IReadOnlyList<KeyValuePair<string, object>>? resourceConstantLabels = null,
        SeriesRegistry? series = null)
    {
        TextFormatSerializer serializer = useOpenMetrics ? TextFormatSerializer.OpenMetricsV1 : TextFormatSerializer.PrometheusV1;
        var prometheusMetric = PrometheusMetric.Create(metric, disableTotalNameSuffixForCounters: false, appendSuffixes);
        var options = new TextFormatSerializerOptions(suppressScopeInfo, resourceConstantLabels, series);

        return serializer.WriteMetric(
            buffer,
            cursor,
            metric,
            prometheusMetric,
            writeType: true,
            writeUnit: true,
            writeHelp: true,
            unitOverride: null,
            helpOverride: null,
            options);
    }

    private static List<string> GetSampleLines(string output, string seriesName)
        => output
            .Split('\n')
            .Where(line => line.StartsWith(seriesName, StringComparison.Ordinal) && line.Length > seriesName.Length && line[seriesName.Length] is ' ' or '{')
            .ToList();

    private static int CountSamples(string output, string seriesName) => GetSampleLines(output, seriesName).Count;

    private static string WriteMetricWithDotsEscaping(byte[] buffer, Metric metric)
        => WriteMetricWithEscaping(buffer, metric, PrometheusProtocol.DotsEscaping);

    private static string WriteMetricWithEscaping(byte[] buffer, Metric metric, string escaping)
        => WriteMetricWithEscaping(buffer, metric, escaping, useOpenMetrics: true);

    private static string WriteMetricWithEscaping(byte[] buffer, Metric metric, string escaping, bool useOpenMetrics)
    {
        var protocol = new PrometheusProtocol(
            useOpenMetrics ? PrometheusProtocol.OpenMetricsMediaType : PrometheusProtocol.PrometheusTextMediaType,
            escaping,
            useOpenMetrics ? PrometheusProtocol.OpenMetricsV1 : PrometheusProtocol.PrometheusV1,
            isOpenMetrics: useOpenMetrics);

        var serializer = TextFormatSerializer.GetSerializer(protocol);

        var cursor = serializer.WriteMetric(
            buffer,
            0,
            metric,
            PrometheusMetric.Create(metric, false),
            writeType: true,
            writeUnit: true,
            writeHelp: true,
            unitOverride: null,
            helpOverride: null);

        return Encoding.UTF8.GetString(buffer, 0, cursor);
    }

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
    [GeneratedRegex("(?m)^((?:[^\\s{]*_created(?:\\{[^}]*\\})?|\\{[^}]*_created[^}]*\\})\\s+)\\S+$")]
    private static partial Regex CreatedMetric();

    [GeneratedRegex("(?m)^(.+?\\s#\\s\\{[^}]*\\}\\s+\\S+\\s+)\\S+$")]
    private static partial Regex ExemplarTimestamp();

    [GeneratedRegex("(?m)(trace_{1,2}id|span_{1,2}id)=\"[^\"]*\"")]
    private static partial Regex SpanOrTraceIds();

    [GeneratedRegex("telemetry_sdk_version=\"[^\"]*\"")]
    private static partial Regex SdkVersion();
#else
    private static Regex CreatedMetric() => new("(?m)^((?:[^\\s{]*_created(?:\\{[^}]*\\})?|\\{[^}]*_created[^}]*\\})\\s+)\\S+$", RegexOptions.Compiled);

    private static Regex ExemplarTimestamp() => new("(?m)^(.+?\\s#\\s\\{[^}]*\\}\\s+\\S+\\s+)\\S+$", RegexOptions.Compiled);

    private static Regex SpanOrTraceIds() => new("(?m)(trace_{1,2}id|span_{1,2}id)=\"[^\"]*\"", RegexOptions.Compiled);

    private static Regex SdkVersion() => new("telemetry_sdk_version=\"[^\"]*\"", RegexOptions.Compiled);
#endif

    private static void AssertTruncatedWithoutBufferingValue(Func<string> action)
    {
        Assert.Equal("TRUNCATED", action());

#if NET
        var before = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal("TRUNCATED", action());

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 256 * 1024, $"{allocated} bytes were allocated.");
#endif
    }

    private static Dictionary<string, object?> CreateReferenceKeyedDictionary()
        => new(new ReferenceKeyComparer())
        {
            // Two distinct string instances with the same text are distinct keys for the comparer
            [new string('a', 1)] = 1,
            [new string('a', 1)] = 2,
        };

    private sealed class UnsupportedValue
    {
        public override string? ToString() => null;
    }

    private sealed class ReferenceKeyComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => ReferenceEquals(x, y);

        public int GetHashCode(string obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
