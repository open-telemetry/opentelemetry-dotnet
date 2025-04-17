// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

/*
BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.3880/23H2/2023Update/SunValley3)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.107
  [Host]     : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2


| Method                        | Mean       | Error     | StdDev     | Median     | Gen0   | Gen1   | Allocated |
|------------------------------ |-----------:|----------:|-----------:|-----------:|-------:|-------:|----------:|
| NoListenerStringInterpolation | 135.503 ns | 2.7458 ns |  4.5114 ns | 135.391 ns | 0.0114 |      - |      72 B |
| NoListenerExtensionMethod     |  40.218 ns | 0.8249 ns |  2.2581 ns |  39.809 ns | 0.0102 |      - |      64 B |
| NoListener                    |   1.930 ns | 0.0626 ns |  0.1264 ns |   1.889 ns |      - |      - |         - |
| UnnecessaryIsEnabledCheck     |   1.531 ns | 0.0542 ns |  0.1267 ns |   1.518 ns |      - |      - |         - |
| CreateLoggerRepeatedly        |  53.797 ns | 1.0927 ns |  1.7331 ns |  53.401 ns | 0.0038 |      - |      24 B |
| OneProcessor                  | 111.558 ns | 2.9821 ns |  8.5082 ns | 109.311 ns | 0.0063 |      - |      40 B |
| BatchProcessor                | 263.650 ns | 5.2908 ns | 14.1223 ns | 258.984 ns | 0.0200 | 0.0043 |     128 B |
| TwoProcessors                 | 108.701 ns | 2.1964 ns |  4.3355 ns | 108.025 ns | 0.0063 |      - |      40 B |
| ThreeProcessors               | 105.099 ns | 1.8106 ns |  2.1554 ns | 105.796 ns | 0.0063 |      - |      40 B |
*/

namespace Benchmarks.Logs;

public class LogBenchmarks
{
    private const double FoodPrice = 2.99;
    private static readonly string FoodName = "tomato";

    private readonly ILogger loggerWithNoListener;
    private readonly ILogger loggerWithOneProcessor;
    private readonly ILogger loggerWithBatchProcessor;
    private readonly ILogger loggerWithTwoProcessors;
    private readonly ILogger loggerWithThreeProcessors;

    private readonly ILoggerFactory loggerFactoryWithNoListener;
    private readonly ILoggerFactory loggerFactoryWithOneProcessor;
    private readonly ILoggerFactory loggerFactoryWithBatchProcessor;
    private readonly ILoggerFactory loggerFactoryWithTwoProcessor;
    private readonly ILoggerFactory loggerFactoryWithThreeProcessor;

    public LogBenchmarks()
    {
        this.loggerFactoryWithNoListener = LoggerFactory.Create(builder => { });
        this.loggerWithNoListener = this.loggerFactoryWithNoListener.CreateLogger<LogBenchmarks>();

        this.loggerFactoryWithOneProcessor = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging
                .AddProcessor(new NoopLogProcessor()));
        });
        this.loggerWithOneProcessor = this.loggerFactoryWithOneProcessor.CreateLogger<LogBenchmarks>();

        this.loggerFactoryWithBatchProcessor = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging
                .AddProcessor(new BatchLogRecordExportProcessor(new NoopExporter())));
        });
        this.loggerWithBatchProcessor = this.loggerFactoryWithBatchProcessor.CreateLogger<LogBenchmarks>();

        this.loggerFactoryWithTwoProcessor = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging
                .AddProcessor(new NoopLogProcessor())
                .AddProcessor(new NoopLogProcessor()));
        });
        this.loggerWithTwoProcessors = this.loggerFactoryWithTwoProcessor.CreateLogger<LogBenchmarks>();

        this.loggerFactoryWithThreeProcessor = LoggerFactory.Create(builder =>
        {
            builder.UseOpenTelemetry(logging => logging
                .AddProcessor(new NoopLogProcessor())
                .AddProcessor(new NoopLogProcessor())
                .AddProcessor(new NoopLogProcessor()));
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
        this.loggerWithNoListener.LogInformation("Hello from {Name} {Price}.", FoodName, FoodPrice);
    }

    [Benchmark]
    public void NoListener()
    {
        this.loggerWithNoListener.FoodRecallNotice(
                brandName: "Contoso",
                productDescription: "Salads",
                productType: "Food & Beverages",
                recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
                companyName: "Contoso Fresh Vegetables, Inc.");
    }

    [Benchmark]
    public void UnnecessaryIsEnabledCheck()
    {
        if (this.loggerWithNoListener.IsEnabled(LogLevel.Information))
        {
            this.loggerWithNoListener.FoodRecallNotice(
                brandName: "Contoso",
                productDescription: "Salads",
                productType: "Food & Beverages",
                recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
                companyName: "Contoso Fresh Vegetables, Inc.");
        }
    }

    [Benchmark]
    public void CreateLoggerRepeatedly()
    {
        var logger = this.loggerFactoryWithNoListener.CreateLogger<LogBenchmarks>();
        logger.FoodRecallNotice(
                brandName: "Contoso",
                productDescription: "Salads",
                productType: "Food & Beverages",
                recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
                companyName: "Contoso Fresh Vegetables, Inc.");
    }

    [Benchmark]
    public void OneProcessor()
    {
        this.loggerWithOneProcessor.FoodRecallNotice(
                brandName: "Contoso",
                productDescription: "Salads",
                productType: "Food & Beverages",
                recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
                companyName: "Contoso Fresh Vegetables, Inc.");
    }

    [Benchmark]
    public void BatchProcessor()
    {
        this.loggerWithBatchProcessor.FoodRecallNotice(
                brandName: "Contoso",
                productDescription: "Salads",
                productType: "Food & Beverages",
                recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
                companyName: "Contoso Fresh Vegetables, Inc.");
    }

    [Benchmark]
    public void TwoProcessors()
    {
        this.loggerWithTwoProcessors.FoodRecallNotice(
                brandName: "Contoso",
                productDescription: "Salads",
                productType: "Food & Beverages",
                recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
                companyName: "Contoso Fresh Vegetables, Inc.");
    }

    [Benchmark]
    public void ThreeProcessors()
    {
        this.loggerWithThreeProcessors.FoodRecallNotice(
                brandName: "Contoso",
                productDescription: "Salads",
                productType: "Food & Beverages",
                recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
                companyName: "Contoso Fresh Vegetables, Inc.");
    }

    internal class NoopLogProcessor : BaseProcessor<LogRecord>
    {
    }

    internal class NoopExporter : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            return ExportResult.Success;
        }
    }
}
