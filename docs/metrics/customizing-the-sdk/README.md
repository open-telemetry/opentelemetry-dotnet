# Customizing OpenTelemetry .NET SDK for Metrics

## MeterProvider

As shown in the [getting-started](../getting-started/README.md) doc, a valid
[`MeterProvider`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#meterprovider)
must be configured and built to collect metrics with OpenTelemetry .NET Sdk.
`MeterProvider` holds all the configuration for tracing like metricreaders,
views, etc. Naturally, almost all the customizations must be done on the
`MeterProvider`.

## Building a MeterProvider

Building a `MeterProvider` is done using `MeterProviderBuilder` which must be
obtained by calling `Sdk.CreateMeterProviderBuilder()`. `MeterProviderBuilder`
exposes various methods which configures the provider it is going to build. These
includes methods like `AddSource`, `AddView` etc, and are explained in
subsequent sections of this document. Once configuration is done, calling
`Build()` on the `MeterProviderBuilder` builds the `MeterProvider` instance.
Once built, changes to its configuration is not allowed. In most cases,
a single `MeterProvider` is created at the application startup,
and is disposed when application shuts down.

The snippet below shows how to build a basic `MeterProvider`. This will create
a provider with default configuration, and is not particularly useful. The
subsequent sections shows how to build a more useful provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

using var meterProvider = Sdk.CreateMeterProviderBuilder().Build();
```

## MeterProvider configuration

`MeterProvider` holds the metrics configuration, which includes the following:

1. The list of `Meter`s from which measurements are collected.
2. The list of instrumentations enabled via
   [InstrumentationLibrary](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library).
3. The list of
   [MetricReaders](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#metricreader),
   including exporting readers which exports metrics to
   [Exporters](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#metricexporter)
4. The
   [Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
   associated with the metrics.
5. The list of
   [Views](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view)
   to be used.

### Meter

// TODO

### View

// TODO

### Instrumentation

// TODO

### MetricReader

// TODO

### Resource

// TODO
