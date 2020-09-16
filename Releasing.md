# Release process

Only for Maintainers.

1. Tag with version to be released e.g.:

   ```sh
   git tag -a 0.4.0-beta -m "0.4.0-beta"
   git push origin 0.4.0-beta
   ```
2. Run the following powershell script from the root of the repo.
   This updates all the changelog to have release date for the
   current version being released.
   Replace the date with actual date, version with actual version.
   The actual version would be the tag name from step1 appended with
   ".1"

```powershell
    $changelogs = Get-ChildItem -Path . -Recurse -Filter changelog.md
    foreach ($changelog in $changelogs)
    {
     (Get-Content -Path $changelog.FullName) -replace "Unreleased", "Unreleased

    ## 0.6.0-beta.1

    Released 2020-Sep-15" | Set-Content -Path $changelog.FullName
    }
```

3. Submit PR with the above changes, and get it merged.

4. Open [Pack and publish to MyGet
   workflow](https://github.com/open-telemetry/opentelemetry-dotnet/actions?query=workflow%3A%22Pack+and+publish+to+Myget%22)
   and manually trigger a build. At the end of this,
   MyGet will have the packages. The package name will be the tag name used in step1 appended
   with ".1".

5. Validate using MyGet packages. Basic sanity checks :)

6. From the above build, get the artifacts from the drop, which has all the
   nuget packages and symbols (*.snupkg files).

7. Copy all the nuget files and symbols into a local folder.

8. Download latest [nuget.exe](https://www.nuget.org/downloads).

9. Obtain the API key from nuget.org (Only maintainers have access)

10. Run the following command from PowerShell from local folder used in step 7:

   ```powershell
   .\nuget.exe setApiKey <your_API_key> get-childitem | where {$_.extension -eq
   ".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source
   https://api.nuget.org/v3/index.json}
   ```

11. Do Github release. Run the following powershell from the root of the repo
    to generate combined changelog.
```powershell
    $changelogs = Get-ChildItem -Path . -Recurse -Filter changelog.md
foreach ($changelog in $changelogs)
{
 Add-Content -Path .\combinedchangelog.md $changelog.Directory.Name
 $lines = Get-Content -Path $changelog.FullName
 $started = $false
 $ended = $false
 foreach ($line in $lines)
     {        
        if($line -like "## *" -and $started -ne $true)
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
