# Changelog

## Unreleased

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

* Bug fix for Prometheus Exporter reporting StatusCode 204
  instead of 200, when no metrics are collected
  ([#3643](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3643))
* Added overloads which accept a name to the `MeterProviderBuilder`
  `AddPrometheusHttpListener` extension to allow for more fine-grained options
  management
  ([#3648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3648))
* Added support for OpenMetrics UNIT metadata
  ([#3651](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3651))
* Added `"# EOF\n"` ending following the [OpenMetrics
  specification](https://github.com/OpenObservability/OpenMetrics/blob/main/specification/OpenMetrics.md)
  ([#3654](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3654))

## 1.4.0-alpha.2

Released 2022-Aug-18

* Split up Prometheus projects based on its hosting mechanism,
  HttpListener and AspNetCore, into their own projects
  and assemblies.
  ([#3430](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3430)
  [#3503](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3503)
  [#3507](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3507))
* Fixed bug
  [#2840](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2840) by
  allowing `+` and `*` to be used in the URI prefixes (e.g. `"http://*:9184"`).
  ([#3521](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3521))

## 1.3.0-rc.2

Released 2022-June-1

## 1.3.0-beta.2

Released 2022-May-16

## 1.3.0-beta.1

Released 2022-Apr-15

* Added `IApplicationBuilder` extension methods to help with Prometheus
  middleware configuration on ASP.NET Core
  ([#3029](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3029))
* Changed Prometheus exporter to return 204 No Content and log a warning event
  if there are no metrics to collect.
* Removes .NET Framework 4.6.1. The minimum .NET Framework
  version supported is .NET 4.6.2. ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.2.0-rc5

Released 2022-Apr-12

## 1.2.0-rc4

Released 2022-Mar-30

## 1.2.0-rc3

Released 2022-Mar-04

## 1.2.0-rc2

Released 2022-Feb-02

* Update default `httpListenerPrefixes` for PrometheusExporter to be `http://localhost:9464/`.
([#2783](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2783))

## 1.2.0-rc1

Released 2021-Nov-29

* Bug fix for handling Histogram with empty buckets.
  ([#2651](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2651))

## 1.2.0-beta2

Released 2021-Nov-19

* Added scrape endpoint response caching feature &
  `ScrapeResponseCacheDurationMilliseconds` option
  ([#2610](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2610))

## 1.2.0-beta1

Released 2021-Oct-08

## 1.2.0-alpha4

Released 2021-Sep-23

## 1.2.0-alpha3

Released 2021-Sep-13

* Bug fixes
  ([#2289](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2289)
  [#2309](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2309))

## 1.2.0-alpha2

Released 2021-Aug-24

* Revamped to support the new Metrics API/SDK.
  Supports Counter, Gauge and Histogram.

## 1.0.0-rc1.1

Released 2020-Nov-17

* Initial release
