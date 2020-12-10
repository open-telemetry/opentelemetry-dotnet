# Versioning details

This follows the [OpenTelemetry versioning proposal](https://github.com/open-telemetry/oteps/pull/143/files)

OpenTelemetry .NET follows [SemVer V2](https://semver.org/spec/v2.0.0.html)
guidelines. This means that, for any stable packages released from this repo,
all public APIs will remain backward compatible, unless a major version bump
occurs. This applies to the API, SDK, as well as Exporters, Instrumentation etc.
shipped from this repo.

For example, users can take a dependency on 1.0.0 version of any package, with
the assurance that all future releases until 2.0.0 will be backward compatible.
Any method/function which are planned to be removed in 2.0, will be marked
[Obsolete](https://docs.microsoft.com/dotnet/api/system.obsoleteattribute)
first.

## Pre-releases

Pre-release packages are denoted by appending identifiers such as
-Alpha, -Beta, -RC
etc. There are no API guarantees in pre-releases. In general, an RC pre-release
is more stable than a Beta release, which is more stable than an Alpha release.

### Public API change detection

For convenience, every project in this repo uses
[PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/tree/master/src/PublicApiAnalyzers)
and lists public API in the directory "publicAPI". Any changes to public API,
without corresponding changes here will result in build breaks - this helps
catch any unintended changes to public API from being shipped accidentally. This
also helps reviewers quickly understand if a given PR is making public API
changes. For example,
[this](https://github.com/open-telemetry/opentelemetry-dotnet/tree/master/src/OpenTelemetry.Instrumentation.AspNetCore/.publicApi)
shows the public per target framework for the
`OpenTelemetry.Instrumentation.AspNetCore` package.

Since no stable version has been released so far, every API is listed in the
"Unshipped.txt" file. Once 1.0.0 is shipped, it'll be moved to "Shipped.txt"
file.

## Packaging

OpenTelemetry is structured around signals like Traces, Metrics, Logs, Baggage
etc. OpenTelemetry .NET mostly provides multiple signals in the same package.
i.e the `OpenTelemetry` package will consist of SDK components from *all* the
signals. Instrumentations also follow the same model - for example, there will
be a single package `OpenTelemetry.Instrumentation.AspNetCore` for ASP.NET Core
instrumentation, which produces Traces, Metrics and handles propagation, instead
of separate packages.

Due to the fact that OpenTelemetry .NET relies on .NET runtime to provide many
instrumentation API, API package is naturally split into multiple. i.e
`System.Diagnostics.DiagnosticSource` provides the Tracing API and
`Microsoft.Extensions.Logging.Abstractions` provide the Logging API. In the
future, APIs for Metrics, Baggage and Propagators will also come from .NET
runtime itself.

## Experimental Signals

Due to the fact that OpenTelemetry specification for different signals achieve stability at different pace, we'll need to deal with shipping experimental, non-stable signals. As of Dec 2020, Metrics and Logs specs have not been declared stable.

### Approach 1 - Using Obsolete attribute

Note: This is the currently implemented approach.

Any experimental signal which is shipped as part of a normal package will be
marked
[Obsolete](https://docs.microsoft.com/dotnet/api/system.obsoleteattribute).
(Ideally we need an "Experimental" attribute, but .NET has only built-in support
for "Obsolete", so we are leveraging it. Using "Obsolete" code results in
compile time warnings to caution users.) This allows an experimental signal to
co-exist in the same package as other, non-experimental signals. For example,
the OpenTelemetry 1.0.0 package will consist of Traces and Metrics. As Metrics
signal is not ready for stable release, every method dealing with Metrics will
be marked "Obsolete". Once Metrics signal gets stable, it'll be added to a
release with minor version bump, say 1.2.0. The "Obsolete" Metric methods can be
removed later with a major version bump.

### Example with Approach1

Following shows an example on how the `OpenTelemetry` package versioning works
with this approach:

`OpenTelemetry` 0.7.0-beta1 release : Pre-release, no API guarantees.

`OpenTelemetry` 1.0.0-RC1 release : Pre-release, no API guarantees, but more
stable than beta.

`OpenTelemetry` 1.0.0 release : Stable release consisting of Traces, Propagators
and Metrics. Metrics will be marked "Obsolete", to warn users that Metrics is
not Stable.

`OpenTelemetry` 1.0.1 release : Bug fixes.

`OpenTelemetry` 1.1.0 release : New features added.

`OpenTelemetry` 1.2.0 release : Add stable metric support. This will be additive
changes. Any existing Obsolete Metric code will continue to be present.

`OpenTelemetry` 1.3.0 release : More features.

`OpenTelemetry` 2.0.0 release : Remove all Obsolete methods from 1.* releases.

### Approach 2 - Separate package for experimental

An alternate option to deal with experimental signal is to remove the signal
from the common packages, and ship as separate package. For example,
OpenTelemetry 1.0.0 will only contain stable signals like Traces, Baggage,
Propagators. There will be a separate packages like OpenTelemetry.API.Metrics
and OpenTelemetry.Metrics, which contains the metric API and SDK respectively.
The packages can be released as pre-releases with no stability guarantees. Once
the signal achieve stable quality, it'll be made part of the main package and
released as a minor version update.

### Example with Approach2

Following shows an example on how the `OpenTelemetry` package versioning works
with this approach:

`OpenTelemetry` 0.7.0-beta1 release : Pre-release, no API guarantees.

`OpenTelemetry` 1.0.0-RC1 release : Pre-release, no API guarantees, but more
stable than beta.

`OpenTelemetry` 1.0.0 release : Stable release consisting of only stable signals - Traces, Propagators, Baggage.

`OpenTelemetry.Metrics` 1.0.0-alpha release : Pre-release consisting of Metric SDK. Alpha indicates early stages of development.

`OpenTelemetry` 1.0.1 release : Bug fixes.

`OpenTelemetry` 1.1.0 release : New features added.

`OpenTelemetry.Metrics` 1.0.0-beta release : Metric evolves to beta status. Still a pre-release.

`OpenTelemetry.Metrics` 1.0.0-RC release : Metric evolves to RC status. Still a pre-release, but the final RC release will be done when Metrics is declared stable.

`OpenTelemetry` 1.2.0 release : Add metric support. This will be additive changes. User who were previously using the *final* RC package of `OpenTelemetry.Metrics`, will need no code change. They'll simply remove `OpenTelemetry.Metrics` and update `OpenTelemetry` to 1.2.0, with no code changes.

In the case of `OpenTelemetry.API.Metrics` package, users will require code changes, as the plan for Metrics API is to have it come from the .NET Runtime.

`OpenTelemetry` 1.3.0 release : More features.

`OpenTelemetry` 2.0.0 release : Remove any Obsolete methods from 1.* releases.

TODO: Treat Logs also as non-stable.
