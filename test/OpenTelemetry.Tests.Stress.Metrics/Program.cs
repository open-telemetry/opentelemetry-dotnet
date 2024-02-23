// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using CommandLine;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private const int ArraySize = 10;
    private const int MaxHistogramMeasurement = 1000;

    private static readonly Meter TestMeter = new(Utils.GetCurrentMethodName());
    private static readonly Histogram<long> TestHistogram = TestMeter.CreateHistogram<long>("TestHistogram");
    private static readonly Counter<long> TestCounter = TestMeter.CreateCounter<long>("TestCounter");
    private static readonly string[] DimensionValues = new string[ArraySize];
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
    private static TestType testType;

    protected enum TestType
    {
        /// <summary>Histogram.</summary>
        Histogram,

        /// <summary>Counter.</summary>
        Counter,
    }

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<StressTestOptions>(args)
            .WithParsed(LaunchStressTest);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
        var random = ThreadLocalRandom.Value;
        if (testType == TestType.Histogram)
        {
            TestHistogram.Record(
                random.Next(MaxHistogramMeasurement),
                new("DimName1", DimensionValues[random.Next(0, ArraySize)]),
                new("DimName2", DimensionValues[random.Next(0, ArraySize)]),
                new("DimName3", DimensionValues[random.Next(0, ArraySize)]));
        }
        else if (testType == TestType.Counter)
        {
            TestCounter.Add(
               100,
               new("DimName1", DimensionValues[random.Next(0, ArraySize)]),
               new("DimName2", DimensionValues[random.Next(0, ArraySize)]),
               new("DimName3", DimensionValues[random.Next(0, ArraySize)]));
        }
    }

    protected static void WriteRunInformationToConsole(StressTestOptions options)
    {
        if (options.PrometheusTestMetricsPort != 0)
        {
            Console.Write($", testPrometheusEndpoint = http://localhost:{options.PrometheusTestMetricsPort}/metrics/");
        }
    }

    private static void LaunchStressTest(StressTestOptions options)
    {
        for (int i = 0; i < ArraySize; i++)
        {
            DimensionValues[i] = $"DimValue{i}";
        }

        var builder = Sdk.CreateMeterProviderBuilder()
            .AddMeter(TestMeter.Name);

        if (options.PrometheusTestMetricsPort != 0)
        {
            builder.AddPrometheusHttpListener(o => o.UriPrefixes = new string[] { $"http://localhost:{options.PrometheusTestMetricsPort}/" });
        }

        if (options.EnableExemplars)
        {
            builder.SetExemplarFilter(new AlwaysOnExemplarFilter());
        }

        if (options.AddViewToFilterTags)
        {
            builder
                .AddView("TestCounter", new MetricStreamConfiguration { TagKeys = new string[] { "DimName1" } })
                .AddView("TestHistogram", new MetricStreamConfiguration { TagKeys = new string[] { "DimName1" } });
        }

        if (options.AddOtlpExporter)
        {
            builder.AddOtlpExporter((exporterOptions, readerOptions) =>
            {
                readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.OtlpExporterExportIntervalMilliseconds;
            });
        }

        using var meterProvider = builder.Build();

        testType = options.TestType;

        RunStressTest(options);
    }

    protected partial class StressTestOptions
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [Option('t', "type", HelpText = "The metrics stress test type to run. Valid values: [Histogram, Counter]. Default value: Histogram.", Required = false)]
        public TestType TestType { get; set; } = TestType.Histogram;

        [Option('m', "metrics_port", HelpText = "The Prometheus http listener port where Prometheus will be exposed for retrieving test metrics while the stress test is running. Set to '0' to disable. Default value: 9185.", Required = false)]
        public int PrometheusTestMetricsPort { get; set; } = 9185;

        [Option('v', "view", HelpText = "Whether or not a view should be configured to filter tags for the stress test. Default value: False.", Required = false)]
        public bool AddViewToFilterTags { get; set; }

        [Option('o', "otlp", HelpText = "Whether or not an OTLP exporter should be added for the stress test. Default value: False.", Required = false)]
        public bool AddOtlpExporter { get; set; }

        [Option('i', "interval", HelpText = "The OTLP exporter export interval in milliseconds. Default value: 5000.", Required = false)]
        public int OtlpExporterExportIntervalMilliseconds { get; set; } = 5000;

        [Option('e', "exemplars", HelpText = "Whether or not to enable exemplars for the stress test. Default value: False.", Required = false)]
        public bool EnableExemplars { get; set; }
    }

    private sealed class NoOptions
    {
    }
}
