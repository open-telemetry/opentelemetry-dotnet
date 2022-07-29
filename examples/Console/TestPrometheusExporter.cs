// <copyright file="TestPrometheusExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal class TestPrometheusExporter
{
    private static readonly Meter MyMeter = new("MyMeter");
    private static readonly Meter MyMeter2 = new("MyMeter2");
    private static readonly Counter<double> Counter = MyMeter.CreateCounter<double>("myCounter", description: "A counter for demonstration purpose.");
    private static readonly Histogram<long> MyHistogram = MyMeter.CreateHistogram<long>("myHistogram");
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());

    internal static object Run(int port)
    {
        /* prometheus.yml example. Adjust port as per actual.

        global:
          scrape_interval: 1s
          evaluation_interval: 1s

        scrape_configs:
          - job_name: "opentelemetry"
            static_configs:
              - targets: ["localhost:9464"]
        */

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MyMeter.Name)
            .AddMeter(MyMeter2.Name)
            .AddPrometheusHttpListener(
                exporterOptions => exporterOptions.ScrapeResponseCacheDurationMilliseconds = 0,
                listenerOptions => listenerOptions.Prefixes = new string[] { $"http://localhost:{port}/" })
            .Build();

        var process = Process.GetCurrentProcess();
        MyMeter.CreateObservableCounter("thread.cpu_time", () => GetThreadCpuTime(process), "ms");

        // If the same Instrument name+unit combination happened under different Meters, PrometheusExporter
        // exporter will output duplicated metric names. Related issues and PRs:
        // * https://github.com/open-telemetry/opentelemetry-specification/pull/2017
        // * https://github.com/open-telemetry/opentelemetry-specification/pull/2035
        // * https://github.com/open-telemetry/opentelemetry-dotnet/pull/2593
        //
        // MyMeter2.CreateObservableCounter("thread.cpu_time", () => GetThreadCpuTime(process), "ms");

        using var token = new CancellationTokenSource();

        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                Counter.Add(9.9, new("name", "apple"), new("color", "red"));
                Counter.Add(99.9, new("name", "lemon"), new("color", "yellow"));
                MyHistogram.Record(ThreadLocalRandom.Value.Next(1, 1500), new("tag1", "value1"), new("tag2", "value2"));
                Task.Delay(10).Wait();
            }
        });

        System.Console.WriteLine($"PrometheusExporter exposes metrics via http://localhost:{port}/metrics/");
        System.Console.WriteLine($"Press Esc key to exit...");
        while (true)
        {
            if (System.Console.KeyAvailable)
            {
                var key = System.Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape)
                {
                    token.Cancel();
                    System.Console.WriteLine($"Exiting...");
                    break;
                }
            }

            Task.Delay(200).Wait();
        }

        return null;
    }

    private static IEnumerable<Measurement<double>> GetThreadCpuTime(Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            yield return new(thread.TotalProcessorTime.TotalMilliseconds, new("ProcessId", process.Id), new("ThreadId", thread.Id));
        }
    }
}
