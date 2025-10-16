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

### OTEL1004

Description: ExemplarReservoir Support

Details: [OTEL1004](./OTEL1004.md)

### OTEL1005

Description: OnEnding Implementation

Details: [OTEL1005](./OTEL1005.md)

## Inactive

Experimental APIs which have been released stable or removed:

<!-- When an experimental API is released or removed:
 1) Move the section from above down here.
 2) Delete the individual file from the repo and switch the link here to a
    permalink to the last version.
 3) Add the version info for when the API was released stable or removed. If
    removed add details for alternative solution or reasoning.
-->

### OTEL1002

Description: Metrics Exemplar Support

Details: [OTEL1002](https://github.com/open-telemetry/opentelemetry-dotnet/blob/b8ea807bae1a5d9b0f3d6d23b1e1e10f5e096a25/docs/diagnostics/experimental-apis/OTEL1002.md)

Released stable: `1.9.0`

### OTEL1003

Description: MetricStreamConfiguration CardinalityLimit Support

Details: [OTEL1003](https://github.com/open-telemetry/opentelemetry-dotnet/blob/9f41eadf03f3dcc5e76c686b61fb39849f046312/docs/diagnostics/experimental-apis/OTEL1003.md)

Released stable: `1.10.0`
