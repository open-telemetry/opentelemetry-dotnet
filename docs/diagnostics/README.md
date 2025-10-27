# OpenTelemetry Diagnostics

This document describes the diagnostic categories used in OpenTelemetry .NET
components. Diagnostics are used by the compiler to report information to users
about experimental and/or obsolete code being invoked or to suggest improvements
to specific code patterns identified through static analysis.

## Experimental APIs

Range: OTEL1000 - OTEL1999

Experimental APIs exposed in OpenTelemetry .NET pre-release builds. APIs are
exposed experimentally when either the OpenTelemetry Specification has
explicitly marked some feature as
[experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md)
or when the SIG members are still working through the design for a feature and
want to solicit feedback from the community.

> [!NOTE]
> Experimental APIs are exposed as `public` in pre-release builds and `internal`
in stable builds.

For defined diagnostics see: [OpenTelemetry .NET Experimental
APIs](./experimental-apis/README.md)
