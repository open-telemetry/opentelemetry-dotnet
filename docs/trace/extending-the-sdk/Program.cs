// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ExtendingTheSdk;

public static class Program
{
    private static readonly ActivitySource DemoSource = new("OTel.Demo");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new MySampler())
            .AddSource("OTel.Demo")
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddDetector(new MyResourceDetector()))
            .AddProcessor(new MyProcessor("ProcessorA"))
            .AddProcessor(new MyProcessor("ProcessorB"))
            .AddProcessor(new SimpleActivityExportProcessor(new MyExporter("ExporterX")))
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
