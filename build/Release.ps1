param (
    [string]$TagName,
    [string]$NuGetApiKey,
    [bool]$CoreComponents,
    [bool]$NonCoreComponents
)

function PressKeyToContinue {
    # TODO - make text disappear after pressing the key - write spaces on the same line starting at pos 0?
    Write-Host -NoNewLine "Press any key to continue..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

If (("" -eq $TagName)) {
    Write-Error "Please provide a tag name (version name) for the release." -ErrorAction Stop
}

PressKeyToContinue #TODO remove

# Checkout `main` branch and create new branch for changes
# TODO ensure branch is clean, otherwise abort
$ChangelogBranch = "changelogs-$TagName"
git fetch upstream main
git checkout main
git merge upstream/main
git checkout -b $ChangelogBranch

# CHANGELOG Processing and Updating
$CombinedChangelog = ""
ForEach ($ChangelogPath in Get-ChildItem -Path . -Recurse -Filter CHANGELOG.md) {
    $CombinedChangelog += "**$($ChangelogPath.Directory.Name)**"
    $ChangelogContent = Get-Content -Path $ChangelogPath.FullName
    $IsStarted = $False
    ForEach ($Line in $ChangelogContent) {
        If (-not $IsStarted -and $Line -like "## Unreleased") {
            $IsStarted = $True
        }
        elseif ($IsStarted -and $Line -like "## *") {
            break
        }
        elseif ($IsStarted) {
            $CombinedChangelog += $Line
        }
    }

    $NewFileHeader = "Unreleased`n`n## $TagName`n`nReleased $(Get-Date -UFormat '%Y-%b-%d')"
    (Get-Content -Path $ChangelogPath.FullName) -Replace "Unreleased", $NewFileHeader | Set-Content -Path $ChangelogPath.FullName
}

# Submit PR and Merge
$ChangelogTitle = "CHANGELOG.md Files Update - $TagName"
git commit -am $ChangelogTitle
git push --set-upstream origin $ChangelogBranch
gh pr create --title $ChangelogTitle
# TODO --reviewer @reyang
PressKeyToContinue

# Tag Git with version to be released
git tag -a $TagName -m $TagName
If ($CoreComponents) {
    git tag -a "core-$TagName" -m "$TagName of all core components"
}
If ($NonCoreComponents) {
    git tag -a $TagName -m "$TagName of all non-core components"
}
git push origin $TagName

# Trigger MyGet Build and Download Artifacts from the Drop
Push-Location "$HOME/Downloads"
gh workflow run "publish-packages-1.0.yml"
gh run download --name "windows-latest-packages" --dir .
Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "src/nuget.exe"

If (-not $NonCoreComponents) {
    Write-Output "Delete Core Packages"
    explorer "src"
    PressKeyToContinue
}

# Upload to NuGet
./nuget.exe setApiKey $NuGetApiKey
ForEach ($NuPkgPath in Get-ChildItem -Path "src" -Recurse -Filter "*.nupkg") {
    ./nuget.exe push $NuPkgPath.fullname -Source "https://api.nuget.org/v3/index.json"
}

Write-Output "Validate the package is uploaded to NuGet"
Start-Process "https://www.nuget.org/profiles/OpenTelemetry"
PressKeyToContinue

# GitHub Release
gh release create $TagName --notes $CombinedChangelog
# TODO add taggings for metrics release?
# TODO separate version from instrumentation/hosting/OTshim package
PressKeyToContinue

# Update `OTelPreviousStableVer` in `Common.props` if needed
# TODO verify this means a stable release
$CommonPropsFile = "build/Common.props"
If (-not $TagName.Contains("-")) {
    ForEach ($Line in Get-Content -Path $CommonPropsFile) {
        If ($Line -like "*<OTelPreviousStableVer>*") {
            $StartIndex = $Line.IndexOf(">") + 1
            $EndIndex = $Line.IndexOf("</")
            $OTelPreviousStableVer = $line.Substring($StartIndex, $EndIndex - $StartIndex)
            break
        }
    }
}

If ($OTelPreviousStableVer -and $TagName -ne $OTelPreviousStableVer) {
    $OTelVersionBranch = "otel-version-$TagName"
    $OTelVersionTitle = "Update OTelPreviousStableVer in build/Common.props - $TagName"
    git fetch upstream main
    git checkout main
    git merge upstream/main
    git checkout -b $OTelVersionBranch

    $Old = "<OTelPreviousStableVer>$OTelPreviousStableVer</OTelPreviousStableVer>"
    $New = "<OTelPreviousStableVer>$TagName</OTelPreviousStableVer>"
    $lines -Replace $Old, $New | Set-Content -Path $CommonPropsFile

    git commit -am $OTelVersionTitle
    git push --set-upstream origin $OTelVersionBranch
    gh pr create --title $OTelVersionTitle
}

# Update OpenTelemetry.io Document
Start-Process "https://github.com/open-telemetry/opentelemetry.io/tree/main/content/en/docs/instrumentation/net"
PressKeyToContinue
