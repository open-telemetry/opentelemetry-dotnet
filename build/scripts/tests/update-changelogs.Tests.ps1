#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $script:scriptPath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "update-changelogs.ps1"
}

Describe "update-changelogs.ps1" {

    It "updates the CHANGELOG only for projects matching the tag prefix" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $matchingProject = Join-Path -Path $work -ChildPath "src/OpenTelemetry"
        $otherProject = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Other"

        New-Item -Path $matchingProject -ItemType Directory -Force | Out-Null
        New-Item -Path $otherProject -ItemType Directory -Force | Out-Null

        Set-Content `
            -Path (Join-Path -Path $matchingProject -ChildPath "OpenTelemetry.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"
        Set-Content `
            -Path (Join-Path -Path $otherProject -ChildPath "OpenTelemetry.Other.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>other-</MinVerTagPrefix></PropertyGroup></Project>"

        Set-Content `
            -Path (Join-Path -Path $matchingProject -ChildPath "CHANGELOG.md") `
            -Value "# Changelog`n`nUnreleased`n`n* Some change`n"
        Set-Content `
            -Path (Join-Path -Path $otherProject -ChildPath "CHANGELOG.md") `
            -Value "# Changelog`n`nUnreleased`n`n* Other change`n"

        Push-Location -Path $work -ErrorAction Stop
        try {
            # 6> redirects the Information stream so the test output stays quiet.
            & $scriptPath -minVerTagPrefix "core-" -version "1.2.3" 6>$null
        }
        finally {
            Pop-Location
        }

        $matchingChangelog = Get-Content -Path (Join-Path -Path $matchingProject -ChildPath "CHANGELOG.md") -Raw
        $matchingChangelog | Should -Match "## 1\.2\.3" -Because "the version heading should be added for a matching project"
        $matchingChangelog | Should -Match "Released \d{4}-\w{3}-\d{2}" -Because "a release date should be added for a matching project"
        $matchingChangelog | Should -Match "\* Some change" -Because "existing changelog entries should be preserved"

        $otherChangelog = Get-Content -Path (Join-Path -Path $otherProject -ChildPath "CHANGELOG.md") -Raw
        $otherChangelog | Should -Not -Match "## 1\.2\.3" -Because "projects with a different tag prefix should not be updated"
        $otherChangelog | Should -Match "Unreleased" -Because "the unreleased heading should be left untouched for non-matching projects"
    }
}
