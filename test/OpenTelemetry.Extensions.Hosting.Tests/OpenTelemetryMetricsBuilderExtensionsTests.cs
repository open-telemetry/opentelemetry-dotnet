// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Tests;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

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

        using (var host = MetricTestsBase.BuildHost(
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

        using (var host = MetricTestsBase.BuildHost(
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

        using (var host = MetricTestsBase.BuildHost(
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

        using (var host = MetricTestsBase.BuildHost(
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

        using (var host = MetricTestsBase.BuildHost(
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

        using (var host = MetricTestsBase.BuildHost(
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

        using (var host = MetricTestsBase.BuildHost(
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
        using var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log);

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

        var duplicateMetricInstrumentEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 38);

        // Note: We currently log a duplicate warning anytime a metric is reactivated.
        Assert.Single(duplicateMetricInstrumentEvents);

        var metricInstrumentDeactivatedEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 52);

        Assert.Single(metricInstrumentDeactivatedEvents);

        var metricInstrumentRemovedEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 53);

        Assert.Single(metricInstrumentRemovedEvents);
    }

    [Theory]
    [InlineData(false, MetricReaderTemporalityPreference.Delta)]
    [InlineData(true, MetricReaderTemporalityPreference.Delta)]
    [InlineData(false, MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(true, MetricReaderTemporalityPreference.Cumulative)]
    public void ReloadOfMetricsViaIConfigurationWithoutExportCleanupTest(bool useWithMetricsStyle, MetricReaderTemporalityPreference temporalityPreference)
    {
        using var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log);

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

        var duplicateMetricInstrumentEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 38);

        // Note: We currently log a duplicate warning anytime a metric is reactivated.
        Assert.Single(duplicateMetricInstrumentEvents);

        var metricInstrumentDeactivatedEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 52);

        Assert.Single(metricInstrumentDeactivatedEvents);

        var metricInstrumentRemovedEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 53);

        Assert.Single(metricInstrumentRemovedEvents);
    }

    [Fact]
    public void WhenOpenTelemetrySdkIsDisabledExceptionNotThrown()
    {
        // Arrange
        string meterName = "TestMeter";

        using (new EnvironmentVariableScope("OTEL_SDK_DISABLED", "true"))
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
        Assert.Single(exportedItems);

        AssertMetricWithLongSum(exportedItems[0], expectedValue);
    }

    private static void AssertMetricWithLongSum(Metric metric, long expectedValue = 1)
    {
        List<MetricPoint> metricPoints = [];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);

        var metricPoint = metricPoints[0];
        Assert.Equal(expectedValue, metricPoint.GetSumLong());
    }

    private static void AssertSingleMetricWithDoubleSum(List<Metric> exportedItems, double expectedValue = 1)
    {
        Assert.Single(exportedItems);

        AssertMetricWithDoubleSum(exportedItems[0], expectedValue);
    }

    private static void AssertMetricWithDoubleSum(Metric metric, double expectedValue = 1)
    {
        List<MetricPoint> metricPoints = [];
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);

        var metricPoint = metricPoints[0];
        Assert.Equal(expectedValue, metricPoint.GetSumDouble());
    }
}
