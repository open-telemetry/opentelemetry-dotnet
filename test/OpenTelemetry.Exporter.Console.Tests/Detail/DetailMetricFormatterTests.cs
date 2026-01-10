// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests.Compact;

public class DetailMetricFormatterTests
{
    [Fact]
    public async Task DetailMetricOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();

        using var meter = new Meter("TestMeter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice"))
            .AddMeter(meter.Name) // All instruments from this meter are enabled.
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Formatter = "detail";
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

        Assert.Matches(@"Metric Name:\s+counter", output);
        Assert.Matches(@"Unit: things", output);
        Assert.Matches(@"Value: 10", output);
    }

    [Fact]
    public async Task DetailHistogramOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();

        using var meter = new Meter("TestMeter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice"))
            .AddMeter(meter.Name) // All instruments from this meter are enabled.
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Formatter = "detail";
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

        Assert.Matches(@"Metric Name:\s+histogram", output);

        Assert.Matches(@"tag1: value1", output);

        Assert.Matches(@"Value: Sum: 400 Count: 4 Min: 50 Max: 150", output);

        Assert.Contains("(25,50]:1", output, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task DetailExponentialHistogramOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();

        using var meter = new Meter("TestMeter");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice"))
            .AddMeter(meter.Name) // All instruments from this meter are enabled.
            .AddView(instrument =>
                {
                    return instrument.GetType().GetGenericTypeDefinition() == typeof(Histogram<>)
                        ? new Base2ExponentialBucketHistogramConfiguration()
                        : null;
                })
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Formatter = "detail";
                    exporterOptions.Console = mockConsole;

                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 100;
                    metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
            .Build();

        // Act
        var histogram = meter.CreateHistogram<double>("histogram2"); // No Unit

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

        Assert.Matches(@"Metric Name:\s+histogram", output);

        Assert.Matches(@"tag1: value1", output);

        Assert.Matches(@"Value: Sum: 400 Count: 4 Min: 50 Max: 150", output);
    }

    [Fact]
    public async Task AdditionalAttributesMetricOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();

        var meterTags = new Dictionary<string, object?>() { { "meterTag1", "zero" } };
        using var meter = new Meter("TestMeter", "version1", meterTags, "source1");

        var resourceAttributes = new Dictionary<string, object>() { { "att1", "val1" } };
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice").AddAttributes(resourceAttributes))
            .AddMeter(meter.Name) // All instruments from this meter are enabled.
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Formatter = "detail";
                    exporterOptions.Console = mockConsole;

                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 100;
                    metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                })
            .Build();

        // Act
        var counterTags = new Dictionary<string, object?>() { { "counterTag1", "alpha" } };
        var counter = meter.CreateCounter<int>("counter", "things", "A count of things", counterTags);

        counter?.Add(
            50,
            new KeyValuePair<string, object?>("tag1", "value1"));
        counter?.Add(
            100,
            new KeyValuePair<string, object?>("tag1", "value1"));
        counter?.Add(
            150,
            new KeyValuePair<string, object?>("tag1", "value1"));

        counter?.Add(
            200,
            new KeyValuePair<string, object?>("tag2", "value2"));

        await Task.Delay(150);

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Metric: {0}", output);

        Assert.Matches(@"Metric Name:\s+counter", output);
        Assert.Matches(@"att1:\s+val1", output);
        Assert.Matches(@"Version:\s+version1", output);
        Assert.Matches(@"meterTag1:\s+zero", output);
        Assert.Matches(@"Exemplars", output);
    }
}
