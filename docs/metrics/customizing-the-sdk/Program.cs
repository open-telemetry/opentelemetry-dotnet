// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace CustomizingTheSdk;

internal static class Program
{
    private static readonly Meter Meter1 = new("CompanyA.ProductA.Library1", "1.0");
    private static readonly Meter Meter2 = new("CompanyA.ProductB.Library2", "1.0");

    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(resource => resource.AddAttributes(new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("static-attribute1", "v1"),
                    new KeyValuePair<string, object>("static-attribute2", "v2"),
                }))
            .ConfigureResource(resource => resource.AddService("MyServiceName"))
            .AddMeter(Meter1.Name)
            .AddMeter(Meter2.Name)

            // Rename an instrument to new name.
            .AddView(instrumentName: "MyCounter", name: "MyCounterRenamed")

            // Change Histogram boundaries using the Explicit Bucket Histogram aggregation.
            .AddView(instrumentName: "MyHistogram", new ExplicitBucketHistogramConfiguration() { Boundaries = [10.0, 20.0] })

            // Change Histogram to use the Base2 Exponential Bucket Histogram aggregation.
            .AddView(instrumentName: "MyExponentialBucketHistogram", new Base2ExponentialBucketHistogramConfiguration())

            // For the instrument "MyCounterCustomTags", aggregate with only the keys "tag1", "tag2".
            .AddView(instrumentName: "MyCounterCustomTags", new MetricStreamConfiguration() { TagKeys = ["tag1", "tag2"] })

            // Drop the instrument "MyCounterDrop".
            .AddView(instrumentName: "MyCounterDrop", MetricStreamConfiguration.Drop)

            // Configure the Explicit Bucket Histogram aggregation with custom boundaries and new name.
            .AddView(instrumentName: "histogramWithMultipleAggregations", new ExplicitBucketHistogramConfiguration() { Boundaries = [10.0, 20.0], Name = "MyHistogramWithExplicitHistogram" })

            // Use Base2 Exponential Bucket Histogram aggregation and new name.
            .AddView(instrumentName: "histogramWithMultipleAggregations", new Base2ExponentialBucketHistogramConfiguration() { Name = "MyHistogramWithBase2ExponentialBucketHistogram" })

            // An instrument which does not match any views
            // gets processed with default behavior. (SDK default)
            // Uncommenting the following line will
            // turn off the above default. i.e any
            // instrument which does not match any views
            // gets dropped.
            // .AddView(instrumentName: "*", MetricStreamConfiguration.Drop)
            .AddConsoleExporter()
            .Build();

        var random = new Random();

        var counter = Meter1.CreateCounter<long>("MyCounter");
        for (int i = 0; i < 20000; i++)
        {
            counter.Add(1, new("tag1", "value1"), new("tag2", "value2"));
        }

        var histogram = Meter1.CreateHistogram<long>("MyHistogram");
        for (int i = 0; i < 20000; i++)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            histogram.Record(random.Next(1, 1000), new("tag1", "value1"), new("tag2", "value2"));
        }

        var exponentialBucketHistogram = Meter1.CreateHistogram<long>("MyExponentialBucketHistogram");
        for (int i = 0; i < 20000; i++)
        {
            exponentialBucketHistogram.Record(random.Next(1, 1000), new("tag1", "value1"), new("tag2", "value2"));
        }

        var histogramWithMultipleAggregations = Meter1.CreateHistogram<long>("histogramWithMultipleAggregations");
        for (int i = 0; i < 20000; i++)
        {
            histogramWithMultipleAggregations.Record(random.Next(1, 1000), new("tag1", "value1"), new("tag2", "value2"));
        }

        var counterCustomTags = Meter1.CreateCounter<long>("MyCounterCustomTags");
        for (int i = 0; i < 20000; i++)
        {
            counterCustomTags.Add(1, new("tag1", "value1"), new("tag2", "value2"), new("tag3", "value4"));
        }

        var counterDrop = Meter1.CreateCounter<long>("MyCounterDrop");
        for (int i = 0; i < 20000; i++)
        {
            counterDrop.Add(1, new("tag1", "value1"), new("tag2", "value2"));
        }

        var histogram2 = Meter2.CreateHistogram<long>("MyHistogram2");
        for (int i = 0; i < 20000; i++)
        {
            histogram2.Record(random.Next(1, 1000), new("tag1", "value1"), new("tag2", "value2"));
#pragma warning restore CA5394 // Do not use insecure randomness
        }
    }
}
