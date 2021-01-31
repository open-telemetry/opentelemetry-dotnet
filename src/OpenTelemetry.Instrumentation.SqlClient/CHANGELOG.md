# Changelog

## Unreleased

## 1.0.0-rc2

Released 2021-Jan-29

* Microsoft.Data.SqlClient v2.0.0 and higher is now properly instrumented
  on .NET Framework.
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
  [events](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md).
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
