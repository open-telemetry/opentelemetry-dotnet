# OTLP Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)

[The OTLP (OpenTelemetry Protocol) exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md)
implementation.

## Prerequisite

* An endpoint capable of accepting OTLP, like [OpenTelemetry
  Collector](https://opentelemetry.io/docs/collector/) or similar.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

## Enable Trace Exporter

This exporter provides `AddOtlpExporter()` extension method on `TracerProviderBuilder`
to enable exporting of traces. The following snippet adds the Exporter with default
[configuration](#configuration).

```csharp
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    // rest of config not shown here.
    .AddOtlpExporter()
    .Build();
```

See the [`TestOtlpExporter.cs`](../../examples/Console/TestOtlpExporter.cs) for
runnable example.

## Enable Metric Exporter

This exporter provides `AddOtlpExporter()` extension method on `MeterProviderBuilder`
to enable exporting of metrics. The following snippet adds the Exporter with default
[configuration](#configuration).

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    // rest of config not shown here.
    .AddOtlpExporter()
    .Build();
```

By default, `AddOtlpExporter()` pairs the OTLP MetricExporter with a
[PeriodicExportingMetricReader](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#periodic-exporting-metricreader)
with metric export interval of 60 secs and
[Temporality](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/data-model.md#temporality)
set as `Cumulative`. See
[`TestMetrics.cs`](../../examples/Console/TestMetrics.cs) for example on how to
customize the `MetricReaderOptions` or see the [Environment
Variables](#environment-variables) section below on how to customize using
environment variables.

## Enable Log Exporter

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.AddOtlpExporter();
    });
});
```

By default, `AddOtlpExporter()` pairs the OTLP Log Exporter with a [batching
processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/sdk.md#batching-processor).
See [`TestLogs.cs`](../../examples/Console/TestLogs.cs) for example on how to
customize the `LogRecordExportProcessorOptions` or see the [Environment
Variables](#environment-variables) section below on how to customize using
environment variables.

> [!NOTE]
> For details on how to configure logging with OpenTelemetry check the
[Console](../../docs/logs/getting-started-console/README.md) or [ASP.NET
Core](../../docs/logs/getting-started-aspnetcore/README.md) tutorial.

**ILogger Scopes**: OTLP Log Exporter supports exporting `ILogger` scopes as
Attributes. Scopes must be enabled at the SDK level using
[IncludeScopes](../../docs/logs/customizing-the-sdk/Readme.md#includescopes)
setting on `OpenTelemetryLoggerOptions`.

> [!NOTE]
> Scope attributes with key set as empty string or `{OriginalFormat}`
are ignored by exporter. Duplicate keys are exported as is.

## Configuration

You can configure the `OtlpExporter` through `OtlpExporterOptions`
and environment variables.

> [!NOTE]
> The `OtlpExporterOptions` type setters take precedence over the environment variables.

This can be achieved by providing an `Action<OtlpExporterOptions>` delegate to
the `AddOtlpExporter()` method or using the `Configure<OtlpExporterOptions>()`
Options API extension:

```csharp
// Set via delegate using code:
appBuilder.Services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddOtlpExporter(o => {
        // ...
    }));

// Set via Options API using code:
appBuilder.Services.Configure<OtlpExporterOptions>(o => {
    // ...
});

// Set via Options API using configuration:
appBuilder.Services.Configure<OtlpExporterOptions>(
    appBuilder.Configuration.GetSection("OpenTelemetry:otlp"));
```

If additional services from the dependency injection are required for
configuration they can be accessed through the Options API like this:

```csharp
// Step 1: Register user-created configuration service.
appBuilder.Services.AddSingleton<MyOtlpConfigurationService>();

// Step 2: Use Options API to configure OtlpExporterOptions with user-created service.
appBuilder.Services.AddOptions<OtlpExporterOptions>()
    .Configure<MyOtlpConfigurationService>((o, configService) => {
        o.Endpoint = configService.ResolveOtlpExporterEndpoint();
    });
```

> [!NOTE]
> The `OtlpExporterOptions` class is shared by logging, metrics, and tracing. To
> bind configuration specific to each signal use the `name` parameter on the
> `AddOtlpExporter` extensions:
>
> ```csharp
> // Step 1: Bind options to config using the name parameter.
> appBuilder.Services.Configure<OtlpExporterOptions>("tracing", appBuilder.Configuration.GetSection("OpenTelemetry:tracing:otlp"));
> appBuilder.Services.Configure<OtlpExporterOptions>("metrics", appBuilder.Configuration.GetSection("OpenTelemetry:metrics:otlp"));
> appBuilder.Services.Configure<OtlpExporterOptions>("logging", appBuilder.Configuration.GetSection("OpenTelemetry:logging:otlp"));
>
> // Step 2: Register OtlpExporter using the name parameter.
> appBuilder.Services.AddOpenTelemetry()
>     .WithTracing(builder => builder.AddOtlpExporter("tracing", configure: null))
>     .WithMetrics(builder => builder.AddOtlpExporter("metrics", configure: null));
>
> appBuilder.Logging.AddOpenTelemetry(builder => builder.AddOtlpExporter(
>     "logging",
>     options =>
>     {
>         // Note: Options can also be set via code but order is important. In the example here the code will apply after configuration.
>         options.Endpoint = new Uri("http://localhost/logs");
>     }));
> ```

### OtlpExporterOptions

* `Protocol`: OTLP transport protocol. Supported values:
  `OtlpExportProtocol.Grpc` and `OtlpExportProtocol.HttpProtobuf`.
   The default is `OtlpExportProtocol.Grpc`.

* `Endpoint`: Target to which the exporter is going to send traces or metrics.
  The endpoint must be a valid Uri with scheme (http or https) and host, and MAY
  contain a port and path. The default is "localhost:4317" for
  `OtlpExportProtocol.Grpc` and "localhost:4318" for
  `OtlpExportProtocol.HttpProtobuf`.

* `Headers`: Optional headers for the connection.

* `HttpClientFactory`: A factory function called to create the `HttpClient`
  instance that will be used at runtime to transmit telemetry over HTTP when the
  `HttpProtobuf` protocol is configured. See [Configure
  HttpClient](#configure-httpclient) for more details.

* `TimeoutMilliseconds` : Max waiting time for the backend to process a batch.

The following options are only applicable to `OtlpTraceExporter`:

* `ExportProcessorType`: Whether the exporter should use [Batch or Simple
  exporting
  processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors).
  The default is Batch.

* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

See the [`TestOtlpExporter.cs`](../../examples/Console/TestOtlpExporter.cs) for
an example of how to use the exporter.

### LogRecordExportProcessorOptions

The `LogRecordExportProcessorOptions` class may be used to configure processor &
batch settings for logging:

```csharp
// Set via delegate using code:
appBuilder.Logging.AddOpenTelemetry(options =>
{
    options.AddOtlpExporter((exporterOptions, processorOptions) =>
    {
        processorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 2000;
        processorOptions.BatchExportProcessorOptions.MaxExportBatchSize = 5000;
    });
});

// Set via Options API using code:
appBuilder.Services.Configure<LogRecordExportProcessorOptions>(o =>
{
    o.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 2000;
    o.BatchExportProcessorOptions.MaxExportBatchSize = 5000;
});

// Set via Options API using configuration:
appBuilder.Services.Configure<LogRecordExportProcessorOptions>(
    appBuilder.Configuration.GetSection("OpenTelemetry:Logging"));
```

### MetricReaderOptions

The `MetricReaderOptions` class may be used to configure reader settings for
metrics:

```csharp
// Set via delegate using code:
appBuilder.Services.AddOpenTelemetry()
    .WithMetrics(builder => builder.AddOtlpExporter((exporterOptions, readerOptions) =>
    {
        readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10_000;
    }));

// Set via Options API using code:
appBuilder.Services.Configure<MetricReaderOptions>(o =>
{
    o.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10_000;
});

// Set via Options API using configuration:
appBuilder.Services.Configure<MetricReaderOptions>(
    appBuilder.Configuration.GetSection("OpenTelemetry:Metrics"));
```

## Environment Variables

The following environment variables can be used to override the default
values of the `OtlpExporterOptions`
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md)).

| Environment variable          | `OtlpExporterOptions` property        |
| ------------------------------| --------------------------------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `Endpoint`                            |
| `OTEL_EXPORTER_OTLP_HEADERS`  | `Headers`                             |
| `OTEL_EXPORTER_OTLP_TIMEOUT`  | `TimeoutMilliseconds`                 |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `Protocol` (`grpc` or `http/protobuf`)|

The following environment variables can be used to override the default values
for `BatchExportProcessorOptions` in case of `OtlpTraceExporter` (following the
[OpenTelemetry
specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#batch-span-processor))

| Environment variable             | `OtlpExporterOptions.BatchExportProcessorOptions` property  |
| ---------------------------------| ------------------------------------------------------------|
| `OTEL_BSP_SCHEDULE_DELAY`        | `ScheduledDelayMilliseconds`                                |
| `OTEL_BSP_EXPORT_TIMEOUT`        | `ExporterTimeoutMilliseconds`                               |
| `OTEL_BSP_MAX_QUEUE_SIZE`        | `MaxQueueSize`                                              |
| `OTEL_BSP_MAX_EXPORT_BATCH_SIZE` | `MaxExportBatchSize`                                        |

The following environment variables can be used to override the default values
for `BatchExportProcessorOptions` in case of `OtlpLogExporter` (following the
[OpenTelemetry
specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#batch-logrecord-processor))

| Environment variable              | `LogRecordExportProcessorOptions.BatchExportProcessorOptions` property  |
| ----------------------------------| ------------------------------------------------------------------------|
| `OTEL_BLRP_SCHEDULE_DELAY`        | `ScheduledDelayMilliseconds`                                            |
| `OTEL_BLRP_EXPORT_TIMEOUT`        | `ExporterTimeoutMilliseconds`                                           |
| `OTEL_BLRP_MAX_QUEUE_SIZE`        | `MaxQueueSize`                                                          |
| `OTEL_BLRP_MAX_EXPORT_BATCH_SIZE` | `MaxExportBatchSize`                                                    |

The following environment variables can be used to override the default values
of the `PeriodicExportingMetricReaderOptions` (following the [OpenTelemetry
specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.12.0/specification/sdk-environment-variables.md#periodic-exporting-metricreader).

| Environment variable                                | `PeriodicExportingMetricReaderOptions` property |
| ----------------------------------------------------| ------------------------------------------------|
| `OTEL_METRIC_EXPORT_INTERVAL`                       | `ExportIntervalMilliseconds`                    |
| `OTEL_METRIC_EXPORT_TIMEOUT`                        | `ExportTimeoutMilliseconds`                     |

| Environment variable                                | `MetricReaderOptions` property                  |
| ----------------------------------------------------| ------------------------------------------------|
| `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` | `PeriodicExportingMetricReaderOptions`          |

The following environment variables can be used to override the default
values of the attribute limits
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#attribute-limits)).

* `OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT`
* `OTEL_ATTRIBUTE_COUNT_LIMIT`

The following environment variables can be used to override the default
values of the span limits
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#span-limits)).

* `OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT`
* `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT`
* `OTEL_SPAN_EVENT_COUNT_LIMIT`
* `OTEL_SPAN_LINK_COUNT_LIMIT`
* `OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT`
* `OTEL_LINK_ATTRIBUTE_COUNT_LIMIT`

The following environment variables can be used to override the default
values of the log record limits
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md#logrecord-limits)).

* `OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT`
* `OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT`

## Environment Variables for Experimental Features

### Otlp Log Exporter

* `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES`

When set to `true`, it enables export of `LogRecord.EventId.Id` as
`logrecord.event.id` and `LogRecord.EventId.Name` to `logrecord.event.name`.

## Configure HttpClient

The `HttpClientFactory` option is provided on `OtlpExporterOptions` for users
who want to configure the `HttpClient` used by the `OtlpTraceExporter` and/or
`OtlpMetricExporter` when `HttpProtobuf` protocol is used. Simply replace the
function with your own implementation if you want to customize the generated
`HttpClient`:

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddOtlpExporter(o =>
        {
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
            o.HttpClientFactory = () =>
            {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value");
                return client;
            };
        }));
```

> [!NOTE]
> `DefaultRequestHeaders` can be used for [HTTP Basic Access
Authentication](https://en.wikipedia.org/wiki/Basic_access_authentication), for
more complex authentication requirement,
[`System.Net.Http.DelegatingHandler`](https://learn.microsoft.com/dotnet/api/system.net.http.delegatinghandler)
can be used to handle token refresh, as explained
[here](https://stackoverflow.com/questions/56204350/how-to-refresh-a-token-using-ihttpclientfactory).

For users using
[IHttpClientFactory](https://docs.microsoft.com/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
you may also customize the named "OtlpTraceExporter" and/or "OtlpMetricExporter"
`HttpClient` using the built-in `AddHttpClient` extension:

```csharp
services.AddHttpClient(
    "OtlpTraceExporter",
    configureClient: (client) =>
        client.DefaultRequestHeaders.Add("X-MyCustomHeader", "value"));
```

> [!NOTE]
> The single instance returned by `HttpClientFactory` is reused by all export
requests.

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Exporter-OpenTelemetryProtocol" for its internal
logging. Please refer to [SDK
troubleshooting](../OpenTelemetry/README.md#troubleshooting) for instructions on
seeing these internal logs.

## References

* [OpenTelemetry
  Collector](https://github.com/open-telemetry/opentelemetry-collector)
* [OpenTelemetry Project](https://opentelemetry.io/)
* [OTLP proto files](https://github.com/open-telemetry/opentelemetry-proto)
