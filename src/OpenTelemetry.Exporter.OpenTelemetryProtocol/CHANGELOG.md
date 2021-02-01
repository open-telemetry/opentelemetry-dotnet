# Changelog

## Unreleased

## 1.0.0-rc2

Released 2021-Jan-29

* Changed `OltpTraceExporter` class and constructor from internal to public.
  ([#1612](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1612))

* In `OtlpExporterOptions.cs`: Exporter options now include a switch for
  Batch vs Simple exporter, and settings for batch exporting properties.

* Introduce a `netstandard2.1` build enabling the exporter to use the
  [gRPC for .NET](https://github.com/grpc/grpc-dotnet) library instead of the
  [gRPC for C#](https://github.com/grpc/grpc/tree/master/src/csharp) library
  for .NET Core 3.0+ applications. This required some breaking changes to the
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
