# Open Telemetry - Jaeger Exporter - Apache Thrift 

This folder contains a stripped-down fork of the [ApacheThrift 0.13.0.1](https://www.nuget.org/packages/ApacheThrift/0.13.0.1) library from the [apache/thrift](https://github.com/apache/thrift/tree/0.13.0) repo.

This was done because the official NuGet has two issues:

* The .NET Standard 2.0 target requires `Microsoft.AspNetCore.Http.Abstractions (>= 2.2.0)` which forces Jaeger consumers to use at least .NET Core 2.2+. We wanted to support at least .NET Core 2.1 which is the LTS version.
* The nupkg contains a net45 library with a different API than the .NET Standard 2.0 library. This breaks .NET Framework consumers of OpenTelemetry using Jaeger unless we force selection of the lib/netstandard2.0 reference instead.

Ideally we would consume the official package but these issues made it difficult.

Changes:

* Everything made internal.
* Added https://github.com/apache/thrift/pull/2093.
* Added https://github.com/apache/thrift/pull/2057.