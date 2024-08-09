# Getting Started with OpenTelemetry Protocol (OTLP) in 5 Minutes - Console Application

If you haven't already, download and install the [.NET
SDK](https://dotnet.microsoft.com/download) and
[Docker](https://www.docker.com/) on your computer.

Install and run the [Standalone .NET Aspire
dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone)
using Docker:

> [!NOTE]
> The .NET Aspire dashboard is being used to view telemetry locally. For the
> purposes of this guide it is being used as a visualization tool to verify the
> output of the OpenTelemetry .NET SDK. For a list of vendors with support for
> ingestion of [OpenTelemetry Protocol
(OTLP)](https://github.com/open-telemetry/opentelemetry-proto/tree/main/docs)
> see: [Vendors](../README.md#vendors).

PowerShell:

```powershell
docker run --rm -it `
    -p 18888:18888 `
    -p 4317:18889 `
    -p 4318:18890 `
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true `
    -d `
    --name aspire-dashboard `
    mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

Bash:

```bash
docker run --rm -it \
    -p 18888:18888 \
    -p 4317:18889 \
    -p 4318:18890 \
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    -d \
    --name aspire-dashboard \
    mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

> [!CAUTION]
> `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS` is being used to disable
> authentication for the Aspire dashboard. For instructions on how to run with
> authentication enabled see: [Login to the
> dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone?#login-to-the-dashboard).

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

Congratulations! You are now collecting logs, metrics, and traces using
OpenTelemetry.

What does the above program do?

The program creates an
[ActivitySource](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource)
which represents an [OpenTelemetry
Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer).

```csharp
private static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");
```

The `ActivitySource` instance is used to start an
[Activity](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity)
which represents an [OpenTelemetry
Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span)
and set several `Tags`, which represents
[Attributes](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-attributes)
on it. It also sets the
[Status](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status)
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

The program creates a
[Meter](https://learn.microsoft.com/dotnet/api/system.diagnostics.metrics.meter)
which represents an [OpenTelemetry
Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
and a
[Counter&lt;int&gt;](https://learn.microsoft.com/dotnet/api/system.diagnostics.metrics.counter-1)
which represents an [OpenTelemetry
Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#counter).

```csharp
private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary");
private static readonly Counter<int> MyCounter = MyMeter.CreateCounter<int>("execution.count");
```

The `Counter<int>` is used to record a measurement:

```csharp
MyCounter.Add(1);
```

The program creates an
[ILogger&lt;Program&gt;](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)
instance to emit logs:

```csharp
var logger = openTelemetrySdk.GetLoggerFactory().CreateLogger<Program>();

logger.LogInformation("Application starting");
```

The program uses the
[OpenTelemetry.Exporter.OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
package to export telemetry via OTLP to the .NET Aspire dashboard. This is done
by starting the OpenTelemetry SDK manually and calling extension methods for
configuration:

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

The `OpenTelemetrySdk.Create` call initializes the OpenTelemetry SDK. For more
details see: [Initialize the SDK
manually](../../README.md#initialize-the-sdk-manually).

The `tracing.AddSource` and `metrics.AddMeter` calls tell the OpenTelemetry SDK
to listen to the custom `ActivitySource` and `Meter` created by the app to emit
telemetry. For more details see:
  * [Activity Source](../../trace/customizing-the-sdk#activity-source)
  * [Meter](../../metrics/customizing-the-sdk#meter)

The `UseOtlpExporter` extension configures the OpenTelemetry .NET OTLP exporter
for logging, metrics, and tracing. For more details see: [Enable OTLP Exporter
for all
signals](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md#enable-otlp-exporter-for-all-signals).

## Learn more

* [OpenTelemetry .NET Logs](../../logs/README.md)
* [OpenTelemetry .NET Metrics](../../metrics/README.md)
* [OpenTelemetry .NET Traces](../../trace/README.md)
