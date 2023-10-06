// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace GettingStartedPrometheusGrafana;

public class Program
{
    private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary", "1.0");
    private static readonly Counter<long> MyFruitCounter = MyMeter.CreateCounter<long>("MyFruitCounter");

    public static void Main()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("MyCompany.MyProduct.MyLibrary")
            .AddConsoleExporter()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:9090/api/v1/otlp/v1/metrics");
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            })
            .Build();

        Console.WriteLine("Press any key to exit");
        while (!Console.KeyAvailable)
        {
            Thread.Sleep(1000);
            MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
            MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
            MyFruitCounter.Add(1, new("name", "lemon"), new("color", "yellow"));
            MyFruitCounter.Add(2, new("name", "apple"), new("color", "green"));
            MyFruitCounter.Add(5, new("name", "apple"), new("color", "red"));
            MyFruitCounter.Add(4, new("name", "lemon"), new("color", "yellow"));
        }
    }
}
