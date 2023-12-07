# Changelog

## Unreleased

## 1.6.0-rc.1

Released 2023-Dec-01

* Removed support for `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable. The
  library will now emit only the
  [stable](https://github.com/open-telemetry/semantic-conventions/tree/v1.23.0/docs/http)
  semantic conventions.
  ([#5068](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5068))

* Update activity DisplayName as per the specification.
  ([#5078](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5078))

* Removed reference to `OpenTelemetry` package. This is a **breaking change**
  for users relying on
  [SuppressDownstreamInstrumentation](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.GrpcNetClient#suppressdownstreaminstrumentation)
  option in `OpenTelemetry.Instrumentation.GrpcNetClient`. For details, check
  out this
  [issue](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5092).
  ([#5077](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5077))

* **Breaking Change**: Renamed `HttpClientInstrumentationOptions` to
  `HttpClientTraceInstrumentationOptions`.
  ([#5109](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5109))

* **Breaking Change**: Removed `http.user_agent` tag from HttpClient activity.
  ([#5110](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5110))

* `HttpWebRequest` : Introduced additional values for `error.type` tag on
  activity and `http.client.request.duration` metric.
  ([#5111](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5111))

## 1.6.0-beta.3

Released 2023-Nov-17

* Removed the Activity Status Description that was being set during
  exceptions. Activity Status will continue to be reported as `Error`.
  This is a **breaking change**. `EnrichWithException` can be leveraged
  to restore this behavior.
  ([#5025](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5025))

* Updated `http.request.method` to match specification guidelines.
  * For activity, if the method does not belong to one of the [known
    values](https://github.com/open-telemetry/semantic-conventions/blob/v1.22.0/docs/http/http-spans.md#:~:text=http.request.method%20has%20the%20following%20list%20of%20well%2Dknown%20values)
    then the request method will be set on an additional tag
    `http.request.method.original` and `http.request.method` will be set to
    `_OTHER`.
  * For metrics, if the original method does not belong to one of the [known
    values](https://github.com/open-telemetry/semantic-conventions/blob/v1.22.0/docs/http/http-spans.md#:~:text=http.request.method%20has%20the%20following%20list%20of%20well%2Dknown%20values)
    then `http.request.method` on `http.client.request.duration` metric will be
    set to `_OTHER`

  `http.request.method` is set on `http.client.request.duration` metric or
  activity when `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable is set to
  `http` or `http/dup`.
  ([#5003](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5003))

* An additional attribute `error.type` will be added to activity and
  `http.client.request.duration` metric in case of failed requests as per the
  [specification](https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md#common-attributes).

  Users moving to `net8.0` or newer frameworks from lower versions will see
  difference in values in case of an exception. `net8.0` or newer frameworks add
  the ability to further drill down the exceptions to a specific type through
  [HttpRequestError](https://learn.microsoft.com/dotnet/api/system.net.http.httprequesterror?view=net-8.0)
  enum. For lower versions, the individual types will be rolled in to a single
  type. This could be a **breaking change** if alerts are set based on the values.

  The attribute will only be added when `OTEL_SEMCONV_STABILITY_OPT_IN`
  environment variable is set to `http` or `http/dup`.

  ([#5005](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5005))
  ([#5034](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5034))

* Fixed `network.protocol.version` attribute values to match the specification.
  ([#5006](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5006))

* Set `network.protocol.version` value using the protocol version on the
  received response. If the request fails without response, then
  `network.protocol.version` attribute will not be set on Activity and
  `http.client.request.duration` metric.
  ([#5043](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5043))

## 1.6.0-beta.2

Released 2023-Oct-26

* Introduced a new metric for `HttpClient`, `http.client.request.duration`
  measured in seconds. The OTel SDK (starting with version 1.6.0)
  [applies custom histogram buckets](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4820)
  for this metric to comply with the
  [Semantic Convention for Http Metrics](https://github.com/open-telemetry/semantic-conventions/blob/2bad9afad58fbd6b33cc683d1ad1f006e35e4a5d/docs/http/http-metrics.md).
  This new metric is only available for users who opt-in to the new
  semantic convention by configuring the `OTEL_SEMCONV_STABILITY_OPT_IN`
  environment variable to either `http` (to emit only the new metric) or
  `http/dup` (to emit both the new and old metrics).
  ([#4870](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4870))

  * New metric: `http.client.request.duration`
    * Unit: `s` (seconds)
    * Histogram Buckets: `0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5,
    0.75, 1,  2.5, 5, 7.5, 10`
  * Old metric: `http.client.duration`
    * Unit: `ms` (milliseconds)
    * Histogram Buckets: `0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500,
    5000, 7500, 10000`

  Note: The older `http.client.duration` metric and
  `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable will eventually be
  removed after the HTTP semantic conventions are marked stable. At which time
  this instrumentation can publish a stable release. Refer to the specification
  for more information regarding the new HTTP semantic conventions:
  * [http-spans](https://github.com/open-telemetry/semantic-conventions/blob/2bad9afad58fbd6b33cc683d1ad1f006e35e4a5d/docs/http/http-spans.md)
  * [http-metrics](https://github.com/open-telemetry/semantic-conventions/blob/2bad9afad58fbd6b33cc683d1ad1f006e35e4a5d/docs/http/http-metrics.md)

* Added support for publishing `http.client.duration` &
  `http.client.request.duration` metrics on .NET Framework for `HttpWebRequest`.
  ([#4870](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4870))

* Following `HttpClient` metrics will now be enabled by default when targeting
  `.NET8.0` framework or newer.

  * **Meter** : `System.Net.Http`
    * `http.client.request.duration`
    * `http.client.active_requests`
    * `http.client.open_connections`
    * `http.client.connection.duration`
    * `http.client.request.time_in_queue`

  * **Meter** : `System.Net.NameResolution`
    * `dns.lookups.duration`

  For details about each individual metric check [System.Net metrics
  docs
  page](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-system-net).

  **NOTES**:
  * When targeting `.NET8.0` framework or newer, `http.client.request.duration` metric
    will only follow
    [v1.22.0](https://github.com/open-telemetry/semantic-conventions/blob/v1.22.0/docs/http/http-metrics.md#metric-httpclientrequestduration)
    semantic conventions specification. Ability to switch behavior to older
    conventions using  `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable is
    not available.
  * Users can opt-out of metrics that are not required using
    [views](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics/customizing-the-sdk#drop-an-instrument).

  ([#4931](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4931))

* Added `url.scheme` attribute to `http.client.request.duration` metric. The
  metric will be emitted when `OTEL_SEMCONV_STABILITY_OPT_IN` environment
  variable is set to `http` or `http/dup`.
  ([#4989](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4989))

* Updated description for `http.client.request.duration` metrics to match spec
  definition.
  ([#4990](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4990))

* `dns.lookups.duration` metric is renamed to `dns.lookup.duration`. This change
  impacts only users on `.NET8.0` or newer framework.
  ([#5049](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5049))

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
  ([#4538](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4538),
  [#4639](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4639))

## 1.5.0-beta.1

Released 2023-Jun-05

* Bumped the package version to `1.5.0-beta.1` to keep its major and minor
  version in sync with that of the core packages. This would make it more
  intuitive for users to figure out what version of core packages would work
  with a given version of this package. The pre-release identifier has also been
  changed from `rc` to `beta` as we believe this more accurately reflects the
  status of this package. We believe the `rc` identifier will be more
  appropriate as semantic conventions reach stability.

* Fixed an issue of missing `http.client.duration` metric data in case of
  network failures (when response is not available).
  ([#4098](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4098))

* Improve perf by avoiding boxing of common status codes values.
  ([#4361](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4361),
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
