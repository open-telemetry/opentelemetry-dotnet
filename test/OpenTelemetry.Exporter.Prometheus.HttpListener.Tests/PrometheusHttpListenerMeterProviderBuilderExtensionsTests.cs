// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    public void TestAddPrometheusHttpListener_Defaults_Are_Correct()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddPrometheusHttpListener()
            .Build();

        var serviceProvider = meterProvider.GetServiceProvider();

        Assert.NotNull(serviceProvider);

        var options = serviceProvider.GetRequiredService<IOptionsMonitor<PrometheusHttpListenerOptions>>();

        Assert.NotNull(options);
        Assert.NotNull(options.CurrentValue);

        Assert.Equal("localhost", options.CurrentValue.Host);
        Assert.Equal(9464, options.CurrentValue.Port);
        Assert.Equal("/metrics", options.CurrentValue.ScrapeEndpointPath);
    }

    [Fact]
    public void TestAddPrometheusHttpListener_Configuration_From_Environment_Variables()
    {
        using (new EnvironmentVariableScope("OTEL_EXPORTER_PROMETHEUS_HOST", "127.0.0.1"))
        using (new EnvironmentVariableScope("OTEL_EXPORTER_PROMETHEUS_PORT", "4649"))
        {
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddPrometheusHttpListener()
                .Build();

            var serviceProvider = meterProvider.GetServiceProvider();

            Assert.NotNull(serviceProvider);

            var options = serviceProvider.GetRequiredService<IOptionsMonitor<PrometheusHttpListenerOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.CurrentValue);

            Assert.Equal("127.0.0.1", options.CurrentValue.Host);
            Assert.Equal(4649, options.CurrentValue.Port);
            Assert.Equal("/metrics", options.CurrentValue.ScrapeEndpointPath);
        }
    }

    [Fact]
    public void TestAddPrometheusHttpListener_Manual_Configuration_Overrides_Environment_Variables()
    {
        using (new EnvironmentVariableScope("OTEL_EXPORTER_PROMETHEUS_HOST", "prometheus.local"))
        using (new EnvironmentVariableScope("OTEL_EXPORTER_PROMETHEUS_PORT", "4649"))
        {
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddPrometheusHttpListener((options) =>
                {
                    options.Host = "127.0.0.1";
                    options.Port = 5464;
                    options.ScrapeEndpointPath = "/custom-metrics";
                })
                .Build();

            var serviceProvider = meterProvider.GetServiceProvider();

            Assert.NotNull(serviceProvider);

            var options = serviceProvider.GetRequiredService<IOptionsMonitor<PrometheusHttpListenerOptions>>();

            Assert.NotNull(options);
            Assert.NotNull(options.CurrentValue);

            Assert.Equal("127.0.0.1", options.CurrentValue.Host);
            Assert.Equal(5464, options.CurrentValue.Port);
            Assert.Equal("/custom-metrics", options.CurrentValue.ScrapeEndpointPath);
        }
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
