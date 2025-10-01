# Release Notes

This file contains highlights and announcements covering all components.
For more details see `CHANGELOG.md` files maintained in the root source
directory of each individual package.

## 1.13.0

Release details: [1.13.0](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.13.0)

* gRPC calls to export traces, logs, and metrics using `OtlpExportProtocol.Grpc`
  now set the `TE=trailers` HTTP request header to improve interoperability.
* `EventName` is now exported by default as `EventName` instead of
  `logrecord.event.name` when specified through `ILogger` or the experimental
  log bridge API.

## 1.12.0

Release details: [1.12.0](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.12.0)

* **Breaking Change**: `OpenTelemetry.Exporter.OpenTelemetryProtocol` now
  defaults to using OTLP/HTTP instead of OTLP/gRPC when targeting .NET Framework
  and .NET Standard. This change may cause telemetry export to fail unless
  appropriate adjustments are made. Explicitly setting OTLP/gRPC may result in a
  `NotSupportedException` unless further configuration is applied. See
  [#6209](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6209) for
  full details and mitigation guidance. [#6229](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6229)

## 1.11.1

Release details: [1.11.1](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.11.1)

* Fixed a bug preventing `OpenTelemetry.Exporter.OpenTelemetryProtocol` from
  exporting telemetry on .NET Framework.

## 1.11.0

Release details: [1.11.0](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.11.0)

* `OpenTelemetry.Exporter.OpenTelemetryProtocol` no longer depends on the
  `Google.Protobuf`, `Grpc`, or `Grpc.Net.Client` packages. Serialization and
  transmission of outgoing data is now performed manually to improve the overall
  performance.

## 1.10.0

Release details: [1.10.0](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.10.0)

* Bumped the package versions of `System.Diagnostic.DiagnosticSource` and other
  Microsoft.Extensions.* packages to `9.0.0`.

* Added support for new APIs introduced in `System.Diagnostics.DiagnosticSource`
  `9.0.0`:

  * [InstrumentAdvice&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.diagnostics.metrics.instrumentadvice-1)

    For details see: [Explicit bucket histogram
    aggregation](./docs/metrics/customizing-the-sdk/README.md#explicit-bucket-histogram-aggregation).

  * [Gauge&lt;T&gt;](https://learn.microsoft.com/dotnet/api/system.diagnostics.metrics.gauge-1)

  * [ActivitySource.Tags](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource.tags)
    (supported in OtlpExporter & ConsoleExporter)

* Experimental features promoted to stable:

  * `CardinalityLimit` can now be managed for individual metrics via the View
    API. For details see: [Changing cardinality limit for a
    Metric](./docs/metrics/customizing-the-sdk/README.md#changing-the-cardinality-limit-for-a-metric).

  * The [overflow
    attribute](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#overflow-attribute)
    (`otel.metric.overflow`) behavior is now enabled by default. The
    `OTEL_DOTNET_EXPERIMENTAL_METRICS_EMIT_OVERFLOW_ATTRIBUTE` environment
    variable is no longer required. For details see: [Cardinality
    Limits](./docs/metrics/README.md#cardinality-limits).

  * The MetricPoint reclaim behavior is now enabled by default when Delta
    aggregation temporality is used. The
    `OTEL_DOTNET_EXPERIMENTAL_METRICS_RECLAIM_UNUSED_METRIC_POINTS` environment
    variable is no longer required. For details see: [Cardinality
    Limits](./docs/metrics/README.md#cardinality-limits).

* Added `OpenTelemetrySdk.Create` API for configuring OpenTelemetry .NET signals
  (logging, tracing, and metrics) via a single builder. This new API simplifies
  bootstrap and teardown, and supports cross-cutting extensions targeting
  `IOpenTelemetryBuilder`.

* Removed out of support `net6.0` target and added `net9.0` target.

## 1.9.0

Release details: [1.9.0](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.9.0)

* `Exemplars` are now part of the stable API! For details see: [customizing
  exemplars
  collection](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics/customizing-the-sdk#exemplars).

* `WithLogging` is now part of the stable API! Logging, Metrics, and Tracing can
  now all be configured using the `With` style and the builders finally have
  parity in their APIs.

## 1.8.0

Release details: [1.8.0](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.8.0)

* `TracerProvider` sampler can now be configured via the `OTEL_TRACES_SAMPLER` &
  `OTEL_TRACES_SAMPLER_ARG` envvars.

* A new `UseOtlpExporter` cross-cutting extension has been added to register the
  `OtlpExporter` and enable all signals in a single call.

* `exception.type`, `exception.message`, `exception.stacktrace` will now
  automatically be included by the `OtlpLogExporter` when logging exceptions.
  Previously an experimental environment variable had to be set.

## 1.7.0

Release details: [1.7.0](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.7.0)

* Bumped the package versions of System.Diagnostic.DiagnosticSource and other
  Microsoft.Extensions.* packages to `8.0.0`.

* Added `net8.0` targets to all the components.

* OTLP Exporter
  * Updated to use `ILogger` `CategoryName` as the instrumentation scope for
    logs.
  * Added named options support for OTLP Log Exporter.
  * Added support for instrumentation scope attributes in metrics.
  * Added support under an experimental flag to emit log exception attributes.
  * Added support under an experimental flag to emit log eventId and eventName.
    attributes.

* Added support for the
  [IMetricsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.diagnostics.metrics.imetricsbuilder)
  API.

* Added an experimental opt-in metrics feature to reclaim unused MetricPoints
  which enables a higher number of unique dimension combinations to be emitted.
  See [reclaim unused metric
  points](https://github.com/open-telemetry/opentelemetry-dotnet/blob/32c64d04defb5c92d056fd8817638151168b10da/docs/metrics/README.md#cardinality-limits)
  for more details.
