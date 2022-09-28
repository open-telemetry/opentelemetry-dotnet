# Dropping support for .NET Standard

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
out-of-support frameworks - even as far back as .NET Core 2.0.

However, referencing the latest version of OpenTelemetry .NET by an application
targeting .NET Core 3.1 or .NET 5, for example, will generate the following
build warnings:

> System.Diagnostics.DiagnosticSource doesn't support netcoreapp3.1. Consider updating your TargetFramework to net6.0 or later.

> System.Diagnostics.DiagnosticSource doesn't support net5.0. Consider updating your TargetFramework to net6.0 or later.

These warnings can be suppressed by setting the
`SuppressTfmSupportBuildWarnings` MSBuild property. However,
OpenTelemetry .NET is not tested against out-of-support frameworks. Therefore
there is no guarantee that it will continue to function.

## The plan to remove .NET Standard targets

The plan moving forward is to align the support cycle of OpenTelemetry .NET
with the support cycle of .NET. To achieve this, OpenTelemetry .NET plans to
drop .NET Standard builds of its artifacts. OpenTelemetry packages will only
offer implementation specific target frameworks (e.g., `net462` and `net6.0`).
This will enable us to easily test and validate that OpenTelemetry .NET works
as expected for all the frameworks we target.

At this time, the frameworks targeted will include at least `net462` and
`net6.0`, but may include others like `net7.0`, when appropriate. As target
frameworks reach end of life, we will remove those targets during a major
version release roughly aligned with the corresponding major version release
of .NET.

For example:

Currently the [OpenTelemetry SDK project](/blob/ee11de90a37915c68d9d44cdd283ba6047b394a3/src/OpenTelemetry/OpenTelemetry.csproj#L4)
contains the following targets:

```xml
<TargetFrameworks>net6.0;netstandard2.1;netstandard2.0;net462</TargetFrameworks>
```

When .NET 6 reaches end of life November 2024, the `net6.0` and .NET Standard targets
will be removed and we will perform a major version release of the OpenTelemetry
.NET SDK. Presumably, at that time .NET 8 will have been released, and the
the project file may look like this:

```xml
<TargetFrameworks>net8.0;net462</TargetFrameworks>
```

Since dropping a framework from a package is a source breaking change, the above
example describes our process for all of our stable packages.

That said, the OpenTelemetry .NET project offers a number of packages that have
not yet had a stable release. We will be removing .NET Standard builds from the
following packages in the next minor release:

* `OpenTelemetry.Exporter.Prometheus.AspNetCore`
* `OpenTelemetry.Exporter.Prometheus.HttpListener`
* `OpenTelemetry.Exporter.ZPages`
* `OpenTelemetry.Extensions.Hosting`
* `OpenTelemetry.Instrumentation.AspNetCore`
* `OpenTelemetry.Instrumentation.GrpcNetClient`
* `OpenTelemetry.Instrumentation.Http`
* `OpenTelemetry.Instrumentation.SqlClient`
* `OpenTelemetry.Shims.OpenTracing`

## How users will be impacted by removing .NET Standard builds

### Sharing code between .NET Framework and .NET 6+ applications

It has been a common practice for users to place shared code in a class library
targeting .NET Standard. For example, the following project might be used to
centralize the configuration of OpenTelemetry across both your .NET Framework
and .NET 6+ applications.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OpenTelemetry" Version="xxx" />
  </ItemGroup>
</Project>
```

With the removal of .NET Standard targets from OpenTelemetry packages, this
example project must now be multi-targeted as follows:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net6.0;net462</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="OpenTelemetry" Version="xxx" />
  </ItemGroup>
</Project>
```

### Referencing OpenTelemetry packages from Xamarin and Mono projects

Technically, both Xamarin and Mono implement `netstandard2.0`. However,
OpenTelemetry does not currently support either despite currently offering
a `netstandard2.0` build.

If in the future Xamarin or Mono are supported by OpenTelemetry .NET, we
will add this support via framework specific targets (i.e., `xamarin.android`
or `net6.0-android`).
