# Changelog

## Unreleased

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
