# Changelog

This file contains individual changes for the
OpenTelemetry.Configuration.Declarative package. For highlights and
announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

* Initial implementation of the `OpenTelemetry.Configuration.Declarative`
  package. Adds declarative configuration (YAML) support for the OpenTelemetry
  .NET SDK with support for `disabled` and `resource.attributes` /
  `resource.attributes_list`.
  ([#7413](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7413))

* Added internal `ConfigProperties` type - a typed, immutable property bag
  for reading parsed YAML configuration values (scalars, nested mappings, and
  sequences).
  ([#7657](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7657))

* Introduced `DeclarativeConfigurationDocument` and
  `DeclarativeConfigurationDocumentAccessor`. Each file is parsed at most once
  per application lifetime.
  ([#7690](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7690))
