# Changelog

This file contains individual changes for the OpenTelemetry.Extensions.Hosting
package. For highlights and announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

* **Breaking Change** NuGet packages now use the Sigstore bundle format
  (`.sigstore.json`) for digital signatures instead of separate signature
  (`.sig`) and certificate (`.pem`) files. This requires cosign 3.0 or later
  for verification. See the [Digital signing
  section](../../README.md#digital-signing) for updated verification instructions.
  ([#6623](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6623))

* Update to stable versions for .NET 10.0 NuGet packages.
  ([#6667](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6667))

## 1.14.0-rc.1

Released 2025-Oct-21

* **Breaking Change** When targeting `net8.0`, the package now depends on version
  `8.0.0` of the `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Diagnostics.Abstractions`,
  `Microsoft.Extensions.Hosting.Abstractions` and
  `Microsoft.Extensions.Logging.Configuration` NuGet packages.
  ([#6327](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6327))

* Add support for .NET 10.0.
  ([#6307](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6307))

## 1.13.1

Released 2025-Oct-09

## 1.13.0

Released 2025-Oct-01

## 1.12.0

Released 2025-Apr-29

## 1.11.2

Released 2025-Mar-04

## 1.11.1

Released 2025-Jan-22

## 1.11.0

Released 2025-Jan-15

## 1.11.0-rc.1

Released 2024-Dec-11

## 1.10.0

Released 2024-Nov-12

* Updated `Microsoft.Extensions.Hosting.Abstractions` package
  version to `9.0.0`.
  ([#5967](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5967))

## 1.10.0-rc.1

Released 2024-Nov-01

## 1.10.0-beta.1

Released 2024-Sep-30

* Updated `Microsoft.Extensions.Hosting.Abstractions` package
  version to `9.0.0-rc.1.24431.7`.
  ([#5853](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5853))

## 1.9.0

Released 2024-Jun-14

## 1.9.0-rc.1

Released 2024-Jun-07

* The experimental APIs previously covered by `OTEL1000`
  (`OpenTelemetryBuilder.WithLogging` method) are now be part of the public API
  and supported in stable builds.
  ([#5648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5648))

## 1.9.0-alpha.1

Released 2024-May-20

* Reverted obsoletion of `OpenTelemetryBuilder`.
  ([#5571](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5571))

## 1.8.1

Released 2024-Apr-17

## 1.8.0

Released 2024-Apr-02

## 1.8.0-rc.1

Released 2024-Mar-27

## 1.8.0-beta.1

Released 2024-Mar-14

* `OpenTelemetryBuilder` has been marked obsolete. Component authors using
  `OpenTelemetryBuilder` for cross-cutting signal configuration extensions
  should switch to targeting `IOpenTelemetryBuilder` instead.
  ([#5265](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5265))

## 1.7.0

Released 2023-Dec-08

## 1.7.0-rc.1

Released 2023-Nov-29

* Updated `Microsoft.Extensions.Hosting.Abstractions` package
  version to `8.0.0`.
  ([#5051](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5051))

* The `OpenTelemetryBuilder.WithMetrics` method will now register an
  `IMetricsListener` named 'OpenTelemetry' into the `IServiceCollection` to
  enable metric management via the new `Microsoft.Extensions.Diagnostics` .NET 8
  APIs.
  ([#4958](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4958))

* The `OpenTelemetryBuilder.WithLogging` experimental API method will now
  register an `ILoggerProvider` named 'OpenTelemetry' into the
  `IServiceCollection` to enable `ILoggerFactory` integration.
  ([#5072](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5072))

## 1.7.0-alpha.1

Released 2023-Oct-16

* Changed the behavior of the `OpenTelemetryBuilder.AddOpenTelemetry` extension
  to INSERT OpenTelemetry services at the beginning of the `IServiceCollection`
  in an attempt to provide a better experience for end users capturing telemetry
  in hosted services. Note that this does not guarantee that OpenTelemetry
  services will be initialized while other hosted services start, so it is
  possible to miss telemetry until OpenTelemetry services are fully initialized.
  ([#4883](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4883))

## 1.6.0

Released 2023-Sep-05

## 1.6.0-rc.1

Released 2023-Aug-21

* **Experimental (pre-release builds only):** Added [Logs Bridge
  API](https://github.com/open-telemetry/opentelemetry-specification/blob/976432b74c565e8a84af3570e9b82cb95e1d844c/specification/logs/bridge-api.md)
  implementation (`OpenTelemetryBuilder.WithLogging`).
  ([#4735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4735))

## 1.6.0-alpha.1

Released 2023-Jul-12

## 1.5.1

Released 2023-Jun-26

## 1.5.0

Released 2023-Jun-05

## 1.5.0-rc.1

Released 2023-May-25

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
