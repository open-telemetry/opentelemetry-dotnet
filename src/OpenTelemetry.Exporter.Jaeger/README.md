# Jaeger Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Jaeger.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Jaeger.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger)

The Jaeger exporter communicates to a Jaeger Agent through the compact thrift
protocol on the Compact Thrift API port.

## Supported .NET Versions

This package supports all the officially supported versions of [.NET
Core](https://dotnet.microsoft.com/download/dotnet-core).

For .NET Framework, versions 4.6 and above are supported.

## Prerequisite

* [Get Jaeger](https://www.jaegertracing.io/docs/1.13/getting-started/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Jaeger
```

## Configuration

You can configure the `JaegerExporter` through `JaegerExporterOptions`
properties:

* `AgentHost`: Usually `localhost` since an agent should usually be running on
  the same machine as your application or service.
* `AgentPort`: The compact thrift protocol port of the Jaeger Agent (default
  `6831`).
* `MaxPayloadSizeInBytes`: The maximum size of each UDP packet that gets
  sent to the agent. (default `4096`).
* `ProcessTags`: Which tags should be sent with telemetry.
* `ExportProcessorType`: Whether the exporter should use
  [Batch or Simple exporting processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors)
  .
* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

See the
[`TestJaegerExporter.cs`](../../examples/Console/TestJaegerExporter.cs)
for an example of how to use the exporter.

## References

* [Jaeger](https://www.jaegertracing.io)
* [OpenTelemetry Project](https://opentelemetry.io/)
