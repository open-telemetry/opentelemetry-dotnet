# OpenTelemetry .NET

[![Slack](https://img.shields.io/badge/slack-@cncf/otel/dotnet-brightgreen.svg?logo=slack)](https://cloud-native.slack.com/archives/C01N3BC2W7Q)
[![codecov.io](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet/branch/main/graphs/badge.svg?)](https://codecov.io/gh/open-telemetry/opentelemetry-dotnet/)
[![Release](https://img.shields.io/github/v/release/open-telemetry/opentelemetry-dotnet)](https://github.com/open-telemetry/opentelemetry-dotnet/releases/)
[![Nuget](https://img.shields.io/nuget/v/OpenTelemetry.svg)](https://www.nuget.org/profiles/OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.svg)](https://www.nuget.org/profiles/OpenTelemetry)
[![Linux](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/linux-ci.yml/badge.svg?branch=main)](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/linux-ci.yml)
[![Windows](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/windows-ci.yml/badge.svg?branch=main)](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/windows-ci.yml)

The .NET [OpenTelemetry](https://opentelemetry.io/) client.

## Supported .NET Versions

Packages shipped from this repository generally support all the officially
supported versions of [.NET
Core](https://dotnet.microsoft.com/download/dotnet-core), and [.NET
Framework](https://dotnet.microsoft.com/download/dotnet-framework) except for
versions lower than `.NET Framework 4.6.1`.
Any exceptions to this are noted in the individual `README.md` files.

## Getting Started

If you are new here, please read the getting started docs:

* [logs](./docs/logs/getting-started/README.md)
* [metrics](./docs/metrics/getting-started/README.md)
* [trace](./docs/trace/getting-started/README.md)

This repository includes multiple installable components, available on
[NuGet](https://www.nuget.org/profiles/OpenTelemetry). Each component has its
individual `README.md` file, which covers the instruction on how to install and
how to get started. To find all the available components, please take a look at
the `src` folder.

Here are the most commonly used components:

* [OpenTelemetry .NET API](./src/OpenTelemetry.Api/README.md)
* [OpenTelemetry .NET SDK](./src/OpenTelemetry/README.md)

Here are the [instrumentation
libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library):

* [ASP.NET](./src/OpenTelemetry.Instrumentation.AspNet/README.md)
* [ASP.NET Core](./src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
* [Grpc.Net.Client](./src/OpenTelemetry.Instrumentation.GrpcNetClient/README.md)
* [HTTP clients](./src/OpenTelemetry.Instrumentation.Http/README.md)
* [Redis client](./src/OpenTelemetry.Instrumentation.StackExchangeRedis/README.md)
* [SQL client](./src/OpenTelemetry.Instrumentation.SqlClient/README.md)

Here are the [exporter
libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#exporter-library):

* [Console](./src/OpenTelemetry.Exporter.Console/README.md)
* [In-memory](./src/OpenTelemetry.Exporter.InMemory/README.md)
* [Jaeger](./src/OpenTelemetry.Exporter.Jaeger/README.md)
* [OTLP](./src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
  (OpenTelemetry Protocol)
* [Zipkin](./src/OpenTelemetry.Exporter.Zipkin/README.md)
* [Prometheus](./src/OpenTelemetry.Exporter.Prometheus/README.md)

See the [OpenTelemetry registry](https://opentelemetry.io/registry/?s=net) for
more exporters.

## Customization

OpenTelemetry .NET is designed to be customizable and extensible. Here are the
most common customization and extension scenarios:

* [Building a custom instrumentation
  library](./docs/trace/extending-the-sdk/README.md#instrumentation-library)
* [Building a custom log
  exporter/processor/sampler](./docs/logs/extending-the-sdk/README.md)
* [Building a custom metrics
  exporter/reader/exemplar](./docs/metrics/extending-the-sdk/README.md)
* [Building a custom trace
  exporter/processor/sampler](./docs/trace/extending-the-sdk/README.md)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md)

We meet weekly on Tuesdays, and the time of the meeting alternates between 11AM
PT and 4PM PT. The meeting is subject to change depending on contributors'
availability. Check the [OpenTelemetry community
calendar](https://calendar.google.com/calendar/embed?src=google.com_b79e3e90j7bbsa2n2p5an5lf60%40group.calendar.google.com)
for specific dates and for Zoom meeting links.

Meeting notes are available as a public [Google
doc](https://docs.google.com/document/d/1yjjD6aBcLxlRazYrawukDgrhZMObwHARJbB9glWdHj8/edit?usp=sharing).
If you have trouble accessing the doc, please get in touch on
[Slack](https://cloud-native.slack.com/archives/C01N3BC2W7Q).

[Maintainers](https://github.com/open-telemetry/community/blob/main/community-membership.md#maintainer)
([@open-telemetry/dotnet-maintainers](https://github.com/orgs/open-telemetry/teams/dotnet-maintainers)):

* [Alan West](https://github.com/alanwest), New Relic
* [Cijo Thomas](https://github.com/cijothomas), Microsoft
* [Mikel Blanchard](https://github.com/CodeBlanch), Microsoft

[Approvers](https://github.com/open-telemetry/community/blob/main/community-membership.md#approver)
([@open-telemetry/dotnet-approvers](https://github.com/orgs/open-telemetry/teams/dotnet-approvers)):

* [Reiley Yang](https://github.com/reyang), Microsoft
* [Robert Paj&#x105;k](https://github.com/pellared), Splunk
* [Utkarsh Umesan Pillai](https://github.com/utpilla), Microsoft

[Emeritus
Maintainer/Approver/Triager](https://github.com/open-telemetry/community/blob/main/community-membership.md#emeritus-maintainerapprovertriager):

* [Bruno Garcia](https://github.com/bruno-garcia)
* [Eddy Nakamura](https://github.com/eddynaka)
* [Liudmila Molkova](https://github.com/lmolkova)
* [Mike Goldsmith](https://github.com/MikeGoldsmith)
* [Paulo Janotti](https://github.com/pjanotti)
* [Sergey Kanzhelev](https://github.com/SergeyKanzhelev)
* [Victor Lu](https://github.com/victlu)

### Thanks to all the people who have contributed

[![contributors](https://contributors-img.web.app/image?repo=open-telemetry/opentelemetry-dotnet)](https://github.com/open-telemetry/opentelemetry-dotnet/graphs/contributors)

## Release Schedule

Only the [core components](./VERSIONING.md#core-components) of the repo have
released a stable version. Components which are marked
[pre-release](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/VERSIONING.md#pre-releases),
are still work in progress and can undergo many breaking changes before stable
release.

See special note about [Metrics release
plans](https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501).

See the [release
notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases) for
existing releases.

See the [project
milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestones)
for details on upcoming releases. The dates and features described in issues and
milestones are estimates, and subject to change.

Daily builds from this repo are published to MyGet, and can be installed from
[this source](https://www.myget.org/F/opentelemetry/api/v3/index.json).
