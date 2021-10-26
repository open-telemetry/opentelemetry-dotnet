# Jaeger Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Jaeger.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Jaeger.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger)

The Jaeger exporter converts OpenTelemetry traces into the Jaeger model
following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/jaeger.md).

The exporter communicates to a Jaeger Agent through the thrift protocol on
the Compact Thrift API port, and as such only supports Thrift over UDP.

## Supported .NET Versions

This package supports all the officially supported versions of [.NET
Core](https://dotnet.microsoft.com/download/dotnet-core).

For .NET Framework, versions 4.6.1 and above are supported.

## Prerequisite

* [Get Jaeger](https://www.jaegertracing.io/docs/1.13/getting-started/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Jaeger
```

## Configuration

You can configure the `JaegerExporter` through `JaegerExporterOptions`
and environment variables. The `JaegerExporterOptions` setters
take precedence over the environment variables.

## Options Properties

The `JaegerExporter` can be configured using the `JaegerExporterOptions`
properties:

* `AgentHost`: The Jaeger Agent host (default `localhost`).
* `AgentPort`: The compact thrift protocol UDP port of the Jaeger Agent
  (default `6831`).
* `MaxPayloadSizeInBytes`: The maximum size of each UDP packet that gets
  sent to the agent (default `4096`).
* `ExportProcessorType`: Whether the exporter should use
  [Batch or Simple exporting processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors)
  (default `ExportProcessorType.Batch`).
* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

See the
[`TestJaegerExporter.cs`](../../examples/Console/TestJaegerExporter.cs)
for an example of how to use the exporter.

## Environment Variables

The following environment variables can be used to override the default
values of the `JaegerExporterOptions`.

| Environment variable              | `JaegerExporterOptions` property |
| --------------------------------- | -------------------------------- |
| `OTEL_EXPORTER_JAEGER_AGENT_HOST` | `AgentHost`                      |
| `OTEL_EXPORTER_JAEGER_AGENT_PORT` | `AgentPort`                      |

`FormatException` is thrown in case of an invalid value for any of the
supported environment variables.

## References

* [Jaeger](https://www.jaegertracing.io)
* [OpenTelemetry Project](https://opentelemetry.io/)
