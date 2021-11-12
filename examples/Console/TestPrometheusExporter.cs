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
    private static readonly Meter MyMeter = new Meter("MyMeter");
    private static readonly Counter<double> Counter = MyMeter.CreateCounter<double>("myCounter", description: "A counter for demonstration purpose.");
    private static readonly Histogram<long> MyHistogram = MyMeter.CreateHistogram<long>("myHistogram");
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random());

    internal static object Run(int port)
    {
        /* prometheus.yml

        global:
          scrape_interval: 1s
          evaluation_interval: 1s

        scrape_configs:
          - job_name: "opentelemetry"
            static_configs:
              - targets: ["localhost:9184"]
        */

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MyMeter.Name)
            .AddPrometheusExporter(opt =>
            {
                opt.StartHttpListener = true;
                opt.HttpListenerPrefixes = new string[] { $"http://localhost:{port}/" };
            })
            .Build();

        var process = Process.GetCurrentProcess();
        MyMeter.CreateObservableCounter("thread.cpu_time", () => GetThreadCpuTime(process), "ms");

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

        System.Console.WriteLine($"PrometheusExporter is listening on http://localhost:{port}/metrics/");
        System.Console.WriteLine($"Press any key to exit...");
        System.Console.ReadKey();
        token.Cancel();

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
