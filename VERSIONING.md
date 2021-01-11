# Versioning details

This follows the [OpenTelemetry versioning
proposal](https://github.com/open-telemetry/oteps/pull/143/files)

OpenTelemetry .NET follows [SemVer V2](https://semver.org/spec/v2.0.0.html)
guidelines. This means that, for any stable packages released from this repo,
all public APIs will remain [backward
compatible](https://github.com/dotnet/runtime/blob/master/docs/coding-guidelines/breaking-change-rules.md#breaking-change-rules),
unless a major version bump occurs. This applies to the API, SDK, as well as
Exporters, Instrumentation etc. shipped from this repo.

For example, users can take a dependency on 1.0.0 version of any package, with
the assurance that all future releases until 2.0.0 will be backward compatible.
Any method/function which are planned to be removed in 2.0, will be marked
[Obsolete](https://docs.microsoft.com/dotnet/api/system.obsoleteattribute)
first.

## Pre-releases

Pre-release packages are denoted by appending identifiers such as -Alpha, -Beta,
-RC etc. There are no API guarantees in pre-releases. Each release can contain
breaking changes and functionality could be removed as well. In general, an RC
pre-release is more stable than a Beta release, which is more stable than an
Alpha release.

### Public API change detection

For convenience, every project in this repo uses
[PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/tree/master/src/PublicApiAnalyzers)
and lists public API in the directory "publicAPI". Any changes to public API,
without corresponding changes here will result in build breaks - this helps
catch any unintended changes to public API from being shipped accidentally. This
also helps reviewers quickly understand if a given PR is proposing public API
changes. For example,
[this](https://github.com/open-telemetry/opentelemetry-dotnet/tree/master/src/OpenTelemetry.Instrumentation.AspNetCore/.publicApi)
shows the public APIs, per target framework for the
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
future, APIs for Metrics, Propagators etc. will also come from .NET runtime
itself.

## Experimental Signals

Due to the fact that OpenTelemetry specification for different signals achieve
stability at different pace, we'll need to deal with shipping experimental,
non-stable signals. As of Dec 2020, Metrics and Logs specs have not been
declared stable.

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

### Downsides with Approach 1

It could be confusing if the term `Obsolete` is used for more than one purpose -
one is the original intended usage for marking Obsolete things, and the
additional usage for experimental signals. It *may* be used for Metrics, as the
current Metrics API is based on a draft version of the spec which is already
removed. As we are building a new metrics API, we need to deprecate the original
ones, so marking them as `Obsolete` is justified. However, this approach cannot
be used for the general purpose of shipping experimental features.

### Example with Approach 1

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
from the common packages, and ship as separate package (i.e a package with a
different name.) For example, OpenTelemetry 1.0.0 will only contain stable
signals like Traces, Baggage, Propagators. There will be a separate packages
like OpenTelemetry.API.Metrics and OpenTelemetry.Metrics, which contains the
metric API and SDK respectively. The packages can be released as pre-releases
with no stability guarantees. Once the signal achieve stable quality, it'll be
made part of the main package and released as a minor version update.

### Downsides with Approach 2

The number of packages will explode over time. There is no option to delete a
package from nuget.org, so the experimental packages will remain as orphan ones,
potentially confusing users. For example, if Metrics API was moved off to a
separate package, then the instrumentation package
OpenTelemetry.Instrumentation.AspNetCore must be also split into Traces and
Metrics ones separately.

Another downside is the lack of "clean transition", arising from the fact that
there are common classes used to initialize various signals. These
functionalities are currently in the `Sdk` class of the main package. eg:
`Sdk.CreateTracerProviderBuilder()`, `Sdk.CreateMeterProviderBuilder()` etc. If
metrics are in a separate package, the `Sdk` class cannot provide these entry
points until the metrics code is moved to the main package. So users who use the
experimental packages, will have to do code changes, when they upgrade to the
main package consisting of the metrics.

### Example with Approach2

Following shows an example on how the `OpenTelemetry` package versioning works
with this approach:

`OpenTelemetry` 0.7.0-beta1 release : Pre-release, no API guarantees.

`OpenTelemetry` 1.0.0-RC1 release : Pre-release, no API guarantees, but more
stable than beta.

`OpenTelemetry` 1.0.0 release : Stable release consisting of only stable signals
: Traces, Propagators, Baggage.

`OpenTelemetry.Metrics` 1.0.0-alpha release : Pre-release consisting of Metric
SDK. Alpha indicates early stages of development. Metrics entry point will be
contained in this package. (eg: new MeterProviderBuild().Build())

`OpenTelemetry` 1.0.1 release : Bug fixes.

`OpenTelemetry` 1.1.0 release : New features added. No metric code yet.

`OpenTelemetry.Metrics` 1.0.0-beta release : Metric evolves to beta status.
Still a pre-release.

`OpenTelemetry.Metrics` 1.0.0-RC release : Metric evolves to RC status. Still a
pre-release, but a final RC release will be done when Metrics is declared
stable.

`OpenTelemetry` 1.2.0 release : Add metric support. This will be additive
changes. Metrics entry point will be added the main package (just like Traces.)
(eg: Sdk.CreateMeterProviderBuilder().Build()) User who were previously using
the *final* RC package of `OpenTelemetry.Metrics`, will need to change the
Metric entry point code. Apart from that, they'll simply remove
`OpenTelemetry.Metrics` and update `OpenTelemetry` to 1.2.0, with no *other*
code changes.

In the case of `OpenTelemetry.API.Metrics` package, users will require code
changes, as the plan for Metrics API is to have it come from the .NET Runtime.

`OpenTelemetry` 1.3.0 release : More features.

`OpenTelemetry` 2.0.0 release : Remove any Obsolete methods from 1.* releases

### Approach 3 - Same package name, but version differently

The approach keeps the same amount of packages, but release (multiple) versions
of the same, with experimental signals part of non-stable versions.

This involves managing more than one branch in Github - master for regular work,
and a separate experimental branch for experimental features. There can be 'n'
experimental branches, if there is a need. The immediate future will have
metrics only as separate branch.

Release stable features as stable packages (OpenTelemetry 1.0.0, 1.1.0 etc.),
from master branch. Release experimental features as different versions of the
same package (OpenTelemetry 1.5.0-alpha.1), from experimental branch.

### Downsides with Approach 3

The "experimental" branches must be frequently kept updated with the master
branch.

### Example with Approach 3

Following shows an example on how the `OpenTelemetry` package versioning works
with this approach:

`OpenTelemetry` 0.7.0-beta1 release : Pre-release, no API guarantees.

`OpenTelemetry` 1.0.0-RC1 release : Pre-release, no API guarantees, but more
stable than beta.

`OpenTelemetry` 1.0.0 release : Stable release consisting of only stable signals
:- Traces, Propagators, Baggage. This is released from master branch.

`OpenTelemetry` 1.5.0-alpha release : Pre-release consisting of Metric SDK.
Alpha indicates early stages of development. Metric entry points are from the
`Sdk` class. This could be released at the same time as `OpenTelemetry 1.0.0` or
shortly after that. This is released from "experimental-metrics" branch which is
in sync with master.

`OpenTelemetry` 1.0.1 release : Bug fixes to traces. Released from master
branch. Changes are merged to "experimental-metrics" branch.

`OpenTelemetry` 1.1.0 release : New features added to traces. Released from
master branch. Changes are merged to "experimental-metrics" branch.

`OpenTelemetry` 1.5.0-beta release : Metric evolves to beta status. Still a
pre-release released from "experimental-metrics" branch.

`OpenTelemetry` 1.5.0-RC release : Metric evolves to RC status. Still a
pre-release released from "experimental-metrics" branch, but closer to stable.

`OpenTelemetry` 1.5.0 release : Stable package consisting of Traces and Metrics.
User who were consuming Metrics features from 1.5.0-RC requires *no* code
change.
