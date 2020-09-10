# ZPages Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.ZPages.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.ZPages)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.ZPages.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.ZPages)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.ZPages
```

## Configuration

You can configure the `ZPagesExporter` by following the directions below:

* `Url`: The url to listen to. Typically it ends with `/rpcz` like `http://localhost:7284/rpcz/`.
* `RetentionTime`: The retention time (in milliseconds) for the metrics.

See the
[`TestZPagesExporter.cs`](../../examples/Console/TestZPagesExporter.cs)
for an example of how to use the exporter.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [zPages](https://opencensus.io/zpages/)
