Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

$trimmingWarningCount = 0
$aotWarningCount = 0
foreach($line in Get-Content .\build\myoutput.log) 
{
    Write-Host $line
    if ($line -like "*Trim analysis warning IL*") 
    { 
        $trimmingWarningCount += 1
    } elseif ($line -like "*AOT analysis warning IL*") 
    {
        $aotWarningCount += 1
    }
}

$expectedTrimmingWarningCount = 0
$expectedaotWarningCount = 0

Write-Host "trimmingWarningCount: ", $trimmingWarningCount
Write-Host "aotWarningCount: ", $aotWarningCount

$testPassed = 0
if ($trimmingWarningCount -ne $expectedTrimmingWarningCount ) 
{
    $testPassed = 1
    Write-Host "trimmingWarningCount: ", $trimmingWarningCount, "is not as expected. Expected count is:", $expectedTrimmingWarningCount
}

if ($aotWarningCount -ne $expectedaotWarningCount ) 
{
    $testPassed = 1
    Write-Host "aotWarningCount: ", $aotWarningCount, "is not as expected. Expected count is:", $expectedaotWarningCount 
}

Exit $testPassed
