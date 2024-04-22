param(
  [Parameter(Mandatory=$true)][string]$minVerTagPrefix
)

$projectDirs = Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$minVerTagPrefix</MinVerTagPrefix>" -List | Select Path | Split-Path -Parent

foreach ($projectDir in $projectDirs) {
  $path = Join-Path -Path $projectDir -ChildPath "CHANGELOG.md"

  $directory = Split-Path $Path -Parent | Split-Path -Leaf

    $lines = Get-Content -Path $path

  $headingWritten = $false
  $started = $false
  $content = ""

  foreach ($line in $lines)
  {
    if ($line -like "## Unreleased" -and $started -ne $true)
    {
      $started = $true
    }
    elseif ($line -like "## *" -and $started -eq $true)
    {
      break
    }
    else
    {
        if ($started -eq $true)
        {
            $content += $line + "`r`n"
        }
    }
  }

  if ([string]::IsNullOrWhitespace($content) -eq $false)
  {
    Add-Content -Path ".\$($minVerTagPrefix)combinedchangelog.md" -Value "**$($directory)**"
    Add-Content -Path ".\$($minVerTagPrefix)combinedchangelog.md" -NoNewline -Value $content
  }
}
