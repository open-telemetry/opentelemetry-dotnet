// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpMetrics = OpenTelemetry.Proto.Metrics.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection("EnvVars")]
public class OtlpMetricsExporterTests : IDisposable
{
    private static readonly KeyValuePair<string, object?>[] KeyValues =
    [
        new("key1", "value1"),
        new("key2", 123),
    ];

    public OtlpMetricsExporterTests()
    {
        OtlpSpecConfigDefinitionTests.ClearEnvVars();
    }

    [Fact]
    public void TestAddOtlpExporter_SetsCorrectMetricReaderDefaults()
    {
        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddOtlpExporter()
            .Build();

        CheckMetricReaderDefaults();

        meterProvider.Dispose();

        meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddOtlpExporter()
            .Build();

        CheckMetricReaderDefaults();

        meterProvider.Dispose();

        void CheckMetricReaderDefaults()
        {
            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;

            var metricReader = typeof(MetricReader)
                .Assembly
                .GetType("OpenTelemetry.Metrics.MeterProviderSdk")?
                .GetField("reader", bindingFlags)?
                .GetValue(meterProvider) as PeriodicExportingMetricReader;

            Assert.NotNull(metricReader);

            var exportIntervalMilliseconds = (int?)typeof(PeriodicExportingMetricReader)?
                .GetField("ExportIntervalMilliseconds", bindingFlags)?
                .GetValue(metricReader);

            Assert.Equal(60000, exportIntervalMilliseconds);
        }
    }

    [Fact]
    public void TestAddOtlpExporter_NamedOptions()
    {
        int defaultExporterOptionsConfigureOptionsInvocations = 0;
        int namedExporterOptionsConfigureOptionsInvocations = 0;

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<OtlpExporterOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);
                services.Configure<MetricReaderOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<OtlpExporterOptions>("Exporter2", o => namedExporterOptionsConfigureOptionsInvocations++);
                services.Configure<MetricReaderOptions>("Exporter2", o => namedExporterOptionsConfigureOptionsInvocations++);

                services.Configure<OtlpExporterOptions>("Exporter3", o => namedExporterOptionsConfigureOptionsInvocations++);
                services.Configure<MetricReaderOptions>("Exporter3", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddOtlpExporter()
            .AddOtlpExporter("Exporter2", o => { })
            .AddOtlpExporter("Exporter3", (eo, ro) => { })
            .Build();

        Assert.Equal(2, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(4, namedExporterOptionsConfigureOptionsInvocations);
    }

    [Fact]
    public void UserHttpFactoryCalled()
    {
        OtlpExporterOptions options = new OtlpExporterOptions();

        var defaultFactory = options.HttpClientFactory;

        int invocations = 0;
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.HttpClientFactory = () =>
        {
            invocations++;
            return defaultFactory();
        };

        using (var exporter = new OtlpMetricExporter(options))
        {
            Assert.Equal(1, invocations);
        }

        using (var provider = Sdk.CreateMeterProviderBuilder()
            .AddOtlpExporter(o =>
            {
                o.Protocol = OtlpExportProtocol.HttpProtobuf;
                o.HttpClientFactory = options.HttpClientFactory;
            })
            .Build())
        {
            Assert.Equal(2, invocations);
        }

        options.HttpClientFactory = () => throw new NotSupportedException();
        Assert.Throws<NotSupportedException>(() =>
        {
            using var exporter = new OtlpMetricExporter(options);
        });
    }

    [Fact]
    public void ServiceProviderHttpClientFactoryInvoked()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddHttpClient();

        int invocations = 0;

        services.AddHttpClient("OtlpMetricExporter", configureClient: (client) => invocations++);

        services.AddOpenTelemetry().WithMetrics(builder => builder
            .AddOtlpExporter(o => o.Protocol = OtlpExportProtocol.HttpProtobuf));

        using var serviceProvider = services.BuildServiceProvider();

        var meterProvider = serviceProvider.GetRequiredService<MeterProvider>();

        Assert.Equal(1, invocations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToOtlpResourceMetricsTest(bool includeServiceNameInResource)
    {
        var resourceBuilder = ResourceBuilder.CreateEmpty();
        if (includeServiceNameInResource)
        {
            resourceBuilder.AddAttributes(
                new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "service-name"),
                    new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceNamespace, "ns1"),
                });
        }

        var metrics = new List<Metric>();

        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        using var meter = new Meter(name: $"{Utils.GetCurrentMethodName()}.{includeServiceNameInResource}", version: "0.0.1", tags: meterTags);
        using var provider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateCounter<int>("counter");
        counter.Add(100);

        provider.ForceFlush();

        var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);
        var request = CreateMetricExportRequest(batch, resourceBuilder.Build());

        Assert.Single(request.ResourceMetrics);
        var resourceMetric = request.ResourceMetrics.First();
        var otlpResource = resourceMetric.Resource;

        if (includeServiceNameInResource)
        {
            Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service-name");
            Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns1");
        }
        else
        {
            Assert.DoesNotContain(otlpResource.Attributes, kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName);
        }

        Assert.Single(resourceMetric.ScopeMetrics);
        var instrumentationLibraryMetrics = resourceMetric.ScopeMetrics.First();
        Assert.Equal(string.Empty, instrumentationLibraryMetrics.SchemaUrl);
        Assert.Equal(meter.Name, instrumentationLibraryMetrics.Scope.Name);
        Assert.Equal("0.0.1", instrumentationLibraryMetrics.Scope.Version);

        Assert.Equal(2, instrumentationLibraryMetrics.Scope.Attributes.Count);
        Assert.Contains(instrumentationLibraryMetrics.Scope.Attributes, (kvp) => kvp.Key == "key1" && kvp.Value.StringValue == "value1");
        Assert.Contains(instrumentationLibraryMetrics.Scope.Attributes, (kvp) => kvp.Key == "key2" && kvp.Value.StringValue == "value2");
    }

    [Theory]
    [InlineData("test_gauge", null, null, 123L, null)]
    [InlineData("test_gauge", null, null, null, 123.45)]
    [InlineData("test_gauge", null, null, 123L, null, true)]
    [InlineData("test_gauge", null, null, null, 123.45, true)]
    [InlineData("test_gauge", "description", "unit", 123L, null)]
    public void TestGaugeToOtlpMetric(string name, string? description, string? unit, long? longValue, double? doubleValue, bool enableExemplars = false)
    {
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(enableExemplars ? ExemplarFilterType.AlwaysOn : ExemplarFilterType.AlwaysOff)
            .AddInMemoryExporter(metrics)
            .Build();

        if (longValue.HasValue)
        {
            meter.CreateObservableGauge(name, () => longValue.Value, unit, description);
        }
        else
        {
            meter.CreateObservableGauge(name, () => doubleValue!.Value, unit, description);
        }

        provider.ForceFlush();

        var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);

        var request = CreateMetricExportRequest(batch, ResourceBuilder.CreateEmpty().Build());

        var resourceMetric = request.ResourceMetrics.Single();
        var scopeMetrics = resourceMetric.ScopeMetrics.Single();
        var actual = scopeMetrics.Metrics.Single();

        Assert.Equal(name, actual.Name);
        Assert.Equal(description ?? string.Empty, actual.Description);
        Assert.Equal(unit ?? string.Empty, actual.Unit);

        Assert.Equal(OtlpMetrics.Metric.DataOneofCase.Gauge, actual.DataCase);

        Assert.NotNull(actual.Gauge);
        Assert.Null(actual.Sum);
        Assert.Null(actual.Histogram);
        Assert.Null(actual.ExponentialHistogram);
        Assert.Null(actual.Summary);

        Assert.Single(actual.Gauge.DataPoints);
        var dataPoint = actual.Gauge.DataPoints.First();
        Assert.True(dataPoint.StartTimeUnixNano > 0);
        Assert.True(dataPoint.TimeUnixNano > 0);

        if (longValue.HasValue)
        {
            Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsInt, dataPoint.ValueCase);
            Assert.Equal(longValue, dataPoint.AsInt);
        }
        else
        {
            Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsDouble, dataPoint.ValueCase);
            Assert.Equal(doubleValue, dataPoint.AsDouble);
        }

        Assert.Empty(dataPoint.Attributes);

        VerifyExemplars(longValue, doubleValue, enableExemplars, d => d.Exemplars.FirstOrDefault(), dataPoint);
    }

    [Theory]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_counter", null, null, null, 123.45, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Cumulative, false, true)]
    [InlineData("test_counter", null, null, null, 123.45, MetricReaderTemporalityPreference.Cumulative, false, true)]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Delta)]
    [InlineData("test_counter", null, null, null, 123.45, MetricReaderTemporalityPreference.Delta)]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Delta, false, true)]
    [InlineData("test_counter", null, null, null, 123.45, MetricReaderTemporalityPreference.Delta, false, true)]
    [InlineData("test_counter", "description", "unit", 123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Delta, true)]
    public void TestCounterToOtlpMetric(string name, string? description, string? unit, long? longValue, double? doubleValue, MetricReaderTemporalityPreference aggregationTemporality, bool enableKeyValues = false, bool enableExemplars = false)
    {
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(enableExemplars ? ExemplarFilterType.AlwaysOn : ExemplarFilterType.AlwaysOff)
            .AddInMemoryExporter(metrics, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = aggregationTemporality;
            })
            .Build();

        var attributes = enableKeyValues ? KeyValues : Array.Empty<KeyValuePair<string, object?>>();
        if (longValue.HasValue)
        {
            var counter = meter.CreateCounter<long>(name, unit, description);
            counter.Add(longValue.Value, attributes);
        }
        else
        {
            var counter = meter.CreateCounter<double>(name, unit, description);
            counter.Add(doubleValue!.Value, attributes);
        }

        provider.ForceFlush();

        var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);
        var request = CreateMetricExportRequest(batch, ResourceBuilder.CreateEmpty().Build());

        var resourceMetric = request.ResourceMetrics.Single();
        var scopeMetrics = resourceMetric.ScopeMetrics.Single();
        var actual = scopeMetrics.Metrics.Single();

        Assert.Equal(name, actual.Name);
        Assert.Equal(description ?? string.Empty, actual.Description);
        Assert.Equal(unit ?? string.Empty, actual.Unit);

        Assert.Equal(OtlpMetrics.Metric.DataOneofCase.Sum, actual.DataCase);

        Assert.Null(actual.Gauge);
        Assert.NotNull(actual.Sum);
        Assert.Null(actual.Histogram);
        Assert.Null(actual.ExponentialHistogram);
        Assert.Null(actual.Summary);

        Assert.True(actual.Sum.IsMonotonic);

        var otlpAggregationTemporality = aggregationTemporality == MetricReaderTemporalityPreference.Cumulative
            ? OtlpMetrics.AggregationTemporality.Cumulative
            : OtlpMetrics.AggregationTemporality.Delta;
        Assert.Equal(otlpAggregationTemporality, actual.Sum.AggregationTemporality);

        Assert.Single(actual.Sum.DataPoints);
        var dataPoint = actual.Sum.DataPoints.First();
        Assert.True(dataPoint.StartTimeUnixNano > 0);
        Assert.True(dataPoint.TimeUnixNano > 0);

        if (longValue.HasValue)
        {
            Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsInt, dataPoint.ValueCase);
            Assert.Equal(longValue, dataPoint.AsInt);
        }
        else
        {
            Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsDouble, dataPoint.ValueCase);
            Assert.Equal(doubleValue, dataPoint.AsDouble);
        }

        if (attributes.Length > 0)
        {
            OtlpTestHelpers.AssertOtlpAttributes(attributes, dataPoint.Attributes);
        }
        else
        {
            Assert.Empty(dataPoint.Attributes);
        }

        VerifyExemplars(longValue, doubleValue, enableExemplars, d => d.Exemplars.FirstOrDefault(), dataPoint);
    }

    [Theory]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_counter", null, null, null, 123.45, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Cumulative, false, true)]
    [InlineData("test_counter", null, null, null, 123.45, MetricReaderTemporalityPreference.Cumulative, false, true)]
    [InlineData("test_counter", null, null, -123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_counter", null, null, null, -123.45, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Delta)]
    [InlineData("test_counter", null, null, null, -123.45, MetricReaderTemporalityPreference.Delta)]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Delta, false, true)]
    [InlineData("test_counter", null, null, null, -123.45, MetricReaderTemporalityPreference.Delta, false, true)]
    [InlineData("test_counter", "description", "unit", 123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_counter", null, null, 123L, null, MetricReaderTemporalityPreference.Delta, true)]
    public void TestUpDownCounterToOtlpMetric(string name, string? description, string? unit, long? longValue, double? doubleValue, MetricReaderTemporalityPreference aggregationTemporality, bool enableKeyValues = false, bool enableExemplars = false)
    {
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(enableExemplars ? ExemplarFilterType.AlwaysOn : ExemplarFilterType.AlwaysOff)
            .AddInMemoryExporter(metrics, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = aggregationTemporality;
            })
            .Build();

        var attributes = enableKeyValues ? KeyValues : Array.Empty<KeyValuePair<string, object?>>();
        if (longValue.HasValue)
        {
            var counter = meter.CreateUpDownCounter<long>(name, unit, description);
            counter.Add(longValue.Value, attributes);
        }
        else
        {
            var counter = meter.CreateUpDownCounter<double>(name, unit, description);
            counter.Add(doubleValue!.Value, attributes);
        }

        provider.ForceFlush();

        var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);

        var request = CreateMetricExportRequest(batch, ResourceBuilder.CreateEmpty().Build());

        var resourceMetric = request.ResourceMetrics.Single();
        var scopeMetrics = resourceMetric.ScopeMetrics.Single();
        var actual = scopeMetrics.Metrics.Single();

        Assert.Equal(name, actual.Name);
        Assert.Equal(description ?? string.Empty, actual.Description);
        Assert.Equal(unit ?? string.Empty, actual.Unit);

        Assert.Equal(OtlpMetrics.Metric.DataOneofCase.Sum, actual.DataCase);

        Assert.Null(actual.Gauge);
        Assert.NotNull(actual.Sum);
        Assert.Null(actual.Histogram);
        Assert.Null(actual.ExponentialHistogram);
        Assert.Null(actual.Summary);

        Assert.False(actual.Sum.IsMonotonic);

        var otlpAggregationTemporality = aggregationTemporality == MetricReaderTemporalityPreference.Cumulative
            ? OtlpMetrics.AggregationTemporality.Cumulative
            : OtlpMetrics.AggregationTemporality.Cumulative;
        Assert.Equal(otlpAggregationTemporality, actual.Sum.AggregationTemporality);

        Assert.Single(actual.Sum.DataPoints);
        var dataPoint = actual.Sum.DataPoints.First();
        Assert.True(dataPoint.StartTimeUnixNano > 0);
        Assert.True(dataPoint.TimeUnixNano > 0);

        if (longValue.HasValue)
        {
            Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsInt, dataPoint.ValueCase);
            Assert.Equal(longValue, dataPoint.AsInt);
        }
        else
        {
            Assert.Equal(OtlpMetrics.NumberDataPoint.ValueOneofCase.AsDouble, dataPoint.ValueCase);
            Assert.Equal(doubleValue, dataPoint.AsDouble);
        }

        if (attributes.Length > 0)
        {
            OtlpTestHelpers.AssertOtlpAttributes(attributes, dataPoint.Attributes);
        }
        else
        {
            Assert.Empty(dataPoint.Attributes);
        }

        VerifyExemplars(longValue, doubleValue, enableExemplars, d => d.Exemplars.FirstOrDefault(), dataPoint);
    }

    [Theory]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, null, 123.45, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Cumulative, false, true)]
    [InlineData("test_histogram", null, null, null, 123.45, MetricReaderTemporalityPreference.Cumulative, false, true)]
    [InlineData("test_histogram", null, null, -123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, null, -123.45, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Delta)]
    [InlineData("test_histogram", null, null, null, 123.45, MetricReaderTemporalityPreference.Delta)]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Delta, false, true)]
    [InlineData("test_histogram", null, null, null, 123.45, MetricReaderTemporalityPreference.Delta, false, true)]
    [InlineData("test_histogram", "description", "unit", 123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Delta, true)]
    public void TestExponentialHistogramToOtlpMetric(string name, string? description, string? unit, long? longValue, double? doubleValue, MetricReaderTemporalityPreference aggregationTemporality, bool enableKeyValues = false, bool enableExemplars = false)
    {
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(enableExemplars ? ExemplarFilterType.AlwaysOn : ExemplarFilterType.AlwaysOff)
            .AddInMemoryExporter(metrics, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = aggregationTemporality;
            })
            .AddView(instrument =>
            {
                return new Base2ExponentialBucketHistogramConfiguration();
            })
            .Build();

        var attributes = enableKeyValues ? KeyValues : Array.Empty<KeyValuePair<string, object?>>();
        if (longValue.HasValue)
        {
            var histogram = meter.CreateHistogram<long>(name, unit, description);
            histogram.Record(longValue.Value, attributes);
            histogram.Record(0, attributes);
        }
        else
        {
            var histogram = meter.CreateHistogram<double>(name, unit, description);
            histogram.Record(doubleValue!.Value, attributes);
            histogram.Record(0, attributes);
        }

        provider.ForceFlush();

        var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);
        var request = CreateMetricExportRequest(batch, ResourceBuilder.CreateEmpty().Build());

        var resourceMetric = request.ResourceMetrics.Single();
        var scopeMetrics = resourceMetric.ScopeMetrics.Single();
        var actual = scopeMetrics.Metrics.Single();

        Assert.Equal(name, actual.Name);
        Assert.Equal(description ?? string.Empty, actual.Description);
        Assert.Equal(unit ?? string.Empty, actual.Unit);

        Assert.Equal(OtlpMetrics.Metric.DataOneofCase.ExponentialHistogram, actual.DataCase);

        Assert.Null(actual.Gauge);
        Assert.Null(actual.Sum);
        Assert.Null(actual.Histogram);
        Assert.NotNull(actual.ExponentialHistogram);
        Assert.Null(actual.Summary);

        var otlpAggregationTemporality = aggregationTemporality == MetricReaderTemporalityPreference.Cumulative
            ? OtlpMetrics.AggregationTemporality.Cumulative
            : OtlpMetrics.AggregationTemporality.Delta;
        Assert.Equal(otlpAggregationTemporality, actual.ExponentialHistogram.AggregationTemporality);

        Assert.Single(actual.ExponentialHistogram.DataPoints);
        var dataPoint = actual.ExponentialHistogram.DataPoints.First();
        Assert.True(dataPoint.StartTimeUnixNano > 0);
        Assert.True(dataPoint.TimeUnixNano > 0);

        Assert.Equal(20, dataPoint.Scale);
        Assert.Equal(1UL, dataPoint.ZeroCount);
        if (longValue > 0 || doubleValue > 0)
        {
            Assert.Equal(2UL, dataPoint.Count);
        }
        else
        {
            Assert.Equal(1UL, dataPoint.Count);
        }

        if (longValue.HasValue)
        {
            if (longValue > 0)
            {
                Assert.Equal((double)longValue, dataPoint.Sum);
                Assert.Null(dataPoint.Negative);
                Assert.True(dataPoint.Positive.Offset > 0);
                Assert.Equal(1UL, dataPoint.Positive.BucketCounts[0]);
            }
            else
            {
                Assert.Equal(0, dataPoint.Sum);
                Assert.Null(dataPoint.Negative);
                Assert.Equal(0, dataPoint.Positive.Offset);
                Assert.Empty(dataPoint.Positive.BucketCounts);
            }
        }
        else
        {
            if (doubleValue > 0)
            {
                Assert.Equal(doubleValue, dataPoint.Sum);
                Assert.Null(dataPoint.Negative);
                Assert.True(dataPoint.Positive.Offset > 0);
                Assert.Equal(1UL, dataPoint.Positive.BucketCounts[0]);
            }
            else
            {
                Assert.Equal(0, dataPoint.Sum);
                Assert.Null(dataPoint.Negative);
                Assert.Equal(0, dataPoint.Positive.Offset);
                Assert.Empty(dataPoint.Positive.BucketCounts);
            }
        }

        if (attributes.Length > 0)
        {
            OtlpTestHelpers.AssertOtlpAttributes(attributes, dataPoint.Attributes);
        }
        else
        {
            Assert.Empty(dataPoint.Attributes);
        }

        VerifyExemplars(null, longValue ?? doubleValue, enableExemplars, d => d.Exemplars.FirstOrDefault(), dataPoint);
        if (enableExemplars)
        {
            VerifyExemplars(null, 0, enableExemplars, d => d.Exemplars.Skip(1).FirstOrDefault(), dataPoint);
        }
    }

    [Theory]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, null, 123.45, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Cumulative, false, true)]
    [InlineData("test_histogram", null, null, null, 123.45, MetricReaderTemporalityPreference.Cumulative, false, true)]
    [InlineData("test_histogram", null, null, -123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, null, -123.45, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Delta)]
    [InlineData("test_histogram", null, null, null, 123.45, MetricReaderTemporalityPreference.Delta)]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Delta, false, true)]
    [InlineData("test_histogram", null, null, null, 123.45, MetricReaderTemporalityPreference.Delta, false, true)]
    [InlineData("test_histogram", "description", "unit", 123L, null, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("test_histogram", null, null, 123L, null, MetricReaderTemporalityPreference.Delta, true)]
    public void TestHistogramToOtlpMetric(string name, string? description, string? unit, long? longValue, double? doubleValue, MetricReaderTemporalityPreference aggregationTemporality, bool enableKeyValues = false, bool enableExemplars = false)
    {
        var metrics = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(enableExemplars ? ExemplarFilterType.AlwaysOn : ExemplarFilterType.AlwaysOff)
            .AddInMemoryExporter(metrics, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = aggregationTemporality;
            })
            .Build();

        var attributes = enableKeyValues ? KeyValues : Array.Empty<KeyValuePair<string, object?>>();
        if (longValue.HasValue)
        {
            var histogram = meter.CreateHistogram<long>(name, unit, description);
            histogram.Record(longValue.Value, attributes);
        }
        else
        {
            var histogram = meter.CreateHistogram<double>(name, unit, description);
            histogram.Record(doubleValue!.Value, attributes);
        }

        provider.ForceFlush();

        var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);
        var request = CreateMetricExportRequest(batch, ResourceBuilder.CreateEmpty().Build());

        var resourceMetric = request.ResourceMetrics.Single();
        var scopeMetrics = resourceMetric.ScopeMetrics.Single();
        var actual = scopeMetrics.Metrics.Single();

        Assert.Equal(name, actual.Name);
        Assert.Equal(description ?? string.Empty, actual.Description);
        Assert.Equal(unit ?? string.Empty, actual.Unit);

        Assert.Equal(OtlpMetrics.Metric.DataOneofCase.Histogram, actual.DataCase);

        Assert.Null(actual.Gauge);
        Assert.Null(actual.Sum);
        Assert.NotNull(actual.Histogram);
        Assert.Null(actual.ExponentialHistogram);
        Assert.Null(actual.Summary);

        var otlpAggregationTemporality = aggregationTemporality == MetricReaderTemporalityPreference.Cumulative
            ? OtlpMetrics.AggregationTemporality.Cumulative
            : OtlpMetrics.AggregationTemporality.Delta;
        Assert.Equal(otlpAggregationTemporality, actual.Histogram.AggregationTemporality);

        Assert.Single(actual.Histogram.DataPoints);
        var dataPoint = actual.Histogram.DataPoints.First();
        Assert.True(dataPoint.StartTimeUnixNano > 0);
        Assert.True(dataPoint.TimeUnixNano > 0);

        Assert.Equal(1UL, dataPoint.Count);

        // Known issue: Negative measurements affect the Sum. Per the spec, they should not.
        if (longValue.HasValue)
        {
            Assert.Equal((double)longValue, dataPoint.Sum);
        }
        else
        {
            Assert.Equal(doubleValue, dataPoint.Sum);
        }

        int bucketIndex;
        for (bucketIndex = 0; bucketIndex < dataPoint.ExplicitBounds.Count; ++bucketIndex)
        {
            if (dataPoint.Sum <= dataPoint.ExplicitBounds[bucketIndex])
            {
                break;
            }

            Assert.Equal(0UL, dataPoint.BucketCounts[bucketIndex]);
        }

        Assert.Equal(1UL, dataPoint.BucketCounts[bucketIndex]);

        if (attributes.Length > 0)
        {
            OtlpTestHelpers.AssertOtlpAttributes(attributes, dataPoint.Attributes);
        }
        else
        {
            Assert.Empty(dataPoint.Attributes);
        }

        VerifyExemplars(null, longValue ?? doubleValue, enableExemplars, d => d.Exemplars.FirstOrDefault(), dataPoint);
    }

    [Theory]
    [InlineData("cuMulative", MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("DeltA", MetricReaderTemporalityPreference.Delta)]
    [InlineData("invalid", MetricReaderTemporalityPreference.Cumulative)]
    public void TestTemporalityPreferenceUsingConfiguration(string configValue, MetricReaderTemporalityPreference expectedTemporality)
    {
        var testExecuted = false;

        var configData = new Dictionary<string, string?> { [OtlpSpecConfigDefinitionTests.MetricsData.TemporalityKeyName] = configValue };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);

                services.PostConfigure<MetricReaderOptions>(o =>
                {
                    testExecuted = true;
                    Assert.Equal(expectedTemporality, o.TemporalityPreference);
                });
            })
            .AddOtlpExporter()
            .Build();

        Assert.True(testExecuted);
    }

    [Theory]
    [InlineData("cuMulative", MetricReaderTemporalityPreference.Cumulative)]
    [InlineData("DeltA", MetricReaderTemporalityPreference.Delta)]
    [InlineData("invalid", MetricReaderTemporalityPreference.Cumulative)]
    public void TestTemporalityPreferenceUsingEnvVar(string configValue, MetricReaderTemporalityPreference expectedTemporality)
    {
        Environment.SetEnvironmentVariable(OtlpSpecConfigDefinitionTests.MetricsData.TemporalityKeyName, configValue);

        var testExecuted = false;

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.PostConfigure<MetricReaderOptions>(o =>
                {
                    testExecuted = true;
                    Assert.Equal(expectedTemporality, o.TemporalityPreference);
                });
            })
            .AddOtlpExporter()
            .Build();

        Assert.True(testExecuted);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ToOtlpExemplarTests(bool enableTagFiltering, bool enableTracing)
    {
        ActivitySource? activitySource = null;
        Activity? activity = null;
        TracerProvider? tracerProvider = null;

        using var meter = new Meter(Utils.GetCurrentMethodName());

        var exportedItems = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(i =>
            {
                return !enableTagFiltering
                    ? null
                    : new MetricStreamConfiguration
                    {
                        TagKeys = Array.Empty<string>(),
                    };
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        if (enableTracing)
        {
            activitySource = new ActivitySource(Utils.GetCurrentMethodName());
            tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySource.Name)
                .SetSampler(new AlwaysOnSampler())
                .Build();

            activity = activitySource.StartActivity("testActivity");
        }

        var counterDouble = meter.CreateCounter<double>("testCounterDouble");
        var counterLong = meter.CreateCounter<long>("testCounterLong");

        counterDouble.Add(1.18D, new KeyValuePair<string, object?>("key1", "value1"));
        counterLong.Add(18L, new KeyValuePair<string, object?>("key1", "value1"));

        meterProvider.ForceFlush();

        var batch = new Batch<Metric>(exportedItems.ToArray(), exportedItems.Count);
        var request = CreateMetricExportRequest(batch, ResourceBuilder.CreateEmpty().Build());

        Assert.Single(request.ResourceMetrics);
        var resourceMetric = request.ResourceMetrics.First();
        var otlpResource = resourceMetric.Resource;

        Assert.Single(resourceMetric.ScopeMetrics);
        var instrumentationLibraryMetrics = resourceMetric.ScopeMetrics.First();
        Assert.Equal(meter.Name, instrumentationLibraryMetrics.Scope.Name);

        var scopeMetrics = resourceMetric.ScopeMetrics.Single();
        var otlpCounterDoubleMetric = scopeMetrics.Metrics.Single(m => m.Name == counterDouble.Name);
        var otlpCounterLongMetric = scopeMetrics.Metrics.Single(m => m.Name == counterLong.Name);

        AssertExemplars(1.18D, otlpCounterDoubleMetric);
        AssertExemplars(18L, otlpCounterLongMetric);

        activity?.Dispose();
        tracerProvider?.Dispose();
        activitySource?.Dispose();

        void AssertExemplars<T>(T value, OtlpMetrics.Metric metric)
            where T : struct
        {
            Assert.Single(metric.Sum.DataPoints);
            var dataPoint = metric.Sum.DataPoints.First();
            var otlpExemplar = dataPoint.Exemplars.First();

            Assert.NotEqual(default, otlpExemplar.TimeUnixNano);
            if (!enableTracing)
            {
                Assert.Equal(ByteString.Empty, otlpExemplar.TraceId);
                Assert.Equal(ByteString.Empty, otlpExemplar.SpanId);
            }
            else
            {
                byte[] traceIdBytes = new byte[16];
                Assert.NotNull(activity);
                activity.TraceId.CopyTo(traceIdBytes);

                byte[] spanIdBytes = new byte[8];
                activity.SpanId.CopyTo(spanIdBytes);

                Assert.Equal(ByteString.CopyFrom(traceIdBytes), otlpExemplar.TraceId);
                Assert.Equal(ByteString.CopyFrom(spanIdBytes), otlpExemplar.SpanId);
            }

            if (typeof(T) == typeof(long))
            {
                Assert.Equal((long)(object)value, otlpExemplar.AsInt);
            }
            else if (typeof(T) == typeof(double))
            {
                Assert.Equal((double)(object)value, otlpExemplar.AsDouble);
            }
            else
            {
                Assert.Fail("Unexpected type");
            }

            if (!enableTagFiltering)
            {
                var tagEnumerator = otlpExemplar.FilteredAttributes.GetEnumerator();
                Assert.False(tagEnumerator.MoveNext());
            }
            else
            {
                var tagEnumerator = otlpExemplar.FilteredAttributes.GetEnumerator();
                Assert.True(tagEnumerator.MoveNext());

                var tag = tagEnumerator.Current;
                Assert.Equal("key1", tag.Key);
                Assert.Equal("value1", tag.Value.StringValue);
            }
        }
    }

    [Fact]
    public void MetricsSerialization_ExpandsBufferForMetricsAndSerializes()
    {
        var metrics = new List<Metric>();

        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        using var meter = new Meter(name: {Utils.GetCurrentMethodName(), version: "0.0.1", tags: meterTags);
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metrics)
            .Build();

        var counter = meter.CreateCounter<int>("counter");
        counter.Add(100);

        provider.ForceFlush();

        var batch = new Batch<Metric>(metrics.ToArray(), metrics.Count);

        var buffer = new byte[50];
        var writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, ResourceBuilder.CreateEmpty().Build(), in batch);
        using var stream = new MemoryStream(buffer, 0, writePosition);

        var metricsData = OtlpMetrics.MetricsData.Parser.ParseFrom(stream);

        var request = new OtlpCollector.ExportMetricsServiceRequest();
        request.ResourceMetrics.Add(metricsData.ResourceMetrics);

        Assert.True(buffer.Length > 50);

        Assert.Single(request.ResourceMetrics);
        var resourceMetric = request.ResourceMetrics.First();

        Assert.Single(resourceMetric.ScopeMetrics);
        var instrumentationLibraryMetrics = resourceMetric.ScopeMetrics.First();
        Assert.Equal(string.Empty, instrumentationLibraryMetrics.SchemaUrl);
        Assert.Equal(meter.Name, instrumentationLibraryMetrics.Scope.Name);
        Assert.Equal("0.0.1", instrumentationLibraryMetrics.Scope.Version);

        Assert.Equal(2, instrumentationLibraryMetrics.Scope.Attributes.Count);
        Assert.Contains(instrumentationLibraryMetrics.Scope.Attributes, (kvp) => kvp.Key == "key1" && kvp.Value.StringValue == "value1");
        Assert.Contains(instrumentationLibraryMetrics.Scope.Attributes, (kvp) => kvp.Key == "key2" && kvp.Value.StringValue == "value2");
    }

    public void Dispose()
    {
        OtlpSpecConfigDefinitionTests.ClearEnvVars();
        GC.SuppressFinalize(this);
    }

    private static void VerifyExemplars<T>(long? longValue, double? doubleValue, bool enableExemplars, Func<T, OtlpMetrics.Exemplar?> getExemplarFunc, T state)
    {
        var exemplar = getExemplarFunc(state);

        if (enableExemplars)
        {
            Assert.NotNull(exemplar);
            Assert.NotEqual(default, exemplar.TimeUnixNano);
            if (longValue.HasValue)
            {
                Assert.Equal(longValue.Value, exemplar.AsInt);
            }
            else
            {
                Assert.NotNull(doubleValue);
                Assert.Equal(doubleValue.Value, exemplar.AsDouble);
            }
        }
        else
        {
            Assert.Null(exemplar);
        }
    }

    private static OtlpCollector.ExportMetricsServiceRequest CreateMetricExportRequest(in Batch<Metric> batch, Resource resource)
    {
        var buffer = new byte[4096];
        var writePosition = ProtobufOtlpMetricSerializer.WriteMetricsData(ref buffer, 0, resource, in batch);
        using var stream = new MemoryStream(buffer, 0, writePosition);

        var metricsData = OtlpMetrics.MetricsData.Parser.ParseFrom(stream);

        var request = new OtlpCollector.ExportMetricsServiceRequest();
        request.ResourceMetrics.Add(metricsData.ResourceMetrics);
        return request;
    }
}
