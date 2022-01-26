param (
    [Parameter(Mandatory)]
    # https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
    [ValidatePattern("^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$")]
    [string]
    $TagName,
    [Parameter(Mandatory)]
    [ValidatePattern("[0-9a-z]{46}")]
    [string]
    $NugetApiKey,
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [bool]
    $CoreComponents,
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [bool]
    $NonCoreComponents
)

Function PressKeyToContinue {
    param (
        $Instruction
    )

    Write-Host $Instruction
    Write-Host -NoNewLine "Press any key to continue..."
    $Null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# Ensure branch is clean, otherwise abort
$ChangedFiles = $(git status --porcelain | Measure-Object | Select-Object -Expand Count)
if ($ChangedFiles -gt 0) {
    Write-Error "Aborting. Found uncommitted changes." -ErrorAction Stop
}

# Checkout `main` branch and create new branch for changes
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
gh pr create --title $ChangelogTitle # TODO --reviewer @reyang
PressKeyToContinue("Merge the PR")

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
$DownloadDirectory = "$HOME/Downloads"
gh workflow run "publish-packages-1.0.yml"
gh run download --name "windows-latest-packages" --dir $DownloadDirectory
Push-Location "$DownloadDirectory/src"
Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "src/nuget.exe"

# TODO automate this delete?
If (-not $NonCoreComponents) {
    Explorer "."
    PressKeyToContinue("Delete Core Packages")
}

# Upload to NuGet
./nuget.exe setApiKey $NugetApiKey
ForEach ($NuPkgPath in Get-ChildItem -Path "." -Filter "*.nupkg" -Recurse) {
    ./nuget.exe push $NuPkgPath.fullname -Source "https://api.nuget.org/v3/index.json"
}
Pop-Location

Start-Process "https://www.nuget.org/profiles/OpenTelemetry"
PressKeyToContinue("Validate the package is uploaded to NuGet (takes a few minutes)")

# GitHub Release
gh release create $TagName --notes $CombinedChangelog
# TODO add taggings for metrics release?
# TODO separate version from instrumentation/hosting/OTshim package

# Update `OTelPreviousStableVer` in `Common.props`, if needed
$CommonPropsFile = "build/Common.props"
# TODO verify this means a stable release
If (-not $TagName.Contains("-")) {
    [xml] $CommonProps = Get-Content $CommonPropsFile
    $OTelPreviousStableVer = $CommonProps.Project.PropertyGroup.OTelPreviousStableVer

    If ($OTelPreviousStableVer -ne $TagName) {
        # Start new branch from upstream/main
        $OTelVersionBranch = "otel-version-$TagName"
        $OTelVersionTitle = "Update OTelPreviousStableVer in $CommonPropsFile - $TagName"
        git fetch upstream main
        git checkout main
        git merge upstream/main
        git checkout -b $OTelVersionBranch

        # Update XML element with newest version
        $Lines = Get-Content -Path $CommonPropsFile
        $Old = "<OTelPreviousStableVer>$OTelPreviousStableVer</OTelPreviousStableVer>"
        $New = "<OTelPreviousStableVer>$TagName</OTelPreviousStableVer>"
        $Lines -Replace $Old, $New | Set-Content -Path $CommonPropsFile

        # Create PR
        git commit -am $OTelVersionTitle
        git push --set-upstream origin $OTelVersionBranch
        gh pr create --title $OTelVersionTitle
    }
}

# Update OpenTelemetry.io Document
# TODO what do you change here?
Start-Process "https://github.com/open-telemetry/opentelemetry.io/tree/main/content/en/docs/instrumentation/net"
PressKeyToContinue("Update OpenTelemetry.io Document")
