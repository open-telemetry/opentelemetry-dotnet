// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PrometheusHttpListenerMeterProviderBuilderExtensionsTests
{
    [Fact]
    public void TestAddPrometheusHttpListener_NamedOptions()
    {
        var defaultExporterOptionsConfigureOptionsInvocations = 0;
        var namedExporterOptionsConfigureOptionsInvocations = 0;

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<PrometheusHttpListenerOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<PrometheusHttpListenerOptions>("Exporter2", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddPrometheusHttpListener()
            .AddPrometheusHttpListener("Exporter2", o => o.ScrapeEndpointPath = "/metrics2")
            .Build();

        Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
    }

    [Fact]
    public void TestAddPrometheusHttpListener_UsesConfiguredCacheDuration()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddPrometheusHttpListener(options => options.ScrapeResponseCacheDurationMilliseconds = 123)
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(meterProvider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle
        Assert.Equal(123, exporter!.ScrapeResponseCacheDurationMilliseconds);
    }
}
