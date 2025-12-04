# Zipkin Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Zipkin.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Zipkin.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin)

> [!IMPORTANT]
> This package is [Deprecated](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/versioning-and-stability.md)
> and it will stop receiving any updates in December 2026.
> Zipkin already supports OTLP ingestion. Change your exporter to
> `OpenTelemetry.Exporter.OpenTelemetryProtocol` as stated in the
> [Deprecating Zipkin Exporter blog post](https://opentelemetry.io/blog/2025/deprecating-zipkin-exporters/).

## Prerequisite

* [Get Zipkin](https://zipkin.io/pages/quickstart.html)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Zipkin
```

## Enable/Add Zipkin as a tracing exporter

You can enable the `ZipkinExporter` with the `AddZipkinExporter()` extension
method on `TracerProviderBuilder`.

## Configuration

You can configure the `ZipkinExporter` through `ZipkinExporterOptions`
and environment variables. The `ZipkinExporterOptions` setters
take precedence over the environment variables.

### Configuration using Properties

* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

* `Endpoint`: URI address to receive telemetry (default
  `http://localhost:9411/api/v2/spans`).

* `ExportProcessorType`: Whether the exporter should use [Batch or Simple
  exporting
  processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors).

* `HttpClientFactory`: A factory function called to create the `HttpClient`
  instance that will be used at runtime to transmit spans over HTTP. See
  [Configure HttpClient](#configure-httpclient) for more details.

* `MaxPayloadSizeInBytes`: Maximum payload size of UTF8 JSON chunks sent to
  Zipkin (default 4096).

* `ServiceName`: Name of the service reporting telemetry. If the `Resource`
   associated with the telemetry has "service.name" defined, then it'll be
   preferred over this option.

* `UseShortTraceIds`: Whether the trace's ID should be shortened before sending
   to Zipkin (default false).

See
[`TestZipkinExporter.cs`](../../examples/Console/TestZipkinExporter.cs)
for example use.

### Configuration using Dependency Injection

This exporter allows easy configuration of `ZipkinExporterOptions` from
dependency injection container, when used in conjunction with
[`OpenTelemetry.Extensions.Hosting`](../OpenTelemetry.Extensions.Hosting/README.md).

See the [Startup](../../examples/AspNetCore/Program.cs) class of the ASP.NET
Core application for example use.

### Configuration using Environment Variables

The following environment variables can be used to override the default
values of the `ZipkinExporterOptions`.

| Environment variable            | `ZipkinExporterOptions` property |
| --------------------------------| -------------------------------- |
| `OTEL_EXPORTER_ZIPKIN_ENDPOINT` | `Endpoint`                       |

## Configure HttpClient

The `HttpClientFactory` option is provided on `ZipkinExporterOptions` for users
who want to configure the `HttpClient` used by the `ZipkinExporter`. Simply
replace the function with your own implementation if you want to customize the
generated `HttpClient`:

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddZipkinExporter(o => o.HttpClientFactory = () =>
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value");
            return client;
        }));
```

For users using
[IHttpClientFactory](https://docs.microsoft.com/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
you may also customize the named "ZipkinExporter" `HttpClient` using the
built-in `AddHttpClient` extension:

```csharp
services.AddHttpClient(
    "ZipkinExporter",
     configureClient: (client) =>
        client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value"));
```

Note: The single instance returned by `HttpClientFactory` is reused by all
export requests.

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Exporter-Zipkin" for its internal logging. Please
refer to [SDK troubleshooting](../OpenTelemetry/README.md#troubleshooting) for
instructions on seeing these internal logs.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Zipkin](https://zipkin.io)
