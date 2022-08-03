# Changelog

## Unreleased

## 1.0.0-rc9.4

Released 2022-Jun-03

## 1.0.0-rc9.3

Released 2022-Apr-15

## 1.0.0-rc9.2

Released 2022-Apr-12

## 1.0.0-rc9.1

Released 2022-Mar-30

## 1.0.0-rc10 (broken. use 1.0.0-rc9.1 and newer)

Released 2022-Mar-04

* Fixes an issue where the initialization of some aspects of the SDK can be
  delayed when using the `AddOpenTelemetryTracing` and
  `AddOpenTelemetryMetrics` methods. Namely, self-diagnostics and the default
  context propagator responsible for propagating trace context and baggage.
  ([#2901](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2901))

## 1.0.0-rc9

Released 2022-Feb-02

## 1.0.0-rc8

Released 2021-Oct-08

* Removes upper constraint for Microsoft.Extensions.Hosting.Abstractions
  dependency.
  ([#2179](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2179))

* Added `AddOpenTelemetryMetrics` extensions on `IServiceCollection` to register
  OpenTelemetry `MeterProvider` with application services. Added
  `AddInstrumentation<T>`, `AddReader<T>`, and `Configure` extensions on
  `MeterProviderBuilder` to support dependency injection scenarios.
  ([#2412](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2412))

## 1.0.0-rc7

Released 2021-Jul-12

## 1.0.0-rc6

Released 2021-Jun-25

* Added `GetServices` extension.
  ([#2058](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2100))

## 1.0.0-rc5

Released 2021-Jun-09

## 1.0.0-rc4

Released 2021-Apr-23

* Added `AddInstrumentation<T>`, `AddProcessor<T>`, `SetSampler<T>`, and
  `Configure` extensions to support dependency injection through the
  OpenTelemetry.Extensions.Hosting `TracerProviderBuilder`.
  ([#1889](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1889))

## 1.0.0-rc3

Released 2021-Mar-19

## 1.0.0-rc2

Released 2021-Jan-29

## 1.0.0-rc1.1

Released 2020-Nov-17

## 0.8.0-beta.1

Released 2020-Nov-5

* Removed AddOpenTelemetryTracing method which takes Func returning
  TracerProvider.

## 0.7.0-beta.1

Released 2020-Oct-16

## 0.6.0-beta.1

Released 2020-Sep-15

* Renamed all extension methods from AddOpenTelemetryTracerProvider to AddOpenTelemetryTracing

## 0.5.0-beta.2

Released 2020-08-28

* Renamed all extension methods from AddOpenTelemetry to AddOpenTelemetryTracerProvider

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
