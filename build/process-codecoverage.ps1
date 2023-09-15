$files = Get-ChildItem "TestResults" -Filter "*.coverage" -Recurse
Write-Host $env:USERPROFILE
foreach ($file in $files)
{
    $command = 'dotnet coverage merge --output-format xml --output ' + $file.DirectoryName + '\' + $file.Name + '.xml '+ $file.FullName
    Write-Host $command
    Invoke-Expression $command
}
