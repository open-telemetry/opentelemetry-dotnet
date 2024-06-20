function AddLabelsOnIssuesForPackageFoundInBody {
  param(
    [Parameter(Mandatory=$true)][int]$issueNumber,
    [Parameter(Mandatory=$true)][string]$issueBody
  )

  $match = [regex]::Match($issueBody, '^[#]+ Package\s*(OpenTelemetry(?:\.\w+)*)')
  if ($match.Success -eq $false)
  {
      Return
  }

  gh issue edit $issueNumber --add-label $("pkg:" + $match.Groups[1].Value)
}

Export-ModuleMember -Function AddLabelsOnIssuesForPackageFoundInBody

function AddLabelsOnPullRequestsBasedOnFilesChanged {
  param(
    [Parameter(Mandatory=$true)][int]$pullRequestNumber,
    [Parameter(Mandatory=$true)][string]$labelPackagePrefix # 'pkg:' on main repo, 'comp:' on contrib repo
  )

  # Note: This function is intended to work on main repo and on contrib. Please
  # keep them in sync.

  $repoLabels = gh label list --json name,id -L 200 | ConvertFrom-Json

  $filesChangedOnPullRequest = gh pr diff $pullRequestNumber --name-only

  $labelsOnPullRequest = (gh pr view $pullRequestNumber --json labels | ConvertFrom-Json).labels

  $visitedProjects = New-Object System.Collections.Generic.HashSet[string]
  $labelsToAdd = New-Object System.Collections.Generic.HashSet[string]
  $labelsToRemove = New-Object System.Collections.Generic.HashSet[string]

  # Note: perf label may be added but it is kind of a guess so we don't remove
  # it automatically in order to also allow manual inclusion after reviewing files
  $managedLabels = 'infra', 'documentation', 'dependencies'
  $rootInfraFiles = 'global.json', 'NuGet.config', 'codeowners'
  $documentationFiles = 'readme.md', 'contributing.md', 'releasing.md', 'versioning.md'

  foreach ($fileChanged in $filesChangedOnPullRequest)
  {
    $fileChanged = $fileChanged.ToLower()
    $fullFileName = [System.IO.Path]::GetFileName($fileChanged)
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($fileChanged)
    $fileExtension = [System.IO.Path]::GetExtension($fileChanged)

    if ($fileChanged.StartsWith('src/') -or $fileChanged.StartsWith('test/'))
    {
      $match = [regex]::Match($fileChanged, '^(?:(?:src)|(?:test))\/(.*?)\/.+$')
      if ($match.Success -eq $false)
      {
          continue
      }
      $rawProjectName = $match.Groups[1].Value
      if ($rawProjectName.Contains(".benchmarks") -or $rawProjectName.Contains(".stress"))
      {
        $added = $labelsToAdd.Add("perf")
      }

      $projectName = $rawProjectName.Replace(".tests", "").Replace(".benchmarks", "").Replace(".stress", "")
      if ($visitedProjects.Contains($projectName))
      {
        continue
      }

      $added = $visitedProjects.Add($projectName);

      foreach ($repoLabel in $repoLabels)
      {
        if ($repoLabel.name.StartsWith($labelPackagePrefix))
        {
            $package = $repoLabel.name.Substring($labelPackagePrefix.Length).ToLower()
            if ($package.StartsWith('opentelemetry') -eq $false)
            {
                # Note: On contrib labels don't have "OpenTelemetry." prefix
                $package = 'opentelemetry.' + $package
            }
            if ($package -eq $projectName)
            {
                $added = $labelsToAdd.Add($repoLabel.name)
                break
            }
        }
      }
    }

    if ($documentationFiles.Contains($fullFileName) -or
        $fileChanged.StartsWith('docs/') -or
        $fileChanged.StartsWith('examples/'))
    {
        $added = $labelsToAdd.Add("documentation")
    }

    if ($fileChanged.StartsWith('build/') -or
        $fileChanged.StartsWith('.github/') -or
        $rootInfraFiles.Contains($fullFileName) -or
        $fileExtension -eq ".props" -or
        $fileExtension -eq ".targets" -or
        $fileChanged.StartsWith('test\openTelemetry.aotcompatibility'))
    {
        $added = $labelsToAdd.Add("infra")
    }

    if ($fileChanged.StartsWith('test\benchmarks'))
    {
        $added = $labelsToAdd.Add("perf")
    }

    if ($fullFileName -eq 'directory.packages.props')
    {
        $added = $labelsToAdd.Add("dependencies")
    }
  }

  foreach ($labelOnPullRequest in $labelsOnPullRequest)
  {
     if ($labelsToAdd.Contains($labelOnPullRequest.name))
     {
        $removed = $labelsToAdd.Remove($labelOnPullRequest.name)
     }
     elseif ($labelOnPullRequest.name.StartsWith($labelPackagePrefix) -or
        $managedLabels.Contains($labelOnPullRequest.name))
     {
        $added = $labelsToRemove.Add($labelOnPullRequest.name)
     }
  }

  if ($labelsToAdd.Count -gt 0)
  {
      foreach ($label in $labelsToAdd)
      {
        gh pr edit $pullRequestNumber --add-label $label
      }
  }

  if ($labelsToRemove.Count -gt 0)
  {
      foreach ($label in $labelsToRemove)
      {
        gh pr edit $pullRequestNumber --remove-label $label
      }
  }
}

Export-ModuleMember -Function AddLabelsOnPullRequestsBasedOnFilesChanged
