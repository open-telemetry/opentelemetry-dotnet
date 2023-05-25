# Changelog

## Unreleased

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
