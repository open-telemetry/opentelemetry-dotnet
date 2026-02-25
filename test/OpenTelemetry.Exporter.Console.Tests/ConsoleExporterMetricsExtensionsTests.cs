// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleExporterMetricsExtensionsTests
{
    [Fact]
    public void AddConsoleExporter_WithNoParameters_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter()
            .Build();

        // Act
        counter.Add(100);

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithConfigureAction_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter((options) => options.Targets = ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        counter.Add(100);

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithNameAndConfigureAction_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        counter.Add(100);

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithNameAndNullConfigure_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter("custom-name", (Action<ConsoleExporterOptions>?)null)
            .Build();

        // Act
        counter.Add(100);

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_ExportsMetric_Success()
    {
        // Arrange
        var meterName = Utils.GetCurrentMethodName();
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddConsoleExporter((options) => options.Targets = ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        counter.Add(100);
        meterProvider!.ForceFlush();

        // Assert
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddConsoleExporter_ThrowsOnNullBuilder()
    {
        // Act and Assert
        MeterProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddConsoleExporter());
    }
}
