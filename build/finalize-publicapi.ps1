param(
  [Parameter(Mandatory=$true)][string]$minVerTagPrefix
)

# For stable releases "Unshipped" PublicApi text files are merged into "Shipped" versions.

$projectDirs = Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$minVerTagPrefix</MinVerTagPrefix>" -List | Select Path | Split-Path -Parent

$path = "\.publicApi\**\PublicAPI.Shipped.txt";

foreach ($projectDir in $projectDirs) {
    $searchPath = Join-Path -Path $projectDir -ChildPath $path;

    Write-Host "Search glob: $searchPath";

    Get-ChildItem -Path $searchPath -Recurse |
        ForEach-Object {
            Write-Host "Shipped: $_";

            [string]$shipped = $_.FullName;
            [string]$unshipped = $shipped -replace ".shipped.txt", ".Unshipped.txt";

            if (Test-Path $unshipped) {
                Write-Host "Unshipped: $unshipped";

                Get-Content $shipped, $unshipped |  # get contents of both text files
                    Where-Object {$_ -ne ""} |      # filter empty lines
                    Sort-Object |                   # sort lines
                    Get-Unique |                    # filter duplicates
                    Set-Content $shipped;           # write to shipped.txt

                Clear-Content $unshipped;           # empty unshipped.txt

                Write-Host "...MERGED and SORTED";
            }

            Write-Host "";
        }
}
