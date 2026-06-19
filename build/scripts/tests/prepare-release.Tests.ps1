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

        Push-Location -Path $work
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
