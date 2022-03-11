// <copyright file="Skeleton.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private static readonly Meter StressMeter = new("OpenTelemetry.Tests.Stress");
    private static volatile bool bContinue = true;
    private static volatile string output = "Test results not available yet.";

    static Program()
    {
        var process = Process.GetCurrentProcess();
        StressMeter.CreateObservableGauge("Process.NonpagedSystemMemorySize64", () => process.NonpagedSystemMemorySize64);
        StressMeter.CreateObservableGauge("Process.PagedSystemMemorySize64", () => process.PagedSystemMemorySize64);
        StressMeter.CreateObservableGauge("Process.PagedMemorySize64", () => process.PagedMemorySize64);
        StressMeter.CreateObservableGauge("Process.WorkingSet64", () => process.WorkingSet64);
        StressMeter.CreateObservableGauge("Process.VirtualMemorySize64", () => process.VirtualMemorySize64);
    }

    public static void Stress(int concurrency = 0, int prometheusPort = 0)
    {
#if DEBUG
        Console.WriteLine("***WARNING*** The current build is DEBUG which may affect timing!");
        Console.WriteLine();
#endif

        if (concurrency < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(concurrency), "concurrency level should be a non-negative number.");
        }

        if (concurrency == 0)
        {
            concurrency = Environment.ProcessorCount;
        }

        using var meter = new Meter("OpenTelemetry.Tests.Stress." + Guid.NewGuid().ToString("D"));
        var cntLoopsTotal = 0UL;
        meter.CreateObservableCounter(
            "OpenTelemetry.Tests.Stress.Loops",
            () => unchecked((long)cntLoopsTotal),
            description: "The total number of `Run()` invocations that are completed.");
        var dLoopsPerSecond = 0D;
        meter.CreateObservableGauge(
            "OpenTelemetry.Tests.Stress.LoopsPerSecond",
            () => dLoopsPerSecond,
            description: "The rate of `Run()` invocations based on a small sliding window of few hundreds of milliseconds.");
        var dCpuCyclesPerLoop = 0D;
#if NET462
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
        {
            meter.CreateObservableGauge(
                "OpenTelemetry.Tests.Stress.CpuCyclesPerLoop",
                () => dCpuCyclesPerLoop,
                description: "The average CPU cycles for each `Run()` invocation, based on a small sliding window of few hundreds of milliseconds.");
        }

        using var meterProvider = prometheusPort != 0 ? Sdk.CreateMeterProviderBuilder()
            .AddMeter(StressMeter.Name)
            .AddMeter(meter.Name)
            .AddPrometheusExporter(options =>
            {
                options.StartHttpListener = true;
                options.HttpListenerPrefixes = new string[] { $"http://localhost:{prometheusPort}/" };
                options.ScrapeResponseCacheDurationMilliseconds = 0;
            })
            .Build() : null;

        var statistics = new long[concurrency];
        var watchForTotal = Stopwatch.StartNew();

        Parallel.Invoke(
            () =>
            {
                Console.Write($"Running (concurrency = {concurrency}");

                if (prometheusPort != 0)
                {
                    Console.Write($", prometheusEndpoint = http://localhost:{prometheusPort}/metrics/");
                }

                Console.WriteLine("), press <Esc> to stop...");

                var bOutput = false;
                var watch = new Stopwatch();
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        switch (key)
                        {
                            case ConsoleKey.Enter:
                                Console.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToString("O"), output));
                                break;
                            case ConsoleKey.Escape:
                                bContinue = false;
                                return;
                            case ConsoleKey.Spacebar:
                                bOutput = !bOutput;
                                break;
                        }

                        continue;
                    }

                    if (bOutput)
                    {
                        Console.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToString("O"), output));
                    }

                    var cntLoopsOld = (ulong)statistics.Sum();
                    var cntCpuCyclesOld = GetCpuCycles();

                    watch.Restart();
                    Thread.Sleep(200);
                    watch.Stop();

                    cntLoopsTotal = (ulong)statistics.Sum();
                    var cntCpuCyclesNew = GetCpuCycles();

                    var nLoops = cntLoopsTotal - cntLoopsOld;
                    var nCpuCycles = cntCpuCyclesNew - cntCpuCyclesOld;

                    dLoopsPerSecond = (double)nLoops / ((double)watch.ElapsedMilliseconds / 1000.0);
                    dCpuCyclesPerLoop = nLoops == 0 ? 0 : nCpuCycles / nLoops;

                    output = $"Loops: {cntLoopsTotal:n0}, Loops/Second: {dLoopsPerSecond:n0}, CPU Cycles/Loop: {dCpuCyclesPerLoop:n0}";
                    Console.Title = output;
                }
            },
            () =>
            {
                Parallel.For(0, concurrency, (i) =>
                {
                    statistics[i] = 0;
                    while (bContinue)
                    {
                        Run();
                        statistics[i]++;
                    }
                });
            });

        watchForTotal.Stop();
        cntLoopsTotal = (ulong)statistics.Sum();
        var totalLoopsPerSecond = (double)cntLoopsTotal / ((double)watchForTotal.ElapsedMilliseconds / 1000.0);
        var cntCpuCyclesTotal = GetCpuCycles();
        var cpuCyclesPerLoopTotal = cntLoopsTotal == 0 ? 0 : cntCpuCyclesTotal / cntLoopsTotal;
        Console.WriteLine("Stopping the stress test...");
        Console.WriteLine($"* Total Loops: {cntLoopsTotal:n0}");
        Console.WriteLine($"* Average Loops/Second: {totalLoopsPerSecond:n0}");
        Console.WriteLine($"* Average CPU Cycles/Loop: {cpuCyclesPerLoopTotal:n0}");
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryProcessCycleTime(IntPtr hProcess, out ulong cycles);

    private static ulong GetCpuCycles()
    {
#if NET462
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
        {
            return 0;
        }

        if (!QueryProcessCycleTime((IntPtr)(-1), out var cycles))
        {
            return 0;
        }

        return cycles;
    }
}
