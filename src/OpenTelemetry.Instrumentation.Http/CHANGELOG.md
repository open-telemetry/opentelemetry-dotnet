# Changelog

## Unreleased

* Added `net.peer.name` and `net.peer.port` as dimensions on
  `http.client.duration` metric.
  ([#3907](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3907))

* **Breaking change** `http.host` will no longer be populated on activity.
  `net.peer.name` and `net.peer.port` attributes will be populated instead.
  ([#3832](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3832))

## 1.0.0-rc9.9

Released 2022-Nov-07

* Added back `netstandard2.0` target.
  ([#3787](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3787))

* **Breaking change**: The `Enrich` callback option has been removed. For better
  usability, it has been replaced by three separate options: In case of
  `HttpClient` the new options are `EnrichWithHttpRequestMessage`,
  `EnrichWithHttpResponseMessage` and `EnrichWithException` and in case of
  `HttpWebRequest` the new options are `EnrichWithHttpWebRequest`,
  `EnrichWithHttpWebResponse` and `EnrichWithException`. Previously, the single
  `Enrich` callback required the consumer to detect which event triggered the
  callback to be invoked (e.g., request start, response end, or an exception)
  and then cast the object received to the appropriate type:
  `HttpRequestMessage`, `HttpResponsemessage`, or `Exception` in case of
  `HttpClient` and `HttpWebRequest`,`HttpWebResponse` and `Exception` in case of
  `HttpWebRequest`. The separate callbacks make it clear what event triggers
  them and there is no longer the need to cast the argument to the expected
  type.
  ([#3792](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3792))

* Fixed an issue which prevented custom propagators from being called on .NET 7+
  runtimes for non-sampled outgoing `HttpClient` spans.
  ([#3828](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3828))

* **Breaking change**: The same API is now exposed for `net462` and
  `netstandard2.0` targets. The `Filter` property on options is now exposed as
  `FilterHttpRequestMessage` (called for .NET & .NET Core) and
  `FilterHttpWebRequest` (called for .NET Framework).
  ([#3793](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3793))

## 1.0.0-rc9.8

Released 2022-Oct-17

* In case of .NET Core, additional spans created during retries will now be
exported.
([[#3729](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3729)])

## 1.0.0-rc9.7

Released 2022-Sep-29

* Dropped `netstandard2.0` target and added `net6.0`. .NET 5 reached EOL
  in May 2022 and .NET Core 3.1 reaches EOL in December 2022. End of support
  dates for .NET are published
  [here](https://dotnet.microsoft.com/download/dotnet).
  The instrumentation for HttpClient now requires .NET 6 or later.
  This does not affect applications targeting .NET Framework.
  ([#3664](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3664))

* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddHttpClientInstrumentation` extension to allow for more fine-grained
  options management
  ([#3664](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3664),
  [#3667](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3667))

## 1.0.0-rc9.6

Released 2022-Aug-18

* Updated to use Activity native support from `System.Diagnostics.DiagnosticSource`
  to set activity status.
  ([#3118](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3118))
  ([#3555](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3555))

* Changed activity source name from `OpenTelemetry.HttpWebRequest`
  to `OpenTelemetry.Instrumentation.Http.HttpWebRequest` for `HttpWebRequest`s
  and from `OpenTelemetry.Instrumentation.Http`
  to `OpenTelemetry.Instrumentation.Http.HttpClient` for `HttpClient`.
  ([#3515](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3515))

## 1.0.0-rc9.5

Released 2022-Aug-02

* Added `http.scheme` tag to tracing instrumentation.
  ([#3464](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3464))

* [Breaking] Removes `SetHttpFlavor` option. "http.flavor" is
  now always automatically populated.
  To remove this tag, set "http.flavor" to null using `ActivityProcessor`.
  ([#3380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3380))

* Fix `Enrich` not getting invoked when SocketException due to HostNotFound
  occurs.
  ([#3407](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3407))

## 1.0.0-rc9.4

Released 2022-Jun-03

## 1.0.0-rc9.3

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework
  version supported is .NET 4.6.2. ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.0.0-rc9.2

Released 2022-Apr-12

## 1.0.0-rc9.1

Released 2022-Mar-30

* Updated `TracerProviderBuilderExtensions.AddHttpClientInstrumentation` to support
  `IDeferredTracerProviderBuilder` and `IOptions<HttpClientInstrumentationOptions>`
  ([#3051](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3051))

## 1.0.0-rc10 (broken. use 1.0.0-rc9.1 and newer)

Released 2022-Mar-04

## 1.0.0-rc9

Released 2022-Feb-02

* Fixed an issue with `Filter` and `Enrich` callbacks not firing under certain
  conditions when gRPC is used
  ([#2698](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2698))

## 1.0.0-rc8

Released 2021-Oct-08

* Removes .NET Framework 4.5.2 support. The minimum .NET Framework
  version supported is .NET 4.6.1. ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

* `HttpClient` instances created before `AddHttpClientInstrumentation` is called
  on .NET Framework will now also be instrumented
  ([#2364](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2364))

## 1.0.0-rc7

Released 2021-Jul-12

## 1.0.0-rc6

Released 2021-Jun-25

## 1.0.0-rc5

Released 2021-Jun-09

## 1.0.0-rc4

Released 2021-Apr-23

* Sanitize `http.url` attribute.
  ([#1961](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1961))
* Added `RecordException` to HttpClientInstrumentationOptions and
  HttpWebRequestInstrumentationOptions which allows Exception to be reported as
  ActivityEvent.
* Update `AddHttpClientInstrumentation` extension method for .NET Framework to
  use only use `HttpWebRequestInstrumentationOptions`
  ([#1982](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1982))

## 1.0.0-rc3

Released 2021-Mar-19

* Leverages added AddLegacySource API from OpenTelemetry SDK to trigger Samplers
  and ActivityProcessors. Samplers, ActivityProcessor.OnStart will now get the
  Activity before any enrichment done by the instrumentation.
  ([#1836](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1836))
* Performance optimization by leveraging sampling decision and short circuiting
  activity enrichment.
  ([#1894](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1894))

## 1.0.0-rc2

Released 2021-Jan-29

* `otel.status_description` tag will no longer be set to the http status
  description/reason phrase for outgoing http spans.
  ([#1579](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1579))

* Moved the DiagnosticListener filtering logic from HttpClientInstrumentation
  ctor to OnStartActivity method of HttpHandlerDiagnosticListener.cs; Updated
  the logic of OnStartActivity to inject propagation data into Headers for
  filtered out events as well.
  ([#1707](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1707))

## 1.0.0-rc1.1

Released 2020-Nov-17

* HttpInstrumentation sets ActivitySource to activities created outside
  ActivitySource.
  ([#1515](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1515/))

## 0.8.0-beta.1

Released 2020-Nov-5

* Instrumentation for `HttpWebRequest` no longer store raw objects like
  `HttpWebRequest` in Activity.CustomProperty. To enrich activity, use the
  Enrich action on the instrumentation.
  ([#1407](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1407))
* Renamed TextMapPropagator to TraceContextPropagator, CompositePropagator to
  CompositeTextMapPropagator. IPropagator is renamed to TextMapPropagator and
  changed from interface to abstract class.
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1427))
* Propagators.DefaultTextMapPropagator will be used as the default Propagator
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1428))
* Removed Propagator from Instrumentation Options. Instrumentation now always
  respect the Propagator.DefaultTextMapPropagator.
  ([#1448](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1448))

## 0.7.0-beta.1

Released 2020-Oct-16

* Instrumentation no longer store raw objects like `HttpRequestMessage` in
  Activity.CustomProperty. To enrich activity, use the Enrich action on the
  instrumentation.
  ([#1261](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1261))
* Span Status is populated as per new spec
  ([#1313](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1313))

## 0.6.0-beta.1

Released 2020-Sep-15

## 0.5.0-beta.2

Released 2020-08-28

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
