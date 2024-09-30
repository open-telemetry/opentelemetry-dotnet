# OpenTelemetry .NET

[![Slack](https://img.shields.io/badge/slack-@cncf/otel/dotnet-brightgreen.svg?logo=slack)](https://cloud-native.slack.com/archives/C01N3BC2W7Q)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet/branch/main/graphs/badge.svg?)](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet/)
[![Nuget](https://img.shields.io/nuget/v/OpenTelemetry.svg)](https://www.nuget.org/profiles/OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.svg)](https://www.nuget.org/profiles/OpenTelemetry)
[![Build](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/ci.yml)

The .NET [OpenTelemetry](https://opentelemetry.io/) implementation.

<details>
<summary>Table of Contents</summary>

* [Supported .NET versions](#supported-net-versions)
* [Project status](#project-status)
* [Getting started](#getting-started)
  * [Getting started with Logging](#getting-started-with-logging)
  * [Getting started with Metrics](#getting-started-with-metrics)
  * [Getting started with Tracing](#getting-started-with-tracing)
* [Repository structure](#repository-structure)
* [Troubleshooting](#troubleshooting)
* [Extensibility](#extensibility)
* [Releases](#releases)
* [Contributing](#contributing)
* [References](#references)

</details>

## Supported .NET versions

Packages shipped from this repository generally support all the officially
supported versions of [.NET](https://dotnet.microsoft.com/download/dotnet) and
[.NET Framework](https://dotnet.microsoft.com/download/dotnet-framework) (an
older Windows-based .NET implementation), except `.NET Framework 3.5`.
Any exceptions to this are noted in the individual `README.md`
files.

## Project status

**Stable** across all 3 signals (`Logs`, `Metrics`, and `Traces`).

> [!CAUTION]
> Certain components, marked as
[pre-release](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/VERSIONING.md#pre-releases),
are still work in progress and can undergo breaking changes before stable
release. Check the individual `README.md` file for each component to understand its
current state.

To understand which portions of the [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification)
have been implemented in OpenTelemetry .NET see: [Spec Compliance
Matrix](https://github.com/open-telemetry/opentelemetry-specification/blob/main/spec-compliance-matrix.md).

## Getting started

If you are new here, please read the getting started docs:

### Getting started with Logging

If you are new to
[logging](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/README.md),
it is recommended to first follow the [getting started in 5 minutes - ASP.NET
Core Application](./docs/logs/getting-started-aspnetcore/README.md) guide or
the [getting started in 5 minutes - Console
Application](./docs/logs/getting-started-console/README.md) guide to get up
and running.

For general information and best practices see: [OpenTelemetry .NET
Logs](./docs/logs/README.md). For a more detailed explanation of SDK logging
features see: [Customizing OpenTelemetry .NET SDK for
Logs](./docs/logs/customizing-the-sdk/README.md).

### Getting started with Metrics

If you are new to
[metrics](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/README.md),
it is recommended to first follow the [getting started in 5 minutes - ASP.NET
Core Application](./docs/metrics/getting-started-aspnetcore/README.md) guide
or the [getting started in 5 minutes - Console
Application](./docs/metrics/getting-started-console/README.md) guide to get
up and running.

For general information and best practices see: [OpenTelemetry .NET
Metrics](./docs/metrics/README.md). For a more detailed explanation of SDK
metric features see: [Customizing OpenTelemetry .NET SDK for
Metrics](./docs/metrics/customizing-the-sdk/README.md).

### Getting started with Tracing

If you are new to
[traces](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/README.md),
it is recommended to first follow the [getting started in 5 minutes - ASP.NET
Core Application](./docs/trace/getting-started-aspnetcore/README.md) guide
or the [getting started in 5 minutes - Console
Application](./docs/trace/getting-started-console/README.md) guide to get up
and running.

For general information and best practices see: [OpenTelemetry .NET
Traces](./docs/trace/README.md). For a more detailed explanation of SDK tracing
features see: [Customizing OpenTelemetry .NET SDK for
Tracing](./docs/trace/customizing-the-sdk/README.md).

## Repository structure

This repository includes only what is defined in the [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification)
and is shipped as separate packages through
[NuGet](https://www.nuget.org/profiles/OpenTelemetry). Each component has an
individual `README.md` and `CHANGELOG.md` file which covers the instructions on
how to install and get started, and details about the individual changes made
(respectively). To find all the available components, please take a look at the
`src` folder.

Here are the most commonly used components:

* [OpenTelemetry API](./src/OpenTelemetry.Api/README.md)
* [OpenTelemetry SDK](./src/OpenTelemetry/README.md)
* [OpenTelemetry Hosting
  Extensions](./src/OpenTelemetry.Extensions.Hosting/README.md)

Here are the [exporter
libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#exporter-library):

* [Console](./src/OpenTelemetry.Exporter.Console/README.md)
* [In-memory](./src/OpenTelemetry.Exporter.InMemory/README.md)
* [OTLP](./src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
  (OpenTelemetry Protocol)
* [Prometheus AspNetCore](./src/OpenTelemetry.Exporter.Prometheus.AspNetCore/README.md)
* [Prometheus HttpListener](./src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md)
* [Zipkin](./src/OpenTelemetry.Exporter.Zipkin/README.md)

Additional packages including [instrumentation
libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
exporters, resource detectors, and extensions can be found in the
[opentelemetry-dotnet-contrib
repository](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)
and/or the [OpenTelemetry
registry](https://opentelemetry.io/ecosystem/registry/?language=dotnet).

## Troubleshooting

For general instructions see:
[Troubleshooting](./src/OpenTelemetry/README.md#troubleshooting). Additionally
`README.md` files for individual components may contain more detailed
troubleshooting information.

## Extensibility

OpenTelemetry .NET is designed to be extensible. Here are the most common
extension scenarios:

* Building a custom [instrumentation
  library](./docs/trace/extending-the-sdk/README.md#instrumentation-library).
* Building a custom exporter for
  [logs](./docs/logs/extending-the-sdk/README.md#exporter),
  [metrics](./docs/metrics/extending-the-sdk/README.md#exporter), and
  [traces](./docs/trace/extending-the-sdk/README.md#exporter).
* Building a custom processor for
  [logs](./docs/logs/extending-the-sdk/README.md#processor) and
  [traces](./docs/trace/extending-the-sdk/README.md#processor).
* Building a custom sampler for
  [traces](./docs/trace/extending-the-sdk/README.md#sampler).

## Releases

For details about upcoming planned releases see:
[Milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestones).
The dates and features described in issues and milestones are estimates and
subject to change.

For highlights and annoucements for stable releases see: [Release
Notes](./RELEASENOTES.md).

To access packages, source code, and/or view a list of changes for all
components in a release see:
[Releases](https://github.com/open-telemetry/opentelemetry-dotnet/releases).

Nightly builds from this repo are published to [MyGet](https://www.myget.org),
and can be installed using the
`https://www.myget.org/F/opentelemetry/api/v3/index.json` source.

## Contributing

For information about contributing to the project see:
[CONTRIBUTING.md](CONTRIBUTING.md).

We meet weekly on Tuesdays, and the time of the meeting alternates between 9AM
PT and 4PM PT. The meeting is subject to change depending on contributors'
availability. Check the [OpenTelemetry community
calendar](https://github.com/open-telemetry/community?tab=readme-ov-file#calendar)
for specific dates and for Zoom meeting links.

Meeting notes are available as a public [Google
doc](https://docs.google.com/document/d/1yjjD6aBcLxlRazYrawukDgrhZMObwHARJbB9glWdHj8/edit?usp=sharing).
If you have trouble accessing the doc, please get in touch on
[Slack](https://cloud-native.slack.com/archives/C01N3BC2W7Q).

The meeting is open for all to join. We invite everyone to join our meeting,
regardless of your experience level. Whether you're a seasoned OpenTelemetry
developer, just starting your journey, or simply curious about the work we do,
you're more than welcome to participate!

[Maintainers](https://github.com/open-telemetry/community/blob/main/community-membership.md#maintainer)
([@open-telemetry/dotnet-maintainers](https://github.com/orgs/open-telemetry/teams/dotnet-maintainers)):

* [Alan West](https://github.com/alanwest), New Relic
* [Mikel Blanchard](https://github.com/CodeBlanch), Microsoft

[Approvers](https://github.com/open-telemetry/community/blob/main/community-membership.md#approver)
([@open-telemetry/dotnet-approvers](https://github.com/orgs/open-telemetry/teams/dotnet-approvers)):

* [Cijo Thomas](https://github.com/cijothomas), Microsoft
* [Piotr Kie&#x142;kowicz](https://github.com/Kielek), Splunk
* [Reiley Yang](https://github.com/reyang), Microsoft
* [Utkarsh Umesan Pillai](https://github.com/utpilla), Microsoft

[Triagers](https://github.com/open-telemetry/community/blob/main/community-membership.md#triager)
([@open-telemetry/dotnet-triagers](https://github.com/orgs/open-telemetry/teams/dotnet-triagers)):

* [Martin Thwaites](https://github.com/martinjt), Honeycomb
* [Timothy "Mothra" Lee](https://github.com/TimothyMothra), Microsoft

[Emeritus
Maintainer/Approver/Triager](https://github.com/open-telemetry/community/blob/main/community-membership.md#emeritus-maintainerapprovertriager):

* [Bruno Garcia](https://github.com/bruno-garcia)
* [Eddy Nakamura](https://github.com/eddynaka)
* [Liudmila Molkova](https://github.com/lmolkova)
* [Mike Goldsmith](https://github.com/MikeGoldsmith)
* [Paulo Janotti](https://github.com/pjanotti)
* [Robert Paj&#x105;k](https://github.com/pellared)
* [Sergey Kanzhelev](https://github.com/SergeyKanzhelev)
* [Victor Lu](https://github.com/victlu)
* [Vishwesh Bankwar](https://github.com/vishweshbankwar)

### Thanks to all the people who have contributed

[![contributors](https://contributors-img.web.app/image?repo=open-telemetry/opentelemetry-dotnet)](https://github.com/open-telemetry/opentelemetry-dotnet/graphs/contributors)

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTelemetry Specification](https://github.com/open-telemetry/opentelemetry-specification)
