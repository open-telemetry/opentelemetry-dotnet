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
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
packages:

```sh
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs).

Run the application again (using `dotnet run`) and then browse to the url shown
in the console for your application (ex `http://localhost:5154`). You should see
the trace output from the console.

```text
Activity.TraceId:            c28f7b480d5c7dfc30cfbd80ad29028d
Activity.SpanId:             27e478bbf9fdec10
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: Microsoft.AspNetCore
Activity.DisplayName:        GET /
Activity.Kind:               Server
Activity.StartTime:          2024-07-04T13:03:37.3318740Z
Activity.Duration:           00:00:00.3693734
Activity.Tags:
    server.address: localhost
    server.port: 5154
    http.request.method: GET
    url.scheme: https
    url.path: /
    network.protocol.version: 2
    user_agent.original: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36
    http.route: /
    http.response.status_code: 200
Resource associated with Activity:
    service.name: getting-started-aspnetcore
    service.instance.id: a388466b-4969-4bb0-ad96-8f39527fa66b
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 1.9.0
```

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program uses the
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
package to automatically create traces for incoming ASP.NET Core requests and
uses the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
package to write traces to the console. This is done by configuring an
OpenTelemetry [TracerProvider](../customizing-the-sdk/README.MD#tracerprovider)
using extension methods and setting it to auto-start when the host is started:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: builder.Environment.ApplicationName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());
```

> [!NOTE]
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
