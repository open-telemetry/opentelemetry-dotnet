// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Tests;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class OpenTelemetryMetricsBuilderExtensionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableMetricsTestByte(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<byte>("TestCounter");
            counter.Add(1);
        }

        AssertSingleMetricWithLongSum(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableMetricsTestShort(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<short>("TestCounter");
            counter.Add(1);
        }

        AssertSingleMetricWithLongSum(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableMetricsTestInt(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<int>("TestCounter");
            counter.Add(1);
        }

        AssertSingleMetricWithLongSum(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableMetricsTestLong(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<long>("TestCounter");
            counter.Add(1);
        }

        AssertSingleMetricWithLongSum(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableMetricsTestFloat(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<float>("TestCounter");
            counter.Add(1);
        }

        AssertSingleMetricWithDoubleSum(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableMetricsTestDouble(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<double>("TestCounter");
            counter.Add(1);
        }

        AssertSingleMetricWithDoubleSum(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableMetricsWithAddMeterTest(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder
                .AddSdkMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<long>("TestCounter");
            counter.Add(1);
        }

        AssertSingleMetricWithLongSum(exportedItems);
    }

    [Theory]
    [InlineData(false, MetricReaderTemporalityPreference.Delta)]
    [InlineData(true, MetricReaderTemporalityPreference.Delta)]
    [InlineData(false, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(true, MetricReaderTemporalityPreference.Cumulative)]
    public void ReloadOfMetricsViaIConfigurationWithExportCleanupTest(bool useWithMetricsStyle, MetricReaderTemporalityPreference temporalityPreference)
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        var source = new MemoryConfigurationSource();
        var memory = new MemoryConfigurationProvider(source);
        using var configuration = new ConfigurationRoot([memory]);

        using var host = MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureAppConfiguration: (context, builder) => builder.AddConfiguration(configuration),
            configureMeterProviderBuilder: builder => builder
                .AddInMemoryExporter(exportedItems, reader => reader.TemporalityPreference = temporalityPreference));

        var meterProvider = host.Services.GetRequiredService<MeterProvider>();
        var options = host.Services.GetRequiredService<IOptionsMonitor<MetricsOptions>>();

        var counter = meter.CreateCounter<long>("TestCounter");
        counter.Add(1);

        meterProvider.ForceFlush();

        Assert.Empty(exportedItems);

        memory.Set($"Metrics:EnabledMetrics:{meter.Name}:Default", "true");

        configuration.Reload();

        counter.Add(1);

        meterProvider.ForceFlush();

        AssertSingleMetricWithLongSum(exportedItems);

        exportedItems.Clear();

        memory.Set($"Metrics:EnabledMetrics:{meter.Name}:Default", "false");

        configuration.Reload();

        counter.Add(1);

        meterProvider.ForceFlush();

        if (temporalityPreference == MetricReaderTemporalityPreference.Cumulative)
        {
            // Note: When in Cumulative the metric shows up on the export
            // immediately after being deactivated and then is ignored.
            AssertSingleMetricWithLongSum(exportedItems);

            meterProvider.ForceFlush();
            exportedItems.Clear();
            Assert.Empty(exportedItems);
        }
        else
        {
            Assert.Empty(exportedItems);
        }

        memory.Set($"Metrics:OpenTelemetry:EnabledMetrics:{meter.Name}:Default", "true");

        configuration.Reload();

        counter.Add(1);

        meterProvider.ForceFlush();

        AssertSingleMetricWithLongSum(exportedItems);

        var metricInstrumentDeactivatedEvents = eventListener.Messages.Where((e) => e.EventId == 52);

        Assert.Single(metricInstrumentDeactivatedEvents);

        var metricInstrumentRemovedEvents = eventListener.Messages.Where((e) => e.EventId == 53);

        Assert.Single(metricInstrumentRemovedEvents);
    }

    [Theory]
    [InlineData(false, MetricReaderTemporalityPreference.Delta)]
    [InlineData(true, MetricReaderTemporalityPreference.Delta)]
    [InlineData(false, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(true, MetricReaderTemporalityPreference.Cumulative)]
    public void ReloadOfMetricsViaIConfigurationWithoutExportCleanupTest(bool useWithMetricsStyle, MetricReaderTemporalityPreference temporalityPreference)
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        var source = new MemoryConfigurationSource();
        var memory = new MemoryConfigurationProvider(source);
        memory.Set($"Metrics:EnabledMetrics:{meter.Name}:Default", "true");
        using var configuration = new ConfigurationRoot([memory]);

        using var host = MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureAppConfiguration: (context, builder) => builder.AddConfiguration(configuration),
            configureMeterProviderBuilder: builder => builder
                .AddInMemoryExporter(exportedItems, reader => reader.TemporalityPreference = temporalityPreference));

        var meterProvider = host.Services.GetRequiredService<MeterProvider>();
        var options = host.Services.GetRequiredService<IOptionsMonitor<MetricsOptions>>();

        var counter = meter.CreateCounter<long>("TestCounter");
        counter.Add(1);

        memory.Set($"Metrics:EnabledMetrics:{meter.Name}:Default", "false");
        configuration.Reload();
        counter.Add(1);

        memory.Set($"Metrics:EnabledMetrics:{meter.Name}:Default", "true");
        configuration.Reload();
        counter.Add(1);

        meterProvider.ForceFlush();

        // Note: We end up with 2 of the same metric being exported. This is
        // because the current behavior when something is deactivated is to
        // remove the metric. The next publish creates a new metric.
        Assert.Equal(2, exportedItems.Count);

        AssertMetricWithLongSum(exportedItems[0]);
        AssertMetricWithLongSum(exportedItems[1]);

        exportedItems.Clear();

        counter.Add(1);

        meterProvider.ForceFlush();

        AssertSingleMetricWithLongSum(
            exportedItems,
            expectedValue: temporalityPreference == MetricReaderTemporalityPreference.Delta ? 1 : 2);

        var duplicateMetricInstrumentEvents = eventListener.Messages.Where((e) => e.EventId == 38);

        // Note: We currently log a duplicate warning anytime a metric is reactivated.
        Assert.Single(duplicateMetricInstrumentEvents);

        var metricInstrumentDeactivatedEvents = eventListener.Messages.Where((e) => e.EventId == 52);

        Assert.Single(metricInstrumentDeactivatedEvents);

        var metricInstrumentRemovedEvents = eventListener.Messages.Where((e) => e.EventId == 53);

        Assert.Single(metricInstrumentRemovedEvents);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, ".*")]
    [InlineData(true, ".*")]
    public void EnableMetricsViaIConfigurationUsingPrefixAndDefaultRulesTest(bool useWithMetricsStyle, string ruleSuffix)
    {
        // Arrange
        var prefix = Utils.GetCurrentMethodName();
        var rule = prefix + ruleSuffix;

        using var enabledMeter = new Meter($"{prefix}.Enabled");
        using var disabledMeter = new Meter($"Disabled.{prefix}");
        List<Metric> exportedItems = [];

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureAppConfiguration: (context, builder) => builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Metrics:EnabledMetrics:Default"] = "false",
                [$"Metrics:EnabledMetrics:{rule}"] = "true",
            }),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            enabledMeter.CreateCounter<long>("EnabledCounter").Add(1);
            disabledMeter.CreateCounter<long>("DisabledCounter").Add(1);
        }

        // Assert
        var metric = Assert.Single(exportedItems);
        Assert.Equal("EnabledCounter", metric.Name);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void EnableMetricsViaIConfigurationAddConfigurationRequiresSectionTest(bool useWithMetricsStyle, bool passSection)
    {
        // Arrange
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = [];

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Metrics:EnabledMetrics:Default"] = "false",
                [$"Metrics:EnabledMetrics:{meter.Name}"] = "true",
            })
            .Build();

        using (MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.AddConfiguration(
                passSection ? configuration.GetSection("Metrics") : configuration),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            meter.CreateCounter<long>("TestCounter").Add(1);
        }

        // Assert
        if (passSection)
        {
            AssertSingleMetricWithLongSum(exportedItems);
        }
        else
        {
            Assert.Empty(exportedItems);
        }
    }

    [Fact]
    public void WhenOpenTelemetrySdkIsDisabledExceptionNotThrown()
    {
        // Arrange
        var meterName = "TestMeter";

        using (EnvironmentVariableScope.Create("OTEL_SDK_DISABLED", "true"))
        {
            var services = new ServiceCollection();

            services
                .AddOpenTelemetry()
                .WithMetrics((p) => p.AddMeter(meterName));

            // Act
            using var provider = services.BuildServiceProvider();

            // Assert - No-op implementation is in use
            var meterProvider = provider.GetRequiredService<MeterProvider>();
            Assert.IsType<OpenTelemetrySdk.NoopMeterProvider>(meterProvider);

            // Verify that no exception is thrown when using a meter
            using var meter = new Meter(meterName);
            var counter = meter.CreateCounter<long>("test_counter");
            counter.Add(1);

            meterProvider.ForceFlush();
        }
    }

    private static void AssertSingleMetricWithLongSum(List<Metric> exportedItems, long expectedValue = 1)
    {
        var item = Assert.Single(exportedItems);

        AssertMetricWithLongSum(item, expectedValue);
    }

    private static void AssertMetricWithLongSum(Metric metric, long expectedValue = 1)
    {
        List<MetricPoint> metricPoints = [];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        var metricPoint = Assert.Single(metricPoints);
        Assert.Equal(expectedValue, metricPoint.GetSumLong());
    }

    private static void AssertSingleMetricWithDoubleSum(List<Metric> exportedItems, double expectedValue = 1)
    {
        var item = Assert.Single(exportedItems);

        AssertMetricWithDoubleSum(item, expectedValue);
    }

    private static void AssertMetricWithDoubleSum(Metric metric, double expectedValue = 1)
    {
        List<MetricPoint> metricPoints = [];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        var metricPoint = Assert.Single(metricPoints);
        Assert.Equal(expectedValue, metricPoint.GetSumDouble());
    }
}
