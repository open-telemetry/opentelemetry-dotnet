function CreateDraftRelease {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag
  )

  $packages = (Get-ChildItem -Path src/*/bin/Release/*.nupkg).Name

  $notes = ''
  $firstPackageVersion = ''

  foreach ($package in $packages)
  {
      $match = [regex]::Match($package, '(.*)\.(\d+\.\d+\.\d+.*?)\.nupkg')
      $packageName = $match.Groups[1].Value
      $packageVersion = $match.Groups[2].Value

      if ($firstPackageVersion -eq '')
      {
          $firstPackageVersion = $packageVersion
      }

      $changelogContent = Get-Content -Path "src/$packageName/CHANGELOG.md"

      $started = $false
      $content = ""

      foreach ($line in $changelogContent)
      {
          if ($line -like "## $packageVersion" -and $started -ne $true)
          {
              $started = $true
          }
          elseif ($line -like "Released *" -and $started -eq $true)
          {
              continue
          }
          elseif ($line -like "## *" -and $started -eq $true)
          {
              break
          }
          else
          {
              if ($started -eq $true -and ([string]::IsNullOrWhitespace($line) -eq $false -or $content.Length -gt 0))
              {
                  $content += "  " + $line + "`r`n"
              }
          }
      }

      if ([string]::IsNullOrWhitespace($content) -eq $true)
      {
          $content = "   No notable changes."
      }

      $content = $content.trimend()

      $notes +=
@"
* NuGet: [$packageName v$packageVersion](https://www.nuget.org/packages/$packageName/$packageVersion)

$content

  See [CHANGELOG](https://github.com/$gitRepository/blob/$tag/src/$packageName/CHANGELOG.md) for details.

"@
  }

  if ($firstPackageVersion -match '-alpha' -or $firstPackageVersion -match '-beta' -or $firstPackageVersion -match '-rc')
  {
    gh release create $tag `
      --title $tag `
      --verify-tag `
      --notes $notes `
      --prerelease `
      --draft
  }
  else
  {
    gh release create $tag `
      --title $tag `
      --verify-tag `
      --notes $notes `
      --latest `
      --draft
  }
}

Export-ModuleMember -Function CreateDraftRelease

function CreateStableVersionUpdatePullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter()][string]$targetBranch="main",
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $match = [regex]::Match($tag, '.*?-(.*)')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse version from tag'
  }

  $packageVersion = $match.Groups[1].Value

  $branch="release/post-stable-${tag}-update"

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git switch --create $branch origin/$targetBranch --no-track 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  (Get-Content Directory.Packages.props) `
      -replace '<OTelLatestStableVer>.*<\/OTelLatestStableVer>', "<OTelLatestStableVer>$packageVersion</OTelLatestStableVer>" |
    Set-Content Directory.Packages.props

  git add Directory.Packages.props 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git add failure'
  }

  git commit -m "Update OTelLatestStableVer in Directory.Packages.props to $packageVersion." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  $body =
@"
Note: This PR was opened automatically by the [package workflow](https://github.com/$gitRepository/actions/workflows/publish-packages-1.0.yml).

Merge once packages are available on NuGet and the build passes.

## Changes

* Sets ``OTelLatestStableVer`` in ``Directory.Packages.props`` to ``$packageVersion``.
"@

  gh pr create `
    --title "[repo] Core stable release $packageVersion updates" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label infra
}

Export-ModuleMember -Function CreateStableVersionUpdatePullRequest
