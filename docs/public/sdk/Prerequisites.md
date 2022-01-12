# Prerequisites

1. Download and install the [.NET Core SDK](https://dotnet.microsoft.com/download)
1. Create a new console application and run it

    ```sh
    dotnet new console --output getting-started
    cd getting-started
    dotnet run
    ```

1. You should see the following output

    ```text
    Hello World!
    ```

1. Install the latest `OpenTelemetry.Exporter.Console` package

    ```sh
    dotnet add package --prerelease OpenTelemetry.Exporter.Console
    ```

Congratulations! You have a working .NET console application.

Follow the getting started guides to add telemetry to your project.
