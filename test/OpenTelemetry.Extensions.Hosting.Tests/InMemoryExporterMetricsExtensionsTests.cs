// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Diagnostics.Metrics;
using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests;

/// <summary>
/// These tests verify that <see cref="InMemoryExporter"/> works with <see cref="IDeferredMeterProviderBuilder"/>.
/// </summary>
public class InMemoryExporterMetricsExtensionsTests
{
    [Fact]
    public async Task DeferredMeterProviderBuilder_WithMetric()
    {
        var meterName = Utils.GetCurrentMethodName();
        var exportedItems = new List<Metric>();

        await RunMetricsTest(
            configure: builder => builder
                .AddMeter(meterName)
                .AddInMemoryExporter(exportedItems),
            testAction: () =>
            {
                using var meter = new Meter(meterName);
                var counter = meter.CreateCounter<long>("meter");
                counter.Add(10);
            });

        Assert.Single(exportedItems);
        var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();
        Assert.True(metricPointsEnumerator.MoveNext());
        Assert.Equal(10, metricPointsEnumerator.Current.GetSumLong());
    }

    [Fact]
    public async Task DeferredMeterProviderBuilder_WithMetricSnapshot()
    {
        var meterName = Utils.GetCurrentMethodName();
        var exportedItems = new List<MetricSnapshot>();

        await RunMetricsTest(
            configure: builder => builder
                .AddMeter(meterName)
                .AddInMemoryExporter(exportedItems),
            testAction: () =>
            {
                using var meter = new Meter(meterName);
                var counter = meter.CreateCounter<long>("meter");
                counter.Add(10);
            });

        Assert.Single(exportedItems);
        Assert.Equal(10, exportedItems[0].MetricPoints[0].GetSumLong());
    }

    private static async Task RunMetricsTest(Action<MeterProviderBuilder> configure, Action testAction)
    {
        using var host = await new HostBuilder()
           .ConfigureWebHost(webBuilder => webBuilder
               .UseTestServer()
               .ConfigureServices(services => services.AddOpenTelemetry().WithMetrics(configure))
               .Configure(app => app.Run(httpContext =>
               {
                   testAction.Invoke();

                   var meterProvider = app.ApplicationServices.GetRequiredService<MeterProvider>();
                   meterProvider.ForceFlush();

                   return Task.CompletedTask;
               })))
           .StartAsync();

        using var response = await host.GetTestClient().GetAsync(new Uri($"/{nameof(RunMetricsTest)}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await host.StopAsync();
    }
}
#endif
