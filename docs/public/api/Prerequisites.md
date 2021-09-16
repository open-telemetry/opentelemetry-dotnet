# Prerequisites

1. Download and install the [.NET Core SDK](https://dotnet.microsoft.com/download)
1. Create a new console application and run it
    ```sh
    dotnet new console -o myApp
    cd myApp
    dotnet run
    ```
1. You should see the following output
    ```text
    Hello World!
    ```
1. Install the `OpenTelemetry.Exporter.Console` package
    ```sh
    dotnet add package --prerelease OpenTelemetry.Exporter.Console
    ```

Now that you have a working dotnet console application you can follow the next steps for the kind of telemetry you wish to add: logs, metrics, traces.
