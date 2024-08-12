# Release Notes

This file contains highlights and announcements covering all components.
For more details see `CHANGELOG.md` files maintained in the root source
directory of each individual package.

## 1.9.0

* `Exemplars` are now part of the stable API! For details see: [customizing
  exemplars
  collection](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics/customizing-the-sdk#exemplars).

* `WithLogging` is now part of the stable API! Logging, Metrics, and Tracing can
  now all be configured using the `With` style and the builders finally have
  parity in their APIs.

## 1.8.0

* `TracerProvider` sampler can now be configured via the `OTEL_TRACES_SAMPLER` &
  `OTEL_TRACES_SAMPLER_ARG` envvars.

* A new `UseOtlpExporter` cross-cutting extension has been added to register the
  `OtlpExporter` and enable all signals in a single call.

* `exception.type`, `exception.message`, `exception.stacktrace` will now
  automatically be included by the `OtlpLogExporter` when logging exceptions.
  Previously an experimental environment variable had to be set.

## 1.7.0

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
