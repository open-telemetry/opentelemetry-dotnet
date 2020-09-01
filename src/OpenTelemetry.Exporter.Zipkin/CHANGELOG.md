# Changelog

## Unreleased

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
