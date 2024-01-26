# Changelog

## Unreleased

* **Breaking Change** :
  [SuppressDownstreamInstrumentation](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.GrpcNetClient#suppressdownstreaminstrumentation)
  option will no longer be supported when used with certain versions of
  `OpenTelemetry.Instrumentation.Http` package. Check out this
  [issue](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5092)
  for details and workaround.
  ([#5077](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5077))
* Removed `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable support. The
  library will now emit the
  [stable](https://github.com/open-telemetry/semantic-conventions/tree/v1.23.0/docs/http)
  HTTP semantic conventions.
  ([#5259](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5259))

## 1.6.0-beta.3

Released 2023-Nov-17

## 1.6.0-beta.2

Released 2023-Oct-26

## 1.5.1-beta.1

Released 2023-Jul-20

* The new network semantic conventions can be opted in to by setting
  the `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable. This allows for a
  transition period for users to experiment with the new semantic conventions
  and adapt as necessary. The environment variable supports the following
  values:
  * `http` - emit the new, frozen (proposed for stable) networking
  attributes, and stop emitting the old experimental networking
  attributes that the instrumentation emitted previously.
  * `http/dup` - emit both the old and the frozen (proposed for stable)
  networking attributes, allowing for a more seamless transition.
  * The default behavior (in the absence of one of these values) is to continue
  emitting the same network semantic conventions that were emitted in
  `1.5.0-beta.1`.
  * Note: this option will eventually be removed after the new
  network semantic conventions are marked stable. Refer to the
  specification for more information regarding the new network
  semantic conventions for
  [spans](https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/rpc/rpc-spans.md).
  ([#4658](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4658))

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

* Updated OTel SDK dependency to 1.4.0

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

 **Breaking change** The `Enrich` callback option has been removed. For better
  usability, it has been replaced by two separate options:
  `EnrichWithHttpRequestMessage`and `EnrichWithHttpResponseMessage`. Previously,
  the single `Enrich` callback required the consumer to detect which event
  triggered the callback to be invoked (e.g., request start or response end) and
  then cast the object received to the appropriate type: `HttpRequestMessage`
  and `HttpResponseMessage`. The separate callbacks make it clear what event
  triggers them and there is no longer the need to cast the argument to the
  expected type.
  ([#3804](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3804))

## 1.0.0-rc9.8

Released 2022-Oct-17

## 1.0.0-rc9.7

Released 2022-Sep-29

* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddGrpcClientInstrumentation` extension to allow for more fine-grained
  options management
  ([#3665](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3665))

## 1.0.0-rc9.6

Released 2022-Aug-18

* Updated to use Activity native support from `System.Diagnostics.DiagnosticSource`
  to set activity status.
  ([#3118](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3118))
  ([#3569](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3569))

## 1.0.0-rc9.5

Released 2022-Aug-02

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
  ([#1260](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1260))

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
