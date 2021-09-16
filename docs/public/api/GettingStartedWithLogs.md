# Getting Started With Logs

1. Install the `Microsoft.Extensions.Logging` package
    ```sh
    dotnet add package Microsoft.Extensions.Logging
    ```
1. Update `Program.cs` with the following
    ```c#
    using Microsoft.Extensions.Logging;
    using OpenTelemetry.Logs;

    public class Program
    {
        public static void Main()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options => options
                    .AddConsoleExporter());
            });

            var logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        }
    }
    ```
1. Run the application
    ```sh
    dotnet run
    ```
1. You should see the following output
    ```text
    LogRecord.TraceId:            00000000000000000000000000000000
    LogRecord.SpanId:             0000000000000000
    LogRecord.Timestamp:          2020-11-13T23:50:33.5764463Z
    LogRecord.EventId:            0
    LogRecord.CategoryName:       Program
    LogRecord.LogLevel:           Information
    LogRecord.TraceFlags:         None
    LogRecord.State:              Hello from tomato 2.99.
    ```

Congratulations! You are now collecting logs using OpenTelemetry.

<!-- TODO Can we just import the Program.cs file (skip copyright would be nice too) -->

## What does the above program do?

The program uses the [`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger) API to log a formatted string with a severity level of `Information`. Click
[here](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel)
for more information on the different logs levels. Opentelemetry captures this
and sends it to `ConsoleExporter`. `ConsoleExporter` simply displays it on the
console.
<!-- TODO extract this common explanation of what ConsoleExporter does -->
