# Release process

**Note: Approvers (collaborators) can perform much of the release process but
Maintainers (admins) are needed to merge PRs and for the push to NuGet.**

 1. Decide the component(s) and tag name (version name) to be released. We use
    [MinVer](https://github.com/adamralph/minver) to do versioning, which
    produces version numbers based on git tags.

    Notes:

       * There are different categories of packages. Check the project file for
         what you want to release and look for `MinVerTagPrefix`.

         * `core-`: Core packages. These packages are defined\goverened by the
           OpenTelemetry Specification or are part of fundamental infrastructure
           and have released stable versions. They may be released as `alpha`,
           `beta`, `rc`, or stable.

           * `OpenTelemetry.Api` - Defined by spec (API)
           * `OpenTelemetry.Api.ProviderBuilderExtensions` - Fundamental
             infrastructure
           * `OpenTelemetry` - Defined by spec (SDK)
           * `OpenTelemetry.Exporter.Console` - Defined by spec
           * `OpenTelemetry.Exporter.InMemory` - Defined by spec
           * `OpenTelemetry.Exporter.OpenTelemetryProtocol` - Defined by spec
           * `OpenTelemetry.Exporter.Zipkin` - Defined by spec
           * `OpenTelemetry.Extensions.Hosting` - Fundamental infrastructure
           * `OpenTelemetry.Extensions.Propagators` - Defined by spec

         * `coreunstable-`: Core unstable packages. These packages are
           defined\goverened by the OpenTelemetry Specification or are part of
           fundamental infrastructure but have not released stable versions. As
           of the `1.9.0` release cycle they may only be released as `alpha` or
           `beta`.

           * `OpenTelemetry.Exporter.Prometheus.AspNetCore` - Defined by spec
             (experimental)
           * `OpenTelemetry.Exporter.Prometheus.HttpListener` - Defined by spec
             (experimental)
           * `OpenTelemetry.Shims.OpenTracing` - Defined by spec (stable but
             incomplete implementation)

       * As of the `1.9.0` release cycle core unstable packages always depend on
         the stable versions of core packages. Before releasing a non-core
         component ensure the `OTelLatestStableVer` property in
         `Directory.Packages.props` has been updated to the latest stable core
         version.

 2. Run the [Prepare for a
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/prepare-release.yml)
    workflow. Specify the `tag-prefix` and the `version` for the release. Make
    sure to run the workflow on the branch being released. This is typically
    `main` but could be some other branch for hotfix (eg `main-1.8.0`). The
    workflow will open a PR to update `CHANGELOG.md` files for the projects
    being released. If a stable version is specified as the `version` parameter,
    the workflow will also merge the contents of any detected
    `PublicAPI.Unshipped.txt` files in the `.publicApi` folder into the
    corresponding `PublicAPI.Shipped.txt` files for the projects being released.

    <details>
    <summary>Instructions for preparing for a release manually</summary>

    * Update CHANGELOG files

       Run the PowerShell script `.\build\scripts\update-changelogs.ps1
       -minVerTagPrefix [MinVerTagPrefix] -version [Version]`. Where
       `[MinVerTagPrefix]` is the tag prefix (eg `core-`) for the components
       being released and `[Version]` is the version being released (eg
       `1.9.0`). This will update `CHANGELOG.md` files for the projects being
       released.

    * **Stable releases only**: Normalize PublicApi files

       Run the PowerShell script `.\build\scripts\finalize-publicapi.ps1
       -minVerTagPrefix [MinVerTagPrefix]`. Where `[MinVerTagPrefix]` is the tag
       prefix (eg `core-`) for the components being released. This will merge
       the contents of any detected `PublicAPI.Unshipped.txt` files in the
       `.publicApi` folder into the corresponding `PublicAPI.Shipped.txt` files
       for the projects being released.
    </details

 3. For stable releases, use the `/UpdateReleaseNotes` command on the PR opened
    by [Prepare for a
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/prepare-release.yml)
    workflow in step 2 to update [Release Notes](../RELEASENOTES.md) with any
    big or interesting new features.

    * The `/UpdateReleaseDates` command may also be used to update dates in
      `CHANGELOG.md` files. This is useful when the PR is opened a few days
      before the planned release date to review public API changes.

 4. :stop_sign: The PR opened by [Prepare for a
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/prepare-release.yml)
    workflow in step 2 has to be merged.

 5. Once the PR opened by [Prepare for a
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/prepare-release.yml)
    workflow in step 2 has been merged a trigger will automatically add a
    comment and lock the PR. Post a comment with "/CreateReleaseTag" in the
    body. This will tell the [Prepare for a
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/prepare-release.yml)
    workflow to push the tag for the merge commit of the PR which will trigger
    the [Build, pack, and publish to
    MyGet](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml)
    workflow.

    <details>
    <summary>Instructions for pushing tags manually</summary>

    Note: In the below examples `git push origin` is used. If running in a fork,
    add the main repo as `upstream` and use `git push upstream` instead. Pushing
    a tag to `origin` in a fork pushes the tag to the fork.

    * If releasing core components, add and push the tag prefixed with `core-`.
    For example:

       ```sh
       git tag -a core-1.4.0-beta.1 -m "1.4.0-beta.1 of all core components"
       git push origin core-1.4.0-beta.1
       ```

    * If releasing core unstable components, push the tag prefixed with
    `coreunstable-`. For example:

       ```sh
       git tag -a coreunstable-1.9.0-beta.1 -m "1.9.0-beta.1 of all core unstable components"
       git push origin coreunstable-1.9.0-beta.1
       ```

    Pushing the tag will kick off the [Build, pack, and publish to
    MyGet](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml)
    workflow.
    </details>

 6. :stop_sign: Wait for the [Build, pack, and publish to
    MyGet](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml)
    workflow to complete. When complete a trigger will automatically add a
    comment on the PR opened by [Prepare for a
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/prepare-release.yml)
    workflow in step 2. Use MyGet or download the packages using the provided
    link to validate locally everything works. After validation has been
    performed have a maintainer post a comment with "/PushPackages" in the body.
    This will trigger the [Complete
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/prepare-release.yml)
    workflow to push the packages to NuGet and publish the draft release created
    by the [Build, pack, and publish to
    MyGet](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml)
    workflow. Comments will automatically be added on the PR opened by [Prepare
    for a
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/prepare-release.yml)
    workflow in step 2 as the process is run and the PR will be unlocked.

    <details>
    <summary>Instructions for pushing packages to NuGet manually</summary>

    1. The [Build, pack, and publish to
       MyGet](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml)
       workflow pushes the packages to MyGet and attaches them as artifacts on
       the workflow run.

    2. Validate locally everything works using the packages pushed to MyGet or
       downloaded from the drop attached to the workflow run. Basic sanity
       checks :)

    3. Download the artifacts from the drop attached to the workflow run. The
       artifacts archive (`.zip`) contains all the NuGet packages (`.nupkg`) and
       symbols (`.snupkg`) from the build which were pushed to MyGet.

    4. Extract the artifacts from the archive (`.zip`) into a local folder.

    5. Download latest [nuget.exe](https://www.nuget.org/downloads) into the
       same folder from step 4.

    6. Create or regenerate an API key from nuget.org (only maintainers have
       access). When creating API keys make sure it is set to expire in 1 day or
       less.

    7. Run the following commands from PowerShell from local folder used in step
       4:

       ```powershell
       .\nuget.exe setApiKey <actual api key>

       get-childitem -Recurse | where {$_.extension -eq ".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source https://api.nuget.org/v3/index.json}
       ```

    8. Validate that the package(s) are uploaded. Packages are available
       immediately to maintainers on nuget.org but aren't publicly visible until
       scanning completes. This process usually takes a few minutes.

    9. Open the
       [Releases](https://github.com/open-telemetry/opentelemetry-dotnet/releases)
       page on the GitHub repository. The [Build, pack, and publish to
       MyGet](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml)
       workflow creates a draft release for the tag which was pushed. Edit the
       draft Release and click `Publish release`.
    </details>

 7. If a new stable version of the core packages was released, a PR should have
    been automatically created by the [Complete
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/post-release.yml)
    workflow to update the `OTelLatestStableVer` property in
    `Directory.Packages.props` to the just released stable version. Merge that
    PR once the build passes (this requires the packages be available on NuGet).

 9. The [Complete
    release](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/post-release.yml)
    workflow should have invoked the [Core version
    update](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/actions/workflows/core-version-update.yml)
    workflow on the
    [opentelemetry-dotnet-contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/)
    repository which opens a PR to update dependencies. Verify this PR was
    opened successfully.

 9. For stable releases post an announcement in the [Slack
    channel](https://cloud-native.slack.com/archives/C01N3BC2W7Q) announcing the
    release and link to the release notes.
