#Requires -PSEdition Core
#Requires -Version 7

BeforeAll {
    $modulePath = Join-Path -Path (Split-Path -Path $PSScriptRoot -Parent) -ChildPath "publish-packages.psm1"

    Import-Module -Name $modulePath -Force

    # Use PowerShell itself to run scripts that emulate the real commands, as the
    # commands are run as native executables (and in parallel in separate runspaces)
    # so cannot be mocked by Pester.
    $pwsh = (Get-Process -Id $PID).Path

    function NewFakeCommand {
        param(
            [Parameter(Mandatory = $true)][string]$Name,
            [Parameter(Mandatory = $true)][string]$Script,
            [Parameter()][string[]]$Arguments = @()
        )

        $scriptPath = Join-Path -Path $TestDrive -ChildPath "$(New-Guid).ps1"
        Set-Content -Path $scriptPath -Value $Script

        return @{
            Name = $Name
            Command = $pwsh
            Arguments = @("-NoProfile", "-NonInteractive", "-File", $scriptPath) + $Arguments
        }
    }

    function InvokeAndCaptureCommands {
        param(
            [Parameter(Mandatory = $true)][hashtable[]]$Commands,
            [Parameter()][int]$MaxParallelism = 4,
            [Parameter()][int]$MaxAttempts = 3
        )

        $output = $null
        $thrown = $null

        try {
            InvokeCommandsWithRetry `
                -commands $Commands `
                -maxParallelism $MaxParallelism `
                -maxAttempts $MaxAttempts `
                -retryDelaySeconds 0 `
                -OutVariable output | Out-Null
        }
        catch {
            $thrown = $_
        }

        return [pscustomobject]@{
            Output = @($output)
            Error = $thrown
        }
    }
}

AfterAll {
    Remove-Module -Name "publish-packages" -Force -ErrorAction SilentlyContinue
}

Describe "InvokeCommandWithRetry" {

    It "returns the output of a command that succeeds" {
        $command = NewFakeCommand -Name "Good command" -Script "Write-Output 'Hello world'"

        $result = InvokeCommandWithRetry -command $command -retryDelaySeconds 0

        $result.Name | Should-Be "Good command"
        $result.Succeeded | Should-BeTrue
        $result.Attempt | Should-Be 1
        $result.Log | Should-ContainCollection "    Hello world"
        $result.Warnings.Count | Should-Be 0
    }

    It "retries a command that fails transiently" {
        Mock -CommandName "Start-Sleep" -ModuleName "publish-packages" -MockWith { }

        $marker = Join-Path -Path $TestDrive -ChildPath (New-Guid)
        $command = NewFakeCommand -Name "Flaky command" -Script @"
if (Test-Path -Path '$marker') { Write-Output 'Recovered'; exit 0 }
New-Item -Path '$marker' -ItemType File | Out-Null
Write-Error 'Transient failure'
exit 1
"@

        $result = InvokeCommandWithRetry -command $command -retryDelaySeconds 5

        $result.Succeeded | Should-BeTrue -Because "the command succeeded when retried"
        $result.Attempt | Should-Be 2
        $result.Warnings | Should-BeCollection @("Flaky command failed on attempt 1 of 3 with exit code 1.")
        $result.Log | Should-ContainCollection "    Recovered"
        ($result.Log -join "`n") | Should-MatchString "Transient failure"

        Should-Invoke -CommandName "Start-Sleep" -ModuleName "publish-packages" -Exactly -Times 1 -ParameterFilter {
            $Seconds -ge 5 -and $Seconds -lt 10
        }
    }

    It "fails when a command fails on every attempt" {
        $command = NewFakeCommand -Name "Bad command" -Script "exit 42"

        $result = InvokeCommandWithRetry -command $command -maxAttempts 2 -retryDelaySeconds 0

        $result.Succeeded | Should-BeFalse
        $result.Attempt | Should-Be 2
        $result.Warnings | Should-BeCollection @(
            "Bad command failed on attempt 1 of 2 with exit code 42.",
            "Bad command failed on attempt 2 of 2 with exit code 42."
        )
    }

    It "fails when a command cannot be found" {
        $command = @{ Name = "Missing command"; Command = "this-command-does-not-exist-$(New-Guid)"; Arguments = @() }

        $result = InvokeCommandWithRetry -command $command -maxAttempts 1

        $result.Succeeded | Should-BeFalse
        $result.Warnings | Should-BeCollection @("Missing command failed on attempt 1 of 1 with exit code -1.")
        ($result.Log -join "`n") | Should-MatchString "this-command-does-not-exist"
    }

    It "does not log the arguments of the command" {
        $command = NewFakeCommand -Name "Secret command" -Script "exit 0" -Arguments @("--token", "not-a-real-secret")

        $result = InvokeCommandWithRetry -command $command

        $result.Succeeded | Should-BeTrue
        ($result.Log -join "`n") | Should-NotMatchString "not-a-real-secret"
    }

    It "writes the standard output of a command to a file without logging it" {
        $outputPath = Join-Path -Path $TestDrive -ChildPath "$(New-Guid).jsonl"
        $command = NewFakeCommand -Name "Output command" -Script @"
Write-Output '{"line":1}'
Write-Output '{"line":2}'
[Console]::Error.WriteLine('A diagnostic message')
exit 0
"@
        $command.OutputPath = $outputPath

        $result = InvokeCommandWithRetry -command $command

        $result.Succeeded | Should-BeTrue
        Get-Content -Path $outputPath | Should-BeCollection @('{"line":1}', '{"line":2}')
        ($result.Log -join "`n") | Should-NotMatchString '"line"'
        ($result.Log -join "`n") | Should-MatchString "A diagnostic message"
    }
}

Describe "InvokeCommandsWithRetry" {

    It "runs every command in parallel and logs their output" {
        $commands = 1..5 | ForEach-Object {
            NewFakeCommand -Name "Command $_" -Script "Write-Output 'Hello from command $_'"
        }

        $result = InvokeAndCaptureCommands -Commands $commands

        $result.Error | Should-BeNull -Because "all of the commands succeeded"
        foreach ($i in 1..5) {
            $result.Output | Should-ContainCollection "::group::Command $i succeeded on attempt 1"
            $result.Output | Should-ContainCollection "    Hello from command $i"
        }
        @($result.Output | Where-Object { $_ -like "::warning::*" }).Count | Should-Be 0
    }

    It "fails when any command fails on every attempt" {
        $commands = @(
            (NewFakeCommand -Name "Good command" -Script "exit 0"),
            (NewFakeCommand -Name "Bad command" -Script "exit 42")
        )

        $result = InvokeAndCaptureCommands -Commands $commands -MaxAttempts 2

        $result.Error | Should-NotBeNull -Because "one of the commands failed"
        $result.Error.Exception.Message | Should-Be "1 of 2 command(s) failed."
        $result.Output | Should-ContainCollection "::group::Good command succeeded on attempt 1"
        $result.Output | Should-ContainCollection "::group::Bad command failed"
        $result.Output | Should-ContainCollection "::warning::Bad command failed on attempt 1 of 2 with exit code 42."
        $result.Output | Should-ContainCollection "::warning::Bad command failed on attempt 2 of 2 with exit code 42."
        $result.Output | Should-ContainCollection "::error::Bad command failed after 2 attempts."
    }
}

Describe "SignFilesWithCosign" {

    BeforeEach {
        Mock -CommandName "InitializeCosignTrustedMaterial" -ModuleName "publish-packages" -MockWith {
            $trustedRoot.Value = "/tuf/trusted_root.json"
            $signingConfig.Value = "/tuf/signing_config.v0.2.json"
        }
        Mock -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -MockWith { }
    }

    It "signs each file with the trusted material from TUF" {
        SignFilesWithCosign -files @("/bin/a.dll", "/bin/b.dll") -maxParallelism 2 -maxAttempts 5

        Should-Invoke -CommandName "InitializeCosignTrustedMaterial" -ModuleName "publish-packages" -Exactly -Times 1 -ParameterFilter {
            $maxAttempts -eq 5
        }
        Should-Invoke -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -Exactly -Times 1 -ParameterFilter {
            $commands.Count -eq 2 -and
            $maxParallelism -eq 2 -and
            $maxAttempts -eq 5 -and
            $commands[0].Name -eq "Signing /bin/a.dll" -and
            $commands[0].Command -eq "cosign" -and
            ($commands[0].Arguments -join " ") -eq "sign-blob --yes --timeout 60s --bundle /bin/a.dll.sigstore.json --signing-config /tuf/signing_config.v0.2.json --trusted-root /tuf/trusted_root.json /bin/a.dll" -and
            $commands[1].Arguments[-1] -eq "/bin/b.dll"
        }
    }
}

Describe "VerifyCosignSignatures" {

    BeforeEach {
        Mock -CommandName "InitializeCosignTrustedMaterial" -ModuleName "publish-packages" -MockWith {
            $trustedRoot.Value = "/tuf/trusted_root.json"
            $signingConfig.Value = "/tuf/signing_config.v0.2.json"
        }
        Mock -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -MockWith { }
    }

    It "verifies each file with the trusted root from TUF" {
        VerifyCosignSignatures `
            -files @("/pkg/a.dll") `
            -certificateIdentity "https://github.com/owner/repo/.github/workflows/publish.yml@refs/tags/v1" `
            -certificateOidcIssuer "https://token.actions.githubusercontent.com"

        Should-Invoke -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -Exactly -Times 1 -ParameterFilter {
            $commands.Count -eq 1 -and
            $commands[0].Name -eq "Verifying signature of /pkg/a.dll" -and
            $commands[0].Command -eq "cosign" -and
            ($commands[0].Arguments -join " ") -eq "verify-blob --bundle /pkg/a.dll.sigstore.json --certificate-identity https://github.com/owner/repo/.github/workflows/publish.yml@refs/tags/v1 --certificate-oidc-issuer https://token.actions.githubusercontent.com --trusted-root /tuf/trusted_root.json --use-signed-timestamps /pkg/a.dll"
        }
    }
}

Describe "InitializeCosignTrustedMaterial" {

    It "returns the paths of the trusted material fetched by cosign and restores TUF_ROOT" {
        Mock -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -MockWith {
            $targets = Join-Path $env:TUF_ROOT "tuf-repo-cdn.sigstore.dev" "targets"
            New-Item -Path $targets -ItemType Directory -Force | Out-Null
            Set-Content -Path (Join-Path $targets "trusted_root.json") -Value "{}"
            Set-Content -Path (Join-Path $targets "signing_config.v0.2.json") -Value "{}"
        }

        $previous = $env:TUF_ROOT
        $env:TUF_ROOT = "original-value"

        try {
            InModuleScope -ModuleName "publish-packages" {
                $trustedRoot = $null
                $signingConfig = $null

                InitializeCosignTrustedMaterial -trustedRoot ([ref]$trustedRoot) -signingConfig ([ref]$signingConfig)

                $trustedRoot | Should-BeLikeString "*sigstore-tuf-*trusted_root.json"
                $signingConfig | Should-BeLikeString "*sigstore-tuf-*signing_config.v0.2.json"
            }

            $env:TUF_ROOT | Should-Be "original-value"
        }
        finally {
            $env:TUF_ROOT = $previous
        }

        Should-Invoke -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -Exactly -Times 1 -ParameterFilter {
            $commands.Count -eq 1 -and
            $commands[0].Command -eq "cosign" -and
            ($commands[0].Arguments -join " ") -eq "initialize" -and
            $maxParallelism -eq 1
        }
    }

    It "throws if cosign does not create the trusted material" {
        Mock -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -MockWith { }

        {
            InModuleScope -ModuleName "publish-packages" {
                $trustedRoot = $null
                $signingConfig = $null
                InitializeCosignTrustedMaterial -trustedRoot ([ref]$trustedRoot) -signingConfig ([ref]$signingConfig)
            }
        } | Should-Throw -ExceptionMessage "cosign initialize did not create the expected file*"
    }
}

Describe "VerifyAttestations" {

    BeforeEach {
        Mock -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -MockWith { }
    }

    It "fetches the trusted root once and uses it to verify each file" {
        VerifyAttestations -files @("/pkg/a.nupkg", "/pkg/b.snupkg") -repository "owner/repo" -maxParallelism 3

        Should-Invoke -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -Exactly -Times 1 -ParameterFilter {
            $commands.Count -eq 1 -and
            $commands[0].Command -eq "gh" -and
            ($commands[0].Arguments -join " ") -eq "attestation trusted-root" -and
            $commands[0].OutputPath -like "*trusted_root-*.jsonl" -and
            $maxParallelism -eq 1
        }

        Should-Invoke -CommandName "InvokeCommandsWithRetry" -ModuleName "publish-packages" -Exactly -Times 1 -ParameterFilter {
            $commands.Count -eq 2 -and
            $maxParallelism -eq 3 -and
            $commands[0].Name -eq "Verifying attestation for /pkg/a.nupkg" -and
            $commands[0].Command -eq "gh" -and
            ($commands[0].Arguments -join " ") -like "attestation verify --repo owner/repo --custom-trusted-root *trusted_root-*.jsonl /pkg/a.nupkg" -and
            $commands[1].Arguments[-1] -eq "/pkg/b.snupkg"
        }
    }
}
