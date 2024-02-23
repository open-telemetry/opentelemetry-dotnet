// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
using System.Text.Json;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Tests.Stress;

public abstract class StressTest<T> : IDisposable
    where T : StressTestOptions
{
    private volatile bool bContinue = true;
    private volatile string output = "Test results not available yet.";

    protected StressTest(T options)
    {
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public T Options { get; }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void RunSynchronously()
    {
#if DEBUG
        Console.WriteLine("***WARNING*** The current build is DEBUG which may affect timing!");
        Console.WriteLine();
#endif

        var options = this.Options;

        if (options.Concurrency < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Concurrency), "Concurrency level should be a non-negative number.");
        }

        if (options.Concurrency == 0)
        {
            options.Concurrency = Environment.ProcessorCount;
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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            meter.CreateObservableGauge(
                "OpenTelemetry.Tests.Stress.CpuCyclesPerLoop",
                () => dCpuCyclesPerLoop,
                description: "The average CPU cycles for each `Run()` invocation, based on a small sliding window of few hundreds of milliseconds.");
        }

        using var meterProvider = options.PrometheusInternalMetricsPort != 0 ? Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddRuntimeInstrumentation()
            .AddPrometheusHttpListener(o => o.UriPrefixes = new string[] { $"http://localhost:{options.PrometheusInternalMetricsPort}/" })
            .Build() : null;

        var statistics = new long[options.Concurrency];
        var watchForTotal = Stopwatch.StartNew();

        TimeSpan? duration = options.DurationSeconds > 0
            ? TimeSpan.FromSeconds(options.DurationSeconds)
            : null;

        Parallel.Invoke(
            () =>
            {
                Console.WriteLine($"Options: {JsonSerializer.Serialize(options)}");
                Console.WriteLine($"Run {Process.GetCurrentProcess().ProcessName}.exe --help to see available options.");
                Console.Write($"Running (concurrency = {options.Concurrency}");

                if (options.PrometheusInternalMetricsPort != 0)
                {
                    Console.Write($", internalPrometheusEndpoint = http://localhost:{options.PrometheusInternalMetricsPort}/metrics/");
                }

                this.WriteRunInformationToConsole();

                Console.WriteLine("), press <Esc> to stop, press <Spacebar> to toggle statistics in the console...");
                Console.WriteLine(this.output);

                var outputCursorTop = Console.CursorTop - 1;

                var bOutput = true;
                var watch = new Stopwatch();
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        switch (key)
                        {
                            case ConsoleKey.Enter:
                                Console.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToString("O"), this.output));
                                break;
                            case ConsoleKey.Escape:
                                this.bContinue = false;
                                return;
                            case ConsoleKey.Spacebar:
                                bOutput = !bOutput;
                                break;
                        }

                        continue;
                    }

                    if (bOutput)
                    {
                        var tempCursorLeft = Console.CursorLeft;
                        var tempCursorTop = Console.CursorTop;
                        Console.SetCursorPosition(0, outputCursorTop);
                        Console.WriteLine(this.output.PadRight(Console.BufferWidth));
                        Console.SetCursorPosition(tempCursorLeft, tempCursorTop);
                    }

                    var cntLoopsOld = (ulong)statistics.Sum();
                    var cntCpuCyclesOld = StressTestNativeMethods.GetCpuCycles();

                    watch.Restart();
                    Thread.Sleep(200);
                    watch.Stop();

                    cntLoopsTotal = (ulong)statistics.Sum();
                    var cntCpuCyclesNew = StressTestNativeMethods.GetCpuCycles();

                    var nLoops = cntLoopsTotal - cntLoopsOld;
                    var nCpuCycles = cntCpuCyclesNew - cntCpuCyclesOld;

                    dLoopsPerSecond = (double)nLoops / ((double)watch.ElapsedMilliseconds / 1000.0);
                    dCpuCyclesPerLoop = nLoops == 0 ? 0 : nCpuCycles / nLoops;

                    var totalElapsedTime = watchForTotal.Elapsed;

                    if (duration.HasValue)
                    {
                        this.output = $"Loops: {cntLoopsTotal:n0}, Loops/Second: {dLoopsPerSecond:n0}, CPU Cycles/Loop: {dCpuCyclesPerLoop:n0}, RemainingTime (Seconds): {(duration.Value - totalElapsedTime).TotalSeconds:n0}";
                        if (totalElapsedTime > duration)
                        {
                            this.bContinue = false;
                            return;
                        }
                    }
                    else
                    {
                        this.output = $"Loops: {cntLoopsTotal:n0}, Loops/Second: {dLoopsPerSecond:n0}, CPU Cycles/Loop: {dCpuCyclesPerLoop:n0}, RunningTime (Seconds): {totalElapsedTime.TotalSeconds:n0}";
                    }

                    Console.Title = this.output;
                }
            },
            () =>
            {
                Parallel.For(0, options.Concurrency, (i) =>
                {
                    statistics[i] = 0;
                    while (this.bContinue)
                    {
                        this.RunWorkItemInParallel();
                        statistics[i]++;
                    }
                });
            });

        watchForTotal.Stop();
        cntLoopsTotal = (ulong)statistics.Sum();
        var totalLoopsPerSecond = (double)cntLoopsTotal / ((double)watchForTotal.ElapsedMilliseconds / 1000.0);
        var cntCpuCyclesTotal = StressTestNativeMethods.GetCpuCycles();
        var cpuCyclesPerLoopTotal = cntLoopsTotal == 0 ? 0 : cntCpuCyclesTotal / cntLoopsTotal;
        Console.WriteLine("Stopping the stress test...");
        Console.WriteLine($"* Total Running Time (Seconds) {watchForTotal.Elapsed.TotalSeconds:n0}");
        Console.WriteLine($"* Total Loops: {cntLoopsTotal:n0}");
        Console.WriteLine($"* Average Loops/Second: {totalLoopsPerSecond:n0}");
        Console.WriteLine($"* Average CPU Cycles/Loop: {cpuCyclesPerLoopTotal:n0}");
#if !NETFRAMEWORK
        Console.WriteLine($"* GC Total Allocated Bytes: {GC.GetTotalAllocatedBytes()}");
#endif
    }

    protected virtual void WriteRunInformationToConsole()
    {
    }

    protected abstract void RunWorkItemInParallel();

    protected virtual void Dispose(bool isDisposing)
    {
    }
}
