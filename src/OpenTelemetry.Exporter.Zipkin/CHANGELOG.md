# Changelog

## Unreleased

* Added ExportProcessorType to exporter options
  ([#1504](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1504))
* Zipkin tags used for InstrumentationLibrary changed from library.name,
  library.version to otel.library.name, otel.library.version respectively.
* Sending `service.namespace` as Zipkin tag.
  ([#1521](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1521))

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

* Renamed extension method from `UseZipkinExporter` to `AddZipkinExporter`
  ([#1066](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1066))
* Changed `ZipkinExporter` to use `BatchExportActivityProcessor` by default
  ([#1103](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1103))
* Fixed issue when span has both the `net.peer.name` and `net.peer.port`
  attributes but did not include `net.peer.port` in the service address field
  ([#1168](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1168)).

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
