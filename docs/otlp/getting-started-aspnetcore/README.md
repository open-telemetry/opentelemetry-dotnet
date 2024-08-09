<!-- markdownlint-disable MD013 -->
# Getting Started with OpenTelemetry Protocol (OTLP) in 5 Minutes - ASP.NET Core Application
<!-- markdownlint-enable MD013 -->

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
> see: [Vendors](../README.md#vendor-support).

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

Congratulations! You are now collecting logs, metrics, and traces using
OpenTelemetry.

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

The `AddOpenTelemetry` extension is part of the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
package and registers the OpenTelemetry SDK into the host. For more details see:
[Initialize the SDK using a host](../../README.md#initialize-the-sdk-using-a-host).

The `tracing.AddAspNetCoreInstrumentation()` and
`metrics.AddAspNetCoreInstrumentation()` calls register AspNetCore
instrumentation. For more details see:
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md).

The `UseOtlpExporter` extension configures the OpenTelemetry .NET OTLP exporter
for logging, metrics, and tracing. For more details see: [Enable OTLP Exporter
for all
signals](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md#enable-otlp-exporter-for-all-signals).

The programs maps the index route ("/") to write out the OpenTelemetry trace
information on the response:

```csharp
app.MapGet("/", () => $"Hello World! OpenTelemetry Trace: {Activity.Current?.Id}");
```

In OpenTelemetry .NET the [Activity
class](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity)
represents the OpenTelemetry Specification
[Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span).
For more details about how the OpenTelemetry Specification is implemented in
.NET see: [Introduction to OpenTelemetry .NET Tracing
API](../../../src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api).

## Next steps

Explore and add relevant [instrumentation
libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
and resource detectors to your OpenTelemetry configuration by visiting the
[opentelemetry-dotnet-contrib
repository](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)
and/or the [OpenTelemetry
registry](https://opentelemetry.io/ecosystem/registry/?language=dotnet).
Instrumentation libraries help automatically generate telemetry for common
application tasks such as making an [outgoing HTTP
call](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.Http).
Resource detectors help decorate telemetry with information about the hosting
environment.

Use the
[ActivitySource](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource),
[Meter](https://learn.microsoft.com/dotnet/api/system.diagnostics.metrics.meter),
and
[ILogger&lt;T&gt;](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)
APIs to add custom telemetry to your application and/or libraries. The [Getting
Started with OpenTelemetry Protocol (OTLP) in 5 Minutes - Console
Application](../getting-started-console/README.md) guide contains an example for
how to add telemetry manually.

Explore how to use
[samplers](../../trace/customizing-the-sdk/README.md#samplers) to control
distributed tracing costs.

Consider turning on advanced features such as
[exemplars](../../metrics/customizing-the-sdk/README.md#exemplars) to correlate
metrics to distributed traces.

Deploy your application to production. This guide uses OpenTelemetry Protocol
(OTLP) defaults which means all telemetry will be sent to
`http://localhost:4317` using the `OtlpExportProtocol.Grpc` protocol. But these
settings may be configured using a variety of mechanisms. For details about how
to configure the `OpenTelemetry.Exporter.OpenTelemetryProtocol` package see:
[Configuration](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md#configuration).

## Learn more

* [OpenTelemetry .NET Logs](../../logs/README.md)
* [OpenTelemetry .NET Metrics](../../metrics/README.md)
* [OpenTelemetry .NET Traces](../../trace/README.md)
