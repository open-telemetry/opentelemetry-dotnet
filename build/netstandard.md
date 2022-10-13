# The future of .NET Standard support

## Background

The OpenTelemetry .NET project depends heavily on APIs provided by .NET.
Specifically, the APIs provided under `System.Diagnostics.DiagnosticSource`.

With the release of .NET 6, [it was announced](https://github.com/dotnet/announcements/issues/190)
that the .NET team will drop out-of-support frameworks for a number of packages
including `System.Diagnostics.DiagnosticSource`. This practice will continue
with the release of .NET 7. Frameworks no longer supported will include .NET
Core 3.1 and .NET 5. Refer to the [.NET download](https://dotnet.microsoft.com/download/dotnet)
page to view the end of support dates for each version of .NET.

The core packages offered by OpenTelemetry .NET currently ship a .NET
Standard build (i.e., `netstandard2.0` and/or `netstandard2.1`).
Therefore, OpenTelemetry .NET can technically be consumed by projects targeting
out-of-support frameworks like .NET Core 3.1, and even as far back as .NET Core 2.0.

However, referencing the latest version of OpenTelemetry .NET by an application
targeting .NET Core 3.1 or .NET 5 will generate the following build warnings:

> System.Diagnostics.DiagnosticSource doesn't support netcoreapp3.1. Consider updating your TargetFramework to net6.0 or later.
> System.Diagnostics.DiagnosticSource doesn't support net5.0. Consider updating your TargetFramework to net6.0 or later.

These warnings can be suppressed by setting the
`SuppressTfmSupportBuildWarnings` MSBuild property. However,
OpenTelemetry .NET is not tested against out-of-support frameworks. Therefore
there is no guarantee that it will continue to function.

## Continued support of .NET Standard

OpenTelemetry .NET will continue to ship .NET Standard targets of its
packages (i.e., `netstandard2.0` and/or `netstandard2.1`).

Implementation specific target frameworks (e.g., `net8.0`, `net471`) may
continue to be added or removed as necessary. Removal of implementation
specific target frameworks will always be announced and may occur in minor
version releases. For example, in the recent past we removed `net461`,
`netcoreapp3.1`, and `net5.0` targets.

## How users will be impacted

### Removing implementation specific frameworks may cause older applications to fallback to a .NET Standard build

For example, if you have an application that targets `net6.0` and you reference
the most current version of OpenTelemetry .NET (as of today that is
1.4.0-alpha), then you will receive a `net6.0` targeted build of OpenTelemetry.
In a future version after .NET 8 is released, for example, we may remove the
`net6.0` target and replace it with a `net8.0` target.

You will still be able to upgrade to this later version of OpenTelemetry .NET
from your `net6.0` targeted application, however at this point you'll receive
a `netstandard2.x` target of OpenTelemetry .NET. Furthermore, you may begin
to receive build warnings indicating that your target framework is no longer
supported by `System.Diagnostics.DiagnosticSource`.

This can come with some consequences like missing out on performance
enhancements or features that were previously available in the old `net6.0`
target of OpenTelemetry .NET. We will not intentionally break older
applications that fallback to a .NET Standard build, but we cannot make any
guarantees.

### Referencing OpenTelemetry packages from Xamarin and Mono projects

Technically, both Xamarin and Mono implement `netstandard2.0`. However,
it is a known issue that OpenTelemetry does not currently support either
despite currently offering a `netstandard2.0` build.

If in the future OpenTelemetry .NET supports the Xamarin and/or Mono
frameworks, we may do so either via the `nestandard2.0` target or
implementation specific targets (e.g., `xamarin.android`, `net6.0-android`,
etc).
