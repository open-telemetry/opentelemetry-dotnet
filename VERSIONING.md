# Versioning details

This follows the [OpenTelemetry versioning proposal](https://github.com/open-telemetry/oteps/pull/143/files)

OpenTelemetry .NET follows [SemVer V2](https://semver.org/spec/v2.0.0.html)
guidelines. This means that, for any packages released from this repo, all
public APIs will remain backward compatible, unless a major version bump occurs.
This applies to the API, SDK, as well as Exporters, Instrumentation etc. shipped
from this repo.

For example, users can take a dependency on 1.0.0 version of any package, with
the assurance that all future releases until 2.0.0 will be backward compatible.
Any method/function which are planned to be removed in 2.0, will be marked
[Obsolete](https://docs.microsoft.com/dotnet/api/system.obsoleteattribute)
first.

## Pre-releases

Pre-release packages are identified by identifies such as -Alpha, -Beta, -RC
etc. There is no API guarantees in pre-releases. In general, an RC pre-release
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
etc. OpenTelemetry .NET does not ship separate package per signal. There is a
single package which includes all the signals. i.e `OpenTelemetry.API` package
will consist of API components from *all* the signals. `OpenTelemetry` package
will consist of SDK components from *all* the signals. Instrumentations also
follow the same model - for example, There will be a single package
`OpenTelemetry.Instrumentation.AspNetCore` for ASP.NET Core instrumentation,
which produces Traces, Metrics and handles propagation, instead of separate
packages for traces and metrics.

## Experimental Signals

### Current approach

Given the fact that all signals are shipped as a single package, the following
approach is currently used to deal with experimental signals.

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
removed with a major version bump.

### Alternate option

An alternate option to deal with experimental signal is to remove the signal
from the common packages, and ship as separate package. For example,
OpenTelemetry 1.0.0 will only contain Traces, and there will be a separate
package OpenTelemetry.Metrics.Experimental, which contains the metric signal.
Once the signal achieve stable quality, it'll be made part of the main package
and released as a minor version update. i.e OpenTelemetry 1.2.0 will contain
Traces and Metrics.

## Examples

Following shows an example on how the `OpenTelemetry` package versioning works:

`OpenTelemetry` 0.7.0-beta1 release : Pre-release, no API guarantees.

`OpenTelemetry` 1.0.0-RC1 release : Pre-release, no API guarantees, but more
stable than beta.

`OpenTelemetry` 1.0.0 release : Stable release consisting of Traces, Propagators
and Metrics. Metrics will be marked "Obsolete", to warn users that Metrics is
not Stable.

`OpenTelemetry` 1.0.1 release : Bug fixes.

`OpenTelemetry` 1.1.0 release : New features added.

`OpenTelemetry` 1.2.0 release : Add metric support. This will be additive changes.

`OpenTelemetry` 1.3.0 release : More features.

`OpenTelemetry` 2.0.0 release : Remove all Obsolete methods from 1.* releases.
