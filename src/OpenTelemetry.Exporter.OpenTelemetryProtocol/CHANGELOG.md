# Changelog

## Unreleased

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
`event.id`, `LogRecord.EventId.Name` as `event.name` and
`LogRecord.CategoryName` as `dotnet.ilogger.category`.

  * The attributes for `LogRecord.EventId.Id` and  `LogRecord.EventId.Name` will
be exported when `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES` will
be set to `true`.

  * The attribute for `LogRecord.CategoryName` will be exported when
`OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_CATEGORY_LOG_ATTRIBUTE` will be set to
`true`.

  **NOTE**: These attributes were removed in [1.6.0-rc.1](#160-rc1) release in
  order to support stable release of OTLP Log Exporter. The attributes will now be
  available via environment variables mentioned above.
  ([#4925](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4925))

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
  [Attribute Limits](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#attribute-limits)
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
  Limits](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#attribute-limits)
  the OTLP exporter will now send "dropped" counts where applicable (ex:
  [dropped_attributes_count](https://github.com/open-telemetry/opentelemetry-proto/blob/001e5eabf3ea0193ef9343c1b9a057d23d583d7c/opentelemetry/proto/trace/v1/trace.proto#L191)).
  ([#3580](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3580))

## 1.4.0-alpha.1

Released 2022-Aug-02

* Adds support for limiting the length and count of attributes exported from
  the OTLP exporter. These
  [Attribute Limits](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#attribute-limits)
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
  [spec](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/common/common.md).
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
