// <copyright file="Program.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace CustomizingTheSdk;

public class Program
{
    private static readonly Meter Meter1 = new("CompanyA.ProductA.Library1", "1.0");
    private static readonly Meter Meter2 = new("CompanyA.ProductB.Library2", "1.0");

    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(Meter1.Name)
            .AddMeter(Meter2.Name)

            // Rename an instrument to new name.
            .AddView(instrumentName: "MyCounter", name: "MyCounterRenamed")

            // Change Histogram boundaries
            .AddView(instrumentName: "MyHistogram", new ExplicitBucketHistogramConfiguration() { Boundaries = new double[] { 10, 20 } })

            // For the instrument "MyCounterCustomTags", aggregate with only the keys "tag1", "tag2".
            .AddView(instrumentName: "MyCounterCustomTags", new MetricStreamConfiguration() { TagKeys = new string[] { "tag1", "tag2" } })

            // Drop the instrument "MyCounterDrop".
            .AddView(instrumentName: "MyCounterDrop", MetricStreamConfiguration.Drop)

            // Advanced selection criteria and config via Func<Instrument, MetricStreamConfiguration>
            .AddView((instrument) =>
            {
                if (instrument.Meter.Name.Equals("CompanyA.ProductB.Library2") &&
                    instrument.GetType().Name.Contains("Histogram"))
                {
                    return new ExplicitBucketHistogramConfiguration() { Boundaries = new double[] { 10, 20 } };
                }

                return null;
            })

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
            histogram.Record(random.Next(1, 1000), new("tag1", "value1"), new("tag2", "value2"));
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
        }
    }
}
