# Changelog

This file contains individual changes for the
OpenTelemetry.Configuration.Declarative package. For highlights and
announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

* Added internal `ConfigProperties` type - a typed, immutable property bag
  for reading parsed YAML configuration values (scalars, nested mappings, and
  sequences).
  ([#XXXX](https://github.com/open-telemetry/opentelemetry-dotnet/pull/XXXX))

* Initial implementation of the `OpenTelemetry.Configuration.Declarative`
  package. Adds declarative configuration (YAML) support for the OpenTelemetry
  .NET SDK with support for `disabled` and `resource.attributes` /
  `resource.attributes_list`.
  ([#7413](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7413))
