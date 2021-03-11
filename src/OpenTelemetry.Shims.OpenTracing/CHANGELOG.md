# Changelog

## Unreleased

## 1.0.0-rc2

Released 2021-Jan-29

* Made the following shim classes internal: `ScopeManagerShim`,
  `SpanBuilderShim`, `SpanContextShim`, `SpanShim`.
  ([#1619](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1619))

## 1.0.0-rc1.1

Released 2020-Nov-17

## 0.8.0-beta.1

Released 2020-Nov-5

* Renamed TextMapPropagator to TraceContextPropagator, CompositePropapagor
  to CompositeTextMapPropagator. IPropagator is renamed to TextMapPropagator
  and changed from interface to abstract class.
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
