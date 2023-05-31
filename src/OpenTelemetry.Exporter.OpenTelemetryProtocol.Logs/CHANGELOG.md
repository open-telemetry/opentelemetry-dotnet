# Changelog

## Unreleased

## 1.5.0-rc.1

Released 2023-May-25

* The `OpenTelemetryLoggerOptions.AddOtlpExporter` extension no longer
  automatically sets `OpenTelemetryLoggerOptions.ParseStateValues` to `true`.
  The OpenTelemetry SDK now automatically sets `Attributes` (aka `StateValues`)
  for the common cases where `ParseStateValues` was previously required.
  `ParseStateValues` can be set to `true` manually by users to enable parsing of
  custom states which do not implement `IReadOnlyList` / `IEnumerable`
  interfaces.
  ([#4334](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334))

* Updated to use the new `LogRecord.Attributes` field as `LogRecord.StateValues`
  is now marked obsolete. There is no impact to transmitted data (`StateValues`
  and `Attributes` are equivalent).
  ([#4334](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4334))

* Fixed issue where the
  [observed time](https://github.com/open-telemetry/opentelemetry-proto/blob/395c8422fe90080314c7d9b4114d701a0c049e1f/opentelemetry/proto/logs/v1/logs.proto#L138)
  field of the OTLP log record was not set. It is now correctly set to equal
  the
  [time](https://github.com/open-telemetry/opentelemetry-proto/blob/395c8422fe90080314c7d9b4114d701a0c049e1f/opentelemetry/proto/logs/v1/logs.proto#L121)
  field.
  ([#4444](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4444))

## 1.5.0-alpha.2

Released 2023-Mar-31

## 1.5.0-alpha.1

Released 2023-Mar-07

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

* `OtlpExporterOptions` can now be bound to `IConfiguation` and
  `HttpClientFactory` may be used to manage the `HttpClient` instance used when
  `HttpProtobuf` is configured
  ([#3640](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3640))

## 1.4.0-alpha.2

Released 2022-Aug-18

## 1.4.0-alpha.1

Released 2022-Aug-02

## 1.3.0-rc.2

Released 2022-June-1

## 1.0.0-rc9.3

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework
  version supported is .NET 4.6.2. ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.0.0-rc9.2

Released 2022-Apr-12

## 1.0.0-rc9.1

Released 2022-Mar-30

## 1.0.0-rc10 (broken. use 1.0.0-rc9.1 and newer)

Released 2022-Mar-04

## 1.0.0-rc9

Released 2022-Feb-02

## 1.0.0-rc8

Released 2021-Oct-08
