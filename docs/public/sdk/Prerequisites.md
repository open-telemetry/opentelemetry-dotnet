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

## OpenTelemetry .NET special note

Metrics in OpenTelemetry .NET is a somewhat unique implementation of the
OpenTelemetry project, as most of the Metrics API are incorporated directly
into the .NET runtime itself. From a high level, what this means is that you
can instrument your application by simply depending on
`System.Diagnostics.DiagnosticSource` package.
