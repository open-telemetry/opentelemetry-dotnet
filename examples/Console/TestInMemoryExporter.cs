// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal sealed class TestInMemoryExporter
{
    // To run this example, run the following command from
    // the reporoot\examples\Console\.
    // (eg: C:\repos\opentelemetry-dotnet\examples\Console\)
    //
    // dotnet run inmemory
    internal static int Run(InMemoryOptions options)
    {
        // List that will be populated with the traces by InMemoryExporter
        var exportedItems = new List<Activity>();

        RunWithActivitySource(exportedItems);

        // List exportedItems is populated with the Activity objects logged by TracerProvider
        foreach (var activity in exportedItems)
        {
            System.Console.WriteLine($"ActivitySource: {activity.Source.Name} logged the activity {activity.DisplayName}");
        }

        return 0;
    }

    private static void RunWithActivitySource(ICollection<Activity> exportedItems)
    {
        // Enable OpenTelemetry for the sources "Samples.SampleServer" and "Samples.SampleClient"
        // and use InMemory exporter.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Samples.SampleClient", "Samples.SampleServer")
            .ConfigureResource(r => r.AddService("inmemory-test"))
            .AddInMemoryExporter(exportedItems)
            .Build();

        // The above line is required only in applications
        // which decide to use OpenTelemetry.
        using var sample = new InstrumentationWithActivitySource();
        sample.Start();

        System.Console.WriteLine("Traces are being created and exported " +
            "to the collection passed in the background. " +
            "Press ENTER to stop.");
        System.Console.ReadLine();
    }
}
