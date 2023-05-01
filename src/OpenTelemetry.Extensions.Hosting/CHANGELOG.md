# Changelog

## Unreleased

## 1.5.0-alpha.2

Released 2023-Mar-31

## 1.5.0-alpha.1

Released 2023-Mar-07

## 1.4.0

Released 2023-Feb-24

* Updated OTel SDK dependency to 1.4.0

* Removed deprecated extensions: `AddOpenTelemetryTracing`,
  `AddOpenTelemetryMetrics`, `Configure`, & `GetServices`.
  ([#4071](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4071))

## 1.4.0-rc.4

Released 2023-Feb-10

* Added `AddOpenTelemetry` extension from SDK and removed `StartWithHost`.
  `AddOpenTelemetry` now registers the `IHostedService` used to start collecting
  traces and/or metrics.
  ([#4174](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4174))

## 1.4.0-rc.3

Released 2023-Feb-01

## 1.4.0-rc.2

Released 2023-Jan-09

* If the OpenTelemetry SDK cannot start it will now throw exceptions and prevent
  the host from starting.
  ([#4006](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4006))

## 1.4.0-rc.1

Released 2022-Dec-12

* Added the `OpenTelemetryBuilder.StartWithHost` extension.
  ([#3923](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3923))

## 1.0.0-rc9.9

Released 2022-Nov-07

## 1.0.0-rc9.8

Released 2022-Oct-17

## 1.0.0-rc9.7

Released 2022-Sep-29

* Dependency injection support when configuring
  `TracerProvider` has been moved into the SDK.
  ([#3533](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3533))

* Dependency injection support when configuring
  `MeterProvider` has been moved into the SDK.
  ([#3646](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3646))

## 1.0.0-rc9.6

Released 2022-Aug-18

## 1.0.0-rc9.5

Released 2022-Aug-02

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
