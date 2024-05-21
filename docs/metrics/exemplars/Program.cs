// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new("OpenTelemetry.Demo.Exemplar");
    private static readonly Meter MyMeter = new("OpenTelemetry.Demo.Exemplar");
    private static readonly Counter<long> MyFruitCounter = MyMeter.CreateCounter<long>("MyFruitCounter");

    public static void Main()
    {
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("OpenTelemetry.Demo.Exemplar")
            .AddOtlpExporter()
            .Build();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("OpenTelemetry.Demo.Exemplar")
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddOtlpExporter((exporterOptions, metricReaderOptions) =>
            {
                exporterOptions.Endpoint = new Uri("http://localhost:9090/api/v1/otlp/v1/metrics");
                exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
            })
            .Build();

        Console.WriteLine("Press any key to exit");

        while (!Console.KeyAvailable)
        {
            using (var parent = MyActivitySource.StartActivity("Parent Operation"))
            {
                parent?.SetTag("key1", "value1");
                parent?.SetTag("key2", "value2");

                using (var child = MyActivitySource.StartActivity("Child Operation"))
                {
                    child?.SetTag("key3", "value3");
                    child?.SetTag("key4", "value4");

                    MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
                    MyFruitCounter.Add(1, new("name", "lemon"), new("color", "yellow"));
                }
            }

            MyFruitCounter.Add(2, new("name", "apple"), new("color", "green"));
            MyFruitCounter.Add(4, new("name", "lemon"), new("color", "yellow"));

            Thread.Sleep(300);
        }

        // Dispose meter provider before the application ends.
        // This will flush the remaining metrics and shutdown the metrics pipeline.
        meterProvider.Dispose();

        // Dispose tracer provider before the application ends.
        // This will flush the remaining spans and shutdown the tracing pipeline.
        tracerProvider.Dispose();
    }
}
