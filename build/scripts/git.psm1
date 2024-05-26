$gitHubBotUserName="opentelemetrybot"
$gitHubBotEmail="170672328+CodeBlanchBot@users.noreply.github.com" #107717825+opentelemetrybot@users.noreply.github.com

$repoViewResponse = gh repo view --json nameWithOwner | ConvertFrom-Json

$gitRepository = $repoViewResponse.nameWithOwner

Export-ModuleMember -Variable gitHubBotUserName
Export-ModuleMember -Variable gitHubBotEmail
Export-ModuleMember -Variable gitRepository
