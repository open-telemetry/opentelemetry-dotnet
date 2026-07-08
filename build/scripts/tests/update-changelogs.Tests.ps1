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

        # The script formats the release date using the invariant culture, so
        # the expected value is computed the same way.
        $expectedReleaseDate = [System.DateTime]::Now.ToString('yyyy-MMM-dd', [System.Globalization.CultureInfo]::InvariantCulture)

        $matchingChangelog = Get-Content -Path (Join-Path -Path $matchingProject -ChildPath "CHANGELOG.md") -Raw
        $matchingChangelog | Should-MatchString "## 1\.2\.3" -Because "the version heading should be added for a matching project"
        $matchingChangelog | Should-BeLikeString "*Released $expectedReleaseDate*" -Because "a release date should be added for a matching project"
        $matchingChangelog | Should-MatchString "\* Some change" -Because "existing changelog entries should be preserved"

        $otherChangelog = Get-Content -Path (Join-Path -Path $otherProject -ChildPath "CHANGELOG.md") -Raw
        $otherChangelog | Should-NotMatchString "## 1\.2\.3" -Because "projects with a different tag prefix should not be updated"
        $otherChangelog | Should-MatchString "Unreleased" -Because "the unreleased heading should be left untouched for non-matching projects"
    }

    It "writes the release date using the invariant culture" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") `
            -Value "# Changelog`n`nUnreleased`n`n* Some change`n"

        # The invariant date does not depend on the current culture, so it is
        # safe to compute it up front.
        $expectedReleaseDate = [System.DateTime]::Now.ToString('yyyy-MMM-dd', [System.Globalization.CultureInfo]::InvariantCulture)

        $originalCulture = [System.Globalization.CultureInfo]::CurrentCulture
        try {
            # Run the script under a non-English culture; if the date were
            # formatted using the current culture the month would be localized
            # (e.g. "juin" under fr-FR) instead of the invariant "Jun".
            [System.Globalization.CultureInfo]::CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo('fr-FR')

            Push-Location -Path $work -ErrorAction Stop
            try {
                & $scriptPath -minVerTagPrefix "core-" -version "1.2.3" 6>$null
            }
            finally {
                Pop-Location
            }
        }
        finally {
            [System.Globalization.CultureInfo]::CurrentCulture = $originalCulture
        }

        $changelog = Get-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Raw
        $changelog | Should-BeLikeString "*Released $expectedReleaseDate*" -Because "the release date should use invariant (en-US) formatting even under a non-English culture"
    }
}
