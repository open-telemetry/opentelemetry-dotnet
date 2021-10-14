# OTLP Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)

[The OTLP (OpenTelemetry Protocol) exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md)
implementation.

## Prerequisite

* [Get OpenTelemetry Collector](https://opentelemetry.io/docs/collector/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

## Configuration

You can configure the `OtlpExporter` through `OtlpExporterOptions`
properties and environment variables. The `OtlpExporterOptions`
setters take precedence over the environment variables.

## Options Properties

* `Endpoint`: Target to which the exporter is going to send traces or metrics.
  The endpoint must be a valid Uri with scheme (http or https) and host, and MAY
  contain a port and path.
* `Headers`: Optional headers for the connection.
* `TimeoutMilliseconds` : Max waiting time for the backend to process a batch.
* `Protocol`: OTLP transport protocol. Supported values:
  `OtlpExportProtocol.Grpc` and `OtlpExportProtocol.HttpProtobuf`.
* `ExportProcessorType`: Whether the exporter should use [Batch or Simple
  exporting
  processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors)
  .
* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

See the [`TestOtlpExporter.cs`](../../examples/Console/TestOtlpExporter.cs) for
an example of how to use the exporter.

## Environment Variables

The following environment variables can be used to override the default
values of the `OtlpExporterOptions`
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md)).

| Environment variable          | `OtlpExporterOptions` property    |
| ------------------------------| ----------------------------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `Endpoint`                        |
| `OTEL_EXPORTER_OTLP_HEADERS`  | `Headers`                         |
| `OTEL_EXPORTER_OTLP_TIMEOUT`  | `TimeoutMilliseconds`             |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `Protocol` (grpc or http/protobuf)|

## OTLP Logs

This package currently only supports exporting traces and metrics. Once the
[OTLP log data model](https://github.com/open-telemetry/opentelemetry-proto#maturity-level)
is deemed stable, the OTLP log exporter will be folded into this package.

In the meantime, support for exporting logs is provided by installing the
[`OpenTelemetry.Exporter.OpenTelemetryProtocol.Logs`](../OpenTelemetry.Exporter.OpenTelemetryProtocol.Logs/README.md)
package.

## Special case when using insecure channel

If your application is targeting .NET Core 3.1, and you are using an insecure
(HTTP) endpoint, the following switch must be set before adding `OtlpExporter`.

```csharp
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport",
 true);
```

See
[this](https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client)
for more information.

## References

* [OpenTelemetry
  Collector](https://github.com/open-telemetry/opentelemetry-collector)
* [OpenTelemetry Project](https://opentelemetry.io/)
* [OTLP proto files](https://github.com/open-telemetry/opentelemetry-proto)
