// <copyright file="TestInMemoryExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal class TestInMemoryExporter
{
    // To run this example, run the following command from
    // the reporoot\examples\Console\.
    // (eg: C:\repos\opentelemetry-dotnet\examples\Console\)
    //
    // dotnet run inmemory
    internal static object Run(InMemoryOptions options)
    {
        // List that will be populated with the traces by InMemoryExporter
        var exportedItems = new List<Activity>();

        RunWithActivitySource(exportedItems);

        // List exportedItems is populated with the Activity objects logged by TracerProvider
        foreach (var activity in exportedItems)
        {
            System.Console.WriteLine($"ActivitySource: {activity.Source.Name} logged the activity {activity.DisplayName}");
        }

        return null;
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
