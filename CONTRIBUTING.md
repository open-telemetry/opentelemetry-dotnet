# Contributing to opentelemetry-dotnet

The OpenTelemetry .NET special interest group (SIG) meets regularly. See the
OpenTelemetry [community](https://github.com/open-telemetry/community#net-sdk)
repo for information on this and other language SIGs.

See the [public meeting
notes](https://docs.google.com/document/d/1yjjD6aBcLxlRazYrawukDgrhZMObwHARJbB9glWdHj8/edit?usp=sharing)
for a summary description of past meetings. To request edit access, join the
meeting or get in touch on
[Slack](https://cloud-native.slack.com/archives/C01N3BC2W7Q).

Anyone may contribute but there are benefits of being a member of our community.
See the [community membership
document](https://github.com/open-telemetry/community/blob/main/community-membership.md)
on how to become a
[**Member**](https://github.com/open-telemetry/community/blob/main/guides/contributor/membership.md#member),
[**Triager**](https://github.com/open-telemetry/community/blob/main/guides/contributor/membership.md#triager),
[**Approver**](https://github.com/open-telemetry/community/blob/main/guides/contributor/membership.md#approver),
and
[**Maintainer**](https://github.com/open-telemetry/community/blob/main/guides/contributor/membership.md#maintainer).

## Give feedback

We are always looking for your feedback.

You can do this by [submitting a GitHub issue](https://github.com/open-telemetry/opentelemetry-dotnet/issues/new).

You may also prefer writing on [#otel-dotnet Slack channel](https://cloud-native.slack.com/archives/C01N3BC2W7Q).

### Report a bug

Reporting bugs is an important contribution. Please make sure to include:

* Expected and actual behavior;
* OpenTelemetry, OS, and .NET versions you are using;
* Steps to reproduce;
* [Minimal, reproducible example](https://stackoverflow.com/help/minimal-reproducible-example).

### Request a feature

If you would like to work on something that is not listed as an issue
(e.g. a new feature or enhancement) please create an issue and describe your proposal.

## Find a buddy and get started quickly

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
those get merged. Buddies will not be available 24/7, but are committed to
responding during their normal working hours.

## Development Environment

You can contribute to this project from a Windows, macOS, or Linux machine.

On all platforms, the minimum requirements are:

* Git client and command line tools

* [.NET SDK (latest stable version)](https://dotnet.microsoft.com/download)

  > [!NOTE]
  > At times a pre-release version of the .NET SDK may be required to build code
    in this repository. Check
    [global.json](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/global.json)
    to verify the current required version.

### Linux or MacOS

* Visual Studio 2022+ for Mac or Visual Studio Code

Mono might be required by your IDE but is not required by this project. This is
because unit tests targeting .NET Framework (i.e: `net462`) are disabled outside
of Windows.

### Windows

* Visual Studio 2022+ or Visual Studio Code
* .NET Framework 4.6.2+

## Public API validation

It is critical to **NOT** make breaking changes to public APIs which have been
released in stable builds. We also strive to keep a minimal public API surface.
This repository is using
[Microsoft.CodeAnalysis.PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
and [Package
validation](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview)
to validate public APIs.

* `Microsoft.CodeAnalysis.PublicApiAnalyzers` will validate public API
  changes/additions against a set of "public API files" which capture the
  shipped/unshipped public APIs. These files must be maintained manually (not
  recommended) or by using tooling/code fixes built into the package (see below
  for details).

  Public API files are also used to perform public API reviews by repo
  approvers/maintainers before releasing stable builds.

* `Package validation` will validate public API changes/additions against
  previously released NuGet packages.

  This is performed automatically by the build/CI
  [package-validation](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/.github/workflows/package-validation.yml)
  workflow.

  By default package validation is **NOT** run for local builds. To enable
  package validation in local builds set the `EnablePackageValidation` property
  to `true` in
  [Common.prod.props](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/build/Common.prod.props)
  (please do not check in this change).

### Working with Microsoft.CodeAnalysis.PublicApiAnalyzers

#### Update public API files when writing code

[IntelliSense](https://docs.microsoft.com/visualstudio/ide/using-intellisense)
will [suggest
modifications](https://github.com/dotnet/roslyn-analyzers/issues/3322#issuecomment-591031429)
to the `PublicAPI.Unshipped.txt` file when you make changes. After reviewing
these changes, ensure they are reflected across all targeted frameworks. You can
do this by:

* Using the "Fix all occurrences in Project" feature in Visual Studio.

* Manually cycling through each framework using Visual Studio's target framework
  dropdown (in the upper right corner) and applying the IntelliSense
  suggestions.

> [!IMPORTANT]
> Do **NOT** modify `PublicAPI.Shipped.txt` files. New features and bug fixes
  **SHOULD** only require changes to `PublicAPI.Unshipped.txt` files. If you
  have to modify a "shipped" file it likely means you made a mistake and broke a
  stable API. Typically only maintainers modify the `PublicAPI.Shipped.txt` file
  while performing stable releases. If you need help reach out to an approver or
  maintainer on Slack or open a draft PR.

#### Enable public API validation in new projects

1. If you are **NOT** using experimental APIs:
   * If your API is the same across all target frameworks:
     * You only need two files: `.publicApi/PublicAPI.Shipped.txt` and
       `.publicApi/PublicAPI.Unshipped.txt`.
   * If your APIs differ between target frameworks:
     * Place the shared APIs in `.publicApi/PublicAPI.Shipped.txt` and
       `.publicApi/PublicAPI.Unshipped.txt`.
     * Create framework-specific files for API differences (e.g.,
       `.publicApi/net462/PublicAPI.Shipped.txt` and
       `.publicApi/net462/PublicAPI.Unshipped.txt`).

2. If you are using experimental APIs:
   * Follow the rules above, but create an additional layer in your folder
     structure:
     * For stable APIs: `.publicApi/Stable/*`.
     * For experimental APIs: `.publicApi/Experimental/*`.
   * The `Experimental` folder should contain APIs that are public only in
     pre-release builds. Typically the `Experimental` folder only contains
     `PublicAPI.Unshipped.txt` files as experimental APIs are never shipped
     stable.

    Example folder structure can be found
    [here](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api/.publicApi).

## Pull Requests

### How to create pull requests

Everyone is welcome to contribute code to `opentelemetry-dotnet` via GitHub pull
requests (PRs).

To create a new PR, fork the project on GitHub and clone the upstream repo:

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

Check out a new branch, make modifications, and push the branch to your fork:

```sh
$ git checkout -b feature
# edit files
$ git commit
$ git push fork feature
```

Open a pull request against the main `opentelemetry-dotnet` repo.

#### Tips and best practices for pull requests

* If the PR is not ready for review, please mark it as
  [`draft`](https://github.blog/2019-02-14-introducing-draft-pull-requests/).
* Make sure CLA is signed and all required CI checks are clear.
* Submit small, focused PRs addressing a single concern/issue.
* Make sure the PR title reflects the contribution.
* Write a summary that helps understand the change.
* Include usage examples in the summary, where applicable.
* Include benchmarks (before/after) in the summary, for contributions that are
  performance enhancements.
* We are open to bot generated PRs or AI/LLM assisted PRs. Actually, we are
  using
  [dependabot](https://docs.github.com/en/code-security/dependabot/dependabot-security-updates/configuring-dependabot-security-updates)
  to automate the security updates. However, if you use bots to generate spam
  PRs (e.g. incorrect, noisy, non-improvements, unintelligible, trying to sell
  your product, etc.), we might close the PR right away with a warning, and if
  you keep doing so, we might block your user account.

### How to get pull requests merged

A PR is considered to be **ready to merge** when:

* It has received approval from
  [Approvers](https://github.com/open-telemetry/community/blob/main/community-membership.md#approver).
  /
  [Maintainers](https://github.com/open-telemetry/community/blob/main/community-membership.md#maintainer).
* Major feedback/comments are resolved.
* It has been open for review for at least one working day. This gives people
  reasonable time to review.
  * Trivial change (typo, cosmetic, doc, etc.) doesn't have to wait for one day.
  * Urgent fix can take exception as long as it has been actively communicated.

Any maintainer can merge PRs once they are **ready to merge** however
maintainers might decide to wait on merging changes until there are more
approvals and/or dicussion, or based on other factors such as release timing and
risk to users. For example if a stable release is planned and a new change is
introduced adding public API(s) or behavioral changes it might be held until the
next alpha/beta release.

If a PR has become stuck (e.g. there is a lot of debate and people couldn't
agree on the direction), the owner should try to get people aligned by:

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

## Design choices

As with other OpenTelemetry clients, opentelemetry-dotnet follows the
[opentelemetry-specification](https://github.com/open-telemetry/opentelemetry-specification).

It's especially valuable to read through the [library
guidelines](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/library-guidelines.md).

### Focus on capabilities not structural compliance

OpenTelemetry is an evolving specification, one where the desires and use cases
are clear, but the method to satisfy those uses cases are not.

As such, contributions should provide functionality and behavior that conforms
to the specification, but the interface and structure is flexible.

It is preferable to have contributions follow the idioms of the language rather
than conform to specific API names or argument patterns in the spec.

For a deeper discussion, see [this spec
issue](https://github.com/open-telemetry/opentelemetry-specification/issues/165).

## Style guide

This project includes a [`.editorconfig`](./.editorconfig) file which is
supported by all the IDEs/editors mentioned above. It works with the IDE/editor
only and does not affect the actual build of the project.

This repository also includes StyleCop ruleset files under the `./build` folder.
These files are used to configure the _StyleCop.Analyzers_ which runs during
build. Breaking the rules will result in a build failure.

## New projects

New projects are required to:

* Use [nullable reference
types](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/nullable-reference-types).

   This should be enabled automatically via
   [Common.props](https://github.com/open-telemetry/opentelemetry-dotnet/blob/990deee419ab4c1449efd628bed3df57a50963a6/build/Common.props#L9).
   New project MUST NOT disable this.

* Pass [static
analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview).

> [!NOTE]
> There are other project-level features enabled automatically via
[Common.props](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/build/Common.props)
new projects must NOT manually override these settings.

## License requirements

OpenTelemetry .NET is licensed under the [Apache License, Version
2.0](./LICENSE.TXT).

### Copying files from other projects

OpenTelemetry .NET uses some files from other projects, typically where a binary
distribution does not exist or would be inconvenient.

The following rules must be followed for PRs that include files from another
project:

* The license of the file is
  [permissive](https://en.wikipedia.org/wiki/Permissive_free_software_licence).

* The license of the file is left intact.

* The contribution is correctly attributed in the [3rd party
  notices](./THIRD-PARTY-NOTICES.TXT) file in the repository, as needed.

See
[EnvironmentVariablesExtensions.cs](./src/Shared/EnvironmentVariables/EnvironmentVariablesExtensions.cs)
for an example of a file copied from another project and attributed in the [3rd
party notices](./THIRD-PARTY-NOTICES.TXT) file.
