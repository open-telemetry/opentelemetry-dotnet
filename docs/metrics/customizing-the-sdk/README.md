# Customizing OpenTelemetry .NET SDK for Metrics

## MeterProvider

As shown in the [getting-startedgetting started in 5 minutes - Console
Application](../getting-started-console/README.md) doc, a valid
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

In a typical application, a single `MeterProvider` is created at application
startup and disposed at application shutdown. It is important to ensure that the
provider is not disposed too early. Actual mechanism depends on the application
type. For example, in a typical ASP.NET application, `MeterProvider` is created
in `Application_Start`, and disposed in `Application_End` (both methods are a
part of the Global.asax.cs file) as shown
[here](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/examples/AspNet/Global.asax.cs).
In a typical ASP.NET Core application, `MeterProvider` lifetime is managed by
leveraging the built-in Dependency Injection container as shown
[here](../../../examples/AspNetCore/Program.cs).

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
than one meters. It also supports wildcard subscription model. It is important
to note that *all* the instruments from the meter will be enabled, when a
`Meter` is added. To selectively drop some instruments from a `Meter`, use the
[View](#view) feature, as shown [here](#drop-an-instrument).

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

> [!NOTE]
> A common mistake while configuring `MeterProvider` is forgetting to
add the required `Meter`s to the provider. It is recommended to leverage the
wildcard subscription model where it makes sense. For example, if your
application is expecting to enable instruments from a number of libraries from a
company "Abc", the you can use `AddMeter("Abc.*")` to enable all meters whose
name starts with "Abc.".

### View

A
[View](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view)
provides the ability to customize the metrics that are output by the SDK.
Following sections explains how to use `AddView` method that takes the
instrument name as the first parameter, the `View` configuration is then applied
to the matching instrument name.

#### Rename an instrument

When SDK produces Metrics, the name of Metric is by default the name of the
instrument. View may be used to rename a metric to a different name. This is
particularly useful if there are conflicting instrument names, and you do not
own the instrument to create it with a different name.

```csharp
    // Rename an instrument to new name.
    .AddView(instrumentName: "MyCounter", name: "MyCounterRenamed")
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
    // Because "color" is dropped the resulting metric values are - name:apple LongSum Value:3 and name:lemon LongSum Value:2
    ...

    // If you provide an empty `string` array as `TagKeys` to the `MetricStreamConfiguration`
    // the SDK will drop all the dimensions associated with the metric
    .AddView(
        instrumentName: "MyFruitCounter",
        metricStreamConfiguration: new MetricStreamConfiguration
        {
            TagKeys = Array.Empty<string>(),
        })

    ...
    // both "name" and "color" are dropped
    MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
    MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
    MyFruitCounter.Add(2, new("name", "apple"), new("color", "green"));
    // Because both "name" and "color" are dropped the resulting metric value is - LongSum Value:5
    ...
```

#### Configuring the aggregation of a Histogram

There are two types of
[Histogram aggregations](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#histogram-aggregations):
the
[Explicit bucket histogram aggregation](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#explicit-bucket-histogram-aggregation)
and the
[Base2 exponential bucket histogram aggregation](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#base2-exponential-bucket-histogram-aggregation).
Views can be used to select which aggregation is used and to configure the
parameters of the aggregation. By default, the explicit bucket aggregation is
used.

##### Explicit bucket histogram aggregation

By default, the [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.14.0/specification/metrics/sdk.md#explicit-bucket-histogram-aggregation)
defines explicit buckets (aka boundaries) for Histograms as: `[ 0, 5, 10, 25,
50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000 ]`.

###### Customizing explicit buckets when using histogram aggregation

There are two mechanisms available to configure explicit buckets when using
histogram aggregation:

* View API - Part of the OpenTelemetry .NET SDK.
* Advice API - Part of the `System.Diagnostics.DiagnosticSource` package
  starting with version `9.0.0`.

> [!IMPORTANT]
> When both the View API and Advice API are used, the View API takes precedence.
  If explicit buckets are not provided by either the View API or the Advice API
  then the SDK defaults apply.

* View API

  Views can be used to provide custom explicit buckets for a Histogram. This
  requires the use of `ExplicitBucketHistogramConfiguration`.

  ```csharp
   // Change Histogram boundaries to count measurements under the following buckets:
   // (-inf, 10]
   // (10, 20]
   // (20, +inf)
   .AddView(
       instrumentName: "MyHistogram",
       new ExplicitBucketHistogramConfiguration { Boundaries = new double[] { 10, 20 } })

   // If you provide an empty `double` array as `Boundaries` to the `ExplicitBucketHistogramConfiguration`,
   // the SDK will only export the sum, count, min and max for the measurements.
   // There are no buckets exported in this case.
   .AddView(
       instrumentName: "MyHistogram",
       new ExplicitBucketHistogramConfiguration { Boundaries = Array.Empty<double>() })
  ```

* Advice API

  Starting with the `1.10.0` SDK, explicit buckets for a Histogram may be provided
  by instrumentation authors when the instrument is created. This is generally
  recommended to be used by library authors when the SDK defaults don't match the
  required granularity for the histogram being emitted.

  See: [Using Advice to customize Histogram
  instruments](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation#using-advice-to-customize-histogram-instruments).

##### Base2 exponential bucket histogram aggregation

By default, a Histogram is configured to use the
`ExplicitBucketHistogramConfiguration`. Views are used to switch a Histogram to
use the `Base2ExponentialBucketHistogramConfiguration`.

The bucket boundaries for a Base2 Exponential Bucket Histogram Aggregation
are determined dynamically based on the configured `MaxSize` and `MaxScale`
parameters. The parameters are used to adjust the resolution of the Histogram
buckets. Larger values of `MaxScale` enables higher resolution, however the
scale may be adjusted down such that the full range of recorded values fit
within the maximum number of buckets defined by `MaxSize`. The default
`MaxSize` is 160 buckets and the default `MaxScale` is 20.

```csharp
    // Change the maximum number of buckets for "MyHistogram"
    .AddView(
        instrumentName: "MyHistogram",
        new Base2ExponentialBucketHistogramConfiguration { MaxSize = 40 })
```

#### Produce multiple metrics from single instrument

When an instrument matches multiple views, it can generate multiple metrics. For
instance, if an instrument is matched by two different view configurations, it
will result in two separate metrics being produced from that single instrument.
Below is an example demonstrating how to leverage this capability to create two
independent metrics from a single instrument. In this example, a histogram
instrument is used to report measurements, and views are configured to produce
two metrics : one aggregated using `ExplicitBucketHistogramConfiguration` and the
other using `Base2ExponentialBucketHistogramConfiguration`.

```csharp
    var histogramWithMultipleAggregations = meter.CreateHistogram<long>("HistogramWithMultipleAggregations");

    // Configure the Explicit Bucket Histogram aggregation with custom boundaries and new name.
    .AddView(instrumentName: "HistogramWithMultipleAggregations", new ExplicitBucketHistogramConfiguration() { Boundaries = new double[] { 10, 20 }, Name = "MyHistogramWithExplicitHistogram" })

    // Use Base2 Exponential Bucket Histogram aggregation and new name.
    .AddView(instrumentName: "HistogramWithMultipleAggregations", new Base2ExponentialBucketHistogramConfiguration() { Name = "MyHistogramWithBase2ExponentialBucketHistogram" })

    // Both views rename the metric to avoid name conflicts. However, in this case,
    // renaming one would be sufficient.

    // This measurement will be aggregated into two separate metrics.
    histogramWithMultipleAggregations.Record(10, new("tag1", "value1"), new("tag2", "value2"));
```

When using views that produce multiple metrics from single instrument, it's
crucial to rename the metric to prevent conflicts. In the event of conflict,
OpenTelemetry will emit an internal warning but will still export both metrics.
The impact of this behavior depends on the backend or receiver being used. You
can refer to [OpenTelemetry's
specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/data-model.md#opentelemetry-protocol-data-model-consumer-recommendations)
for more details.

Below example is showing the *BAD* practice. DO NOT FOLLOW it.

```csharp
    var histogram = meter.CreateHistogram<long>("MyHistogram");

    // Configure a view to aggregate based only on the "location" tag.
    .AddView(instrumentName: "MyHistogram", metricStreamConfiguration: new MetricStreamConfiguration
        {
            TagKeys = new string[] { "location" },
        })

    // Configure another view to aggregate based only on the "status" tag.
    .AddView(instrumentName: "MyHistogram", metricStreamConfiguration: new MetricStreamConfiguration
        {
            TagKeys = new string[] { "status" },
        })

    // The measurement below will be aggregated into two metric streams, but both will have the same name.
    // OpenTelemetry will issue a warning about this conflict and pass both streams to the exporter.
    // However, this may cause issues depending on the backend.
    histogram.Record(10, new("location", "seattle"), new("status", "OK"));
```

The modified version, avoiding name conflict is shown below:

```csharp
    var histogram = meter.CreateHistogram<long>("MyHistogram");

    // Configure a view to aggregate based only on the "location" tag,
    // and rename the metric.
    .AddView(instrumentName: "MyHistogram", metricStreamConfiguration: new MetricStreamConfiguration
        {
            Name = "MyHistogramWithLocation",
            TagKeys = new string[] { "location" },
        })

    // Configure a view to aggregate based only on the "status" tag,
    // and rename the metric.
    .AddView(instrumentName: "MyHistogram", metricStreamConfiguration: new MetricStreamConfiguration
        {
            Name = "MyHistogramWithStatus",
            TagKeys = new string[] { "status" },
        })

    // The measurement below will be aggregated into two separate metrics, "MyHistogramWithLocation"
    // and "MyHistogramWithStatus".
    histogram.Record(10, new("location", "seattle"), new("status", "OK"));
```

> [!NOTE]
> The SDK currently does not support any changes to `Aggregation` type
by using Views.

See [Program.cs](./Program.cs) for a complete example.

#### Change the ExemplarReservoir

> [!NOTE]
> `MetricStreamConfiguration.ExemplarReservoirFactory` is an experimental API only
  available in pre-release builds. For details see:
  [OTEL1004](../../diagnostics/experimental-apis/OTEL1004.md).

To set the [ExemplarReservoir](#exemplarreservoir) for an instrument, use the
`MetricStreamConfiguration.ExemplarReservoirFactory` property on the View API:

> [!IMPORTANT]
> Setting `MetricStreamConfiguration.ExemplarReservoirFactory` alone will NOT
  enable `Exemplar`s for an instrument. An [ExemplarFilter](#exemplarfilter)
  MUST also be used.

```csharp
    // Use MyCustomExemplarReservoir for "MyFruitCounter"
    .AddView(
        instrumentName: "MyFruitCounter",
        new MetricStreamConfiguration { ExemplarReservoirFactory = () => new MyCustomExemplarReservoir() })
```

### Changing maximum Metric Streams

Every instrument results in the creation of a single Metric stream. With Views,
it is possible to produce more than one Metric stream from a single instrument.
To protect the SDK from unbounded memory usage, SDK limits the maximum number of
metric streams. All the measurements from the instruments created after reaching
this limit will be dropped. The default is 1000, and `SetMaxMetricStreams` can
be used to override the default.

Consider the below example. Here we set the maximum number of `MetricStream`s
allowed to be `1`. This means that the SDK would export measurements from only
one `MetricStream`. The very first instrument that is published
(`MyFruitCounter` in this case) will create a `MetricStream` and the SDK will
thereby reach the maximum `MetricStream` limit of `1`. The measurements from any
subsequent instruments added will be dropped.

```csharp
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

Counter<long> MyFruitCounter = MyMeter.CreateCounter<long>("MyFruitCounter");
Counter<long> AnotherFruitCounter = MyMeter.CreateCounter<long>("AnotherFruitCounter");

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("MyCompany.MyProduct.MyLibrary")
    .AddConsoleExporter()
    .SetMaxMetricStreams(1) // The default value is 1000
    .Build();

// SDK only exports measurements from `MyFruitCounter`.
MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));

// The measurements from `AnotherFruitCounter` are dropped as the maximum
// `MetricStream`s allowed is `1`.
AnotherFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
```

### Changing the cardinality limit for a MeterProvider

To set the default [cardinality limit](../README.md#cardinality-limits) for all
metrics managed by a given `MeterProvider`, use the
`MeterProviderBuilder.SetMaxMetricPointsPerMetricStream` extension:

> [!CAUTION]
> `MeterProviderBuilder.SetMaxMetricPointsPerMetricStream` is marked `Obsolete`
  in stable builds since 1.10.0 and has been replaced by
  `MetricStreamConfiguration.CardinalityLimit`.

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("MyCompany.MyProduct.MyLibrary")
    .SetMaxMetricPointsPerMetricStream(4000) // Note: The default value is 2000
    .AddConsoleExporter()
    .Build();
```

### Changing the cardinality limit for a Metric

To set the [cardinality limit](../README.md#cardinality-limits) for an
individual metric, use the `MetricStreamConfiguration.CardinalityLimit` property
on the View API:

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("MyCompany.MyProduct.MyLibrary")
    // Set a custom CardinalityLimit (10) for "MyFruitCounter"
    .AddView(
        instrumentName: "MyFruitCounter",
        new MetricStreamConfiguration { CardinalityLimit = 10 })
    .AddConsoleExporter()
    .Build();
```

### Exemplars

Exemplars are example data points for aggregated data. They provide access to
the raw measurement value, time stamp when measurement was made, and trace
context, if any. It also provides "Filtered Tags", which are attributes (Tags)
that are [dropped by a view](#select-specific-tags). Exemplars are an opt-in
feature, and allow customization via ExemplarFilter and ExemplarReservoir.

Exemplar collection in OpenTelemetry .NET is done automatically (once Exemplar
feature itself is enabled on `MeterProvider`). There is no separate API
to report exemplar data. If an app is already using existing Metrics API
(manually or via instrumentation libraries), exemplars can be configured/enabled
without requiring instrumentation changes.

While the SDK is capable of producing exemplars automatically, the exporters
(and the backends) must also support them in order to be useful. OTLP Metric
Exporter has support for this today, and this [end-to-end
tutorial](../exemplars/README.md) demonstrates how to use exemplars to achieve
correlation from metrics to traces, which is one of the primary use cases for
exemplars.

#### Default behavior

Exemplars in OpenTelemetry .NET are **off by default**
(`ExemplarFilterType.AlwaysOff`). The [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplarfilter)
recommends Exemplars collection should be on by default
(`ExemplarFilterType.TraceBased`) however there is a performance cost associated
with Exemplars so OpenTelemetry .NET has taken a more conservative stance for
its default behavior.

#### ExemplarFilter

`ExemplarFilter` determines which measurements are offered to the configured
`ExemplarReservoir`, which makes the final decision about whether or not the
offered measurement gets recorded as an `Exemplar`. Generally `ExemplarFilter`
is a mechanism to control the overhead associated with the offering and
recording of `Exemplar`s.

OpenTelemetry SDK comes with the following `ExemplarFilter`s (defined on
`ExemplarFilterType`):

* (Default behavior) `AlwaysOff`: Makes no measurements eligible for becoming an
  `Exemplar`. Using this disables `Exemplar` collection and avoids all
  performance costs associated with `Exemplar`s.
* `AlwaysOn`: Makes all measurements eligible for becoming an `Exemplar`.
* `TraceBased`: Makes those measurements eligible for becoming an `Exemplar`
  which are recorded in the context of a sampled `Activity` (span).

The `SetExemplarFilter` extension method on `MeterProviderBuilder` can be used
to set the desired `ExemplarFilterType` and enable `Exemplar` collection:

> [!NOTE]
> The `SetExemplarFilter` API was added in the `1.9.0` release.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    // rest of config not shown
    .SetExemplarFilter(ExemplarFilterType.TraceBased)
    .Build();
```

It is also possible to configure the `ExemplarFilter` by using following
environmental variables:

> [!NOTE]
> Programmatically calling `SetExemplarFilter` will override any defaults set
  using environment variables or configuration.

| Environment variable       | Description                                        | Notes |
| -------------------------- | -------------------------------------------------- |-------|
| `OTEL_METRICS_EXEMPLAR_FILTER` | Sets the default `ExemplarFilter` to use for all metrics. | Added in `1.9.0` |
| `OTEL_DOTNET_EXPERIMENTAL_METRICS_EXEMPLAR_FILTER_HISTOGRAMS`        | Sets the default `ExemplarFilter` to use for histogram metrics. If set `OTEL_DOTNET_EXPERIMENTAL_METRICS_EXEMPLAR_FILTER_HISTOGRAMS` takes precedence over `OTEL_METRICS_EXEMPLAR_FILTER` for histogram metrics. | Experimental key (may be removed or changed in the future). Added in `1.9.0` |

Allowed values:

* `always_off`: Equivalent to `ExemplarFilterType.AlwaysOff`
* `always_on`: Equivalent to `ExemplarFilterType.AlwaysOn`
* `trace_based`: Equivalent to `ExemplarFilterType.TraceBased`

#### ExemplarReservoir

`ExemplarReservoir` receives the measurements sampled by the `ExemplarFilter`
and is responsible for recording `Exemplar`s. The following are the default
reservoirs:

* `AlignedHistogramBucketExemplarReservoir` is the default reservoir used for
Histograms with buckets, and it stores at most one `Exemplar` per histogram
bucket. The `Exemplar` stored is the last measurement recorded - i.e. any new
measurement overwrites the previous one in that bucket.

* `SimpleFixedSizeExemplarReservoir` is the default reservoir used for all
metrics except histograms with buckets. It has a fixed reservoir pool, and
implements the equivalent of [naive
reservoir](https://en.wikipedia.org/wiki/Reservoir_sampling). The reservoir pool
size (currently defaulting to 1) determines the maximum number of `Exemplar`s
stored. Exponential histograms use a `SimpleFixedSizeExemplarReservoir` with a
pool size equal to the number of buckets up to a max of `20`.

See [Change the ExemplarReservoir](#change-the-exemplarreservoir) for details on
how to use the View API to change `ExemplarReservoir`s for an instrument.

See [Building your own
ExemplarReservoir](../extending-the-sdk/README.md#exemplarreservoir) for details
on how to implement custom `ExemplarReservoir`s.

### Instrumentation

// TODO

### MetricReader

[MetricReader](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#metricreader)
allows collecting the pre-aggregated metrics from the SDK. They are typically
paired with a
[MetricExporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#metricexporter)
which does the actual export of metrics.

Though `MetricReader` can be added by using the `AddReader` method on
`MeterProviderBuilder`, most users use the extension methods on
`MeterProviderBuilder` offered by exporter libraries, which adds the correct
`MetricReader`, that is configured to export metrics to the exporter.

Refer to the individual exporter docs to learn how to use them:

* [Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
* [In-memory](../../../src/OpenTelemetry.Exporter.InMemory/README.md)
* [OTLP](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
  (OpenTelemetry Protocol)
* [Prometheus HttpListener](../../../src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md)
* [Prometheus AspNetCore](../../../src/OpenTelemetry.Exporter.Prometheus.AspNetCore/README.md)

### Resource

[Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
is the immutable representation of the entity producing the telemetry. If no
`Resource` is explicitly configured, the
[default](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#semantic-attributes-with-sdk-provided-default-value)
is to use a resource indicating this
[Service](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#service)
and [Telemetry
SDK](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#telemetry-sdk).
The `ConfigureResource` method on `MeterProviderBuilder` can be used to
configure the resource on the provider. `ConfigureResource` accepts an `Action`
to configure the `ResourceBuilder`. Multiple calls to `ConfigureResource` can be
made. When the provider is built, it builds the final `Resource` combining all
the `ConfigureResource` calls. There can only be a single `Resource` associated
with a provider. It is not possible to change the resource builder *after* the
provider is built, by calling the `Build()` method on the
`MeterProviderBuilder`.

`ResourceBuilder` offers various methods to construct resource comprising of
attributes from various sources. For example, `AddService()` adds
[Service](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#service)
resource. `AddAttributes` can be used to add any additional attributes to the
`Resource`. It also allows adding `ResourceDetector`s.

It is recommended to model attributes that are static throughout the lifetime of
the process as Resources, instead of adding them as attributes(tags) on each
measurement.

Follow [this](../../resources/README.md#resource-detector) document
to learn about writing custom resource detectors.

The snippet below shows configuring the `Resource` associated with the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(r => r.AddAttributes(new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("static-attribute1", "v1"),
                    new KeyValuePair<string, object>("static-attribute2", "v2"),
                }))
    .ConfigureResource(resourceBuilder => resourceBuilder.AddService("service-name"))
    .Build();
```

It is also possible to configure the `Resource` by using following
environmental variables:

| Environment variable       | Description                                        |
| -------------------------- | -------------------------------------------------- |
| `OTEL_RESOURCE_ATTRIBUTES` | Key-value pairs to be used as resource attributes. See the [Resource SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable) for more details. |
| `OTEL_SERVICE_NAME`        | Sets the value of the `service.name` resource attribute. If `service.name` is also provided in `OTEL_RESOURCE_ATTRIBUTES`, then `OTEL_SERVICE_NAME` takes precedence. |
