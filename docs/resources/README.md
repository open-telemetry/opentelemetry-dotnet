# Resources

## Resource Detector

OpenTelemetry .NET SDK provides a resource detector for detecting resource
information from the `OTEL_RESOURCE_ATTRIBUTES` and `OTEL_SERVICE_NAME`
environment variables.

Custom resource detectors can be implemented:

* ResourceDetectors should inherit from
  `OpenTelemetry.Resources.IResourceDetector`, (which belongs to the
  [OpenTelemetry](../../src/OpenTelemetry/README.md) package), and implement
  the `Detect` method.

A demo `ResourceDetector` is shown [here](./extending-the-sdk/MyResourceDetector.cs):

```csharp
using OpenTelemetry.Resources;

internal class MyResourceDetector : IResourceDetector
{
    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("key", "val"),
        };

        return new Resource(attributes);
    }
}
```

There are two different ways to add the custom `ResourceDetector` to the
OTEL signals, via the `Sdk.Create` approach:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ExtendingTheSdk;

public class Program
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
                options.SetResourceBuilder(ResourceBuilder
                    .CreateDefault().AddDetector(
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
            counter.Add(1, new("tag1", "value1"), new("tag2", "value2"));

        var logger = loggerFactory.CreateLogger("OTel.Demo");
        logger
            .LogInformation("Hello from {name} {price}.", "tomato", 2.99);
    }
}
```

or via `OpenTelemetry.Extensions.Hosting` method:

```csharp
    services.AddSingleton<MyResourceDetector>();

    services.AddOpenTelemetry()
        .ConfigureResource(builder => builder
            .AddDetector(sp =>
                sp.GetRequiredService<MyResourceDetector>()))
        .WithTracing(builder => builder.AddConsoleExporter())
        .WithMetrics(builder => builder.AddConsoleExporter());
```
