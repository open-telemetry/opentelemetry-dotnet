# Changelog

## Unreleased

## 1.5.1-beta.1

Released 2023-Jul-20

* The new HTTP and network semantic conventions can be opted in to by setting
  the `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable. This allows for a
  transition period for users to experiment with the new semantic conventions
  and adapt as necessary. The environment variable supports the following
  values:
  * `http` - emit the new, frozen (proposed for stable) HTTP and networking
  attributes, and stop emitting the old experimental HTTP and networking
  attributes that the instrumentation emitted previously.
  * `http/dup` - emit both the old and the frozen (proposed for stable) HTTP
  and networking attributes, allowing for a more seamless transition.
  * The default behavior (in the absence of one of these values) is to continue
  emitting the same HTTP and network semantic conventions that were emitted in
  `1.5.0-beta.1`.
  * Note: this option will eventually be removed after the new HTTP and
  network semantic conventions are marked stable. At which time this
  instrumentation can receive a stable release, and the old HTTP and
  network semantic conventions will no longer be supported. Refer to the
  specification for more information regarding the new HTTP and network
  semantic conventions for both
  [spans](https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/http/http-spans.md)
  and
  [metrics](https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/http/http-metrics.md).
  ([#4537](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4537),
  [#4606](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4606),
  [#4660](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4660))

* Fixed an issue affecting NET 7.0+. If custom propagation is being used
  and tags are added to an Activity during sampling then that Activity would be dropped.
  ([#4637](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4637))

## 1.5.0-beta.1

Released 2023-Jun-05

* Bumped the package version to `1.5.0-beta.1` to keep its major and minor
  version in sync with that of the core packages. This would make it more
  intuitive for users to figure out what version of core packages would work
  with a given version of this package. The pre-release identifier has also been
  changed from `rc` to `beta` as we believe this more accurately reflects the
  status of this package. We believe the `rc` identifier will be more
  appropriate as semantic conventions reach stability.

* Fix issue where baggage gets cleared when the ASP.NET Core Activity
   is stopped. The instrumentation no longer clears baggage. One problem
   this caused was that it prevented Activity processors from accessing baggage
   during their `OnEnd` call.
([#4274](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4274))

* Added direct reference to `System.Text.Encodings.Web` with minimum version of
`4.7.2` due to [CVE-2021-26701](https://github.com/dotnet/runtime/issues/49377).
This impacts target frameworks `netstandard2.0` and `netstandard2.1` which has a
reference to `Microsoft.AspNetCore.Http.Abstractions` that depends on
`System.Text.Encodings.Web` >= 4.5.0.
([#4399](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4399))

* Improve perf by avoiding boxing of common status codes values.
  ([#4360](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4360),
  [#4363](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4363))

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

* **Users migrating from version `1.0.0-rc9.9` will see the following breaking
  changes:**
  * Updated `http.status_code` dimension type from string to int for
  `http.server.duration` metric.
  ([#3930](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3930))
  * `http.host` will no longer be populated on `http.server.duration` metric.
  `net.host.name` and `net.host.port` attributes will be populated instead.
([#3928](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3928))

  * The `http.server.duration` metric's `http.target` attribute is replaced with
`http.route` attribute.
([#3903](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3903))

  * `http.host` will no longer be populated on activity. `net.host.name` and
  `net.host.port` attributes will be populated instead.
  ([#3858](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3858))

* Extension method `AddAspNetCoreInstrumentation` on `MeterProviderBuilder` now
  supports `AspNetCoreMetricsInstrumentationOptions`. This option class exposes
  configuration properties for metric filtering and tag enrichment.
  ([#3948](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3948),
  [#3982](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3982))

## 1.0.0-rc9.9

Released 2022-Nov-07

* **Breaking change** The `Enrich` callback option has been removed.
  For better usability, it has been replaced by three separate options:
  `EnrichWithHttpRequest`, `EnrichWithHttpResponse` and `EnrichWithException`.
  Previously, the single `Enrich` callback required the consumer to detect
  which event triggered the callback to be invoked (e.g., request start,
  response end, or an exception) and then cast the object received to the
  appropriate type: `HttpRequest`, `HttpResponse`, or `Exception`. The separate
  callbacks make it clear what event triggers them and there is no longer the
  need to cast the argument to the expected type.
  ([#3749](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3749))

* Added back `netstandard2.0` and `netstandard2.1` targets.
([#3755](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3755))

## 1.0.0-rc9.8

Released 2022-Oct-17

## 1.0.0-rc9.7

Released 2022-Sep-29

* Performance improvement (Reduced memory allocation) - Updated DiagnosticSource
event subscription to specific set of events.
([#3519](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3519))

* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddAspNetCoreInstrumentation` extension to allow for more fine-grained
  options management
  ([#3661](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3661))

* Fix issue where when an application has an ExceptionFilter, the exception data
  wouldn't be collected.
  ([#3475](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3475))

## 1.0.0-rc9.6

Released 2022-Aug-18

* Removed `netstandard2.0` and `netstandard2.1` targets. .NET 5 reached EOL
  in May 2022 and .NET Core 3.1 reaches EOL in December 2022. End of support
  dates for .NET are published
  [here](https://dotnet.microsoft.com/download/dotnet). The
  instrumentation for ASP.NET Core now requires .NET 6 or later.
  ([#3567](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3567))

* Fixed an issue where activity started within middleware was modified by
  instrumentation library.
  ([#3498](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3498))

* Updated to use Activity native support from
  `System.Diagnostics.DiagnosticSource` to set activity status.
  ([#3118](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3118))
  ([#3555](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3555))

## 1.0.0-rc9.5

Released 2022-Aug-02

* Fix Remote IP Address - NULL reference exception.
  ([#3481](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3481))
* Metrics instrumentation to correctly populate `http.flavor` tag.
  (1.1 instead of HTTP/1.1 etc.)
  ([#3379](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3379))
* Tracing instrumentation to populate `http.flavor` tag.
  ([#3372](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3372))
* Tracing instrumentation to populate `http.scheme` tag.
  ([#3392](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3392))

## 1.0.0-rc9.4

Released 2022-Jun-03

* Added additional metric dimensions.
  ([#3247](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3247))
* Removes net5.0 target as .NET 5.0 is going out
  of support. The package keeps netstandard2.1 target, so it
  can still be used with .NET5.0 apps.
  ([#3147](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3147))

## 1.0.0-rc9.3

Released 2022-Apr-15

## 1.0.0-rc9.2

Released 2022-Apr-12

## 1.0.0-rc9.1

Released 2022-Mar-30

* Fix: Http server span status is now unset for `400`-`499`.
  ([#2904](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2904))
* Fix: drop direct reference of the `Microsoft.AspNetCore.Http.Features` from
  net5 & net6 targets (already part of the FrameworkReference since the net5).
  ([#2860](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2860))
* Reduce allocations calculating the http.url tag.
  ([#2947](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2947))

## 1.0.0-rc10 (broken. use 1.0.0-rc9.1 and newer)

Released 2022-Mar-04

## 1.0.0-rc9

Released 2022-Feb-02

## 1.0.0-rc8

Released 2021-Oct-08

* Replaced `http.path` tag on activity with `http.target`.
  ([#2266](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2266))

## 1.0.0-rc7

Released 2021-Jul-12

## 1.0.0-rc6

Released 2021-Jun-25

## 1.0.0-rc5

Released 2021-Jun-09

* Fixes bug
  [#1740](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1740):
  Instrumentation.AspNetCore for gRPC services omits ALL rpc.* attributes under
  certain conditions
  ([#1879](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1879))

## 1.0.0-rc4

Released 2021-Apr-23

* When using OpenTelemetry.Extensions.Hosting you can now bind
  `AspNetCoreInstrumentationOptions` from DI.
  ([#1997](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1997))

## 1.0.0-rc3

Released 2021-Mar-19

* Leverages added AddLegacySource API from OpenTelemetry SDK to trigger Samplers
  and ActivityProcessors. Samplers, ActivityProcessor.OnStart will now get the
  Activity before any enrichment done by the instrumentation.
  ([#1836](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1836))
* Performance optimization by leveraging sampling decision and short circuiting
  activity enrichment. `Filter` and `Enrich` are now only called if
  `activity.IsAllDataRequested` is `true`
  ([#1899](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1899))

## 1.0.0-rc2

Released 2021-Jan-29

## 1.0.0-rc1.1

Released 2020-Nov-17

* AspNetCoreInstrumentation sets ActivitySource to activities created outside
  ActivitySource.
  ([#1515](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1515/))
* For gRPC invocations, leading forward slash is trimmed from span name in order
  to conform to the specification.
  ([#1551](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1551))

## 0.8.0-beta.1

Released 2020-Nov-5

* Record `Exception` in AspNetCore instrumentation based on `RecordException` in
  `AspNetCoreInstrumentationOptions`
  ([#1408](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1408))
* Added configuration option `EnableGrpcAspNetCoreSupport` to enable or disable
  support for adding OpenTelemetry RPC attributes when using
  [Grpc.AspNetCore](https://www.nuget.org/packages/Grpc.AspNetCore/). This
  option is enabled by default.
  ([#1423](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1423))
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

* Instrumentation no longer store raw objects like `HttpRequest` in
  Activity.CustomProperty. To enrich activity, use the Enrich action on the
  instrumentation.
  ([#1261](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1261))
* Span Status is populated as per new spec
  ([#1313](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1313))

## 0.6.0-beta.1

Released 2020-Sep-15

* For gRPC invocations, the `grpc.method` and `grpc.status_code` attributes
  added by the library are removed from the span. The information from these
  attributes is contained in other attributes that follow the conventions of
  OpenTelemetry.
  ([#1260](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1260))

## 0.5.0-beta.2

Released 2020-08-28

* Added Filter public API on AspNetCoreInstrumentationOptions to allow filtering
  of instrumentation based on HttpContext.

* Asp.Net Core Instrumentation automatically populates HttpRequest, HttpResponse
  in Activity custom property

* Changed the default propagation to support W3C Baggage
  ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048))
  * The default ITextFormat is now `CompositePropagator(TraceContextFormat,
    BaggageFormat)`. Baggage sent via the [W3C
    Baggage](https://github.com/w3c/baggage/blob/master/baggage/HTTP_HEADER_FORMAT.md)
    header will now be parsed and set on incoming Http spans.
* Introduced support for Grpc.AspNetCore (#803).
  * Attributes are added to gRPC invocations: `rpc.system`, `rpc.service`,
    `rpc.method`. These attributes are added to an existing span generated by
    the instrumentation. This is unlike the instrumentation for client-side gRPC
    calls where one span is created for the gRPC call and a separate span is
    created for the underlying HTTP call in the event both gRPC and HTTP
    instrumentation are enabled.
* Renamed `ITextPropagator` to `IPropagator`
  ([#1190](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1190))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
