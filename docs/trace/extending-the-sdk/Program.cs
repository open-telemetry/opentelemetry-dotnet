// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ExtendingTheSdk;

internal static class Program
{
    private static readonly ActivitySource DemoSource = new("OTel.Demo");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new MySampler())
            .AddSource("OTel.Demo")
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddDetector(new MyResourceDetector()))
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new MyProcessor("ProcessorA"))
            .AddProcessor(new MyProcessor("ProcessorB"))
            .AddProcessor(new SimpleActivityExportProcessor(new MyExporter("ExporterX")))
#pragma warning restore CA2000 // Dispose objects before losing scope
            .AddMyExporter()
            .Build();

        using (var foo = DemoSource.StartActivity("Foo"))
        {
            using (var bar = DemoSource.StartActivity("Bar"))
            {
                using (var baz = DemoSource.StartActivity("Baz"))
                {
                }
            }
        }
    }
}
