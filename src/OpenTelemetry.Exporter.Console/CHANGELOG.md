# Changelog

This file contains individual changes for the OpenTelemetry.Exporter.Console
package. For highlights and announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

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

* Added direct reference to `System.Text.Json` for the `net8.0` target with
  minimum version of `8.0.5` in response to
  [CVE-2024-30105](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w) &
  [CVE-2024-43485](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-43485).
  ([#5874](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5874),
  [#5891](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5891))

* Added support for Instrumentation Scope Attributes (i.e
  [ActivitySource.Tags](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource.tags))
  when writing traces to the console.
  ([#5935](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5935))

## 1.10.0-beta.1

Released 2024-Sep-30

## 1.9.0

Released 2024-Jun-14

## 1.9.0-rc.1

Released 2024-Jun-07

* The experimental APIs previously covered by `OTEL1000`
  (`LoggerProviderBuilder.AddConsoleExporter` extension) are now part of the
  public API and supported in stable builds.
  ([#5648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5648))

## 1.9.0-alpha.1

Released 2024-May-20

## 1.8.1

Released 2024-Apr-17

## 1.8.0

Released 2024-Apr-02

* Added support for `ActivitySource.Version` property.
  ([#5472](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5472))

## 1.8.0-rc.1

Released 2024-Mar-27

## 1.8.0-beta.1

Released 2024-Mar-14

## 1.7.0

Released 2023-Dec-08

## 1.7.0-rc.1

Released 2023-Nov-29

* Added support for Instrumentation Scope Attributes (i.e
  [Meter.Tags](https://learn.microsoft.com/dotnet/api/system.diagnostics.metrics.meter.tags)),
  when writing metrics to the console, fixing issue
  [#4563](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4563).
  ([#5089](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5089))

## 1.7.0-alpha.1

Released 2023-Oct-16

## 1.6.0

Released 2023-Sep-05

## 1.6.0-rc.1

Released 2023-Aug-21

## 1.6.0-alpha.1

Released 2023-Jul-12

* **Experimental (pre-release builds only):**

  * Note: See
    [#4735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4735)
    for the introduction of experimental api support.

  * Add back support for Exemplars. See
    [exemplars](../../docs/metrics/customizing-the-sdk/README.md#exemplars) for
    instructions to enable exemplars.
    ([#4553](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4553))

  * Updated to display `Severity` and `SeverityText` text instead of
    `ILogger.LogLevel` when exporting `LogRecord` instances.
    ([#4568](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4568))

  * Added `LoggerProviderBuilder.AddConsoleExporter` registration extension.
    ([#4583](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4583))

## 1.5.1

Released 2023-Jun-26

## 1.5.0

Released 2023-Jun-05

* Remove support for exporting `Exemplars`. This would be added back in the
  `1.6.*` prerelease versions right after `1.5.0` stable version is released.
  ([#4533](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4533))

## 1.5.0-rc.1

Released 2023-May-25

* Add display of bucket boundaries when using exponential histograms.
  ([#4507](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4507))

* Added direct reference to `System.Text.Encodings.Web` with minimum version of
  `4.7.2` in response to
  [CVE-2021-26701](https://github.com/dotnet/runtime/issues/49377).
  ([#4390](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4390))

* Updated `LogRecord` console output: `Body` is now shown (if set),
  `StateValues` are now written as `Attributes`, and `State` is no longer
  processed.
  ([#4334](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334))

## 1.5.0-alpha.2

Released 2023-Mar-31

## 1.5.0-alpha.1

Released 2023-Mar-07

* Added Exemplar support. See [exemplars](../../docs/metrics/customizing-the-sdk/README.md#exemplars)
  for instructions to enable exemplars.

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

* Bug fix to prevent ConsoleExporter from failing when exporting an
  `ActivityLink` without Tags.
  ([#3932](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3932))

## 1.4.0-beta.3

Released 2022-Nov-07

* Bumped the minimum required version of `System.Text.Json` to 4.7.2 in response
to [CVE-2021-26701](https://github.com/dotnet/runtime/issues/49377).
([#3789](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3789))

LogRecordExporter to print full exception details instead of just Message, when
using `ILogger` to log exception.
([#3784](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3784))

Added support to print Histogram Min, Max in MetricExporter.

## 1.4.0-beta.2

Released 2022-Oct-17

## 1.4.0-beta.1

Released 2022-Sep-29

* Changed the behavior of `ConsoleExporter`, the exporter will stop outputting
  the data if it is disposed.
  ([#3578](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3578))

* Added overloads which accept a name to the `MeterProviderBuilder`
  `AddConsoleExporter` extension to allow for more fine-grained options
  management
  ([#3648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3648))

* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddConsoleExporter` extension to allow for more fine-grained options
  management
  ([#3657](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3657))

## 1.4.0-alpha.2

Released 2022-Aug-18

## 1.4.0-alpha.1

Released 2022-Aug-02

* The `MetricReaderOptions` defaults can be overridden using
  `OTEL_METRIC_EXPORT_INTERVAL` and `OTEL_METRIC_EXPORT_TIMEOUT`
  environmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.12.0/specification/sdk-environment-variables.md#periodic-exporting-metricreader).
  ([#3424](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3424))

## 1.3.0

Released 2022-Jun-03

## 1.3.0-rc.2

Released 2022-June-1

* Improve the conversion and formatting of attribute values.
  The list of data types that must be supported per the
  [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/common#attribute)
  is more narrow than what the .NET OpenTelemetry SDK supports. Numeric
  [built-in value types](https://docs.microsoft.com/dotnet/csharp/language-reference/builtin-types/built-in-types)
  are supported by converting to a `long` or `double` as appropriate except for
  numeric types that could cause overflow (`ulong`) or rounding (`decimal`)
  which are converted to strings. Non-numeric built-in types - `string`,
  `char`, `bool` are supported. All other types are converted to a `string`.
  Array values are also supported.
  ([#3311](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3311))
* Fix conversion of array-valued attributes. They were previously
  converted to a string like "System.String[]".
  ([#3311](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3311))

## 1.3.0-beta.2

Released 2022-May-16

## 1.3.0-beta.1

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework
  version supported is .NET 4.6.2. ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.2.0

Released 2022-Apr-15

## 1.2.0-rc5

Released 2022-Apr-12

## 1.2.0-rc4

Released 2022-Mar-30

* Added StatusCode, StatusDescription support to
  `ConsoleActivityExporter`.
  ([#2929](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2929)
  [#3061](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3061))

* `AddConsoleExporter` extension method by default sets up exporter
   to export metrics every 10 seconds.

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
