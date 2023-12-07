// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.AspNetCore.Tests;

public sealed class PrometheusExporterMeterProviderBuilderExtensionsTests
{
    [Fact]
    public void TestAddPrometheusExporter_NamedOptions()
    {
        int defaultExporterOptionsConfigureOptionsInvocations = 0;
        int namedExporterOptionsConfigureOptionsInvocations = 0;

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<PrometheusAspNetCoreOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<PrometheusAspNetCoreOptions>("Exporter2", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddPrometheusExporter()
            .AddPrometheusExporter("Exporter2", o => { })
            .Build();

        Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
    }
}