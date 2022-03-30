# Changelog

* Added ASP.NET metrics instrumentation to collect `http.server.duration`.
  ([#2985](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2985))
* Fix: Http server span status is now unset for `400`-`499`.
  ([#2904](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2904))

## Unreleased

## 1.0.0-rc9.1

Released 2022-Mar-30

## 1.0.0-rc10 (broken. use 1.0.0-rc9.1 and newer)

Released 2022-Mar-04

## 1.0.0-rc9

Released 2022-Feb-02

## 1.0.0-rc8

Released 2021-Oct-08

* Removes .NET Framework 4.5.2, .NET 4.6 support. The minimum .NET Framework
  version supported is .NET 4.6.1. ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

* Replaced `http.path` tag on activity with `http.target`.
  ([#2266](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2266))

* ASP.NET instrumentation now uses
  [OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule/)
  instead of
  [Microsoft.AspNet.TelemetryCorrelation](https://www.nuget.org/packages/Microsoft.AspNet.TelemetryCorrelation/)
  to listen for incoming http requests to the process. Please see the (Step 2:
  Modify
  Web.config)[https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.AspNet#step-2-modify-webconfig]
  README section for details on the new HttpModule definition required.
  ([#2222](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2222))

* Added `RecordException` option. Specify `true` to have unhandled exception
  details automatically captured on spans.
  ([#2256](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2256))

## 1.0.0-rc7

Released 2021-Jul-12

## 1.0.0-rc6

Released 2021-Jun-25

## 1.0.0-rc5

Released 2021-Jun-09

## 1.0.0-rc4

Released 2021-Apr-23

* Sanitize `http.url` attribute. ([#1961](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1961))

## 1.0.0-rc3

Released 2021-Mar-19

* Leverages added AddLegacySource API from OpenTelemetry SDK to trigger Samplers
  and ActivityProcessors. Samplers, ActivityProcessor.OnStart will now get the
  Activity before any enrichment done by the instrumentation.
  ([#1836](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1836))
* Performance optimization by leveraging sampling decision and short circuiting
  activity enrichment.
  ([#1903](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1903))

## 1.0.0-rc2

Released 2021-Jan-29

## 1.0.0-rc1.1

Released 2020-Nov-17

* AspNetInstrumentation sets ActivitySource to activities created outside
  ActivitySource.
  ([#1515](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1515/))

## 0.8.0-beta.1

Released 2020-Nov-5

* Renamed TextMapPropagator to TraceContextPropagator, CompositePropapagor to
  CompositeTextMapPropagator. IPropagator is renamed to TextMapPropagator and
  changed from interface to abstract class.
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1427))
* Propagators.DefaultTextMapPropagator will be used as the default Propagator.
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1428))
* Removed Propagator from Instrumentation Options. Instrumentation now always
  respect the Propagator.DefaultTextMapPropagator.
  ([#1448](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1448))

## 0.7.0-beta.1

Released 2020-Oct-16

* Instrumentation no longer store raw objects like `HttpRequest` in
  Activity.CustomProperty. To enrich activity, use the Enrich action on the
  instrumentation.
  ([#1261](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1261))
* Span Status is populated as per new spec
  ([#1313](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1313))

## 0.6.0-beta.1

Released 2020-Sep-15

## 0.5.0-beta.2

Released 2020-08-28

* Added Filter public API on AspNetInstrumentationOptions to allow filtering of
  instrumentation based on HttpContext.

* Asp.Net Instrumentation automatically populates HttpRequest, HttpResponse in
  Activity custom property

* Changed the default propagation to support W3C Baggage
  ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048))
  * The default ITextFormat is now `CompositePropagator(TraceContextFormat,
    BaggageFormat)`. Baggage sent via the [W3C
    Baggage](https://github.com/w3c/baggage/blob/master/baggage/HTTP_HEADER_FORMAT.md)
    header will now be parsed and set on incoming Http spans.
* Renamed `ITextPropagator` to `IPropagator`
  ([#1190](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1190))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
