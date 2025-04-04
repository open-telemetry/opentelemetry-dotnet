// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal sealed class TestZipkinExporter
{
    internal static int Run(ZipkinOptions options)
    {
        // Prerequisite for running this example.
        // Setup zipkin inside local docker using following command:
        // docker run -d -p 9411:9411 openzipkin/zipkin

        // To run this example, run the following command from
        // the reporoot\examples\Console\.
        // (eg: C:\repos\opentelemetry-dotnet\examples\Console\)
        //
        // dotnet run zipkin -u http://localhost:9411/api/v2/spans

        // Enable OpenTelemetry for the sources "Samples.SampleServer" and "Samples.SampleClient"
        // and use the Zipkin exporter.

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Samples.SampleClient", "Samples.SampleServer")
            .ConfigureResource(r => r.AddService("zipkin-test"))
            .AddZipkinExporter(o =>
            {
                o.Endpoint = new Uri(options.Uri);
            })
            .Build();

        using var sample = new InstrumentationWithActivitySource();
        sample.Start();

        System.Console.WriteLine("Traces are being created and exported " +
                                 "to Zipkin in the background. Use Zipkin to view them. " +
                                 "Press ENTER to stop.");
        System.Console.ReadLine();

        return 0;
    }
}
