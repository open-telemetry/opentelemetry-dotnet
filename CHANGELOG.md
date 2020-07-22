# Changelog

Please update changelog as part of any significant pull request. Place short
description of your change into "Unreleased" section. As part of release
process content of "Unreleased" section content will generate release notes for
the release.

## Unreleased

* Modified Prometheus Exporter to add listening on all hostnames support.
    1. Modified the content of PrometheusExporterOptions from `Uri()` to
       `string`.
    2. `HttpListener()` can support "+" as: hostname which listens on all
       ports.
    3. Modified `examples/Console/TestPrometheusExporter.cs` to safely use the
       new implementation.
    4. Jaeger exporter implemented

* Copy from
  [OpenCensus](http://github.com/census-instrumentation/opencensus-csharp) at
  commit #`0474607a16282252697f989113d68bdf71959070`.
