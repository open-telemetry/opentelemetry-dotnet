Release process.

Only for Maintainers.

1. Tag with version to be released eg: git tag -a 0.4.0-beta -m ".4.0.0-beta"
   git push origin 0.4.0-beta

2. Submit a PR and merge it. (any harmless PR) To trigger CI for master. (no
   longer required, but confirm)

3. https://github.com/open-telemetry/opentelemetry-dotnet/actions?query=workflow%3A%22Pack+and+publish+to+Myget%22
   Wait for the above CI build pipeline to finish. Its triggered when the above
   PR is merged. At the end of this, myget will have the packages.

4. Validate using myget packages. Basic sanity checks :)

5. From the above build, get the artifacts from the drop, which has all the
   nuget packages.

6. Copy the nuget into a local folder.

7. Download latest nuget.exe to this folder: https://www.nuget.org/downloads

8. Obtain the API key from nuget.org (Only maintainers have access)

9. run the following command from powershell from the above folder..

.\nuget.exe setApiKey <your_API_key> get-childitem | where {$_.extension -eq
".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source
https://api.nuget.org/v3/index.json}
