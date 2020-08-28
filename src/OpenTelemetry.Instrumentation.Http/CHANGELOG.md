# Changelog

## Unreleased

* Rename FilterFunc to Filter.

* HttpClient/HttpWebRequest instrumentation will now add the raw Request,
  Response, and/or Exception objects to the Activity it creates
  ([#1099](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1099))
* Changed the default propagation to support W3C Baggage
  ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048))
  * The default ITextFormat is now `CompositePropagator(TraceContextFormat,
    BaggageFormat)`. Outgoing Http request will now send Baggage using the [W3C
    Baggage](https://github.com/w3c/baggage/blob/master/baggage/HTTP_HEADER_FORMAT.md)
    header. Previously Baggage was sent using the `Correlation-Context` header,
    which is now outdated.
* Removed `AddHttpInstrumentation` and `AddHttpWebRequestInstrumentation` (.NET
  Framework) `TracerProviderBuilderExtensions`. `AddHttpClientInstrumentation`
  will now register `HttpClient` instrumentation on .NET Core and `HttpClient` +
  `HttpWebRequest` instrumentation on .NET Framework.
* Renamed `ITextPropagator` to `IPropagator`
  ([#1190](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1190))

## 0.3.0-beta

Released 2020-07-23

* Initial release
