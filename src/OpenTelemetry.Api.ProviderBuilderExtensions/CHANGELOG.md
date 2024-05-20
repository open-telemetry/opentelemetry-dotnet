# Changelog

## Unreleased

## 1.9.0-alpha.1

Released 2024-May-20

## 1.8.1

Released 2024-Apr-17

## 1.8.0

Released 2024-Apr-02

## 1.8.0-rc.1

Released 2024-Mar-27

## 1.8.0-beta.1

Released 2024-Mar-14

* Added `IOpenTelemetryBuilder` interface to support authoring extensions which
  can configure multiple OpenTelemetry signals (tracing, metrics, and/or logs).
  ([#5265](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5265))

## 1.7.0

Released 2023-Dec-08

## 1.7.0-rc.1

Released 2023-Nov-29

* Updated `Microsoft.Extensions.DependencyInjection.Abstractions` package
  version to `8.0.0`.
  ([#5051](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5051))

## 1.7.0-alpha.1

Released 2023-Oct-16

## 1.6.0

Released 2023-Sep-05

## 1.6.0-rc.1

Released 2023-Aug-21

## 1.6.0-alpha.1

Released 2023-Jul-12

* **Experimental (pre-release builds only):** Added extension methods to support
  using the [Logs Bridge
  API](https://github.com/open-telemetry/opentelemetry-specification/blob/976432b74c565e8a84af3570e9b82cb95e1d844c/specification/logs/bridge-api.md)
  implementation (eg `LoggerProviderBuilder`) with dependency injection.
  ([#4433](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4433),
  [#4735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4735))

## 1.5.1

Released 2023-Jun-26

## 1.5.0

Released 2023-Jun-05

* Added an `IServiceCollection.ConfigureOpenTelemetryMeterProvider` overload
  which may be used to configure `MeterProviderBuilder`s while the
  `IServiceCollection` is modifiable (before the `IServiceProvider` has been
  created).
  ([#4517](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4517))

## 1.5.0-rc.1

Released 2023-May-25

* Fixed a bug which prevented the
  `TracerProviderBuilder.AddInstrumentation(IServiceProvider, TracerProvider)`
  factory extension from being called during construction of the SDK
  `TracerProvider`.
  ([#4468](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4468))

* Added an `IServiceCollection.ConfigureOpenTelemetryTracerProvider` overload
  which may be used to configure `TracerProviderBuilder`s while the
  `IServiceCollection` is modifiable (before the `IServiceProvider` has been
  created).
  ([#4508](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4508))

## 1.5.0-alpha.2

Released 2023-Mar-31

## 1.5.0-alpha.1

Released 2023-Mar-07

## 1.4.0

Released 2023-Feb-24

## 1.4.0-rc.4

Released 2023-Feb-10

## 1.4.0-rc.3

Released 2023-Feb-01

* Removed `ConfigureBuilder` from the public API.
  ([#4103](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4103))

* Renamed package from `OpenTelemetry.Extensions.DependencyInjection` to
  `OpenTelemetry.Api.ProviderBuilderExtensions`.
  ([#4125](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4125))

## 1.4.0-rc.2

Released 2023-Jan-09

## 1.4.0-rc.1

Released 2022-Dec-12

Initial release.
