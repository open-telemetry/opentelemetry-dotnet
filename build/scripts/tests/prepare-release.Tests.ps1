#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "prepare-release.psm1"

    # Define a stub for the GitHub CLI if it is not installed so that Pester is
    # always able to mock it. The real 'gh' is never invoked by these tests.
    if (-not (Get-Command -Name "gh" -ErrorAction SilentlyContinue)) {
        function global:gh { throw "The 'gh' command should have been mocked but was invoked with: $args" }
    }

    Import-Module -Name $modulePath -Force
}

AfterAll {
    Remove-Module -Name "prepare-release" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "function:gh" -Force -ErrorAction SilentlyContinue
}

Describe "CreatePullRequestToUpdateChangelogsAndPublicApis" {

    BeforeEach {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        # Create stubs for the scripts that are invoked via a relative path so
        # the real CHANGELOG/public API files are never modified.
        $stubScripts = Join-Path -Path $work -ChildPath "build/scripts"
        New-Item -Path $stubScripts -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $stubScripts -ChildPath "update-changelogs.ps1") `
            -Value "param([string]`$minVerTagPrefix, [string]`$version)"
        Set-Content `
            -Path (Join-Path -Path $stubScripts -ChildPath "finalize-publicapi.ps1") `
            -Value "param([string]`$minVerTagPrefix)"

        # RELEASENOTES.md already contains the version so no extra comment is posted.
        Set-Content -Path (Join-Path -Path $work -ChildPath "RELEASENOTES.md") -Value "# Release Notes`n`n## 1.2.3`n`nNotes."

        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 0 }
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if (($args -contains "pr") -and ($args -contains "create")) {
                return "https://github.com/open-telemetry/opentelemetry-dotnet/pull/789"
            }
            return $null
        }
    }

    It "throws when the version <Version> is not valid" -ForEach @(
        @{ Version = "not-a-version" }
        @{ Version = "1.2" }
        @{ Version = "1.2.3.4" }
        @{ Version = "1.2.3-preview.1" }
    ) {
        {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -minVerTagPrefix "core-" `
                -version $Version `
                -requestedByUserName "someone"
        } | Should -Throw "*did not match expected format*" -Because "'$Version' is not a valid release version"
    }

    It "opens a pull request to prepare a stable release" {
        Push-Location -Path $work -ErrorAction Stop
        try {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -minVerTagPrefix "core-" `
                -version "1.2.3" `
                -requestedByUserName "someone" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "git" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "switch" -and $args -contains "--create" -and ($args -join " ") -match "otelbot/prepare-core-1\.2\.3-release"
        } -Because "a release branch should be created for the version"

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "pr" -and
            $args -contains "create" -and
            $args -contains "--label" -and
            $args -contains "release" -and
            (($args -join " ") -match "\[release\] Prepare release core-1\.2\.3")
        } -Because "a labelled pull request should be opened for the release"
    }

    It "configures the git user when a name and email are provided" {
        Push-Location -Path $work -ErrorAction Stop
        try {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -minVerTagPrefix "core-" `
                -version "1.2.3" `
                -requestedByUserName "someone" `
                -gitUserName "otelbot" `
                -gitUserEmail "otelbot@example.com" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "git" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "config" -and $args -contains "user.name" -and $args -contains "otelbot"
        } -Because "the git user name should be configured when provided"
        Should -Invoke -CommandName "git" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "config" -and $args -contains "user.email" -and $args -contains "otelbot@example.com"
        } -Because "the git user email should be configured when provided"
    }

    It "comments when RELEASENOTES.md is missing the version being released" {
        Set-Content -Path (Join-Path -Path $work -ChildPath "RELEASENOTES.md") -Value "# Release Notes`n`nNo entry yet."

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -minVerTagPrefix "core-" `
                -version "1.2.3" `
                -requestedByUserName "someone" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "RELEASENOTES")
        } -Because "the author should be prompted to add release notes when they are missing"
    }

    It "prepares a prerelease without finalizing public APIs" {
        Push-Location -Path $work -ErrorAction Stop
        try {
            CreatePullRequestToUpdateChangelogsAndPublicApis `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -minVerTagPrefix "core-" `
                -version "1.2.3-alpha.1" `
                -requestedByUserName "someone" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "create" -and (($args -join " ") -match "Prepare release core-1\.2\.3-alpha\.1")
        } -Because "a prerelease pull request should still be opened"
    }

    It "throws when creating the release branch fails" {
        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 1 }

        Push-Location -Path $work -ErrorAction Stop
        try {
            {
                CreatePullRequestToUpdateChangelogsAndPublicApis `
                    -gitRepository "open-telemetry/opentelemetry-dotnet" `
                    -minVerTagPrefix "core-" `
                    -version "1.2.3" `
                    -requestedByUserName "someone" 6>$null
            } | Should -Throw "*git switch failure*" -Because "a failure to create the branch should stop the release"
        }
        finally {
            Pop-Location
        }
    }

    It "throws when the pull request number cannot be parsed" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith { return "no url here" }

        Push-Location -Path $work -ErrorAction Stop
        try {
            {
                CreatePullRequestToUpdateChangelogsAndPublicApis `
                    -gitRepository "open-telemetry/opentelemetry-dotnet" `
                    -minVerTagPrefix "core-" `
                    -version "1.2.3" `
                    -requestedByUserName "someone" 6>$null
            } | Should -Throw "*Could not parse pull request number*" -Because "the PR URL is required to continue"
        }
        finally {
            Pop-Location
        }
    }
}

Describe "LockPullRequestAndPostNoticeToCreateReleaseTag" {

    It "posts a notice and locks the pull request" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","mergeCommit":{"oid":"abc123"}}'
            }
            return $null
        }

        LockPullRequestAndPostNoticeToCreateReleaseTag `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -pullRequestNumber "789" `
            -expectedPrAuthorUserName "otelbot"

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "/CreateReleaseTag")
        } -Because "a comment offering to create the release tag should be posted"
        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "lock"
        } -Because "the pull request should be locked after posting the notice"
    }

    It "throws when the pull request author is not the expected user" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            return '{"author":{"login":"someone-else"},"title":"[release] Prepare release core-1.2.3","mergeCommit":{"oid":"abc123"}}'
        }

        {
            LockPullRequestAndPostNoticeToCreateReleaseTag `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot"
        } | Should -Throw "*PR author was unexpected*" -Because "only pull requests opened by the expected bot should be processed"
    }
}

Describe "CreateReleaseTagAndPostNoticeOnPullRequest" {

    It "creates and pushes the release tag and posts a notice" {
        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 0 }
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","mergeCommit":{"oid":"abc123"}}'
            }
            return $null
        }

        CreateReleaseTagAndPostNoticeOnPullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -pullRequestNumber "789" `
            -expectedPrAuthorUserName "otelbot" 6>$null

        Should -Invoke -CommandName "git" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "tag" -and $args -contains "core-1.2.3" -and $args -contains "abc123"
        } -Because "the release tag should be created on the merge commit"
        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "core-1\.2\.3")
        } -Because "a notice about the pushed tag should be posted on the pull request"
    }
}

Describe "UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest" {

    BeforeEach {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"

        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 0 }
    }

    It "updates the release date and comments on the pull request" {
        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 1.2.3

Released 0000-00-00

* An item.
"@

        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-core-1.2.3-release","author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3"}'
            }
            if ($args -contains "api") {
                return '{"permission":"write"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "maintainer" 6>$null
        }
        finally {
            Pop-Location
        }

        $changelog = Get-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Raw
        $changelog | Should -Match "Released \d{4}-\w{3}-\d{2}" -Because "the placeholder release date should be replaced with today's date"
        $changelog | Should -Not -Match "0000-00-00" -Because "the placeholder date should no longer be present"

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "I updated the CHANGELOG release dates")
        } -Because "a notice should confirm the dates were updated"
    }

    It "comments that no update was needed when the dates are already valid" {
        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 9.9.9

Released 2020-01-01
"@

        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-core-1.2.3-release","author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3"}'
            }
            if ($args -contains "api") {
                return '{"permission":"write"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "maintainer" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "valid release dates")
        } -Because "the function reports when no CHANGELOG needed updating"
    }

    It "refuses to update when the commenter lacks write permission" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-core-1.2.3-release","author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3"}'
            }
            if ($args -contains "api") {
                return '{"permission":"read"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateChangelogReleaseDatesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "drive-by" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "don't have permission")
        } -Because "only maintainers and approvers may update the PR"
    }
}

Describe "UpdateReleaseNotesAndPostNoticeOnPullRequest" {

    BeforeEach {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        New-Item -Path $work -ItemType Directory -Force | Out-Null
        Set-Content -Path (Join-Path -Path $work -ChildPath "RELEASENOTES.md") -Value "# Release Notes`n`n## 1.0.0`n`nOld notes.`n`n##"
        Mock -CommandName "git" -ModuleName "prepare-release" -MockWith { $global:LASTEXITCODE = 0 }
    }

    It "adds the release notes and comments on the pull request" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-core-1.2.3-release","author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3"}'
            }
            if ($args -contains "api") {
                return '{"permission":"admin"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateReleaseNotesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "maintainer" `
                -commentBody "/UpdateReleaseNotes`n`nThese are the new release notes." 6>$null
        }
        finally {
            Pop-Location
        }

        $releaseNotes = Get-Content -Path (Join-Path -Path $work -ChildPath "RELEASENOTES.md") -Raw
        $releaseNotes | Should -Match "## 1\.2\.3" -Because "a section for the released version should be added"
        $releaseNotes | Should -Match "These are the new release notes\." -Because "the supplied notes should be inserted"

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "RELEASENOTES")
        } -Because "a notice should confirm the release notes were updated"
    }

    It "declines to add release notes for a prerelease" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-core-1.2.3-alpha.1-release","author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3-alpha.1"}'
            }
            if ($args -contains "api") {
                return '{"permission":"admin"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateReleaseNotesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "maintainer" `
                -commentBody "/UpdateReleaseNotes`n`nNotes." 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "prereleases")
        } -Because "release notes are not added for prereleases or unstable packages"
    }

    It "refuses to update when the commenter lacks write permission" {
        Mock -CommandName "gh" -ModuleName "prepare-release" -MockWith {
            if ($args -contains "view") {
                return '{"headRefName":"otelbot/prepare-core-1.2.3-release","author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3"}'
            }
            if ($args -contains "api") {
                return '{"permission":"read"}'
            }
            return $null
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            UpdateReleaseNotesAndPostNoticeOnPullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -commentUserName "drive-by" `
                -commentBody "/UpdateReleaseNotes`n`nNotes." 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "prepare-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "don't have permission")
        } -Because "only maintainers and approvers may update the PR"
    }
}
