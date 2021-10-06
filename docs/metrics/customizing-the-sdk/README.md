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
exposes various methods which configures the provider it is going to build.
These includes methods like `AddSource`, `AddView` etc, and are explained in
subsequent sections of this document. Once configuration is done, calling
`Build()` on the `MeterProviderBuilder` builds the `MeterProvider` instance.
Once built, changes to its configuration is not allowed. In most cases, a single
`MeterProvider` is created at the application startup, and is disposed when
application shuts down.

The snippet below shows how to build a basic `MeterProvider`. This will create a
provider with default configuration, and is not particularly useful. The
subsequent sections shows how to build a more useful provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

using var meterProvider = Sdk.CreateMeterProviderBuilder().Build();
```

## MeterProvider configuration

`MeterProvider` holds the metrics configuration, which includes the following:

1. The list of `Meter`s from which instruments are created to report
   measurements.
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

[`Meter`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
is used for creating
[`Instruments`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument),
which are then used to report
[Measurements](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#measurement).
The SDK follows an explicit opt-in model for listening to meters. i.e, by
default, it listens to no meters. Every meter which is used to create instrument
must be explicitly added to the meter provider.

`AddSource` method on `MeterProviderBuilder` can be used to add a `Meter` to the
provider. The name of the `Meter` (case-insensitive) must be the argument to
this method. Multiple `AddSource` can be called to add more than one meter. It
also supports wild-card subscription model as well.

It is not possible to add meters *after* the provider is built, by calling the
`Build()` method on the `MeterProviderBuilder`.

The snippet below shows how to add meters to the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    // The following enables instruments from Meter
    // named "MyCompany.MyProduct.MyLibrary" only.
    .AddSource("MyCompany.MyProduct.MyLibrary")
    // The following enables instruments from all Meters
    // whose name starts with  "AbcCompany.XyzProduct.".
    .AddSource("AbcCompany.XyzProduct.*")
    .Build();
```

See [Program.cs](./Program.cs) for complete example.

**Note**
A common mistake while configuring `MeterProvider` is forgetting to add all
`Meter`s to the provider. It is recommended to leverage the wild card
subscription model where it makes sense. For example, if your application is
expecting to enable instruments from a number of libraries from a company "Abc",
the you can use `AddSource("Abc.*")` to enable all sources whose name starts
with "Abc.".

### View

// TODO

### Instrumentation

// TODO

### MetricReader

// TODO

### Resource

// TODO
