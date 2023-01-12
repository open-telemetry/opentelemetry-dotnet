# Getting Started with OpenTelemetry .NET in 5 Minutes - ASP.NET Core Application

First, download and install the [.NET
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new web application:

```sh
dotnet new webapp -o aspnetcoreapp
cd aspnetcoreapp
```

Install the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
packages:

```sh
dotnet add package OpenTelemetry.Exporter.Console --prerelease
dotnet add package OpenTelemetry.Extensions.Hosting --prerelease
dotnet add package OpenTelemetry.Instrumentation.AspNetCore --prerelease
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs).

Run the application again (using `dotnet run`) and then browse to the url shown
in the console for your application (ex `http://localhost:5033`). You should see
the trace output from the console.

```text
Activity.TraceId:            27ceaa21a82883dc4f5365e3c00dbf62
Activity.SpanId:             34eaacb083842c6e
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: Microsoft.AspNetCore
Activity.DisplayName:        /
Activity.Kind:               Server
Activity.StartTime:          2023-01-11T23:02:22.1251620Z
Activity.Duration:           00:00:00.1825101
Activity.Tags:
    net.host.name: localhost
    net.host.port: 5033
    http.method: GET
    http.scheme: http
    http.target: /
    http.url: http://localhost:5033/
    http.flavor: 1.1
    http.user_agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36 Edg/108.0.1462.76
    http.status_code: 200
Resource associated with Activity:
    service.name: OTel.NET Getting Started
    service.instance.id: d828fb42-f423-4ec4-8276-9405998a4bd7
```

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program uses the
[OpenTelemetry.Instrumentation.AspNetCore](../../../src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
project to automatically create traces for incoming ASP.NET Core requests and
uses the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
project to write traces to the console. This is done by configuring an
OpenTelemetry [TracerProvider](../customizing-the-sdk/README.MD#tracerprovider)
using extension methods and setting it to auto-start when the host is started:

```csharp
appBuilder.Services.AddOpenTelemetry()
    .ConfigureResource(builder => builder
        .AddService(serviceName: "OTel.NET Getting Started"))
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .StartWithHost();
```

**Note:** The `StartWithHost` extension is part of the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
project.

As an additional exercise, try modifying the Index page
([PageModel](./Pages/Index.cshtml.cs) and [RazorPage](./Pages/Index.cshtml)) to
display the active trace information.

## Learn more

* [Getting Started with Jaeger](../getting-started-jaeger/README.md)
* [Customizing OpenTelemetry .NET SDK](../customizing-the-sdk/README.md)
* [Extending the OpenTelemetry .NET SDK](../extending-the-sdk/README.md)
