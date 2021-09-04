# Changelog

## Unreleased

* `JaegerExporterOptions.BatchExportProcessorOptions` is initialized with
  `BatchExportActivityProcessorOptions` which supports field value overriding
  using `OTEL_BSP_SCHEDULE_DELAY`, `OTEL_BSP_EXPORT_TIMEOUT`,
  `OTEL_BSP_MAX_QUEUE_SIZE`, `OTEL_BSP_MAX_EXPORT_BATCH_SIZE`
  envionmental variables as defined in the
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
  envionmental variables as defined in the
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
  ([#1579](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1579) &
  [#1620](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1620))

* Jaeger will no longer send the `otel.status_code` tag if the value is `UNSET`.
  ([#1609](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1609) &
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

* Changed `JaegerExporter` to use `BatchExportActivityProcessor` by default
  ([#1125](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1125))
* Span links will now be sent as `FOLLOWS_FROM` reference type. Previously they
  were sent as `CHILD_OF`.
  ([#970](https://github.com/open-telemetry/opentelemetry-dotnet/pull/970))
* Fixed issue when span has both the `net.peer.name` and `net.peer.port`
  attributes but did not include `net.peer.port` in the `peer.service` field
  ([#1195](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1195)).

* Renamed extension method from `UseJaegerExporter` to `AddJaegerExporter`.

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
