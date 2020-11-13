# Changelog

## Unreleased

* Removed `GetResource` and `SetResource` `Activity` extension methods. Added
  `GetResource` extension method on `BaseProvider`
  ([#1463](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1463))
* Added `ParentProvider` property on `BaseProcessor` and `BaseExporter` classes.
  ([#1463](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1463))
* `Resource` is no longer added to observed `Activity` objects as a
  `CustomProperty`.
  ([#1463](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1463))
* Remove RentrantExportProcessor as it is not required by spec.
* `ActivitySourceAdapter` supports setting `ActivitySource` for Activities
  created without `ActivitySource`.
  ([#1515](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1515/))
  * Implemented Shutdown for TracerProvider
    ([#1489](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1489))

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
  ([#1282](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1282))
  ([#1285](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1285))
* Renamed `SamplingDecision` options (`NotRecord` to `Drop`, `Record` to
  `RecordOnly`, and `RecordAndSampled` to `RecordAndSample`)
  ([#1297](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1297))
* Added `ILogger`/`Microsoft.Extensions.Logging` integration
  ([#1308](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1308))
  ([#1315](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1315))
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
