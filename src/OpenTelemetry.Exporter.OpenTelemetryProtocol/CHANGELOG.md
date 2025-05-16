# Changelog

This file contains individual changes for the
OpenTelemetry.Exporter.OpenTelemetryProtocol package. For highlights and
announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

* Fixed an issue in .NET Framework where OTLP export of traces, logs, and
  metrics using `OtlpExportProtocol.Grpc` did not correctly set the initial
  write position, resulting in gRPC protocol errors.
  ([#6280](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6280))

## 1.12.0

Released 2025-Apr-29

* **Breaking Change**: .NET Framework and .NET Standard builds now default to
  exporting over OTLP/HTTP instead of OTLP/gRPC. **This change could result in a
  failure to export telemetry unless appropriate measures are taken.**
  Additionally, if you explicitly configure the exporter to use OTLP/gRPC it may
  result in a `NotSupportedException` without further configuration. Please
  carefully review issue
  ([#6209](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6209))
  for additional information and workarounds.
  ([#6229](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6229))

## 1.11.2

Released 2025-Mar-04

* Fixed a bug in .NET Framework gRPC export client where the default success
  export response was incorrectly marked as false, now changed to true, ensuring
  exports are correctly marked as successful.
  ([#6099](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6099))

* Fixed an issues causing trace exports to fail when
  `Activity.StatusDescription` exceeds 127 bytes.
  ([#6119](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6119))

* Fixed incorrect log serialization of attributes with null values, causing
  some backends to reject logs.
  ([#6149](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6149))

## 1.11.1

Released 2025-Jan-22

* Fixed an issue where the OTLP gRPC exporter did not export logs, metrics, or
  traces in .NET Framework projects.
  ([#6083](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6083))

## 1.11.0

Released 2025-Jan-15

## 1.11.0-rc.1

Released 2024-Dec-11

* Removed the following package references:

  * `Google.Protobuf`
  * `Grpc`
  * `Grpc.Net.Client`

  These changes were made to streamline dependencies and reduce the footprint of
  the exporter.
  ([#6005](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6005))

* Switched from using the `Google.Protobuf` library for serialization to a
  custom manual implementation of protobuf serialization.
  ([#6005](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6005))

* Fixed an issue where a `service.name` was added to the resource if it was
  missing. The exporter now respects the resource data provided by the SDK
  without modifications.
  ([#6015](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6015))

* Removed the peer service resolver, which was based on earlier experimental
  semantic conventions that are not part of the stable specification. This
  change ensures that the exporter no longer modifies or assumes the value of
  peer service attributes, aligning it more closely with OpenTelemetry protocol
  specifications.
  ([#6005](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6005))

## 1.10.0

Released 2024-Nov-12

## 1.10.0-rc.1

Released 2024-Nov-01

* Added support for exporting instrumentation scope attributes from
  `ActivitySource.Tags`.
  ([#5897](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5897))

## 1.10.0-beta.1

Released 2024-Sep-30

* **Breaking change**: Non-primitive attribute (logs) and tag (traces) values
  converted using `Convert.ToString` will now format using
  `CultureInfo.InvariantCulture`.
  ([#5700](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5700))

* Fixed an issue causing `NotSupportedException`s to be thrown on startup when
  `AddOtlpExporter` registration extensions are called while using custom
  dependency injection containers which automatically create services (Unity,
  Grace, etc.).
  ([#5808](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5808))

  * Fixed `PlatformNotSupportedException`s being thrown during export when running
  on mobile platforms which caused telemetry to be dropped silently.
  ([#5821](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5821))

## 1.9.0

Released 2024-Jun-14

## 1.9.0-rc.1

Released 2024-Jun-07

* The experimental APIs previously covered by `OTEL1000`
  (`LoggerProviderBuilder.AddOtlpExporter` extension) are now part of the public
  API and supported in stable builds.
  ([#5648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5648))

## 1.9.0-alpha.1

Released 2024-May-20

* `User-Agent` header format changed from
  `OTel-OTLP-Exporter-Dotnet/{NuGet Package Version}+{Commit Hash}`
  to `OTel-OTLP-Exporter-Dotnet/{NuGet Package Version}`.
  ([#5528](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5528))

* Implementation of [OTLP
  specification](https://github.com/open-telemetry/opentelemetry-proto/blob/v1.2.0/opentelemetry/proto/trace/v1/trace.proto#L112-L133)
  for propagating `Span` and `SpanLink` flags containing W3C trace flags and
  `parent_is_remote` information.
  ([#5563](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5563))

* Introduced experimental support for automatically retrying export to the otlp
  endpoint by storing the telemetry offline during transient network errors.
  Users can enable this feature by setting the
  `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY` environment variable to `disk`. The
  default path where the telemetry is stored is obtained by calling
  [Path.GetTempPath()](https://learn.microsoft.com/dotnet/api/system.io.path.gettemppath)
  or can be customized by setting
  `OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH` environment
  variable.
  ([#5527](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5527))

## 1.8.1

Released 2024-Apr-17

* Fix native AoT warnings in `OpenTelemetry.Exporter.OpenTelemetryProtocol`.
  ([#5520](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5520))

## 1.8.0

Released 2024-Apr-02

* `OtlpExporter` will no longer throw an exception (even on .NET Core 3.1)
   when the `System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport`
  `AppContext` switch is NOT set AND using `OtlpExportProtocol.Grpc`
  to send to an insecure ("http") endpoint.
  `System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport`
  is not required to be set [when using .NET 5 or newer](https://learn.microsoft.com/aspnet/core/grpc/troubleshoot?view=aspnetcore-8.0#call-insecure-grpc-services-with-net-core-client).
  ([#5486](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5486))

* Replaced environment variable
  `OTEL_DOTNET_EXPERIMENTAL_OTLP_ENABLE_INMEMORY_RETRY` with
  `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY`. `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY`
  when set to `in_memory` will enable automatic retries in case of transient
  failures during data export to an OTLP endpoint.
  ([#5495](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5495))

## 1.8.0-rc.1

Released 2024-Mar-27

## 1.8.0-beta.1

Released 2024-Mar-14

* **Experimental (pre-release builds only):** Added
  `LoggerProviderBuilder.AddOtlpExporter` registration extensions.
  [#5103](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5103)

* Removed the `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES`
  environment variable, following the stabilization of the exception attributes
  `exception.type`, `exception.message`, and `exception.stacktrace` in the
  [OpenTelemetry Semantic
  Conventions](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-logs.md#semantic-conventions-for-exceptions-in-logs).
  These attributes, corresponding to `LogRecord.Exception`, are now stable and
  will be automatically included in exports.
  ([#5258](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5258))

* Updated `OtlpLogExporter` to set `body` on the data model from
  `LogRecord.Body` if `{OriginalFormat}` attribute is NOT found and
  `FormattedMessage` is `null`. This is typically the case when using the
  experimental Logs Bridge API.
  ([#5268](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5268))

* Updated `OtlpLogExporter` to set instrumentation scope name on the data model
  from `LogRecord.Logger.Name` if `LogRecord.CategoryName` is `null`. This is
  typically the case when using the experimental Logs Bridge API.
  ([#5300](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5300))

* URL encoded values in `OTEL_EXPORTER_OTLP_HEADERS` are now correctly decoded
  as it is mandated by the specification.
  ([#5316](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5268))

* **Experimental (pre-release builds only):** Add support in
  `OtlpMetricExporter` for emitting exemplars supplied on Counters, Gauges, and
  ExponentialHistograms.
  ([#5397](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5397))

* Setting `Endpoint` or `HttpClientFactory` properties on `OtlpExporterOptions`
  to `null` will now result in an `ArgumentNullException` being thrown.
  ([#5434](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5434))

* Introduced experimental support for automatically retrying export to the otlp
  endpoint when transient network errors occur. Users can enable this feature by
  setting `OTEL_DOTNET_EXPERIMENTAL_OTLP_ENABLE_INMEMORY_RETRY` environment
  variable to true.
  ([#5435](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5435))

* Added `IOpenTelemetryBuilder.UseOtlpExporter` extension to simplify setup of
  the OTLP Exporter when all three signals are used (logs, metrics, and traces).
  The new extension has the following behaviors:

  * Calling `UseOtlpExporter` will automatically enable logging, tracing, and
    metrics. Additional calls to `WithLogging`, `WithMetrics`, and `WithTracing`
    are NOT required however for metrics and tracing sources/meters still need
    to be enabled.

  * `UseOtlpExporter` can only be called once and cannot be used with the
    existing `AddOtlpExporter` extensions. Extra calls will result in
    `NotSupportedException`s being thrown.

  * `UseOtlpExporter` will register the OTLP Exporter at the end of the
    processor pipeline for logging and tracing.

  * The OTLP Exporters added for logging, tracing, and metrics can be configured
    using environment variables or `IConfiguration`.

  For details see: [README > Enable OTLP Exporter for all
  signals](./README.md#enable-otlp-exporter-for-all-signals).

  PR: [#5400](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5400)

## 1.7.0

Released 2023-Dec-08

## 1.7.0-rc.1

Released 2023-Nov-29

* Made `OpenTelemetry.Exporter.OtlpLogExporter` public.
  ([#4979](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4979))

* Updated the `OpenTelemetryLoggerOptions.AddOtlpExporter` extension to retrieve
  `OtlpExporterOptions` and `LogRecordExportProcessorOptions` using the
  `IServiceProvider` / Options API so that they can be controlled via
  `IConfiguration` (similar to metrics and traces).
  ([#4916](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4916))

* Added an `OpenTelemetryLoggerOptions.AddOtlpExporter` extension overload which
  accepts a `name` parameter to support named options.
  ([#4916](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4916))

* Add support for Instrumentation Scope Attributes (i.e [Meter
  Tags](https://learn.microsoft.com/dotnet/api/system.diagnostics.metrics.meter.tags)),
  fixing issue
  [#4563](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4563).
  ([#5089](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5089))

## 1.7.0-alpha.1

Released 2023-Oct-16

* Bumped the version of `Google.Protobuf` used by the project to `3.22.5` so
  that consuming applications can be published as NativeAOT successfully. Also,
  a new performance feature can be used instead of reflection emit, which is
  not AOT-compatible. Removed the dependency on `System.Reflection.Emit.Lightweight`.
  ([#4859](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4859))

* Added support for `OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT`
  and `OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT`.
  ([#4887](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4887))

* Added ability to export attributes corresponding to `LogRecord.Exception` i.e.
`exception.type`, `exception.message` and `exception.stacktrace`. These
attributes will be exported when
`OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES` environment
variable will be set to `true`.

  **NOTE**: These attributes were removed in [1.6.0-rc.1](#160-rc1) release in
  order to support stable release of OTLP Log Exporter. The attributes will now be
  available via environment variable mentioned above.
  ([#4892](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4892))

* Added ability to export attributes corresponding to `LogRecord.EventId.Id` as
`logrecord.event.id` and `LogRecord.EventId.Name` as `logrecord.event.name`. The
attributes will be exported when
`OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES` will be set to `true`.

  **NOTE**: These attributes were removed in [1.6.0-rc.1](#160-rc1) release in
  order to support stable release of OTLP Log Exporter. The attributes will now
  be available via environment variable mentioned above.
  ([#4925](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4925))

* `LogRecord.CategoryName` will now be exported as
[InstrumentationScope](https://github.com/open-telemetry/opentelemetry-dotnet/blob/3c2bb7c93dd2e697636479a1882f49bb0c4a362e/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/opentelemetry/proto/common/v1/common.proto#L71-L81)
`name` field under
[ScopeLogs](https://github.com/open-telemetry/opentelemetry-dotnet/blob/3c2bb7c93dd2e697636479a1882f49bb0c4a362e/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/opentelemetry/proto/logs/v1/logs.proto#L64-L75).
([#4941](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4941))

## 1.6.0

Released 2023-Sep-05

## 1.6.0-rc.1

Released 2023-Aug-21

* **Breaking change**: Excluded attributes corresponding to
`LogRecord.Exception`, `LogRecord.EventId` and `LogRecord.CategoryName` from the
exported data. See following details for reasoning behind removing each
individual property:
  * `LogRecord.Exception`: The semantic conventions for attributes corresponding
    to exception data are not yet stable. Track issue
    [#4831](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4831)
    for details.
  * `LogRecord.EventId`: The attributes corresponding to this property are
    specific to .NET logging data model and there is no established convention
    defined for them yet. Track issue
    [#4776](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4776)
    for details.
  * `LogRecord.CategoryName`: The attribute corresponding to this property is
    specific to .NET logging data model and there is no established convention
    defined for it yet. Track issue
    [#3491](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3491)
    for details.

  This change is temporarily done in order to release **stable** version of OTLP
  Log Exporter.
  ([#4781](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4781))

* Added extension method for configuring export processor options for otlp log
exporter.
([#4733](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4733))

* Added support for configuring the metric exporter's temporality using the
  environment variable `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` as
  defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.23.0/specification/metrics/sdk_exporters/otlp.md#additional-configuration).
  ([#4667](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4667))

## 1.6.0-alpha.1

Released 2023-Jul-12

* Merged `OpenTelemetry.Exporter.OpenTelemetryProtocol.Logs` package into
  `OpenTelemetry.Exporter.OpenTelemetryProtocol`. Going Forward,
  `OpenTelemetry.Exporter.OpenTelemetryProtocol` will be the only package needed
  for all 3 signals (Logs, Metrics, and Traces). All the changes made in
  [`OpenTelemetry.Exporter.OpenTelemetryProtocol.Logs`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.5.0/src/OpenTelemetry.Exporter.OpenTelemetryProtocol.Logs/CHANGELOG.md#changelog)
  are now included in this package.
  ([#4556](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4556))

* Updated Grpc.Net.Client to `2.45.0` to fix unobserved exception
  from failed calls.
  ([#4573](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4573))

* Updated Grpc.Net.Client to `2.52.0` to address the vulnerability reported by
  CVE-2023-32731. Refer to
  [https://github.com/grpc/grpc/pull/32309](https://github.com/grpc/grpc/pull/32309)
  for more details.
  ([#4647](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4647))

* **Experimental (pre-release builds only):**

  * Note: See
    [#4735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4735)
    for the introduction of experimental api support.

  * Add back support for Exemplars. See
    [exemplars](../../docs/metrics/customizing-the-sdk/README.md#exemplars) for
    instructions to enable exemplars.
    ([#4553](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4553))

  * Updated to support `Severity` and `SeverityText` when exporting
    `LogRecord`s.
    ([#4568](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4568))

## 1.5.1

Released 2023-Jun-26

## 1.5.0

Released 2023-Jun-05

* Remove support for exporting `Exemplars`. This would be added back in the
  `1.6.*` prerelease versions right after `1.5.0` stable version is released.
  ([#4533](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4533))

## 1.5.0-rc.1

Released 2023-May-25

* Revert version of `Google.Protobuf` to `3.19.4` (see
  [#4201](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4201)).
  This also reintroduces the `System.Reflection.Emit.Lightweight` dependency.
  ([#4407](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4407))

## 1.5.0-alpha.2

Released 2023-Mar-31

* Add support for exporting histograms aggregated using the
  [Base2 Exponential Bucket Histogram Aggregation](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#base2-exponential-bucket-histogram-aggregation).
  ([#4337](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4337))

* Added support to set `TraceState` when converting the
  System.Diagnostics.Activity object to its corresponding
  OpenTelemetry.Proto.Trace.V1.Span object.
  ([#4331](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4331))

## 1.5.0-alpha.1

Released 2023-Mar-07

* Bumped the version of `Google.Protobuf` used by the project to `3.22.0` so
  that a new performance feature can be used instead of reflection. Removed the
  dependency on `System.Reflection.Emit.Lightweight`.
  ([#4201](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4201))

* Added Exemplar support. See [exemplars](../../docs/metrics/customizing-the-sdk/README.md#exemplars)
  for instructions to enable exemplars.

## 1.4.0

Released 2023-Feb-24

* Updated OTel SDK dependency to 1.4.0

* `AddOtlpExporter` extension methods will now always create a new options
  instance when named options are NOT used.
  ([#4200](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4200))

## 1.4.0-rc.4

Released 2023-Feb-10

* Added a direct dependency on System.Reflection.Emit.Lightweight which
  previously came transitively through the OpenTelemetry SDK.
  ([#4140](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4140))

## 1.4.0-rc.3

Released 2023-Feb-01

* Include User-Agent header
  [per the specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#user-agent).
  ([#4120](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4120))

## 1.4.0-rc.2

Released 2023-Jan-09

* For `AddOtlpExporter` extension methods, configuration delegates will be
  executed inline and not through Options API when named options are NOT used.
  ([#4058](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4058))

## 1.4.0-rc.1

Released 2022-Dec-12

* Fix default values for `OTEL_ATTRIBUTE_COUNT_LIMIT`,
  `OTEL_ATTRIBUTE_COUNT_LIMIT`,
  `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT`,
  `OTEL_SPAN_EVENT_COUNT_LIMIT`,
  `OTEL_SPAN_LINK_COUNT_LIMIT`,
  `OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT`,
  `OTEL_LINK_ATTRIBUTE_COUNT_LIMIT`. All of them are defaulted to `128`.
  ([#3978](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3978))

## 1.4.0-beta.3

Released 2022-Nov-07

* Log Exporter modified to no longer prefix scope-depth when exporting ILogger
  scopes as attributes. Empty keys and {OriginalFormat} key will be ignored from
  scopes.
  ([#3843](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3843))

## 1.4.0-beta.2

Released 2022-Oct-17

* OTLP histogram data points will now include `Min` and `Max` values when
  they are present.
  ([#2735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2735))

* Adds support for limiting the length and count of attributes exported from
  the OTLP log exporter. These
  [Attribute Limits](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#attribute-limits)
  are configured via the environment variables defined in the specification.
  ([#3684](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3684))

* Added support for loading environment variables from `IConfiguration` when
  using the `AddOtlpExporter` extensions
  ([#3760](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3760))

## 1.4.0-beta.1

Released 2022-Sep-29

* Added overloads which accept a name to the `MeterProviderBuilder`
  `AddOtlpExporter` extension to allow for more fine-grained options management
  ([#3648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3648))

* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddOtlpExporter` extension to allow for more fine-grained options management
  ([#3653](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3653))

## 1.4.0-alpha.2

Released 2022-Aug-18

* When using [Attribute
  Limits](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#attribute-limits)
  the OTLP exporter will now send "dropped" counts where applicable (ex:
  [dropped_attributes_count](https://github.com/open-telemetry/opentelemetry-proto/blob/001e5eabf3ea0193ef9343c1b9a057d23d583d7c/opentelemetry/proto/trace/v1/trace.proto#L191)).
  ([#3580](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3580))

## 1.4.0-alpha.1

Released 2022-Aug-02

* Adds support for limiting the length and count of attributes exported from
  the OTLP exporter. These
  [Attribute Limits](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#attribute-limits)
  are configured via the environment variables defined in the specification.
  ([#3376](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3376))

* The `MetricReaderOptions` defaults can be overridden using
  `OTEL_METRIC_EXPORT_INTERVAL` and `OTEL_METRIC_EXPORT_TIMEOUT`
  environmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.12.0/specification/sdk-environment-variables.md#periodic-exporting-metricreader).
  ([#3424](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3424))

## 1.3.0

Released 2022-Jun-03

## 1.3.0-rc.2

Released 2022-June-1

## 1.3.0-beta.2

Released 2022-May-16

* LogExporter to support Logging Scopes.
  ([#3218](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3218))

* Support `HttpProtobuf` protocol with logs & added `HttpClientFactory`
option
 ([#3225](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3225))

* Removes net5.0 target and replaced with net6.0
  as .NET 5.0 is going out of support.
  The package keeps netstandard2.1 target, so it
  can still be used with .NET5.0 apps.
  ([#3147](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3147))

* Fix handling of array-valued attributes for the OTLP trace exporter.
  ([#3238](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3238))

* Improve the conversion and formatting of attribute values to the OTLP format.
  The list of data types that must be supported per the
  [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/common#attribute)
  is more narrow than what the .NET OpenTelemetry SDK supports. Numeric
  [built-in value types](https://docs.microsoft.com/dotnet/csharp/language-reference/builtin-types/built-in-types)
  are supported by converting to a `long` or `double` as appropriate except for
  numeric types that could cause overflow (`ulong`) or rounding (`decimal`)
  which are converted to strings. Non-numeric built-in types - `string`,
  `char`, `bool` are supported. All other types are converted to a `string`.
  Array values are also supported.
  ([#3262](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3262))
  ([#3274](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3274))

## 1.3.0-beta.1

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework
  version supported is .NET 4.6.2. ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.2.0

Released 2022-Apr-15

* LogExporter to correctly map Severity to OTLP.
  ([#3177](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3177))

* LogExporter to special case {OriginalFormat} to populate
  Body. ([#3182](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182))

## 1.2.0-rc5

Released 2022-Apr-12

* Updated underlying proto files to
  [v0.16.0](https://github.com/open-telemetry/opentelemetry-proto/releases/tag/v0.16.0).
  The LogRecord.Name field was removed. The CategoryName provided
  when calling CreateLogger previously populated this field. For now,
  CategoryName is no longer exported via OTLP. It will be reintroduced
  in the future as an attribute.

## 1.2.0-rc4

Released 2022-Mar-30

* Added support for Activity Status and StatusDescription which were
  added to Activity from `System.Diagnostics.DiagnosticSource` version 6.0.
  Prior to version 6.0, setting the status of an Activity was provided by the
  .NET OpenTelemetry API via the `Activity.SetStatus` extension method in the
  `OpenTelemetry.Trace` namespace. Internally, this extension method added the
  status as tags on the Activity: `otel.status_code` and `otel.status_description`.
  Therefore, to maintain backward compatibility, the exporter falls back to using
  these tags to infer status.
 ([#3100](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3100))

* Fixed OTLP metric exporter to default to a periodic 60 second export cycle.
  A bug was introduced in #2717 that caused the OTLP metric export to default
  to a manual export cycle (i.e., requiring an explicit flush). A workaround
  for this bug has been provided
  [here](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2979#issuecomment-1061060541).
  ([#2982](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2982))

* Bumped minimum required gRPC version (2.23.0 to 2.44.0).
  Fixes issues building on Apple Silicon (M1).
  ([#2963](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2963))

* Fixed issue where the configuration of an OTLP exporter could be changed
  after instantiation by altering the original `OtlpExporterOptions` provided.
  ([#3066](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3066))

* TraceExporter to stop populating `DeprecatedCode` in OTLP Status.

## 1.2.0-rc3

Released 2022-Mar-04

* LogExporter bug fix to handle null EventName.
  ([#2871](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2871))

* Fixed the default endpoint for OTLP exporter over HTTP/Protobuf.
  The default value is `http://localhost:4318`.
  ([#2868](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2868))

* Removes metric related configuration options from `OtlpExporterOptions`.
  `MetricReaderType`, `PeriodicExporterMetricReaderOptions`, and `Temporality`
  are now configurable via the `MetricReaderOptions`.
  ([#2717](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2717))

* Exporter bug fix to not throw exceptions from Export method.
  ([#2915](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2915))

* OTLP LogExporter modified to not drop the whole batch if a single log from the
  batch is invalid.
  ([#2934](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2934))

## 1.2.0-rc2

Released 2022-Feb-02

* Added validation that insecure channel is configured correctly when using
  .NET Core 3.x for gRPC-based exporting.
  ([#2691](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2691))

* Changed `OtlpLogExporter` to convert `ILogger` structured log inputs to
  `Attributes` in OpenTelemetry (only active when `ParseStateValues` is `true`
  on `OpenTelemetryLoggerOptions`)

## 1.2.0-rc1

Released 2021-Nov-29

* Added configuration options for `MetricReaderType` to allow for configuring
  the `OtlpMetricExporter` to export either manually or periodically.
  ([#2674](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2674))

* The internal log message used when OTLP export client connection failure occurs,
  will now include the endpoint uri as well.
  ([#2686](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2686))

* Support `HttpProtobuf` protocol with metrics & added `HttpClientFactory`
  option
  ([#2696](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2696))

## 1.2.0-beta2

Released 2021-Nov-19

* Changed `OtlpExporterOptions` constructor to throw
  `FormatException` if it fails to parse any of the supported environment
  variables.

* Changed `OtlpExporterOptions.MetricExportIntervalMilliseconds` to default
  60000 milliseconds.
  ([#2641](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2641))

## 1.2.0-beta1

Released 2021-Oct-08

* `MeterProviderBuilder` extension methods now support `OtlpExporterOptions`
  bound to `IConfiguration` when using OpenTelemetry.Extensions.Hosting
  ([#2413](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2413))
* Extended `OtlpExporterOptions` by `Protocol` property. The property can be
  overridden by `OTEL_EXPORTER_OTLP_PROTOCOL` environmental variable (grpc or http/protobuf).
  Implemented OTLP over HTTP binary protobuf trace exporter.
  ([#2292](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2292))

## 1.2.0-alpha4

Released 2021-Sep-23

## 1.2.0-alpha3

Released 2021-Sep-13

* `OtlpExporterOptions.BatchExportProcessorOptions` is initialized with
  `BatchExportActivityProcessorOptions` which supports field value overriding
  using `OTEL_BSP_SCHEDULE_DELAY`, `OTEL_BSP_EXPORT_TIMEOUT`,
  `OTEL_BSP_MAX_QUEUE_SIZE`, `OTEL_BSP_MAX_EXPORT_BATCH_SIZE`
  environmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/sdk-environment-variables.md#batch-span-processor).
  ([#2219](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2219))

## 1.2.0-alpha2

Released 2021-Aug-24

* The `OtlpExporterOptions` defaults can be overridden using
  `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_HEADERS` and `OTEL_EXPORTER_OTLP_TIMEOUT`
  environmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md).
  ([#2188](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2188))

* Changed default temporality for Metrics to be cumulative.

## 1.2.0-alpha1

Released 2021-Jul-23

* Removes .NET Framework 4.5.2, .NET 4.6 support. The minimum .NET Framework
  version supported is .NET 4.6.1. ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

* Add Metrics support.([#2174](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2174))

## 1.1.0

Released 2021-Jul-12

## 1.1.0-rc1

Released 2021-Jun-25

## 1.1.0-beta4

Released 2021-Jun-09

## 1.1.0-beta3

Released 2021-May-11

## 1.1.0-beta2

Released 2021-Apr-23

* Resolves `System.TypeInitializationException` exception when using the
  exporter with an application that references Google.Protobuf 3.15. The OTLP
  exporter now depends on Google.Protobuf 3.15.5 enabling the use of the new
  `UnsafeByteOperations.UnsafeWrap` to avoid unnecessary allocations.
  ([#1873](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1873))

* Null values in string arrays are preserved according to
  [spec](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/common/README.md).
  ([#1919](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1919)
  [#1945](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1945))

* When using OpenTelemetry.Extensions.Hosting you can now bind
  `OtlpExporterOptions` to `IConfiguration` using the `Configure` extension (ex:
  `services.Configure<OtlpExporterOptions>(this.Configuration.GetSection("Otlp"));`).
  ([#1942](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1942))

## 1.1.0-beta1

Released 2021-Mar-19

## 1.0.1

Released 2021-Feb-10

## 1.0.0-rc4

Released 2021-Feb-09

* Add back support for secure gRPC connections over https.
  ([#1804](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1804))

## 1.0.0-rc3

Released 2021-Feb-04

* Moved `OtlpTraceExporter` and `OtlpExporterOptions` classes to
  `OpenTelemetry.Exporter` namespace.
  ([#1770](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1770))
* Changed default port for OTLP Exporter from 55680 to 4317
* Default ServiceName, if not found in Resource, is obtained from SDK using
  GetDefaultResource().
* Modified the data type of Headers option to string; Added a new option called
  TimeoutMilliseconds for computing the `deadline` to be used by gRPC client for
  `Export`
  ([#1781](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1781))
* Removes Grpc specific options from OTLPExporterOptions, which removes support
  for secure connections. See [1778](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1778)
  for details.
* Endpoint is made Uri for all target frameworks.

## 1.0.0-rc2

Released 2021-Jan-29

* Changed `OtlpTraceExporter` class and constructor from internal to public.
  ([#1612](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1612))

* In `OtlpExporterOptions.cs`: Exporter options now include a switch for Batch
  vs Simple exporter, and settings for batch exporting properties.

* Introduce a `netstandard2.1` build enabling the exporter to use the [gRPC for
  .NET](https://github.com/grpc/grpc-dotnet) library instead of the [gRPC for
  C#](https://github.com/grpc/grpc/tree/master/src/csharp) library for .NET Core
  3.0+ applications. This required some breaking changes to the
  `OtlpExporterOptions`.
  ([#1662](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1662))

## 1.0.0-rc1.1

Released 2020-Nov-17

* Code generated from proto files has been marked internal. This includes
  everything under the `OpenTelemetry.Proto` namespace.
  ([#1524](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1524))
* The `OtlpExporter` class has been made internal.
  ([#1528](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1528))
* Removed `ServiceName` from options available on the `AddOtlpExporter`
  extension. It is not required by the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#configuration-options).
  ([#1557](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1557))

## 0.8.0-beta.1

Released 2020-Nov-5

* `peer.service` tag is now added to outgoing spans (went not already specified)
  following the [Zipkin remote endpoint
  rules](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/zipkin.md#remote-endpoint)
  ([#1392](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1392))
* Added `ServiceName` to options available on the `AddOtlpExporter` extension
  ([#1420](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1420))

## 0.7.0-beta.1

Released 2020-Oct-16

## 0.6.0-beta.1

Released 2020-Sep-15

## 0.5.0-beta.2

Released 2020-08-28

* Allow configurable gRPC channel options
  ([#1033](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1033))
* Renamed extension method from `UseOtlpExporter` to `AddOtlpExporter`
  ([#1066](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1066))
* Changed `OtlpExporter` to use `BatchExportActivityProcessor` by default
  ([#1104](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1104))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
