#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $script:scriptPath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "report-unreleased-changes.ps1"
}

Describe "report-unreleased-changes.ps1" {

    # The script accepts a -repoRoot parameter and only reads files, so every
    # test points it at an isolated fixture under $TestDrive. The real working
    # tree is never read or written.

    It "reports unreleased changes grouped by tag prefix" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $package = Join-Path -Path $work -ChildPath "src/OpenTelemetry"
        New-Item -Path $package -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $package -ChildPath "OpenTelemetry.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"
        Set-Content -Path (Join-Path -Path $package -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## Unreleased

* Added the thing.

## 1.0.0

Released 2024-01-01
"@

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString "# Unreleased Changes" -Because "the report should have a top-level heading"
        $output | Should-MatchString '## `core` packages' -Because "packages should be grouped by their (trimmed) tag prefix"
        $output | Should-MatchString ':package: OpenTelemetry' -Because "the package should be listed in the summary table"
        $output | Should-MatchString '### OpenTelemetry' -Because "the package should have a changes section"
        $output | Should-MatchString '\* Added the thing\.' -Because "the unreleased changes should be included"
    }

    It "reports that there are no unreleased changes when none are present" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $package = Join-Path -Path $work -ChildPath "src/OpenTelemetry"
        New-Item -Path $package -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $package -ChildPath "OpenTelemetry.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"
        Set-Content -Path (Join-Path -Path $package -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## Unreleased

## 1.0.0

Released 2024-01-01
"@

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString "No packages have any unreleased changes" -Because "an empty unreleased section should be treated as no changes"
        $output | Should-NotMatchString "# Unreleased Changes" -Because "the full report should not be produced when there is nothing to report"
    }

    It "ignores changelogs that have no unreleased section" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $package = Join-Path -Path $work -ChildPath "src/OpenTelemetry"
        New-Item -Path $package -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $package -ChildPath "OpenTelemetry.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"
        Set-Content -Path (Join-Path -Path $package -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 1.0.0

Released 2024-01-01
"@

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString "No packages have any unreleased changes" -Because "a changelog without an Unreleased heading contributes nothing"
    }

    It "omits packages whose unreleased section is empty" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $withChanges = Join-Path -Path $work -ChildPath "src/OpenTelemetry"
        New-Item -Path $withChanges -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $withChanges -ChildPath "OpenTelemetry.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"
        Set-Content -Path (Join-Path -Path $withChanges -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## Unreleased

* A real change.
"@

        $empty = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Exporter.Empty"
        New-Item -Path $empty -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $empty -ChildPath "OpenTelemetry.Exporter.Empty.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"
        Set-Content -Path (Join-Path -Path $empty -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## Unreleased

## 1.0.0
"@

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString ':package: OpenTelemetry \|' -Because "the package with changes should be listed"
        $output | Should-NotMatchString "OpenTelemetry.Exporter.Empty" -Because "the package without changes should be omitted"
    }

    It "groups packages without a resolvable tag prefix under 'unknown'" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        # A package whose csproj has no MinVerTagPrefix element.
        $noPrefix = Join-Path -Path $work -ChildPath "src/OpenTelemetry.NoPrefix"
        New-Item -Path $noPrefix -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $noPrefix -ChildPath "OpenTelemetry.NoPrefix.csproj") `
            -Value "<Project><PropertyGroup></PropertyGroup></Project>"
        Set-Content -Path (Join-Path -Path $noPrefix -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## Unreleased

* A change without a prefix.
"@

        # A package with no csproj at all.
        $noProject = Join-Path -Path $work -ChildPath "src/OpenTelemetry.NoProject"
        New-Item -Path $noProject -ItemType Directory -Force | Out-Null
        Set-Content -Path (Join-Path -Path $noProject -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## Unreleased

* Another change without a project.
"@

        $output = (& $scriptPath -repoRoot $work) -join "`n"

        $output | Should-MatchString '## `unknown` packages' -Because "packages without a tag prefix should be grouped under 'unknown'"
        $output | Should-MatchString ':package: OpenTelemetry.NoPrefix' -Because "a package whose csproj has no MinVerTagPrefix should still be reported"
        $output | Should-MatchString ':package: OpenTelemetry.NoProject' -Because "a package with no csproj should still be reported"
    }

    It "does not modify the working tree" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $package = Join-Path -Path $work -ChildPath "src/OpenTelemetry"
        New-Item -Path $package -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $package -ChildPath "OpenTelemetry.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"
        $changelogPath = Join-Path -Path $package -ChildPath "CHANGELOG.md"
        Set-Content -Path $changelogPath -Value @"
# Changelog

## Unreleased

* A change.
"@
        $before = Get-Content -Path $changelogPath -Raw

        & $scriptPath -repoRoot $work | Out-Null

        Get-Content -Path $changelogPath -Raw | Should-Be $before -Because "the report script must only read, never modify, the changelog files"
    }
}
