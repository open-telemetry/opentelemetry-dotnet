function InvokeCommandWithRetry {
  param(
    [Parameter(Mandatory=$true)][hashtable]$command,
    [Parameter()][int]$maxAttempts = 3,
    [Parameter()][int]$retryDelaySeconds = 5
  )

  # The command is a hashtable with the following keys:
  #   Name        - Description of the command used in the logs (e.g. "Signing foo.dll").
  #   Command     - The executable to run (e.g. "cosign").
  #   Arguments   - The arguments to pass to the executable. These are never logged.
  #   OutputPath  - Optional path to write the standard output of the command to if it succeeds.

  $ErrorActionPreference = "Stop"

  $arguments = @($command.Arguments)
  $log = [System.Collections.Generic.List[string]]::new()
  $warnings = [System.Collections.Generic.List[string]]::new()
  $succeeded = $false

  for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    $log.Add("Attempt $attempt of $($maxAttempts):")

    try {
      $output = & $command.Command @arguments 2>&1
      $exitCode = $LASTEXITCODE
    }
    catch {
      $output = @($_.ToString())
      $exitCode = -1
    }

    $standardOutput = @($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] })

    # Do not log the standard output if it is being written to a file
    foreach ($line in ($command.OutputPath ? ($output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }) : $output)) {
      $log.Add("    $line")
    }

    if ($exitCode -eq 0) {
      if ($command.OutputPath) {
        $standardOutput | Set-Content -Path $command.OutputPath
        $log.Add("    Wrote $($standardOutput.Count) line(s) of output to $($command.OutputPath)")
      }
      $succeeded = $true
      break
    }

    $warnings.Add("$($command.Name) failed on attempt $attempt of $maxAttempts with exit code $exitCode.")

    if ($attempt -lt $maxAttempts -and $retryDelaySeconds -gt 0) {
      Start-Sleep -Seconds (Get-Random -Minimum ($retryDelaySeconds * $attempt) -Maximum (2 * $retryDelaySeconds * $attempt))
    }
  }

  return [pscustomobject]@{
    Name = $command.Name
    Succeeded = $succeeded
    Attempt = $succeeded ? $attempt : $maxAttempts
    Log = $log
    Warnings = $warnings
  }
}

Export-ModuleMember -Function InvokeCommandWithRetry

function InvokeCommandsWithRetry {
  param(
    [Parameter(Mandatory=$true)][hashtable[]]$commands,
    [Parameter()][int]$maxParallelism = 8,
    [Parameter()][int]$maxAttempts = 3,
    [Parameter()][int]$retryDelaySeconds = 5
  )

  # The commands are run in parallel (as they are dominated by network latency, not CPU)
  # and each one is retried with a randomized backoff to tolerate transient errors from remote
  # services. See InvokeCommandWithRetry for the keys each command hashtable supports.

  $modulePath = $PSCommandPath
  $failed = 0

  $commands | ForEach-Object -ThrottleLimit $maxParallelism -Parallel {
    Import-Module -Name $using:modulePath
    InvokeCommandWithRetry -command $_ -maxAttempts $using:maxAttempts -retryDelaySeconds $using:retryDelaySeconds
  } | ForEach-Object {
    $status = $_.Succeeded ? "succeeded on attempt $($_.Attempt)" : "failed"
    Write-Output "::group::$($_.Name) $status"
    $_.Log | Write-Output
    Write-Output "::endgroup::"

    $_.Warnings | ForEach-Object { Write-Output "::warning::$_" }

    if (-not $_.Succeeded) {
      Write-Output "::error::$($_.Name) failed after $($_.Attempt) attempts."
      $failed++
    }
  }

  if ($failed -gt 0) {
    throw "$failed of $($commands.Count) command(s) failed."
  }
}

Export-ModuleMember -Function InvokeCommandsWithRetry

function InitializeCosignTrustedMaterial {
  param(
    [Parameter(Mandatory=$true)][ref]$trustedRoot,
    [Parameter(Mandatory=$true)][ref]$signingConfig,
    [Parameter()][int]$maxAttempts = 3
  )

  # cosign refreshes its TUF cache on every invocation, which fails when multiple
  # processes share the same cache concurrently (particularly on Windows), so fetch the
  # trusted root and signing configuration once into a dedicated cache and pass the
  # files to each cosign invocation explicitly so that the TUF cache is not used at all.
  $tufRoot = Join-Path ([System.IO.Path]::GetTempPath()) "sigstore-tuf-$([System.Guid]::NewGuid())"
  $previousTufRoot = $env:TUF_ROOT

  try {
    $env:TUF_ROOT = $tufRoot

    InvokeCommandsWithRetry `
      -commands @(@{ Name = "Initializing Sigstore trusted root"; Command = "cosign"; Arguments = @("initialize") }) `
      -maxParallelism 1 `
      -maxAttempts $maxAttempts
  }
  finally {
    $env:TUF_ROOT = $previousTufRoot
  }

  $targets = Join-Path $tufRoot "tuf-repo-cdn.sigstore.dev" "targets"
  $trustedRoot.Value = Join-Path $targets "trusted_root.json"
  $signingConfig.Value = Join-Path $targets "signing_config.v0.2.json"

  foreach ($path in @($trustedRoot.Value, $signingConfig.Value)) {
    if (-not (Test-Path -Path $path -PathType Leaf)) {
      throw "cosign initialize did not create the expected file '$path'."
    }
  }
}

function SignFilesWithCosign {
  param(
    [Parameter(Mandatory=$true)][string[]]$files,
    [Parameter()][int]$maxParallelism = 8,
    [Parameter()][int]$maxAttempts = 3
  )

  $trustedRoot = $null
  $signingConfig = $null

  InitializeCosignTrustedMaterial `
    -trustedRoot ([ref]$trustedRoot) `
    -signingConfig ([ref]$signingConfig) `
    -maxAttempts $maxAttempts

  $commands = $files | ForEach-Object {
    @{
      Name = "Signing $_"
      Command = "cosign"
      Arguments = @(
        "sign-blob",
        "--yes",
        "--timeout", "60s",
        "--bundle", "$_.sigstore.json",
        "--signing-config", $signingConfig,
        "--trusted-root", $trustedRoot,
        $_
      )
    }
  }

  InvokeCommandsWithRetry -commands $commands -maxParallelism $maxParallelism -maxAttempts $maxAttempts
}

Export-ModuleMember -Function SignFilesWithCosign

function VerifyCosignSignatures {
  param(
    [Parameter(Mandatory=$true)][string[]]$files,
    [Parameter(Mandatory=$true)][string]$certificateIdentity,
    [Parameter(Mandatory=$true)][string]$certificateOidcIssuer,
    [Parameter()][int]$maxParallelism = 8,
    [Parameter()][int]$maxAttempts = 3
  )

  $trustedRoot = $null
  $signingConfig = $null

  InitializeCosignTrustedMaterial `
    -trustedRoot ([ref]$trustedRoot) `
    -signingConfig ([ref]$signingConfig) `
    -maxAttempts $maxAttempts

  $commands = $files | ForEach-Object {
    @{
      Name = "Verifying signature of $_"
      Command = "cosign"
      Arguments = @(
        "verify-blob",
        "--bundle", "$_.sigstore.json",
        "--certificate-identity", $certificateIdentity,
        "--certificate-oidc-issuer", $certificateOidcIssuer,
        "--trusted-root", $trustedRoot,
        "--use-signed-timestamps",
        $_
      )
    }
  }

  InvokeCommandsWithRetry -commands $commands -maxParallelism $maxParallelism -maxAttempts $maxAttempts
}

Export-ModuleMember -Function VerifyCosignSignatures

function VerifyAttestations {
  param(
    [Parameter(Mandatory=$true)][string[]]$files,
    [Parameter(Mandatory=$true)][string]$repository,
    [Parameter()][int]$maxParallelism = 8,
    [Parameter()][int]$maxAttempts = 3
  )

  # As with cosign, concurrent invocations of the GitHub CLI fail to initialize
  # the shared Sigstore TUF cache, so fetch the trusted root once and pass it explicitly.
  $trustedRoot = Join-Path ([System.IO.Path]::GetTempPath()) "trusted_root-$([System.Guid]::NewGuid()).jsonl"

  InvokeCommandsWithRetry `
    -commands @(@{ Name = "Fetching Sigstore trusted root"; Command = "gh"; Arguments = @("attestation", "trusted-root"); OutputPath = $trustedRoot }) `
    -maxParallelism 1 `
    -maxAttempts $maxAttempts

  $commands = $files | ForEach-Object {
    @{
      Name = "Verifying attestation for $_"
      Command = "gh"
      Arguments = @(
        "attestation",
        "verify",
        "--repo", $repository,
        "--custom-trusted-root", $trustedRoot,
        $_
      )
    }
  }

  InvokeCommandsWithRetry -commands $commands -maxParallelism $maxParallelism -maxAttempts $maxAttempts
}

Export-ModuleMember -Function VerifyAttestations
