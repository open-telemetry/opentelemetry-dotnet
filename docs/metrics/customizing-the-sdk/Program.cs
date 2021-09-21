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
            .AddView((instrument) =>
            {
                if (instrument.GetType().Name.Contains("Histogram"))
                {
                    var metricConfig = new MetricStreamConfig();
                    metricConfig.HistogramBounds = new double[] { 0, 10, 20 };
                    return metricConfig;
                }

                // Null to indicate this View doesn't
                // have any say on if or how the instrument
                // gets processed. SDK will do the default here.
                return null;
            })
            .AddView((instrument) =>
            {
                if (instrument.GetType().Name.Contains("Histogram"))
                {
                    var metricConfig = new MetricStreamConfig();
                    metricConfig.HistogramBounds = new double[] { 100, 1000 };
                    metricConfig.Name = "customBucketHistogram";
                    return metricConfig;
                }

                return null;
            })
            .AddView((instrument) =>
            {
                if (instrument.Name.Equals("MyCounter"))
                {
                    var metricConfig = new MetricStreamConfig();
                    metricConfig.Name = "MyCounterRenamed";
                    return metricConfig;
                }

                return null;
            })
            .AddView((instrument) =>
            {
                if (instrument.Name.Equals("MyCounterDrop"))
                {
                    var metricConfig = new MetricStreamConfig();
                    metricConfig.Aggregation = Aggregation.None;
                    return metricConfig;
                }

                return null;
            })

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
