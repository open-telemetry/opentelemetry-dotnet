# Changelog

## Unreleased

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
