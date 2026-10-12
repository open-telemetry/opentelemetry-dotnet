$ErrorActionPreference = 'Continue'

$dumpDir = Join-Path $env:RUNNER_TEMP 'crash-dumps'
$dumps = @(Get-ChildItem -Path $dumpDir -Filter '*.dmp' -ErrorAction SilentlyContinue | Select-Object -First 3)
if ($dumps.Count -eq 0) {
  Write-Host 'No crash dumps found to analyze.'
  exit 0
}

$toolPath = Join-Path $env:RUNNER_TEMP 'dotnet-dump'
dotnet tool install dotnet-dump --tool-path $toolPath --version $env:DOTNET_DUMP_VERSION
if ($LASTEXITCODE -ne 0) {
  Write-Host '::warning::Failed to install dotnet-dump, skipping crash dump analysis.'
  exit 0
}

$dotnetDump = Join-Path $toolPath 'dotnet-dump.exe'

function Invoke-Sos([string] $Dump, [string[]] $Commands, [string] $RuntimeDirectory) {
  $arguments = @('analyze', $Dump)
  if ($RuntimeDirectory) {
    # Windows PowerShell 5.1 needs the embedded quotes escaped to keep the path together.
    $arguments += @('-c', ('setclrpath \"' + $RuntimeDirectory + '\"'))
  }
  foreach ($command in $Commands) {
    $arguments += @('-c', $command)
  }
  $arguments += @('-c', 'exit')
  & $dotnetDump $arguments 2>&1 | ForEach-Object { "$_" }
}

foreach ($dump in $dumps) {
  Write-Host "::group::Analysis of $($dump.Name)"
  $report = Join-Path $dumpDir "analysis-$($dump.BaseName).txt"

  # The runtime that crashed in pwsh is the one bundled next to pwsh.exe, so
  # point SOS at it to find the matching mscordaccore.dll.
  $runtimeDirectory = $null
  if ($dump.Name -like 'pwsh*') {
    $runtimeDirectory = Split-Path -Parent (Get-Command pwsh -ErrorAction SilentlyContinue).Source
  }

  # Pass 1: overview of the process, plus the integrity of the heap and sync blocks.
  $overview = Invoke-Sos $dump.FullName @(
    'sosstatus',
    'clrthreads',
    'clrstack -all',
    'syncblk -all',
    'dumpheap -stat',
    'dumpheap -type System.__ComObject',
    'verifyheap',
    'eeheap -gc') $runtimeDirectory

  # Pass 2: for the suspect threads, show their arguments, locals and stack objects.
  # The thread that hit the fatal error has a System.ExecutionEngineException in the Exception
  # column of the clrthreads table, and no managed frames, so it is selected from there first.
  $threads = @()
  foreach ($line in $overview) {
    if ($line -match '^\s*\S+\s+\d+\s+([0-9a-fA-F]+)\s+[0-9a-fA-F]{16}\s+.*ExecutionEngineException') {
      $threads += $Matches[1]
    }
  }

  # Then any thread that is in the middle of COM interop.
  $blocks = ($overview -join "`n") -split '(?=OS Thread Id: )'
  foreach ($block in $blocks) {
    # The capturing match must come last as it sets $Matches.
    if ($block -match 'IL_STUB|GetCOMIPFromRCW|CreateElevatedEntry|TaskbarJumpList' -and
        $block -match 'OS Thread Id: 0x([0-9a-fA-F]+)') {
      $threads += $Matches[1]
    }
  }

  $suspects = @()
  foreach ($thread in ($threads | Select-Object -Unique -First 5)) {
    # SOS expects the OS thread id in decimal.
    $osThreadId = [Convert]::ToUInt32($thread, 16)
    $stack = Invoke-Sos $dump.FullName @("setthread -t $osThreadId", 'clrstack -a', 'dumpstackobjects') $runtimeDirectory
    $suspects += "===== Thread 0x$thread (suspect) ====="
    $suspects += $stack

    # Pass 3: describe every distinct object found on that thread's stack.
    # Only the lines after the dumpstackobjects header list objects (clrstack lines look similar).
    $header = [Array]::FindIndex([string[]]$stack, [Predicate[string]]{ param($line) $line -match 'SP/REG\s+Object' })
    $addresses = @()
    if ($header -ge 0) {
      $addresses = @($stack[($header + 1)..($stack.Count - 1)] |
        ForEach-Object { if ($_ -match '^\s*[0-9a-fA-F]{8,16}\s+([0-9a-fA-F]{8,16})\s+\S') { $Matches[1] } } |
        Select-Object -Unique -First 25)
    }
    if ($addresses.Count -gt 0) {
      $suspects += "===== Objects referenced from thread 0x$thread ====="
      $suspects += Invoke-Sos $dump.FullName ($addresses | ForEach-Object { "dumpobj $_" }) $runtimeDirectory
    }
  }

  # Pass 4: describe any object that verifyheap found without the sync block index in its header
  # that the sync block table says it should have.
  $mismatches = @($overview |
    ForEach-Object { if ($_ -match 'Object ([0-9a-fA-F]+) \S+ should have a SyncBlock index of \d+ but instead had no SyncBlock') { $Matches[1] } } |
    Select-Object -Unique -First 10)
  if ($mismatches.Count -gt 0) {
    $suspects += '===== Objects with a missing sync block index in their header ====='
    $commands = @()
    foreach ($address in $mismatches) {
      $commands += @("dumpobj $address", "gcwhere $address", "gcroot $address")
    }
    $suspects += Invoke-Sos $dump.FullName $commands $runtimeDirectory
  }

  $output = @('===== Overview =====') + $overview + $suspects
  $output | Out-File -FilePath $report -Encoding utf8
  $output | ForEach-Object { Write-Host $_ }
  Write-Host '::endgroup::'
}
