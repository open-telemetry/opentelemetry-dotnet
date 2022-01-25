# Release Process

Only for Maintainers.

## Prerequisites

- Install [GitHub CLI](https://cli.github.com/)

## Steps

1. Decide the tag name (version name) to be released.
1. Obtain the API key from [nuget.org](https://www.nuget.org/profiles/OpenTelemetry) <!-- TODO: improve URL -->
1. Run the following script from the root of the repository

   `./build/Release.ps1 $TagName $NugetApiKey $CoreComponents $NonCoreComponents`
   Example:

   ```powershell
   $TagName = "1.0.0-rc2"
   $NugetApiKey = "<INSERT_API_KEY_HERE>"
   $CoreComponents = $True
   $NonCoreComponents = $False
   ```
