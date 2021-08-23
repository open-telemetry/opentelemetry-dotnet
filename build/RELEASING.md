# Release process

Only for Maintainers.

 1. Decide the tag name (version name) to be released.
    eg: 1.0.0-rc2, 1.0.0 etc.

 2. Run the following PowerShell from the root of the
    repo to get combined changelog (to be used later).

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

    This generates combined changelog to be used in Github release.
    Once contents of combined changelog is saved somewhere,
    delete the file.

 3. Run the following PowerShell script from the root of the repo.
    This updates all the changelog to have release date for the
    current version being released.
    Replace the version with actual version.
    The actual version would be the tag name from step1.

    ```powershell
         $changelogs = Get-ChildItem -Path . -Recurse -Filter changelog.md
        foreach ($changelog in $changelogs)
        {
         (Get-Content -Path $changelog.FullName) -replace "Unreleased", "Unreleased

    ## 1.0.0-rc2

    Released $(Get-Date -UFormat '%Y-%b-%d')" | Set-Content -Path $changelog.FullName
        }
    ```

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

11. Obtain the API key from nuget.org (Only maintainers have access)

12. Run the following commands from PowerShell from local folder used in step 9:

    ```powershell
    .\nuget.exe setApiKey <actual api key>

    get-childitem -Recurse | where {$_.extension -eq
    ".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source
    https://api.nuget.org/v3/index.json}
    ```

13. Packages would be available in nuget.org in few minutes.
    Validate that the package is uploaded.

14. Delete the API key generated in step 11.

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
