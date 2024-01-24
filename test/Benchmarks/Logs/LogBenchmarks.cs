// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.101
  [Host]     : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2


| Method                        | Mean       | Error     | StdDev    | Gen0   | Allocated |
|------------------------------ |-----------:|----------:|----------:|-------:|----------:|
| NoListenerStringInterpolation | 124.458 ns | 2.5188 ns | 2.2329 ns | 0.0114 |      72 B |
| NoListenerExtensionMethod     |  36.326 ns | 0.2916 ns | 0.2435 ns | 0.0102 |      64 B |
| NoListener                    |   1.375 ns | 0.0586 ns | 0.0896 ns |      - |         - |
| OneProcessor                  |  98.133 ns | 1.8805 ns | 1.5703 ns | 0.0063 |      40 B |
| TwoProcessors                 | 105.414 ns | 0.4610 ns | 0.3850 ns | 0.0063 |      40 B |
| ThreeProcessors               | 102.023 ns | 1.4187 ns | 1.1847 ns | 0.0063 |      40 B |
*/

namespace Benchmarks.Logs;

public class LogBenchmarks
{
    private const double FoodPrice = 2.99;
    private static readonly string FoodName = "tomato";

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
    public void NoListenerStringInterpolation()
    {
        this.loggerWithNoListener.LogInformation($"Hello from {FoodName} {FoodPrice}.");
    }

    [Benchmark]
    public void NoListenerExtensionMethod()
    {
        this.loggerWithNoListener.LogInformation("Hello from {name} {price}.", FoodName, FoodPrice);
    }

    [Benchmark]
    public void NoListener()
    {
        Food.SayHello(this.loggerWithNoListener, FoodName, FoodPrice);
    }

    [Benchmark]
    public void OneProcessor()
    {
        Food.SayHello(this.loggerWithOneProcessor, FoodName, FoodPrice);
    }

    [Benchmark]
    public void TwoProcessors()
    {
        Food.SayHello(this.loggerWithTwoProcessors, FoodName, FoodPrice);
    }

    [Benchmark]
    public void ThreeProcessors()
    {
        Food.SayHello(this.loggerWithThreeProcessors, FoodName, FoodPrice);
    }

    internal class DummyLogProcessor : BaseProcessor<LogRecord>
    {
    }
}
