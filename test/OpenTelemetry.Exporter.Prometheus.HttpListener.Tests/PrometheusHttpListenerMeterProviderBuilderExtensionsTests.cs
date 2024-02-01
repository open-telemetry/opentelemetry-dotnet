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
        int defaultExporterOptionsConfigureOptionsInvocations = 0;
        int namedExporterOptionsConfigureOptionsInvocations = 0;

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
}
