# Changelog

## Unreleased

* Removes upper constraint for Microsoft.Extensions.Hosting.Abstractions
  dependency. ([#2179](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2179))

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
