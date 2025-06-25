# OTLP Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)

[The OTLP (OpenTelemetry Protocol) exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md)
implementation.

<details>
<summary>Table of Contents</summary>

* [Installation](#installation)
* [Enable Log Exporter](#enable-log-exporter)
* [Enable Metric Exporter](#enable-metric-exporter)
* [Enable Trace Exporter](#enable-trace-exporter)
* [Enable OTLP Exporter for all signals](#enable-otlp-exporter-for-all-signals)
* [Configuration](#configuration)
  * [OtlpExporterOptions](#otlpexporteroptions)
  * [LogRecordExportProcessorOptions](#logrecordexportprocessoroptions)
  * [MetricReaderOptions](#metricreaderoptions)
  * [Environment Variables](#environment-variables)
    * [Exporter configuration](#exporter-configuration)
    * [Attribute limits](#attribute-limits)
  * [Configure HttpClient](#configure-httpclient)
* [Experimental features](#experimental-features)
* [Troubleshooting](#troubleshooting)

</details>

## Prerequisite

* An endpoint capable of accepting OTLP, like [OpenTelemetry
  Collector](https://opentelemetry.io/docs/collector/) or similar.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

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

## Enable OTLP Exporter for all signals

Starting with the `1.8.0-beta.1` version you can use the cross-cutting
`UseOtlpExporter` extension to simplify registration of the OTLP exporter for
all signals (logs, metrics, and traces).

> [!NOTE]
> The cross cutting extension is currently only available when using the
  `AddOpenTelemetry` extension in the
  [OpenTelemetry.Extensions.Hosting](../OpenTelemetry.Extensions.Hosting/README.md)
  package.

```csharp
appBuilder.Services.AddOpenTelemetry()
    .UseOtlpExporter();
```

The `UseOtlpExporter` has the following behaviors:

* Calling `UseOtlpExporter` automatically enables logging, metrics, and tracing
  however only telemetry which has been enabled will be exported.

  There are different mechanisms available to enable telemetry:

  * Logging

    `ILogger` telemetry is controlled by category filters typically set through
    configuration. For details see: [Log
    Filtering](../../docs/logs/customizing-the-sdk/README.md#log-filtering) and
    [Logging in
    .NET](https://docs.microsoft.com/dotnet/core/extensions/logging).

  * Metrics

    Metrics telemetry is controlled by calling `MeterProviderBuilder.AddMeter`
    to listen to
    [Meter](https://learn.microsoft.com/dotnet/api/system.diagnostics.meter)s
    emitting metrics. Typically instrumentation packages will make this call
    automatically.

    Examples:

    ```csharp
    appBuilder.Services.AddOpenTelemetry()
        .UseOtlpExporter()
        .WithMetrics(metrics => metrics
            .AddMeter(MyMeter.Name) // Listen to custom telemetry
            .AddAspNetCoreInstrumentation() // Use instrumentation to listen to telemetry
        );
    ```

    ```csharp
    appBuilder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics
        .AddMeter(MyMeter.Name) // Listen to custom telemetry
        .AddAspNetCoreInstrumentation() // Use instrumentation to listen to telemetry
    );

    appBuilder.Services.AddOpenTelemetry()
        .UseOtlpExporter();
    ```

    For details see: [Meter](../../docs/metrics/customizing-the-sdk/README.md#meter).

    When using `Microsoft.Extensions.Hosting` v8.0.0 or greater (a standard part
    of ASP.NET Core) `Meter`s and `Instrument`s can also be enabled using
    configuration.

    `appSettings.json` metrics configuration example:

    ```json
    {
      "Metrics": {
        "EnabledMetrics": {
          "Microsoft.AspNetCore.*": true,
          "System.*": true,
          "MyCompany.*": true,
        }
      }
    }
    ```

    For details about the built-in metrics exposed by .NET see: [Built-in
    metrics in
    .NET](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics).

  * Tracing

    Trace telemetry is controlled by calling `TracerProviderBuilder.AddSource`
    to listen to
    [ActivitySource](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource)s
    emitting traces. Typically instrumentation packages will make this call
    automatically.

    Examples:

    ```csharp
    appBuilder.Services.AddOpenTelemetry()
        .UseOtlpExporter()
        .WithTracing(tracing => tracing
            .AddSource(MyActivitySource.Name) // Listen to custom telemetry
            .AddAspNetCoreInstrumentation() // Use instrumentation to listen to telemetry
        );
    ```

    ```csharp
    appBuilder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
        .AddSource(MyActivitySource.Name) // Listen to custom telemetry
        .AddAspNetCoreInstrumentation() // Use instrumentation to listen to telemetry
    );

    appBuilder.Services.AddOpenTelemetry()
        .UseOtlpExporter();
    ```

    For details see: [Activity Source](../../docs/trace/customizing-the-sdk/README.md#activity-source).

* The exporter registered by `UseOtlpExporter` will be added as the last
  processor in the pipeline established for logging and tracing.

* `UseOtlpExporter` can only be called once. Subsequent calls will result in a
  `NotSupportedException` being thrown.

* `UseOtlpExporter` cannot be called in addition to signal-specific
  `AddOtlpExporter` methods. If `UseOtlpExporter` is called signal-specific
  `AddOtlpExporter` calls will result in a `NotSupportedException` being thrown.

### Configuring signals when using UseOtlpExporter

`UseOtlpExporter` supports the full set of [environment
variables](#environment-variables) listed below including the signal-specific
overrides and users are encouraged to use this mechanism to configure their
exporters.

A `UseOtlpExporter` overload is provided which may be used to set the protocol
and base URL:

```csharp
appBuilder.Services.AddOpenTelemetry()
    .UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, new Uri("http://localhost:4318/"));
```

> [!NOTE]
> When the protocol is set to `OtlpExportProtocol.HttpProtobuf` a
  signal-specific path will be appended automatically to the base URL when
  constructing exporters.

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

> [!NOTE]
> When using `OtlpExportProtocol.HttpProtobuf`, the full URL MUST be
> provided, including the signal-specific path v1/{signal}. For example, for
> traces, the full URL will look like `http://your-custom-endpoint/v1/traces`.

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

The following environment variables can be used to configure the OTLP Exporter
for logs, traces, and metrics.

> [!NOTE]
> In OpenTelemetry .NET environment variable keys are retrieved using
  `IConfiguration` which means they may be set using other mechanisms such as
  defined in `appSettings.json` or specified on the command-line.

### Exporter configuration

The [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification/)
defines environment variables which can be used to configure the [OTLP
exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md)
and its associated processor
([logs](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#batch-logrecord-processor)
&
[traces](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#batch-span-processor))
or reader
([metrics](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#periodic-exporting-metricreader)).

* All signals

  The following environment variables can be used to override the default
  values of the `OtlpExporterOptions`:

  | Environment variable          | `OtlpExporterOptions` property        |
  | ------------------------------| --------------------------------------|
  | `OTEL_EXPORTER_OTLP_ENDPOINT` | `Endpoint`                            |
  | `OTEL_EXPORTER_OTLP_HEADERS`  | `Headers`                             |
  | `OTEL_EXPORTER_OTLP_TIMEOUT`  | `TimeoutMilliseconds`                 |
  | `OTEL_EXPORTER_OTLP_PROTOCOL` | `Protocol` (`grpc` or `http/protobuf`)|

  The following environment variables can be used to configure mTLS
  (mutual TLS) authentication (.NET 8.0+ only):

  | Environment variable                             | `OtlpMtlsOptions` property    | Description                           |
  | -------------------------------------------------| ----------------------------- | ------------------------------------- |
  | `OTEL_EXPORTER_OTLP_CERTIFICATE`                 | `CaCertificatePath`           | Path to CA certificate file (PEM)     |
  | `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE`          | `ClientCertificatePath`       | Path to client certificate file (PEM) |
  | `OTEL_EXPORTER_OTLP_CLIENT_KEY`                  | `ClientKeyPath`               | Path to client private key file (PEM) |

* Logs:

  The following environment variables can be used to override the default values
  for the batch processor configured for logging:

  | Environment variable              | `BatchExportLogRecordProcessorOptions` property                         |
  | ----------------------------------| ------------------------------------------------------------------------|
  | `OTEL_BLRP_SCHEDULE_DELAY`        | `ScheduledDelayMilliseconds`                                            |
  | `OTEL_BLRP_EXPORT_TIMEOUT`        | `ExporterTimeoutMilliseconds`                                           |
  | `OTEL_BLRP_MAX_QUEUE_SIZE`        | `MaxQueueSize`                                                          |
  | `OTEL_BLRP_MAX_EXPORT_BATCH_SIZE` | `MaxExportBatchSize`                                                    |

  The following environment variables can be used to override the default values
  of the `OtlpExporterOptions` used for logging when using the [UseOtlpExporter
  extension](#enable-otlp-exporter-for-all-signals):

  | Environment variable                  | `OtlpExporterOptions` property        | UseOtlpExporter | AddOtlpExporter |
  | --------------------------------------| --------------------------------------|-----------------|-----------------|
  | `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`    | `Endpoint`                            | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_LOGS_HEADERS`     | `Headers`                             | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_LOGS_TIMEOUT`     | `TimeoutMilliseconds`                 | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_LOGS_PROTOCOL`    | `Protocol` (`grpc` or `http/protobuf`)| Supported       | Not supported   |

* Metrics:

  The following environment variables can be used to override the default value
  of the `TemporalityPreference` setting for the reader configured for metrics
  when using OTLP exporter:

  | Environment variable                                | `MetricReaderOptions` property                  |
  | ----------------------------------------------------| ------------------------------------------------|
  | `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` | `TemporalityPreference`                         |

  The following environment variables can be used to override the default values
  of the periodic exporting metric reader configured for metrics:

  | Environment variable                                | `PeriodicExportingMetricReaderOptions` property |
  | ----------------------------------------------------| ------------------------------------------------|
  | `OTEL_METRIC_EXPORT_INTERVAL`                       | `ExportIntervalMilliseconds`                    |
  | `OTEL_METRIC_EXPORT_TIMEOUT`                        | `ExportTimeoutMilliseconds`                     |

  The following environment variables can be used to override the default values
  of the `OtlpExporterOptions` used for metrics when using the [UseOtlpExporter
  extension](#enable-otlp-exporter-for-all-signals):

  | Environment variable                  | `OtlpExporterOptions` property        | UseOtlpExporter | AddOtlpExporter |
  | --------------------------------------| --------------------------------------|-----------------|-----------------|
  | `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` | `Endpoint`                            | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_METRICS_HEADERS`  | `Headers`                             | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_METRICS_TIMEOUT`  | `TimeoutMilliseconds`                 | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_METRICS_PROTOCOL` | `Protocol` (`grpc` or `http/protobuf`)| Supported       | Not supported   |

* Tracing:

  The following environment variables can be used to override the default values
  for the batch processor configured for tracing:

  | Environment variable             | `BatchExportActivityProcessorOptions` property              |
  | ---------------------------------| ------------------------------------------------------------|
  | `OTEL_BSP_SCHEDULE_DELAY`        | `ScheduledDelayMilliseconds`                                |
  | `OTEL_BSP_EXPORT_TIMEOUT`        | `ExporterTimeoutMilliseconds`                               |
  | `OTEL_BSP_MAX_QUEUE_SIZE`        | `MaxQueueSize`                                              |
  | `OTEL_BSP_MAX_EXPORT_BATCH_SIZE` | `MaxExportBatchSize`                                        |

  The following environment variables can be used to override the default values
  of the `OtlpExporterOptions` used for tracing when using the [UseOtlpExporter
  extension](#enable-otlp-exporter-for-all-signals):

  | Environment variable                  | `OtlpExporterOptions` property        | UseOtlpExporter | AddOtlpExporter |
  | --------------------------------------| --------------------------------------|-----------------|-----------------|
  | `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`  | `Endpoint`                            | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_TRACES_HEADERS`   | `Headers`                             | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_TRACES_TIMEOUT`   | `TimeoutMilliseconds`                 | Supported       | Not supported   |
  | `OTEL_EXPORTER_OTLP_TRACES_PROTOCOL`  | `Protocol` (`grpc` or `http/protobuf`)| Supported       | Not supported   |

### Attribute limits

The [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification/)
defines environment variables which can be used to configure [attribute
limits](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#attribute-limits).

The following environment variables can be used to configure default attribute
limits:

* `OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT`
* `OTEL_ATTRIBUTE_COUNT_LIMIT`

The following environment variables can be used to configure span limits used
for tracing:

* `OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT`
* `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT`
* `OTEL_SPAN_EVENT_COUNT_LIMIT`
* `OTEL_SPAN_LINK_COUNT_LIMIT`
* `OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT`
* `OTEL_LINK_ATTRIBUTE_COUNT_LIMIT`

The following environment variables can be used to configure log record limits
used for logging:

* `OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT`
* `OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT`

## Configure HttpClient

The `HttpClientFactory` option is provided on `OtlpExporterOptions` for users
who want to configure the `HttpClient` used by the `OtlpTraceExporter`,
`OtlpMetricExporter`, and/or `OtlpLogExporter` when `HttpProtobuf` protocol is
used. Simply replace the function with your own implementation if you want to
customize the generated `HttpClient`:

> [!NOTE]
> The `HttpClient` instance returned by the `HttpClientFactory` function is used
  for all export requests.

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
  Authentication](https://en.wikipedia.org/wiki/Basic_access_authentication).
  For more complex authentication requirements,
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
> `IHttpClientFactory` is NOT currently supported by `OtlpLogExporter`.

## Experimental features

The following features are exposed experimentally in the OTLP Exporter. Features
are exposed experimentally when either the [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification)
has explicitly marked something
[experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md)
or when the SIG members are still working through the design for a feature and
want to solicit feedback from the community.

### Environment variables

> [!NOTE]
> In OpenTelemetry .NET environment variable keys are retrieved using
  `IConfiguration` which means they may be set using other mechanisms such as
  defined in appSettings.json or specified on the command-line.

* All signals

  * `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY`

    * When set to `in_memory`, it enables in-memory retry for transient errors
    encountered while sending telemetry.

      Added in `1.8.0`.

    * When set to `disk`, it enables retries by storing telemetry on disk during
    transient errors.  The default path where the telemetry is stored is
    obtained by calling
    [Path.GetTempPath()](https://learn.microsoft.com/dotnet/api/system.io.path.gettemppath)
    or can be customized by setting
    `OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH` environment
    variable.

      The OTLP exporter utilizes a forked version of the
      [OpenTelemetry.PersistentStorage.FileSystem](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.PersistentStorage.FileSystem)
      library to store telemetry data on disk. When a transient failure occurs,
      a file is created at the specified directory path on disk containing the
      serialized request data that was attempted to be sent to the OTLP
      ingestion. A background thread attempts to resend any offline stored
      telemetry every 60 seconds. For more details on how these files are
      managed on disk, refer to the [File
      details](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.PersistentStorage.FileSystem#file-details).

      Added in **TBD** (Unreleased).

* Logs

  * `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES`

    When set to `true`, it enables export of `LogRecord.EventId.Id` as
    `logrecord.event.id` and `LogRecord.EventId.Name` as `logrecord.event.name`.

    Added in `1.7.0-alpha.1`.

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
