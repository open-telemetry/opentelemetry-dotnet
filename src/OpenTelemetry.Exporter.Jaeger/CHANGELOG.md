# Changelog

## Unreleased

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

* Enabled performance optimizations for .NET 6.0+ runtimes.
  ([#4349](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4349))

## 1.5.0-alpha.1

Released 2023-Mar-07

## 1.4.0

Released 2023-Feb-24

* Updated OTel SDK dependency to 1.4.0

## 1.4.0-rc.4

Released 2023-Feb-10

## 1.4.0-rc.3

Released 2023-Feb-01

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
  using the `AddJaegerExporter` extension
  ([#3720](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3720))

## 1.4.0-beta.1

Released 2022-Sep-29

* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddJaegerExporter` extension to allow for more fine-grained options
  management
  ([#3656](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3656))

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
  array-valued attributes are serialzed to a JSON-array representation.
  ([#3281](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3281))

## 1.3.0-beta.2

Released 2022-May-16

* Removes net5.0 target and replaced with net6.0
  as .NET 5.0 is going out of support.
  The package keeps netstandard2.1 target, so it
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
 ([#3073](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3073))

## 1.2.0-rc3

Released 2022-Mar-04

* Change supported values for `OTEL_EXPORTER_JAEGER_PROTOCOL`
  Supported values: `udp/thrift.compact` and `http/thrift.binary` defined
  in the [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/9a0a3300c6269c2837a1d7c9c5232ec816f63222/specification/sdk-environment-variables.md?plain=1#L129).
  ([#2914](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2914))
* Change handling of `OTEL_EXPORTER_JAEGER_ENDPOINT` to require the path to
  post. Previous versions of this library would append `/api/traces` to the
  value specified in this variable, but now the application author must do so.
  This change must also be made if you manually configure the
  `JaegerExporterOptions` class - the `Endpoint` must now include the path.
  For most environments, this will be `/api/traces`. The effective default
  is still `http://localhost:14268/api/traces`. This was done to match
  the clarified [specification](https://github.com/open-telemetry/opentelemetry-specification/pull/2333))
  ([#2847](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2847))

## 1.2.0-rc2

Released 2022-Feb-02

* Improved span duration's precision from millisecond to microsecond
  ([#2814](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2814))

## 1.2.0-rc1

Released 2021-Nov-29

## 1.2.0-beta2

Released 2021-Nov-19

* Changed `JaegerExporterOptions` constructor to throw
  `FormatException` if it fails to parse any of the supported environment
  variables.

* Added support for sending spans directly to a Jaeger Collector over HTTP
  ([#2574](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2574))

## 1.2.0-beta1

Released 2021-Oct-08

## 1.2.0-alpha4

Released 2021-Sep-23

## 1.2.0-alpha3

Released 2021-Sep-13

* `JaegerExporterOptions.BatchExportProcessorOptions` is initialized with
  `BatchExportActivityProcessorOptions` which supports field value overriding
  using `OTEL_BSP_SCHEDULE_DELAY`, `OTEL_BSP_EXPORT_TIMEOUT`,
  `OTEL_BSP_MAX_QUEUE_SIZE`, `OTEL_BSP_MAX_EXPORT_BATCH_SIZE`
  environmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/sdk-environment-variables.md#batch-span-processor).
  ([#2219](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2219))

## 1.2.0-alpha2

Released 2021-Aug-24

## 1.2.0-alpha1

Released 2021-Jul-23

* Removes .NET Framework 4.6 support. The minimum .NET Framework
  version supported is .NET 4.6.1. ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

* The `JaegerExporterOptions` defaults can be overridden using
  `OTEL_EXPORTER_JAEGER_AGENT_HOST` and `OTEL_EXPORTER_JAEGER_AGENT_PORT`
  environmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md#jaeger-exporter).
  ([#2123](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2123))

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
  `JaegerExporterOptions` to `IConfiguration` using the `Configure` extension
  (ex:
  `services.Configure<JaegerExporterOptions>(this.Configuration.GetSection("Jaeger"));`).
  ([#1889](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1889))
* Fixed data corruption when creating Jaeger Batch messages
  ([#1372](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1372))

## 1.1.0-beta1

Released 2021-Mar-19

## 1.0.1

Released 2021-Feb-10

## 1.0.0-rc4

Released 2021-Feb-09

## 1.0.0-rc3

Released 2021-Feb-04

* Moved `JaegerExporter` and `JaegerExporterOptions` classes to
  `OpenTelemetry.Exporter` namespace.
  ([#1770](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1770))
* Default ServiceName, if not found in Resource is obtained from SDK
  using GetDefaultResource().
  [#1768](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1768)
* Removed ProcessTags from JaegerExporterOptions. The alternate option is
  to use Resource attributes.

## 1.0.0-rc2

Released 2021-Jan-29

* Changed `JaegerExporter` class and constructor from internal to public.
  ([#1612](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1612))

* In `JaegerExporterOptions`: Exporter options now include a switch for Batch vs
  Simple exporter, and settings for batch exporting properties.

* Jaeger will now set the `error` tag when `otel.status_code` is set to `ERROR`.
  ([#1579](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1579)
  [#1620](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1620))

* Jaeger will no longer send the `otel.status_code` tag if the value is `UNSET`.
  ([#1609](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1609)
  [#1620](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1620))

* Span Event.Name will now be populated as the `event` field on Jaeger Logs
  instead of `message`.
  ([#1609](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1609))

* `JaegerExporter` batch format has changed to be compliant with the spec. This
  may impact the way spans are displayed in Jaeger UI.
  ([#1732](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1732))

## 1.0.0-rc1.1

Released 2020-Nov-17

* Jaeger tags used for InstrumentationLibrary changed from library.name,
  library.version to otel.library.name, otel.library.version respectively.
  ([#1513](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1513))
* The `JaegerExporter` class has been made internal.
  ([#1540](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1540))
* Removed `ServiceName` from options available on the `AddJaegerExporter`
  extension. It is not required by the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/jaeger.md).
  ([#1572](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1572))

## 0.8.0-beta.1

Released 2020-Nov-5

* Moving Jaeger Process from public to internal.
  ([#1421](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1421))

## 0.7.0-beta.1

Released 2020-Oct-16

* Renamed `MaxPacketSize` -> `MaxPayloadSizeInBytes` on `JaegerExporterOptions`.
  Lowered the default value from 65,000 to 4096.
  ([#1247](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1274))

## 0.6.0-beta.1

Released 2020-Sep-15

* Removed `MaxFlushInterval` from `JaegerExporterOptions`. Batching is now
  handled  by `BatchExportActivityProcessor` exclusively.
  ([#1254](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1254))

## 0.5.0-beta.2

Released 2020-08-28

* Changed `JaegerExporter` to use `BatchExportActivityProcessor` by default.
  ([#1125](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1125))
* Span links will now be sent as `FOLLOWS_FROM` reference type. Previously they
  were sent as `CHILD_OF`.
  ([#970](https://github.com/open-telemetry/opentelemetry-dotnet/pull/970))
* Fixed issue when span has both the `net.peer.name` and `net.peer.port`
  attributes but did not include `net.peer.port` in the `peer.service` field.
  ([#1195](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1195))

* Renamed extension method from `UseJaegerExporter` to `AddJaegerExporter`.

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
