// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleExporterOptionsTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var options = new ConsoleExporterOptions();

        // Assert
        Assert.Equal(ConsoleExporterOutputTargets.Console, options.Targets);
    }

    [Fact]
    public void CanSetTargets_ToConsole()
    {
        // Act
        var options = new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Console,
        };

        // Assert
        Assert.Equal(ConsoleExporterOutputTargets.Console, options.Targets);
    }

    [Fact]
    public void CanSetTargets_ToDebug()
    {
        // Act
        var options = new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Debug,
        };

        // Assert
        Assert.Equal(ConsoleExporterOutputTargets.Debug, options.Targets);
    }

    [Fact]
    public void CanSetTargets_ToBoth()
    {
        // Act
        var options = new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug,
        };

        // Assert
        Assert.Equal(ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug, options.Targets);
    }

    [Fact]
    public void CanCheckTargets_HasFlag_Console()
    {
        // Act
        var options = new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Console,
        };

        // Assert
        Assert.True(options.Targets.HasFlag(ConsoleExporterOutputTargets.Console));
        Assert.False(options.Targets.HasFlag(ConsoleExporterOutputTargets.Debug));
    }

    [Fact]
    public void CanCheckTargets_HasFlag_Debug()
    {
        // Act
        var options = new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Debug,
        };

        // Assert
        Assert.False(options.Targets.HasFlag(ConsoleExporterOutputTargets.Console));
        Assert.True(options.Targets.HasFlag(ConsoleExporterOutputTargets.Debug));
    }

    [Fact]
    public void CanCheckTargets_HasFlag_Both()
    {
        // Act
        var options = new ConsoleExporterOptions
        {
            Targets = ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug,
        };

        // Assert
        Assert.True(options.Targets.HasFlag(ConsoleExporterOutputTargets.Console));
        Assert.True(options.Targets.HasFlag(ConsoleExporterOutputTargets.Debug));
    }
}
