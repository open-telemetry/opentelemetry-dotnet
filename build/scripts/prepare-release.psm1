function CreatePullRequestToUpdateChangelogsAndPublicApis {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$minVerTagPrefix,
    [Parameter(Mandatory=$true)][string]$version,
    [Parameter(Mandatory=$true)][string]$requestedByUserName,
    [Parameter()][string]$targetBranch="main",
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $match = [regex]::Match($version, '^(\d+\.\d+\.\d+)(?:-((?:alpha)|(?:beta)|(?:rc))\.(\d+))?$')
  if ($match.Success -eq $false)
  {
      throw 'Input version did not match expected format'
  }

  $isPrerelease = $version -match '-alpha' -or $version -match '-beta' -or $version -match '-rc'
  $tag="${minVerTagPrefix}${version}"
  $branch="release/prepare-${tag}-release"

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git switch --create $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  $body =
@"
Note: This PR was opened automatically by the [prepare release workflow](https://github.com/$gitRepository/actions/workflows/prepare-release.yml).

Requested by: @$requestedByUserName

## Changes

* CHANGELOG files updated for projects being released.
"@

  # Update CHANGELOGs
  & ./build/scripts/update-changelogs.ps1 -minVerTagPrefix $minVerTagPrefix -version $version

  # Update publicApi files for stable releases
  if ($isPrerelease -ne $true)
  {
    & ./build/scripts/finalize-publicapi.ps1 -minVerTagPrefix $minVerTagPrefix

    $body += "`r`n* Public API files updated for projects being released (only performed for stable releases)."
  }

  $body +=
@"

## Commands

``/UpdateReleaseDates``: Use to update release dates in CHANGELOGs before merging [``approvers``, ``maintainers``]
"@

  if ($minVerTagPrefix -eq 'core-' -and $isPrerelease -ne $true)
  {
    $body +=
@"

``/UpdateReleaseNotes``: Use to update ``RELEASENOTES.md`` before merging [``approvers``, ``maintainers``]
"@
  }

  $body +=
@"

``/UpdateReleaseDates``: Use to update release dates in CHANGELOGs before merging [``approvers``, ``maintainers``]
``/CreateReleaseTag``: Use after merging to push the release tag and trigger the job to create packages [``approvers``, ``maintainers``]
``/PushPackages``: Use after the created packages have been validated to push to NuGet [``maintainers``]
"@

  git commit -a -m "Prepare repo to release $tag." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $branch 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  $createPullRequestResponse = gh pr create `
    --title "[release] Prepare release $tag" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label release

  Write-Host $createPullRequestResponse

  $match = [regex]::Match($createPullRequestResponse, "\/pull\/(.*)$")
  if ($match.Success -eq $false)
  {
      throw 'Could not parse pull request number from gh pr create response'
  }

  $pullRequestNumber = $match.Groups[1].Value

  if ($minVerTagPrefix -eq 'core-' -and $isPrerelease -ne $true)
  {
    $found = Select-String -Path "RELEASENOTES.md" -Pattern "## $version" -Quiet
    if ($found -eq $false)
    {
      $body =
@"
I noticed this PR is releasing a stable version of core packages but there isn't any content in ``RELEASENOTES.md`` for the version being released.

It is important to update ``RELEASENOTES.md`` before creating the release tag because a permalink will become part of the package(s).

Post a comment with "/UpdateReleaseNotes" in the body if you would like me to update release notes.

Note: In the comment everything below "/UpdateReleaseNotes" will be added to ``RELEASENOTES.md`` for the version being released. If something is already there it will be replaced.
"@

      gh pr comment $pullRequestNumber --body $body
    }
  }
}

Export-ModuleMember -Function CreatePullRequestToUpdateChangelogsAndPublicApis

function LockPullRequestAndPostNoticeToCreateReleaseTag {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$botUserName
  )

  $prViewResponse = gh pr view $pullRequestNumber --json mergeCommit,author,title | ConvertFrom-Json

  if ($prViewResponse.author.login -ne $botUserName)
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[release\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tag = $match.Groups[1].Value

  $commit = $prViewResponse.mergeCommit.oid
  if ([string]::IsNullOrEmpty($commit) -eq $true)
  {
      throw 'Could not find merge commit'
  }

  $body =
@"
I noticed this PR was merged.

Post a comment with "/CreateReleaseTag" in the body if you would like me to create the release tag ``$tag`` for [the merge commit](https://github.com/$gitRepository/commit/$commit) which will trigger the package workflow.
"@

  gh pr comment $pullRequestNumber --body $body

  gh pr lock $pullRequestNumber
}

Export-ModuleMember -Function LockPullRequestAndPostNoticeToCreateReleaseTag

function CreateReleaseTagAndPostNoticeOnPullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$botUserName,
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $prViewResponse = gh pr view $pullRequestNumber --json mergeCommit,author,title | ConvertFrom-Json

  if ($prViewResponse.author.login -ne $botUserName)
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[release\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tag = $match.Groups[1].Value

  $commit = $prViewResponse.mergeCommit.oid
  if ([string]::IsNullOrEmpty($commit) -eq $true)
  {
      throw 'Could not find merge commit'
  }

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git tag -a $tag -m "$tag" $commit 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git tag failure'
  }

  git push origin $tag 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  $body =
@"
I just pushed the [$tag](https://github.com/$gitRepository/releases/tag/$tag) tag.

The [package workflow](https://github.com/$gitRepository/actions/workflows/publish-packages-1.0.yml) should begin momentarily.
"@

  gh pr comment $pullRequestNumber --body $body
}

Export-ModuleMember -Function CreateReleaseTagAndPostNoticeOnPullRequest

function UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$botUserName,
    [Parameter(Mandatory=$true)][string]$commentUserName,
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $prViewResponse = gh pr view $pullRequestNumber --json headRefName,author,title | ConvertFrom-Json

  if ($prViewResponse.author.login -ne $botUserName)
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[release\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tag = $match.Groups[1].Value

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix or version from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value

  $commentUserPermission = gh api "repos/$gitRepository/collaborators/$commentUserName/permission" | ConvertFrom-Json
  if ($commentUserPermission.permission -ne 'admin' -and $commentUserPermission.permission -ne 'write')
  {
    gh pr comment $pullRequestNumber `
      --body "I'm sorry @$commentUserName but you don't have permission to update this PR. Only maintainers and approvers can update this PR."
    return
  }

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git switch $prViewResponse.headRefName 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  $updatedFiles = 0
  $newHeader =
@"
## $version

Released $(Get-Date -UFormat '%Y-%b-%d')
"@

  $projectDirs = Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$tagPrefix</MinVerTagPrefix>" -List | Select Path | Split-Path -Parent

  foreach ($projectDir in $projectDirs)
  {
    $content = (Get-Content "$projectDir/CHANGELOG.md" -Raw)

    $newContent = $content -replace "## $version\s*Released .*", $newHeader

    if ($content -ne $newContent)
    {
      $updatedFiles++
      Set-Content -Path "$projectDir/CHANGELOG.md" $newContent.Trim()
    }
  }

  if ($updatedFiles -eq 0)
  {
    gh pr comment $pullRequestNumber --body "All of the CHANGELOG files have valid release dates."
    return
  }

  git commit -a -m "Update CHANGELOG release dates for $tag." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $prViewResponse.headRefName 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  gh pr comment $pullRequestNumber --body "I updated the CHANGELOG release dates."
}

Export-ModuleMember -Function UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest

function UpdateReleaseNotesAndPostNoticeOnPullRequest {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$botUserName,
    [Parameter(Mandatory=$true)][string]$commentUserName,
    [Parameter(Mandatory=$true)][string]$commentBody,
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $prViewResponse = gh pr view $pullRequestNumber --json headRefName,author,title | ConvertFrom-Json

  if ($prViewResponse.author.login -ne $botUserName)
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[release\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tag = $match.Groups[1].Value

  $match = [regex]::Match($tag, '^(.*?-)(.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse prefix or version from tag'
  }

  $tagPrefix = $match.Groups[1].Value
  $version = $match.Groups[2].Value
  $isPrerelease = $version -match '-alpha' -or $version -match '-beta' -or $version -match '-rc'

  $commentUserPermission = gh api "repos/$gitRepository/collaborators/$commentUserName/permission" | ConvertFrom-Json
  if ($commentUserPermission.permission -ne 'admin' -and $commentUserPermission.permission -ne 'write')
  {
    gh pr comment $pullRequestNumber `
      --body "I'm sorry @$commentUserName but you don't have permission to update this PR. Only maintainers and approvers can update this PR."
    return
  }

  if ($tagPrefix -ne 'core-' -or $isPrerelease -eq $true)
  {
    gh pr comment $pullRequestNumber `
      --body "I'm sorry @$commentUserName but we don't typically add release notes for prereleases or unstable packages."
    return
  }

  if ([string]::IsNullOrEmpty($gitUserName) -eq $false)
  {
    git config user.name $gitUserName
  }
  if ([string]::IsNullOrEmpty($gitUserEmail) -eq $false)
  {
    git config user.email $gitUserEmail
  }

  git switch $prViewResponse.headRefName 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git switch failure'
  }

  $releaseNotesContent = (Get-Content -Path "RELEASENOTES.md" -Raw)

  $match = [regex]::Match($commentBody, '[\w\W\s]*\/UpdateReleaseNotes.*$([\w\W\s]*)', [Text.RegularExpressions.RegexOptions]::Multiline)
  if ($match.Success -eq $false)
  {
      throw 'Could not find release notes content'
  }

  $content = $match.Groups[1].Value.Trim() -replace "`r`n", "`n"

  $body =
@"
## $version

$content

##
"@

  $match = [regex]::Match($releaseNotesContent, "(## $version[\w\W\s]*?)##", [Text.RegularExpressions.RegexOptions]::Multiline)
  if ($match.Success -eq $true)
  {
    $content = [regex]::Replace($releaseNotesContent, "(## $version[\w\W\s]*?)##", $body, [Text.RegularExpressions.RegexOptions]::Multiline)
    Set-Content -Path "RELEASENOTES.md" -Value $content.TrimEnd()
  }
  else {
    $match = [regex]::Match($releaseNotesContent, '(# Release Notes[\w\W\s]*?)##', [Text.RegularExpressions.RegexOptions]::Multiline)
    if ($match.Success -eq $false)
    {
      throw 'Could not find release notes header'
    }

    $body = $match.Groups[1].Value + $body
    $content = [regex]::Replace($releaseNotesContent, '(# Release Notes[\w\W\s]*?)##', $body, [Text.RegularExpressions.RegexOptions]::Multiline)
    Set-Content -Path "RELEASENOTES.md" -Value $content.TrimEnd()
  }

  git commit -a -m "Update RELEASENOTES for $tag." 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git commit failure'
  }

  git push -u origin $prViewResponse.headRefName 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  gh pr comment $pullRequestNumber --body "I updated ``RELEASENOTES.md``."
}

Export-ModuleMember -Function UpdateReleaseNotesAndPostNoticeOnPullRequest
