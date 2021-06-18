$commonProps = Get-Content -Path 'Common.props' -Raw
$commonProps -match '<MicrosoftCodeCoveragePkgVer>\[(.+)\]</MicrosoftCodeCoveragePkgVer>'
$microsoftCodeCoveragePkgVer = $Matches[1]
$files = Get-ChildItem "TestResults" -Filter "*.coverage" -Recurse
Write-Host $env:USERPROFILE
foreach ($file in $files)
{
    $command = $env:USERPROFILE+ '\.nuget\packages\microsoft.codecoverage\' + $microsoftCodeCoveragePkgVer + '\build\netstandard1.0\CodeCoverage\CodeCoverage.exe analyze /output:' + $file.DirectoryName + '\' + $file.Name + '.xml '+ $file.FullName
    Write-Host $command
    Invoke-Expression $command
}
