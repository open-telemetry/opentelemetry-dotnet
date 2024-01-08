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
[OpenTelemetry.Instrumentation.AspNetCore](../../../src/OpenTelemetry.Exporter.Console/README.md)
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
Export http.server.duration, Measures the duration of inbound HTTP requests., Unit: ms, Meter: OpenTelemetry.Instrumentation.AspNetCore/1.0.0.0
(2023-04-11T21:49:43.6915232Z, 2023-04-11T21:50:50.6564690Z] http.flavor: 1.1 http.method: GET http.route: / http.scheme: http http.status_code: 200 net.host.name: localhost net.host.port: 5000 Histogram
Value: Sum: 3.5967 Count: 11 Min: 0.073 Max: 2.5539
(-Infinity,0]:0
(0,5]:11
(5,10]:0
(10,25]:0
(25,50]:0
(50,75]:0
(75,100]:0
(100,250]:0
(250,500]:0
(500,750]:0
(750,1000]:0
(1000,2500]:0
(2500,5000]:0
(5000,7500]:0
(7500,10000]:0
(10000,+Infinity]:0
```

Congratulations! You are now collecting metrics using OpenTelemetry.

What does the above program do?

The program uses the
[OpenTelemetry.Instrumentation.AspNetCore](../../../src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
package to automatically create metrics for incoming ASP.NET Core requests, uses
the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
package to write metrics to the console every 1000 milliseconds. This is done by
configuring an OpenTelemetry
[MeterProvider](../customizing-the-sdk/README.MD#meterprovider) using extension
methods and setting it to auto-start when the host is started:

```csharp
appBuilder.Services.AddOpenTelemetry()
    .ConfigureResource(builder => builder
        .AddService(serviceName: "OTel.NET Getting Started"))
    .WithMetrics(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter((exporterOptions, metricReaderOptions) =>
        {
            metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
        })
    );
```

> **Note**
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
