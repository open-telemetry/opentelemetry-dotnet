# Versioning details

This document describes the versioning and stability policy of components
shipped from this repository, as per the [OpenTelemetry versioning and stability
specification](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/versioning-and-stability.md).

This repo follows [SemVer V2](https://semver.org/spec/v2.0.0.html) guidelines.
This means that, for any stable packages released from this repo, all public
APIs will remain [backward
compatible](https://github.com/dotnet/runtime/blob/master/docs/coding-guidelines/breaking-change-rules.md#breaking-change-rules),
unless a major version bump occurs. This applies to the API, SDK, as well as
Exporters, Instrumentation etc. shipped from this repo.

For example, users can take a dependency on 1.0.0 version of any package, with
the assurance that all future releases until 2.0.0 will be backward compatible.
Any method which is planned to be removed in 2.0, will be marked
[Obsolete](https://docs.microsoft.com/dotnet/api/system.obsoleteattribute)
first.

## API and SDK compatibility

API packages are supported with an SDK version that has same MAJOR version and
equal or greater MINOR or PATCH version. For example, application/library that
is instrumented with OpenTelemetry.API 1.1.0, will be compatible with SDK
versions [1.1.0, 2.0.0).

## Core components

Core components refer to the set of components which are required as per the
spec. This includes API, SDK, and exporters which are required by the
specification. These exporters are OTLP, Zipkin (Deprecated), Console and InMemory.

The core components are always versioned and released together. For example, if
Console exporter has a bug fix and is released as 1.0.1, then all other core
components are also released as 1.0.1, even if there is no code change in other
components.

Starting with 1.4.0,
[OpenTelemetry.Extensions.Hosting](./src/OpenTelemetry.Extensions.Hosting/README.md)
will also be versioned and released together as a core component.

## Pre-releases

Pre-release packages are denoted by appending identifiers such as -Alpha, -Beta,
-RC etc. There are no API guarantees in pre-releases. Each release can contain
breaking changes and functionality could be removed as well. In general, an RC
pre-release is more stable than a Beta release, which is more stable than an
Alpha release.

## Public API change detection

For convenience, every project in this repo uses
[PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/tree/master/src/PublicApiAnalyzers)
and lists public API in the directory "publicAPI". Any changes to public API,
without corresponding changes here will result in build breaks - this helps
catch any unintended changes to public API from being shipped accidentally. This
also helps reviewers quickly understand if a given PR is proposing public API
changes. For example,
[this](https://github.com/open-telemetry/opentelemetry-dotnet/tree/master/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/.publicApi)
shows the public APIs, per target framework for the
`OpenTelemetry.Exporter.OpenTelemetryProtocol` package.

APIs which are released as part of stable packages will be listed in the
"Shipped.txt" file, and those APIs which are released as part of
[pre-release](#pre-releases) packages in the "Unshipped.txt". APIs will be moved
from "Unshipped.txt" to "Shipped.txt" when the packages move from
[pre-release](#pre-releases) to stable.

## Packaging

OpenTelemetry is structured around signals like Traces, Metrics, Logs, Baggage
etc. OpenTelemetry .NET mostly provides multiple signals in the same package.
i.e the `OpenTelemetry` package will consist of SDK components from *all* the
signals. Instrumentations also follow the same model - for example, there will
be a single package `OpenTelemetry.Instrumentation.AspNetCore` for ASP.NET Core
instrumentation, which produces Traces, Metrics and handles propagation, instead
of separate packages.

OpenTelemetry .NET relies on .NET runtime to provide several instrumentation
APIs. Currently, `System.Diagnostics.DiagnosticSource` and
`Microsoft.Extensions.Logging.Abstractions` are the packages from .NET runtime
this repository is dependent on.

### Packaging non-stable signals

Due to the fact that OpenTelemetry specification for different signals achieve
stability at different pace, we'll need to deal with shipping non-stable
signals. As of Jan 2021, Metrics and Logs specs have not been declared stable.
Non-stable signals are shipped as separate non-stable versions of the package to
ensure they do not affect the stable signals. Following example demonstrates how
non-stable signals are shipped, with the example of Metrics as the non-stable
signal. (Actual versions may vary, below numbers are for demonstrating the
approach only.)

`OpenTelemetry` package versioning with stable and non-stable signal (Metrics):

`OpenTelemetry` 0.7.0-beta1 release : Pre-release, no API guarantees.

`OpenTelemetry` 1.0.0-rc1 release : Pre-release, no API guarantees, but more
stable than beta.

`OpenTelemetry` 1.0.0 release : Stable release consisting of only stable signals
:- Traces, Propagators, Baggage.

`OpenTelemetry` 1.2.0-alpha release : Pre-release consisting of Metric SDK.
Alpha indicates early stages of development. Metric entry points are from the
`Sdk` class. This could be released at the same time as `OpenTelemetry 1.0.0` or
shortly after that. This may be released from an "experimental-metrics" branch
which is in sync with master.

`OpenTelemetry` 1.0.1 release : Bug fixes to traces. Does not contain any
metrics code.

`OpenTelemetry` 1.1.0 release : New features added to traces. Does not contain
any metrics code.

`OpenTelemetry` 1.2.0-beta release : Metric evolves to beta status. Still a
pre-release, so breaking changes can still occur.

`OpenTelemetry` 1.2.0-rc1 release : Metric evolves to RC status. Still a
pre-release, but closer to stable.

`OpenTelemetry` 1.2.0 release : Stable package consisting of Traces and Metrics.
User who were consuming Metrics features from 1.2.0-rc1 requires *no* code
change.
