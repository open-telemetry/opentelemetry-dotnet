// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace LearningMoreInstruments;

internal static class Program
{
    private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary", "1.0");
    private static readonly Histogram<long> MyHistogram = MyMeter.CreateHistogram<long>("MyHistogram");

    static Program()
    {
        var process = Process.GetCurrentProcess();

        MyMeter.CreateObservableCounter("Thread.CpuTime", () => GetThreadCpuTime(process), "ms");

        MyMeter.CreateObservableGauge("Thread.State", () => GetThreadState(process));
    }

    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("MyCompany.MyProduct.MyLibrary")
            .AddConsoleExporter()
            .Build();

        var random = new Random();
        for (int i = 0; i < 1000; i++)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            MyHistogram.Record(random.Next(1, 1000));
#pragma warning restore CA5394 // Do not use insecure randomness
        }
    }

    private static IEnumerable<Measurement<double>> GetThreadCpuTime(Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            yield return new(thread.TotalProcessorTime.TotalMilliseconds, new("ProcessId", process.Id), new("ThreadId", thread.Id));
        }
    }

    private static IEnumerable<Measurement<int>> GetThreadState(Process process)
    {
        foreach (ProcessThread thread in process.Threads)
        {
            yield return new((int)thread.ThreadState, new("ProcessId", process.Id), new("ThreadId", thread.Id));
        }
    }
}
