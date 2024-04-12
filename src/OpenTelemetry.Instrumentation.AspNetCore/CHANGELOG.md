# Changelog

## 1.8.1

Released 2024-Apr-12

* **Breaking Change**: Fixed tracing instrumentation so that by default any
  values detected in the query string component of requests are replaced with
  the text `Redacted` when building the `url.query` tag. For example,
  `?key1=value1&key2=value2` becomes `?key1=Redacted&key2=Redacted`. You can
  disable this redaction by setting the environment variable
  `OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION` to `true`.
  ([#5532](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5532))

## 1.8.0

Released 2024-Apr-04

* Fixed an issue for spans when `server.port` attribute was not set with
  `server.address` when it has default values (`80` for `HTTP` and
  `443` for `HTTPS` protocol).
  ([#5419](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5419))

* Fixed an issue where the `http.request.method_original` attribute was not set
  on activity. Now, when `http.request.method` is set and the original method
  is converted to its canonical form (e.g., `Get` is converted to `GET`),
  the original value `Get` will be stored in `http.request.method_original`.
  ([#5471](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5471))

* Fixed the name of spans that have `http.request.method` attribute set to `_OTHER`.
  The span name will be set as `HTTP {http.route}` as per the [specification](https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/http/http-spans.md#name).
  ([#5484](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5484))

## 1.7.1

Released 2024-Feb-09

* Fixed issue
  [#4466](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4466)
  where the activity instance returned by `Activity.Current` was different than
  instance obtained from `IHttpActivityFeature.Activity`.
  ([#5136](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5136))

* Fixed an issue where the `http.route` attribute was not set on either the
  `Activity` or `http.server.request.duration` metric generated from a
  request when an exception handling middleware is invoked. One caveat is that
  this fix does not address the problem for the `http.server.request.duration`
  metric when running ASP.NET Core 8. ASP.NET Core 8 contains an equivalent fix
  which should ship in version 8.0.2
  (see: [dotnet/aspnetcore#52652](https://github.com/dotnet/aspnetcore/pull/52652)).
  ([#5135](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5135))

* Fixes scenario when the `net6.0` target of this library is loaded into a
  .NET 7+ process and the instrumentation does not behave as expected. This
  is an unusual scenario that does not affect users consuming this package
  normally. This fix is primarily to support the
  [opentelemetry-dotnet-instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5252)
  project.
  ([#5252](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5252))

## 1.7.0

Released 2023-Dec-13

## 1.6.0 - First stable release of this library

Released 2023-Dec-13

* Re-introduced support for gRPC instrumentation as an opt-in experimental
  feature. From now onwards, gRPC can be enabled by setting
  `OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION` flag to
  `True`. `OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION` can
  be set as an environment variable or via IConfiguration. The change is
  introduced in order to support stable release of `http` instrumentation.
  Semantic conventions for RPC is still
  [experimental](https://github.com/open-telemetry/semantic-conventions/tree/main/docs/rpc)
  and hence the package will only support it as an opt-in experimental feature.
  Note that the support was removed in `1.6.0-rc.1` version of the package and
  versions released before `1.6.0-rc.1` had gRPC instrumentation enabled by
  default.
  ([#5130](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5130))

## 1.6.0-rc.1

Released 2023-Dec-01

* Removed support for `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable. The
  library will now emit only the
  [stable](https://github.com/open-telemetry/semantic-conventions/tree/v1.23.0/docs/http)
  semantic conventions.
  ([#5066](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5066))

* Removed `netstandard2.1` target.
  ([#5094](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5094))

* Removed support for grpc instrumentation to unblock stable release of http
  instrumentation. For details, see issue
  [#5098](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5098)
  ([#5097](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5097))

* **Breaking Change** : Renamed `AspNetCoreInstrumentationOptions` to
  `AspNetCoreTraceInstrumentationOptions`.
  ([#5108](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5108))

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
    then `http.request.method` on `http.server.request.duration` metric will be
    set to `_OTHER`

  `http.request.method` is set on `http.server.request.duration` metric or
  activity when `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable is set to
  `http` or `http/dup`.
  ([#5001](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5001))

* An additional attribute `error.type` will be added to activity and
`http.server.request.duration` metric when the request results in unhandled
exception. The attribute value will be set to full name of exception type.

  The attribute will only be added when `OTEL_SEMCONV_STABILITY_OPT_IN`
  environment variable is set to `http` or `http/dup`.
  ([#4986](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4986))

* Fixed `network.protocol.version` attribute values to match the specification.
  ([#5007](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5007))

* Calls to `/metrics` will now be included in the `http.server.request.duration`
  metric. This change may affect Prometheus pull scenario if the Prometheus
  server sends request to the scraping endpoint that contains `/metrics` in
  path.
  ([#5044](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5044))

* Fixes the `http.route` attribute for scenarios in which it was
  previously missing or incorrect. Additionally, the `http.route` attribute
  is now the same for both the metric and `Activity` emitted for a request.
  Lastly, the `Activity.DisplayName` has been adjusted to have the format
  `{http.request.method} {http.route}` to conform with [the specification](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/http/http-spans.md#name).
  There remain scenarios when using conventional routing or Razor pages where
  `http.route` is still incorrect. See [#5056](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5056)
  and [#5057](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5057)
  for more details.
  ([#5026](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5026))

* Removed `network.protocol.name` from `http.server.request.duration` metric as
  per spec.
  ([#5049](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5049))

## 1.6.0-beta.2

Released 2023-Oct-26

* Introduced a new metric, `http.server.request.duration` measured in seconds.
  The OTel SDK (starting with version 1.6.0)
  [applies custom histogram buckets](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4820)
  for this metric to comply with the
  [Semantic Convention for Http Metrics](https://github.com/open-telemetry/semantic-conventions/blob/2bad9afad58fbd6b33cc683d1ad1f006e35e4a5d/docs/http/http-metrics.md).
  This new metric is only available for users who opt-in to the new
  semantic convention by configuring the `OTEL_SEMCONV_STABILITY_OPT_IN`
  environment variable to either `http` (to emit only the new metric) or
  `http/dup` (to emit both the new and old metrics).
  ([#4802](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4802))
  * New metric: `http.server.request.duration`
    * Unit: `s` (seconds)
    * Histogram Buckets: `0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5,
    0.75, 1,  2.5, 5, 7.5, 10`
  * Old metric: `http.server.duration`
    * Unit: `ms` (milliseconds)
    * Histogram Buckets: `0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500,
    5000, 7500, 10000`

   Note: the older `http.server.duration` metric and
   `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable will eventually be
   removed after the HTTP semantic conventions are marked stable.
   At which time this instrumentation can publish a stable release. Refer to
   the specification for more information regarding the new HTTP semantic
   conventions for both
   [spans](https://github.com/open-telemetry/semantic-conventions/blob/2bad9afad58fbd6b33cc683d1ad1f006e35e4a5d/docs/http/http-spans.md)
   and
   [metrics](https://github.com/open-telemetry/semantic-conventions/blob/2bad9afad58fbd6b33cc683d1ad1f006e35e4a5d/docs/http/http-metrics.md).

* Following metrics will now be enabled by default when targeting `.NET8.0` or
  newer framework:

  * **Meter** : `Microsoft.AspNetCore.Hosting`
    * `http.server.request.duration`
    * `http.server.active_requests`

  * **Meter** : `Microsoft.AspNetCore.Server.Kestrel`
    * `kestrel.active_connections`
    * `kestrel.connection.duration`
    * `kestrel.rejected_connections`
    * `kestrel.queued_connections`
    * `kestrel.queued_requests`
    * `kestrel.upgraded_connections`
    * `kestrel.tls_handshake.duration`
    * `kestrel.active_tls_handshakes`

  * **Meter** : `Microsoft.AspNetCore.Http.Connections`
    * `signalr.server.connection.duration`
    * `signalr.server.active_connections`

  * **Meter** : `Microsoft.AspNetCore.Routing`
    * `aspnetcore.routing.match_attempts`

  * **Meter** : `Microsoft.AspNetCore.Diagnostics`
    * `aspnetcore.diagnostics.exceptions`

  * **Meter** : `Microsoft.AspNetCore.RateLimiting`
    * `aspnetcore.rate_limiting.active_request_leases`
    * `aspnetcore.rate_limiting.request_lease.duration`
    * `aspnetcore.rate_limiting.queued_requests`
    * `aspnetcore.rate_limiting.request.time_in_queue`
    * `aspnetcore.rate_limiting.requests`

  For details about each individual metric check [ASP.NET Core
  docs
  page](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-aspnetcore).

  **NOTES**:
  * When targeting `.NET8.0` framework or newer, `http.server.request.duration` metric
    will only follow
    [v1.22.0](https://github.com/open-telemetry/semantic-conventions/blob/v1.22.0/docs/http/http-metrics.md#metric-httpclientrequestduration)
    semantic conventions specification. Ability to switch behavior to older
    conventions using `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable is
    not available.
  * Users can opt-out of metrics that are not required using
    [views](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics/customizing-the-sdk#drop-an-instrument).

  ([#4934](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4934))

* Added `network.protocol.name` dimension to `http.server.request.duration`
metric. This change only affects users setting `OTEL_SEMCONV_STABILITY_OPT_IN`
to `http` or `http/dup`.
([#4934](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4934))

* **Breaking**: Removed `Enrich` and `Filter` support for **metrics**
  instrumentation. With this change, `AspNetCoreMetricsInstrumentationOptions`
  is no longer available.
  ([#4981](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4981))

  * `Enrich` migration:

    An enrichment API for the `http.server.request.duration` metric is available
    inside AspNetCore for users targeting .NET 8.0 (or newer). For details see:
    [Enrich the ASP.NET Core request
    metric](https://learn.microsoft.com/aspnet/core/log-mon/metrics/metrics?view=aspnetcore-8.0#enrich-the-aspnet-core-request-metric).

  * `Filter` migration:

    There is no comparable filter mechanism currently available for any .NET
    version. Please [share your
    feedback](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4982)
    if you are impacted by this feature gap.

      > **Note**
    > The [View API](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics/customizing-the-sdk#select-specific-tags)
    may be used to drop dimensions.

* Updated description for `http.server.request.duration` metrics to match spec
  definition.
  ([#4990](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4990))

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
