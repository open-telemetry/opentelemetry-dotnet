# OpenTelemetry Stress Tests for Logs

This is Stress Test specifically for logging, and is
based on the [OpenTelemetry.Tests.Stress](../OpenTelemetry.Tests.Stress/README.md).

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
  -c, --concurrency      The concurrency (maximum degree of parallelism) for the stress test. Default value: Environment.ProcessorCount.

  -p, --internal_port    The Prometheus http listener port where Prometheus will be exposed for retrieving internal metrics while the stress test is running. Set to '0' to
                         disable. Default value: 9464.

  -d, --duration         The duration for the stress test to run in seconds. If set to '0' or a negative value the stress test will run until canceled. Default value: 0.
```
