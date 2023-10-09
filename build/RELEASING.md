# Release process

Only for Maintainers.

 1. Decide the tag name (version name) to be released. e.g. 1.4.0-beta.1,
    1.0.0-rc9.7 etc.

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

 5. Submit PR with the above changes, and get it merged.

 6. Tag Git with version to be released. We use
    [MinVer](https://github.com/adamralph/minver) to do versioning, which
    produces version numbers based on git tags.

    Note: If releasing only core components, only add and push the tag prefixed
    with `core-`. For example:

    ```sh
    git tag -a core-1.4.0-beta.1 -m "1.4.0-beta.1 of all core components"
    git push origin core-1.4.0-beta.1
    ```

    If releasing only non-core components, only add and push the tags without
    prefix. For example:

    ```sh
    git tag -a 1.0.0-rc9.7 -m "1.0.0-rc9.7 of all non-core components"
    git push origin 1.0.0-rc9.7
    ```

    If releasing both, push both tags above.

 7. Go to the [list of
    tags](https://github.com/open-telemetry/opentelemetry-dotnet/tags)
    and find the tag created for the core components. Click the three
    dots next to the tag and choose `Create release`.
      * Give the release a name based on the tags created
      (e.g., `1.4.0-beta.1 / 1.0.0-rc9.7`).
      * Paste the contents of combined changelog from Step 2.
      * Check "This is a pre-release" if applicable.
      * Click "Publish release". This will kick off the [Pack and publish to
      MyGet workflow](https://github.com/open-telemetry/opentelemetry-dotnet/actions/workflows/publish-packages-1.0.yml).

 8. Validate using MyGet packages. Basic sanity checks :)

 9. From the above build, get the artifacts from the drop, which has all the
    NuGet packages.

10. Copy all the NuGet files and symbols into a local folder. If only releasing
    core packages, only copy them over.

11. Download latest [nuget.exe](https://www.nuget.org/downloads) into the same
    folder from Step 9.

12. Obtain the API key from nuget.org (Only maintainers have access)

13. Run the following commands from PowerShell from local folder used in Step 9:

    ```powershell
    .\nuget.exe setApiKey <actual api key>

    get-childitem -Recurse | where {$_.extension -eq ".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source https://api.nuget.org/v3/index.json}
    ```

14. Packages would be available in nuget.org in few minutes. Validate that the
    package is uploaded.

15. Delete the API key generated in Step 11.

16. Update the OpenTelemetry.io document
    [here](https://github.com/open-telemetry/opentelemetry.io/tree/main/content/en/docs/net)
    by sending a Pull Request.

17. If a new stable version of the core packages were released, update
    `OTelLatestStableVer` in Directory.Packages.props to the just released
    stable version.
