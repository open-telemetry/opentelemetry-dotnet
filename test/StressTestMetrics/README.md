# OpenTelemetry Metrics Stress Run

Use the following example to run Stress test from command line:

Navigate to `./test/StressTestMetrics` directory and run the following command:

`dotnet run --framework netcoreapp3.1 --configuration Release`

The program shows the writes/sec on the console window title
and updates it every second.

At the end of the run, it also shows the overall writes/sec.
