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

* Added a `PluginComponentProvider` abstraction and registry that allows SDK
  components and plugins to register named factories for declarative-config
  components which participate in YAML-driven configuration.
  ([#7710](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7710))

* Parse the full configuration document, including top-level sections the
  package does not yet apply. Substitution and YAML structure validation is
  applied, so previously ignored errors are now reported.
  ([#TODO](https://github.com/open-telemetry/opentelemetry-dotnet/pull/TODO))
