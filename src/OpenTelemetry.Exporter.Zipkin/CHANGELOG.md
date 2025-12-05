# Changelog

This file contains individual changes for the OpenTelemetry.Exporter.Zipkin
package. For highlights and announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

## 1.14.0

Released 2025-Nov-12

* **Breaking Change** NuGet packages now use the Sigstore bundle format
  (`.sigstore.json`) for digital signatures instead of separate signature
  (`.sig`) and certificate (`.pem`) files. This requires cosign 3.0 or later
  for verification. See the [Digital signing
  section](../../README.md#digital-signing) for updated verification instructions.
  ([#6623](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6623))

## 1.14.0-rc.1

Released 2025-Oct-21

* **Breaking Change** When targeting `net8.0`, the package now depends on version
  `8.0.0` of the `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Diagnostics.Abstractions` and
  `Microsoft.Extensions.Logging.Configuration` NuGet packages.
  ([#6327](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6327))

* Add support for .NET 10.0.
  ([#6307](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6307))

## 1.13.1

Released 2025-Oct-09

## 1.13.0

Released 2025-Oct-01

* Removed the peer service resolver, which was based on earlier experimental
  semantic conventions that are not part of the stable specification. This
  change ensures that the exporter no longer modifies or assumes the value of
  peer service attributes.
  ([#6191](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6191))

* Extended remote endpoint calculation to align with the [opentelemetry-specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.40.0/specification/trace/sdk_exporters/zipkin.md#otlp---zipkin).
  ([#6191](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6191))

## 1.12.0

Released 2025-Apr-29

## 1.11.2

Released 2025-Mar-04

## 1.11.1

Released 2025-Jan-22

## 1.11.0

Released 2025-Jan-15

## 1.11.0-rc.1

Released 2024-Dec-11

## 1.10.0

Released 2024-Nov-12

## 1.10.0-rc.1

Released 2024-Nov-01

* Added direct reference to `System.Text.Json` for the `net8.0` target with
  minimum version of `8.0.5` in response to
  [CVE-2024-30105](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w) &
  [CVE-2024-43485](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2024-43485).
  ([#5874](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5874),
  [#5891](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5891))

## 1.10.0-beta.1

Released 2024-Sep-30

* **Breaking change**: Non-primitive tag values converted using
  `Convert.ToString` will now format using `CultureInfo.InvariantCulture`.
  ([#5700](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5700))

* Fixed `PlatformNotSupportedException`s being thrown during export when running
  on mobile platforms which caused telemetry to be dropped silently.
 ([#5821](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5821))

## 1.9.0

Released 2024-Jun-14

## 1.9.0-rc.1

Released 2024-Jun-07

## 1.9.0-alpha.1

Released 2024-May-20

## 1.8.1

Released 2024-Apr-17

## 1.8.0

Released 2024-Apr-02

## 1.8.0-rc.1

Released 2024-Mar-27

* Zipkin tags used for Instrumentation Library changed from `otel.library.name` and
  `otel.library.version` to `otel.scope.name` and `otel.scope.version` respectively.
  Old versions of attributes are deprecated, but still exported
  for [backward compatibility](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.31.0/specification/common/mapping-to-non-otlp.md#instrumentationscope).
  ([#5473](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5473))

## 1.8.0-beta.1

Released 2024-Mar-14

## 1.7.0

Released 2023-Dec-08

## 1.7.0-rc.1

Released 2023-Nov-29

## 1.7.0-alpha.1

Released 2023-Oct-16

## 1.6.0

Released 2023-Sep-05

## 1.6.0-rc.1

Released 2023-Aug-21

## 1.6.0-alpha.1

Released 2023-Jul-12

## 1.5.1

Released 2023-Jun-26

## 1.5.0

Released 2023-Jun-05

## 1.5.0-rc.1

Released 2023-May-25

* Added direct reference to `System.Text.Encodings.Web` with minimum version of
`4.7.2` in response to [CVE-2021-26701](https://github.com/dotnet/runtime/issues/49377).

## 1.5.0-alpha.2

Released 2023-Mar-31

## 1.5.0-alpha.1

Released 2023-Mar-07

## 1.4.0

Released 2023-Feb-24

* Updated OTel SDK dependency to 1.4.0

## 1.4.0-rc.4

Released 2023-Feb-10

## 1.4.0-rc.3

Released 2023-Feb-01

* Changed EnvironmentVariable parsing to not throw a `FormatException` and
  instead log a warning.
  ([#4095](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4095))

## 1.4.0-rc.2

Released 2023-Jan-09

## 1.4.0-rc.1

Released 2022-Dec-12

## 1.4.0-beta.3

Released 2022-Nov-07

* Bumped the minimum required version of `System.Text.Json` to 4.7.2 in response
to [CVE-2021-26701](https://github.com/dotnet/runtime/issues/49377).
([#3789](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3789))

## 1.4.0-beta.2

Released 2022-Oct-17

* Added support for loading environment variables from `IConfiguration` when
  using the `AddZipkinExporter` extension
  ([#3759](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3759))

## 1.4.0-beta.1

Released 2022-Sep-29

* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddZipkinExporter` extension to allow for more fine-grained options
  management
  ([#3655](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3655))

## 1.4.0-alpha.2

Released 2022-Aug-18

## 1.4.0-alpha.1

Released 2022-Aug-02

## 1.3.0

Released 2022-Jun-03

## 1.3.0-rc.2

Released 2022-June-1

* Improve the conversion and formatting of attribute values.
  The list of data types that must be supported per the
  [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/common#attribute)
  is more narrow than what the .NET OpenTelemetry SDK supports. Numeric
  [built-in value types](https://docs.microsoft.com/dotnet/csharp/language-reference/builtin-types/built-in-types)
  are supported by converting to a `long` or `double` as appropriate except for
  numeric types that could cause overflow (`ulong`) or rounding (`decimal`)
  which are converted to strings. Non-numeric built-in types - `string`,
  `char`, `bool` are supported. All other types are converted to a `string`.
  Array values are also supported.
  ([#3281](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3281))
* Fix conversion of array-valued resource attributes. They were previously
  converted to a string like "System.String[]".
  ([#3281](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3281))
* Fix exporting of array-valued attributes on an `Activity`. Previously, each
  item in the array would result in a new tag on an exported `Activity`. Now,
  array-valued attributes are serialized to a JSON-array representation.
  ([#3281](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3281))

## 1.3.0-beta.2

Released 2022-May-16

* Removes net5.0 target and replaced with net6.0
  as .NET 5.0 is going out of support.
  The package keeps netstandard2.0 target, so it
  can still be used with .NET5.0 apps.
  ([#3147](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3147))

## 1.3.0-beta.1

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework
  version supported is .NET 4.6.2. ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.2.0

Released 2022-Apr-15

## 1.2.0-rc5

Released 2022-Apr-12

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
 ([#3003](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3003))

## 1.2.0-rc3

Released 2022-Mar-04

* Modified Export method to catch all exceptions.
  ([#2935](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2935))

## 1.2.0-rc2

Released 2022-Feb-02

## 1.2.0-rc1

Released 2021-Nov-29

## 1.2.0-beta2

Released 2021-Nov-19

* Changed `ZipkinExporterOptions` constructor to throw
  `FormatException` if it fails to parse any of the supported environment
  variables.

* Added `HttpClientFactory` option
  ([#2654](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2654))

## 1.2.0-beta1

Released 2021-Oct-08

* Added .NET 5.0 target and threading optimizations
  ([#2405](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2405))

## 1.2.0-alpha4

Released 2021-Sep-23

## 1.2.0-alpha3

Released 2021-Sep-13

* `ZipkinExporterOptions.BatchExportProcessorOptions` is initialized with
  `BatchExportActivityProcessorOptions` which supports field value overriding
  using `OTEL_BSP_SCHEDULE_DELAY`, `OTEL_BSP_EXPORT_TIMEOUT`,
  `OTEL_BSP_MAX_QUEUE_SIZE`, `OTEL_BSP_MAX_EXPORT_BATCH_SIZE`
  environmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/sdk-environment-variables.md#batch-span-processor).
  ([#2219](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2219))

## 1.2.0-alpha2

Released 2021-Aug-24

* Enabling endpoint configuration in ZipkinExporterOptions via
  `OTEL_EXPORTER_ZIPKIN_ENDPOINT` environment variable.
  ([#1453](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1453))

## 1.2.0-alpha1

Released 2021-Jul-23

* Removes .NET Framework 4.5.2, .NET 4.6 support. The minimum .NET Framework
  version supported is .NET 4.6.1. ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

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

* When using OpenTelemetry.Extensions.Hosting you can now bind
  `ZipkinExporterOptions` to `IConfiguration` using the `Configure` extension
  (ex:
  `services.Configure<ZipkinExporterOptions>(this.Configuration.GetSection("Zipkin"));`).
  ([#1889](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1889))

## 1.1.0-beta1

Released 2021-Mar-19

## 1.0.1

Released 2021-Feb-10

## 1.0.0-rc4

Released 2021-Feb-09

## 1.0.0-rc3

Released 2021-Feb-04

* Moved `ZipkinExporter` and `ZipkinExporterOptions` classes to
  `OpenTelemetry.Exporter` namespace.
  ([#1770](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1770))
* Removes ability to configure ServiceName for Zipkin. ServiceName must come
  via Resource. If service name is not found in Resource, Zipkin uses
  GetDefaultResource() from the SDK to obtain it.
  [#1768](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1768)

## 1.0.0-rc2

Released 2021-Jan-29

* Changed `ZipkinExporter` class and constructor from internal to public.
  ([#1612](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1612))

* Zipkin will now set the `error` tag to the `Status.Description` value or an
  empty string when `Status.StatusCode` (`otel.status_code` tag) is set to
  `ERROR`.
  ([#1579](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1579)
  [#1620](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1620)
  [#1655](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1655))

* Zipkin will no longer send the `otel.status_code` tag if the value is `UNSET`.
  ([#1609](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1609)
  [#1620](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1620))

* Zipkin bool tag values will now be sent as `true`/`false` instead of
  `True`/`False`.
  ([#1609](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1609))

* Span tags will no longer be populated with Resource Attributes.
  ([#1663](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1663))

* Spans will no longer be held in memory indefinitely when `ZipkinExporter`
  cannot connect to the configured endpoint.
  ([#1726](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1726))

## 1.0.0-rc1.1

Released 2020-Nov-17

* Added ExportProcessorType to exporter options
  ([#1504](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1504))
* Zipkin tags used for InstrumentationLibrary changed from library.name,
  library.version to otel.library.name, otel.library.version respectively.
* Sending `service.namespace` as Zipkin tag.
  ([#1521](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1521))
* The `ZipkinExporter` class has been made internal.
  ([#1540](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1540))

## 0.8.0-beta.1

Released 2020-Nov-5

* ZipkinExporter will now respect global Resource set via
  `TracerProviderBuilder.SetResource`.
  ([#1385](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1385))

## 0.7.0-beta.1

Released 2020-Oct-16

* Removed unused `TimeoutSeconds` and added `MaxPayloadSizeInBytes` on
  `ZipkinExporterOptions`. The default value for `MaxPayloadSizeInBytes` is
  4096.
  ([#1247](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1274))

## 0.6.0-beta.1

Released 2020-Sep-15

## 0.5.0-beta.2

Released 2020-08-28

* Renamed extension method from `UseZipkinExporter` to `AddZipkinExporter`.
  ([#1066](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1066))
* Changed `ZipkinExporter` to use `BatchExportActivityProcessor` by default.
  ([#1103](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1103))
* Fixed issue when span has both the `net.peer.name` and `net.peer.port`
  attributes but did not include `net.peer.port` in the service address field.
  ([#1168](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1168))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
