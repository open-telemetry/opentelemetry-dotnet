# Changelog

## Unreleased

* Allowed metric instrument names to contain `/` characters. ([#4887](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4877))

## 1.6.0

Released 2023-Sep-05

* Increased the character limit of the Meter instrument name from 63 to 255.
  ([#4774](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4774))

* Update default size for `SimpleExemplarReservoir` to `1`.
  ([#4803](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4803))

* Update Metrics SDK to override the default histogram buckets for a set of
  well-known histogram metrics from ASP.NET Core and HttpClient runtime. These
  histogram metrics which have their `Unit` as `s` (second) will have their
  default histogram buckets as `[ 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25,
  0.5, 0.75, 1, 2.5, 5, 7.5, 10 ]`.
  ([#4820](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4820))

## 1.6.0-rc.1

Released 2023-Aug-21

* **Experimental Feature** Added an opt-in feature to aggregate any metric
  measurements that were dropped due to reaching the [max MetricPoints
  limit](https://github.com/open-telemetry/opentelemetry-dotnet/tree/core-1.6.0-alpha.1/docs/metrics/customizing-the-sdk).
  When this feature is enabled, SDK would aggregate such measurements using a
  reserved MetricPoint with a single tag with key as `otel.metric.overflow` and
  value as `true`. The feature is turned-off by default. You can enable it by
  setting the environment variable
  `OTEL_DOTNET_EXPERIMENTAL_METRICS_EMIT_OVERFLOW_ATTRIBUTE` to `true` before
  setting up the `MeterProvider`.
  ([#4737](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4737))

## 1.6.0-alpha.1

Released 2023-Jul-12

* **Experimental (pre-release builds only):**

  * Note: See
    [#4735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4735)
    for the introduction of experimental api support.

  * Add back support for Exemplars. See
    [exemplars](../../docs/metrics/customizing-the-sdk/README.md#exemplars) for
    instructions to enable exemplars.
    ([#4553](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4553))

  * Added [Logs Bridge
    API](https://github.com/open-telemetry/opentelemetry-specification/blob/976432b74c565e8a84af3570e9b82cb95e1d844c/specification/logs/bridge-api.md)
    implementation (`Sdk.CreateLoggerProviderBuilder`, etc.).
    ([#4433](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4433))

  * Obsoleted `LogRecord.LogLevel` in favor of the `LogRecord.Severity` property
    which matches the [OpenTelemetry Specification > Logs DataModel > Severity
    definition](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/data-model.md#field-severitynumber).
    ([#4433](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4433))

  * Added `LogRecord.Logger` property to access the [OpenTelemetry Specification
    Instrumentation
    Scope](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-scope)
    provided during Logger creation.
    ([#4433](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4433))

* Fix the issue of potentially running into the `ArgumentException`: `An
  instance of EventSource with Guid af2d5796-946b-50cb-5f76-166a609afcbb already
  exists.` when using any of the following exporters: `ConsoleExporter`,
  `OtlpExporter`, `ZipkinExporter`, `JaegerExporter`.

## 1.5.1

Released 2023-Jun-26

* Fixed a breaking change causing `LogRecord.State` to be `null` where it was
  previously set to a valid value when
  `OpenTelemetryLoggerOptions.ParseStateValues` is `false` and states implement
  `IReadOnlyList` or `IEnumerable` of `KeyValuePair<string, object>`s.
  ([#4609](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4609))

* **Breaking Change** Removed the support for parsing `TState` types passed to
  the `ILogger.Log<TState>` API when `ParseStateValues` is true and `TState`
  does not implement either `IReadOnlyList<KeyValuePair<string, object>>` or
  `IEnumerable<KeyValuePair<string, object>>`. This feature was first introduced
  in the `1.5.0` stable release with
  [#4334](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334) and
  has been removed because it makes the OpenTelemetry .NET SDK incompatible with
  native AOT.
  ([#4614](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4614))

## 1.5.0

Released 2023-Jun-05

* Fixed a bug introduced by
  [#4508](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4508) in
  1.5.0-rc.1 which caused the "Build" extension to return `null` when performing
  chained/fluent calls.
  ([#4529](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4529))

* Marked `Exemplars` and related APIs `internal` as the spec for `Exemplars` is
  not stable yet. This would be added back in the `1.6.*` prerelease versions
  right after `1.5.0` stable version is released.
  ([#4533](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4533))

## 1.5.0-rc.1

Released 2023-May-25

* The default resource provided by `ResourceBuilder.CreateDefault()` now adds
  the `telemetry.sdk.*` attributes defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/tree/12fcec1ff255b1535db75708e52a3a21f86f0fae/specification/resource/semantic_conventions#semantic-attributes-with-sdk-provided-default-value).
  ([#4369](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4369))

* Fixed an issue with `HashCode` computations throwing exceptions on .NET
  Standard 2.1 targets.
  ([#4362](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4362))

* Update value of the resource attribute `telemetry.sdk.version` to show the tag
  name which resembles the package version of the SDK.
  ([#4375](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4375))

* Obsoleted `State` and `StateValues` properties and added `Body` and
  `Attributes` properties on `LogRecord`. Note: `LogRecord.Attributes` and
  `LogRecord.StateValues` point to the same data. "Attributes" is what the
  OpenTelemetry Specification defines so this was changed for clarity &
  consistency with the specification.
  ([#4334](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334))

* Tweaked the behavior of the `OpenTelemetryLoggerOptions.ParseStateValues`
  flag:

  * `LogRecord.Attributes` (aka `LogRecord.StateValues`) are now automatically
  included for all log messages with states implementing `IReadOnlyList` or
  `IEnumerable`.

  * `OpenTelemetryLoggerOptions.ParseStateValues` is now used to tell the SDK to
  parse (using reflection) attributes for custom states which do not implement
  `IReadOnlyList` or `IEnumerable`. Only top-level properties are included.

  * `LogRecord.State` will only be set to the raw state object if no attributes
  are found.

  See [#4334](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334)
  for details.

* If a template (`{OriginalFormat}` attribute) cannot be found on log messages a
  formatted message will now automatically be generated (even if
  `OpenTelemetryLoggerOptions.IncludeFormattedMessage` is set to `false`).
  ([#4334](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334))

## 1.5.0-alpha.2

Released 2023-Mar-31

* Enabling `SetErrorStatusOnException` on TracerProvider will now set the
`Status` property on Activity to `ActivityStatusCode.Error` in case of an error.
This will be done in addition to current behavior of setting `otel.status_code`
tag on activity.
([#4336](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4336))

* Add support for configuring the
  [Base2 Exponential Bucket Histogram Aggregation](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#base2-exponential-bucket-histogram-aggregation)
  using the `AddView` API. This aggregation is supported by OTLP but not yet by
  Prometheus.
  ([#4337](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4337))

* Implementation of `SuppressInstrumentationScope` changed to improve
  performance.
  ([#4304](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4304))

## 1.5.0-alpha.1

Released 2023-Mar-07

* Added Exemplar support. See [exemplars](../../docs/metrics/customizing-the-sdk/README.md#exemplars)
  for instructions to enable exemplars.

* Added `AddDetector` factory overload on `ResourceBuilder`.
  ([#4261](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4261))

## 1.4.0

Released 2023-Feb-24

## 1.4.0-rc.4

Released 2023-Feb-10

* Removed the dependency on System.Reflection.Emit.Lightweight
  ([#4140](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4140))

* Moved the `AddOpenTelemetry` extension into the
  `OpenTelemetry.Extensions.Hosting` package so that the `StartWithHost` API
  could be removed.
  ([#4174](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4174))

## 1.4.0-rc.3

Released 2023-Feb-01

* Removed the dependency on
  Microsoft.Extensions.Configuration.EnvironmentVariables
  ([#4092](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4092))

* Removed the explicit reference to Microsoft.Extensions.Options version 5.0 and
  reverted back to the transitive reference of version 3.1
  ([#4093](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4093))

* Added `SetSampler`, `AddProcessor`, & `AddReader` factory extensions.
  ([#4103](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4103))

## 1.4.0-rc.2

Released 2023-Jan-09

* Performance Improvement: Update the internal structure used to store metric
  dimensions from a combination of `string[]` and `object[]` to a
  `KeyValuePair<string, object>[]`. This results in faster copying of the metric
  dimensions required for `MetricPoint` lookup on the hot path.
  ([#4059](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4059))

## 1.4.0-rc.1

Released 2022-Dec-12

* Added dependency injection support in the `ResourceBuilder` class and added
  support for loading environment variables from `IConfiguration` for the
  `AddEnvironmentVariableDetector` extension (Logs)
  ([#3889](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3889))

* Refactored `AddInstrumentation`, `ConfigureServices` and `ConfigureBuilder`
  APIs into the OpenTelemetry.Extensions.DependencyInjection package and added
  the `IServiceCollection.AddOpenTelemetry` API
  ([#3923](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3923))

* Removed `ConfigureResource` on `OpenTelemetryLoggingOptions`
  ([#3999](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3999))

## 1.4.0-beta.3

Released 2022-Nov-07

* Fix instrument naming enforcement implementation to match the spec.
  ([#3821](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3821))

* Added support for loading environment variables from `IConfiguration` when
  using the `MetricReaderOptions` & `BatchExportActivityProcessorOptions`
  classes.
  ([#3760](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3760),
  [#3776](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3776))

* Added dependency injection support in the `ResourceBuilder` class and added
  support for loading environment variables from `IConfiguration` for the
  `AddEnvironmentVariableDetector` extension (Traces & Metrics)
  ([#3782](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3782),
  [#3798](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3798))

* Breaking: MetricPoint API to retrieve Histogram Min, Max changed. The existing
  pattern of checking if Min/Max is available with `HasMinMax()` and then
  retrieving the same using `GetHistogramMin()`, `GetHistogramMax()` is replaced
  with a single API `TryGetHistogramMinMaxValues(out double min, out double
  max)`.
  ([#3822](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3822))

## 1.4.0-beta.2

Released 2022-Oct-17

* Make recording of `Min` and `Max` for histograms configurable, enabled by
  default.
  ([#2735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2735))

* Changed default bucket boundaries for Explicit Bucket Histogram from [0, 5,
  10, 25, 50, 75, 100, 250, 500, 1000] to [0, 5, 10, 25, 50, 75, 100, 250, 500,
  750, 1000, 2500, 5000, 7500, 10000].
  ([#3722](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3722))

* Fixed an issue where `LogRecord.ForEachScope` may return scopes from a
  previous log if accessed in a custom processor before
  `BatchLogRecordExportProcessor.OnEnd` is fired.
  ([#3731](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3731))

* Added support for loading environment variables from `IConfiguration` when
  using `TracerProviderBuilder` or `MeterProviderBuilder`
  ([#3720](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3720))

## 1.4.0-beta.1

Released 2022-Sep-29

* Use binary search for histograms with 50 or more supplied boundaries.
  ([#3252](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3252))

* Allows samplers the ability to modify tracestate if desired.
  ([#3610](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3610))

* Added support for `UpDownCounter` and `ObservableUpDownCounter` instruments.
  ([#3606](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3606))

* Added support for dependency injection scenarios when configuring
  `MeterProvider`.
  ([#3646](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3646))

* Revert new logging APIs pending OTel specification changes.
  ([#3702](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3702))

* Fix Histogram synchronization issue: Use the same synchronization mechanism
  for Histograms Update and Snapshot.
  ([#3534](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3534))

## 1.4.0-alpha.2

Released 2022-Aug-18

* Added `Sdk.CreateLoggerProviderBuilder` method and support for dependency
  injection scenarios when configuring `OpenTelemetryLoggerProvider`
  ([#3504](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3504))

* Added support for dependency injection scenarios when configuring
  `TracerProvider`
  ([#3533](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3533))

## 1.4.0-alpha.1

Released 2022-Aug-02

* `TracerProviderSDK` modified for spans with remote parent. For such spans
  activity will be created irrespective of SamplingResult, to maintain context
  propagation.
  ([#3329](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3329))
* Fix issue where a measurement would be dropped when recording it with a
  null-valued tag.
  ([#3325](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3325))
* `CompositeProcessor` will now ensure `ParentProvider` is set on its children
  ([#3368](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3368))
* Added `ForceFlush` and helper ctors on `OpenTelemetryLoggerProvider`
  ([#3364](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3364))
* `Timestamp`, `TraceId`, `SpanId`, `TraceFlags`, `TraceState`, `CategoryName`,
  `LogLevel`, `EventId`, & `Exception` properties on `LogRecord` now expose
  `set` methods
  ([#3378](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3378))
* Handle possible exception when initializing the default service name.
  ([#3405](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3405))
* Add `ConfigureResource` which can replace SetResourceBuilder more succinctly
  in most cases and has greater flexibility (applies to TracerProviderBuilder,
  MeterProviderBuilder, OpenTelemetryLoggingOptions).
  ([#3307](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3307))
* `LogRecord` instances are now reused to reduce memory pressure
  ([#3385](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3385))
* Fix exact match of activity source name when `wildcard` is used.
  ([#3446](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3446))
* Added AddOpenTelemetry `ILoggingBuilder` extensions which accept
  `OpenTelemetryLoggerProvider` directly
  ([#3489](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3489))

## 1.3.0

Released 2022-Jun-03

## 1.3.0-rc.2

Released 2022-June-1

* Fix null reference exception when a metric view does not match an instrument.
  ([#3285](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3285))
* Swallow `ObjectDisposedException` in `BatchExportProcessor` and
  `PeriodicExportingMetricReader`.
  ([#3291](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3291))

## 1.3.0-beta.2

Released 2022-May-16

* Exposed public setters for `LogRecord.State`, `LogRecord.StateValues`, and
  `LogRecord.FormattedMessage`.
  ([#3217](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3217))

## 1.3.0-beta.1

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework version supported is
  .NET 4.6.2.
  ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))
* Bumped minimum required version of `Microsoft.Extensions.Logging` and
  `Microsoft.Extensions.Logging.Configuration` to 3.1.0
  ([#2582](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3196))

## 1.2.0

Released 2022-Apr-15

* Make setter for `MetricReaderOptions.PeriodicExportingMetricReaderOptions`
  property public.
  ([#3184](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3184))

## 1.2.0-rc5

Released 2022-Apr-12

* Removed the `Temporality` setting on `MetricReader` and replaced it with
  `TemporalityPreference`. This is a breaking change. `TemporalityPreference` is
  used to determine the `AggregationTemporality` used on a per-instrument kind
  basis. Currently, there are two preferences:
  * `Cumulative`: Measurements from all instrument kinds are aggregated using
    `AggregationTemporality.Cumulative`.
  * `Delta`: Measurements from `Counter`, `ObservableCounter`, and `Histogram`
    instruments are aggregated using `AggregationTemporality.Delta`. When
    UpDownCounters are supported with [DiagnosticSource version 7.0
    onwards](https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource/7.0.0-preview.2.22152.2),
    they will be aggregated using `AggregationTemporality.Cumulative`.
  ([#3153](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3153))
* Fix issue where `ExplicitBucketHistogramConfiguration` could be used to
  configure metric streams for instruments that are not histograms. Currently,
  it is not possible to change the aggregation of an instrument with views. This
  may be possible in the future.
  ([#3126](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3126))
* Conformed to the specification to ensure that each view that an instrument
  matches results in a new metric stream. With this change it is possible for
  views to introduce conflicting metric streams. Any conflicts encountered will
  result in a diagnostic log.
  ([#3148](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3148))

## 1.2.0-rc4

Released 2022-Mar-30

* The `PeriodicExportingMetricReader` now accepts an
  `ExportIntervalMilliseconds` of `-1` indicating an infinite export interval
  period.
  ([#2982](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2982))
* Fix bug where multiple views selecting a single instrument can result in
  duplicate updates to a single metric point.
  ([#3006](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3006))
* Added the `PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds`
  option.
  ([#3038](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3038))
* Removed `MetricReaderType`. This enumeration was previously used when
  configuring a metric reader with an exporter to configure whether the export
  cycle would be periodic or manual (i.e., requiring a explicit call to flush
  metrics). This change affects the push-based metric exporters: OTLP, Console,
  and InMemory. For these exporters, a manual export cycle can now be achieved
  by setting `PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds`
  to `-1`.
  ([#3038](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3038))
* Marked members of the `MetricPoint` `struct` which do not mutate state as
  `readonly`
  ([#3065](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3065))
* [Bug fix] OpenTelemetryLoggerProvider is now unaffected by changes to
  OpenTelemetryLoggerOptions after the LoggerFactory is built.
  ([#3055](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3055))

## 1.2.0-rc3

Released 2022-Mar-04

* Instantiating multiple metric instruments with the same name and also
  identical in all other respects - same type, description, and unit - result in
  a single metric stream aggregating measurements from all the identical
  instruments. Instantiating multiple metric instruments with the same name but
  differ in some respect - different type, description, or unit - will result in
  a separate metric stream for each distinct instrument.
  ([#2916](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2916))
* The `Meter` property on `OpenTelemetry.Metrics.Metric` has been removed. It
  now has `MeterName` and `MeterVersion` properties.
  ([#2916](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2916))
* Added support for implementing custom `ResourceDetector`.
  ([#2949](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2949/)
  [#2897](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2897))
* Perf improvement for Histogram and HistogramSumCount by implementing lock-free
  updates.
  ([#2951](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2951)
  [#2961](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2961))

## 1.2.0-rc2

Released 2022-Feb-02

* Make `MetricPoint` of `MetricPointAccessor` readonly.
  ([#2736](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2736))
* Fail-fast when using AddView with guaranteed conflict.
  ([#2751](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2751))
* Swallow `ObjectDisposedException` from the `BatchExportProcessor` worker
  thread.
  ([#2844](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2844))
* Performance improvement: when emitting metrics, users are strongly advised to
  provide tags with same Key order, to achieve maximum performance.
  ([#2805](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2805))

## 1.2.0-rc1

Released 2021-Nov-29

* Prevent accessing activity Id before sampler runs in case of legacy
  activities.
  ([#2659](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2659))
* Added `ReadOnlyTagCollection` and expose `Tags` on `MetricPoint` instead of
  `Keys`+`Values`
  ([#2642](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2642))
* Refactored `MetricPoint` and added public methods: `GetBucketCounts`,
  `GetExplicitBounds`, `GetHistogramCount`, and `GetHistogramSum`
  ([#2657](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2657))
* Remove MetricStreamConfiguration.Aggregation, as the feature to customize
  aggregation is not implemented yet.
  ([#2660](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2660))
* Removed the public property `HistogramMeasurements` and added a public method
  `GetHistogramBuckets` instead. Renamed the class `HistogramMeasurements` to
  `HistogramBuckets` and added an enumerator of type `HistogramBucket` for
  enumerating `BucketCounts` and `ExplicitBounds`. Removed `GetBucketCounts` and
  `GetExplicitBounds` methods from `MetricPoint`.
  ([#2664](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2664))
* Refactored temporality setting to align with the latest spec.
  ([#2666](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2666))
* Removed the public properties `LongValue`, `DoubleValue`, in favor of their
  counterpart public methods `GetSumLong`, `GetSumDouble`,
  `GetGaugeLastValueLong`, `GetGaugeLastValueDouble`.
  ([#2667](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2667))
* MetricType modified to reserve bits for future types.
  ([#2693](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2693))

## 1.2.0-beta2

Released 2021-Nov-19

* Renamed `HistogramConfiguration` to `ExplicitBucketHistogramConfiguration` and
  changed its member `BucketBounds` to `Boundaries`.
  ([#2638](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2638))
* Metrics with the same name but from different meters are allowed.
  ([#2634](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2634))
* Metrics SDK will not provide inactive Metrics to delta exporter.
  ([#2629](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2629))
* Histogram bounds are validated when added to a View.
  ([#2573](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2573))
* Changed `BatchExportActivityProcessorOptions` constructor to throw
  `FormatException` if it fails to parse any of the supported environment
  variables.
* Added `BaseExporter.ForceFlush`.
  ([#2525](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2525))
* Exposed public `Batch(T[] items, int count)` constructor on `Batch<T>` struct
  ([#2542](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2542))
* Added wildcard support for AddMeter.
  ([#2459](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2459))
* Add support for multiple Metric readers
  ([#2596](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2596))
* Add ability to configure MaxMetricStreams, MaxMetricPointsPerMetricStream
  ([#2635](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2635))

## 1.2.0-beta1

Released 2021-Oct-08

* Exception from Observable instrument callbacks does not result in entire
  metrics being lost.
* SDK is allocation-free on recording of measurements with up to 8 tags.
* TracerProviderBuilder.AddLegacySource now supports wildcard activity names.
  ([#2183](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2183))
* Instrument and View names are validated [according with the
  spec](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument).
  ([#2470](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2470))

## 1.2.0-alpha4

Released 2021-Sep-23

* `BatchExportProcessor.OnShutdown` will now log the count of dropped telemetry
  items.
  ([#2331](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2331))
* Changed `CompositeProcessor<T>.OnForceFlush` to meet with the spec
  requirement. Now the SDK will invoke `ForceFlush` on all registered
  processors, even if there is a timeout.
  ([#2388](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2388))

## 1.2.0-alpha3

Released 2021-Sep-13

* Metrics perf improvements, bug fixes. Replace MetricProcessor with
  MetricReader.
  ([#2306](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2306))
* Add `BatchExportActivityProcessorOptions` which supports field value
  overriding using `OTEL_BSP_SCHEDULE_DELAY`, `OTEL_BSP_EXPORT_TIMEOUT`,
  `OTEL_BSP_MAX_QUEUE_SIZE`, `OTEL_BSP_MAX_EXPORT_BATCH_SIZE` environmental
  variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/sdk-environment-variables.md#batch-span-processor).
  ([#2219](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2219))

## 1.2.0-alpha2

Released 2021-Aug-24

* More Metrics features. All instrument types, push/pull exporters,
  Delta/Cumulative temporality supported.
* `ResourceBuilder.CreateDefault` has detectors for `OTEL_RESOURCE_ATTRIBUTES`,
  `OTEL_SERVICE_NAME` environment variables so that explicit
  `AddEnvironmentVariableDetector` call is not needed.
  ([#2247](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2247))
* `ResourceBuilder.AddEnvironmentVariableDetector` handles `OTEL_SERVICE_NAME`
   environmental variable.
   ([#2209](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2209))
* Removes upper constraint for Microsoft.Extensions.Logging dependencies.
  ([#2179](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2179))
* OpenTelemetryLogger modified to not throw, when the formatter supplied in
  ILogger.Log call is null.
  ([#2200](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2200))

## 1.2.0-alpha1

Released 2021-Jul-23

* Add basic Metrics support with a single pipeline, and supporting Counter
  (sync) instrument. Push and Pull exporters are supported.
  ([#2174](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2174))
* Removes .NET Framework 4.5.2, .NET 4.6 support. The minimum .NET Framework
  version supported is .NET 4.6.1.
  ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

## 1.1.0

Released 2021-Jul-12

## 1.1.0-rc1

Released 2021-Jun-25

* Moved `IDeferredTracerProviderBuilder` to API library.
  ([#2058](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2100))

## 1.1.0-beta4

Released 2021-Jun-09

## 1.1.0-beta3

Released 2021-May-11

* `AddLegacySource()` moved out of `TracerProviderBuilderExtensions` and into
  public API
  ([#2019](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2019))
* Fixed an issue causing inconsistent log scopes when using
  `BatchLogRecordExportProcessor`. To make parsing scopes easier the
  `LogRecord.ForEachScope` signature has been changed to receive instances of
  `LogRecordScope` (a new type which implements
  `IEnumerator<KeyValuePair<string, object>>` for accessing scope items)
  ([#2026](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2026))

## 1.1.0-beta2

Released 2021-Apr-23

* Use `AssemblyFileVersionAttribute` instead of `FileVersionInfo.GetVersionInfo`
  to get the SDK version attribute to ensure that it works when the assembly is
  not loaded directly from a file on disk
  ([#1908](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1908))

## 1.1.0-beta1

Released 2021-Mar-19

* Removed SuppressScope Increment/Decrement from DiagnosticSourceListeners.
  ([1893](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1893))
* Added `TracerProviderBuilder.SetErrorStatusOnException` which automatically
  sets the activity status to `Error` when exception happened.
  ([#1858](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1858)
  [#1875](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1875))
* Added `ForceFlush` to `TracerProvider`.
  ([#1837](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1837))
* Added a TracerProviderBuilder extension method called `AddLegacySource` which
  is used by instrumentation libraries that use DiagnosticSource to get
  activities processed without ActivitySourceAdapter.
  [#1836](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1836)
  [#1860](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1860)
* Added new constructor with optional parameters to allow customization of
  `ParentBasedSampler` behavior.
  ([#1727](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1727))
* The application base directory is now tested after the current directory when
  searching for the [self diagnostic configuration
  file](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/README.md#troubleshooting).
  ([#1865](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1865))
* Resource Attributes now accept primitive arrays as values.
  ([#1852](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1852))
* Fixed
  [#1846](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1846):
  `ParentBasedSampler` will no longer explicitly consider Activity links.
  ([#1851](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1851))
* Added `IncludeScopes`, `IncludeFormattedMessage`, & `ParseStateValues` on
  `OpenTelemetryLoggerOptions`. Added `FormattedMessage`, `StateValues`, &
  `ForEachScope` on `LogRecord`.
  ([#1869](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1869)
  [#1883](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1883))
* Added `SetResourceBuilder` support to `OpenTelemetryLoggerOptions`.
  ([#1913](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1913))
* Added `IDeferredTracerProviderBuilder` and `TracerProviderBuilderBase` to
  support dependency injection through OpenTelemetry.Extensions.Hosting.
  ([#1889](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1889))

## 1.0.1

Released 2021-Feb-10

## 1.0.0-rc4

Released 2021-Feb-09

## 1.0.0-rc3

Released 2021-Feb-04

* Default `Resource` will now contain service.name instead of Telemetry SDK.
  ([#1744](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1744))
* Added GetDefaultResource() method to `Provider`.
  ([#1768](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1768))

## 1.0.0-rc2

Released 2021-Jan-29

* The following extension methods on `ResourceBuilder` has been moved from the
  `OpenTelemetry` namespace to the `OpenTelemetry.Resources` namespace:
  `AddEnvironmentVariableDetector`, `AddAttributes`, `AddService`, and
  `AddTelemetrySdk`.
  ([#1576](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1576))
* Metrics API/SDK support is in an experimental state and is not recommended for
  production use. All metric APIs have been marked with the `Obsolete`
  attribute. See
  [#1501](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501)
  for more information.
  ([#1611](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1611))
* Modified SimpleExportProcessor and BatchExportProcessor to abstract classes;
  Added SimpleActivityExportProcessor, SimpleLogRecordExportProcessor,
  BatchActivityExportProcessor, BatchLogRecordExportProcessor; Added the check
  for Activity.Recorded in SimpleActivityExportProcessor and
  BatchActivityExportProcessor
  ([#1622](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1622))
* Added check in `ActivitySourceAdapter` class for root activity if trace ID is
  overridden by calling `SetParentId`
  ([#1355](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1355))
* Resource Attributes now accept int, short, and float as values, converting
  them to supported data types (long for int/short, double for float). For
  invalid attributes we now throw an exception instead of logging an error.
  ([#1720](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1720))
* Merging "this" resource with an "other" resource now prioritizes the "other"
  resource's attributes in a conflict. We've rectified to follow a recent change
  to the spec. We previously prioritized "this" resource's tags.
  ([#1728](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1728))
* `BatchExportProcessor` will now flush any remaining spans left in a `Batch`
  after the export operation has completed.
  ([#1726](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1726))
* Fixed a bug to allow the Self Diagnostics log file to be opened simultaneously
  by another process in read-only mode for .NET Framework.
  ([#1693](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1693))
* Metrics removed as it is not part 1.0.0 release. See issue
  [#1501](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501)
  for details on Metric release plans.
* Fix Resource attribute telemetry.sdk.version to have correct file version.
* Metrics removed as it is not part 1.0.0 release. See issue
  [#1501](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501)
  for details on Metric release plans.

## 1.0.0-rc1.1

Released 2020-Nov-17

* Removed `GetResource` and `SetResource` `Activity` extension methods. Added
  `GetResource` extension method on `BaseProvider`
  ([#1463](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1463))
* Added `ParentProvider` property on `BaseProcessor` and `BaseExporter` classes.
  ([#1463](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1463))
* `Resource` is no longer added to observed `Activity` objects as a
  `CustomProperty`.
  ([#1463](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1463))
* Removed `ReentrantExportProcessor` as it is not required by spec.
  ([#1496](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1496))
* `ActivitySourceAdapter` supports setting `ActivitySource` for Activities
  created without `ActivitySource`.
  ([#1515](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1515/))
* Implemented `Shutdown` for `TracerProvider`.
  ([#1489](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1489))
* `Resources.CreateServiceResource` has been removed in favor of the
  `ResourceBuilder` API.
  ([#1533](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1533))
* `TracerProviderBuilder.SetResource` has been changed to
  `TracerProviderBuilder.SetResourceBuilder`.
  ([#1533](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1533))
* By default `TracerProvider` will set a `Resource` containing [Telemetry
    SDK](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#telemetry-sdk)
    details
    ([#1533](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1533)):
  * `telemetry.sdk.name` = `opentelemetry`
  * `telemetry.sdk.language` = `dotnet`
  * `telemetry.sdk.version` = [SDK version]
* `Resource` constructor marked as internal, as `ResourceBuilder` is the
  recommended API to build resources.
  ([#1566](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1566))
* Changed BaseExportProcessor to have it override OnExport instead of OnEnd;
  Added check for ActivityTraceFlags to BaseExportProcessor OnEnd
  ([#1574](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1574))

## 0.8.0-beta.1

Released 2020-Nov-5

* TracerProviderBuilder API changes Renamed AddInstrumentation to
  AddDiagnosticSourceInstrumentation and made internal. Added AddInstrumentation
  ([#1454](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1454))
* DiagnosticSource subscription helper classes (DiagnosticSourceSubscriber,
  ListenerHandler,PropertyFetcher) are made internal.

## 0.7.0-beta.1

Released 2020-Oct-16

* Changed `ActivityExporter.OnShutdown`, `ActivityExporter.Shutdown`,
  `ActivityProcessor.OnShutdown` and `ActivityProcessor.Shutdown` to return
  boolean value
  ([#1282](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1282)
  [#1285](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1285))
* Renamed `SamplingDecision` options (`NotRecord` to `Drop`, `Record` to
  `RecordOnly`, and `RecordAndSampled` to `RecordAndSample`)
  ([#1297](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1297))
* Added `ILogger`/`Microsoft.Extensions.Logging` integration
  ([#1308](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1308)
  [#1315](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1315))
* Changed exporter and processor to generic types
  ([#1328](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1328)):
  * `ActivityExporter` changed to `BaseExporter<Activity>`
  * `ActivityProcessor` changed to `BaseProcessor<Activity>`
  * `BatchExportActivityProcessor` changed to `BatchExportProcessor<Activity>`
  * `ReentrantExportActivityProcessor` changed to
    `ReentrantExportProcessor<Activity>`
  * `SimpleExportActivityProcessor` changed to `SimpleExportProcessor<Activity>`

## 0.6.0-beta.1

Released 2020-Sep-15

* Fixes [953](https://github.com/open-telemetry/opentelemetry-dotnet/issues/953)
* Changes arising from `DiagnosticSource` changes
  ([#1203](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1203))
* `PropertyFetcher` is now public
  ([#1232](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1232))
* `PropertyFetcher` changed to `PropertyFetcher<T>`
  ([#1238](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1238))

## 0.5.0-beta.2

Released 2020-08-28

* Changed `ActivityProcessor` to implement `IDisposable`
  ([#975](https://github.com/open-telemetry/opentelemetry-dotnet/pull/975))
* Samplers now get the actual TraceId of the Activity to be created.
  ([#1007](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1007))
* Changed the default sampler from `AlwaysOn` to `ParentOrElse(AlwaysOn)` to
  match the spec
  ([#1013](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1013))
* Added `SuppressInstrumentationScope` API
  ([#988](https://github.com/open-telemetry/opentelemetry-dotnet/pull/988)
  [#1067](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1067))
* Changed `BroadcastActivityProcessor` to `FanOutActivityProcessor`
  ([#1015](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1015))
* Changed `TracerProviderBuilder` and `TracerProviderSdk` design to simply the
  flow and usage
  ([#1008](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1008)
  [#1027](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1027)
  [#1035](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1035))
* Changed `AddActivitySource` to `AddSource` with params support
  ([#1036](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1036))
* Modified Sampler implementation to match the spec
  ([#1037](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1037))
* Refactored simple export and batch export APIs
  ([#1078](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1078)
  [#1081](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1081)
  [#1083](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1083)
  [#1085](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1085)
  [#1087](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1087)
  [#1094](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1094)
  [#1113](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1113)
  [#1127](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1127)
  [#1129](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1129)
  [#1135](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1135))
* Changed `MeterProviderBuilder` and `MeterProviderSdk` design to simply the
  flow and usage
  ([#1149](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1149))
* Renamed `ParentOrElseSampler` to `ParentBasedSampler`
  ([#1173](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1173))
* Renamed `ProbabilitySampler` to `TraceIdRatioBasedSampler`
  ([#1174](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1174))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
