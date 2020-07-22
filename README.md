# OpenTelemetry .NET

[![Gitter
chat](https://badges.gitter.im/open-telemetry/opentelemetry-dotnet.svg)](https://gitter.im/open-telemetry/opentelemetry-dotnet)
[![Build
Status](https://action-badges.now.sh/open-telemetry/opentelemetry-dotnet)](https://github.com/open-telemetry/opentelemetry-dotnet/actions)
[![Release](https://img.shields.io/github/v/release/open-telemetry/opentelemetry-dotnet?include_prereleases&style=)](https://github.com/open-telemetry/opentelemetry-dotnet/releases/)

The .NET [OpenTelemetry](https://opentelemetry.io/) client.

## Getting Started

If you are new here, please [get started in 5 minutes](./docs/getting-started.md).

This repository includes multiple installable components, available on
[NuGet](https://www.nuget.org/profiles/OpenTelemetry).

Each package has its individual `README.md` file, which covers the instruction
on how to install and how to get started. To find all the available components,
please take a look at the `src` folder.

Here are the most commonly used components:

* [OpenTelemetry .NET API](./src/OpenTelemetry.Api/README.md)
* [OpenTelemetry .NET SDK](./src/OpenTelemetry/README.md)
* [OpenTelemetry .NET Instrumentation](./docs/instrumentation.md)

Here are the exporters:

* [Console Exporter](./src/OpenTelemetry.Exporter.Console/README.md)
* [Jaeger Exporter](./src/OpenTelemetry.Exporter.Jaeger/README.md)
* [OpenTelemetry Protocol Exporter](./src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
* [Prometheus Exporter](./src/OpenTelemetry.Exporter.Prometheus/README.md)
* [Zipkin Exporter](./src/OpenTelemetry.Exporter.Zipkin/README.md)

See the [OpenTelemetry registry](https://opentelemetry.io/registry/?s=net) for
more exporters.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md)

We meet weekly on Tuesdays, and the time of the meeting alternates between 11AM
PT and 4PM PT. The meeting is subject to change depending on contributors'
availability. Check the [OpenTelemetry community
calendar](https://calendar.google.com/calendar/embed?src=google.com_b79e3e90j7bbsa2n2p5an5lf60%40group.calendar.google.com)
for specific dates.

Meetings take place via [Zoom video conference](https://zoom.us/j/8287234601).

Meeting notes are available as a public [Google
doc](https://docs.google.com/document/d/1yjjD6aBcLxlRazYrawukDgrhZMObwHARJbB9glWdHj8/edit?usp=sharing).
For edit access, get in touch on
[Gitter](https://gitter.im/open-telemetry/opentelemetry-dotnet).

Approvers
([@open-telemetry/dotnet-approvers](https://github.com/orgs/open-telemetry/teams/dotnet-approvers)):

* [Bruno Garcia](https://github.com/bruno-garcia), Sentry
* [Christoph Neumueller](https://github.com/discostu105), Dynatrace
* [Liudmila Molkova](https://github.com/lmolkova), Microsoft
* [Paulo Janotti](https://github.com/pjanotti), Splunk

Triagers:

* [Reiley Yang](https://github.com/reyang), Microsoft

*Find more about the approver role in [community
repository](https://github.com/open-telemetry/community/blob/master/community-membership.md#approver).*

Maintainers
([@open-telemetry/dotnet-maintainers](https://github.com/orgs/open-telemetry/teams/dotnet-maintainers)):

* [Cijo Thomas](https://github.com/cijothomas), Microsoft
* [Mike Goldsmith](https://github.com/MikeGoldsmith), LightStep
* [Mikel Blanchard](https://github.com/CodeBlanch), CoStar Group
* [Sergey Kanzhelev](https://github.com/SergeyKanzhelev), Google

*Find more about the maintainer role in [community
repository](https://github.com/open-telemetry/community/blob/master/community-membership.md#maintainer).*

## Release Schedule

OpenTelemetry .NET is under active development.

The library is not yet _generally available_, and releases aren't guaranteed to
conform to a specific version of the specification. Future releases will not
attempt to maintain backwards compatibility with previous releases. Each alpha
and beta release includes significant changes to the API and SDK packages,
making them incompatible with each other.

See the [release
notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases) for
existing releases.

See the [project
milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestones)
for details on upcoming releases. The dates and features described in issues
and milestones are estimates, and subject to change.
