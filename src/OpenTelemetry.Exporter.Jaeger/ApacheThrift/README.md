# OpenTelemetry - Jaeger Exporter - Apache Thrift

This folder contains a stripped-down fork of the [ApacheThrift
0.13.0.1](https://www.nuget.org/packages/ApacheThrift/0.13.0.1) library from the
[apache/thrift](https://github.com/apache/thrift/tree/0.13.0) repo. Only the
client bits we need to transmit spans to Jaeger using the compact Thrift
protocol over UDP are included. Removing the other stuff (mainly the server
bits) allowed us to also remove all of the dependencies ApacheThrift requires
with the exception of `System.Threading.Tasks.Extensions` (needed for .NET
Standard 2.0 target only).

This was done because the official NuGet has two issues:

* The .NET Standard 2.0 target requires `Microsoft.AspNetCore.Http.Abstractions
  (>= 2.2.0)` which forces Jaeger consumers to use at least .NET Core 2.2+. We
  wanted to support at least .NET Core 2.1 which is the LTS version.
* The nupkg contains a net45 library with a different API than the .NET Standard
  2.0 library. This breaks .NET Framework consumers of OpenTelemetry using
  Jaeger unless we force selection of the lib/netstandard2.0 reference instead.

Ideally we would consume the official package but these issues made it
difficult.

Changes:

* Everything made internal.
* Added [PR#2093](https://github.com/apache/thrift/pull/2093).
* Added [PR#2057](https://github.com/apache/thrift/pull/2057).

The included files were made synchronous and anything unused was removed. [See
PR #1374](https://github.com/open-telemetry/opentelemetry-dotnet/pull/1374).
