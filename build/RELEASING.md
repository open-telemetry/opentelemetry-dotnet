# Release process

**Only for Maintainers.**

 1. Decide the component(s) and tag name (version name) to be released.

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

         * Everything else: Instrumentation packages have dedicated tags. Some
           packages have released stable and some have not. These packages may
           be released as `alpha`, `beta`, `rc`, or stable depending on the
           stability of the semantic conventions used by the instrumentation.

           * Stable:
             * `OpenTelemetry.Instrumentation.AspNetCore` (`Instrumentation.AspNetCore-`)
             * `OpenTelemetry.Instrumentation.Http` (`Instrumentation.Http-`)

           * Unstable:
             * `OpenTelemetry.Instrumentation.GrpcNetClient` (`Instrumentation.GrpcNetClient-`)
             * `OpenTelemetry.Instrumentation.SqlClient` (`Instrumentation.SqlClient-`)

       * As of the `1.9.0` release cycle instrumentation packages and core
         unstable packages always depend on the stable versions of core
         packages. Before releasing a non-core component ensure the
         `OTelLatestStableVer` property in `Directory.Packages.props` has been
         updated to the latest stable core version.

 2. Generate combined CHANGELOG

    Run the PowerShell script `.\build\generate-combined-changelog.ps1
    -minVerTagPrefix [MinVerTagPrefix]`. Where `[MinVerTagPrefix]` is the tag
    prefix (eg `core-`) for the components being released. This generates a
    combined changelog file to be used in GitHub release. The file is created in
    the repo root but should not be checked in. Move it or copy the contents off
    and then delete the file.

 3. Update CHANGELOG files

    Run the PowerShell script `.\build\update-changelogs.ps1 -minVerTagPrefix
    [MinVerTagPrefix] -version [Version]`. Where `[MinVerTagPrefix]` is the tag
    prefix (eg `core-`) for the components being released and `[Version]` is the
    version being released (eg `1.9.0`). This will update `CHANGELOG.md` files
    for the projects being released.

 4. **Stable releases only**: Normalize PublicApi files

    Run the PowerShell script `.\build\finalize-publicapi.ps1 -minVerTagPrefix
    [MinVerTagPrefix]`. Where `[MinVerTagPrefix]` is the tag prefix (eg `core-`)
    for the components being released. This will merge the contents of any
    detected `PublicAPI.Unshipped.txt` files in the `.publicApi` folder into the
    corresponding `PublicAPI.Shipped.txt` files for the projects being released.

 5. Tag Git with version to be released. We use
    [MinVer](https://github.com/adamralph/minver) to do versioning, which
    produces version numbers based on git tags.

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

    * If releasing a particular non-core component which has a dedicated
    `MinverTagPrefix` (such as AspNetCore instrumentation), push the tag with
    that particular prefix. For example:

       ```sh
       git tag -a Instrumentation.AspNetCore-1.6.0 -m "1.6.0 of AspNetCore instrumentation library"
       git push origin Instrumentation.AspNetCore-1.6.0
       ```

 6. Go to the [list of
    tags](https://github.com/open-telemetry/opentelemetry-dotnet/tags) and find
    the tag(s) which were pushed. Click the three dots next to the tag and
    choose `Create release`.
      * Give the release a name based on the tags created (e.g.
      `core-1.9.0-beta.1` or `Instrumentation.Http-1.8.0`).
      * Paste the contents of combined changelog from Step 2. Only include
        projects with changes.
      * Check "This is a pre-release" if applicable.
      * Click "Publish release". This will kick off the [Pack and publish to
      MyGet
      workflow](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml).

 7. Validate using MyGet packages. Basic sanity checks :)

 8. From the above build, get the artifacts from the drop, which has all the
    NuGet packages.

 9. Copy all the NuGet files and symbols for the packages being released into a
    local folder.

10. Download latest [nuget.exe](https://www.nuget.org/downloads) into the same
    folder from Step 9.

11. Create or regenerate an API key from nuget.org (only maintainers have
    access). When creating API keys make sure it is set to expire in 1 day or
    less.

12. Run the following commands from PowerShell from local folder used in Step 9:

    ```powershell
    .\nuget.exe setApiKey <actual api key>

    get-childitem -Recurse | where {$_.extension -eq ".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source https://api.nuget.org/v3/index.json}
    ```

13. Validate that the package(s) are uploaded. Packages are available
    immediately to maintainers on nuget.org but aren't publicly visible until
    scanning completes. This process usually takes a few minutes.

14. If a new stable version of the core packages was released, open a PR to
    update the `OTelLatestStableVer` property in `Directory.Packages.props` to
    the just released stable version.

15. If a new stable version of a package with a dedicated `MinVerTagPrefix` was
    released (typically instrumentation packages) open a PR to update
    `PackageValidationBaselineVersion` in the project file to reflect the stable
    version which was just released.

16. If a new stable version of the core packages was released, open an issue in
    the
    [opentelemetry-dotnet-contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)
    repo to notify maintainers to begin upgrading dependencies.

17. Once the packages are available on nuget.org post an announcement in the
    [Slack channel](https://cloud-native.slack.com/archives/C01N3BC2W7Q). Note
    any big or interesting new features as part of the announcement.
