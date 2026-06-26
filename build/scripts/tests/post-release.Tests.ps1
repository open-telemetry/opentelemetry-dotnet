#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "post-release.psm1"

    # Define a stub for the GitHub CLI if it is not installed so that Pester is
    # always able to mock it. The real 'gh' is never invoked by these tests.
    if (-not (Get-Command -Name "gh" -ErrorAction SilentlyContinue)) {
        function global:gh { throw "The 'gh' command should have been mocked but was invoked with: $args" }
    }

    Import-Module -Name $modulePath -Force
}

AfterAll {
    Remove-Module -Name "post-release" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "function:gh" -Force -ErrorAction SilentlyContinue
}

Describe "GetCoreDependenciesForProjects" {

    It "returns only the dependency map and does not leak command output" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Foo.csproj") -Value "<Project></Project>"

        $assetsDirectory = Join-Path -Path $work -ChildPath "artifacts/obj/OpenTelemetry.Foo"
        New-Item -Path $assetsDirectory -ItemType Directory -Force | Out-Null

        $assets = @'
{
  "OpenTelemetry": {
    "target": "Package",
    "version": "1.9.0"
  },
  "Newtonsoft.Json": {
    "target": "Package",
    "version": "13.0.0"
  }
}
'@
        Set-Content -Path (Join-Path -Path $assetsDirectory -ChildPath "project.assets.json") -Value $assets

        $result = InModuleScope -ModuleName "post-release" -Parameters @{ Work = $work } {
            param($Work)

            # Simulate 'dotnet restore' writing progress output to the success
            # stream. If that output is not suppressed it pollutes the return value.
            Mock -CommandName "dotnet" -MockWith {
                Write-Output "Determining projects to restore..."
                Write-Output "Restored OpenTelemetry.Foo.csproj"
            }

            Push-Location -Path $Work -ErrorAction Stop
            try {
                GetCoreDependenciesForProjects
            }
            finally {
                Pop-Location
            }
        }

        @($result).Count | Should -Be 1 -Because "'dotnet restore' output must be piped to Out-Null so it does not leak into the return value"
        $result | Should -BeOfType [hashtable] -Because "the function should return only the dependency map"

        $dependencies = $result.Values | Select-Object -First 1
        $dependencies["OpenTelemetry"] | Should -Be "1.9.0" -Because "the version should be parsed from project.assets.json"
        $dependencies.ContainsKey("Newtonsoft.Json") | Should -BeFalse -Because "only OpenTelemetry core packages should be tracked"
    }
}

Describe "CreateDraftRelease" {

    BeforeEach {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Foo.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>foo-</MinVerTagPrefix></PropertyGroup></Project>"
    }

    It "creates a non-prerelease draft release with notes from the CHANGELOG" {
        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 1.9.0

* Added a new feature.

## 1.8.0

Released 2024-01-01
"@

        Mock -CommandName "gh" -ModuleName "post-release" -MockWith { }

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreateDraftRelease -gitRepository "open-telemetry/opentelemetry-dotnet" -tag "foo-1.9.0" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "release" -and
            $args -contains "create" -and
            $args -contains "foo-1.9.0" -and
            $args -contains "--latest" -and
            $args -contains "--draft" -and
            (($args -join " ") -match "OpenTelemetry.Foo v1\.9\.0") -and
            (($args -join " ") -match "Added a new feature\.")
        } -Because "a stable release should be created as a draft with notes built from the CHANGELOG"
    }

    It "creates a prerelease draft release for a prerelease tag" {
        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## 1.9.0-alpha.1

* A prerelease change.

## 1.8.0

Released 2024-01-01
"@

        Mock -CommandName "gh" -ModuleName "post-release" -MockWith { }

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreateDraftRelease -gitRepository "open-telemetry/opentelemetry-dotnet" -tag "foo-1.9.0-alpha.1" 6>$null
        }
        finally {
            Pop-Location
        }

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "create" -and
            $args -contains "foo-1.9.0-alpha.1" -and
            $args -contains "--prerelease" -and
            $args -contains "--draft"
        } -Because "a prerelease tag should create a prerelease draft"
    }

    It "throws when no projects match the tag prefix" {
        Push-Location -Path $work -ErrorAction Stop
        try {
            { CreateDraftRelease -gitRepository "open-telemetry/opentelemetry-dotnet" -tag "missing-1.0.0" 6>$null } |
                Should -Throw "*No projects found*" -Because "no project uses the 'missing-' tag prefix"
        }
        finally {
            Pop-Location
        }
    }
}

Describe "InvokeCoreVersionUpdateWorkflowInRemoteRepository" {

    It "dispatches the core version update workflow with the tag" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith { }

        InvokeCoreVersionUpdateWorkflowInRemoteRepository `
            -remoteGitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
            -tag "core-1.2.3"

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "workflow" -and
            $args -contains "run" -and
            $args -contains "core-version-update.yml" -and
            $args -contains "tag=core-1.2.3"
        } -Because "the remote workflow should be dispatched with the tag passed through"
    }

    It "throws when the tag cannot be parsed" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith { }

        {
            InvokeCoreVersionUpdateWorkflowInRemoteRepository `
                -remoteGitRepository "open-telemetry/opentelemetry-dotnet-contrib" `
                -tag "1.2.3"
        } | Should -Throw "*Could not parse prefix or version*" -Because "a tag without a prefix cannot be parsed"
    }
}

Describe "TryPostReleasePublishedNoticeOnPrepareReleasePullRequest" {

    It "posts a published notice on the matching prepare release pull request" {
        Mock -CommandName "git" -ModuleName "post-release" -MockWith {
            $global:LASTEXITCODE = 0
            return "abc123"
        }
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "list") {
                return '[{"number":42,"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","comments":[{"author":{"login":"otelbot-comment"},"body":"The packages for [core-1.2.3](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.2.3) are now available: [Download](https://example.com)."}]}]'
            }
            return $null
        }

        TryPostReleasePublishedNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -expectedPrAuthorUserName "otelbot" `
            -expectedCommentAuthorUserName "otelbot-comment" `
            -tag "core-1.2.3" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "has been published")
        } -Because "a published notice should be posted on the matching prepare release PR"
    }

    It "does nothing when no prepare release pull request is found" {
        Mock -CommandName "git" -ModuleName "post-release" -MockWith {
            $global:LASTEXITCODE = 0
            return "abc123"
        }
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "list") { return "[]" }
            return $null
        }

        TryPostReleasePublishedNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -expectedPrAuthorUserName "otelbot" `
            -expectedCommentAuthorUserName "otelbot-comment" `
            -tag "core-1.2.3" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Times 0 -ParameterFilter {
            $args -contains "comment"
        } -Because "no notice should be posted when there is no matching pull request"
    }

    It "does nothing when the matching pull request has no packages-ready comment" {
        Mock -CommandName "git" -ModuleName "post-release" -MockWith {
            $global:LASTEXITCODE = 0
            return "abc123"
        }
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "list") {
                return '[{"number":42,"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","comments":[]}]'
            }
            return $null
        }

        TryPostReleasePublishedNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -expectedPrAuthorUserName "otelbot" `
            -expectedCommentAuthorUserName "otelbot-comment" `
            -tag "core-1.2.3" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Times 0 -ParameterFilter {
            $args -contains "comment"
        } -Because "a notice is only posted when the packages-ready comment is present"
    }
}

Describe "TryPostPackagesReadyNoticeOnPrepareReleasePullRequest" {

    It "posts a packages-ready notice on the matching pull request" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "list") {
                return '[{"number":42,"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","comments":[{"author":{"login":"otelbot-comment"},"body":"I just pushed the [core-1.2.3](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.2.3) tag."}]}]'
            }
            return $null
        }

        TryPostPackagesReadyNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -tag "core-1.2.3" `
            -tagSha "abc123" `
            -packagesUrl "https://example.com/packages" `
            -expectedPrAuthorUserName "otelbot" `
            -expectedCommentAuthorUserName "otelbot-comment" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "are now available")
        } -Because "a packages-ready notice should be posted on the matching prepare release PR"
        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "unlock"
        } -Because "the PR is unlocked to allow the comment"
        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "lock"
        } -Because "the PR is locked again after commenting"
    }

    It "does nothing when no pull request matches the search" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "list") {
                return '[{"number":42,"author":{"login":"someone-else"},"title":"unrelated","comments":[]}]'
            }
            return $null
        }

        TryPostPackagesReadyNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -tag "core-1.2.3" `
            -tagSha "abc123" `
            -packagesUrl "https://example.com/packages" `
            -expectedPrAuthorUserName "otelbot" `
            -expectedCommentAuthorUserName "otelbot-comment" 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Times 0 -ParameterFilter {
            $args -contains "comment"
        } -Because "no notice should be posted when no pull request matches"
    }
}

Describe "PushPackagesPublishReleaseUnlockAndPostNoticeOnPrepareReleasePullRequest" {

    It "publishes the release without pushing to NuGet when not requested" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "view") {
                return '{"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","comments":[{"author":{"login":"otelbot-comment"},"body":"The packages for [core-1.2.3](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.2.3) are now available: [Download](https://example.com)."}]}'
            }
            if ($args -contains "api") {
                return '{"user":{"permissions":{"maintain":true}}}'
            }
            return $null
        }

        PushPackagesPublishReleaseUnlockAndPostNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -pullRequestNumber "789" `
            -expectedPrAuthorUserName "otelbot" `
            -expectedCommentAuthorUserName "otelbot-comment" `
            -commentUserName "maintainer" `
            -artifactDownloadPath "$TestDrive/artifacts" `
            -pushToNuget $false 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "release" -and $args -contains "edit" -and $args -contains "--draft=false"
        } -Because "the release should be published by clearing the draft flag"
    }

    It "pushes packages to NuGet when requested" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "view") {
                return '{"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","comments":[{"author":{"login":"otelbot-comment"},"body":"The packages for [core-1.2.3](https://github.com/open-telemetry/opentelemetry-dotnet/releases/tag/core-1.2.3) are now available: [Download](https://example.com)."}]}'
            }
            if ($args -contains "api") {
                return '{"user":{"permissions":{"maintain":true}}}'
            }
            return $null
        }
        Mock -CommandName "dotnet" -ModuleName "post-release" -MockWith { $global:LASTEXITCODE = 0 }

        PushPackagesPublishReleaseUnlockAndPostNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -pullRequestNumber "789" `
            -expectedPrAuthorUserName "otelbot" `
            -expectedCommentAuthorUserName "otelbot-comment" `
            -commentUserName "maintainer" `
            -artifactDownloadPath "$TestDrive/artifacts" `
            -pushToNuget $true 6>$null

        Should -Invoke -CommandName "dotnet" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "nuget" -and $args -contains "push"
        } -Because "packages should be pushed to NuGet when requested"
    }

    It "refuses to push when the commenter is not a maintainer" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "view") {
                return '{"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","comments":[]}'
            }
            if ($args -contains "api") {
                return '{"user":{"permissions":{"maintain":false}}}'
            }
            return $null
        }

        PushPackagesPublishReleaseUnlockAndPostNoticeOnPrepareReleasePullRequest `
            -gitRepository "open-telemetry/opentelemetry-dotnet" `
            -pullRequestNumber "789" `
            -expectedPrAuthorUserName "otelbot" `
            -expectedCommentAuthorUserName "otelbot-comment" `
            -commentUserName "not-a-maintainer" `
            -artifactDownloadPath "$TestDrive/artifacts" `
            -pushToNuget $false 6>$null

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "comment" -and (($args -join " ") -match "don't have permission to push packages")
        } -Because "non-maintainers should be told they cannot push packages"
        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Times 0 -ParameterFilter {
            $args -contains "release" -and $args -contains "edit"
        } -Because "the release should not be published when permission is denied"
    }

    It "throws when the packages-ready comment is missing" {
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if ($args -contains "view") {
                return '{"author":{"login":"otelbot"},"title":"[release] Prepare release core-1.2.3","comments":[]}'
            }
            if ($args -contains "api") {
                return '{"user":{"permissions":{"maintain":true}}}'
            }
            return $null
        }

        {
            PushPackagesPublishReleaseUnlockAndPostNoticeOnPrepareReleasePullRequest `
                -gitRepository "open-telemetry/opentelemetry-dotnet" `
                -pullRequestNumber "789" `
                -expectedPrAuthorUserName "otelbot" `
                -expectedCommentAuthorUserName "otelbot-comment" `
                -commentUserName "maintainer" `
                -artifactDownloadPath "$TestDrive/artifacts" `
                -pushToNuget $false 6>$null
        } | Should -Throw "*Could not find package push comment*" -Because "the packages-ready comment must exist before publishing"
    }
}

Describe "CreateStableVersionUpdatePullRequest" {

    It "throws when the version cannot be parsed from the tag" {
        { CreateStableVersionUpdatePullRequest -gitRepository "open-telemetry/opentelemetry-dotnet" -tag "noprefix" 6>$null } |
            Should -Throw "*Could not parse version from tag*" -Because "a tag without a prefix has no version to extract"
    }

    It "updates the stable version, opens a PR and updates affected CHANGELOGs" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Foo"
        New-Item -Path $project -ItemType Directory -Force | Out-Null

        Set-Content `
            -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") `
            -Value "<Project><PropertyGroup><OTelLatestStableVer>1.0.0</OTelLatestStableVer></PropertyGroup></Project>"

        Set-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Value @"
# Changelog

## Unreleased

* An existing change.

## 1.0.0

Released 2024-01-01
"@

        Mock -CommandName "git" -ModuleName "post-release" -MockWith { $global:LASTEXITCODE = 0 }
        Mock -CommandName "gh" -ModuleName "post-release" -MockWith {
            if (($args -contains "pr") -and ($args -contains "create")) {
                return "https://github.com/open-telemetry/opentelemetry-dotnet/pull/789"
            }
            return $null
        }
        # Mock the internal dependency lookup to avoid running 'dotnet restore'.
        # It reads the current OTelLatestStableVer so the "before" and "after"
        # snapshots differ once the script updates Directory.Packages.props.
        Mock -CommandName "GetCoreDependenciesForProjects" -ModuleName "post-release" -MockWith {
            $version = (Select-String -Path "Directory.Packages.props" -Pattern "<OTelLatestStableVer>(.*?)</OTelLatestStableVer>").Matches[0].Groups[1].Value
            return @{ (Join-Path -Path $PWD -ChildPath "src/OpenTelemetry.Foo") = @{ "OpenTelemetry" = $version } }
        }

        Push-Location -Path $work -ErrorAction Stop
        try {
            CreateStableVersionUpdatePullRequest -gitRepository "open-telemetry/opentelemetry-dotnet" -tag "core-1.2.3" 6>$null
        }
        finally {
            Pop-Location
        }

        (Get-Content -Path (Join-Path -Path $work -ChildPath "Directory.Packages.props") -Raw) |
            Should -Match "<OTelLatestStableVer>1\.2\.3</OTelLatestStableVer>" -Because "the stable version should be bumped in Directory.Packages.props"

        Should -Invoke -CommandName "gh" -ModuleName "post-release" -Exactly -Times 1 -ParameterFilter {
            $args -contains "pr" -and $args -contains "create" -and $args -contains "--label" -and $args -contains "release"
        } -Because "a labelled pull request should be opened for the stable update"

        (Get-Content -Path (Join-Path -Path $project -ChildPath "CHANGELOG.md") -Raw) |
            Should -Match "Updated OpenTelemetry core component version\(s\) to ``1\.2\.3``" -Because "the CHANGELOG of an affected project should be updated"
    }
}
