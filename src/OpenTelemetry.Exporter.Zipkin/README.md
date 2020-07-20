# Zipkin Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Zipkin.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Zipkin.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin)

## Prerequisite

* [Get Zipkin](https://zipkin.io/pages/quickstart.html)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Zipkin
```

## Configuration

Configure `ZipkinTraceExporter` as below:

```csharp
using (var tracerFactory = TracerFactory.Create(builder => builder
    .UseZipkin(o =>
    {
        o.ServiceName = "test-zipkin";
        o.Endpoint = new Uri(zipkinUri);
    })))
{
    var tracer = tracerFactory.GetTracer("zipkin-test");

    // Create a scoped span. It will end automatically when using statement ends
    using (tracer.WithSpan(tracer.StartSpan("Main")))
    {
        Console.WriteLine("About to do a busy work");
        for (var i = 0; i < 10; i++)
        {
            DoWork(i, tracer);
        }
    }
}
```

See
[sample](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/Console/TestZipkin.cs)
for example use.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Zipkin](https://zipkin.io)
