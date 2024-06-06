# OpenTelemetry .NET

[![Slack](https://img.shields.io/badge/slack-@cncf/otel/dotnet-brightgreen.svg?logo=slack)](https://cloud-native.slack.com/archives/C01N3BC2W7Q)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet/branch/main/graphs/badge.svg?)](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet/)
[![Nuget](https://img.shields.io/nuget/v/OpenTelemetry.svg)](https://www.nuget.org/profiles/OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.svg)](https://www.nuget.org/profiles/OpenTelemetry)
[![Build](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/ci.yml)

The .NET [OpenTelemetry](https://opentelemetry.io/) client.

## Supported .NET Versions

Packages shipped from this repository generally support all the officially
supported versions of [.NET](https://dotnet.microsoft.com/download/dotnet) and
[.NET Framework](https://dotnet.microsoft.com/download/dotnet-framework) (an
older Windows-based .NET implementation), except `.NET Framework 3.5`.
Any exceptions to this are noted in the individual `README.md`
files.

## Project Status

**Stable** across all 3 signals i.e. `Logs`, `Metrics`, and `Traces`.

See [Spec Compliance
Matrix](https://github.com/open-telemetry/opentelemetry-specification/blob/main/spec-compliance-matrix.md)
to understand which portions of the specification has been implemented in this
repo.

## Getting Started

If you are new here, please read the getting started docs:

* [Logs](./docs/logs/README.md)
* [Metrics](./docs/metrics/README.md)
* [Traces](./docs/trace/README.md)

This repository includes multiple installable components, available on
[NuGet](https://www.nuget.org/profiles/OpenTelemetry). Each component has its
individual `README.md` file, which covers the instruction on how to install and
how to get started. To find all the available components, please take a look at
the `src` folder.

Here are the most commonly used components:

* [OpenTelemetry .NET API](./src/OpenTelemetry.Api/README.md)
* [OpenTelemetry .NET SDK](./src/OpenTelemetry/README.md)

[Instrumentation libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
can be found in [contrib repository](https://github.com/open-telemetry/opentelemetry-dotnet-contrib).

Here are the [exporter
libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#exporter-library):

* [Console](./src/OpenTelemetry.Exporter.Console/README.md)
* [In-memory](./src/OpenTelemetry.Exporter.InMemory/README.md)
* [OTLP](./src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
  (OpenTelemetry Protocol)
* [Prometheus AspNetCore](./src/OpenTelemetry.Exporter.Prometheus.AspNetCore/README.md)
* [Prometheus HttpListener](./src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md)
* [Zipkin](./src/OpenTelemetry.Exporter.Zipkin/README.md)

See the [OpenTelemetry
registry](https://opentelemetry.io/ecosystem/registry/?language=dotnet) and
[OpenTelemetry .NET Contrib
repo](https://github.com/open-telemetry/opentelemetry-dotnet-contrib) for more
components.

## Troubleshooting

See [Troubleshooting](./src/OpenTelemetry/README.md#troubleshooting).
Additionally check readme file for the individual components for any additional
troubleshooting information.

## Extensibility

OpenTelemetry .NET is designed to be extensible. Here are the most common
extension scenarios:

* Building a custom [instrumentation
  library](./docs/trace/extending-the-sdk/README.md#instrumentation-library).
* Building a custom exporter for
  [logs](./docs/logs/extending-the-sdk/README.md#exporter),
  [metrics](./docs/metrics/extending-the-sdk/README.md#exporter) and
  [traces](./docs/trace/extending-the-sdk/README.md#exporter).
* Building a custom processor for
  [logs](./docs/logs/extending-the-sdk/README.md#processor) and
  [traces](./docs/trace/extending-the-sdk/README.md#processor).
* Building a custom sampler for
  [traces](./docs/trace/extending-the-sdk/README.md#sampler).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md)

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
* [Reiley Yang](https://github.com/reyang), Microsoft
* [Utkarsh Umesan Pillai](https://github.com/utpilla), Microsoft
* [Vishwesh Bankwar](https://github.com/vishweshbankwar), Microsoft

[Triagers](https://github.com/open-telemetry/community/blob/main/community-membership.md#triager)
([@open-telemetry/dotnet-triagers](https://github.com/orgs/open-telemetry/teams/dotnet-triagers)):

* [Martin Thwaites](https://github.com/martinjt), Honeycomb
* [Piotr Kie&#x142;kowicz](https://github.com/Kielek), Splunk

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

### Thanks to all the people who have contributed

[![contributors](https://contributors-img.web.app/image?repo=open-telemetry/opentelemetry-dotnet)](https://github.com/open-telemetry/opentelemetry-dotnet/graphs/contributors)

## Release Schedule

See the [project
milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestones)
for details on upcoming releases. The dates and features described in issues and
milestones are estimates, and subject to change.

See the [release
notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases) for
existing releases.

> [!CAUTION]
> Certain components, marked as
[pre-release](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/VERSIONING.md#pre-releases),
are still work in progress and can undergo breaking changes before stable
release. Check the individual `README.md` file for each component to understand its
current state.

Daily builds from this repo are published to MyGet, and can be installed from
[this source](https://www.myget.org/F/opentelemetry/api/v3/index.json).
