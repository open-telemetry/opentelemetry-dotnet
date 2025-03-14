# Changelog

This file contains individual changes for the OpenTelemetry.Api package. For
highlights and announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

* Added a new overload for `TracerProvider.GetTracer` which accepts an optional
  `IEnumerable<KeyValuePair<string, object?>>? tags` parameter, allowing
  additional attributes to be associated with the `Tracer`.
  ([#6137](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6137))

## 1.11.2

Released 2025-Mar-04

* Revert optimize performance of `TraceContextPropagator.Extract` introduced
  in #5749 to resolve [GHSA-8785-wc3w-h8q6](https://github.com/open-telemetry/opentelemetry-dotnet/security/advisories/GHSA-8785-wc3w-h8q6).
  ([#6161](https://github.com/open-telemetry/opentelemetry-dotnet/pull/6161))

## 1.11.1

Released 2025-Jan-22

## 1.11.0

Released 2025-Jan-15

## 1.11.0-rc.1

Released 2024-Dec-11

## 1.10.0

Released 2024-Nov-12

* Updated `System.Diagnostics.DiagnosticSource` package version to
  `9.0.0`.
  ([#5967](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5967))

## 1.10.0-rc.1

Released 2024-Nov-01

## 1.10.0-beta.1

Released 2024-Sep-30

* **Breaking change:** CompositeTextMapPropagator.Fields now returns a
  unioned set of fields from all combined propagators. Previously this always
  returned an empty set.
  ([#5745](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5745))

* Optimize performance of `TraceContextPropagator.Extract`.
  ([#5749](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5749))

* Obsoleted the `ActivityExtensions.GetStatus` and
  `ActivityExtensions.SetStatus` extension methods. Users should migrate to the
  `System.Diagnostics.DiagnosticSource`
  [Activity.SetStatus](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.setstatus)
  API for setting the status and
  [Activity.Status](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.status)
  &
  [Activity.StatusDescription](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.statusdescription)
  APIs for reading the status of an `Activity` instance.
  ([#5781](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5781))

* Updated `System.Diagnostics.DiagnosticSource` package version to
  `9.0.0-rc.1.24431.7`.
  ([#5853](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5853))

* Obsoleted the `ActivityExtensions.RecordException` extension method. Users
  should migrate to the `System.Diagnostics.DiagnosticSource`
  [Activity.AddException](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.addexception)
  API for adding exceptions on an `Activity` instance.
  ([#5841](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5841))

## 1.9.0

Released 2024-Jun-14

* **Breaking change:** Revert space character encoding change from `+` to `%20`
  for baggage item values from [#5303](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5303)
  ([#5687](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5687))

## 1.9.0-rc.1

Released 2024-Jun-07

* The experimental APIs previously covered by `OTEL1000` (`LoggerProvider`,
  `LoggerProviderBuilder`, & `IDeferredLoggerProviderBuilder`) are now part of
  the public API and supported in stable builds.
  ([#5648](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5648))

## 1.9.0-alpha.1

Released 2024-May-20

* **Breaking change:** Fix space character encoding from `+` to `%20`
  for baggage item values when propagating baggage as defined in
  [W3C Baggage propagation format specification](https://www.w3.org/TR/baggage/).
  ([#5303](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5303))

## 1.8.1

Released 2024-Apr-17

## 1.8.0

Released 2024-Apr-02

## 1.8.0-rc.1

Released 2024-Mar-27

## 1.8.0-beta.1

Released 2024-Mar-14

## 1.7.0

Released 2023-Dec-08

## 1.7.0-rc.1

Released 2023-Nov-29

* Updated `System.Diagnostics.DiagnosticSource` package version to
  `8.0.0`.
  ([#5051](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5051))

## 1.7.0-alpha.1

Released 2023-Oct-16

* Fixed a bug which caused `Tracer.StartRootSpan` to generate a child span if a
  trace was running (`Activity.Current != null`).
  ([#4890](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4890))

* Added a `Tracer` cache inside of `TracerProvider` to prevent repeated calls to
  `GetTracer` from leaking memory.
  ([#4906](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4906))

* Fix `TraceContextPropagator` by validating the first digit of the hex-encoded
  `trace-flags` field of the `traceparent` header.
  ([#4893](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4893))

## 1.6.0

Released 2023-Sep-05

## 1.6.0-rc.1

Released 2023-Aug-21

## 1.6.0-alpha.1

Released 2023-Jul-12

* Updated `System.Diagnostics.DiagnosticSource` package version to `7.0.2`.
  ([#4576](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4576))

* **Breaking change:** In order to make `RuntimeContext` compatible with
  ahead-of-time compilation (AOT), `RuntimeContext.ContextSlotType` can only be
  assigned one of the following types: `AsyncLocalRuntimeContextSlot<>`,
  `ThreadLocalRuntimeContextSlot<>`, and `RemotingRuntimeContextSlot<>`. A
  `System.NotSupportedException` will be thrown if you try to assign any type
  other than the three types mentioned.
  ([#4542](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4542))

* **Experimental (pre-release builds only):** Added [Logs Bridge
  API](https://github.com/open-telemetry/opentelemetry-specification/blob/976432b74c565e8a84af3570e9b82cb95e1d844c/specification/logs/bridge-api.md)
  implementation (`LoggerProviderBuilder`, `LoggerProvider`, `Logger`, etc.).
  ([#4433](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4433),
  [#4735](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4735))

## 1.5.1

Released 2023-Jun-26

## 1.5.0

Released 2023-Jun-05

## 1.5.0-rc.1

Released 2023-May-25

## 1.5.0-alpha.2

Released 2023-Mar-31

## 1.5.0-alpha.1

Released 2023-Mar-07

## 1.4.0

Released 2023-Feb-24

## 1.4.0-rc.4

Released 2023-Feb-10

## 1.4.0-rc.3

Released 2023-Feb-01

## 1.4.0-rc.2

Released 2023-Jan-09

## 1.4.0-rc.1

Released 2022-Dec-12

* Updated to System.Diagnostics.DiagnosticSource version `7.0.0`.

## 1.4.0-beta.3

Released 2022-Nov-07

* Updated to System.Diagnostics.DiagnosticSource version `7.0.0-rc.2.22472.3`.

## 1.4.0-beta.2

Released 2022-Oct-17

## 1.4.0-beta.1

Released 2022-Sep-29

* Updated to System.Diagnostics.DiagnosticSource version `7.0.0-rc.1.22426.10`.
([#3698](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3698))

## 1.4.0-alpha.2

Released 2022-Aug-18

* Updated to System.Diagnostics.DiagnosticSource preview version 7.0.0.

  With this update, applications targeting .NET 5 and lower will receive a
  warning at build time as described [here](https://github.com/dotnet/runtime/pull/72518)
  (note: building using older versions of the .NET SDK produces an error at
  build time). This is because .NET 5 reached EOL in May 2022 and .NET
  Core 3.1 reaches EOL in December 2022. End of support
  dates for .NET are published
  [here](https://dotnet.microsoft.com/download/dotnet).

  There is no guarantee that System.Diagnostics.DiagnosticSource will continue
  to work on older versions of .NET. However, the build warning can be
  suppressed by setting the `SuppressTfmSupportBuildWarnings` MSBuild property.

  This does not affect applications targeting .NET Framework.
  [#3539](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3539)

## 1.4.0-alpha.1

Released 2022-Aug-02

* Add `Activity.RecordException` overload accepting additional attributes to
  add to the `ActivityEvent`.
  [#3433](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3433)

## 1.3.0

Released 2022-Jun-03

## 1.3.0-rc.2

Released 2022-June-1

* `B3Propagator` class from `OpenTelemetry.Extensions.Propagators` namespace has
  been deprecated and moved as is to a new `OpenTelemetry.Extensions.Propagators`
  namespace, shipped as part of the `OpenTelemetry.Extensions.Propagators` package.
  It will be removed in the next major release, see issue [#3259](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3259)

## 1.3.0-beta.2

Released 2022-May-16

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

## 1.2.0-rc3

Released 2022-Mar-04

* Improved wildcard support for `AddSource`, `AddMeter` to cover `?` (which
 matches exactly one character).
 ([#2875](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2875))

## 1.2.0-rc2

Released 2022-Feb-02

* Added `ParentSpanId` to `TelemetrySpan` ([#2740](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2740))

## 1.2.0-rc1

Released 2021-Nov-29

## 1.2.0-beta2

Released 2021-Nov-19

* Updated System.Diagnostics.DiagnosticSource to version 6.0.0.
  ([#2582](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2582))

## 1.2.0-beta1

Released 2021-Oct-08

* Added `IDeferredMeterProviderBuilder`
  ([#2412](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2412))

* Breaking: Renamed `AddSource` to `AddMeter` on MeterProviderBuilder
  to better reflect the intent of the method.

## 1.2.0-alpha4

Released 2021-Sep-23

* Updated System.Diagnostics.DiagnosticSource to version 6.0.0-rc.1.21451.13

## 1.2.0-alpha3

Released 2021-Sep-13

* Static Baggage operations (`SetBaggage`, `RemoveBaggage`, & `ClearBaggage`)
  are now thread-safe. Instance-based Baggage operations no longer mutate
  `Baggage.Current` (breaking behavior change). For details see:
  ([#2298](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2298))

## 1.2.0-alpha2

Released 2021-Aug-24

## 1.2.0-alpha1

Released 2021-Jul-23

* Add Metrics support.([#2174](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2174))

* Removes .NET Framework 4.5.2, .NET 4.6 support. The minimum .NET Framework
  version supported is .NET 4.6.1. ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

## 1.1.0

Released 2021-Jul-12

## 1.1.0-rc1

Released 2021-Jun-25

* Added `IDeferredTracerProviderBuilder`.
  ([#2058](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2100))

## 1.1.0-beta4

Released 2021-Jun-09

## 1.1.0-beta3

Released 2021-May-11

* Adds `AddLegacySource()` to `TracerProviderBuilder`
  ([#2019](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2019))

## 1.1.0-beta2

Released 2021-Apr-23

* `BaggagePropagator` now uses `baggage` as the header name instead of `Baggage`
  to `Extract` from and `Inject` to `carrier`
  ([#2003](https://github.com/open-telemetry/opentelemetry-dotnet/pull/2003))

## 1.1.0-beta1

Released 2021-Mar-19

## 1.0.1

Released 2021-Feb-10

## 1.0.0-rc4

Released 2021-Feb-09

## 1.0.0-rc3

Released 2021-Feb-04

* Relax System.* packages version requirement to remove upper bound.
* Require System.Diagnostics.DiagnosticSource package 5.0.1.

## 1.0.0-rc2

Released 2021-Jan-29

* In order to align with the
  [spec](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status)
  the `Status` (otel.status_code) tag (added on `Activity` using the `SetStatus`
  extension) will now be set as the `UNSET`, `OK`, or `ERROR` string
  representation instead of the `0`, `1`, or `2` integer representation.
  ([#1579](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1579)
  [#1620](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1620))
* Metrics API/SDK support is in an experimental state and is not recommended for
  production use. All metric APIs have been marked with the `Obsolete`
  attribute. See
  [#1501](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501)
  for more information.
  ([#1611](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1611))
* `Status.WithDescription` will now ignore the provided description if the
  `Status.StatusCode` is anything other than `ERROR`.
  ([#1655](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1655))
* Relax System.Diagnostics.DiagnosticSource version requirement to allow
  versions >=5.0. Previously only versions up to 6.0 (excluding 6.0) was
  allowed.
* Metrics removed as it is not part 1.0.0 release. See issue [#1501](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1655)
  for details on Metric release plans.

## 1.0.0-rc1.1

Released 2020-Nov-17

* Updated System.Diagnostics.DiagnosticSource to version 5.0.0
* Mark Activity extension methods as internal as these are not required to be
  public. GetTagValue, EnumerateTags, EnumerateLinks, EnumerateEvents. See
  [#1544](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1544)
  for full changes.
* Changed SpanHelper class from public to internal. Moved SpanHelper.cs to
  OpenTelemetry.Api\Internal
  ([#1555](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1555))

## 0.8.0-beta.1

Released 2020-Nov-5

* Removed `IsValid` property from `Status`
  ([#1415](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1415))
* Removed `IsOk` property from `Status` and fixed `StatusCode` enum values
  ([#1414](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1414))
* `B3Propagator` now supports the value `true` to be passed in for the header
  `X-B3-Sampled`.
  ([#1413](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1413))
* Moving grpc status and helper to grpc project
  ([#1422](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1422))
* Renamed TextMapPropagator to TraceContextPropagator, CompositePropagator to
  CompositeTextMapPropagator. IPropagator is renamed to TextMapPropagator and
  changed from interface to abstract class.
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1427))
* Added GlobalPropagators API via Propagators.DefaultTextMapPropagator.
  ([#1427](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1428))
* Changed SpanAttributeConstants from public to internal
  ([#1457](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1457))

## 0.7.0-beta.1

Released 2020-Oct-16

* `IActivityTagEnumerator` is now `IActivityEnumerator<T>`. Added
  `EnumerateLinks` extension method on `Activity` for retrieving links
  efficiently
  ([#1314](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1314))
* Added `EnumerateEvents` extension method on `Activity` for retrieving events
  efficiently
  ([#1319](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1319))
* Added `EnumerateTags` extension methods on `ActivityLink` & `ActivityEvent`
  for retrieving tags efficiently. Renamed `Activity.EnumerateTagValues` ->
  `Activity.EnumerateTags`.
  ([#1320](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1320))
* Updated System.Diagnostics.DiagnosticSource to version 5.0.0-rc.2.20475.5
  ([#1346](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1346))
* Updated Span Status as per new spec
  ([#1313](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1313))

## 0.6.0-beta.1

Released 2020-Sep-15

* Updated System.Diagnostics.DiagnosticSource to version 5.0.0-rc.1.20451.14
  ([#1265](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1265))
* Added `GetTagValue` extension method on `Activity` for retrieving tag values
  efficiently
  ([#1221](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1221))
* Added `EnumerateTagValues` extension method on `Activity` for enumerating tag
  values efficiently
  ([#1236](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1236))

## 0.5.0-beta.2

Released 2020-08-28

* `Link` and `TelemetrySpan` are using `SpanAttributes` instead of
  `ActivityTagsCollection` or `Dictionary`
  ([#1120](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1120))
* Added `RecordException` in `TelemetrySpan`
  ([#1116](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1116))
* `PropagationContext` is now used instead of `ActivityContext` in the
    `ITextFormat` API
    ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048))
* Added `BaggageFormat` an `ITextFormat` implementation for managing Baggage
    propagation via the [W3C
    Baggage](https://github.com/w3c/baggage/blob/master/baggage/HTTP_HEADER_FORMAT.md)
    header
    ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048))
* Removed `DistributedContext` as it is no longer part of the spec
  ([#1048](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1048))
* Renaming from `ot` to `otel`
  ([#1046](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1046))
* Added `RuntimeContext` API
  ([#948](https://github.com/open-telemetry/opentelemetry-dotnet/pull/948))
* Changed `Link` constructor to accept `ActivityTagsCollection` instead of
  `IDictionary<string, object>` attributes
  ([#954](https://github.com/open-telemetry/opentelemetry-dotnet/pull/954))
* Added more `TelemetrySpan.SetAttribute` overloads with value of type bool,
  int, double (string already existed)
  ([#954](https://github.com/open-telemetry/opentelemetry-dotnet/pull/954))
* Changed `TelemetrySpan.SetAttribute` to match the spec
  ([#954](https://github.com/open-telemetry/opentelemetry-dotnet/pull/954))
  * Setting an attribute with an existing key now results in overwriting it
  * Setting null value has no impact except if null is set to an existing key,
    it gets removed
* Changed `HttpStatusCode` in all spans attribute (http.status_code) to use int
  value
  ([#998](https://github.com/open-telemetry/opentelemetry-dotnet/pull/998))
* Added `CompositePropagator` which accepts a list of `ITextFormat` to match the
  spec ([#923](https://github.com/open-telemetry/opentelemetry-dotnet/pull/923))
* Replaced `ITextFormatActivity` with `ITextFormat`
  ([#923](https://github.com/open-telemetry/opentelemetry-dotnet/pull/923))
* Added `StartRootSpan` and `StartActiveSpan`
  ([#994](https://github.com/open-telemetry/opentelemetry-dotnet/pull/994))
* Changed `StartSpan` to not set the created span as Active to match the spec
  ([#994](https://github.com/open-telemetry/opentelemetry-dotnet/pull/994))
* Updated System.Diagnostics.DiagnosticSource to version
  5.0.0-preview.8.20407.11.
* Removed `CorrelationContext` and added `Baggage`, an implementation of the
  [`Baggage
  API`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/baggage/api.md)
  spec
  ([#1106](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1106))
* Renamed `TraceContextFormat` to `TextMapPropagator`, `BaggageFormat` to
  `BaggagePropagator`, and `B3Format` to `B3Propagator`
  ([#1175](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1175))
* Renamed `ITextPropagator` to `IPropagator`
  ([#1190](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1190))

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
