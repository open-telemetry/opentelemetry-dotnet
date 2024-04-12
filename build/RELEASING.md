# Release process

**Only for Maintainers.**

 1. Decide the component(s) and tag name (version name) to be released.

    Notes:

       * There are different categories of packages. Check the project file for
         what you want to release and look for `MinVerTagPrefix`.

         * `core-`: Core packages. These packages are defined\goverened by the
           OpenTelemetry Specification and have released stable versions. They
           may be released as `alpha`, `beta`, `rc`, or stable.

         * `coreunstable-`: Core unstable packages. These packages are
           defined\goverened by the OpenTelemetry Specification but have not
           released stable versions. They may be released as `alpha` or `beta`.

         * Everything else: Instrumentation packages have dedicated tags. Some
           packages have released stable and some have not. These packages may
           be released as `alpha`, `beta`, `rc`, or stable depending on the
           stability of the semantic conventions used by the instrumentation.

       * Instrumentation packages are core unstable packages always depend on
       the stable versions of core packages. Before releasing a non-core
       component ensure the `OTelLatestStableVer` property in
       `Directory.Packages.props` has been updated to the latest stable core
       version.

       * Core unstable packages may only be released as `alpha` or `beta`.

 2. Run the following PowerShell from the root of the repo to get combined
    changelog (to be used later).

    ```powershell
        $changelogs = Get-ChildItem -Path . -Recurse -Filter changelog.md
        foreach ($changelog in $changelogs)
        {
         Add-Content -Path .\combinedchangelog.md -Value "**$($changelog.Directory.Name)**"
         $lines = Get-Content -Path $changelog.FullName
         $started = $false
         $ended = $false
         foreach ($line in $lines)
             {
                if($line -like "## Unreleased" -and $started -ne $true)
                {
                  $started = $true
                }
                elseif($line -like "## *" -and $started -eq $true)
                {
                  $ended = $true
                  break
                }
                else
                {
                    if ($started -eq $true)
                    {
                        Add-Content -Path .\combinedchangelog.md $line
                    }
                }
             }
        }
    ```

    This generates combined changelog to be used in GitHub release. Once
    contents of combined changelog is saved somewhere, delete the file.

 3. Run the following PowerShell script from the root of the repo. This updates
    all the changelog to have release date for the current version being
    released. Replace the version with actual version. In the script below,
    replace `1.4.0-beta.1` with the tag name chosen for the package in Step 1.

    ```powershell
         $changelogs = Get-ChildItem -Path . -Recurse -Filter changelog.md
        foreach ($changelog in $changelogs)
        {
         (Get-Content -Path $changelog.FullName) -replace "Unreleased", "Unreleased

    ## 1.4.0-beta.1

    Released $(Get-Date -UFormat '%Y-%b-%d')" | Set-Content -Path $changelog.FullName
        }
    ```

 4. Normalize PublicApi files (Stable Release Only): Run the PowerShell script
    `.\build\finalize-publicapi.ps1`. This will merge the contents of
    Unshipped.txt into the Shipped.txt.

 5. The scripts in steps 2-4 run over the entire repo. Remove and undo changes
    under projects which are not being released. Submit a PR with the final
    changes and get it merged.

 6. Tag Git with version to be released. We use
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

 7. Go to the [list of
    tags](https://github.com/open-telemetry/opentelemetry-dotnet/tags) and find
    the tag(s) which were pushed. Click the three dots next to the tag and
    choose `Create release`.
      * Give the release a name based on the tags created (e.g., `1.9.0-beta.1 /
      1.9.0`).
      * Paste the contents of combined changelog from Step 2. Only include
        projects with changes.
      * Check "This is a pre-release" if applicable.
      * Click "Publish release". This will kick off the [Pack and publish to
      MyGet
      workflow](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml).

 8. Validate using MyGet packages. Basic sanity checks :)

 9. From the above build, get the artifacts from the drop, which has all the
    NuGet packages.

10. Copy all the NuGet files and symbols for the packages being released into a
    local folder.

11. Download latest [nuget.exe](https://www.nuget.org/downloads) into the same
    folder from Step 10.

12. Create or regenerate an API key from nuget.org (only maintainers have
    access). When creating API keys make sure it is set to expire in 1 day or
    less.

13. Run the following commands from PowerShell from local folder used in Step 10:

    ```powershell
    .\nuget.exe setApiKey <actual api key>

    get-childitem -Recurse | where {$_.extension -eq ".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source https://api.nuget.org/v3/index.json}
    ```

14. Validate that the package(s) are uploaded. Packages are available
    immediately to maintainers on nuget.org but aren't publicly visible until
    scanning completes. This process usually takes a few minutes.

15. If a new stable version of the core packages was released, open a PR to
    update the `OTelLatestStableVer` property in `Directory.Packages.props` to
    the just released stable version.

16. If a new stable version of the core packages was released, open an issue in
    the
    [opentelemetry-dotnet-contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)
    repo to notify maintainers to begin upgrading dependencies.

17. Once the packages are available on nuget.org post an announcement in the
    [Slack channel](https://cloud-native.slack.com/archives/C01N3BC2W7Q). Note
    any big or interesting new features as part of the announcement.
