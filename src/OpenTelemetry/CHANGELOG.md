# Changelog

## Unreleased

* Make `MetricPoint` of `MetricPointAccessor` readonly.
  ([2736](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2736))

* Fail-fast when using AddView with guaranteed conflict.
  ([2751](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2751))

## 1.2.0-rc1

Released 2021-Nov-29

* Prevent accessing activity Id before sampler runs in case of legacy
  activities.
  ([2659](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2659))

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

* SDK is allocation-free on recording of measurements with upto 8 tags.

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
  `OTEL_BSP_MAX_QUEUE_SIZE`, `OTEL_BSP_MAX_EXPORT_BATCH_SIZE` envionmental
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
  ([#1869](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1869) &
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
* Added check in `ActivitySourceAdapter` class for root activity if traceid is
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
* Fixed a bug to allow the Self Diagnostics log file to be opened simutaneously
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
