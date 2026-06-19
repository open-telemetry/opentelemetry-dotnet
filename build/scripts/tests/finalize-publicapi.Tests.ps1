#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $script:scriptPath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "finalize-publicapi.ps1"
}

Describe "finalize-publicapi.ps1" {

    It "merges, sorts and de-duplicates unshipped public APIs into shipped" -Skip:(-not $IsWindows) {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry"
        $apiDirectory = Join-Path -Path $project -ChildPath ".publicApi/Stable"

        New-Item -Path $apiDirectory -ItemType Directory -Force | Out-Null

        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>core-</MinVerTagPrefix></PropertyGroup></Project>"

        $shippedPath = Join-Path -Path $apiDirectory -ChildPath "PublicAPI.Shipped.txt"
        $unshippedPath = Join-Path -Path $apiDirectory -ChildPath "PublicAPI.Unshipped.txt"

        Set-Content -Path $shippedPath -Value "B`nA`n"
        Set-Content -Path $unshippedPath -Value "C`nA`n"

        Push-Location -Path $work -ErrorAction Stop
        try {
            & $scriptPath -minVerTagPrefix "core-" 6>$null
        }
        finally {
            Pop-Location
        }

        $shipped = @(Get-Content -Path $shippedPath | Where-Object { $_ -ne "" })
        $shipped | Should -Be @("A", "B", "C") -Because "shipped should be the sorted, de-duplicated union of both files"

        (Get-Content -Path $unshippedPath -Raw) | Should -BeNullOrEmpty -Because "the unshipped file should be emptied once merged"
    }

    It "does not touch projects that do not match the tag prefix" {
        $work = Join-Path -Path $TestDrive -ChildPath (New-Guid)

        $project = Join-Path -Path $work -ChildPath "src/OpenTelemetry.Other"
        $apiDirectory = Join-Path -Path $project -ChildPath ".publicApi/Stable"

        New-Item -Path $apiDirectory -ItemType Directory -Force | Out-Null

        Set-Content `
            -Path (Join-Path -Path $project -ChildPath "OpenTelemetry.Other.csproj") `
            -Value "<Project><PropertyGroup><MinVerTagPrefix>other-</MinVerTagPrefix></PropertyGroup></Project>"

        $unshippedPath = Join-Path -Path $apiDirectory -ChildPath "PublicAPI.Unshipped.txt"
        Set-Content -Path $unshippedPath -Value "ShouldRemain"

        Push-Location -Path $work -ErrorAction Stop
        try {
            & $scriptPath -minVerTagPrefix "core-" 6>$null
        }
        finally {
            Pop-Location
        }

        (Get-Content -Path $unshippedPath -Raw).Trim() | Should -Be "ShouldRemain" -Because "projects that do not match the tag prefix should be left untouched"
    }
}
