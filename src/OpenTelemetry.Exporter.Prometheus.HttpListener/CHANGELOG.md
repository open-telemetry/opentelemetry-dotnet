# Changelog

This file contains individual changes for the
OpenTelemetry.Exporter.Prometheus.HttpListener package. For highlights and
announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

* **Breaking Change** When targeting `net8.0`, the package now depends on version
  `8.0.0` of the `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Diagnostics.Abstractions` and
  `Microsoft.Extensions.Logging.Configuration` NuGet packages.
  ([#6327](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6327))

* Add support for .NET 10.0.
  ([#6307](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6307))

* Added the possibility to disable timestamps via the `PrometheusHttpListenerOptions`.
  ([#6600](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6600))

* **Breaking Change** NuGet packages now use the Sigstore bundle format
  (`.sigstore.json`) for digital signatures instead of separate signature
  (`.sig`) and certificate (`.pem`) files. This requires cosign 3.0 or later
  for verification. See the [Digital signing
  section](../../README.md#digital-signing) for updated verification instructions.
  ([#6623](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6623))

* Update to stable versions for .NET 10.0 NuGet packages.
  ([#6667](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6667))

## 1.13.1-beta.1

Released 2025-Oct-10

* Updated OpenTelemetry core component version(s) to `1.13.1`.
  ([#6598](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6598))

## 1.13.0-beta.1

Released 2025-Oct-01

* Updated OpenTelemetry core component version(s) to `1.13.0`.
  ([#6552](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6552))

## 1.12.0-beta.1

Released 2025-May-06

* Updated OpenTelemetry core component version(s) to `1.12.0`.
  ([#6269](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6269))

## 1.11.2-beta.1

Released 2025-Mar-05

* Updated OpenTelemetry core component version(s) to `1.11.2`.
  ([#6169](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6169))

## 1.11.0-beta.1

Released 2025-Jan-16

* Updated OpenTelemetry core component version(s) to `1.11.0`.
  ([#6064](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6064))

## 1.10.0-beta.1

Released 2024-Nov-12

* Added meter-level tags to Prometheus exporter
  ([#5837](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5837))

* Updated OpenTelemetry core component version(s) to `1.10.0`.
  ([#5970](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5970))

## 1.9.0-beta.2

Released 2024-Jun-24

* Fixed a bug which lead to empty responses when the internal buffer is resized
  processing a collection request
  ([#5676](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5676))

## 1.9.0-beta.1

Released 2024-Jun-14

## 1.9.0-alpha.2

Released 2024-May-29

* Fixed issue with OpenMetrics suffixes for Prometheus
  ([#5646](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5646))

## 1.9.0-alpha.1

Released 2024-May-20

* Fixed an issue with corrupted buffers when reading both OpenMetrics and
  plain text formats from Prometheus exporters.
  ([#5623](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5623))

## 1.8.0-rc.1

Released 2024-Mar-27

* Fix serializing scope_info when buffer overflows
  ([#5407](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5407))

* Add `target_info` to Prometheus exporters when using OpenMetrics
  ([#5407](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5407))

## 1.8.0-beta.1

Released 2024-Mar-14

* Added option to disable _total suffix addition to counter metrics
  ([#5305](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5305))

* Export OpenMetrics format from Prometheus exporters
  ([#5107](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5107))

* For requests with OpenMetrics format, scope info is automatically added
  ([#5086](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5086)
  [#5182](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5182))

* **Breaking change** Updated the `PrometheusHttpListener` to throw an exception
  if it can't be started.
  ([#5304](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5304))

## 1.7.0-rc.1

Released 2023-Nov-29

## 1.7.0-alpha.1

Released 2023-Oct-16

* Fixed writing boolean values to use the JSON representation
  ([#4823](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4823))

## 1.6.0-rc.1

Released 2023-Aug-21

* Added support for unit and name conversion following the [OpenTelemetry Specification](https://github.com/open-telemetry/opentelemetry-specification/blob/065b25024549120800da7cda6ccd9717658ff0df/specification/compatibility/prometheus_and_openmetrics.md?plain=1#L235-L240)
  ([#4753](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4753))

## 1.6.0-alpha.1

Released 2023-Jul-12

## 1.5.0-rc.1

Released 2023-May-25

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
  specification](https://github.com/prometheus/OpenMetrics/blob/v1.0.0/specification/OpenMetrics.md)
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
