# Changelog

## Unreleased

* Modified Sampler implementation to match the spec
  ([#1037](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1037))
* Changed `ActivityProcessor` to implement `IDisposable`
  ([#975](https://github.com/open-telemetry/opentelemetry-dotnet/pull/975))
* Samplers now get the actual TraceId of the Activity to be created.
  ([#1007](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1007))
* Changed the default sampler from `AlwaysOn` to `ParentOrElse(AlwaysOn)` to
  match the spec
  ([#1013](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1013))
* Added `SuppressInstrumentationScope` API
  ([#988](https://github.com/open-telemetry/opentelemetry-dotnet/pull/988))
* Changed `BroadcastActivityProcessor` to `FanOutActivityProcessor`
  ([#1015](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1015))
* Changed `TracerProviderBuilder` and `TracerProviderSdk` design to simply the
  flow and usage
  ([#1008](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1008)
  [#1027](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1027)
  [#1035](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1035))
* Changed `AddActivitySource` to `AddSource` with params support
  ([#1036](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1036))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
