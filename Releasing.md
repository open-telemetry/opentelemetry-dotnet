# Release process

Only for Maintainers.

1. Tag with version to be released e.g.: 

   ```sh
   git tag -a 0.4.0-beta -m "0.4.0-beta"
   git push origin 0.4.0-beta
   ```

2. Wait for the [Pack and publish to MyGet
   workflow](https://github.com/open-telemetry/opentelemetry-dotnet/actions?query=workflow%3A%22Pack+and+publish+to+Myget%22) to finish. It's triggered when the you create a new tag. At the end of this, MyGet will have the packages.

3. Validate using MyGet packages. Basic sanity checks :)

4. From the above build, get the artifacts from the drop, which has all the
   nuget packages and symbols (*.snupkg files).

5. Copy all the nuget files and symbols into a local folder.

6. Download latest [nuget.exe](https://www.nuget.org/downloads).

7. Obtain the API key from nuget.org (Only maintainers have access)

8. Run the following command from PowerShell from the above folder.

   ```powershell
   .\nuget.exe setApiKey <your_API_key> get-childitem | where {$_.extension -eq
   ".nupkg"} | foreach ($_) {.\nuget.exe push $_.fullname -Source
   https://api.nuget.org/v3/index.json}
   ```
