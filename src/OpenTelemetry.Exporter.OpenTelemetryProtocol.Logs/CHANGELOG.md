# Changelog

## Unreleased-Logs

* Added overloads which accept a name to the `LoggerProviderBuilder`
  `AddOtlpExporter` extension to allow for more fine-grained options
  management
  ([#3707](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3707))

## Unreleased

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
