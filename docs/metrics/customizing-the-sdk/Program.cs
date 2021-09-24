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
        // TODO: Document views with several examples
        // in the increasing order of complexity.
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSource(MyMeter.Name)
            .AddView(instrumentName: "MyCounter") // MyCounter will be reported with defaults. This is done to ensure that any other wildcard Views for the same instrument does not affect this instrument.
            .AddView(instrumentName: "MyHistogram") // MyHistogram will be aggregated using defaults.
            .AddView(instrumentName: "MyCounterDrop", aggregation: Aggregation.None) // The intention is to drop MyCounterDrop instrument. Due to the wildcard "My", this gets pickup.. TODO: find the right expectation.
            .AddView(name: "MyHistogramCustom", instrumentName: "MyHistogram", histogramBounds: new double[] { 10, 20 }) // MyHistogram will be aggregated using custom bounds and will be outputted as "MyHistogramCustom"
            .AddView(instrumentName: "My", tagKeys: new string[] { "tag1" }) // All instruments whose name starts with My, will be aggregated with a single tag - tag1. MyCounter, MyHistogram will get excluded from this, as their name is already taken.
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
