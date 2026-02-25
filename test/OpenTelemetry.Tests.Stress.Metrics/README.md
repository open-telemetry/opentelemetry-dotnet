# OpenTelemetry Stress Tests for Metrics

This stress test is specifically for Metrics SDK, and is based on the
[OpenTelemetry.Tests.Stress](../OpenTelemetry.Tests.Stress/README.md).

* [Running the stress test](#running-the-stress-test)

## Running the stress test

Open a console, run the following command from the current folder:

```sh
dotnet run --framework net10.0 --configuration Release
```

To see command line options available, run the following command from the
current folder:

```sh
dotnet run --framework net10.0 --configuration Release -- --help
```

The help output includes settings and their explanations:

```text
  -t, --type             The metrics stress test type to run. Valid values: [Histogram, Counter]. Default value: Histogram.

  -m, --metrics_port     The Prometheus http listener port where Prometheus will be exposed for retrieving test metrics while the stress test is running. Set to '0' to disable.
                         Default value: 9185.

  -v, --view             Whether or not a view should be configured to filter tags for the stress test. Default value: False.

  -o, --otlp             Whether or not an OTLP exporter should be added for the stress test. Default value: False.

  -i, --interval         The OTLP exporter export interval in milliseconds. Default value: 5000.

  -e, --exemplars        Whether or not to enable exemplars for the stress test. Default value: False.

  -c, --concurrency      The concurrency (maximum degree of parallelism) for the stress test. Default value: Environment.ProcessorCount.

  -p, --internal_port    The Prometheus http listener port where Prometheus will be exposed for retrieving internal metrics while the stress test is running. Set to '0' to
                         disable. Default value: 9464.

  -d, --duration         The duration for the stress test to run in seconds. If set to '0' or a negative value the stress test will run until canceled. Default value: 0.
```
