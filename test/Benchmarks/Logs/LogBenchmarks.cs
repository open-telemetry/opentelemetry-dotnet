// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method                                 | Mean       | Error     | StdDev    | Median     | Gen0   | Allocated |
|--------------------------------------- |-----------:|----------:|----------:|-----------:|-------:|----------:|
| NoListener                             |  44.633 ns | 0.8442 ns | 1.9733 ns |  43.683 ns | 0.0102 |      64 B |
| NoListenerWithLoggerMessageGenerator   |   1.880 ns | 0.0141 ns | 0.0125 ns |   1.879 ns |      - |         - |
| OneProcessor                           | 126.857 ns | 1.1861 ns | 1.0514 ns | 126.730 ns | 0.0165 |     104 B |
| OneProcessorWithLoggerMessageGenerator | 112.677 ns | 1.0021 ns | 0.8884 ns | 112.605 ns | 0.0063 |      40 B |
| TwoProcessors                          | 129.967 ns | 0.8315 ns | 0.7371 ns | 129.850 ns | 0.0165 |     104 B |
| ThreeProcessors                        | 130.117 ns | 1.1359 ns | 1.0626 ns | 129.991 ns | 0.0165 |     104 B |
*/

namespace Benchmarks.Logs;

public class LogBenchmarks
{
    private readonly ILogger loggerWithNoListener;
    private readonly ILogger loggerWithOneProcessor;
    private readonly ILogger loggerWithTwoProcessors;
    private readonly ILogger loggerWithThreeProcessors;

    public LogBenchmarks()
    {
        using var loggerFactoryWithNoListener = LoggerFactory.Create(builder => { });
        this.loggerWithNoListener = loggerFactoryWithNoListener.CreateLogger<LogBenchmarks>();

        using var loggerFactoryWithOneProcessor = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options
                .AddProcessor(new DummyLogProcessor()));
        });
        this.loggerWithOneProcessor = loggerFactoryWithOneProcessor.CreateLogger<LogBenchmarks>();

        using var loggerFactoryWithTwoProcessor = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor()));
        });
        this.loggerWithTwoProcessors = loggerFactoryWithTwoProcessor.CreateLogger<LogBenchmarks>();

        using var loggerFactoryWithThreeProcessor = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor()));
        });
        this.loggerWithThreeProcessors = loggerFactoryWithThreeProcessor.CreateLogger<LogBenchmarks>();
    }

    [Benchmark]
    public void NoListener()
    {
        this.loggerWithNoListener.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
    }

    [Benchmark]
    public void NoListenerWithLoggerMessageGenerator()
    {
        Food.SayHello(this.loggerWithNoListener, "tomato", 2.99);
    }

    [Benchmark]
    public void OneProcessor()
    {
        this.loggerWithOneProcessor.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
    }

    [Benchmark]
    public void OneProcessorWithLoggerMessageGenerator()
    {
        Food.SayHello(this.loggerWithOneProcessor, "tomato", 2.99);
    }

    [Benchmark]
    public void TwoProcessors()
    {
        this.loggerWithTwoProcessors.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
    }

    [Benchmark]
    public void ThreeProcessors()
    {
        this.loggerWithThreeProcessors.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
    }

    internal class DummyLogProcessor : BaseProcessor<LogRecord>
    {
    }
}