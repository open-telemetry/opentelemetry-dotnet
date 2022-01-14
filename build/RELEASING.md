# Release process

Only for Maintainers.

## Prerequisites

- Install [GitHub CLI](https://cli.github.com/)

## Steps

1. Decide the tag name (version name) to be released.
   eg: 1.0.0-rc2, 1.0.0 etc.

1. Obtain the API key from [nuget.org](#TODO) (Only maintainers have access)

1. Run the following script from the root of the repository
   `./build/Release.ps1 <TagName> <NuGetApiKey> <CoreComponents> <NonCoreComponents>`

 4. Submit PR with the above changes, and get it merged.

 5. Tag Git with version to be released e.g.:

    ```sh
    git tag -a 1.0.0-rc2 -m "1.0.0-rc2"
    git push origin 1.0.0-rc2
    ```

    We use [MinVer](https://github.com/adamralph/minver) to do versioning,
    which produces version numbers based on git tags.

    Note:
    If releasing only core components, prefix the tag
    with "core-". For example:
    git tag -a core-1.1.0-beta1 -m "1.1.0-beta1 of all core components"

    If releasing only non-core components, use tags without
    prefix. For example:
    git tag -a 1.0.0-rc3 -m "1.0.0-rc3 of all non-core components"

    If releasing both, push both tags above.

 6. Open [Pack and publish to MyGet
    workflow](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml)
    and manually trigger a build. At the end of this, MyGet will have the
    packages. The package name will be the tag name used in step 5.

 7. Validate using MyGet packages. Basic sanity checks :)

 8. From the above build, get the artifacts from the drop, which has all the
    NuGet packages.

 9. Copy all the NuGet files and symbols into a local folder. If only
    releasing core packages, only copy them over.

10. Download latest [nuget.exe](https://www.nuget.org/downloads) into
    the same folder from step 9.

12. Run the following commands from PowerShell from local folder used in step 9:

    ```powershell
    .\nuget.exe setApiKey <actual api key>

    get-childitem -Recurse | where {$_.extension -eq
    ".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source
    https://api.nuget.org/v3/index.json}
    ```

13. Packages would be available in nuget.org in few minutes.
    Validate that the package is uploaded.

15. Make the Github release with tag from Step5
    and contents of combinedchangelog from Step2.

    TODO: Add tagging for Metrics release.
    TODO: Separate version for instrumention/hosting/OTshim package.

16. Update the OpenTelemetry.io document
    [here](https://github.com/open-telemetry/opentelemetry.io/tree/main/content/en/docs/net)
    by sending a Pull Request.

17. If a new stable version of the core packages were released,
    update `OTelPreviousStableVer` in Common.props
    to the just released stable version.
