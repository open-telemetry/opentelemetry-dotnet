# OpenTelemetry Stress Tests for Metrics

This is Stress Test specifically for the Metrics based on the
[OpenTelemetry.Tests.Stress](../OpenTelemetry.Tests.Stress/README.md).

* [Running the stress test](#running-the-stress-test)

## Running the stress test

Open a console, run the following command from the current folder:

```sh
dotnet run --framework net6.0 --configuration Release
```

Once the application started, you will see the performance number updates from
the console window title.

Use the `SPACE` key to toggle the console output, which is off by default.

Use the `ENTER` key to print the latest performance statistics.

Use the `ESC` key to exit the stress test.

<!-- markdownlint-disable MD013 -->
```text
Running (concurrency = 1), press <Esc> to stop...
2021-09-28T18:47:17.6807622Z Loops: 17,549,732,467, Loops/Second: 738,682,519, CPU Cycles/Loop: 3
2021-09-28T18:47:17.8846348Z Loops: 17,699,532,304, Loops/Second: 731,866,438, CPU Cycles/Loop: 3
2021-09-28T18:47:18.0914577Z Loops: 17,850,498,225, Loops/Second: 730,931,752, CPU Cycles/Loop: 3
2021-09-28T18:47:18.2992864Z Loops: 18,000,133,808, Loops/Second: 724,029,883, CPU Cycles/Loop: 3
2021-09-28T18:47:18.5052989Z Loops: 18,150,598,194, Loops/Second: 733,026,161, CPU Cycles/Loop: 3
2021-09-28T18:47:18.7116733Z Loops: 18,299,461,007, Loops/Second: 724,950,210, CPU Cycles/Loop: 3
```
<!-- markdownlint-enable MD013 -->

The stress test metrics are exposed via
[PrometheusExporter](../../src/OpenTelemetry.Exporter.Prometheus/README.md),
which can be accessed via
[http://localhost:9184/metrics/](http://localhost:9184/metrics/):

```text
# TYPE Process_NonpagedSystemMemorySize64 gauge
Process_NonpagedSystemMemorySize64 31651 1637385964580

# TYPE Process_PagedSystemMemorySize64 gauge
Process_PagedSystemMemorySize64 238672 1637385964580

# TYPE Process_PagedMemorySize64 gauge
Process_PagedMemorySize64 16187392 1637385964580

# TYPE Process_WorkingSet64 gauge
Process_WorkingSet64 29753344 1637385964580

# TYPE Process_VirtualMemorySize64 gauge
Process_VirtualMemorySize64 2204045848576 1637385964580
```
