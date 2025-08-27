# Updating Microsoft depenencies to match the target framework version

After [requests from OpenTelemetry's users][issue], in a future release the .NET
OpenTelemetry libraries are changing the strategy they use for selecting the
versions of NuGet packages they depend on that are shipped by the .NET team
as part of the core .NET platform (for example `System.Text.Json`).

If you would like to know the background to this, then continue reading this
issue. If you only want to see a summary of the outcome, skip to the end of
this issue.

## Problem

This change attempts to balance two competing perspectives on the way our
dependency versions are managed in the most flexible and pragmatic way.

On the one hand, one group of users want to always use the latest-and-greatest version
of the .NET platform whatever target framework they target in their own projects.

On the other hand, another group of users prefer to keep their dependencies aligned
with the version of .NET that they use in their applications. For example, users
who only wish to use Long Term Support (LTS) versions of .NET do not wish to also
deploy Standard Term Support (STS) versions of .NET dependencies with their
published application. A concrete example of this would be an application targeting
.NET 8 not including any packages from .NET 9.

The approach which matches the wishes of the first group of users forces this behaviour
onto users in the second group. It is not possible for users in the second group
to downgrade the versions of the dependencies included with an application using
OpenTelemetry. They have no flexibility to change this.

However, the approach which matches the wishes of the second group of users does
not force this view on the first group, who are able to opt into upgrading the
dependencies in their applications to newer versions themselves if they wish to.

Features such as [package reference pruning][prune-package-reference] (added in
.NET 9), [Transitive Pinning][transitive-pinning], or adding an explicit package
reference to one or more projects with a `<PackageReference>` element can be used
by users in the first group to upgrade the dependencies they would otherwise only
have in their applications' dependency graph through their use of the OpenTelemetry
packages.

This approach would allow users of applications targeting `net9.0` to upgrade to
the `10.0.x` packages of their own accord once .NET 10 is released without also needing
to upgrade their applications to target `net10.0`.

## Solution

The solution to this problem is implemented by [this pull request][pull-request].

The change is to have the major versions of packages such as `Microsoft.Extensions.*`
and `System.Text.Json` aligned to the major version of the target framework the assembly
is compiled for.

For .NET 9 this would be `9.0.x`, for .NET 10 this would be `10.0.x`, etc.

There is however some nuance to this, rather than a simple alignment as illustrated
above.

By default the `x.0.0` package versions will be used, but individual packages may
be upgraded to later patch versions, if needed, to address functional and/or security
issues.

### Exceptions

#### .NET Framework and netstandard2.x

For .NET Framework and `netstandard2.x` only, the `Microsoft.Extensions.*` and
`System.Text.Json` packages will always track the latest version of .NET. This means
they will stay aligned to `9.0.x` at the time of writing, and will move to `10.0.x`
after the release of .NET 10, `11.0.x` after the release of .NET 11 in 2026, and
so on.

This is because these target frameworks are disconnected from the yearly update cadence
of .NET, particularly for .NET Framework, so we keep them updated to keep pace with
innovation in the .NET stack and to ingest any security fixes over time.

#### System.Diagnostics.DiagnosticSource

The `System.Diagnostics.DiagnosticSource` package will always track the latest
version of .NET and will not align to the target framework.

There are two reasons for this:

1. As this package provides the core functionality OpenTelemetry builds upon
   (e.g. `Activity`, `Meter`), this library needs to be kept up-to-date to leverage
   changes to OpenTelementry functionality as the standards evolve over time as
   changes to Semantic Conventions etc. will not be backported to previous releases
   of the .NET platform.
2. Prior to this change, the `System.Diagnostics.DiagnosticSource` package already
   always used the latest version, and APIs only present in the `9.0.x` versions
   were depended on for all target frameworks. Downgrading this version for `net8.0`
   would then break functionality for OpenTelemetry users of applications targeting
   .NET 8 as these APIs would be missing.

## Summary of changes

| **Target Framework** | **Updated Package Version** | **Effective Change**                      |
|:---------------------|----------------------------:|:------------------------------------------|
| `netstandard2.0`     | `${latest}.0.x`             | **None** - continues to track `${latest}` |
| `net8.0`             | `8.0.x`[^1]                 | Will **not** upgrade to `10.0.x`[^1]      |
| `net9.0`             | `9.0.x`[^1]                 | Will **not** upgrade to `10.0.x`[^1]      |
| `net10.0`            | `10.0.x`[^1]                | Will stay pinned to `10.0.x`[^1]          |
| `net11.0`            | `11.0.x`[^1]                | Will stay pinned to `11.0.x`[^1]          |

[^1]: Except for `System.Diagnostics.DiagnosticSource`, which will always track `${latest}.0.x`

[issue]: https://github.com/open-telemetry/opentelemetry-dotnet/issues/5973
[prune-package-reference]: https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#prunepackagereference
[pull-request]: https://github.com/open-telemetry/opentelemetry-dotnet/pull/6327
[transitive-pinning]: https://learn.microsoft.com/nuget/consume-packages/central-package-management#transitive-pinning
