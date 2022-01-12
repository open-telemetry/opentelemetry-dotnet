# Logs

## Setup

1. Install the `Microsoft.Extensions.Logging` package

    ```sh
    dotnet add package Microsoft.Extensions.Logging
    ```

1. Update `Program.cs` with the following

    ```{literalinclude} ../../logs/getting-started/Program.cs
    :language: c#
    :lines: 28-
    ```

1. Run the application

    ```sh
    dotnet run
    ```

1. You should see the following output

    ```{literalinclude} ../../logs/getting-started/Program.cs
    :language: text
    :lines: 18-25
    ```

Congratulations! You are now collecting logs using OpenTelemetry.

## What does the above program do?

The program uses the [`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger) API to log a formatted string with a severity level of `Information`. Click [here](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel) for more information on the different logs levels. OpenTelemetry captures this and sends it to `ConsoleExporter`.
