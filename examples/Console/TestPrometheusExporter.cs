// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Examples.Console;

internal sealed class TestPrometheusExporter
{
    private static readonly Meter MyMeter = new("MyMeter");
    private static readonly Meter MyMeter2 = new("MyMeter2");
    private static readonly Counter<double> Counter = MyMeter.CreateCounter<double>("myCounter", description: "A counter for demonstration purpose.");
    private static readonly Histogram<long> MyHistogram = MyMeter.CreateHistogram<long>("myHistogram");

    internal static int Run(PrometheusOptions options)
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
                o => o.UriPrefixes = [$"http://localhost:{options.Port}/"])
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
#if NETFRAMEWORK
#pragma warning disable CA5394
                var value = new Random().Next(1, 1500);
#pragma warning restore CA5394
#else
                var value = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 1500);
#endif
                MyHistogram.Record(value, new("tag1", "value1"), new("tag2", "value2"));
                Task.Delay(10).Wait();
            }
        });

        System.Console.WriteLine($"PrometheusExporter exposes metrics via http://localhost:{options.Port}/metrics/");
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

        return 0;
    }

    private static IEnumerable<Measurement<double>> GetThreadCpuTime(Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            yield return new(thread.TotalProcessorTime.TotalMilliseconds, new("ProcessId", process.Id), new("ThreadId", thread.Id));
        }
    }
}
