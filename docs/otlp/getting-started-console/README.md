# Getting Started with OpenTelemetry Protocol (OTLP) in 5 Minutes - Console Application

If you haven't already, download and install the [.NET
SDK](https://dotnet.microsoft.com/download) and
[Docker](https://www.docker.com/) on your computer.

Install and run the [Standalone .NET Aspire
dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone)
using Docker:

```sh
docker run --rm -it -p 18888:18888 -p 4317:18889 -p 4318:18890 -d --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

> [!CAUTION]
> The .NET Aspire dashboard is being used to view telemetry locally. It is a
> developer tool and not meant for production usage.

Create a new console application:

```sh
dotnet new console --output getting-started
cd getting-started
```

Install the
[OpenTelemetry.Exporter.OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
package:

```sh
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs).

Run the application (using `dotnet run`) and then browse to the .NET Aspire
dashboard (eg `http://localhost:18888/`) to view your telemetry.

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program creates an `ActivitySource` which represents an [OpenTelemetry
Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer).

```csharp
private static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");
```

The `ActivitySource` instance is used to start an `Activity` which represents an
[OpenTelemetry
Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span)
and set several `Tags`, which represents
[Attributes](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-attributes)
on it. It also sets the [Status](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status)
to be `Ok`.

```csharp
using (var activity = MyActivitySource.StartActivity("SayHello"))
{
    if (activity?.IsAllDataRequested == true)
    {
        activity.SetTag("foo", 1);
        activity.SetTag("bar", "Hello, World!");
        activity.SetTag("baz", new int[] { 1, 2, 3 });
        activity.SetStatus(ActivityStatusCode.Ok);
    }
}
```

The program creates a `Meter` which represents an [OpenTelemetry
Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
and a `Counter<int>` which represents an [OpenTelemetry
Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#counter).

```csharp
private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary");
private static readonly Counter<int> MyCounter = MyMeter.CreateCounter<int>("execution.count");
```

The `Counter<int>` is used to record a measurement:

```csharp
MyCounter.Add(1);
```

The program creates an `ILogger<Program>` instance to emit logs:

```csharp
var logger = openTelemetrySdk.GetLoggerFactory().CreateLogger<Program>();

logger.LogInformation("Application starting");
```

The program uses the
[OpenTelemetry.Exporter.OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
package to export telemetry via OTLP to the .NET Aspire dashboard. This is done
by configuring and starting the OpenTelemetry SDK using the
`OpenTelemetrySdk.Create` API (added in `1.10.0`) and extension methods:

```csharp
var openTelemetrySdk = OpenTelemetrySdk.Create(builder =>
{
    builder
        .ConfigureResource(resource => resource.AddService(serviceName: "ConsoleApp"))
        .WithTracing(tracing => tracing.AddSource(MyActivitySource.Name))
        .WithMetrics(metrics => metrics.AddMeter(MyMeter.Name))
        .UseOtlpExporter();
}
```

> [!NOTE]
> The `UseOtlpExporter` extension configures the OpenTelemetry .NET OTLP
> exporter for logging, metrics, and tracing. For details see: [Enable OTLP
> Exporter for all
> signals](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md#enable-otlp-exporter-for-all-signals).

The `tracing.AddSource` and `metrics.AddMeter` calls tell the OpenTelemetry SDK
to listen to the custom `ActivitySource` and `Meter` created by the app to emit
telemetry.

## Learn more

* [OpenTelemetry .NET Logs](../../logs/README.md)
* [OpenTelemetry .NET Metrics](../../metrics/README.md)
* [OpenTelemetry .NET Traces](../../trace/README.md)
