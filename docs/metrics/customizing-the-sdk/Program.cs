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

public class Program
{
    private static readonly Meter MyMeter = new Meter("MyCompany.MyProduct.MyLibrary", "1.0");

    public static void Main(string[] args)
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSource(MyMeter.Name)

            // Rename an instrument to new name.
            .AddView(instrumentName: "MyCounter", name: "MyCounterRenamed")

            // Change Histogram bounds
            .AddView(instrumentName: "MyHistogram", new HistogramConfig() { HistogramBounds = new double[] { 10, 20 } })

            // For the instrument "http.request", aggregate with only the keys "http.verb", "http.statusCode".
            .AddView(instrumentName: "http.requests", new AggregationConfig() { TagKeys = new string[] { "http.verb", "http.statusCode" } })

            // Drop the instrument "http.requestsInQueue".
            .AddView(instrumentName: "http.requestsInQueue", new DropAggregationConfig())

            // Advanced selection criteria and config via Func<Instrument, AggregationConfig>
            .AddView((instrument) =>
             {
                 if (instrument.Meter.Name.StartsWith("CompanyA.ProductB") &&
                     instrument.GetType().Name.StartsWith("Histogram"))
                 {
                     return new HistogramConfig() { HistogramBounds = new double[] { 10, 20 } };
                 }

                 return null;
             })

            // Any instrument not explicitly added above gets dropped.
            .AddView(instrumentName: "*", new DropAggregationConfig())
            .AddConsoleExporter()
            .Build();

        var random = new Random();
        var histogram = MyMeter.CreateHistogram<long>("MyHistogram");

        for (int i = 0; i < 20000; i++)
        {
            histogram.Record(random.Next(1, 1000), new("tag1", "value1"), new("tag2", "value2"));
        }

        var counter = MyMeter.CreateCounter<long>("MyCounter");

        for (int i = 0; i < 20000; i++)
        {
            counter.Add(1, new("tag1", "value1"), new("tag2", "value2"));
        }

        var counterDrop = MyMeter.CreateCounter<long>("MyCounterDrop");

        for (int i = 0; i < 20000; i++)
        {
            counterDrop.Add(1, new("tag1", "value1"), new("tag2", "value2"));
        }
    }
}
