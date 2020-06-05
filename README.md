# OpenTelemetry .NET
[![Gitter chat](https://img.shields.io/gitter/room/opentelemetry/opentelemetry-dotnet)](https://gitter.im/open-telemetry/opentelemetry-dotnet)
[![Build Status](https://dev.azure.com/opentelemetry/pipelines/_apis/build/status/open-telemetry.opentelemetry-dotnet-myget-update?branchName=master)](https://dev.azure.com/opentelemetry/pipelines/_build/latest?definitionId=2&branchName=master)

The .NET [OpenTelemetry](https://opentelemetry.io/) client.

## Installation

This repository includes multiple installable packages. The `OpenTelemetry.Api`
package includes abstract classes and no-op implementations for the [OpenTelemetry API 
specification](https://github.com/open-telemetry/opentelemetry-specification).

The `OpenTelemetry` package is the reference implementation of the API.

Libraries that produce telemetry data should only depend on `OpenTelemetry.Api`,
and defer the choice of the SDK to the application developer. Applications may
depend on `OpenTelemetry` or another package that implements the API.

**Please note** that this library is currently in _alpha_, and shouldn't
generally be used in production environments.

The API and SDK packages are available on the following NuGet feeds:
* [MyGet V2](https://www.myget.org/F/opentelemetry/api/v2)
* [MyGet V3](https://www.myget.org/F/opentelemetry/api/v3/index.json)

## Documentation

The online documentation is available at TBD.

## Compatible Exporters

See the [OpenTelemetry registry](https://opentelemetry.io/registry/?s=net) for a list of exporters available.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md)

We meet weekly on Tuesdays, and the time of the meeting alternates between 11AM PT and 4PM PT. The meeting is subject to change depending on contributors' availability. Check the [OpenTelemetry community calendar](https://calendar.google.com/calendar/embed?src=google.com_b79e3e90j7bbsa2n2p5an5lf60%40group.calendar.google.com) for specific dates.

Meetings take place via [Zoom video conference](https://zoom.us/j/8287234601).

Meeting notes are available as a public [Google doc](https://docs.google.com/document/d/1yjjD6aBcLxlRazYrawukDgrhZMObwHARJbB9glWdHj8/edit?usp=sharing). For edit access, get in touch on [Gitter](https://gitter.im/open-telemetry/opentelemetry-dotnet).

Approvers ([@open-telemetry/dotnet-approvers](https://github.com/orgs/open-telemetry/teams/dotnet-approvers)):

- [Bruno Garcia](https://github.com/bruno-garcia), Sentry
- [Christoph Neumueller](https://github.com/discostu105), Dynatrace
- [Liudmila Molkova](https://github.com/lmolkova), Microsoft
- [Mikel Blanchard](https://github.com/CodeBlanch), CoStar Group
- [Paulo Janotti](https://github.com/pjanotti), Splunk

Triagers:

- [Reiley Yang](https://github.com/reyang), Microsoft

*Find more about the approver role in [community repository](https://github.com/open-telemetry/community/blob/master/community-membership.md#approver).*

Maintainers ([@open-telemetry/dotnet-maintainers](https://github.com/orgs/open-telemetry/teams/dotnet-maintainers)):

- [Cijo Thomas](https://github.com/cijothomas), Microsoft
- [Mike Goldsmith](https://github.com/MikeGoldsmith), LightStep
- [Sergey Kanzhelev](https://github.com/SergeyKanzhelev), Google

*Find more about the maintainer role in [community repository](https://github.com/open-telemetry/community/blob/master/community-membership.md#maintainer).*

## Release Schedule

OpenTelemetry .NET is under active development.

The library is not yet _generally available_, and releases aren't guaranteed to
conform to a specific version of the specification. Future releases will not
attempt to maintain backwards compatibility with previous releases. Each alpha
and beta release includes significant changes to the API and SDK packages,
making them incompatible with each other.

See the [release
notes](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
for existing releases.

See the [project
milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestones)
for details on upcoming releases. The dates and features described in issues
and milestones are estimates, and subject to change.
