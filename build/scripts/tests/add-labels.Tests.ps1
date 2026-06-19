#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "add-labels.psm1"

    # Define a stub for the GitHub CLI if it is not installed so that Pester is
    # always able to mock it. The real 'gh' is never invoked by these tests.
    if (-not (Get-Command -Name "gh" -ErrorAction SilentlyContinue)) {
        function global:gh { throw "The 'gh' command should have been mocked but was invoked with: $args" }
    }

    Import-Module -Name $modulePath -Force
}

AfterAll {
    Remove-Module -Name "add-labels" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "function:gh" -Force -ErrorAction SilentlyContinue
}

Describe "AddLabelsOnIssuesForPackageFoundInBody" {

    It "adds a package label when a package is referenced in the issue body" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith { }

        AddLabelsOnIssuesForPackageFoundInBody `
            -issueNumber 123 `
            -issueBody "## Package`n`nOpenTelemetry.Exporter.Console"

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "issue" -and
            $args -contains "edit" -and
            $args -contains "--add-label" -and
            $args -contains "pkg:OpenTelemetry.Exporter.Console"
        } -Because "the package named in the body should be added as a 'pkg:' label"
    }

    It "does nothing when no package is referenced in the issue body" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith { }

        AddLabelsOnIssuesForPackageFoundInBody `
            -issueNumber 123 `
            -issueBody "This issue does not mention a package."

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 0 -Because "no label should be changed when no package is referenced"
    }
}

Describe "AddLabelsOnPullRequestsBasedOnFilesChanged" {

    # Note: the mock bodies execute in the module's scope (because of
    # -ModuleName) so they must return literal values rather than referencing
    # variables defined in the test scope.

    It "adds package and infrastructure labels based on the files changed" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith {
            if (($args -contains "label") -and ($args -contains "list")) {
                return '[{"name":"pkg:OpenTelemetry.Api","id":"1"},{"name":"infra","id":"2"},{"name":"documentation","id":"3"}]'
            }
            if ($args -contains "diff") {
                return @("src/OpenTelemetry.Api/Internal/Foo.cs", "build/scripts/post-release.psm1")
            }
            if ($args -contains "view") {
                return '{"labels":[]}'
            }
            return $null
        }

        AddLabelsOnPullRequestsBasedOnFilesChanged -pullRequestNumber 456 -labelPackagePrefix "pkg:"

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "pkg:OpenTelemetry.Api"
        } -Because "a change under src/OpenTelemetry.Api should add the matching package label"
        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "infra"
        } -Because "a change under build/ should add the infra label"
    }

    It "removes a managed label that no longer applies" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith {
            if (($args -contains "label") -and ($args -contains "list")) {
                return '[{"name":"pkg:OpenTelemetry.Api","id":"1"},{"name":"infra","id":"2"},{"name":"documentation","id":"3"}]'
            }
            if ($args -contains "diff") {
                return @("README.md")
            }
            if ($args -contains "view") {
                return '{"labels":[{"name":"infra"}]}'
            }
            return $null
        }

        AddLabelsOnPullRequestsBasedOnFilesChanged -pullRequestNumber 456 -labelPackagePrefix "pkg:"

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "documentation"
        } -Because "README.md is a documentation file so the documentation label should be added"
        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--remove-label" -and $args -contains "infra"
        } -Because "the existing infra label no longer applies and should be removed"
    }

    It "adds perf and dependencies labels and does not re-add an existing label" {
        Mock -CommandName "gh" -ModuleName "add-labels" -MockWith {
            if (($args -contains "label") -and ($args -contains "list")) {
                return '[{"name":"pkg:OpenTelemetry.Api","id":"1"},{"name":"infra","id":"2"}]'
            }
            if ($args -contains "diff") {
                return @(
                    "src/OpenTelemetry.Api/Foo.cs",
                    "test/OpenTelemetry.Foo.Benchmarks/Bench.cs",
                    "test/benchmarks/Suite.cs",
                    "Directory.Packages.props"
                )
            }
            if ($args -contains "view") {
                return '{"labels":[{"name":"pkg:OpenTelemetry.Api"}]}'
            }
            return $null
        }

        AddLabelsOnPullRequestsBasedOnFilesChanged -pullRequestNumber 456 -labelPackagePrefix "pkg:"

        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "perf"
        } -Because "benchmark and stress projects should add the perf label"
        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Exactly -Times 1 -ParameterFilter {
            $args -contains "edit" -and $args -contains "--add-label" -and $args -contains "dependencies"
        } -Because "changes to Directory.Packages.props should add the dependencies label"
        Should -Invoke -CommandName "gh" -ModuleName "add-labels" -Times 0 -ParameterFilter {
            $args -contains "--add-label" -and $args -contains "pkg:OpenTelemetry.Api"
        } -Because "a label already present on the pull request should not be added again"
    }
}
