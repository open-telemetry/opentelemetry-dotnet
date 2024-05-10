param(
  [Parameter(Mandatory=$true)][int]$issueNumber,
  [Parameter(Mandatory=$true)][string]$issueBody
)

$match = [regex]::Match($issueBody, '^[#]+ Area\s*?(area:\w+)')
if ($match.Success -eq $false)
{
    Return
}

gh issue edit $issueNumber `
  --add-label $match.Groups[1].Value
