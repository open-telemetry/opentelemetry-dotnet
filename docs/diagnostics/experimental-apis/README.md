# OpenTelemetry .NET Experimental APIs

This document describes experimental APIs exposed in OpenTelemetry .NET
pre-relase builds. APIs are exposed experimentally when either the OpenTelemetry
Specification has explicitly marked some feature as
[experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md)
or when the SIG members are still working through the design for a feature and
want to solicit feedback from the community.

> [!NOTE]
> Experimental APIs are exposed as `public` in pre-release builds and `internal`
in stable builds.

## Active

Experimental APIs available in the pre-release builds:

### OTEL1000

Description: `LoggerProvider` and `LoggerProviderBuilder`

Details: [OTEL1000](./OTEL1000.md)

### OTEL1001

Description: Logs Bridge API

Details: [OTEL1001](./OTEL1001.md)

### OTEL1002

Description: Metrics Exemplar Support

Details: [OTEL1002](./OTEL1002.md)

### OTEL1003

Description: MetricStreamConfiguration CardinalityLimit Support

Details: [OTEL1003](./OTEL1003.md)

## Inactive

Experimental APIs which have been released stable or removed:

<!-- When an experimental API is released or removed:
 1) Move the section from above down here.
 2) Delete the individual file from the repo and switch the link here to a
    permalink to the last version.
 3) Add the version info for when the API was released stable or removed. If
    removed add details for alternative solution or reasoning.
-->

None
