# Changelog

## Unreleased

## 1.0.0-rc9.4

Released 2022-Jun-03

* Add `netstandard2.0` target enabling the Grpc.Net.Client instrumentation to
  be consumed by .NET Framework applications.
  ([#3105](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3105))

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

## 1.0.0-rc7

Released 2021-Jul-12

## 1.0.0-rc6

Released 2021-Jun-25

## 1.0.0-rc5

Released 2021-Jun-09

## 1.0.0-rc4

Released 2021-Apr-23

## 1.0.0-rc3

Released 2021-Mar-19

* Leverages added AddLegacySource API from OpenTelemetry SDK to trigger Samplers
  and ActivityProcessors. Samplers, ActivityProcessor.OnStart will now get the
  Activity before any enrichment done by the instrumentation.
  ([#1836](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1836))
* Performance optimization by leveraging sampling decision and short circuiting
  activity enrichment.
  ([#1903](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1904))

## 1.0.0-rc2

Released 2021-Jan-29

## 1.0.0-rc1.1

Released 2020-Nov-17

* Add context propagation, when SuppressDownstreamInstrumentation is enabled.
  [#1464](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1464)
* GrpcNetClientInstrumentation sets ActivitySource to activities created outside
  ActivitySource.
  ([#1515](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1515/))

## 0.8.0-beta.1

Released 2020-Nov-5

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

* The `grpc.method` and `grpc.status_code` attributes added by the library are
  removed from the span. The information from these attributes is contained in
  other attributes that follow the conventions of OpenTelemetry.
  ([#1260](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1260)).

## 0.5.0-beta.2

Released 2020-08-28

* NuGet package renamed to OpenTelemetry.Instrumentation.GrpcNetClient to more
  clearly indicate that this package is specifically for gRPC client
  instrumentation. The package was previously named
  OpenTelemetry.Instrumentation.Grpc.
  ([#1136](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1136))
* Grpc.Net.Client Instrumentation automatically populates HttpRequest in
  Activity custom property
  ([#1099](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1099))
  ([#1128](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1128))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
