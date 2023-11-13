// <copyright file="OpenTelemetryMetricsBuilderExtensionsTests.cs" company="OpenTelemetry Authors">
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
    public void EnableMetricsTest(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = new();

        using (var host = MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<long>("TestCounter");
            counter.Add(1);
        }

        AssertSingleMetricWithLongSumOfOne(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnableMetricsWithAddMeterTest(bool useWithMetricsStyle)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = new();

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

        AssertSingleMetricWithLongSumOfOne(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReloadOfMetricsViaIConfigurationTest(bool useWithMetricsStyle)
    {
        using var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log);

        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = new();

        var source = new MemoryConfigurationSource();
        var memory = new MemoryConfigurationProvider(source);
        var configuration = new ConfigurationRoot(new[] { memory });

        using var host = MetricTestsBase.BuildHost(
            useWithMetricsStyle,
            configureAppConfiguration: (context, builder) => builder.AddConfiguration(configuration),
            configureMeterProviderBuilder: builder => builder
                .AddInMemoryExporter(exportedItems, reader => reader.TemporalityPreference = MetricReaderTemporalityPreference.Delta));

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

        AssertSingleMetricWithLongSumOfOne(exportedItems);

        exportedItems.Clear();

        memory.Set($"Metrics:EnabledMetrics:{meter.Name}:Default", "false");

        configuration.Reload();

        counter.Add(1);

        meterProvider.ForceFlush();

        Assert.Empty(exportedItems);

        memory.Set($"Metrics:OpenTelemetry:EnabledMetrics:{meter.Name}:Default", "true");

        configuration.Reload();

        counter.Add(1);

        meterProvider.ForceFlush();

        AssertSingleMetricWithLongSumOfOne(exportedItems);

        var duplicateMetricInstrumentEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 38);

        Assert.Empty(duplicateMetricInstrumentEvents);

        var metricInstrumentDeactivatedEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 52);

        Assert.Single(metricInstrumentDeactivatedEvents);

        var metricInstrumentReactivatedEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 53);

        Assert.Single(metricInstrumentReactivatedEvents);
    }

    private static void AssertSingleMetricWithLongSumOfOne(List<Metric> exportedItems)
    {
        Assert.Single(exportedItems);

        List<MetricPoint> metricPoints = new();
        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);

        var metricPoint = metricPoints[0];
        Assert.Equal(1, metricPoint.GetSumLong());
    }
}
