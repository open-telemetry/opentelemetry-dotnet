#Requires -PSEdition Core
#Requires -Version 7

BeforeDiscovery {
    $scriptsDirectory = Split-Path -Path $PSScriptRoot -Parent

    $script:moduleFiles = Get-ChildItem -Path $scriptsDirectory -Filter "*.psm1" | ForEach-Object {
        @{ Name = $_.Name; Path = $_.FullName }
    }

    $script:scriptFiles = Get-ChildItem -Path $scriptsDirectory -Filter "*.ps1" | ForEach-Object {
        @{ Name = $_.Name; Path = $_.FullName }
    }
}

Describe "PowerShell scripts" {

    # These tests are intentionally simple: just loading and parsing every
    # script guards against syntax errors and other breaking changes that
    # would otherwise lie dormant until a release workflow happens to run.

    Context "the module <Name>" -ForEach $moduleFiles {

        It "can be imported without error" {
            { Import-Module -Name $Path -Force -ErrorAction Stop } | Should -Not -Throw
        }

        It "exports at least one function" {
            $module = Import-Module -Name $Path -Force -PassThru
            try {
                $module.ExportedFunctions.Count | Should -BeGreaterThan 0
            }
            finally {
                Remove-Module -Name $module.Name -Force -ErrorAction SilentlyContinue
            }
        }
    }

    Context "the script <Name>" -ForEach $scriptFiles {

        It "has valid PowerShell syntax" {
            $errors = $null
            $null = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors)
            $errors | Should -BeNullOrEmpty
        }
    }
}
