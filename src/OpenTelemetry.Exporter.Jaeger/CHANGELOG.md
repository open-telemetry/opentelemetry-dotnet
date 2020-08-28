# Changelog

## Unreleased

## 0.5.0-beta.1

Released 2020-08-28

* Changed `JaegerExporter` to use `BatchExportActivityProcessor` by default
  ([#1125](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1125))
* Span links will now be sent as `FOLLOWS_FROM` reference type. Previously they
  were sent as `CHILD_OF`.
  ([#970](https://github.com/open-telemetry/opentelemetry-dotnet/pull/970))

* Renamed extension method from `UseJaegerExporter` to `AddJaegerExporter`.

## 0.4.0-beta.2

Released 2020-07-24

* First beta release

## 0.3.0-beta

Released 2020-07-23

* Initial release
