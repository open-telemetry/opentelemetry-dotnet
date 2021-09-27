# Changelog

## Unreleased

* `MeterProviderBuilder` extension methods now support `OtlpExporterOptions`
  bound to `IConfiguration` when using OpenTelemetry.Extensions.Hosting
  ([#2413](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2413))

## 1.2.0-alpha4

Released 2021-Sep-23

## 1.2.0-alpha3

Released 2021-Sep-13

* `OtlpExporterOptions.BatchExportProcessorOptions` is initialized with
  `BatchExportActivityProcessorOptions` which supports field value overriding
  using `OTEL_BSP_SCHEDULE_DELAY`, `OTEL_BSP_EXPORT_TIMEOUT`,
  `OTEL_BSP_MAX_QUEUE_SIZE`, `OTEL_BSP_MAX_EXPORT_BATCH_SIZE`
  envionmental variables as defined in the
  [specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/sdk-environment-variables.md#batch-span-processor).
  ([#2219](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2219))

## 1.2.0-alpha2

Released 2021-Aug-24

* The `OtlpExporterOptions` defaults can be overridden using
  `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_HEADERS` and `OTEL_EXPORTER_OTLP_TIMEOUT`
  envionmental variables as defined in the
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
  ([#1919](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1919)) and
  ([#1945](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1945)).

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

* Changed `OltpTraceExporter` class and constructor from internal to public.
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
