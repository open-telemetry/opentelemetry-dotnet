# Getting Started with OpenTelemetry .NET Metrics in 5 Minutes - ASP.NET Core Application

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
the metrics output from the console.

```text
Metric Name: http.server.request.duration, Description: Duration of HTTP server requests., Unit: s, Metric Type: Histogram
Instrumentation scope (Meter):
        Name: Microsoft.AspNetCore.Hosting
(2026-04-02T22:41:03.8661885Z, 2026-04-02T22:41:12.0061720Z] http.request.method: GET http.response.status_code: 200 http.route: / network.protocol.version: 1.1 url.scheme: http
Value: Sum: 0.0276842 Count: 1 Min: 0.0276842 Max: 0.0276842
(-Infinity,0.005]:0
(0.005,0.01]:0
(0.01,0.025]:0
(0.025,0.05]:1
(0.05,0.075]:0
(0.075,0.1]:0
(0.1,0.25]:0
(0.25,0.5]:0
(0.5,0.75]:0
(0.75,1]:0
(1,2.5]:0
(2.5,5]:0
(5,7.5]:0
(7.5,10]:0
(10,+Infinity]:0
```

Congratulations! You are now collecting metrics using OpenTelemetry.

What does the above program do?

The program uses the
[OpenTelemetry.Instrumentation.AspNetCore](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
package to automatically create metrics for incoming ASP.NET Core requests, uses
the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
package to write metrics to the console every 1000 milliseconds. This is done by
configuring an OpenTelemetry
[MeterProvider](../customizing-the-sdk/README.MD#meterprovider) using extension
methods and setting it to auto-start when the host is started:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: builder.Environment.ApplicationName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
        {
            metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
        }));
```

> [!NOTE]
> The `AddOpenTelemetry` extension is part of the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
package.

The index route ("/") is set up to write out a greeting message on the response:

```csharp
app.MapGet("/", () => $"Hello from OpenTelemetry Metrics!");
```

## Learn more

* [Getting Started with Prometheus and Grafana](../getting-started-prometheus-grafana/README.md)
* [Customizing OpenTelemetry .NET SDK](../customizing-the-sdk/README.md)
* [Extending the OpenTelemetry .NET SDK](../extending-the-sdk/README.md)
