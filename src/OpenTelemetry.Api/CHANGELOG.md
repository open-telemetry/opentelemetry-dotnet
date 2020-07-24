# Changelog

## Unreleased

* Introduced `RuntimeContext` API
  ([#948](https://github.com/open-telemetry/opentelemetry-dotnet/pull/948))
* `ITextFormatActivity` got replaced by `ITextFormat` with an additional method
  to be implemented (`IsInjected`)
* Added `CompositePropagator` that accepts a list of `ITextFormat` following
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/context/api-propagators.md#create-a-composite-propagator)

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
