# Jaeger Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Jaeger.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Jaeger.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger)

The Jaeger exporter communicates to a Jaeger Agent through the compact thrift
protocol on the Compact Thrift API port.

## Prerequisite

* [Get Jaeger](https://www.jaegertracing.io/docs/1.13/getting-started/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Jaeger
```

## Configuration

You can configure the `JaegerExporter` by following the directions below:

* `ServiceName`: The name of your application or service.
* `AgentHost`: Usually `localhost` since an agent should usually be running on
  the same machine as your application or service.
* `AgentPort`: The compact thrift protocol port of the Jaeger Agent (default
  `6831`).
* `MaxPacketSize`: The maximum size of each UDP packet that gets sent to the
  agent. (default `65000`).

See the
[`TestJaegerExporter.cs`](../../samples/Console/TestJaegerExporter.cs)
for an example of how to use the exporter.

## References

* [Jaeger](https://www.jaegertracing.io)
* [OpenTelemetry Project](https://opentelemetry.io/)
