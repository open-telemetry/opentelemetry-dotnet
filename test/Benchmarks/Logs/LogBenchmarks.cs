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
| UnnecessaryIsEnabledCheck     |   1.332 ns | 0.0225 ns | 0.0188 ns |      - |         - |
| CreateLoggerRepeatedly        |  48.295 ns | 0.5951 ns | 0.4970 ns | 0.0038 |      24 B |
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

    private readonly ILoggerFactory loggerFactoryWithNoListener;
    private readonly ILoggerFactory loggerFactoryWithOneProcessor;
    private readonly ILoggerFactory loggerFactoryWithTwoProcessor;
    private readonly ILoggerFactory loggerFactoryWithThreeProcessor;

    public LogBenchmarks()
    {
        this.loggerFactoryWithNoListener = LoggerFactory.Create(builder => { });
        this.loggerWithNoListener = this.loggerFactoryWithNoListener.CreateLogger<LogBenchmarks>();

        this.loggerFactoryWithOneProcessor = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging
                .AddProcessor(new DummyLogProcessor()));
        });
        this.loggerWithOneProcessor = this.loggerFactoryWithOneProcessor.CreateLogger<LogBenchmarks>();

        this.loggerFactoryWithTwoProcessor = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor()));
        });
        this.loggerWithTwoProcessors = this.loggerFactoryWithTwoProcessor.CreateLogger<LogBenchmarks>();

        this.loggerFactoryWithThreeProcessor = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor())
                .AddProcessor(new DummyLogProcessor()));
        });
        this.loggerWithThreeProcessors = this.loggerFactoryWithThreeProcessor.CreateLogger<LogBenchmarks>();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.loggerFactoryWithNoListener.Dispose();
        this.loggerFactoryWithOneProcessor.Dispose();
        this.loggerFactoryWithTwoProcessor.Dispose();
        this.loggerFactoryWithThreeProcessor.Dispose();
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
        this.loggerWithNoListener.SayHello(FoodName, FoodPrice);
    }

    [Benchmark]
    public void UnnecessaryIsEnabledCheck()
    {
        if (this.loggerWithNoListener.IsEnabled(LogLevel.Information))
        {
            this.loggerWithNoListener.SayHello(FoodName, FoodPrice);
        }
    }

    [Benchmark]
    public void CreateLoggerRepeatedly()
    {
        var logger = this.loggerFactoryWithNoListener.CreateLogger<LogBenchmarks>();
        logger.SayHello(FoodName, FoodPrice);
    }

    [Benchmark]
    public void OneProcessor()
    {
        this.loggerWithOneProcessor.SayHello(FoodName, FoodPrice);
    }

    [Benchmark]
    public void TwoProcessors()
    {
        this.loggerWithTwoProcessors.SayHello(FoodName, FoodPrice);
    }

    [Benchmark]
    public void ThreeProcessors()
    {
        this.loggerWithThreeProcessors.SayHello(FoodName, FoodPrice);
    }

    internal class DummyLogProcessor : BaseProcessor<LogRecord>
    {
    }
}
