# Getting Started with OpenTelemetry .NET Traces in 5 Minutes - ASP.NET Core Application

First, download and install the [.NET
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new web application:

```sh
dotnet new web -o aspnetcoreapp
cd aspnetcoreapp
```

Install the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md),
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md),
and
[OpenTelemetry.Instrumentation.AspNetCore](../../../src/OpenTelemetry.Exporter.Console/README.md)
packages:

```sh
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore --prerelease
```

> **Note** This quickstart guide uses prerelease packages. For a quickstart
> which only relies on stable packages see: [Getting Started - Console
> Application](../getting-started-console/README.md). For more information about
> when instrumentation will be marked as stable see: [Instrumentation-1.0.0
> milestone](https://github.com/open-telemetry/opentelemetry-dotnet/milestone/23).

Update the `Program.cs` file with the code from [Program.cs](./Program.cs).

Run the application again (using `dotnet run`) and then browse to the url shown
in the console for your application (ex `http://localhost:5154`). You should see
the trace output from the console.

```text
Activity.TraceId:            c1572aa14ee9c0ac037dbdc3e91e5dd7
Activity.SpanId:             45406137f33cc279
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: OpenTelemetry.Instrumentation.AspNetCore
Activity.DisplayName:        /
Activity.Kind:               Server
Activity.StartTime:          2023-01-13T19:38:11.5417593Z
Activity.Duration:           00:00:00.0167407
Activity.Tags:
    net.host.name: localhost
    net.host.port: 5154
    http.method: GET
    http.scheme: http
    http.target: /
    http.url: http://localhost:5154/
    http.flavor: 1.1
    http.user_agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36 Edg/108.0.1462.76
    http.status_code: 200
Resource associated with Activity:
    service.name: OTel.NET Getting Started
    service.instance.id: af85d327-d673-41c8-b529-b7eecf3c90f6
```

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program uses the
[OpenTelemetry.Instrumentation.AspNetCore](../../../src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
package to automatically create traces for incoming ASP.NET Core requests and
uses the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
package to write traces to the console. This is done by configuring an
OpenTelemetry [TracerProvider](../customizing-the-sdk/README.MD#tracerprovider)
using extension methods and setting it to auto-start when the host is started:

```csharp
appBuilder.Services.AddOpenTelemetry()
    .ConfigureResource(builder => builder
        .AddService(serviceName: "OTel.NET Getting Started"))
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());
```

> **Note**
> The `AddOpenTelemetry` extension is part of the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
package.

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

* [Getting Started with Jaeger](../getting-started-jaeger/README.md)
* [Customizing OpenTelemetry .NET SDK](../customizing-the-sdk/README.md)
* [Extending the OpenTelemetry .NET SDK](../extending-the-sdk/README.md)
