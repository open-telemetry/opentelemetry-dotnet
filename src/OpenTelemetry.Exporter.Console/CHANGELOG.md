# Changelog

## Unreleased

* Added StatusCode, StatusDescription support to
  `ConsoleActivityExporter`.
  ([#2929](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2929)
   [#3061](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3061))

## 1.2.0-rc3

Released 2022-Mar-04

* Removes metric related configuration options from `ConsoleExporterOptions`.
  `MetricReaderType`, `PeriodicExporterMetricReaderOptions`, and `Temporality`
  are now configurable via the `MetricReaderOptions`.
  ([#2929](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2929))

## 1.2.0-rc2

Released 2022-Feb-02

Fix MetricExporter to respect Console and Debug flags.
Added `Activity.Links` support to `ConsoleActivityExporter`.

## 1.2.0-rc1

Released 2021-Nov-29

* Added configuration options for `MetricReaderType` to allow for configuring
  the `ConsoleMetricExporter` to export either manually or periodically.
  ([#2648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2648))

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

* Add Histogram Metrics support.
* Changed default temporality to be cumulative.

## 1.2.0-alpha1

Released 2021-Jul-23

* Removes .NET Framework 4.5.2, .NET 4.6 support. The minimum .NET Framework
  version supported is .NET 4.6.1. ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))
* Add Metrics support.([#2174](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2174))

## 1.1.0

Released 2021-Jul-12

* Supports OpenTelemetry.Extensions.Hosting based configuration for
  of `ConsoleExporterOptions`.

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

* Removed code that prints Baggage information
  ([#1825](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1825))
* LogRecordExporter exports Message, Scope, StateValues from LogRecord.
  ([#1871](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1871)
  [#1895](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1895))
* Added Resource support.
  ([#1913](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1913))

## 1.0.1

Released 2021-Feb-10

## 1.0.0-rc4

Released 2021-Feb-09

## 1.0.0-rc3

Released 2021-Feb-04

* Moved `ConsoleActivityExporter` and `ConsoleLogRecordExporter` classes to
  `OpenTelemetry.Exporter` namespace.
  ([#1770](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1770))

## 1.0.0-rc2

Released 2021-Jan-29

* `AddConsoleExporter` extension method for logs moved from
  `OpenTelemetry.Trace` namespace to `OpenTelemetry.Logs` namespace.
  ([#1576](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1576))

* Added `ConsoleActivityExporter` and `ConsoleLogExporter`. Refactored
  `ConsoleExporter` to get rid of type specific check in the class
  ([#1593](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1593))

* Replaced Debug.WriteLine with Trace.WriteLine to display the logs to the Debug
  window with Release configuration
  ([#1719](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1719))

## 1.0.0-rc1.1

Released 2020-Nov-17

## 0.8.0-beta.1

Released 2020-Nov-5

* Add extension method to add `ConsoleExporter` for logs
  ([#1452](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1452))

* Generalized `ConsoleExporter` to add support for logs
  ([#1438](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1438))

## 0.7.0-beta.1

Released 2020-Oct-16

## 0.6.0-beta.1

Released 2020-Sep-15

## 0.5.0-beta.2

Released 2020-08-28

* Changed `UseConsoleExporter` to `AddConsoleExporter`, improved readability
  ([#1051](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1051))

## 0.4.0-beta.2

Released 2020-07-24

## 0.3.0-beta

Released 2020-07-23

* Initial release
