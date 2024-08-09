<!-- markdownlint-disable MD013 -->
# Getting Started with OpenTelemetry Protocol (OTLP) in 5 Minutes - ASP.NET Core Application
<!-- markdownlint-enable MD013 -->

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

Create a new web application:

```sh
dotnet new web -o aspnetcoreapp
cd aspnetcoreapp
```

Install the
[OpenTelemetry.Exporter.OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md),
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md),
and
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
packages:

```sh
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs).

Run the application (using `dotnet run`) and then browse to the .NET Aspire
dashboard (eg `http://localhost:18888/`) to view your telemetry.

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program uses the
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
package to automatically create traces and metrics for incoming ASP.NET Core
requests and uses the
[OpenTelemetry.Exporter.OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
package to export telemetry via OTLP to the .NET Aspire dashboard. This is done
by configuring the OpenTelemetry SDK using extension methods and setting it to
auto-start when the host is started:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: builder.Environment.ApplicationName))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation())
    .UseOtlpExporter();
```

> [!NOTE]
> The `AddOpenTelemetry` extension is part of the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
package.
<!-- This comment is to make sure the two notes above and below are not merged -->
> [!NOTE]
> The `UseOtlpExporter` extension configures the OpenTelemetry .NET OTLP
> exporter for logging, metrics, and tracing. For details see: [Enable OTLP
> Exporter for all
> signals](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md#enable-otlp-exporter-for-all-signals).

The index route ("/") is set up to write out the OpenTelemetry trace information
on the response:

```csharp
app.MapGet("/", () => $"Hello World! OpenTelemetry Trace: {Activity.Current?.Id}");
```

In OpenTelemetry .NET the [Activity
class](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity?view=net-7.0)
represents the OpenTelemetry Specification
[Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span).
For more details about how the OpenTelemetry Specification is implemented in
.NET see: [Introduction to OpenTelemetry .NET Tracing
API](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api#introduction-to-opentelemetry-net-tracing-api).

## Learn more

* [OpenTelemetry .NET Logs](../../logs/README.md)
* [OpenTelemetry .NET Metrics](../../metrics/README.md)
* [OpenTelemetry .NET Traces](../../trace/README.md)
