param(
  [Parameter(Mandatory=$true)][int]$issueNumber,
  [Parameter(Mandatory=$true)][string]$issueBody
)

$match = [regex]::Match($issueBody, '^[#]+ Package\s*(OpenTelemetry(?:\.\w+)*)')
if ($match.Success -eq $false)
{
    Return
}

gh issue edit $issueNumber --add-label $("pkg:" + $match.Groups[1].Value)
