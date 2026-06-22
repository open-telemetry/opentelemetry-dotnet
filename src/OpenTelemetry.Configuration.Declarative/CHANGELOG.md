# Changelog

This file contains individual changes for the OpenTelemetry.Configuration.Declarative
package. For highlights and announcements covering all components see: [Release
Notes](../../RELEASENOTES.md).

## Unreleased

* Initial implementation of the `OpenTelemetry.Configuration.Declarative` package.
  Adds declarative configuration (YAML) support for the OpenTelemetry .NET SDK,
  accepting any `file_format: "1.x"` document (built against schema v1.1), with
  support for `disabled` and `resource.attributes` / `resource.attributes_list`.
  ([#7413](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7413))
