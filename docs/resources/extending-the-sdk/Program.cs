// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ExtendingTheSdk;

internal static class Program
{
    private static readonly ActivitySource DemoSource = new("OTel.Demo");
    private static readonly Meter MeterDemoSource = new("OTel.Demo");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("OTel.Demo")
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddDetector(
                new MyResourceDetector()))
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddDetector(
                new MyResourceDetector()))
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddDetector(
                    new MyResourceDetector()));
            });
        });

        using (var foo = DemoSource.StartActivity("Foo"))
        {
            using (var bar = DemoSource.StartActivity("Bar"))
            {
                using (var baz = DemoSource.StartActivity("Baz"))
                {
                }
            }
        }

        var counter = MeterDemoSource.CreateCounter<long>("counter");
        for (var i = 0; i < 20000; i++)
        {
            counter.Add(1, new KeyValuePair<string, object?>("tag1", "value1"), new KeyValuePair<string, object?>("tag2", "value2"));
        }

        var logger = loggerFactory.CreateLogger("OTel.Demo");
        logger
            .LogInformation("Hello from {Name} {Price}", "tomato", 2.99);
    }
}
