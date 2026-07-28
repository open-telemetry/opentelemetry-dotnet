// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus.AspNetCore.Tests;

public sealed class PrometheusExporterMeterProviderBuilderExtensionsTests
{
    [Fact]
    public void TestAddPrometheusExporter_NamedOptions()
    {
        var defaultExporterOptionsConfigureOptionsInvocations = 0;
        var namedExporterOptionsConfigureOptionsInvocations = 0;

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

    [Fact]
    public void TranslationStrategy_DefaultsToUnderscoreEscapingWithSuffixes()
        => Assert.Equal(PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes, new PrometheusAspNetCoreOptions().TranslationStrategy);

    [Fact]
    public void TranslationStrategy_DelegatesToExporterOptions()
    {
        var options = new PrometheusAspNetCoreOptions
        {
            TranslationStrategy = PrometheusTranslationStrategy.NoTranslation,
        };

        Assert.Equal(PrometheusTranslationStrategy.NoTranslation, options.ExporterOptions.TranslationStrategy);
    }

    [Fact]
    public void DisableTotalNameSuffixForCounters_DelegatesToExporterOptions()
    {
        var options = new PrometheusAspNetCoreOptions { DisableTotalNameSuffixForCounters = true };

        Assert.True(options.ExporterOptions.DisableTotalNameSuffixForCounters);
        Assert.True(options.DisableTotalNameSuffixForCounters);
    }

    [Fact]
    public void ScopeInfoEnabled_DelegatesToExporterOptions()
    {
        var options = new PrometheusAspNetCoreOptions { ScopeInfoEnabled = false };

        Assert.False(options.ExporterOptions.ScopeInfoEnabled);
        Assert.False(options.ScopeInfoEnabled);
    }

    [Fact]
    public void ScrapeResponseCacheDurationMilliseconds_DelegatesToExporterOptions()
    {
        var options = new PrometheusAspNetCoreOptions { ScrapeResponseCacheDurationMilliseconds = 42 };

        Assert.Equal(42, options.ExporterOptions.ScrapeResponseCacheDurationMilliseconds);
        Assert.Equal(42, options.ScrapeResponseCacheDurationMilliseconds);
    }

    [Fact]
    public void TargetInfoEnabled_DelegatesToExporterOptions()
    {
        var options = new PrometheusAspNetCoreOptions { TargetInfoEnabled = false };

        Assert.False(options.ExporterOptions.TargetInfoEnabled);
        Assert.False(options.TargetInfoEnabled);
    }

    [Fact]
    public void ResourceConstantLabels_DelegatesToExporterOptions()
    {
        Func<string, bool> predicate = static _ => true;

        var options = new PrometheusAspNetCoreOptions { ResourceConstantLabels = predicate };

        Assert.Same(predicate, options.ExporterOptions.ResourceConstantLabels);
        Assert.Same(predicate, options.ResourceConstantLabels);
    }

    [Fact]
    public void MaxScrapeResponseSizeBytes_DelegatesToExporterOptions()
    {
        const int Value = PrometheusExporterOptions.InitialScrapeResponseSizeBytes * 4;

        var options = new PrometheusAspNetCoreOptions { MaxScrapeResponseSizeBytes = Value };

        Assert.Equal(Value, options.ExporterOptions.MaxScrapeResponseSizeBytes);
        Assert.Equal(Value, options.MaxScrapeResponseSizeBytes);
    }
}
