# Changelog

## Unreleased

* `PropagationContext` is now used instead of `ActivityContext` in the
    `ITextFormat` API
    ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048))
* Added `BaggageFormat` an `ITextFormat` implementation for managing Baggage
    propagation via the [W3C
    Baggage](https://github.com/w3c/baggage/blob/master/baggage/HTTP_HEADER_FORMAT.md)
    header
    ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048))
* Removed `DistributedContext` as it is no longer part of the spec
  ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048)))
* Renaming from `ot` to `otel`
  ([#1046](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1046))
* Added `RuntimeContext` API
  ([#948](https://github.com/open-telemetry/opentelemetry-dotnet/pull/948))
* Changed `Link` constructor to accept `ActivityTagsCollection` instead of
  `IDictionary<string, object>` attributes
  ([#954](https://github.com/open-telemetry/opentelemetry-dotnet/pull/954))
* Added more `TelemetrySpan.SetAttribute` overloads with value of type bool,
  int, double (string already existed)
  ([#954](https://github.com/open-telemetry/opentelemetry-dotnet/pull/954))
* Changed `TelemetrySpan.SetAttribute` to match the spec
  ([#954](https://github.com/open-telemetry/opentelemetry-dotnet/pull/954))
  * Setting an attribute with an existing key now results in overwriting it
  * Setting null value has no impact except if null is set to an existing key,
    it gets removed
* Changed `HttpStatusCode` in all spans attribute (http.status_code) to use int
  value
  ([#998](https://github.com/open-telemetry/opentelemetry-dotnet/pull/998))
* Added `CompositePropagator` which accepts a list of `ITextFormat` to match the
  spec ([#923](https://github.com/open-telemetry/opentelemetry-dotnet/pull/923))
* Replaced `ITextFormatActivity` with `ITextFormat`
  ([#923](https://github.com/open-telemetry/opentelemetry-dotnet/pull/923))
* Added `StartRootSpan` and `StartActiveSpan`
  ([#994](https://github.com/open-telemetry/opentelemetry-dotnet/pull/994))
* Changed `StartSpan` to not set the created span as Active to match the spec
  ([#994](https://github.com/open-telemetry/opentelemetry-dotnet/pull/994))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
