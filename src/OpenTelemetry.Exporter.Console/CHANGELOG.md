# Changelog

## Unreleased

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
