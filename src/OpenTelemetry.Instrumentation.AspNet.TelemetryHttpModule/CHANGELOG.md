# Changelog

## Unreleased

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

* Adopted the donation
  [Microsoft.AspNet.TelemetryCorrelation](https://github.com/aspnet/Microsoft.AspNet.TelemetryCorrelation)
  from [.NET Foundation](https://dotnetfoundation.org/)
  ([#2223](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2223))

* Renamed the module, refactored existing code
  ([#2224](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2224)
  [#2225](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2225)
  [#2226](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2226)
  [#2229](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2229)
  [#2231](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2231)
  [#2235](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2235)
  [#2238](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2238)
  [#2240](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2240))

* Updated to use
  [ActivitySource](https://docs.microsoft.com/dotnet/api/system.diagnostics.activitysource)
  & OpenTelemetry.API
  ([#2249](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2249) &
  follow-ups (linked to #2249))

* TelemetryHttpModule will now restore Baggage on .NET 4.7.1+ runtimes when IIS
  switches threads
  ([#2314](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2314))
