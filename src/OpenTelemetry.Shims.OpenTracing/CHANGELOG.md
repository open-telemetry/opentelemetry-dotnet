# Changelog

This file contains individual changes for the OpenTelemetry.Shims.OpenTracing
package. For highlights and announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

* Updated OpenTelemetry core component version(s) to `1.11.2`.
  ([#6169](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6169))

## 1.11.0-beta.1

Released 2025-Jan-16

* Updated OpenTelemetry core component version(s) to `1.11.0`.
  ([#6064](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6064))

## 1.10.0-beta.1

Released 2024-Nov-12

* Fixed an issue causing all tag values added via the `ISpanBuilder` API to be
  converted to strings on the `ISpan` started from the builder.
  ([#5797](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5797))

* Updated OpenTelemetry core component version(s) to `1.10.0`.
  ([#5970](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5970))

## 1.9.0-beta.2

Released 2024-Jun-24

## 1.9.0-beta.1

Released 2024-Jun-14

## 1.9.0-alpha.2

Released 2024-May-29

## 1.9.0-alpha.1

Released 2024-May-20

## 1.7.0-beta.1

Released 2023-Dec-08

* Remove obsolete `TracerShim(Tracer, TextMapPropagator)` constructor.
  Use `TracerShim(TracerProvider)`
  or `TracerShim(TracerProvider, TextMapPropagator)` constructors.
  ([#4862](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4862))

## 1.6.0-beta.1

Released 2023-Sep-05

* Fix: Do not raise `ArgumentException` if `Activity` behind the shim span
  has an invalid context.
  ([#2787](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2787))

* Obsolete `TracerShim(Tracer, TextMapPropagator)` constructor.
  Provide `TracerShim(TracerProvider)`
  and `TracerShim(TracerProvider, TextMapPropagator)` constructors.
  ([#4812](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4812))

## 1.5.0-beta.1

Released 2023-Jun-05

* Bumped the package version to `1.5.0-beta.1` to keep its major and minor
  version in sync with that of the core packages. This would make it more
  intuitive for users to figure out what version of core packages would work
  with a given version of this package. The pre-release identifier has also been
  changed from `rc` to `beta` as we believe this more accurately reflects the
  status of this package. We believe the `rc` identifier will be more
  appropriate as semantic conventions reach stability.

## 1.0.0-rc9.14

Released 2023-Feb-24

* Updated OTel API dependency to 1.4.0

## 1.4.0-rc9.13

Released 2023-Feb-10

## 1.0.0-rc9.12

Released 2023-Feb-01

## 1.0.0-rc9.11

Released 2023-Jan-09

## 1.0.0-rc9.10

Released 2022-Dec-12

## 1.0.0-rc9.9

Released 2022-Nov-07

## 1.0.0-rc9.8

Released 2022-Oct-17

## 1.0.0-rc9.7

Released 2022-Sep-29

## 1.0.0-rc9.6

Released 2022-Aug-18

## 1.0.0-rc9.5

Released 2022-Aug-02

* Fix: Handling of OpenTracing spans when used in conjunction
  with legacy "Microsoft.AspNetCore.Hosting.HttpRequestIn" activities.
  ([#3509](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3506))

## 1.0.0-rc9.4

Released 2022-Jun-03

* Removes .NET Framework 4.6.1. The minimum .NET Framework version supported is
  .NET 4.6.2.
  ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.0.0-rc9.3

Released 2022-Apr-15

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

* Removes .NET Framework 4.5.2 support. The minimum .NET Framework version
  supported is .NET 4.6.1.
  ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

## 1.0.0-rc7

Released 2021-Jul-12

## 1.0.0-rc6

Released 2021-Jun-25

## 1.0.0-rc5

Released 2021-Jun-09

## 1.0.0-rc3

Released 2021-Mar-19

## 1.0.0-rc2

Released 2021-Jan-29

* Made the following shim classes internal: `ScopeManagerShim`,
  `SpanBuilderShim`, `SpanContextShim`, `SpanShim`.
  ([#1619](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1619))

## 1.0.0-rc1.1

Released 2020-Nov-17

## 0.8.0-beta.1

Released 2020-Nov-5

* Renamed TextMapPropagator to TraceContextPropagator, CompositePropagator to
  CompositeTextMapPropagator. IPropagator is renamed to TextMapPropagator and
  changed from interface to abstract class.
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1427))

## 0.7.0-beta.1

Released 2020-Oct-16

## 0.6.0-beta.1

Released 2020-Sep-15

## 0.5.0-beta.2

Released 2020-08-28

* Renamed `ITextPropagator` to `IPropagator`
  ([#1190](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1190))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
