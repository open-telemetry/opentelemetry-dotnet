# Changelog

## Unreleased

* Updated `Microsoft.Extensions.Configuration` and
  `Microsoft.Extensions.Options` package version to `8.0.0`.
  ([#5051](https://github.com/open-telemetry/opentelemetry-dotnet/pull/5051))

## 1.6.0-beta.2

Released 2023-Oct-26

## 1.5.1-beta.1

Released 2023-Jul-20

* The new network semantic conventions can be opted in to by setting
  the `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable. This allows for a
  transition period for users to experiment with the new semantic conventions
  and adapt as necessary. The environment variable supports the following
  values:
  * `http` - emit the new, frozen (proposed for stable) networking
  attributes, and stop emitting the old experimental networking
  attributes that the instrumentation emitted previously.
  * `http/dup` - emit both the old and the frozen (proposed for stable)
  networking attributes, allowing for a more seamless transition.
  * The default behavior (in the absence of one of these values) is to continue
  emitting the same network semantic conventions that were emitted in
  `1.5.0-beta.1`.
  * Note: this option will eventually be removed after the new
  network semantic conventions are marked stable. Refer to the
  specification for more information regarding the new network
  semantic conventions for
  [spans](https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/database/database-spans.md).
  ([#4644](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4644))

## 1.5.0-beta.1

Released 2023-Jun-05

* Bumped the package version to `1.5.0-beta.1` to keep its major and minor
  version in sync with that of the core packages. This would make it more
  intuitive for users to figure out what version of core packages would work
  with a given version of this package. The pre-release identifier has also been
  changed from `rc` to `beta` as we believe this more accurately reflects the
  status of this package. We believe the `rc` identifier will be more
  appropriate as semantic conventions reach stability.

## 1.0.0-rc9.14

Released 2023-Feb-24

* Updated OpenTelemetry.Api.ProviderBuilderExtensions dependency to 1.4.0

## 1.4.0-rc9.13

Released 2023-Feb-10

## 1.0.0-rc9.12

Released 2023-Feb-01

## 1.0.0-rc9.11

Released 2023-Jan-09

## 1.0.0-rc9.10

Released 2022-Dec-12

* **Breaking change**: The same API is now exposed for `net462` and
  `netstandard2.0` targets. `SetDbStatement` has been removed. Use
  `SetDbStatementForText` to capture command text and stored procedure names on
  .NET Framework. Note: `Enrich`, `Filter`, `RecordException`, and
  `SetDbStatementForStoredProcedure` options are NOT supported on .NET
  Framework.
  ([#3900](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3900))

* Added overloads which accept a name to the `TracerProviderBuilder`
  `AddSqlClientInstrumentation` extension to allow for more fine-grained options
  management
  ([#3994](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3994))

## 1.0.0-rc9.9

Released 2022-Nov-07

## 1.0.0-rc9.8

Released 2022-Oct-17

* Use `Activity.Status` and `Activity.StatusDescription` properties instead of
  `OpenTelemetry.Trace.Status` and `OpenTelemetry.Trace.Status.Description`
  respectively to set activity status.
  ([#3118](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3118))
  ([#3751](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3751))
* Add support for Filter option for non .NET Framework Targets
  ([#3743](https://github.com/open-telemetry/opentelemetry-dotnet/pull/3743))

## 1.0.0-rc9.7

Released 2022-Sep-29

## 1.0.0-rc9.6

Released 2022-Aug-18

## 1.0.0-rc9.5

Released 2022-Aug-02

* Update the `ActivitySource.Name` from "OpenTelemetry.SqlClient" to
  "OpenTelemetry.Instrumentation.SqlClient".
  ([#3435](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3435))

## 1.0.0-rc9.4

Released 2022-Jun-03

## 1.0.0-rc9.3

Released 2022-Apr-15

* Removes .NET Framework 4.6.1. The minimum .NET Framework version supported is
  .NET 4.6.2.
  ([#3190](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3190))

## 1.0.0-rc9.2

Released 2022-Apr-12

## 1.0.0-rc9.1

Released 2022-Mar-30

## 1.0.0-rc10 (broken. use 1.0.0-rc9.1 and newer)

Released 2022-Mar-04

## 1.0.0-rc9

Released 2022-Feb-02

## 1.0.0-rc8

Released 2021-Oct-08

* Removes .NET Framework 4.5.2 support. The minimum .NET Framework version
  supported is .NET 4.6.1.
  ([#2138](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2138))

## 1.0.0-rc7

Released 2021-Jul-12

## 1.0.0-rc6

Released 2021-Jun-25

## 1.0.0-rc5

Released 2021-Jun-09

## 1.0.0-rc4

Released 2021-Apr-23

* Instrumentation modified to depend only on the API.
* Activities are now created with the `db.system` attribute set for usage during
  sampling.
  ([#1979](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1979))

## 1.0.0-rc3

Released 2021-Mar-19

## 1.0.0-rc2

Released 2021-Jan-29

* Microsoft.Data.SqlClient v2.0.0 and higher is now properly instrumented on
  .NET Framework.
  ([#1599](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1599))
* SqlClientInstrumentationOptions API changes: `SetStoredProcedureCommandName`
  and `SetTextCommandContent` have been renamed to
  `SetDbStatementForStoredProcedure` and `SetDbStatementForText`. They are now
  only available on .NET Core. On .NET Framework they are replaced by a single
  `SetDbStatement` property.
* On .NET Framework, "db.statement_type" attribute is no longer set for
  activities created by the instrumentation.
* New setting on SqlClientInstrumentationOptions on .NET Core: `RecordException`
  can be set to instruct the instrumentation to record SqlExceptions as Activity
  [events](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md).
  ([#1592](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1592))

## 1.0.0-rc1.1

Released 2020-Nov-17

* SqlInstrumentation sets ActivitySource to activities created outside
  ActivitySource.
  ([#1515](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1515/))

## 0.8.0-beta.1

Released 2020-Nov-5

## 0.7.0-beta.1

Released 2020-Oct-16

* Instrumentation no longer store raw objects like `object` in
  Activity.CustomProperty. To enrich activity, use the Enrich action on the
  instrumentation.
  ([#1261](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1261))
* Span Status is populated as per new spec
  ([#1313](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1313))

## 0.6.0-beta.1

Released 2020-Sep-15

## 0.5.0-beta.2

Released 2020-08-28

* .NET Core SqlClient instrumentation will now add the raw Command object to the
  Activity it creates
  ([#1099](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1099))
* Renamed from `AddSqlClientDependencyInstrumentation` to
  `AddSqlClientInstrumentation`

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
