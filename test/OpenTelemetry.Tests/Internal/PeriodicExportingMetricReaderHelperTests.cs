// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Internal.Tests;

public sealed class PeriodicExportingMetricReaderHelperTests : IDisposable
{
    public PeriodicExportingMetricReaderHelperTests()
    {
        ClearEnvVars();
    }

    public void Dispose()
    {
        ClearEnvVars();
    }

    [Fact]
    public void CreatePeriodicExportingMetricReader_Defaults()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = CreatePeriodicExportingMetricReader();
#pragma warning restore CA2000 // Dispose objects before losing scope

        Assert.Equal(60000, reader.ExportIntervalMilliseconds);
        Assert.Equal(30000, reader.ExportTimeoutMilliseconds);
        Assert.Equal(MetricReaderTemporalityPreference.Cumulative, reader.TemporalityPreference);
    }

    [Fact]
    public void CreatePeriodicExportingMetricReader_Defaults_WithTask()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var reader = CreatePeriodicExportingMetricReader(useThreads: false);
#pragma warning restore CA2000 // Dispose objects before losing scope

        Assert.Equal(60000, reader.ExportIntervalMilliseconds);
        Assert.Equal(30000, reader.ExportTimeoutMilliseconds);
        Assert.Equal(MetricReaderTemporalityPreference.Cumulative, reader.TemporalityPreference);
    }

    [Fact]
    public void CreatePeriodicExportingMetricReader_TemporalityPreference_FromOptions()
    {
        var value = MetricReaderTemporalityPreference.Delta;
        using var reader = CreatePeriodicExportingMetricReader(new()
        {
            TemporalityPreference = value,
        });

        Assert.Equal(value, reader.TemporalityPreference);
    }

    [Fact]
    public void CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromOptions()
    {
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey, "88888"); // should be ignored, as value set via options has higher priority
        var value = 123;
        using var reader = CreatePeriodicExportingMetricReader(new()
        {
            PeriodicExportingMetricReaderOptions = new()
            {
                ExportIntervalMilliseconds = value,
            },
        });

        Assert.Equal(value, reader.ExportIntervalMilliseconds);
    }

    [Fact]
    public void CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromOptions()
    {
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey, "99999"); // should be ignored, as value set via options has higher priority
        var value = 456;
        using var reader = CreatePeriodicExportingMetricReader(new()
        {
            PeriodicExportingMetricReaderOptions = new()
            {
                ExportTimeoutMilliseconds = value,
            },
        });

        Assert.Equal(value, reader.ExportTimeoutMilliseconds);
    }

    [Fact]
    public void CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromEnvVar()
    {
        var value = 789;
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey, value.ToString(CultureInfo.InvariantCulture));
        using var reader = CreatePeriodicExportingMetricReader();

        Assert.Equal(value, reader.ExportIntervalMilliseconds);
    }

    [Fact]
    public void CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromEnvVar()
    {
        var value = 246;
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey, value.ToString(CultureInfo.InvariantCulture));
        using var reader = CreatePeriodicExportingMetricReader();

        Assert.Equal(value, reader.ExportTimeoutMilliseconds);
    }

    [Fact]
    public void CreatePeriodicExportingMetricReader_FromIConfiguration()
    {
        var values = new Dictionary<string, string?>()
        {
            [PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey] = "18",
            [PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey] = "19",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new PeriodicExportingMetricReaderOptions(configuration);

        Assert.Equal(18, options.ExportIntervalMilliseconds);
        Assert.Equal(19, options.ExportTimeoutMilliseconds);
    }

    [Fact]
    public void EnvironmentVariableNames()
    {
        Assert.Equal("OTEL_METRIC_EXPORT_INTERVAL", PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey);
        Assert.Equal("OTEL_METRIC_EXPORT_TIMEOUT", PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey);
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportIntervalEnvVarKey, null);
        Environment.SetEnvironmentVariable(PeriodicExportingMetricReaderOptions.OTelMetricExportTimeoutEnvVarKey, null);
    }

    private static PeriodicExportingMetricReader CreatePeriodicExportingMetricReader(
        MetricReaderOptions? options = null, bool useThreads = true)
    {
        options ??= new();

#pragma warning disable CA2000 // Dispose objects before losing scope
        var dummyMetricExporter = new InMemoryExporter<Metric>(Array.Empty<Metric>());
#pragma warning restore CA2000 // Dispose objects before losing scope
        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(dummyMetricExporter, options, useThreads: useThreads);
    }
}
