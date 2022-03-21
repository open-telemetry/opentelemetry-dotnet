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

* `AgentHost`: The Jaeger Agent host (default `localhost`). Used for
  `UdpCompactThrift` protocol.

* `AgentPort`: The Jaeger Agent port (default `6831`). Used for
  `UdpCompactThrift` protocol.

* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if `ExportProcessorType` is set to `Batch`.

* `Endpoint`: The Jaeger Collector HTTP endpoint (default
  `http://localhost:14268`). Used for `HttpBinaryThrift` protocol.

* `ExportProcessorType`: Whether the exporter should use [Batch or Simple
  exporting
  processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors)
  (default `ExportProcessorType.Batch`).

* `HttpClientFactory`: A factory function called to create the `HttpClient`
  instance that will be used at runtime to transmit spans over HTTP when the
  `HttpBinaryThrift` protocol is configured. See [Configure
  HttpClient](#configure-httpclient) for more details.

* `MaxPayloadSizeInBytes`: The maximum size of each batch that gets sent to the
  agent or collector (default `4096`).

* `Protocol`: The protocol to use. The default value is `UdpCompactThrift`.

  | Protocol         | Description                                           |
  |------------------|-------------------------------------------------------|
  |`UdpCompactThrift`| Apache Thrift compact over UDP to a Jaeger Agent.     |
  |`HttpBinaryThrift`| Apache Thrift binary over HTTP to a Jaeger Collector. |

See the [`TestJaegerExporter.cs`](../../examples/Console/TestJaegerExporter.cs)
for an example of how to use the exporter.

## Environment Variables

The following environment variables can be used to override the default
values of the `JaegerExporterOptions`
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#jaeger-exporter)).

| Environment variable              | `JaegerExporterOptions` property                          |
|-----------------------------------|-----------------------------------------------------------|
| `OTEL_EXPORTER_JAEGER_AGENT_HOST` | `AgentHost`                                               |
| `OTEL_EXPORTER_JAEGER_AGENT_PORT` | `AgentPort`                                               |
| `OTEL_EXPORTER_JAEGER_ENDPOINT`   | `Endpoint`                                                |
| `OTEL_EXPORTER_JAEGER_PROTOCOL`   | `Protocol` (`udp/thrift.compact` or `http/thrift.binary`) |

`FormatException` is thrown in case of an invalid value for any of the
supported environment variables.

## Configure HttpClient

The `HttpClientFactory` option is provided on `JaegerExporterOptions` for users
who want to configure the `HttpClient` used by the `JaegerExporter` when
`HttpBinaryThrift` protocol is used. Simply replace the function with your own
implementation if you want to customize the generated `HttpClient`:

```csharp
services.AddOpenTelemetryTracing((builder) => builder
    .AddJaegerExporter(o =>
    {
        o.Protocol = JaegerExportProtocol.HttpBinaryThrift;
        o.HttpClientFactory = () =>
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value");
            return client;
        };
    }));
```

For users using
[IHttpClientFactory](https://docs.microsoft.com/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
you may also customize the named "JaegerExporter" `HttpClient` using the
built-in `AddHttpClient` extension:

```csharp
services.AddHttpClient(
    "JaegerExporter",
    configureClient: (client) =>
        client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value"));
```

Note: The single instance returned by `HttpClientFactory` is reused by all
export requests.

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Exporter-Jaeger" for its internal logging. Please
refer to [SDK troubleshooting](../OpenTelemetry/README.md#troubleshooting) for
instructions on seeing these internal logs.

## References

* [Jaeger](https://www.jaegertracing.io)
* [OpenTelemetry Project](https://opentelemetry.io/)
