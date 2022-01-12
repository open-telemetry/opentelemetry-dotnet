# Introduction

## Exporters

Custom exporters can be implemented to send telemetry data to places which are
not covered by the built-in exporters.

### Built-In Exporters

These exporters work with all types of telemetry: logs, traces, and metrics.

- [Console](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.Console/README.md)
- [InMemory](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.InMemory/README.md)

### Exporter Requirements

Items in the following list refers to either a log record, activity, or metric
depending on what kind of exporter it is.

- Exporters should derive from `OpenTelemetry.BaseExporter`
  (which belongs to the
  [OpenTelemetry](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/README.md)
  package) and implement the `Export` method.
- Exporters can optionally implement the `OnShutdown` method.
- Depending on user's choice and load on the application, `Export` may get
  called with one or more items. TODO items being log records, activites, or
  metrics.
- Exporters will only receive sampled-in data. TODO Data being logs, activities
  or metrics.
- Exporters should not throw exceptions from `Export` and `OnShutdown`.
- Exporters should not modify the `items` (log records, activites, metrics)
  they receive (the same `item` may be exported again by different exporter).
- Exporters are responsible for any retry logic needed by the scenario. The SDK
  does not implement any retry logic.
- Exporters should avoid generating telemetry and causing live-loop, this can
  be done via `OpenTelemetry.SuppressInstrumentationScope`.
- Exporters should use `Activity.TagObjects` collection instead of
  `Activity.Tags` to obtain the full set of attributes (tags).
- Exporters should use `ParentProvider.GetResource()` to get the `Resource`
  associated with the provider.

## Processors

Custom processors can be implemented to cover more scenarios

### Built-In Processors

- [SimpleExportProcessor](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/SimpleExportProcessor.cs)
- [BatchExportProcessor](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/BatchExportProcessor.cs)
- [CompositeProcessor](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/CompositeProcessor.cs)

### Processor Requirements

- Processors should inherit from `OpenTelemetry.BaseProcessor`
  (which belongs to the
  [OpenTelemetry](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/README.md)
  package), and implement the `OnEnd` method.
- Processors can optionally implement the `OnForceFlush` and `OnShutdown`
  methods. `OnForceFlush` should be thread safe.
- Processors should not throw exceptions from `OnEnd`, `OnForceFlush` and
  `OnShutdown`.
- `OnEnd` should be thread safe, and should not block or take long time, since
  they will be called on critical code path.

### Filtering Processors

Most [instrumentation libraries](#instrumentation-library) shipped from this
repo provides a built-in `Filter` option to achieve the same effect. In such
cases, it is recommended to use that option as it offers higher performance.

## Samplers

### Built-In Samplers

OpenTelemetry .NET SDK has provided the following built-in samplers

- [AlwaysOffSampler](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/Trace/AlwaysOffSampler.cs)
- [AlwaysOnSampler](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/Trace/AlwaysOnSampler.cs)
- [ParentBasedSampler](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/Trace/ParentBasedSampler.cs)
- [TraceIdRatioBasedSampler](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/Trace/TraceIdRatioBasedSampler.cs)

### Sampler Requirements

- Samplers should inherit from `OpenTelemetry.Trace.Sampler` (TODO: log & metrics)
  (which belongs to the
  [OpenTelemetry](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry/README.md)
  package), and implement the `ShouldSample` method.
- `ShouldSample` should be thread safe, and should not block or take long time,
  since it will be called on critical code path.

## References

- [Exporter Specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-exporter)
- [Processor Specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor)
- [Sampler Specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampler)
