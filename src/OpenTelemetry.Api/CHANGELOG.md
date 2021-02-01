# Changelog

## Unreleased

## 1.0.0-rc2

Released 2021-Jan-29

* In order to align with the
  [spec](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status)
  the `Status` (otel.status_code) tag (added on `Activity` using the `SetStatus`
  extension) will now be set as the `UNSET`, `OK`, or `ERROR` string
  representation instead of the `0`, `1`, or `2` integer representation.
  ([#1579](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1579) &
  [#1620](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1620))
* Metrics API/SDK support is in an experimental state and is not recommended for
  production use. All metric APIs have been marked with the `Obsolete`
  attribute. See
  [#1501](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501)
  for more information.
  ([#1611](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1611))
* `Status.WithDescription` will now ignore the provided description if the
  `Status.StatusCode` is anything other than `ERROR`.
  ([#1655](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1655))
* Metrics removed as it is not part 1.0.0 release. See issue
  [#1501](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1655)
  for details on Metric release plans.
* Relax System.Diagnostics.DiagnosticSource version requirement to allow
  versions >=5.0. Previously only versions up to 6.0 (excluding 6.0) was
  allowed.

## 1.0.0-rc1.1

Released 2020-Nov-17

* Updated System.Diagnostics.DiagnosticSource to version 5.0.0
* Mark Activity extension methods as internal as these are not required to be
  public. GetTagValue, EnumerateTags, EnumerateLinks, EnumerateEvents. See
  [#1544](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1544)
  for full changes.
* Changed SpanHelper class from public to internal. Moved SpanHelper.cs to
  OpenTelemetry.Api\Internal
  ([#1555](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1555))

## 0.8.0-beta.1

Released 2020-Nov-5

* Removed `IsValid` property from `Status`
  ([#1415](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1415))
* Removed `IsOk` property from `Status` and fixed `StatusCode` enum values
  ([#1414](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1414))
* `B3Propagator` now supports the value `true` to be passed in for the header
  `X-B3-Sampled`.
  ([#1413](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1413))
* Moving grpc status and helper to grpc project
  ([#1422](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1422))
* Renamed TextMapPropagator to TraceContextPropagator, CompositePropapagor to
  CompositeTextMapPropagator. IPropagator is renamed to TextMapPropagator and
  changed from interface to abstract class.
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1427))
* Added GlobalPropagators API via Propagators.DefaultTextMapPropagator.
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1428))
* Changed SpanAttributeConstants from public to internal
  ([#1457](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1457))

## 0.7.0-beta.1

Released 2020-Oct-16

* `IActivityTagEnumerator` is now `IActivityEnumerator<T>`. Added
  `EnumerateLinks` extension method on `Activity` for retrieving links
  efficiently
  ([#1314](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1314))
* Added `EnumerateEvents` extension method on `Activity` for retrieving events
  efficiently
  ([#1319](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1319))
* Added `EnumerateTags` extension methods on `ActivityLink` & `ActivityEvent`
  for retrieving tags efficiently. Renamed `Activity.EnumerateTagValues` ->
  `Activity.EnumerateTags`.
  ([#1320](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1320))
* Updated System.Diagnostics.DiagnosticSource to version 5.0.0-rc.2.20475.5
  ([#1346](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1346))
* Updated Span Status as per new spec
  ([#1313](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1313))

## 0.6.0-beta.1

Released 2020-Sep-15

* Updated System.Diagnostics.DiagnosticSource to version 5.0.0-rc.1.20451.14
  ([#1265](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1265))
* Added `GetTagValue` extension method on `Activity` for retrieving tag values
  efficiently
  ([#1221](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1221))
* Added `EnumerateTagValues` extension method on `Activity` for enumerating tag
  values efficiently
  ([#1236](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1236))

## 0.5.0-beta.2

Released 2020-08-28

* `Link` and `TelemetrySpan` are using `SpanAttributes` instead of
  `ActivityTagsCollection` or `Dictionary`
  ([#1120](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1120))
* Added `RecordException` in `TelemetrySpan`
  ([#1116](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1116))
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
* Updated System.Diagnostics.DiagnosticSource to version
  5.0.0-preview.8.20407.11.
* Removed `CorrelationContext` and added `Baggage`, an implementation of the
  [`Baggage
  API`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/baggage/api.md)
  spec
  ([#1106](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1106))
* Renamed `TraceContextFormat` to `TextMapPropagator`, `BaggageFormat` to
  `BaggagePropagator`, and `B3Format` to `B3Propagator`
  ([#1175](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1175))
* Renamed `ITextPropagator` to `IPropagator`
  ([#1190](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1190))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
