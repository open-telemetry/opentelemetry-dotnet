// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Examples.Console;

internal sealed class TestMetrics
{
    internal static int Run(MetricsOptions options)
    {
        var meterVersion = "1.0";
        var meterTags = new List<KeyValuePair<string, object?>>
        {
            new(
                "MeterTagKey",
                "MeterTagValue"),
        };
        using var meter = new Meter("TestMeter", meterVersion, meterTags);

        var providerBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("myservice"))
            .AddMeter(meter.Name); // All instruments from this meter are enabled.

        if ("otlp".Equals(options.UseExporter, StringComparison.OrdinalIgnoreCase))
        {
            /*
             * Prerequisite to run this example:
             * Set up an OpenTelemetry Collector to run on local docker.
             *
             * Open a terminal window at the examples/Console/ directory and
             * launch the OpenTelemetry Collector with an OTLP receiver, by running:
             *
             *  - On Unix based systems use:
             *     docker run --rm -it -p 4317:4317 -v $(pwd):/cfg otel/opentelemetry-collector:0.123.0 --config=/cfg/otlp-collector-example/config.yaml
             *
             *  - On Windows use:
             *     docker run --rm -it -p 4317:4317 -v "%cd%":/cfg otel/opentelemetry-collector:0.123.0 --config=/cfg/otlp-collector-example/config.yaml
             *
             * Open another terminal window at the examples/Console/ directory and
             * launch the OTLP example by running:
             *
             *     dotnet run metrics --useExporter otlp -e http://localhost:4317
             *
             * The OpenTelemetry Collector will output all received metrics to the stdout of its terminal.
             *
             */

            providerBuilder
                .AddOtlpExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Protocol = options.UseGrpc ? default : OtlpExportProtocol.HttpProtobuf;
#if NETFRAMEWORK
                    if (exporterOptions.Protocol == default)
                    {
                        exporterOptions.HttpClientFactory = () =>
                        {
                            var handler = new WinHttpHandler
                            {
                                ServerCertificateValidationCallback = (_, _, _, _) => true,
                            };

                            return new HttpClient(handler)
                            {
                                Timeout = TimeSpan.FromMilliseconds(exporterOptions.TimeoutMilliseconds),
                            };
                        };
                    }
#endif
                    if (!string.IsNullOrWhiteSpace(options.Endpoint))
                    {
                        exporterOptions.Endpoint = new Uri(options.Endpoint);
                    }

                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.DefaultCollectionPeriodMilliseconds;
                    metricReaderOptions.TemporalityPreference = options.IsDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
                });
        }
        else
        {
            providerBuilder
                .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Targets = ConsoleExporterOutputTargets.Console;

                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.DefaultCollectionPeriodMilliseconds;
                    metricReaderOptions.TemporalityPreference = options.IsDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
                });
        }

        using var provider = providerBuilder.Build();

        Counter<int>? counter = null;
        if (options.FlagCounter ?? true)
        {
            counter = meter.CreateCounter<int>("counter", "things", "A count of things");
        }

        Histogram<int>? histogram = null;
        if (options.FlagHistogram ?? false)
        {
            histogram = meter.CreateHistogram<int>("histogram");
        }

        if (options.FlagGauge ?? false)
        {
            var observableCounter = meter.CreateObservableGauge("gauge", () =>
            {
                return new List<Measurement<int>>()
                {
                    new(
                        (int)Process.GetCurrentProcess().PrivateMemorySize64,
                        new KeyValuePair<string, object?>("tag1", "value1")),
                };
            });
        }

        System.Console.WriteLine("Press any key to exit.");
        while (!System.Console.KeyAvailable)
        {
            histogram?.Record(10);

            histogram?.Record(
                100,
                new KeyValuePair<string, object?>("tag1", "value1"));

            histogram?.Record(
                200,
                new KeyValuePair<string, object?>("tag1", "value2"),
                new KeyValuePair<string, object?>("tag2", "value2"));

            histogram?.Record(
                100,
                new KeyValuePair<string, object?>("tag1", "value1"));

            histogram?.Record(
                200,
                new KeyValuePair<string, object?>("tag2", "value2"),
                new KeyValuePair<string, object?>("tag1", "value2"));

            counter?.Add(10);

            counter?.Add(
                100,
                new KeyValuePair<string, object?>("tag1", "value1"));

            counter?.Add(
                200,
                new KeyValuePair<string, object?>("tag1", "value2"),
                new KeyValuePair<string, object?>("tag2", "value2"));

            counter?.Add(
                100,
                new KeyValuePair<string, object?>("tag1", "value1"));

            counter?.Add(
                200,
                new KeyValuePair<string, object?>("tag2", "value2"),
                new KeyValuePair<string, object?>("tag1", "value2"));

            Task.Delay(500).Wait();
        }

        return 0;
    }
}
