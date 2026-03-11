// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleExporterTracerExtensionsTests
{
    [Fact]
    public void AddConsoleExporter_WithNoParameters_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddConsoleExporter()
            .Build();

        // Act
        using (activitySource.StartActivity("TestActivity"))
        {
            // No-op
        }

        // Assert
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithConfigureAction_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddConsoleExporter((options) => options.Targets = ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        using (activitySource.StartActivity("TestActivity"))
        {
            // No-op
        }

        // Assert
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithNameAndConfigureAction_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddConsoleExporter("custom-name", (options) => options.Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        using (activitySource.StartActivity("TestActivity"))
        {
            // No-op
        }

        // Assert
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddConsoleExporter_WithNameOnly_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddConsoleExporter("custom-name", null)
            .Build();

        // Act
        using (activitySource.StartActivity("TestActivity"))
        {
            // No-op
        }

        // Assert
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddConsoleExporter_ExportsActivity_Success()
    {
        // Arrange
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(name)
            .AddConsoleExporter((options) => options.Targets = ConsoleExporterOutputTargets.Debug)
            .Build();

        // Act
        using (var activity = activitySource.StartActivity("TestActivity"))
        {
            activity?.SetTag("test.key", "test.value");
        }

        // Assert
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddConsoleExporter_ThrowsOnNullBuilder()
    {
        // Act and Assert
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddConsoleExporter());
    }
}
