# OpenTelemetry Stress Tests for Metrics

This stress test is specifically for Metrics SDK, and is based on the
[OpenTelemetry.Tests.Stress](../OpenTelemetry.Tests.Stress/README.md).

You could run the stress test either for `Counter` or for `Histogram`. This is
configurable using a command-line argument. You could provide the command-line
argument as:

- `counter` to run the stress test for `Counter`
- `histogram` to run the stress test for `Histogram`

**NOTE**: If you do not provide any command-line argument, by default, the
Stress Test would be run for `Counter`.

- [Running the stress test](#running-the-stress-test)

## Running the stress test

Open a console, run the right command from the current folder:

- To run the stress test for `Counter`, you could use _ANY_ of these two
  commands:

```sh
dotnet run --framework net6.0 --configuration Release
dotnet run --framework net6.0 --configuration Release counter 
```

- To run the stress test for `Histogram`, run the following command

```sh
dotnet run --framework net6.0 --configuration Release histogram
```
