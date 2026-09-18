# OpenTelemetry .NET SDK

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)

<details>
<summary>Table of Contents</summary>

* [Installation](#installation)
* [Introduction](#introduction)
* [Self-Observability (Experimental)](#self-observability-experimental)
* [Troubleshooting](#troubleshooting)
  * [Self-diagnostics](#self-diagnostics)
* [References](#references)

</details>

## Installation

```shell
dotnet add package OpenTelemetry
```

## Introduction

OpenTelemetry SDK is a reference implementation of the OpenTelemetry API. It
implements the Logging API, Metrics API, Tracing API, Resource API, and the
Context API. Once a valid SDK is installed and configured all the OpenTelemetry
API methods, which were no-ops without an SDK, will start emitting telemetry.
This SDK also ships with
[ILogger](https://learn.microsoft.com/dotnet/core/extensions/logging)
integration to automatically capture and enrich logs emitted using
`Microsoft.Extensions.Logging`.

The SDK deals with concerns such as sampling, processing pipelines (exporting
telemetry to a particular backend, etc.), metrics aggregation, and other
concerns outlined in the [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification).
In most cases, users indirectly install and enable the SDK when they install an
exporter.

To learn how to set up and configure the OpenTelemetry SDK see: [Getting
started](../../README.md#getting-started). For additional details about
initialization patterns see: [Initialize the
SDK](../../docs/README.md#initialize-the-sdk).

## Self-Observability (Experimental)

The SDK can emit metrics about its own internal operations, enabling operators to
monitor the health of the telemetry pipeline itself (e.g., detecting dropped
telemetry due to queue overflow).

> [!NOTE]
> Self-observability metrics are **experimental** and may change in future
> releases. They are emitted under the meter name `otel.sdk.experimental`.

### Opt-in

Self-observability metrics are only emitted when explicitly enabled by
subscribing to the `otel.sdk.experimental` meter. There is no performance cost
unless enabled.

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("otel.sdk.experimental")
    .AddOtlpExporter() // or any exporter
    .Build();
```

### Available Metrics

These metrics follow the [OpenTelemetry SDK Self-Observability Semantic
Conventions](https://opentelemetry.io/docs/specs/semconv/otel/sdk-metrics/).

| Metric Name | Instrument | Unit | Description |
| --- | --- | --- | --- |
| `otel.sdk.processor.log.processed` | Counter | `{log_record}` | Number of log records processed by the SDK, tagged with outcome. |
| `otel.sdk.processor.span.processed` | Counter | `{span}` | Number of spans processed by the SDK, tagged with outcome. |

### Attributes

| Attribute | Description | Example |
| --- | --- | --- |
| `otel.component.type` | The processor type. | `batching_log_processor`, `simple_log_processor`, `batching_span_processor`, `simple_span_processor` |
| `otel.component.name` | Unique instance identifier. | `batching_log_processor/0`, `batching_span_processor/0` |
| `error.type` | Present only on failure. | `queue_full`, `already_shutdown` |

When `error.type` is absent, the item was successfully accepted by the
processor. This means the processor completed its intended handling of the
item; for the Simple and Batching processors it is recorded when the item is
handed to the exporter, and it does **not** indicate that the export itself
succeeded or that the item reached the backend. Export failures are not
reflected in this metric. When present:

* `queue_full` - The batch processor's internal queue was full; the item was
  dropped.
* `already_shutdown` - The processor had already been shut down; the item was
  lost.

> [!NOTE]
> Sampling affects `otel.sdk.processor.span.processed` as follows. Spans dropped
> by the sampler (`DROP`) are not counted, because span processors are not
> invoked for them at all. Spans sampled as `RECORD_ONLY` are counted as
> successfully processed because they do reach the processor and by design are
> never handed to an exporter.

## Troubleshooting

All the components shipped from this repo uses
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
for its internal logging. The name of the `EventSource` used by OpenTelemetry
SDK is "OpenTelemetry-Sdk". To know the `EventSource` names used by other
components, refer to the individual readme files.

While it is possible to view these logs using tools such as
[PerfView](https://github.com/microsoft/perfview),
[dotnet-trace](https://docs.microsoft.com/dotnet/core/diagnostics/dotnet-trace)
etc., this SDK also ships a [self-diagnostics](#self-diagnostics) feature, which
helps with troubleshooting.

### Self-diagnostics

Self-diagnostics captures the internal logs of all OpenTelemetry components
(every `EventSource` whose name starts with "OpenTelemetry-") and writes them to
a rolling log file, to the console, or to both. It is disabled by default.

The quickest way to enable it is with environment variables:

```shell
# Write to rolling log files in ./otel-logs
OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY=./otel-logs

# Or write to stdout and stderr instead, at a lower level
OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS=stdout,stderr
OTEL_LOG_LEVEL=debug
```

`MinimumLevel` defaults to `Warning`, which is intended to be safe to leave
enabled continuously; raise it only while investigating. This diverges from the
OpenTelemetry specification default for `OTEL_LOG_LEVEL` (`info`) - set
`OTEL_LOG_LEVEL=info` to take the specification default. With no sink enabled
the SDK is silent at any level.

It can also be configured in code through the options pipeline:

```csharp
services.Configure<SelfDiagnosticsOptions>(options =>
{
    options.MinimumLevel = LogLevel.Debug;
    options.LogDirectory = "/var/log/otel";
});
```

For the full reference - every option and environment variable, the output
format, what to attach to a bug report, the environment variable redaction
rules, and runtime reconfiguration - see:
[Troubleshooting](../../docs/troubleshooting/README.md).

> [!NOTE]
> The legacy `OTEL_DIAGNOSTICS.json` file mechanism still works but is
> superseded by the above and will be removed in a future major version. See
> [Legacy self-diagnostics
> mechanism](../../docs/troubleshooting/README.md#legacy-self-diagnostics-mechanism).

## References

* [OpenTelemetry Logging SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/sdk.md)
* [OpenTelemetry Metrics SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md)
* [OpenTelemetry Tracing SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md)
* [OpenTelemetry Resource SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
