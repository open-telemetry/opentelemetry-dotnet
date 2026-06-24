#!/usr/bin/env pwsh

# Copyright The OpenTelemetry Authors
# SPDX-License-Identifier: Apache-2.0

#Requires -PSEdition Core
#Requires -Version 7

param(
  [Parameter()][string]$repoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
)

$ErrorActionPreference = "Stop"

function GetUnreleasedChanges {
  param([string]$changelogPath)

  $content = (Get-Content $changelogPath -Raw) -replace "`r`n", "`n"
  $match = [regex]::Match($content, '## Unreleased[ \t]*\n([\s\S]*?)(?=\n## |\z)')
  if (-not $match.Success) {
    return $null
  }

  $section = $match.Groups[1].Value.Trim()
  if ([string]::IsNullOrWhiteSpace($section)) {
    return $null
  }

  return $section
}

function GetMinVerTagPrefix {
  param([string]$packageDir)

  $csproj = Get-ChildItem -Path $packageDir -Filter "*.csproj" -File | Select-Object -First 1
  if ($null -eq $csproj) {
    return $null
  }

  $content = Get-Content $csproj.FullName -Raw
  $match = [regex]::Match($content, '<MinVerTagPrefix>(.*?)</MinVerTagPrefix>')
  if (-not $match.Success) {
    return $null
  }

  return $match.Groups[1].Value.TrimEnd('-')
}

$changelogs = Get-ChildItem -Path (Join-Path $repoRoot "src") -Filter "CHANGELOG.md" -Recurse

$packages = @()

foreach ($changelog in $changelogs) {
  $unreleased = GetUnreleasedChanges -changelogPath $changelog.FullName
  if ($null -eq $unreleased) {
    continue
  }

  $tagPrefix = GetMinVerTagPrefix -packageDir $changelog.Directory.FullName

  $packages += [PSCustomObject]@{
    Name              = $changelog.Directory.Name
    TagPrefix         = $tagPrefix
    UnreleasedChanges = $unreleased
  }
}

$packages = @($packages | Sort-Object Name)

if ($packages.Count -eq 0) {
  @("> [!TIP]", "> No packages have any unreleased changes.") -join "`n"
  return
}

$lines = [System.Collections.Generic.List[string]]::new()

$lines.Add("# Unreleased Changes")

$groups = $packages | Group-Object TagPrefix | Sort-Object Name

foreach ($group in $groups) {
  $groupName = if ($group.Name) { $group.Name } else { 'unknown' }
  $lines.Add("")
  $lines.Add("## ``$groupName`` packages")
  $lines.Add("")
  $lines.Add("| Package |")
  $lines.Add("| :--- |")

  foreach ($pkg in $group.Group) {
    $lines.Add("| :package: $($pkg.Name) |")
  }
}

foreach ($group in $groups) {
  $groupName = if ($group.Name) { $group.Name } else { 'unknown' }
  $lines.Add("")
  $lines.Add("## ``$groupName`` package changes")

  foreach ($pkg in $group.Group) {
    $lines.Add("")
    $lines.Add("### $($pkg.Name)")
    $lines.Add("")
    $lines.Add($pkg.UnreleasedChanges)
  }
}

$lines -join "`n"
