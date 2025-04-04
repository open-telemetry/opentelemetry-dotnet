// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal sealed class TestOTelShimWithConsoleExporter
{
    internal static int Run(OpenTelemetryShimOptions options)
    {
        // Enable OpenTelemetry for the source "MyCompany.MyProduct.MyWebServer"
        // and use a single pipeline with a custom MyProcessor, and Console exporter.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyWebServer")
            .ConfigureResource(r => r.AddService("MyServiceName"))
            .AddConsoleExporter()
            .Build();

        // The above line is required only in applications
        // which decide to use OpenTelemetry.

        var tracer = TracerProvider.Default.GetTracer("MyCompany.MyProduct.MyWebServer");
        using (var parentSpan = tracer.StartActiveSpan("parent span"))
        {
            parentSpan.SetAttribute("mystring", "value");
            parentSpan.SetAttribute("myint", 100);
            parentSpan.SetAttribute("mydouble", 101.089);
            parentSpan.SetAttribute("mybool", true);
            parentSpan.UpdateName("parent span new name");

            var childSpan = tracer.StartSpan("child span");
            childSpan.AddEvent("sample event").SetAttribute("ch", "value").SetAttribute("more", "attributes");
            childSpan.SetStatus(Status.Ok);
            childSpan.End();
        }

        System.Console.WriteLine("Press Enter key to exit.");
        System.Console.ReadLine();

        return 0;
    }
}
