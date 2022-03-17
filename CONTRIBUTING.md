# Contributing to opentelemetry-dotnet

The OpenTelemetry .NET special interest group (SIG) meets regularly. See the
OpenTelemetry [community](https://github.com/open-telemetry/community#net-sdk)
repo for information on this and other language SIGs.

See the [public meeting
notes](https://docs.google.com/document/d/1yjjD6aBcLxlRazYrawukDgrhZMObwHARJbB9glWdHj8/edit?usp=sharing)
for a summary description of past meetings. To request edit access, join the
meeting or get in touch on
[Slack](https://cloud-native.slack.com/archives/C01N3BC2W7Q).

Even though, anybody can contribute, there are benefits of being a member of our
community. See to the [community membership
document](https://github.com/open-telemetry/community/blob/main/community-membership.md)
on how to become a
[**Member**](https://github.com/open-telemetry/community/blob/main/community-membership.md#member),
[**Approver**](https://github.com/open-telemetry/community/blob/main/community-membership.md#approver)
and
[**Maintainer**](https://github.com/open-telemetry/community/blob/main/community-membership.md#maintainer).

## Find a Buddy and Get Started Quickly

If you are looking for someone to help you find a starting point and be a
resource for your first contribution, join our Slack channel and find a buddy!

1. Create your [CNCF Slack account](http://slack.cncf.io/) and join the
   [otel-dotnet](https://cloud-native.slack.com/archives/C01N3BC2W7Q) channel.
2. Post in the room with an introduction to yourself, what area you are
   interested in (check issues marked with [help
   wanted](https://github.com/open-telemetry/opentelemetry-dotnet/labels/help%20wanted)),
   and say you are looking for a buddy. We will match you with someone who has
   experience in that area.

Your OpenTelemetry buddy is your resource to talk to directly on all aspects of
contributing to OpenTelemetry: providing context, reviewing PRs, and helping
those get merged. Buddies will not be available 24/7, but is committed to
responding during their normal contribution hours.

## Development Environment

You can contribute to this project from a Windows, macOS or Linux machine.

On all platforms, the minimum requirements are:

* Git client and command line tools.
* .NET 6.0+

### Linux or MacOS

* Visual Studio 2022+ for Mac or Visual Studio Code

Mono might be required by your IDE but is not required by this project. This is
because unit tests targeting .NET Framework (i.e: `net461`) are disabled outside
of Windows.

### Windows

* Visual Studio 2022+ or Visual Studio Code
* .NET Framework 4.6.1+

### Public API

It is critical to keep public API surface small and clean. This repository is
using `Microsoft.CodeAnalysis.PublicApiAnalyzers` to validate the public APIs.
This analyzer will check if you changed a public property/method so the change
will be easily spotted in pull request. It will also ensure that OpenTelemetry
doesn't expose APIs outside of the library primary concerns like a generic
helper methods.

#### How to enable and configure

* Create a folder in your project called `.publicApi` with the frameworks that
  as folders you target.
* Create two files called `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`
   in each framework that you target.
* Add the following lines to your csproj:

```xml
<ItemGroup>
  <AdditionalFiles Include=".publicApi\$(TargetFramework)\PublicAPI.Shipped.txt" />
  <AdditionalFiles Include=".publicApi\$(TargetFramework)\PublicAPI.Unshipped.txt" />
</ItemGroup>
```

* Use
   [IntelliSense](https://docs.microsoft.com/visualstudio/ide/using-intellisense)
   to update the publicApi files.

## Pull Requests

### How to Send Pull Requests

Everyone is welcome to contribute code to `opentelemetry-dotnet` via GitHub pull
requests (PRs).

To create a new PR, fork the project in GitHub and clone the upstream repo:

```sh
git clone https://github.com/open-telemetry/opentelemetry-dotnet.git
```

Navigate to the repo root:

```sh
cd opentelemetry-dotnet
```

Add your fork as an origin:

```sh
git remote add fork https://github.com/YOUR_GITHUB_USERNAME/opentelemetry-dotnet.git
```

Run tests:

```sh
dotnet test
```

If you made changes to the Markdown documents (`*.md` files), install the latest
[`markdownlint-cli`](https://github.com/igorshubovych/markdownlint-cli) and run:

```sh
markdownlint .
```

Check out a new branch, make modifications and push the branch to your fork:

```sh
$ git checkout -b feature
# edit files
$ git commit
$ git push fork feature
```

Open a pull request against the main `opentelemetry-dotnet` repo.

### How to Receive Comments

* If the PR is not ready for review, please mark it as
  [`draft`](https://github.blog/2019-02-14-introducing-draft-pull-requests/).
* Make sure CLA is signed and all required CI checks are clear.
* Submit small, focused PRs addressing a single
  concern/issue.
* Make sure the PR title reflects the contribution.
* Write a summary that helps understand the change.
* Include usage examples in the summary, where applicable.
* Include benchmarks (before/after) in the summary, for contributions that are
  performance enhancements.

### How to Get PRs Merged

A PR is considered to be **ready to merge** when:

* It has received approval from
  [Approvers](https://github.com/open-telemetry/community/blob/main/community-membership.md#approver).
  /
  [Maintainers](https://github.com/open-telemetry/community/blob/main/community-membership.md#maintainer).
* Major feedbacks are resolved.
* It has been open for review for at least one working day. This gives people
  reasonable time to review.
* Trivial change (typo, cosmetic, doc, etc.) doesn't have to wait for one day.
* Urgent fix can take exception as long as it has been actively communicated.

Any Maintainer can merge the PR once it is **ready to merge**. Note, that some
PRs may not be merged immediately if the repo is in the process of a release and
the maintainers decided to defer the PR to the next release train.

If a PR has been stuck (e.g. there are lots of debates and people couldn't agree
on each other), the owner should try to get people aligned by:

* Consolidating the perspectives and putting a summary in the PR. It is
  recommended to add a link into the PR description, which points to a comment
  with a summary in the PR conversation.
* Tagging subdomain experts (by looking at the change history) in the PR asking
  for suggestion.
* Reaching out to more people on the [CNCF OpenTelemetry .NET Slack
  channel](https://cloud-native.slack.com/archives/C01N3BC2W7Q).
* Stepping back to see if it makes sense to narrow down the scope of the PR or
  split it up.
* If none of the above worked and the PR has been stuck for more than 2 weeks,
  the owner should bring it to the OpenTelemetry .NET SIG
  [meeting](README.md#contributing).

## Design Choices

As with other OpenTelemetry clients, opentelemetry-dotnet follows the
[opentelemetry-specification](https://github.com/open-telemetry/opentelemetry-specification).

It's especially valuable to read through the [library
guidelines](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/library-guidelines.md).

### Focus on Capabilities, Not Structure Compliance

OpenTelemetry is an evolving specification, one where the desires and use cases
are clear, but the method to satisfy those uses cases are not.

As such, contributions should provide functionality and behavior that conforms
to the specification, but the interface and structure is flexible.

It is preferable to have contributions follow the idioms of the language rather
than conform to specific API names or argument patterns in the spec.

For a deeper discussion, see [this spec
issue](https://github.com/open-telemetry/opentelemetry-specification/issues/165).

## Style Guide

This project includes a [`.editorconfig`](./.editorconfig) file which is
supported by all the IDEs/editor mentioned above. It works with the IDE/editor
only and does not affect the actual build of the project.

This repository also includes stylecop ruleset files under the `./build` folder.
These files are used to configure the _StyleCop.Analyzers_ which runs during
build. Breaking the rules will result in a build failure.
