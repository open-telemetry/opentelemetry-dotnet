function CreatePullRequestToUpdateChangelogsAndPublicApis {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$minVerTagPrefix,
    [Parameter(Mandatory=$true)][string]$version,
    [Parameter()][string]$targetBranch="main",
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

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

## Changes

* CHANGELOG files updated for projects being released.
"@

  # Update CHANGELOGs
  & ./build/scripts/update-changelogs.ps1 -minVerTagPrefix $minVerTagPrefix -version $version

  # Update publicApi files for stable releases
  if ($version -notlike "*-alpha*" -and $version -notlike "*-beta*" -and $version -notlike "*-rc*")
  {
    & ./build/scripts/finalize-publicapi.ps1 -minVerTagPrefix $minVerTagPrefix

    $body += "`r`n* Public API files updated for projects being released (only performed for stable releases)."
  }

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

  gh pr create `
    --title "[repo] Prepare release $tag" `
    --body $body `
    --base $targetBranch `
    --head $branch `
    --label infra
}

Export-ModuleMember -Function CreatePullRequestToUpdateChangelogsAndPublicApis

function LockPullRequestAndPostNoticeToCreateReleaseTag {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber
  )

  $prViewResponse = gh pr view $pullRequestNumber --json mergeCommit,author,title | ConvertFrom-Json

  if ($prViewResponse.author.is_bot -eq $false -or $prViewResponse.author.login -ne 'app/github-actions')
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[repo\] Prepare release (.*)$')
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

Post a comment with "/CreateReleaseTag" in the body if you would like me to create the release tag ``$tag`` for [the merge commit](https://github.com/$gitRepository/commit/$commit) and then trigger the package workflow.
"@

  gh pr comment $pullRequestNumber --body $body

  gh pr lock $pullRequestNumber
}

Export-ModuleMember -Function LockPullRequestAndPostNoticeToCreateReleaseTag

function CreateReleaseTag {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$actionRunId,
    [Parameter()][ref]$tag,
    [Parameter()][string]$gitUserName,
    [Parameter()][string]$gitUserEmail
  )

  $prViewResponse = gh pr view $pullRequestNumber --json mergeCommit,author,title | ConvertFrom-Json

  if ($prViewResponse.author.is_bot -eq $false -or $prViewResponse.author.login -ne 'app/github-actions')
  {
      throw 'PR author was unexpected'
  }

  $match = [regex]::Match($prViewResponse.title, '^\[repo\] Prepare release (.*)$')
  if ($match.Success -eq $false)
  {
      throw 'Could not parse tag from PR title'
  }

  $tagValue = $match.Groups[1].Value

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

  git tag -a $tagValue -m "$tagValue" $commit 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git tag failure'
  }

  git push origin $tagValue 2>&1 | % ToString
  if ($LASTEXITCODE -gt 0)
  {
      throw 'git push failure'
  }

  gh pr unlock $pullRequestNumber

  $body =
@"
I just pushed the [$tagValue](https://github.com/$gitRepository/releases/tag/$tagValue) tag.

The [package workflow](https://github.com/$gitRepository/actions/runs/$actionRunId) should begin momentarily.
"@

  gh pr comment $pullRequestNumber --body $body

  $tag.value = $tagValue
}

Export-ModuleMember -Function CreateReleaseTag

function PostPackagesReadyNotice {
  param(
    [Parameter(Mandatory=$true)][string]$gitRepository,
    [Parameter(Mandatory=$true)][string]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$tag,
    [Parameter(Mandatory=$true)][string]$packagesUrl
  )

  $body =
@"
The packages for [$tag](https://github.com/$gitRepository/releases/tag/$tag) are now available: $packagesUrl.

Have a nice day!
"@

  gh pr comment $pullRequestNumber --body $body
}

Export-ModuleMember -Function PostPackagesReadyNotice
