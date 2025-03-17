// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Text.Json.Serialization;
using CommandLine;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Tests.Stress;

public static class Program
{
    private enum MetricsStressTestType
    {
        /// <summary>Histogram.</summary>
        Histogram,

        /// <summary>Counter.</summary>
        Counter,
    }

    public static int Main(string[] args)
    {
        return StressTestFactory.RunSynchronously<MetricsStressTest, MetricsStressTestOptions>(args);
    }

    private sealed class MetricsStressTest : StressTests<MetricsStressTestOptions>
    {
        private const int ArraySize = 10;
        private const int MaxHistogramMeasurement = 1000;

        private static readonly Meter TestMeter = new(Utils.GetCurrentMethodName());
        private static readonly Histogram<long> TestHistogram = TestMeter.CreateHistogram<long>("TestHistogram");
        private static readonly Counter<long> TestCounter = TestMeter.CreateCounter<long>("TestCounter");
        private static readonly string[] DimensionValues = new string[ArraySize];
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
        private readonly MeterProvider meterProvider;

        static MetricsStressTest()
        {
            for (int i = 0; i < ArraySize; i++)
            {
                DimensionValues[i] = $"DimValue{i}";
            }
        }

        public MetricsStressTest(MetricsStressTestOptions options)
            : base(options)
        {
            var builder = Sdk.CreateMeterProviderBuilder().AddMeter(TestMeter.Name);

            if (options.PrometheusTestMetricsPort != 0)
            {
                builder.AddPrometheusHttpListener(o => o.UriPrefixes = [$"http://localhost:{options.PrometheusTestMetricsPort}/"]);
            }

            if (options.EnableExemplars)
            {
                builder.SetExemplarFilter(ExemplarFilterType.AlwaysOn);
            }

            if (options.AddViewToFilterTags)
            {
                builder
                    .AddView("TestCounter", new MetricStreamConfiguration { TagKeys = ["DimName1"] })
                    .AddView("TestHistogram", new MetricStreamConfiguration { TagKeys = ["DimName1"] });
            }

            if (options.AddOtlpExporter)
            {
                builder.AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.OtlpExporterExportIntervalMilliseconds;
                });
            }

            this.meterProvider = builder.Build();
        }

        public override void Dispose()
        {
            this.meterProvider.Dispose();
            base.Dispose();
        }

        protected override void WriteRunInformationToConsole()
        {
            if (this.Options.PrometheusTestMetricsPort != 0)
            {
                Console.Write($", testPrometheusEndpoint = http://localhost:{this.Options.PrometheusTestMetricsPort}/metrics/");
            }
        }

        protected override void RunWorkItemInParallel()
        {
            var random = ThreadLocalRandom.Value!;
            if (this.Options.TestType == MetricsStressTestType.Histogram)
            {
                TestHistogram.Record(
                    random.Next(MaxHistogramMeasurement),
                    new("DimName1", DimensionValues[random.Next(0, ArraySize)]),
                    new("DimName2", DimensionValues[random.Next(0, ArraySize)]),
                    new("DimName3", DimensionValues[random.Next(0, ArraySize)]));
            }
            else if (this.Options.TestType == MetricsStressTestType.Counter)
            {
                TestCounter.Add(
                   100,
                   new("DimName1", DimensionValues[random.Next(0, ArraySize)]),
                   new("DimName2", DimensionValues[random.Next(0, ArraySize)]),
                   new("DimName3", DimensionValues[random.Next(0, ArraySize)]));
            }
        }
    }

    private sealed class MetricsStressTestOptions : StressTestOptions
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [Option('t', "type", HelpText = "The metrics stress test type to run. Valid values: [Histogram, Counter]. Default value: Histogram.", Required = false)]
        public MetricsStressTestType TestType { get; set; } = MetricsStressTestType.Histogram;

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
}
