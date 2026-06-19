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

            Push-Location -Path $Work
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

        Push-Location -Path $work
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

        Push-Location -Path $work
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
        Push-Location -Path $work
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
}
