# Changelog

This file contains individual changes for the OpenTelemetry.Exporter.InMemory
package. For highlights and announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

## 1.14.0-rc.1

Released 2025-Oct-21

* **Breaking Change** When targeting `net8.0`, the package now depends on version
  `8.0.0` of the `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Diagnostics.Abstractions` and
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

## 1.10.0-rc.1

Released 2024-Nov-01

## 1.10.0-beta.1

Released 2024-Sep-30

## 1.9.0

Released 2024-Jun-14

## 1.9.0-rc.1

Released 2024-Jun-07

* The experimental APIs previously covered by `OTEL1000`
  (`LoggerProviderBuilder.AddInMemoryExporter` extension) are now part of the
  public API and supported in stable builds.
  ([#5648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5648))

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

## 1.7.0

Released 2023-Dec-08

## 1.7.0-rc.1

Released 2023-Nov-29

## 1.7.0-alpha.1

Released 2023-Oct-16

## 1.6.0

Released 2023-Sep-05

## 1.6.0-rc.1

Released 2023-Aug-21

## 1.6.0-alpha.1

Released 2023-Jul-12

* **Experimental (pre-release builds only):** Added
  `LoggerProviderBuilder.AddInMemoryExporter` registration extension.
  ([#4584](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4584),
  [#4735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4735))

## 1.5.1

Released 2023-Jun-26

## 1.5.0

Released 2023-Jun-05

## 1.5.0-rc.1

Released 2023-May-25

## 1.5.0-alpha.2

Released 2023-Mar-31

* Fixed issue where the `MetricSnapshot` of a histogram did not capture the min
  and max values.
  ([#4306](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4306))

## 1.5.0-alpha.1

Released 2023-Mar-07

## 1.4.0

Released 2023-Feb-24

* Updated OTel SDK dependency to 1.4.0

## 1.4.0-rc.4

Released 2023-Feb-10

## 1.4.0-rc.3

Released 2023-Feb-01

## 1.4.0-rc.2

Released 2023-Jan-09

## 1.4.0-rc.1

Released 2022-Dec-12

## 1.4.0-beta.3

Released 2022-Nov-07

## 1.4.0-beta.2

Released 2022-Oct-17

## 1.4.0-beta.1

Released 2022-Sep-29

* Changed error handling, `InMemoryExporter` will now throw
  `ObjectDisposedException` if `Export` is invoked after the exporter is
  disposed.
  ([#3607](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3607))

* Added overloads which accept a name to the `MeterProviderBuilder`
  `AddInMemoryExporter` extension to allow for more fine-grained options
  management
  ([#3648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3648))

## 1.4.0-alpha.2

Released 2022-Aug-18

## 1.4.0-alpha.1

Released 2022-Aug-02

* `InMemoryExporter` will now buffer scopes when exporting `LogRecord`
  ([#3360](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3360))

* The `MetricReaderOptions` defaults can be overridden using
  `OTEL_METRIC_EXPORT_INTERVAL` and `OTEL_METRIC_EXPORT_TIMEOUT`
  environmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.12.0/specification/sdk-environment-variables.md#periodic-exporting-metricreader).
  ([#3424](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3424))

## 1.3.0

Released 2022-Jun-03

## 1.3.0-rc.2

Released 2022-June-1

* Adds new `AddInMemoryExporter` extension method to export `Metric` as new
  type `MetricSnapshot`.
  ([#2361](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2361))

## 1.3.0-beta.2

Released 2022-May-16

## 1.3.0-beta.1

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework
  version supported is .NET 4.6.2. ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.2.0-rc5

Released 2022-Apr-12

## 1.2.0-rc4

Released 2022-Mar-30

## 1.2.0-rc3

Released 2022-Mar-04

* Adds the ability to configure `MetricReaderOptions` via the
  `AddInMemoryExporter` extension method.
  ([#2931](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2931))

## 1.2.0-rc2

Released 2022-Feb-02

## 1.2.0-rc1

Released 2021-Nov-29

## 1.2.0-beta2

Released 2021-Nov-19

## 1.2.0-beta1

Released 2021-Oct-08

## 1.2.0-alpha4

Released 2021-Sep-23

## 1.2.0-alpha3

Released 2021-Sep-13

## 1.2.0-alpha2

Released 2021-Aug-24

* Add Metrics
  support.([#2192](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2192))

## 1.2.0-alpha1

Released 2021-Jul-23

* Removes support for .NET Framework 4.5.2 and 4.6. The minimum .NET Framework
  version supported is .NET 4.6.1.
  ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

## 1.1.0

Released 2021-Jul-12

* Supports OpenTelemetry.Extensions.Hosting based configuration for
  `InMemoryExporter`
  ([#2129](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2129))

## 1.1.0-rc1

Released 2021-Jun-25

## 1.1.0-beta4

Released 2021-Jun-09

## 1.1.0-beta3

Released 2021-May-11

## 1.1.0-beta2

Released 2021-Apr-23

## 1.1.0-beta1

Released 2021-Mar-19

## 1.0.1

Released 2021-Feb-10

## 1.0.0-rc4

Released 2021-Feb-09

## 1.0.0-rc3

Released 2021-Feb-04

## 1.0.0-rc2

Released 2021-Jan-29

* `AddInMemoryExporter` extension method for traces moved from `OpenTelemetry`
  namespace to `OpenTelemetry.Trace` namespace.
  ([#1576](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1576))
* `AddInMemoryExporter` extension method for logs moved from
  `Microsoft.Extensions.Logging` namespace to `OpenTelemetry.Logs` namespace.
  ([#1576](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1576))

## 1.0.0-rc1.1

Released 2020-Nov-17

* Updated AddInMemoryExporter extension methods for TracerProviderBuilder and
  OpenTelemetryLoggerOptions
  ([#1514](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1514))

## 0.8.0-beta.1

Released 2020-Nov-5

## 0.7.0-beta.1

Released 2020-Oct-16

* Initial release
