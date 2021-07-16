// <copyright file="TestMetrics.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Examples.Console
{
    internal class TestMetrics
    {
        internal static object Run(MetricsOptions options)
        {
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter") // All instruments from this meter are enabled.
                .AddConsoleExporter(o =>
                    {
                        o.MetricExportInterval = options.DefaultCollectionPeriodMilliseconds;
                        o.IsDelta = options.IsDelta;
                    })
                .Build();

            using var meter = new Meter("TestMeter", "0.0.1");

            Counter<int> counter = null;
            if (options.FlagCounter ?? true)
            {
                counter = meter.CreateCounter<int>("counter", "things", "A count of things");
            }

            Histogram<int> histogram = null;
            if (options.FlagHistogram ?? false)
            {
                histogram = meter.CreateHistogram<int>("histogram");
            }

            if (options.FlagGauge ?? false)
            {
                var observableCounter = meter.CreateObservableGauge<int>("gauge", () =>
                {
                    return new List<Measurement<int>>()
                    {
                        new Measurement<int>(
                            (int)Process.GetCurrentProcess().PrivateMemorySize64,
                            new KeyValuePair<string, object>("tag1", "value1")),
                    };
                });
            }

            if (options.FlagUpDownCounter ?? true)
            {
                var observableCounter = meter.CreateObservableCounter<int>("updown", () =>
                {
                    return new List<Measurement<int>>()
                    {
                        new Measurement<int>(
                            (int)Process.GetCurrentProcess().PrivateMemorySize64,
                            new KeyValuePair<string, object>("tag1", "value1")),
                    };
                });
            }

            var cts = new CancellationTokenSource();

            var tasks = new List<Task>();

            for (int i = 0; i < options.NumTasks; i++)
            {
                var taskno = i;

                tasks.Add(Task.Run(() =>
                {
                    System.Console.WriteLine($"Task started {taskno + 1}/{options.NumTasks}.");

                    var loops = 0;

                    while (!cts.IsCancellationRequested)
                    {
                        if (options.MaxLoops > 0 && loops >= options.MaxLoops)
                        {
                            break;
                        }

                        histogram?.Record(10);

                        histogram?.Record(
                            100,
                            new KeyValuePair<string, object>("tag1", "value1"));

                        histogram?.Record(
                            200,
                            new KeyValuePair<string, object>("tag1", "value2"),
                            new KeyValuePair<string, object>("tag2", "value2"));

                        histogram?.Record(
                            100,
                            new KeyValuePair<string, object>("tag1", "value1"));

                        histogram?.Record(
                            200,
                            new KeyValuePair<string, object>("tag2", "value2"),
                            new KeyValuePair<string, object>("tag1", "value2"));

                        counter?.Add(10);

                        counter?.Add(
                            100,
                            new KeyValuePair<string, object>("tag1", "value1"));

                        counter?.Add(
                            200,
                            new KeyValuePair<string, object>("tag1", "value2"),
                            new KeyValuePair<string, object>("tag2", "value2"));

                        counter?.Add(
                            100,
                            new KeyValuePair<string, object>("tag1", "value1"));

                        counter?.Add(
                            200,
                            new KeyValuePair<string, object>("tag2", "value2"),
                            new KeyValuePair<string, object>("tag1", "value2"));

                        loops++;
                    }
                }));
            }

            cts.CancelAfter(options.RunTime);
            System.Console.WriteLine($"Wait for {options.RunTime} milliseconds.");
            while (!cts.IsCancellationRequested)
            {
                Task.Delay(1000).Wait();
            }

            Task.WaitAll(tasks.ToArray());

            return null;
        }
    }
}
