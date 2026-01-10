// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests.Compact;

public class CompactMetricFormatterTests
{
    [Fact]
    public async Task BasicMetricOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();

        using var meter = new Meter("TestMeter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice"))
            .AddMeter(meter.Name) // All instruments from this meter are enabled.
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Formatter = "compact";
                    exporterOptions.TimestampFormat = string.Empty;
                    exporterOptions.Console = mockConsole;

                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 100;
                    metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
            .Build();

        // Act
        var counter = meter.CreateCounter<int>("counter", "things", "A count of things");

        counter?.Add(10);
        await Task.Delay(150);

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Metric: {0}", output);

        // Contains trace ID and span ID
        Assert.Matches(@"^METRIC \[counter\]", output);

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var values0 = lines[0].Split(' ');
        Assert.Contains("0s", values0[2], StringComparison.InvariantCulture);
        Assert.Contains("unit=things", values0[3], StringComparison.InvariantCulture);
        Assert.Contains("sum=10", values0[4], StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task TaggedMetricOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();

        using var meter = new Meter("TestMeter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice"))
            .AddMeter(meter.Name) // All instruments from this meter are enabled.
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Formatter = "compact";
                    exporterOptions.TimestampFormat = string.Empty;
                    exporterOptions.Console = mockConsole;

                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 100;
                    metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                })
            .Build();

        // Act
        var counter = meter.CreateCounter<int>("counter", "things", "A count of things");

        counter?.Add(
            100,
            new KeyValuePair<string, object?>("tag1", "value1"));
        await Task.Delay(150);

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Metric: {0}", output);

        // Contains trace ID and span ID
        Assert.Matches(@"^METRIC \[counter\]", output);

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var values0 = lines[0].Split(' ');
        Assert.Contains("0s", values0[2], StringComparison.InvariantCulture);
        Assert.Contains("unit=things", values0[3], StringComparison.InvariantCulture);
        Assert.Contains("tag1=value1", values0[4], StringComparison.InvariantCulture);
        Assert.Contains("sum=100", values0[5], StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task CumulativeMeter()
    {
        // Arrange
        var mockConsole = new MockConsole();

        using var meter = new Meter("TestMeter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice"))
            .AddMeter(meter.Name) // All instruments from this meter are enabled.
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Formatter = "compact";
                    exporterOptions.Console = mockConsole;

                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                    metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
            .Build();

        // Act
        var counter = meter.CreateCounter<int>("counter", "things", "A count of things");

        await Task.Delay(600);

        counter?.Add(
            100,
            new KeyValuePair<string, object?>("tag1", "value1"));
        await Task.Delay(50);

        counter?.Add(
            100,
            new KeyValuePair<string, object?>("tag1", "value1"));
        await Task.Delay(350);

        await Task.Delay(600);
        counter?.Add(
            100,
            new KeyValuePair<string, object?>("tag1", "value1"));
        await Task.Delay(50);

        counter?.Add(
            100,
            new KeyValuePair<string, object?>("tag1", "value1"));
        await Task.Delay(350);

        await Task.Delay(600);

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Metric: {0}", output);

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var line200 = lines.FirstOrDefault(x => x.Contains("sum=200", StringComparison.InvariantCulture));
        Assert.NotNull(line200);
        var values200 = line200.Split(' ');
        Assert.Contains("1s", values200[3], StringComparison.InvariantCulture);

        var line400 = lines.FirstOrDefault(x => x.Contains("sum=400", StringComparison.InvariantCulture));
        Assert.NotNull(line400);
        var values400 = line400.Split(' ');
        Assert.Contains("2s", values400[3], StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task HistogramOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();

        using var meter = new Meter("TestMeter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice"))
            .AddMeter(meter.Name) // All instruments from this meter are enabled.
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Formatter = "compact";
                    exporterOptions.TimestampFormat = string.Empty;
                    exporterOptions.Console = mockConsole;

                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 100;
                    metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
            .Build();

        // Act
        var histogram = meter.CreateHistogram<int>("histogram"); // No Unit

        histogram?.Record(
            50,
            new KeyValuePair<string, object?>("tag1", "value1"));
        histogram?.Record(
            100,
            new KeyValuePair<string, object?>("tag1", "value1"));
        histogram?.Record(
            100,
            new KeyValuePair<string, object?>("tag1", "value1"));
        histogram?.Record(
            150,
            new KeyValuePair<string, object?>("tag1", "value1"));

        await Task.Delay(150);

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Metric: {0}", output);

        // Contains trace ID and span ID
        Assert.Matches(@"^METRIC \[histogram\]", output);

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var values0 = lines[0].Split(' ');
        Assert.Contains("0s", values0[2], StringComparison.InvariantCulture);
        Assert.Contains("count=4", values0[4], StringComparison.InvariantCulture);
        Assert.Contains("min=50", values0[5], StringComparison.InvariantCulture);
        Assert.Contains("max=150", values0[6], StringComparison.InvariantCulture);
        Assert.Contains("sum=400", values0[7], StringComparison.InvariantCulture);
    }
}
