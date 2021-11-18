# Customizing OpenTelemetry .NET SDK for Metrics

## MeterProvider

As shown in the [getting-started](../getting-started/README.md) doc, a valid
[`MeterProvider`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#meterprovider)
must be configured and built to collect metrics with OpenTelemetry .NET Sdk.
`MeterProvider` holds all the configuration for metrics like MetricReaders,
Views, etc. Naturally, almost all the customizations must be done on the
`MeterProvider`.

## Building a MeterProvider

Building a `MeterProvider` is done using `MeterProviderBuilder` which must be
obtained by calling `Sdk.CreateMeterProviderBuilder()`. `MeterProviderBuilder`
exposes various methods which configure the provider it is going to build. These
include methods like `AddMeter`, `AddView` etc, and are explained in subsequent
sections of this document. Once configuration is done, calling `Build()` on the
`MeterProviderBuilder` builds the `MeterProvider` instance. Once built, changes
to its configuration is not allowed. In most cases, a single `MeterProvider` is
created at the application startup, and is disposed when application shuts down.

The snippet below shows how to build a basic `MeterProvider`. This will create a
provider with default configuration, and is not particularly useful. The
subsequent sections show how to build a more useful provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

using var meterProvider = Sdk.CreateMeterProviderBuilder().Build();
```

## MeterProvider configuration

`MeterProvider` holds the metrics configuration, which includes the following:

1. The list of `Meter`s from which instruments are created to report
   measurements.
2. The list of instrumentations enabled via [Instrumentation
   Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library).
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
default, it listens to no meters. Every meter which is used to create
instruments must be explicitly added to the meter provider.

`AddMeter` method on `MeterProviderBuilder` can be used to add a `Meter` to the
provider. The name of the `Meter` (case-insensitive) must be provided as an
argument to this method. `AddMeter` can be called multiple times to add more
than one meters. It also supports wild-card subscription model.

It is **not** possible to add meters *once* the provider is built by the
`Build()` method on the `MeterProviderBuilder`.

The snippet below shows how to add meters to the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    // The following enables instruments from Meter
    // named "MyCompany.MyProduct.MyLibrary" only.
    .AddMeter("MyCompany.MyProduct.MyLibrary")
    // The following enables instruments from all Meters
    // whose name starts with  "AbcCompany.XyzProduct.".
    .AddMeter("AbcCompany.XyzProduct.*")
    .Build();
```

See [Program.cs](./Program.cs) for complete example.

**Note:** A common mistake while configuring `MeterProvider` is forgetting to
add the required `Meter`s to the provider. It is recommended to leverage the
wildcard subscription model where it makes sense. For example, if your
application is expecting to enable instruments from a number of libraries from a
company "Abc", the you can use `AddMeter("Abc.*")` to enable all meters whose
name starts with "Abc.".

### View

A
[View](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view)
provides the ability to customize the metrics that are output by the SDK.
Following sections explains how to use this feature. Each section has two code
snippets. The first one uses an overload of `AddView` method that takes in the
name of the instrument as the first parameter. The `View` configuration is then
applied to the matching instrument name. The second code snippet shows how to
use an advanced selection criteria to achieve the same results. This requires
the user to provide a `Func<Instrument, MetricStreamConfiguration>` which offers
more flexibility in filtering the instruments to which the `View` should be
applied.

#### Rename an instrument

When SDK produces Metrics, the name of Metric is by default the name of the
instrument. View may be used to rename a metric to a different name. This is
particularly useful if there are conflicting instrument names, and you do not
own the instrument to create it with a different name.

```csharp
   // Rename an instrument to new name.
   .AddView(instrumentName: "MyCounter", name: "MyCounterRenamed")
```

```csharp
   // Advanced selection criteria and config via Func<Instrument, MetricStreamConfiguration>
   .AddView((instrument) =>
      {
         if (instrument.Meter.Name == "CompanyA.ProductB.LibraryC" &&
            instrument.Name == "MyCounter")
         {
            return new MetricStreamConfiguration() { Name = "MyCounterRenamed" };
         }

         return null;
      })
```

#### Drop an instrument

When using `AddMeter` to add a Meter to the provider, all the instruments from
that `Meter` gets subscribed. Views can be used to selectively drop an
instrument from a Meter. If the goal is to drop every instrument from a `Meter`,
then it is recommended to simply not add that `Meter` using `AddMeter`.

```csharp
   // Drop the instrument "MyCounterDrop".
   .AddView(instrumentName: "MyCounterDrop", MetricStreamConfiguration.Drop)
```

```csharp
   // Advanced selection criteria and config via Func<Instrument, MetricStreamConfiguration>
   .AddView((instrument) =>
      {
         if (instrument.Meter.Name == "CompanyA.ProductB.LibraryC" &&
            instrument.Name == "MyCounterDrop")
         {
            return MetricStreamConfiguration.Drop;
         }

         return null;
      })
```

#### Select specific tags

When recording a measurement from an instrument, all the tags that were provided
are reported as dimensions for the given metric. Views can be used to
selectively choose a subset of dimensions to report for a given metric. This is
useful when you have a metric for which only a few of the dimensions associated
with the metric are of interest to you.

```csharp
    // Only choose "name" as the dimension for the metric "MyFruitCounter"
   .AddView(
      instrumentName: "MyFruitCounter",
      metricStreamConfiguration: new MetricStreamConfiguration
      {
         TagKeys = new string[] { "name" },
      })

   ...
   // Only the dimension "name" is selected, "color" is dropped
   MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
   MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
   MyFruitCounter.Add(2, new("name", "apple"), new("color", "green"));
   ...

   // If you provide an empty `string` array as `TagKeys` to the `MetricStreamConfiguration`
   // the SDK will drop all the dimensions associated with the metric
   .AddView(
      instrumentName: "MyFruitCounter",
      metricStreamConfiguration: new MetricStreamConfiguration
      {
         TagKeys = new string[] { },
      })

   ...
   // both "name" and "color" are dropped
   MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
   MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
   MyFruitCounter.Add(2, new("name", "apple"), new("color", "green"));
   ...
```

```csharp
   // Advanced selection criteria and config via Func<Instrument, MetricStreamConfiguration>
   .AddView((instrument) =>
      {
         if (instrument.Meter.Name == "CompanyA.ProductB.LibraryC" &&
            instrument.Name == "MyFruitCounter")
         {
            return new MetricStreamConfiguration
            {
               TagKeys = new string[] { "name" },
            };
         }

         return null;
      })
```

#### Specify custom boundaries for Histogram

By default, the boundaries used for a Histogram are [`{ 0, 5, 10, 25, 50, 75, 100,
250, 500,
1000}`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#explicit-bucket-histogram-aggregation).
Views can be used to provide custom boundaries for a Histogram. The measurements
are then aggregated using the custom boundaries provided instead of the the
default boundaries. This requires the use of `ExplicitBucketHistogramConfiguration`.

```csharp
   // Change Histogram boundaries to count measurements under the following buckets:
   // (-inf, 10]
   // (10, 20]
   // (20, +inf)
   .AddView(
      instrumentName: "MyHistogram",
      new ExplicitBucketHistogramConfiguration
        { Boundaries = new double[] { 10, 20 } })

   // If you provide an empty `double` array as `Boundaries` to the `ExplicitBucketHistogramConfiguration`,
   // the SDK will only export the sum and count for the measurements.
   // There are no buckets exported in this case.
   .AddView(
      instrumentName: "MyHistogram",
      new ExplicitBucketHistogramConfiguration { Boundaries = new double[] { } })
```

```csharp
   // Advanced selection criteria and config via Func<Instrument, MetricStreamConfiguration>
   .AddView((instrument) =>
      {
         if (instrument.Meter.Name == "CompanyA.ProductB.LibraryC" &&
            instrument.Name == "MyHistogram")
         {
            // `ExplicitBucketHistogramConfiguration` is a child class of `MetricStreamConfiguration`
            return new ExplicitBucketHistogramConfiguration
            {
               Boundaries = new double[] { 10, 20 },
            };
         }

         return null;
      })
```

**NOTE:** The SDK currently does not support any changes to `Aggregation` type
for Views.

See [Program.cs](./Program.cs) for a complete example.

### Instrumentation

// TODO

### MetricReader

// TODO

### Resource

// TODO
