// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal class TestConsoleExporter
{
    // To run this example, run the following command from
    // the reporoot\examples\Console\.
    // (eg: C:\repos\opentelemetry-dotnet\examples\Console\)
    //
    // dotnet run console
    internal static object Run(ConsoleOptions options)
    {
        return RunWithActivitySource();
    }

    private static object RunWithActivitySource()
    {
        // Enable OpenTelemetry for the sources "Samples.SampleServer" and "Samples.SampleClient"
        // and use Console exporter.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Samples.SampleClient", "Samples.SampleServer")
                .ConfigureResource(res => res.AddService("console-test"))
                .AddProcessor(new MyProcessor()) // This must be added before ConsoleExporter
                .AddConsoleExporter()
                .Build();

        // The above line is required only in applications
        // which decide to use OpenTelemetry.
        using (var sample = new InstrumentationWithActivitySource())
        {
            sample.Start();

            System.Console.WriteLine("Traces are being created and exported " +
                "to Console in the background. " +
                "Press ENTER to stop.");
            System.Console.ReadLine();
        }

        return null;
    }

    /// <summary>
    /// An example of custom processor which
    /// can be used to add more tags to an activity.
    /// </summary>
    internal class MyProcessor : BaseProcessor<Activity>
    {
        public override void OnStart(Activity activity)
        {
            if (activity.IsAllDataRequested)
            {
                if (activity.Kind == ActivityKind.Server)
                {
                    activity.SetTag("customServerTag", "Custom Tag Value for server");
                }
                else if (activity.Kind == ActivityKind.Client)
                {
                    activity.SetTag("customClientTag", "Custom Tag Value for Client");
                }
            }
        }
    }
}
